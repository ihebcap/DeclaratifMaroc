using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Declaration.Application.Entities;
using Declaration.Application.Interfaces;
using Declaration.Selection;
using Declaration.Core.Model;
using Declaration.Orchestration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Declaration.Application.Services;

public class DeclarationWorkflowService
{
    private readonly IDeclarationRepository _repository;
    private readonly ISelectionExpliqueeService _selectionService;
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DeclarationWorkflowService> _logger;

    // Verrou d'écriture du fichier de log de valorisation (appels concurrents possibles).
    private static readonly object _logFileLock = new();

    // Verrou de figeage par (déclaration, domaine). Le figeage paresseux est déclenché par
    // GET /lignes, or l'écran Affectations lance N appels /lignes EN PARALLÈLE (un par règlement
    // sélectionné). Le garde « if (count > 0) return » n'étant pas atomique, ces N appels voient
    // tous count=0 simultanément et insèrent chacun le jeu complet de candidates → lignes
    // dupliquées ×N (symptôme : plusieurs lignes de TVA identiques). Ce verrou + une
    // double-vérification sous verrou rendent le figeage réellement idempotent.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _figeageLocks = new();

    /// <summary>
    /// TASK-080 : préfixe stable du motif d'exclusion inter-déclaration, utilisé à la fois pour
    /// construire le message (avec le numéro de la déclaration concurrente) et pour reconnaître,
    /// lors de la revalidation, les lignes Exclue posées par ce garde-fou (par opposition aux
    /// autres motifs d'exclusion). Toute modification doit rester cohérente entre les deux usages.
    /// </summary>
    private const string PrefixeDejaEnCoursAilleurs = "Déjà pris en compte dans la déclaration ";

    public DeclarationWorkflowService(
        IDeclarationRepository repository,
        ISelectionExpliqueeService selectionService,
        IDbConnectionFactory connectionFactory,
        IConfiguration configuration,
        ILogger<DeclarationWorkflowService> logger)
    {
        _repository = repository;
        _selectionService = selectionService;
        _connectionFactory = connectionFactory;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Journal de valorisation : écrit à la fois via ILogger ET dans un fichier du dossier de
    /// déploiement (logs/valorisation.log), pour rester lisible même en Windows Service (pas de
    /// console). Aucune erreur worker/cache n'est avalée en silence.
    /// </summary>
    private void JournaliserValorisation(string message)
    {
        _logger.LogInformation("{ValoMessage}", message);
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(dir);
            var ligne = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}";
            lock (_logFileLock)
                File.AppendAllText(Path.Combine(dir, "valorisation.log"), ligne);
        }
        catch { /* le fichier est un confort ; ILogger reste la source primaire */ }
    }

    /// <summary>
    /// TASK-073 périmètre B : journal d'audit des actions sensibles (réouverture d'une
    /// déclaration clôturée). Même mécanisme que JournaliserValorisation (ILogger + fichier
    /// dédié dans logs/), pour rester consultable même en Windows Service.
    /// </summary>
    private void JournaliserAudit(string message)
    {
        _logger.LogWarning("{AuditMessage}", message);
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(dir);
            var ligne = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}";
            lock (_logFileLock)
                File.AppendAllText(Path.Combine(dir, "audit.log"), ligne);
        }
        catch { /* le fichier est un confort ; ILogger reste la source primaire */ }
    }

    public async Task<DeclarationEntete> CreerDeclarationAsync(int societeId, int exercice, int periode, Declaration.Application.Entities.TypePeriode type)
    {
        if (await _repository.ExistsAsync(societeId, exercice, periode, type))
            throw new InvalidOperationException("Une déclaration existe déjà pour cette société, cet exercice et cette période.");

        // TASK-080 : le Numero doit distinguer le Type — sinon deux déclarations valides pour
        // la même (SocieteId, Exercice, Periode) mais un Type différent (scénario même explicitement
        // couvert par le garde-fou d'exclusivité TASK-080) produisent le MÊME Numero et
        // l'INSERT échoue sur IX_DM_ENTTVA_Numero (contrainte unique). Suffixe "-T" réservé à
        // Trimestrielle : les Numero Mensuelle existants restent inchangés (pas de migration).
        var suffixeType = type == Declaration.Application.Entities.TypePeriode.Trimestrielle ? "-T" : "";
        var numero = $"TVA{societeId}-{exercice}-{periode:D2}{suffixeType}";

        var declaration = new DeclarationEntete
        {
            Id = Guid.NewGuid(),
            Numero = numero,
            SocieteId = societeId,
            Exercice = exercice,
            Periode = periode,
            Type = type,
            Statut = StatutDeclaration.EnCours,
            DateCreation = DateTime.UtcNow
        };

        await _repository.CreateAsync(declaration);
        return declaration;
    }

    /// <summary>
    /// Charge les lignes candidates pour un domaine donné si elles n'ont pas encore été figées.
    /// Lignes éligibles → Etat = Proposee
    /// Lignes rejetées → Etat = Exclue + MotifRejet renseigné (aucun rejet silencieux)
    /// </summary>
    public async Task<IReadOnlyList<Alerte>> ChargerCandidatesSiNecessaireAsync(Guid declarationId, string domaine)
    {
        var declaration = await _repository.GetByIdAsync(declarationId);
        if (declaration == null) throw new ArgumentException("Déclaration introuvable");

        // Chemin rapide sans verrou : déjà figé — TASK-077, la revalidation légère s'exécute
        // MALGRÉ TOUT à chaque appel (rien n'est recalculé/modifié, uniquement une alerte
        // possible), SAUF sur une déclaration clôturée (verrou TASK-028/064 : aucune
        // revalidation ne doit même s'exécuter sur une déclaration figée définitivement).
        if (await _repository.GetLignesCountAsync(declarationId, domaine, null) > 0)
        {
            if (declaration.Statut == StatutDeclaration.Cloturee) return Array.Empty<Alerte>();
            return await RevaliderLignesFigeesAsync(declarationId, domaine);
        }

        // Sérialise le figeage concurrent (appels /lignes parallèles) pour cette
        // (déclaration, domaine). Sans cela, le figeage n'est pas idempotent (cf. _figeageLocks).
        var gate = _figeageLocks.GetOrAdd($"{declarationId:N}|{domaine}", _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            // Double-vérification sous verrou : un appel concurrent a pu figer pendant l'attente.
            if (await _repository.GetLignesCountAsync(declarationId, domaine, null) > 0)
            {
                if (declaration.Statut == StatutDeclaration.Cloturee) return Array.Empty<Alerte>();
                return await RevaliderLignesFigeesAsync(declarationId, domaine);
            }

            var lignes = await ConstruireLignesFigeesAsync(declarationId, declaration, domaine);
            await _repository.SaveLignesCandidatesAsync(lignes);
        }
        finally
        {
            gate.Release();
        }

        // TASK-081 : le pipeline TASK-072/076 vient d'appliquer le garde-fou et d'écrire la
        // sentinelle CodeTaxe='ERREUR' en cache le cas échéant — mais rien ne la relit avant ce
        // retour. Sans cet appel, l'alerte LIGNE_FIGEE_A_REVERIFIER (TASK-077/078) n'apparaît
        // jamais au tout premier chargement, seulement au second appel /lignes (branche « déjà
        // figé » ci-dessus). RevaliderLignesFigeesAsync relit GetLignesAsync, donc voit les
        // lignes qui viennent d'être sauvegardées par SaveLignesCandidatesAsync ci-dessus — même
        // détection, aucune règle dupliquée.
        return await RevaliderLignesFigeesAsync(declarationId, domaine);
    }

    /// <summary>
    /// Exécute le pipeline de premier figeage (sélection candidates → garde-fou d'exclusivité
    /// TASK-080 → orchestrateur Sage → mapping) pour un (déclaration, domaine) — sans persister.
    /// Extrait en méthode <c>protected virtual</c> uniquement pour permettre à un test unitaire
    /// (TASK-081) de substituer l'orchestrateur (dépendant d'un worker Sage réel, non mockable
    /// simplement) sans dupliquer la logique de <see cref="ChargerCandidatesSiNecessaireAsync"/>.
    /// </summary>
    protected virtual async Task<List<LigneCandidate>> ConstruireLignesFigeesAsync(
        Guid declarationId, DeclarationEntete declaration, string domaine)
    {
        var dateDebut = new DateTime(declaration.Exercice, declaration.Periode, 1);
        var dateFin = dateDebut.AddMonths(1).AddDays(-1);

        var grfConnectionString = _connectionFactory.GetGrfConnectionString();
        var candidates = await _selectionService.SelectionnerExpliqueeAsync(
            declaration.SocieteId, dateDebut, dateFin, grfConnectionString);

        // TASK-080 : garde-fou d'exclusivité — un règlement déjà Proposee/Integree dans une
        // AUTRE déclaration (EnCours ou Cloturee) de la même société ne doit pas devenir
        // éligible ici, sous peine de double-affectation (le verrou DT_Id/TASK-028 ne se
        // déclenche, lui, qu'à la clôture — trop tard). Appliqué APRÈS l'évaluateur (qui
        // ignore toute notion de déclaration) et AVANT la sélection des éligibles, pour ne
        // jamais remplacer un motif de rejet déjà posé (DejaDeclare, HorsPeriode, etc.).
        await AppliquerExclusiviteInterDeclarationAsync(candidates, declaration.SocieteId, declarationId);

        var orchestrateur = BuildOrchestrateur(grfConnectionString);

        var affectations = candidates.Where(c => c.EstEligible).Select(c => c.Affectation).ToList();
        var modele = orchestrateur.Traiter(affectations, 2);

        return MapLignesCandidates(declarationId, domaine, candidates, modele);
    }

    /// <summary>
    /// TASK-080 : marque DejaEnCoursAilleurs les candidats par ailleurs Eligible dont la clé
    /// (NumeroFacture, NumeroRapprochement) est déjà Proposee/Integree dans une autre déclaration
    /// de la même société. Ne touche jamais un candidat déjà rejeté par un autre motif — l'ordre
    /// des motifs de rejet (DejaDeclare, HorsPeriode, ...) reste inchangé, cette exclusion est
    /// strictement additionnelle.
    /// </summary>
    private async Task AppliquerExclusiviteInterDeclarationAsync(
        IEnumerable<AffectationCandidate> candidates, int societeId, Guid declarationId)
    {
        var conflits = await _repository.GetConflitsAutreDeclarationAsync(societeId, declarationId);
        if (conflits.Count == 0) return;

        foreach (var c in candidates)
        {
            if (c.Motif != MotifRejet.Eligible) continue;
            var cle = $"{c.Affectation.NumeroFacture}|{c.Affectation.NumeroRapprochement}";
            if (conflits.TryGetValue(cle, out var numeroAutre))
            {
                c.Motif = MotifRejet.DejaEnCoursAilleurs;
                c.ConflitDeclarationNumero = numeroAutre;
            }
        }
    }

    /// <summary>
    /// TASK-077 : revalidation légère (requêtes SQL ciblées, JAMAIS de relecture OM Sage) des
    /// lignes déjà figées dans DM_LGTVA pour ce (déclaration, domaine). Exécutée à CHAQUE appel
    /// de <see cref="ChargerCandidatesSiNecessaireAsync"/> une fois le figeage déjà en place —
    /// signale SEULEMENT, ne modifie JAMAIS une ligne ni un total :
    ///   (a) incohérence Sage détectée après le figeage : sentinelle CodeTaxe='ERREUR' en cache
    ///       (<see cref="IDeclarationRepository.GetEcIdsEnErreurAsync"/>, réutilise le motif déjà
    ///       posé par TASK-072/076, ne duplique pas la règle de détection) ;
    ///   (b) règlement dépointé depuis le figeage : MV_Point courant ≠ Point_Oui sur le MV_Id
    ///       snapshoté par la ligne (<see cref="IDeclarationRepository.GetMvPointsActuelsAsync"/>).
    /// Les lignes figées avant ce correctif (EC_Id=0/MV_Id=0, cf. migration 006) sont ignorées :
    /// aucune clé connue, donc rien à vérifier — jamais un faux signalement.
    /// N'est jamais appelée sur une déclaration clôturée (garde posée par l'appelant).
    /// </summary>
    public async Task<IReadOnlyList<Alerte>> RevaliderLignesFigeesAsync(Guid declarationId, string domaine)
    {
        var alertes = new List<Alerte>();

        var toutesLignes = (await _repository.GetLignesAsync(declarationId, domaine, 1, int.MaxValue, null, null)).ToList();
        var lignes = toutesLignes.Where(l => l.Etat == EtatLigne.Proposee || l.Etat == EtatLigne.Integree).ToList();

        // TASK-080 : réconciliation des lignes Exclue pour conflit inter-déclaration — indépendant
        // de la présence de lignes Proposee/Integree (une déclaration peut n'avoir QUE des lignes
        // exclues pour ce motif). Fait AVANT le retour anticipé ci-dessous.
        var declarationPourReintegration = await _repository.GetByIdAsync(declarationId);
        if (declarationPourReintegration != null)
        {
            var lignesExcluesConflit = toutesLignes
                .Where(l => l.Etat == EtatLigne.Exclue && l.MotifRejet.StartsWith(PrefixeDejaEnCoursAilleurs, StringComparison.Ordinal))
                .ToList();
            if (lignesExcluesConflit.Count > 0)
            {
                var conflitsActuels = await _repository.GetConflitsAutreDeclarationAsync(declarationPourReintegration.SocieteId, declarationId);
                var resolues = lignesExcluesConflit
                    .Where(l => !conflitsActuels.ContainsKey($"{l.NumeroFacture}|{l.NumeroRapprochement}"))
                    .ToList();
                if (resolues.Count > 0)
                {
                    await ReintegrerReglementsLiberesAsync(declarationId, domaine, declarationPourReintegration, resolues);
                    alertes.Add(new Alerte
                    {
                        Niveau = NiveauAlerte.Info,
                        Code = "REGLEMENT_LIBERE_REINTEGRE",
                        Message = $"{resolues.Count} règlement(s) précédemment exclu(s) (déjà pris en compte ailleurs) ont été réintégrés — le conflit avec l'autre déclaration n'existe plus.",
                        RefLigne = "Global"
                    });
                }
            }
        }

        // TASK-082 : une ligne Exclue DÈS LE FIGEAGE (détection TASK-072/076 pendant la lecture du
        // cache, avant même de devenir Proposee — cas réel FC2501717/EC_Id=21473) porte un EC_Id
        // connu dès sa création (MapLignesCandidates) mais était totalement absente de `lignes`
        // ci-dessus : jamais revalidée, donc le badge « ligne à revérifier » de l'écran ②
        // (Affectations) restait silencieux, alors que Synthèse/Contrôle affiche déjà le même
        // motif via l'alerte indépendante LIGNE_EXCLUE (GetCheckupAsync). Ne change AUCUN
        // état/valeur — ajoute seulement ces lignes au contrôle d'incohérence ci-dessous.
        var lignesExcluesIncoherence = toutesLignes
            .Where(l => l.Etat == EtatLigne.Exclue && l.EC_Id > 0 && !l.IncoherenceValidee)
            .ToList();

        if (lignes.Count == 0 && lignesExcluesIncoherence.Count == 0) return alertes;

        // TASK-077 (suite) : auto-guérison des lignes figées AVANT la migration 006 (EC_Id/MV_Id
        // = 0, valeur inconnue à l'origine) — sans ce backfill, ces lignes ne sont JAMAIS
        // revalidées et le PO devrait rouvrir/refiger manuellement la déclaration pour que la
        // revalidation s'applique (contraire à la demande PO : signalement automatique, sans
        // action manuelle). Reconstruit les clés via la MÊME sélection SQL lecture seule que le
        // figeage (jamais de relecture OM Sage), appariée par (NumeroFacture, NumeroRapprochement).
        var aBackfiller = lignes.Where(l => l.EC_Id <= 0 || l.MV_Id <= 0).ToList();
        if (aBackfiller.Count > 0)
        {
            try
            {
                var declaration = declarationPourReintegration;
                if (declaration != null && declaration.Exercice > 0 && declaration.Periode > 0)
                {
                    var dateDebut = new DateTime(declaration.Exercice, declaration.Periode, 1);
                    var dateFin = dateDebut.AddMonths(1).AddDays(-1);
                    var grfConnectionString = _connectionFactory.GetGrfConnectionString();
                    var candidats = await _selectionService.SelectionnerExpliqueeAsync(
                        declaration.SocieteId, dateDebut, dateFin, grfConnectionString);

                    var parCle = candidats
                        .Where(c => c.Affectation.EC_Id > 0)
                        .GroupBy(c => (c.Affectation.NumeroFacture, c.Affectation.NumeroRapprochement))
                        .ToDictionary(g => g.Key, g => g.First().Affectation);

                    foreach (var l in aBackfiller)
                    {
                        if (parCle.TryGetValue((l.NumeroFacture, l.NumeroRapprochement), out var affectation))
                        {
                            l.EC_Id = affectation.EC_Id;
                            l.MV_Id = affectation.MV_Id;
                            await _repository.UpdateLigneClesAsync(l.Id, affectation.EC_Id, affectation.MV_Id);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Best-effort : l'auto-guérison ne doit jamais faire échouer le checkup/figeage.
                JournaliserValorisation($"[VALO-077] déclaration {declarationId} — échec backfill EC_Id/MV_Id : {ex.Message}");
            }
        }

        var ecIds = lignes.Concat(lignesExcluesIncoherence)
            .Where(l => l.EC_Id > 0).Select(l => l.EC_Id).Distinct().ToList();
        var ecIdsEnErreur = await _repository.GetEcIdsEnErreurAsync(ecIds);

        var mvIds = lignes.Where(l => l.MV_Id > 0).Select(l => l.MV_Id).Distinct().ToList();
        var mvPointsActuels = await _repository.GetMvPointsActuelsAsync(mvIds);

        foreach (var l in lignes)
        {
            // TASK-078 : une incohérence déjà VALIDÉE explicitement par le PO (traçabilité,
            // qui/quand) n'est plus resignalée à chaque chargement — la ligne/les totaux restent
            // inchangés dans tous les cas, seule la répétition du signalement est supprimée.
            if (l.IncoherenceValidee) continue;

            if (l.EC_Id > 0 && ecIdsEnErreur.Contains(l.EC_Id))
            {
                alertes.Add(new Alerte
                {
                    Niveau = NiveauAlerte.Warning,
                    Code = "LIGNE_FIGEE_A_REVERIFIER",
                    Message = $"Ligne déjà figée (facture {l.NumeroFacture}, EC_Id={l.EC_Id}) : incohérence Sage HT/TVA/TTC détectée APRÈS le figeage — vérification manuelle requise avant clôture. Ligne et totaux inchangés (aucun recalcul automatique).",
                    RefLigne = l.NumeroFacture
                });
            }

            if (l.MV_Id > 0)
            {
                var pointConnu = mvPointsActuels.TryGetValue(l.MV_Id, out var p) ? p : null;
                if (pointConnu != Declaration.Selection.GrfEnums.Point_Oui)
                {
                    alertes.Add(new Alerte
                    {
                        Niveau = NiveauAlerte.Warning,
                        Code = "LIGNE_FIGEE_A_REVERIFIER",
                        Message = $"Ligne déjà figée (facture {l.NumeroFacture}, règlement {l.NumeroRapprochement}, MV_Id={l.MV_Id}) : règlement dépointé depuis le figeage — vérification manuelle requise avant clôture. Ligne et totaux inchangés (aucun recalcul automatique).",
                        RefLigne = l.NumeroFacture
                    });
                }
            }
        }

        // TASK-082 : même contrôle d'incohérence que ci-dessus, pour les lignes Exclue dès le
        // figeage (cf. lignesExcluesIncoherence). Pas de contrôle MV_Point ici : une ligne Exclue
        // n'a jamais été valorisée/déclarée, donc « règlement dépointé depuis le figeage » n'a pas
        // de sens pour elle — seul le motif d'incohérence Sage doit être resignalé (parité avec
        // l'alerte LIGNE_EXCLUE déjà visible en Synthèse/Contrôle).
        foreach (var l in lignesExcluesIncoherence)
        {
            if (ecIdsEnErreur.Contains(l.EC_Id))
            {
                alertes.Add(new Alerte
                {
                    Niveau = NiveauAlerte.Warning,
                    Code = "LIGNE_FIGEE_A_REVERIFIER",
                    Message = $"Facture {l.NumeroFacture} exclue de la valorisation (EC_Id={l.EC_Id}) : incohérence Sage HT/TVA/TTC détectée — vérification manuelle requise avant clôture. Ligne non valorisée, aucune valeur déclarée pour cette pièce.",
                    RefLigne = l.NumeroFacture
                });
            }
        }

        return alertes;
    }

    /// <summary>
    /// TASK-080 : réintègre des règlements précédemment exclus (DejaEnCoursAilleurs) dont le
    /// conflit n'existe plus (autre déclaration supprimée — TASK-079 — ou ligne concurrente
    /// devenue Exclue/Reportee/Ecartee). Reproduit EXACTEMENT le pipeline du figeage initial
    /// (SelectionnerExpliqueeAsync → ré-application du garde-fou d'exclusivité, au cas où un
    /// AUTRE conflit serait apparu entre-temps → orchestrateur → MapLignesCandidates), plutôt
    /// que de rebasculer l'état de la ligne existante : les lignes Exclue ne portent aucune
    /// valorisation (HT/Taux/TVA/TTC = 0), il faut donc revaloriser, pas seulement changer l'état.
    /// </summary>
    private async Task ReintegrerReglementsLiberesAsync(
        Guid declarationId, string domaine, DeclarationEntete declaration, List<LigneCandidate> lignesLiberees)
    {
        if (declaration.Exercice <= 0 || declaration.Periode <= 0) return;

        var clesLiberees = lignesLiberees
            .Select(l => (l.NumeroFacture, l.NumeroRapprochement))
            .ToHashSet();

        var dateDebut = new DateTime(declaration.Exercice, declaration.Periode, 1);
        var dateFin = dateDebut.AddMonths(1).AddDays(-1);
        var grfConnectionString = _connectionFactory.GetGrfConnectionString();
        var candidats = await _selectionService.SelectionnerExpliqueeAsync(
            declaration.SocieteId, dateDebut, dateFin, grfConnectionString);

        var aReintegrer = candidats
            .Where(c => clesLiberees.Contains((c.Affectation.NumeroFacture, c.Affectation.NumeroRapprochement)))
            .ToList();
        if (aReintegrer.Count == 0) return;

        await AppliquerExclusiviteInterDeclarationAsync(aReintegrer, declaration.SocieteId, declarationId);

        var orchestrateur = BuildOrchestrateur(grfConnectionString);
        var affectationsEligibles = aReintegrer.Where(c => c.EstEligible).Select(c => c.Affectation).ToList();
        var modele = orchestrateur.Traiter(affectationsEligibles, 2);

        var nouvellesLignes = MapLignesCandidates(declarationId, domaine, aReintegrer, modele);

        await _repository.DeleteLignesAsync(lignesLiberees.Select(l => l.Id));
        await _repository.SaveLignesCandidatesAsync(nouvellesLignes);
    }

    /// <summary>
    /// TASK-078 : trace la décision explicite du PO de VALIDER une incohérence déjà signalée
    /// (TASK-077) — accepte l'état en connaissance de cause. Ne modifie AUCUNE ligne ni total ;
    /// supprime seulement la répétition du signalement aux prochains chargements.
    /// </summary>
    public Task ValiderIncoherenceLigneAsync(Guid declarationId, int ecId, string utilisateur) =>
        _repository.ValiderIncoherenceAsync(declarationId, ecId, utilisateur);

    /// <summary>
    /// TASK-078 : resynchronise UNE pièce (EC_Id) après correction côté Sage — relit
    /// explicitement l'OM pour cette seule facture (effet de bord : réécrit
    /// GRC_VENTILATION_SAGE_CACHE via l'orchestrateur, même pipeline que TASK-072/076/077,
    /// aucune règle dupliquée) et réinitialise une éventuelle validation antérieure (les faits
    /// ont changé). Retourne (trouvee, resolue) : resolue=true si la pièce n'est plus en erreur
    /// après relecture.
    /// </summary>
    public async Task<(bool Trouvee, bool Resolue)> ResynchroniserLigneAsync(Guid declarationId, int ecId)
    {
        var lignes = (await _repository.GetLignesAsync(declarationId, "Decaissement", 1, int.MaxValue, null, null))
            .Where(l => l.EC_Id == ecId)
            .ToList();
        if (lignes.Count == 0) return (false, false);
        var ligne = lignes[0];

        var declaration = await _repository.GetByIdAsync(declarationId);
        if (declaration == null) return (false, false);

        var grfConnectionString = _connectionFactory.GetGrfConnectionString();
        var orchestrateur = BuildOrchestrateur(grfConnectionString);

        // Purge d'abord la ligne de cache existante : sans ça, une sentinelle ERREUR portant déjà
        // des montants bruts capturés (TASK-076) ferait considérer TryServireDepuisCache la pièce
        // comme définitivement réglée et ne relirait jamais Sage — même après correction ERP,
        // « Resynchroniser » n'aurait alors aucun effet réel.
        var persistenceCs = _configuration.GetConnectionString("PersistenceConnection") ?? "";
        new VentilationSageCacheRepository().SupprimerEntrees(ecId, persistenceCs);

        var affectation = new AffectationADeclarer
        {
            NumeroFacture = ligne.NumeroFacture,
            NumeroRapprochement = ligne.NumeroRapprochement,
            Sens = SensAffectation.Achat,
            EC_Type = ligne.EcType,
            EC_Id = ligne.EC_Id,
            MV_Id = ligne.MV_Id,
        };

        // Effet de bord voulu : relit Sage pour cette pièce (cache désormais vide, donc traité
        // comme cache miss) et réécrit la ligne de cache (normale si désormais cohérente,
        // sentinelle ERREUR sinon) — même pipeline que le reste du garde-fou, aucune règle de
        // détection dupliquée ici.
        orchestrateur.Traiter(new[] { affectation }, 1);

        await _repository.ReinitialiserValidationIncoherenceAsync(declarationId, ecId);

        var toujoursEnErreur = await _repository.GetEcIdsEnErreurAsync(new[] { ecId });
        return (true, !toujoursEnErreur.Contains(ecId));
    }

    /// <summary>
    /// Rafraîchit la valorisation TVA (famille B) pour une période bornée, sans déclaration.
    /// Sélectionne les factures éligibles de la période, lit les OM Sage et remplit
    /// GRC_VENTILATION_SAGE_CACHE (effet de bord de l'orchestrateur). Aucune ligne de déclaration
    /// écrite, aucun DT_Id touché. Utilisé par le bouton « Rafraîchir » de l'écran Factures.
    /// Retourne le nombre de factures éligibles traitées.
    /// </summary>
    public async Task<RapportValorisation> RafraichirValorisationAsync(int soId, DateTime dateDebut, DateTime dateFin)
    {
        var grfConnectionString = _connectionFactory.GetGrfConnectionString();

        // TASK-050 Partie B — Valorisation facture-first : on lit TOUTES les factures EC_Type=0
        // de la période depuis RT_ECHEANCE, indépendamment du statut de règlement/rapprochement.
        // Avant TASK-050, on partait de RT_MOUVEMENT avec filtre MV_DECAISSE → 465+ factures
        // réglées par chèque (MV_Type=1, MV_DECAISSE=0) étaient silencieusement exclues.
        // La DÉCLARATION reste strictement gated sur EstEligible (RecalculerLignesCandidatesAsync).
        var toutesFactures = await _selectionService.LireFacturesDepuisPeriodeAsync(
            soId, dateDebut, dateFin, grfConnectionString);

        // Valorisation d'affichage : inclut Eligible, NonRapproche (TASK-049) et NonAffecte (TASK-052).
        // - Eligible     → règlement rapproché dans la période, non déclaré (EstValorisable=true)
        // - NonRapproche → affecté mais non rapproché : cache token NULL, jamais servi déclarable
        // - NonAffecte   → aucun règlement affecté : cache token NULL, jamais servi déclarable
        // Les autres motifs (HorsPeriode, DejaDeclare, etc.) ne sont pas valorisés (EstValorisable=false).
        var affectations = toutesFactures.Where(c => c.EstValorisable).Select(c => c.Affectation).ToList();
        var totalFacturesLues = toutesFactures.Count();

        JournaliserValorisation($"[VALO-050] === Rafraîchissement facture-first soId={soId} {dateDebut:yyyy-MM-dd}→{dateFin:yyyy-MM-dd} : "
            + $"{totalFacturesLues} facture(s) lues depuis RT_ECHEANCE, "
            + $"{affectations.Count} valorisable(s) (éligibles + non rapprochées + non affectées) ===");

        var orchestrateur = BuildOrchestrateur(grfConnectionString);
        var modele = orchestrateur.Traiter(affectations, 2); // effet de bord : écriture du cache

        // Rapport transparent : les alertes de l'orchestrateur portent le motif exact par facture.
        var erreurs = modele.Alertes
            .Where(a => a.Niveau == Declaration.Core.Model.NiveauAlerte.Error)
            .Select(a => new MotifValorisation(a.Code, a.Message, a.RefLigne))
            .ToList();

        JournaliserValorisation($"[VALO-050] === Fin : {affectations.Count} traitée(s), {erreurs.Count} en erreur ===");

        return new RapportValorisation(affectations.Count, erreurs);
    }


    /// <summary>
    /// Construit l'orchestrateur de déclaration avec la config worker Sage et la connexion de
    /// persistance (cache TVA). Factorisé pour être partagé entre le chargement des candidates
    /// et le rafraîchissement autonome de la valorisation.
    /// </summary>
    private OrchestrateurDeclaration BuildOrchestrateur(string grfConnectionString)
    {
        var sageCs = _configuration.GetConnectionString("SageConnection") ?? "";
        var builder = new System.Data.SqlClient.SqlConnectionStringBuilder(sageCs);
        var workerExe = _configuration.GetSection("WorkerConfig")?["WorkerExePath"] ?? "SageTaxReader.Console.exe";

        // Déploiement mono-dossier : un chemin worker relatif est résolu à côté de l'exe de l'API.
        if (!Path.IsPathRooted(workerExe))
            workerExe = Path.Combine(AppContext.BaseDirectory, workerExe);

        // Identifiants de l'utilisateur APPLICATIF Sage (Objets Métier), distincts du login SQL.
        // Le worker les mappe sur Loggable.UserName/UserPwd pour ouvrir la base commerciale.
        // À défaut de section SageOM, on retombe sur le login SQL (comportement historique).
        var sageOmUser = _configuration.GetSection("SageOM")?["User"];
        var sageOmPwd = _configuration.GetSection("SageOM")?["Password"];

        var workerConfig = new WorkerConfig
        {
            Server = builder.DataSource,
            Database = builder.InitialCatalog,
            User = string.IsNullOrEmpty(sageOmUser) ? builder.UserID : sageOmUser,
            Password = string.IsNullOrEmpty(sageOmPwd) ? builder.Password : sageOmPwd,
            WorkerExePath = workerExe
        };
        var invoker = new WorkerInvoker();
        var lecteurFgr = new Declaration.Orchestration.LecteurTvaFgr();
        var persistenceCs = _configuration.GetConnectionString("PersistenceConnection") ?? "";
        return new OrchestrateurDeclaration(
            invoker, workerConfig, lecteurFgr, grfConnectionString, sageCs,
            ventilationCache: null, persistenceConnectionString: persistenceCs,
            log: JournaliserValorisation);
    }

    public static List<LigneCandidate> MapLignesCandidates(Guid declarationId, string domaine, IEnumerable<AffectationCandidate> candidates, DeclarationModele modele)
    {
        var lignes = new List<LigneCandidate>();

        foreach (var c in candidates)
        {
            if (!c.EstEligible)
            {
                var etat = c.Motif == MotifRejet.NonRapproche ? EtatLigne.Reportee : EtatLigne.Exclue;
                // TASK-080 : message précis (numéro de la déclaration concurrente) plutôt que le
                // libellé générique — la revalidation (RevaliderLignesFigeesAsync) reconnaît aussi
                // ces lignes via PrefixeDejaEnCoursAilleurs, donc ce préfixe doit rester exact.
                var motifTexte = c.Motif == MotifRejet.DejaEnCoursAilleurs && !string.IsNullOrEmpty(c.ConflitDeclarationNumero)
                    ? $"{PrefixeDejaEnCoursAilleurs}{c.ConflitDeclarationNumero}"
                    : c.MotifLibelle;
                lignes.Add(new LigneCandidate
                {
                    Id = Guid.NewGuid(),
                    DeclarationId = declarationId,
                    Etat = etat,
                    Domaine = domaine,
                    MotifRejet = motifTexte,
                    NumeroFacture = c.Affectation.NumeroFacture,
                    NumeroRapprochement = c.Affectation.NumeroRapprochement,
                    TiersNom = c.Affectation.Tiers.Nom,
                    TiersIdentifiantFiscal = c.Affectation.Tiers.IdentifiantFiscal,
                    TiersICE = c.Affectation.Tiers.Ice,
                    HT = c.Affectation.MontantAffecte,
                    Taux = 0,
                    TVA = 0,
                    TTC = 0,
                    Prorata = 0, // non valorisé — motif explicite ci-dessus, jamais un prorata muet
                    MontantAffecte = c.Affectation.MontantAffecte,
                    ModePaiement = c.Affectation.ModePaiement,
                    DatePaiement = c.Affectation.DatePaiement,
                    DateFacture = c.Affectation.DateFacture,
                    Source = c.Affectation.Source.ToString(),
                    EcType = c.Affectation.EC_Type,
                    EC_Id = c.Affectation.EC_Id,
                    MV_Id = c.Affectation.MV_Id
                });
            }
            else
            {
                var taxesLines = modele.Lignes.Where(l => l.NumeroFacture == c.Affectation.NumeroFacture).ToList();
                if (!taxesLines.Any())
                {
                    var alerte = modele.Alertes.FirstOrDefault(a => a.RefLigne.Contains(c.Affectation.NumeroFacture));
                    string motif = alerte != null ? alerte.Message : "Facture introuvable ou non ventilée";

                    lignes.Add(new LigneCandidate
                    {
                        Id = Guid.NewGuid(),
                        DeclarationId = declarationId,
                        Etat = EtatLigne.Exclue,
                        Domaine = domaine,
                        MotifRejet = motif,
                        NumeroFacture = c.Affectation.NumeroFacture,
                        NumeroRapprochement = c.Affectation.NumeroRapprochement,
                        TiersNom = c.Affectation.Tiers.Nom,
                        TiersIdentifiantFiscal = c.Affectation.Tiers.IdentifiantFiscal,
                        TiersICE = c.Affectation.Tiers.Ice,
                        HT = c.Affectation.MontantAffecte,
                        Taux = 0,
                        TVA = 0,
                        TTC = 0,
                        Prorata = 0, // non valorisé — motif explicite ci-dessus, jamais un prorata muet
                        MontantAffecte = c.Affectation.MontantAffecte,
                        ModePaiement = c.Affectation.ModePaiement,
                        DatePaiement = c.Affectation.DatePaiement,
                        DateFacture = c.Affectation.DateFacture,
                        Source = c.Affectation.Source.ToString(),
                        EcType = c.Affectation.EC_Type,
                        EC_Id = c.Affectation.EC_Id,
                        MV_Id = c.Affectation.MV_Id
                    });
                }
                else
                {
                    foreach (var tl in taxesLines)
                    {
                        lignes.Add(new LigneCandidate
                        {
                            Id = Guid.NewGuid(),
                            DeclarationId = declarationId,
                            Etat = EtatLigne.Proposee,
                            Domaine = domaine,
                            MotifRejet = "",
                            NumeroFacture = c.Affectation.NumeroFacture,
                            NumeroRapprochement = c.Affectation.NumeroRapprochement,
                            TiersNom = c.Affectation.Tiers.Nom,
                            TiersIdentifiantFiscal = c.Affectation.Tiers.IdentifiantFiscal,
                            TiersICE = c.Affectation.Tiers.Ice,
                            HT = tl.HT,
                            Taux = tl.Taux,
                            TVA = tl.Tva,
                            TTC = tl.Ttc,
                            Prorata = tl.Prorata,
                            MontantAffecte = c.Affectation.MontantAffecte,
                            ModePaiement = c.Affectation.ModePaiement,
                            DatePaiement = c.Affectation.DatePaiement,
                            DateFacture = c.Affectation.DateFacture,
                            Source = c.Affectation.Source.ToString(),
                            EcType = c.Affectation.EC_Type,
                            EC_Id = c.Affectation.EC_Id,
                            MV_Id = c.Affectation.MV_Id
                        });
                    }
                }
            }
        }

        return lignes;
    }

    /// <summary>
    /// Vérifie la cohérence de la déclaration.
    /// - Alertes Error bloquent la clôture.
    /// - ControleEquilibre : compare la somme des TTC intégrés vs somme des HT intégrés (delta TVA).
    ///   Un résidu > 5% des lignes déclenche une alerte Warning.
    /// </summary>
    public async Task<DeclarationModele> GetCheckupAsync(Guid declarationId)
    {
        var declaration = await _repository.GetByIdAsync(declarationId);
        if (declaration == null) throw new ArgumentException("Déclaration introuvable");

        var model = new DeclarationModele
        {
            EnTete = new EnTeteDeclaration
            {
                IdentifiantSociete = declaration.SocieteId.ToString(),
                Exercice = declaration.Exercice,
                Type = declaration.Type == Declaration.Application.Entities.TypePeriode.Mensuelle
                    ? Core.Model.TypePeriode.Mensuelle
                    : Core.Model.TypePeriode.Trimestrielle,
                Numero = declaration.Numero
            }
        };

        // Récupère toutes les lignes (tous domaines confondus)
        var toutes = await _repository.GetLignesAsync(declarationId, "Decaissement", 1, int.MaxValue, null, null);
        // TASK-071 : les lignes Proposee sont les lignes valorisées éligibles qui seront posées
        // Integree par CloturerDeclarationAsync avant figeage — le checkup (pré-flight ④ ou
        // interne à la clôture) doit les évaluer comme telles, sinon l'alerte AUCUNE_LIGNE_INTEGREE
        // et le contrôle d'équilibre resteraient bloquants avant même la transition physique,
        // reproduisant le verrou constaté (cas PO 68 règlements / 245 lignes).
        var integrees = toutes.Where(l => l.Etat == EtatLigne.Integree || l.Etat == EtatLigne.Proposee).ToList();
        var exclues   = toutes.Where(l => l.Etat == EtatLigne.Exclue).ToList();

        // Contrôle d'équilibre : TotalMontantAffecte (HT) vs TotalDeclareTtc (TTC), sur les
        // lignes intégrées ou éligibles à l'intégration (Proposee)
        decimal totalHT  = integrees.Sum(l => l.HT);
        decimal totalTtc = integrees.Sum(l => l.TTC);

        model.ControleEquilibre = new ControleEquilibre
        {
            TotalMontantAffecte = totalHT,
            TotalDeclareTtc = totalTtc,
            ResiduExplique = 0m // La TVA représente la différence expliquée
        };

        // Alertes : aucune ligne intégrée ni éligible (Proposee) = bloquant
        if (!integrees.Any())
        {
            model.Alertes.Add(new Alerte
            {
                Niveau = NiveauAlerte.Error,
                Code = "AUCUNE_LIGNE_INTEGREE",
                Message = "Aucune ligne n'a ete integree. La declaration est vide et ne peut pas etre cloturee.",
                RefLigne = "Global"
            });
        }

        // Info : recap des lignes exclues avec motifs
        foreach (var e in exclues)
        {
            model.Alertes.Add(new Alerte
            {
                Niveau = NiveauAlerte.Info,
                Code = "LIGNE_EXCLUE",
                Message = $"Ligne exclue : {e.MotifRejet}",
                RefLigne = e.NumeroFacture
            });
        }

        // Contrôle ICE manquant sur les lignes intégrées ou éligibles
        foreach (var l in integrees.Where(l => string.IsNullOrWhiteSpace(l.TiersICE)))
        {
            model.Alertes.Add(new Alerte
            {
                Niveau = NiveauAlerte.Error,
                Code = "TIERS_SANS_ICE",
                Message = "Ligne intégrée avec tiers sans ICE.",
                RefLigne = l.NumeroFacture
            });
        }

        // TASK-077 : remonte au checkup pré-intégration (étape ④) les avertissements de
        // revalidation des lignes déjà figées (incohérence Sage / règlement dépointé APRÈS
        // le figeage) — sinon ils restent invisibles à l'étape ④ alors qu'ils polluent déjà
        // les totaux ③ (cas réel : montant gonflé non signalé avant confirmation d'intégration).
        // Niveau Warning (non bloquant, décision PO) : n'empêche jamais la clôture.
        if (declaration.Statut != Declaration.Application.Entities.StatutDeclaration.Cloturee)
        {
            var alertesRevalidation = await RevaliderLignesFigeesAsync(declarationId, "Decaissement");
            model.Alertes.AddRange(alertesRevalidation);
        }

        return model;
    }

    public async Task CloturerDeclarationAsync(Guid declarationId)
    {
        var declaration = await _repository.GetByIdAsync(declarationId);
        if (declaration == null) throw new ArgumentException("Déclaration introuvable");

        if (declaration.Statut != StatutDeclaration.EnCours)
            throw new InvalidOperationException("Seule une déclaration EnCours peut être clôturée.");

        // Le checkup évalue déjà les lignes Proposee comme éligibles (TASK-071) : les contrôles
        // bloquants sont donc validés AVANT toute écriture, aucun état "Integree" n'est posé
        // si la clôture doit échouer (pas de dette d'état à compenser).
        var checkup = await GetCheckupAsync(declarationId);
        var bloquantes = checkup.Alertes.Where(a => a.Niveau == NiveauAlerte.Error).ToList();
        if (bloquantes.Any())
        {
            var msgs = string.Join("; ", bloquantes.Select(a => $"[{a.Code}] {a.Message} ({a.RefLigne})"));
            throw new InvalidOperationException($"Clôture refusée — anomalies bloquantes : {msgs}");
        }

        // ── TASK-071 : pose Integree sur les lignes valorisées Proposee — seul chemin du tunnel
        // actuel (①→⑥) capable d'atteindre cet état ; ⑤/DomainGrid reste verrouillé par ④.
        // Réutilise UpdateLignesEtatBulkByIdsAsync (aucune logique de transition dupliquée).
        var toutes = await _repository.GetLignesAsync(declarationId, "Decaissement", 1, int.MaxValue, null, null);
        var aIntegrer = toutes.Where(l => l.Etat == EtatLigne.Proposee).Select(l => l.Id).ToList();
        if (aIntegrer.Any())
            await _repository.UpdateLignesEtatBulkByIdsAsync(aIntegrer, EtatLigne.Integree);

        await _repository.UpdateStatutAsync(declarationId, StatutDeclaration.Cloturee);

        // ── TASK-028 : pose le tampon DT_Id sur les affectations intégrées ──────────────
        // On dérive un identifiant stable et toujours positif à partir du hashcode
        // de l'Id guid (voir DeriveDtId : masque bit-à-bit, pas de Math.Abs sujet à
        // OverflowException sur int.MinValue).
        var dtId = DeriveDtId(declaration.Id);

        var numerosRapprochement = await GetNumerosRapprochementIntegresAsync(declarationId);

        if (numerosRapprochement.Any())
            await _repository.TamponnerAffectationsAsync(dtId, numerosRapprochement);
    }

    /// <summary>
    /// Réouvre une déclaration clôturée : remet le statut à EnCours et efface
    /// le tampon DT_Id sur toutes les affectations associées (transition valeur → NULL
    /// autorisée par le trigger). L'affectation redevient sélectionnable et modifiable.
    /// TASK-073 périmètre B : journalise qui a rouvert, quand et quelle déclaration —
    /// même mécanisme (ILogger + fichier logs/) que JournaliserValorisation, aucune
    /// nouvelle table introduite (aucun équivalent n'existait déjà pour la clôture).
    /// </summary>
    public async Task ReouvriDeclarationAsync(Guid declarationId, string utilisateur)
    {
        var declaration = await _repository.GetByIdAsync(declarationId);
        if (declaration == null) throw new ArgumentException("Déclaration introuvable");

        if (declaration.Statut != StatutDeclaration.Cloturee)
            throw new InvalidOperationException("Seule une déclaration Clôturée peut être rouverte.");

        // Détamponner en premier : le trigger autorise DT_Id valeur → NULL.
        // ── TASK-028 (correctif intégrité) : on borne le dé-tamponnage aux SEULS
        // mouvements de CETTE déclaration (mêmes numéros de rapprochement que la pose).
        // Sans cela, deux déclarations dont le dtId dérivé collisionne partageraient la
        // clause « WHERE DT_Id = @dtId » et rouvrir l'une libérerait silencieusement le
        // verrou de l'autre — précisément le trou d'intégrité que TASK-028 doit fermer.
        var dtId = DeriveDtId(declaration.Id);
        var numerosRapprochement = await GetNumerosRapprochementIntegresAsync(declarationId);
        if (numerosRapprochement.Any())
            await _repository.DetamponnerAffectationsAsync(dtId, numerosRapprochement);

        await _repository.UpdateStatutAsync(declarationId, StatutDeclaration.EnCours);

        JournaliserAudit($"[REOUVERTURE] Déclaration {declaration.Numero} ({declarationId}) rouverte par '{utilisateur}'.");
    }

    /// <summary>
    /// TASK-079 : suppression physique d'une déclaration EnCours (jamais clôturée, jamais
    /// tamponnée DT_Id) — supprime l'entête DM_ENTTVA et ses lignes DM_LGTVA. Une déclaration
    /// Cloturee ne se supprime pas par ce chemin : elle doit d'abord être rouverte (TASK-073),
    /// pour ne pas contourner la traçabilité de la réouverture par un raccourci de suppression.
    /// </summary>
    public async Task SupprimerDeclarationAsync(Guid declarationId, string utilisateur)
    {
        var declaration = await _repository.GetByIdAsync(declarationId);
        if (declaration == null) throw new ArgumentException("Déclaration introuvable");

        if (declaration.Statut != StatutDeclaration.EnCours)
            throw new InvalidOperationException("Seule une déclaration EnCours peut être supprimée — une déclaration clôturée doit d'abord être rouverte (TASK-073).");

        // Garde-fou défensif (TASK-079 §4) : vérifié, pas supposé — aucune affectation ne doit
        // être tamponnée avant clôture (TASK-028), mais on le confirme avant toute suppression
        // physique plutôt que de faire confiance à l'invariant théorique.
        var numerosRapprochement = await GetNumerosRapprochementDeclarationAsync(declarationId);
        if (numerosRapprochement.Any())
        {
            var nbTamponnees = await _repository.CountAffectationsTamponneesAsync(numerosRapprochement);
            if (nbTamponnees > 0)
                throw new InvalidOperationException(
                    $"Suppression refusée — {nbTamponnees} affectation(s) déjà tamponnée(s) (DT_Id) détectée(s) sur cette déclaration EnCours : incohérence à investiguer avant suppression.");
        }

        await _repository.DeleteAsync(declarationId);

        JournaliserAudit($"[SUPPRESSION] Déclaration {declaration.Numero} ({declarationId}) supprimée par '{utilisateur}'.");
    }

    /// <summary>
    /// Numéros de rapprochement distincts de TOUTES les lignes de la déclaration (tout état
    /// confondu) — utilisé par le garde-fou défensif de suppression (TASK-079), contrairement à
    /// <see cref="GetNumerosRapprochementIntegresAsync"/> qui ne porte que sur les lignes Integree
    /// (clôture).
    /// </summary>
    private async Task<List<string>> GetNumerosRapprochementDeclarationAsync(Guid declarationId)
    {
        var lignes = await _repository.GetLignesAsync(
            declarationId, "Decaissement", 1, int.MaxValue, null, null);
        return lignes
            .Where(l => !string.IsNullOrWhiteSpace(l.NumeroRapprochement))
            .Select(l => l.NumeroRapprochement)
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Dérive un identifiant de déclaration (DT_Id) stable et toujours positif à partir
    /// de l'Id guid. Utilise un masque bit-à-bit (et non Math.Abs, qui lève
    /// OverflowException sur int.MinValue). Reste un dérivé de hashcode (collisions
    /// théoriquement possibles) : c'est pourquoi la pose ET le retrait du tampon sont
    /// toujours bornés aux numéros de rapprochement de la déclaration concernée.
    /// </summary>
    private static int DeriveDtId(Guid id) => id.GetHashCode() & int.MaxValue;

    /// <summary>
    /// Numéros de rapprochement distincts des lignes intégrées de la déclaration
    /// (socle du tamponnage/dé-tamponnage RT_AFFECTATION).
    /// </summary>
    private async Task<List<string>> GetNumerosRapprochementIntegresAsync(Guid declarationId)
    {
        var lignes = await _repository.GetLignesAsync(
            declarationId, "Decaissement", 1, int.MaxValue, null, null);
        return lignes
            .Where(l => l.Etat == EtatLigne.Integree && !string.IsNullOrWhiteSpace(l.NumeroRapprochement))
            .Select(l => l.NumeroRapprochement)
            .Distinct()
            .ToList();
    }
}

/// <summary>
/// Rapport d'un rafraîchissement de valorisation TVA : combien de factures traitées et la liste
/// des motifs d'échec par facture (transparence — aucune erreur avalée). Renvoyé tel quel au front.
/// </summary>
public record RapportValorisation(int FacturesTraitees, IReadOnlyList<MotifValorisation> Erreurs);

/// <summary>Motif d'échec de valorisation d'une facture (code technique, message lisible, réf. ligne).</summary>
public record MotifValorisation(string Code, string Message, string RefLigne);
