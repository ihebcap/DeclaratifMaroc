using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
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

    // TASK-156 : verrou anti-chevauchement par soId, PARTAGÉ par TOUS les appelants de
    // l'orchestrateur OM (session COM/Sage via SageTaxReader.Console — la ressource contended).
    // Contention constatée en prod (23/07/2026) : 4 cycles concurrents sur le même soId
    // (13:58:24→14:02:26), chacun relançant sa propre lecture OM pendant que le premier batch
    // tournait encore, jusqu'à saturer la ressource et faire échouer le batch au timeout 300 s
    // codé en dur (Declaration.Orchestration/WorkerInvoker.cs:101).
    //
    // PÉRIMÈTRE CORRIGÉ EN COURS DE ROUTE (23/07/2026, après-midi) : un verrou posé UNIQUEMENT
    // autour de RafraichirValorisationAsync (bouton « Rafraîchir » de l'écran Factures) s'est
    // révélé insuffisant — un second incident réel a été observé à 14:20:41 (batch de 361 pièces,
    // AUCUNE ligne [VALO-050] dans le log, donc PAS déclenché par ce bouton). Le PO a confirmé
    // que ce traitement provenait d'une déclaration (« Passer au calcul »/« Détail des lignes »).
    // Ce chemin (ConstruireLignesFigeesAsync, via ChargerCandidatesSiNecessaireAsync) appelle le
    // MÊME orchestrateur.Traiter() sur le MÊME soId, sans jamais partager de verrou avec
    // RafraichirValorisationAsync — la preuve concrète que le problème n'est pas propre au bouton
    // Factures, mais à TOUT appelant de l'orchestrateur OM pour un soId donné. D'où ce verrou
    // UNIQUE, posé par la méthode privée partagée <see cref="ExecuterAvecVerrouOMAsync"/>, appelé
    // par les 4 sites recensés : RafraichirValorisationAsync, ConstruireLignesFigeesAsync,
    // ReintegrerReglementsLiberesAsync, ResynchroniserLigneAsync.
    //
    // Décision PO actée (23/07/2026) : REJET IMMÉDIAT, UNIFORME sur les 4 chemins — pas d'attente
    // silencieuse, pas de traitement différencié selon l'appelant (le PO a explicitement écarté
    // un comportement spécial pour le chemin déclaration vs le chemin Factures). Un second appel
    // concurrent sur le MÊME soId, peu importe lequel des 4 chemins l'a démarré, est rejeté
    // explicitement (InvalidOperationException, propagée en 409 par les contrôleurs), l'appelant
    // doit relancer manuellement une fois le traitement précédent terminé. Clé par soId (jamais un
    // verrou global) : deux soId distincts restent totalement indépendants, même pattern
    // SemaphoreSlim(1,1) par clé que _figeageLocks ci-dessus.
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> _valorisationLocks = new();

    /// <summary>
    /// TASK-156 : point d'entrée UNIQUE du verrou anti-chevauchement par soId — voir le commentaire
    /// de <see cref="_valorisationLocks"/> ci-dessus pour le contexte complet (4 appelants
    /// partagés, rejet immédiat uniforme). <c>WaitAsync(0)</c> n'attend JAMAIS (timeout = 0) :
    /// il acquiert le verrou s'il est libre, sinon échoue immédiatement — conforme à la décision
    /// PO (rejet immédiat, jamais une file d'attente silencieuse).
    /// </summary>
    private async Task<T> ExecuterAvecVerrouOMAsync<T>(int soId, Func<Task<T>> action)
    {
        var gate = _valorisationLocks.GetOrAdd(soId, _ => new SemaphoreSlim(1, 1));
        if (!await gate.WaitAsync(0))
        {
            throw new InvalidOperationException(
                $"Traitement de valorisation déjà en cours pour cette société (soId={soId}). " +
                "Veuillez réessayer une fois le traitement précédent terminé.");
        }

        try
        {
            return await action();
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Surcharge sans valeur de retour de <see cref="ExecuterAvecVerrouOMAsync{T}"/>, même sémantique.</summary>
    private async Task ExecuterAvecVerrouOMAsync(int soId, Func<Task> action)
    {
        await ExecuterAvecVerrouOMAsync<object?>(soId, async () => { await action(); return null; });
    }

    /// <summary>
    /// TASK-080 : préfixe stable du motif d'exclusion inter-déclaration, utilisé à la fois pour
    /// construire le message (avec le numéro de la déclaration concurrente) et pour reconnaître,
    /// lors de la revalidation, les lignes Exclue posées par ce garde-fou (par opposition aux
    /// autres motifs d'exclusion). Toute modification doit rester cohérente entre les deux usages.
    /// </summary>
    private const string PrefixeDejaEnCoursAilleurs = "Déjà pris en compte dans la déclaration ";

    // TASK-166 : les messages d'avertissement/anomalie construits côté back (chaînes déjà
    // formées, jamais reformatées par le front) doivent afficher les montants sur 2 décimales,
    // cohérentes avec formatMoney() (Intl.NumberFormat('fr-FR')) utilisé partout ailleurs dans
    // l'écran ④ — sinon échelle brute à 6 décimales (ex. "4973,380000 MAD").
    private static string FormatMontantMessage(decimal montant) =>
        montant.ToString("N2", CultureInfo.GetCultureInfo("fr-FR"));

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
    protected virtual Task<List<LigneCandidate>> ConstruireLignesFigeesAsync(
        Guid declarationId, DeclarationEntete declaration, string domaine)
    {
        // TASK-156 : ce chemin (ouverture/calcul d'une déclaration — « Passer au calcul »/« Détail
        // des lignes ») appelle le MÊME orchestrateur.Traiter() sur le MÊME soId que
        // RafraichirValorisationAsync — confirmé responsable de l'incident réel du 23/07/2026
        // 14:20 (batch de 361 pièces sans le moindre log [VALO-050], donc pas déclenché par le
        // bouton Rafraîchir). Partage le MÊME verrou par soId — voir _valorisationLocks.
        return ExecuterAvecVerrouOMAsync(declaration.SocieteId, async () =>
        {
            var dateDebut = new DateTime(declaration.Exercice, declaration.Periode, 1);
            var dateFin = dateDebut.AddMonths(1).AddDays(-1);

            var grfConnectionString = _connectionFactory.GetGrfConnectionString();
            // TASK-154 : connexion Sage résolue par SO_Id AVANT l'appel — F_COMPTET (table Sage) n'est
            // jamais lu sur la connexion GRF (même pattern que BuildOrchestrateurAsync ci-dessous).
            var sageInfo = await _connectionFactory.GetSageConnectionInfoAsync(declaration.SocieteId);
            var candidatesList = await _selectionService.SelectionnerExpliqueeAsync(
                declaration.SocieteId, dateDebut, dateFin, grfConnectionString, sageInfo.ConnectionString);

            if (domaine == "Decaissement")
            {
                candidatesList = candidatesList.Where(c => c.Affectation.Source == SourceAffectation.Decaissement
                                                        || c.Affectation.Source == SourceAffectation.Espece
                                                        || c.Affectation.Source == SourceAffectation.Depense).ToList();
            }
            else if (domaine == "Encaissement")
            {
                candidatesList = candidatesList.Where(c => c.Affectation.Source == SourceAffectation.Encaissement).ToList();
            }

            // TASK-097 : Filtre sur le périmètre réel des règlements sélectionnés — toujours appliqué,
            // y compris quand aucune sélection n'a encore été persistée (selection vide/null), auquel
            // cas le résultat est ZÉRO candidat et jamais "tous les candidats" (invariance serveur,
            // cf. réserve bloquante VERIFY TASK-097).
            var selection = await _repository.GetSelectionReglementsAsync(declarationId);
            var selectionSet = new HashSet<string>(selection ?? Enumerable.Empty<string>());
            candidatesList = candidatesList.Where(c => selectionSet.Contains(c.Affectation.NumeroRapprochement)).ToList();

            var candidates = candidatesList.ToList();

            // TASK-080 : garde-fou d'exclusivité — un règlement déjà Proposee/Integree dans une
            // AUTRE déclaration (EnCours ou Cloturee) de la même société ne doit pas devenir
            // éligible ici, sous peine de double-affectation (le verrou DT_Id/TASK-028 ne se
            // déclenche, lui, qu'à la clôture — trop tard). Appliqué APRÈS l'évaluateur (qui
            // ignore toute notion de déclaration) et AVANT la sélection des éligibles, pour ne
            // jamais remplacer un motif de rejet déjà posé (DejaDeclare, HorsPeriode, etc.).
            await AppliquerExclusiviteInterDeclarationAsync(candidates, declaration.SocieteId, declarationId);

            var orchestrateur = await BuildOrchestrateurAsync(grfConnectionString, declaration.SocieteId);

            var affectations = candidates.Where(c => c.EstEligible).Select(c => c.Affectation).ToList();
            var modele = orchestrateur.Traiter(affectations, 2);

            // TASK-161 : mapping tiers→activité (niveau 2 de la cascade), chargé une seule fois
            // par société pour tout le lot — jamais un aller-retour GRF par ligne.
            var (mappingParNumero, mappingParNom) = await ChargerMappingCodeActiviteTiersAsync(declaration.SocieteId);

            return MapLignesCandidates(declarationId, domaine, candidates, modele, mappingParNumero, mappingParNom);
        });
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
                    // TASK-154 : connexion Sage résolue par SO_Id AVANT l'appel (F_COMPTET est Sage).
                    var sageInfo = await _connectionFactory.GetSageConnectionInfoAsync(declaration.SocieteId);
                    var candidats = await _selectionService.SelectionnerExpliqueeAsync(
                        declaration.SocieteId, dateDebut, dateFin, grfConnectionString, sageInfo.ConnectionString);

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
        // TASK-118 : la sentinelle ERREUR du cache est désormais scopée par SO_Id (EC_Id seul
        // peut collisionner entre deux bases Sage distinctes) — bornée à la société de CETTE
        // déclaration, jamais à une autre.
        var ecIdsEnErreur = await _repository.GetEcIdsEnErreurAsync(declarationPourReintegration?.SocieteId ?? 0, ecIds);

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
                var message = string.IsNullOrWhiteSpace(l.NumeroRapprochement)
                    ? $"Facture {l.NumeroFacture} exclue de la valorisation (EC_Id={l.EC_Id}) : incohérence Sage HT/TVA/TTC détectée — vérification manuelle requise avant clôture. Ligne non valorisée, aucune valeur déclarée pour cette pièce."
                    : $"Facture {l.NumeroFacture}, règlement {l.NumeroRapprochement} exclue de la valorisation (EC_Id={l.EC_Id}) : incohérence Sage HT/TVA/TTC détectée — vérification manuelle requise avant clôture. Ligne non valorisée, aucune valeur déclarée pour cette pièce.";

                alertes.Add(new Alerte
                {
                    Niveau = NiveauAlerte.Warning,
                    Code = "LIGNE_FIGEE_A_REVERIFIER",
                    Message = message,
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
    private Task ReintegrerReglementsLiberesAsync(
        Guid declarationId, string domaine, DeclarationEntete declaration, List<LigneCandidate> lignesLiberees)
    {
        if (declaration.Exercice <= 0 || declaration.Periode <= 0) return Task.CompletedTask;

        // TASK-156 : cette variante de réintégration appelle elle aussi orchestrateur.Traiter()
        // sur le même soId — appelée depuis RevaliderLignesFigeesAsync, y compris depuis la
        // branche "déjà figé" de ChargerCandidatesSiNecessaireAsync qui ne détient PAS le
        // _figeageLocks — partage donc le MÊME verrou soId que les 3 autres sites.
        return ExecuterAvecVerrouOMAsync(declaration.SocieteId, async () =>
        {
            var clesLiberees = lignesLiberees
                .Select(l => (l.NumeroFacture, l.NumeroRapprochement))
                .ToHashSet();

            var dateDebut = new DateTime(declaration.Exercice, declaration.Periode, 1);
            var dateFin = dateDebut.AddMonths(1).AddDays(-1);
            var grfConnectionString = _connectionFactory.GetGrfConnectionString();
            // TASK-154 : connexion Sage résolue par SO_Id AVANT l'appel (F_COMPTET est Sage).
            var sageInfo = await _connectionFactory.GetSageConnectionInfoAsync(declaration.SocieteId);
            var candidatsList = await _selectionService.SelectionnerExpliqueeAsync(
                declaration.SocieteId, dateDebut, dateFin, grfConnectionString, sageInfo.ConnectionString);

            if (domaine == "Decaissement")
            {
                candidatsList = candidatsList.Where(c => c.Affectation.Source == SourceAffectation.Decaissement
                                                        || c.Affectation.Source == SourceAffectation.Espece
                                                        || c.Affectation.Source == SourceAffectation.Depense).ToList();
            }
            else if (domaine == "Encaissement")
            {
                candidatsList = candidatsList.Where(c => c.Affectation.Source == SourceAffectation.Encaissement).ToList();
            }
            var candidats = candidatsList.ToList();

            var aReintegrer = candidats
                .Where(c => clesLiberees.Contains((c.Affectation.NumeroFacture, c.Affectation.NumeroRapprochement)))
                .ToList();
            if (aReintegrer.Count == 0) return;

            await AppliquerExclusiviteInterDeclarationAsync(aReintegrer, declaration.SocieteId, declarationId);

            var orchestrateur = await BuildOrchestrateurAsync(grfConnectionString, declaration.SocieteId);
            var affectationsEligibles = aReintegrer.Where(c => c.EstEligible).Select(c => c.Affectation).ToList();
            var modele = orchestrateur.Traiter(affectationsEligibles, 2);

            // TASK-161 : même résolution qu'au premier figeage (ConstruireLignesFigeesAsync).
            var (mappingParNumero, mappingParNom) = await ChargerMappingCodeActiviteTiersAsync(declaration.SocieteId);
            var nouvellesLignes = MapLignesCandidates(declarationId, domaine, aReintegrer, modele, mappingParNumero, mappingParNom);

            await _repository.DeleteLignesAsync(lignesLiberees.Select(l => l.Id));
            await _repository.SaveLignesCandidatesAsync(nouvellesLignes);
        });
    }

    /// <summary>
    /// TASK-078 : trace la décision explicite du PO de VALIDER une incohérence déjà signalée
    /// (TASK-077) — accepte l'état en connaissance de cause. Ne modifie AUCUNE ligne ni total ;
    /// supprime seulement la répétition du signalement aux prochains chargements.
    /// </summary>
    public Task ValiderIncoherenceLigneAsync(Guid declarationId, int ecId, string utilisateur) =>
        _repository.ValiderIncoherenceAsync(declarationId, ecId, utilisateur);

    /// <summary>
    /// TASK-161 : surcharge manuelle du code activité d'UNE ligne (écran ② Vérifier &amp;
    /// Intégrer), niveau 1 (prioritaire) de la cascade — couvre le cas confirmé PO "même facture,
    /// deux activités" (surcharge posée sur une seule des lignes de la facture, jamais par EC_Id).
    /// Bloquée après clôture (figée telle quelle, décision PO TASK-161 : stable même si le
    /// mapping tiers change ensuite) — contrairement à ValiderIncoherenceLigneAsync ci-dessus
    /// (TASK-078, sans garde de statut), le code activité doit rester immuable une fois la
    /// déclaration close.
    /// </summary>
    public async Task ModifierCodeActiviteLigneAsync(Guid declarationId, Guid ligneId, string codeActivite, string utilisateur)
    {
        var declaration = await _repository.GetByIdAsync(declarationId);
        if (declaration == null) throw new ArgumentException("Déclaration introuvable");
        if (declaration.Statut == StatutDeclaration.Cloturee)
            throw new InvalidOperationException("Le code activité ne peut plus être modifié après clôture de la déclaration.");

        if (!string.IsNullOrEmpty(codeActivite))
        {
            var domaineLigne = await _repository.GetDomaineLigneAsync(ligneId);
            if (domaineLigne == null) throw new ArgumentException("Ligne introuvable");
            await ValiderDomaineCodeActiviteAsync(codeActivite, domaineLigne);
        }

        await _repository.UpdateCodeActiviteLigneAsync(ligneId, codeActivite ?? "", utilisateur);
    }

    /// <summary>
    /// TASK-172 §4 : recroise le domaine réel (P_DECTVAACTIVITE.DTA_Domaine) du code choisi avec le
    /// domaine de la/les ligne(s) ciblée(s) — décision PO : garde-fou dur côté serveur, un filtre
    /// front seul n'empêche pas un appel API direct avec un code du mauvais domaine. Lève une
    /// <see cref="ApplicationException"/> explicite (traduite en 400 par le contrôleur, distincte du
    /// 409 de clôture) si le code est inconnu du référentiel ou incompatible avec le domaine.
    /// </summary>
    private async Task ValiderDomaineCodeActiviteAsync(string codeActivite, string domaineLigne)
    {
        var domaineCode = await _repository.GetDomaineCodeActiviteAsync(codeActivite);
        if (domaineCode == null)
            throw new ApplicationException($"Code activité « {codeActivite} » inconnu dans le référentiel.");

        var domaineLigneAttendu = domaineLigne switch
        {
            "Encaissement" => 1,
            "Decaissement" => 2,
            _ => throw new ApplicationException($"Domaine de ligne '{domaineLigne}' inattendu — impossible de valider le code activité.")
        };
        if (domaineCode != domaineLigneAttendu)
        {
            var libelleLigne = domaineLigne == "Encaissement" ? "Encaissement" : "Décaissement";
            var libelleCode = domaineCode == 1 ? "Encaissement" : "Décaissement";
            throw new ApplicationException(
                $"Code activité « {codeActivite} » réservé au domaine {libelleCode}, incompatible avec une ligne {libelleLigne}.");
        }
    }

    /// <summary>
    /// TASK-172 : référentiel des codes activité, pour la liste déroulante front. <paramref
    /// name="domaine"/> optionnel ("Encaissement"/"Decaissement") filtre par onglet actif.
    /// </summary>
    public Task<IReadOnlyList<CodeActiviteReferentielRow>> GetReferentielCodesActiviteAsync(string? domaine = null) =>
        _repository.GetReferentielCodesActiviteAsync(domaine);

    /// <summary>
    /// TASK-173 : affectation en masse du code activité — même sélection (liste d'IDs ou
    /// domaine+filtre) que UpdateLignesEtatBulkAsync/ByIdsAsync (TASK-012), même garde de clôture et
    /// même validation de domaine que ModifierCodeActiviteLigneAsync. Décision PO §4 : une sélection
    /// mixte Encaissement/Décaissement (par LigneIds) est bloquée explicitement, jamais résolue par
    /// défaut sur l'un des deux domaines.
    /// </summary>
    public async Task ModifierCodeActiviteLignesBulkAsync(
        Guid declarationId, List<Guid>? ligneIds, string? domaine, string? filter, string codeActivite, string utilisateur)
    {
        var declaration = await _repository.GetByIdAsync(declarationId);
        if (declaration == null) throw new ArgumentException("Déclaration introuvable");
        if (declaration.Statut == StatutDeclaration.Cloturee)
            throw new InvalidOperationException("Le code activité ne peut plus être modifié après clôture de la déclaration.");

        string domaineEffectif;
        if (ligneIds != null && ligneIds.Count > 0)
        {
            // TASK-173 correctif (rejet architecte 24/07/2026) : GetDomainesDistinctsLignesAsync
            // est scopé par declarationId — un ligneId d'une autre déclaration (y compris
            // Clôturée) n'est jamais pris en compte, exactement comme s'il n'existait pas.
            var domainesDistincts = await _repository.GetDomainesDistinctsLignesAsync(declarationId, ligneIds);
            if (domainesDistincts.Count > 1)
                throw new ApplicationException(
                    "Sélection mixte Encaissement/Décaissement : l'affectation en masse du code activité doit porter sur un seul domaine à la fois.");
            if (domainesDistincts.Count == 0)
                return; // Aucune ligne trouvée (pour cette déclaration) parmi ces IDs — rien à faire, pas d'erreur.
            domaineEffectif = domainesDistincts[0];
        }
        else if (!string.IsNullOrEmpty(domaine))
        {
            domaineEffectif = domaine;
        }
        else
        {
            throw new ApplicationException("Soit LigneIds soit Domaine doit être renseigné.");
        }

        if (!string.IsNullOrEmpty(codeActivite))
            await ValiderDomaineCodeActiviteAsync(codeActivite, domaineEffectif);

        if (ligneIds != null && ligneIds.Count > 0)
            await _repository.UpdateCodeActiviteBulkByIdsAsync(declarationId, ligneIds, codeActivite ?? "", utilisateur);
        else
            await _repository.UpdateCodeActiviteBulkAsync(declarationId, domaineEffectif, filter, codeActivite ?? "", utilisateur);
    }

    /// <summary>
    /// TASK-078 : resynchronise UNE pièce (EC_Id) après correction côté Sage — relit
    /// explicitement l'OM pour cette seule facture (effet de bord : réécrit
    /// DM_VENTILATION_SAGE_CACHE via l'orchestrateur, même pipeline que TASK-072/076/077,
    /// aucune règle dupliquée) et réinitialise une éventuelle validation antérieure (les faits
    /// ont changé). Retourne (trouvee, resolue) : resolue=true si la pièce n'est plus en erreur
    /// après relecture.
    /// </summary>
    public async Task<(bool Trouvee, bool Resolue)> ResynchroniserLigneAsync(Guid declarationId, int ecId)
    {
        var lignesDec = await _repository.GetLignesAsync(declarationId, "Decaissement", 1, int.MaxValue, null, null);
        var lignesEnc = await _repository.GetLignesAsync(declarationId, "Encaissement", 1, int.MaxValue, null, null);
        var lignes = lignesDec.Concat(lignesEnc).Where(l => l.EC_Id == ecId).ToList();
        if (lignes.Count == 0) return (false, false);
        var ligne = lignes[0];

        var declaration = await _repository.GetByIdAsync(declarationId);
        if (declaration == null) return (false, false);

        // TASK-156 : resynchronisation d'une seule pièce — appelle elle aussi orchestrateur.Traiter()
        // sur le même soId (via le worker OM réel). Partage le MÊME verrou soId que les 3 autres
        // sites (rejet immédiat uniforme si un autre traitement OM tourne déjà pour ce soId).
        return await ExecuterAvecVerrouOMAsync(declaration.SocieteId, async () =>
        {
            var grfConnectionString = _connectionFactory.GetGrfConnectionString();
            var orchestrateur = await BuildOrchestrateurAsync(grfConnectionString, declaration.SocieteId);

            // Purge d'abord la ligne de cache existante : sans ça, une sentinelle ERREUR portant déjà
            // des montants bruts capturés (TASK-076) ferait considérer TryServireDepuisCache la pièce
            // comme définitivement réglée et ne relirait jamais Sage — même après correction ERP,
            // « Resynchroniser » n'aurait alors aucun effet réel.
            var persistenceCs = _configuration.GetConnectionString("PersistenceConnection") ?? "";
            new VentilationSageCacheRepository().SupprimerEntrees(declaration.SocieteId, ecId, persistenceCs);

            var affectation = new AffectationADeclarer
            {
                NumeroFacture = ligne.NumeroFacture,
                NumeroRapprochement = ligne.NumeroRapprochement,
                Sens = ligne.Domaine == "Encaissement" ? SensAffectation.Vente : SensAffectation.Achat,
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

            var toujoursEnErreur = await _repository.GetEcIdsEnErreurAsync(declaration.SocieteId, new[] { ecId });
            return (true, !toujoursEnErreur.Contains(ecId));
        });
    }

    /// <summary>
    /// TASK-176 : resynchronisation EN MASSE — réutilise STRICTEMENT <see cref="ResynchroniserLigneAsync"/>
    /// pour chaque EC_Id résolu par la sélection (LigneIds explicites OU Domaine[+Filter], même contrat
    /// de sélection que UpdateLignesBulk/UpdateCodeActiviteBulk, TASK-012/173). Aucune nouvelle règle de
    /// lecture Sage, aucun nouveau verrou : la seule addition est la boucle d'orchestration.
    ///
    /// Traitement STRICTEMENT SÉQUENTIEL — jamais de Parallel.ForEach / Task.WhenAll : chaque appel
    /// ResynchroniserLigneAsync prend puis relâche le verrou soId (TASK-156, ExecuterAvecVerrouOMAsync) ;
    /// un parallélisme interne s'auto-contentionnerait sur ce même verrou (et sur le même soId en plus).
    ///
    /// Retour AGRÉGÉ (traitées / résolues / toujours en anomalie avec motif) pour un affichage synthétique
    /// côté front, pas N réponses individuelles à corréler. Si le verrou soId est capté par un AUTRE
    /// traitement concurrent (bouton Rafraîchir, chargement d'un autre onglet…) ENTRE deux itérations,
    /// la boucle s'arrête proprement (Interrompu=true, message TASK-156) sans perdre les lignes déjà
    /// resynchronisées ni lever en 500.
    /// </summary>
    public async Task<ResynchroBulkResultat> ResynchroniserLignesBulkAsync(
        Guid declarationId, List<Guid>? ligneIds, string? domaine, string? filter)
    {
        var declaration = await _repository.GetByIdAsync(declarationId);
        if (declaration == null) throw new ArgumentException("Déclaration introuvable");

        // Résolution de la sélection en lignes candidates — lecture seule des lignes déjà persistées,
        // même mécanique de sélection que les autres bulks : soit une liste explicite de LigneIds,
        // soit un Domaine (+ Filter éventuel). Aucune écriture ici.
        List<LigneCandidate> selection;
        if (ligneIds != null && ligneIds.Count > 0)
        {
            var idSet = ligneIds.ToHashSet();
            var lignesDec = await _repository.GetLignesAsync(declarationId, "Decaissement", 1, int.MaxValue, null, null);
            var lignesEnc = await _repository.GetLignesAsync(declarationId, "Encaissement", 1, int.MaxValue, null, null);
            selection = lignesDec.Concat(lignesEnc).Where(l => idSet.Contains(l.Id)).ToList();
        }
        else if (!string.IsNullOrEmpty(domaine))
        {
            selection = (await _repository.GetLignesAsync(declarationId, domaine, 1, int.MaxValue, null, filter)).ToList();
        }
        else
        {
            throw new ApplicationException("Soit LigneIds soit Domaine doit être renseigné.");
        }

        // Une pièce Sage (EC_Id) peut porter plusieurs lignes candidates (plusieurs taux) — on ne
        // resynchronise chaque pièce qu'UNE fois. NumeroFacture conservé pour un retour lisible.
        var pieces = selection
            .Where(l => l.EC_Id > 0)
            .GroupBy(l => l.EC_Id)
            .Select(g => new { EcId = g.Key, NumeroFacture = g.First().NumeroFacture })
            .ToList();

        var resultat = new ResynchroBulkResultat { TotalSelection = pieces.Count };

        foreach (var piece in pieces)
        {
            try
            {
                var (trouvee, resolue) = await ResynchroniserLigneAsync(declarationId, piece.EcId);
                if (!trouvee)
                {
                    resultat.NonTrouvees++;
                    continue;
                }
                resultat.Traitees++;
                if (resolue)
                {
                    resultat.Resolues++;
                }
                else
                {
                    // Motif relu depuis le cache déjà réécrit par la relecture (lecture seule, aucune
                    // nouvelle lecture OM), même source que le diagnostic ligne (TASK-144).
                    var motif = await _repository.GetMotifErreurCacheAsync(declaration.SocieteId, piece.EcId);
                    resultat.ToujoursEnAnomalie.Add(new ResynchroLigneAnomalie
                    {
                        EcId = piece.EcId,
                        NumeroFacture = piece.NumeroFacture ?? "",
                        Motif = string.IsNullOrWhiteSpace(motif)
                            ? "Toujours en anomalie après relecture Sage (cache en erreur)."
                            : motif
                    });
                }
            }
            catch (InvalidOperationException ex)
            {
                // TASK-156 : le verrou soId a été capté par un AUTRE traitement entre deux itérations.
                // Rejet propre — on interrompt sans perdre les lignes déjà passées, jamais un 500.
                resultat.Interrompu = true;
                resultat.MessageInterruption = ex.Message;
                break;
            }
        }

        return resultat;
    }

    /// <summary>
    /// Rafraîchit la valorisation TVA (famille B) pour une période bornée, sans déclaration.
    /// Sélectionne les factures éligibles de la période, lit les OM Sage et remplit
    /// DM_VENTILATION_SAGE_CACHE (effet de bord de l'orchestrateur). Aucune ligne de déclaration
    /// écrite, aucun DT_Id touché. Utilisé par le bouton « Rafraîchir » de l'écran Factures.
    /// Retourne le nombre de factures éligibles traitées.
    /// </summary>
    public Task<RapportValorisation> RafraichirValorisationAsync(int soId, DateTime dateDebut, DateTime dateFin)
    {
        // TASK-156 correctif A : rejet immédiat si un traitement OM est déjà en cours pour ce
        // soId — sur CE chemin (RafraichirValorisationAsync) OU sur l'un des 3 autres appelants
        // partageant le même verrou (ConstruireLignesFigeesAsync, la réintégration de lignes
        // libérées, ResynchroniserLigneAsync — voir le commentaire de _valorisationLocks).
        // Exception explicite, propagée en 409 par FacturesController.
        return ExecuterAvecVerrouOMAsync(soId, async () =>
        {
            var grfConnectionString = _connectionFactory.GetGrfConnectionString();
            // TASK-154 : connexion Sage résolue par SO_Id AVANT l'appel — c'est ce site précis qui
            // levait `SqlException 208 : Nom d'objet 'F_COMPTET' non valide` (F_COMPTET est une table
            // Sage, jamais lue via la connexion GRF).
            var sageInfo = await _connectionFactory.GetSageConnectionInfoAsync(soId);

            // TASK-050 Partie B — Valorisation facture-first : on lit TOUTES les factures EC_Type=0
            // de la période depuis RT_ECHEANCE, indépendamment du statut de règlement/rapprochement.
            // Avant TASK-050, on partait de RT_MOUVEMENT avec filtre MV_DECAISSE → 465+ factures
            // réglées par chèque (MV_Type=1, MV_DECAISSE=0) étaient silencieusement exclues.
            // La DÉCLARATION reste strictement gated sur EstEligible (RecalculerLignesCandidatesAsync).
            var toutesFactures = await _selectionService.LireFacturesDepuisPeriodeAsync(
                soId, dateDebut, dateFin, grfConnectionString, sageInfo.ConnectionString);

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

            var orchestrateur = await BuildOrchestrateurAsync(grfConnectionString, soId);
            var modele = orchestrateur.Traiter(affectations, 2); // effet de bord : écriture du cache

            // Rapport transparent : les alertes de l'orchestrateur portent le motif exact par facture.
            var erreurs = modele.Alertes
                .Where(a => a.Niveau == Declaration.Core.Model.NiveauAlerte.Error)
                .Select(a => new MotifValorisation(a.Code, a.Message, a.RefLigne))
                .ToList();

            JournaliserValorisation($"[VALO-050] === Fin : {affectations.Count} traitée(s), {erreurs.Count} en erreur ===");

            return new RapportValorisation(affectations.Count, erreurs);
        });
    }


    /// <summary>
    /// Construit l'orchestrateur de déclaration avec la config worker Sage et la connexion de
    /// persistance (cache TVA). Factorisé pour être partagé entre le chargement des candidates
    /// et le rafraîchissement autonome de la valorisation.
    ///
    /// TASK-118 : la connexion Sage (Server/Database/User/Password du worker OM) n'est plus
    /// statique (<c>connections.json:SageConnection</c>) — elle est résolue dynamiquement par
    /// <paramref name="soId"/> via <see cref="IDbConnectionFactory.GetSageConnectionInfoAsync"/>
    /// (P_SOCIETE.SO_ErpDb + SO_ErpUserApp/SO_ErpPasswdApp, Server/User/Password SQL toujours
    /// ceux de GrfConnection — confirmation PO 18/07/2026). Échec explicite (exception) propagé
    /// tel quel si le SO_Id est introuvable ou sans SO_ErpDb — aucun repli silencieux.
    /// </summary>
    private async Task<OrchestrateurDeclaration> BuildOrchestrateurAsync(string grfConnectionString, int soId)
    {
        var sageInfo = await _connectionFactory.GetSageConnectionInfoAsync(soId);
        var sageCs = sageInfo.ConnectionString;
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(sageCs);
        var workerExe = _configuration.GetSection("WorkerConfig")?["WorkerExePath"] ?? "SageTaxReader.Console.exe";

        // Déploiement mono-dossier : un chemin worker relatif est résolu à côté de l'exe de l'API.
        if (!Path.IsPathRooted(workerExe))
            workerExe = Path.Combine(AppContext.BaseDirectory, workerExe);

        // TASK-118 : identifiants Sage OM (Objets Métier, distincts du login SQL) désormais
        // exclusivement issus de P_SOCIETE.SO_ErpUserApp/SO_ErpPasswdApp pour ce SO_Id — la
        // section statique connections.json:SageOM est devenue obsolète (non lue).
        var workerConfig = new WorkerConfig
        {
            Server = builder.DataSource,
            Database = builder.InitialCatalog,
            User = sageInfo.OmUser ?? "",
            Password = sageInfo.OmPassword ?? "",
            WorkerExePath = workerExe
        };
        var invoker = new WorkerInvoker();
        var lecteurFgr = new Declaration.Orchestration.LecteurTvaFgr();
        var persistenceCs = _configuration.GetConnectionString("PersistenceConnection") ?? "";
        return new OrchestrateurDeclaration(
            invoker, workerConfig, lecteurFgr, grfConnectionString, sageCs,
            ventilationCache: null, persistenceConnectionString: persistenceCs,
            log: JournaliserValorisation, soId: soId);
    }

    /// <summary>
    /// TASK-161 : charge le mapping tiers→activité (P_SOCIETECODEACTIVITETIERS, lecture seule)
    /// pour cette société et le réduit à deux dictionnaires (par numéro tiers, par intitulé ERP en
    /// repli) consommés par <see cref="Declaration.Core.CodeActiviteResolver"/>. Un doublon de clé
    /// (mapping ambigu côté WinForms) retient arbitrairement le premier — jamais une exception qui
    /// bloquerait tout le figeage pour un problème de paramétrage tiers.
    /// </summary>
    private async Task<(IReadOnlyDictionary<string, string> ParNumero, IReadOnlyDictionary<string, string> ParNom)>
        ChargerMappingCodeActiviteTiersAsync(int soId)
    {
        var rows = await _repository.GetMappingCodeActiviteTiersAsync(soId);
        var parNumero = rows
            .Where(r => !string.IsNullOrWhiteSpace(r.NumeroTiers) && !string.IsNullOrWhiteSpace(r.CodeActivite))
            .GroupBy(r => r.NumeroTiers!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().CodeActivite, StringComparer.Ordinal);
        var parNom = rows
            .Where(r => !string.IsNullOrWhiteSpace(r.ErpIntitule) && !string.IsNullOrWhiteSpace(r.CodeActivite))
            .GroupBy(r => r.ErpIntitule, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().CodeActivite, StringComparer.OrdinalIgnoreCase);
        return (parNumero, parNom);
    }

    public static List<LigneCandidate> MapLignesCandidates(
        Guid declarationId, string domaine, IEnumerable<AffectationCandidate> candidates, DeclarationModele modele,
        IReadOnlyDictionary<string, string>? mappingCodeActiviteParNumero = null,
        IReadOnlyDictionary<string, string>? mappingCodeActiviteParNom = null)
    {
        var lignes = new List<LigneCandidate>();

        foreach (var c in candidates)
        {
            if (!c.EstEligible)
            {
                // TASK-097 : Tout ce qui n'est pas déclarable ne produit aucune ligne du tout
                continue;
            }
            else
            {
                // TASK-161 : niveaux 2/3/4 de la cascade unique (surcharge ligne exclue ici — cette
                // méthode ne construit que des lignes NOUVELLES, sans surcharge possible encore).
                var codeActivite = Declaration.Core.CodeActiviteResolver.Resoudre(
                    surchargeManuelle: null,
                    tiersNumero: c.Affectation.Tiers.Numero,
                    tiersNom: c.Affectation.Tiers.Nom,
                    codeActiviteSage: c.Affectation.Tiers.CodeActivite,
                    mappingParNumero: mappingCodeActiviteParNumero,
                    mappingParNom: mappingCodeActiviteParNom);

                var taxesLines = modele.Lignes.Where(l => l.NumeroFacture == c.Affectation.NumeroFacture).ToList();
                if (!taxesLines.Any())
                {
                    var alerte = modele.Alertes.FirstOrDefault(a => a.RefLigne.Contains(c.Affectation.NumeroFacture));
                    string motif = alerte != null ? alerte.Message : "Facture introuvable ou non ventilée";

                    lignes.Add(new LigneCandidate
                    {
                        Id = Guid.NewGuid(),
                        DeclarationId = declarationId,
                        Etat = EtatLigne.Proposee, // TASK-097 : La ligne reste Proposee
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
                        Prorata = 0,
                        MontantAffecte = c.Affectation.MontantAffecte,
                        ModePaiement = c.Affectation.ModePaiement,
                        DatePaiement = c.Affectation.DatePaiement,
                        DateFacture = c.Affectation.DateFacture,
                        Source = c.Affectation.Source.ToString(),
                        EcType = c.Affectation.EC_Type,
                        EC_Id = c.Affectation.EC_Id,
                        MV_Id = c.Affectation.MV_Id,
                        CodeActivite = codeActivite
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
                            MV_Id = c.Affectation.MV_Id,
                            CodeActivite = codeActivite
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
        var toutesDec = await _repository.GetLignesAsync(declarationId, "Decaissement", 1, int.MaxValue, null, null);
        var toutesEnc = await _repository.GetLignesAsync(declarationId, "Encaissement", 1, int.MaxValue, null, null);
        var toutes = toutesDec.Concat(toutesEnc).ToList();
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

        // Alerte explicite pour les règlements sélectionnés mais exclus du calcul (non éligibles)
        var selection = await _repository.GetSelectionReglementsAsync(declarationId);
        if (selection != null && selection.Count > 0)
        {
            var selectionSet = new HashSet<string>(selection);
            var dateDebut = new DateTime(declaration.Exercice, declaration.Periode, 1);
            var dateFin = dateDebut.AddMonths(1).AddDays(-1);
            var grfConnectionString = _connectionFactory.GetGrfConnectionString();
            // TASK-154 : connexion Sage résolue par SO_Id AVANT l'appel (F_COMPTET est Sage).
            var sageInfo = await _connectionFactory.GetSageConnectionInfoAsync(declaration.SocieteId);
            var candidates = await _selectionService.SelectionnerExpliqueeAsync(
                declaration.SocieteId, dateDebut, dateFin, grfConnectionString, sageInfo.ConnectionString);
            var candidatesList = candidates.ToList();

            await AppliquerExclusiviteInterDeclarationAsync(candidatesList, declaration.SocieteId, declarationId);

            var selectedCandidates = candidatesList
                .Where(c => selectionSet.Contains(c.Affectation.NumeroRapprochement))
                .ToList();

            foreach (var c in selectedCandidates)
            {
                if (!c.EstEligible && (
                    c.Motif == MotifRejet.EcTypeHorsPerimetre ||
                    c.Motif == MotifRejet.Impaye ||
                    c.Motif == MotifRejet.Annule ||
                    c.Motif == MotifRejet.NonComptabilise ||
                    c.Motif == MotifRejet.NonAffecte))
                {
                    string message = c.Motif switch
                    {
                        MotifRejet.EcTypeHorsPerimetre => c.Affectation.EC_Type == 1
                            ? $"Règlement impayé — non déclarable (à traiter phase 2) : {c.Affectation.NumeroRapprochement} (tiers {c.Affectation.Tiers.Nom}, {FormatMontantMessage(c.Affectation.MontantAffecte)} MAD)"
                            : $"Règlement hors périmètre ({ReglementRapprochementRow.LibelleEcType(c.Affectation.EC_Type)}) — non déclarable : {c.Affectation.NumeroRapprochement} (tiers {c.Affectation.Tiers.Nom}, {FormatMontantMessage(c.Affectation.MontantAffecte)} MAD)",
                        MotifRejet.Impaye => $"Règlement impayé — non déclarable : {c.Affectation.NumeroRapprochement} (tiers {c.Affectation.Tiers.Nom}, {FormatMontantMessage(c.Affectation.MontantAffecte)} MAD)",
                        MotifRejet.Annule => $"Règlement annulé — non déclarable : {c.Affectation.NumeroRapprochement} (tiers {c.Affectation.Tiers.Nom}, {FormatMontantMessage(c.Affectation.MontantAffecte)} MAD)",
                        MotifRejet.NonComptabilise => $"Règlement non comptabilisé — non déclarable : {c.Affectation.NumeroRapprochement} (tiers {c.Affectation.Tiers.Nom}, {FormatMontantMessage(c.Affectation.MontantAffecte)} MAD)",
                        MotifRejet.NonAffecte => $"Règlement non affecté — non déclarable : {c.Affectation.NumeroRapprochement} (tiers {c.Affectation.Tiers.Nom}, {FormatMontantMessage(c.Affectation.MontantAffecte)} MAD)",
                        _ => $"Règlement exclu ({c.MotifLibelle}) — non déclarable : {c.Affectation.NumeroRapprochement} (tiers {c.Affectation.Tiers.Nom}, {FormatMontantMessage(c.Affectation.MontantAffecte)} MAD)"
                    };

                    model.Alertes.Add(new Alerte
                    {
                        Niveau = NiveauAlerte.Warning,
                        Code = "REGLEMENT_EXCLU",
                        Message = message,
                        RefLigne = c.Affectation.NumeroFacture ?? "Global"
                    });
                }
            }
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

        // Contrôle facture non ventilée/introuvable sur les lignes intégrées ou éligibles
        // TASK-177 : une incohérence déjà VALIDÉE explicitement par le PO (TASK-078, traçabilité
        // qui/quand sur DM_LGTVA.IncoherenceValidee) ne doit plus bloquer la clôture ici — même
        // garde que celle déjà appliquée dans RevaliderLignesFigeesAsync (ligne ~465) pour
        // LIGNE_FIGEE_A_REVERIFIER. Ligne et totaux restent inchangés : seule la génération de
        // cette alerte Error est supprimée pour la ligne validée.
        foreach (var l in integrees.Where(l => !string.IsNullOrEmpty(l.MotifRejet) && !l.IncoherenceValidee))
        {
            var message = string.IsNullOrWhiteSpace(l.NumeroRapprochement)
                ? $"Ligne en anomalie de recalcul : {l.MotifRejet}"
                : $"Ligne en anomalie de recalcul (facture {l.NumeroFacture}, règlement {l.NumeroRapprochement}) : {l.MotifRejet}";

            model.Alertes.Add(new Alerte
            {
                Niveau = NiveauAlerte.Error,
                Code = "FACTURE_NON_VENTILEE",
                Message = message,
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
            var alertesDec = await RevaliderLignesFigeesAsync(declarationId, "Decaissement");
            var alertesEnc = await RevaliderLignesFigeesAsync(declarationId, "Encaissement");
            model.Alertes.AddRange(alertesDec);
            model.Alertes.AddRange(alertesEnc);
        }

        return model;
    }

    /// <summary>
    /// TASK-155 : reconstruit le <see cref="DeclarationModele"/> complet (entête + lignes) d'une
    /// déclaration, pour l'export XML (CDC DGI, TASK-137) et Excel (TASK-010). Seules les lignes
    /// réellement <see cref="EtatLigne.Integree"/> sont exportées — Proposee/Exclue/Reportee/Ecartee
    /// n'ont jamais été retenues par la clôture (TASK-071) et ne doivent pas apparaître dans un
    /// dépôt officiel. <c>Designation</c> reste vide (gap connu DM_LGTVA/LigneCandidate, hors
    /// périmètre TASK-155 — cf. cartographie de la task, comportement déjà accepté par TASK-011).
    /// </summary>
    /// <exception cref="ArgumentException">Déclaration introuvable.</exception>
    /// <exception cref="InvalidOperationException">
    /// Identifiant fiscal de la société absent de <c>P_SOCIETE.SO_Identifiant</c> — jamais de
    /// valeur placeholder exportée (règle actée TASK-151), la génération doit être bloquée
    /// explicitement pour que le PO/l'ERP renseigne la donnée.
    /// </exception>
    public async Task<DeclarationModele> ConstruireModeleExportAsync(Guid declarationId)
    {
        var declaration = await _repository.GetByIdAsync(declarationId);
        if (declaration == null) throw new ArgumentException("Déclaration introuvable");

        var identifiantFiscal = await _repository.GetIdentifiantFiscalSocieteAsync(declaration.SocieteId);
        if (string.IsNullOrWhiteSpace(identifiantFiscal))
            throw new InvalidOperationException(
                $"Identifiant fiscal de la société (SO_Id={declaration.SocieteId}) non configuré " +
                "(P_SOCIETE.SO_Identifiant absent ou vide) — génération impossible. Contactez le " +
                "PO/l'administrateur ERP pour renseigner cette donnée avant de générer le dépôt.");

        var enTete = new EnTeteDeclaration
        {
            IdentifiantSociete = identifiantFiscal.Trim(),
            Exercice = declaration.Exercice,
            Numero = declaration.Numero,
            Type = declaration.Type == Declaration.Application.Entities.TypePeriode.Mensuelle
                ? Core.Model.TypePeriode.Mensuelle
                : Core.Model.TypePeriode.Trimestrielle
        };
        if (declaration.Type == Declaration.Application.Entities.TypePeriode.Mensuelle)
            enTete.MoisPeriode = declaration.Periode;
        else
            enTete.TrimestrePeriode = declaration.Periode;

        var lignesDec = await _repository.GetLignesAsync(declarationId, "Decaissement", 1, int.MaxValue, null, null);
        var lignesEnc = await _repository.GetLignesAsync(declarationId, "Encaissement", 1, int.MaxValue, null, null);
        var lignesIntegrees = lignesDec.Concat(lignesEnc).Where(l => l.Etat == EtatLigne.Integree).ToList();

        var modele = new DeclarationModele { EnTete = enTete };
        foreach (var l in lignesIntegrees)
        {
            modele.Lignes.Add(new LigneDeclarationEnrichie
            {
                NumeroFacture = l.NumeroFacture,
                NumeroRapprochement = l.NumeroRapprochement,
                Designation = "",
                Tiers = new TiersInfo
                {
                    Nom = l.TiersNom,
                    IdentifiantFiscal = l.TiersIdentifiantFiscal,
                    Ice = l.TiersICE
                },
                // TASK-161 : gap TASK-155 comblé — CodeActivite résolu en cascade et persisté sur
                // la ligne au figeage (jamais null en pratique, cf. LigneCandidate.CodeActivite ;
                // "??" défensif pour une ligne figée avant la migration 010, colonne SQL NULL).
                CodeActivite = l.CodeActivite ?? "",
                HT = l.HT,
                Taux = l.Taux,
                Tva = l.TVA,
                Ttc = l.TTC,
                Prorata = l.Prorata,
                ModePaiement = l.ModePaiement,
                DatePaiement = l.DatePaiement,
                DateFacture = l.DateFacture,
                Source = Enum.Parse<SourceAffectation>(l.Source),
                IsReport = false
            });
        }

        return modele;
    }

    /// <summary>
    /// TASK-160 : construit le modèle de l'export de contrôle ad-hoc (règlements sélectionnés +
    /// factures à déclarer + détail TVA), réexécutable à tout moment avant clôture (EnCours ou
    /// Clôturée), à titre de vérification/partage externe — DISTINCT de
    /// <see cref="ConstruireModeleExportAsync"/> qui exige une déclaration Clôturée et ne prend
    /// que les lignes Integree. Aucun blocage sur l'état ni sur l'identifiant fiscal société : si
    /// le calcul n'a pas encore été lancé, Lignes/Recaps restent simplement vides (jamais une
    /// erreur), cohérent avec <see cref="GetCheckupAsync"/>.
    /// </summary>
    /// <exception cref="ArgumentException">Déclaration introuvable.</exception>
    public async Task<ModeleControle> ConstruireModeleControleAsync(Guid declarationId)
    {
        var declaration = await _repository.GetByIdAsync(declarationId);
        if (declaration == null) throw new ArgumentException("Déclaration introuvable");

        var modele = new ModeleControle
        {
            EnTete = new EnTeteDeclaration
            {
                IdentifiantSociete = declaration.SocieteId.ToString(),
                Exercice = declaration.Exercice,
                Numero = declaration.Numero,
                Type = declaration.Type == Declaration.Application.Entities.TypePeriode.Mensuelle
                    ? Core.Model.TypePeriode.Mensuelle
                    : Core.Model.TypePeriode.Trimestrielle
            }
        };
        if (declaration.Type == Declaration.Application.Entities.TypePeriode.Mensuelle)
            modele.EnTete.MoisPeriode = declaration.Periode;
        else
            modele.EnTete.TrimestrePeriode = declaration.Periode;

        // Règlements sélectionnés : jointure GetSelectionReglementsAsync (numéros) +
        // GetReglementsRapprochementAsync (détail), même pattern que ReglementsSelection.tsx.
        var selection = await _repository.GetSelectionReglementsAsync(declarationId);
        if (selection.Count > 0)
        {
            var selectionSet = new HashSet<string>(selection);
            var dateDebut = new DateTime(declaration.Exercice, declaration.Periode, 1);
            var dateFin = dateDebut.AddMonths(1).AddDays(-1);
            var reglements = await _repository.GetReglementsRapprochementAsync(
                declaration.SocieteId, dateDebut, dateFin,
                new RapprochementFilter(), 1, int.MaxValue, null);

            modele.ReglementsSelectionnes = reglements
                .Where(r => selectionSet.Contains(r.MvNumero))
                .Select(r => new ReglementSelectionneInfo
                {
                    Numero = r.MvNumero,
                    Date = r.MvDate,
                    Montant = r.MvMontant,
                    Tiers = r.Tiers ?? "",
                    Mode = r.Mode,
                    EtatPointage = r.EstRapprocheBanque
                        ? $"Rapproché{(r.DateRapprochement.HasValue ? $" ({r.DateRapprochement.Value:dd/MM/yyyy})" : "")}"
                        : "Non rapproché"
                })
                .ToList();
        }

        // Factures à déclarer : lignes Integree||Proposee (MÊME ensemble que GetCheckupAsync,
        // TASK-108) — pas seulement Integree, contrairement à ConstruireModeleExportAsync.
        var lignesDec = await _repository.GetLignesAsync(declarationId, "Decaissement", 1, int.MaxValue, null, null);
        var lignesEnc = await _repository.GetLignesAsync(declarationId, "Encaissement", 1, int.MaxValue, null, null);
        var lignesRecap = lignesDec.Concat(lignesEnc)
            .Where(l => l.Etat == EtatLigne.Integree || l.Etat == EtatLigne.Proposee)
            .ToList();

        foreach (var l in lignesRecap)
        {
            modele.Lignes.Add(new LigneDeclarationEnrichie
            {
                NumeroFacture = l.NumeroFacture,
                NumeroRapprochement = l.NumeroRapprochement,
                Designation = "",
                Tiers = new TiersInfo
                {
                    Nom = l.TiersNom,
                    IdentifiantFiscal = l.TiersIdentifiantFiscal,
                    Ice = l.TiersICE
                },
                // TASK-161 : gap TASK-155/TASK-160 comblé — cf. commentaire équivalent de
                // ConstruireModeleExportAsync ci-dessus.
                CodeActivite = l.CodeActivite ?? "",
                HT = l.HT,
                Taux = l.Taux,
                Tva = l.TVA,
                Ttc = l.TTC,
                Prorata = l.Prorata,
                ModePaiement = l.ModePaiement,
                DatePaiement = l.DatePaiement,
                DateFacture = l.DateFacture,
                Source = Enum.Parse<SourceAffectation>(l.Source),
                IsReport = false
            });
        }

        // Détail TVA : totaux par taux + contrôle d'équilibre, mêmes agrégats que GetCheckupAsync
        // (recapTaux/controleEquilibre côté DeclarationsController.GetCheckup, TASK-108).
        modele.RecapsParTaux = lignesRecap
            .GroupBy(l => l.Taux)
            .Select(g => new RecapParTaux
            {
                Taux = g.Key,
                TotalHT = g.Sum(x => x.HT),
                TotalTva = g.Sum(x => x.TVA),
                TotalTtc = g.Sum(x => x.TTC)
            })
            .OrderByDescending(r => r.Taux)
            .ToList();

        // TASK-161 : gap comblé — regroupement RÉEL par CodeActivite résolu (au lieu de l'ancien
        // bucket "" unique et systématique). Le bucket "" reste possible (et attendu, décision PO
        // TASK-161 point 3) pour toute ligne dont la cascade n'a rien résolu — jamais masqué,
        // simplement un groupe parmi d'autres désormais.
        modele.RecapsParActivite = lignesRecap
            .GroupBy(l => l.CodeActivite ?? "")
            .Select(g => new RecapParActivite
            {
                CodeActivite = g.Key,
                TotalHT = g.Sum(x => x.HT),
                TotalTva = g.Sum(x => x.TVA),
                TotalTtc = g.Sum(x => x.TTC)
            })
            .OrderBy(r => r.CodeActivite)
            .ToList();

        modele.ControleEquilibre = new ControleEquilibre
        {
            TotalMontantAffecte = lignesRecap.Sum(l => l.HT),
            TotalDeclareTtc = lignesRecap.Sum(l => l.TTC),
            ResiduExplique = 0m
        };

        return modele;
    }

    /// <summary>
    /// TASK-160 : génère le classeur Excel de contrôle en mémoire (flux, aucun fichier disque —
    /// à la différence de <see cref="GenererFichiersExportAsync"/>), pour téléchargement direct
    /// via <c>GET {id}/export-controle</c>.
    /// </summary>
    /// <exception cref="ArgumentException">Déclaration introuvable.</exception>
    public async Task<byte[]> GenererExcelControleAsync(Guid declarationId)
    {
        var modele = await ConstruireModeleControleAsync(declarationId);
        using var stream = new MemoryStream();
        new Declaration.Export.Excel.Exporter().ExporterExcelControle(modele, stream);
        return stream.ToArray();
    }

    /// <summary>
    /// TASK-155 : chemins déterministes des fichiers d'export d'une déclaration — dérivés
    /// uniquement de son Numero/Exercice/Type/Periode (même construction de nom que
    /// <see cref="Declaration.Export.Xml.DeclarationXmlExporter.GenererXml"/>), jamais d'un état
    /// persisté (aucune colonne "généré" en base, cf. cartographie TASK-155 point 7). Le fichier
    /// sur disque EST l'état — <see cref="GenererFichiersExportAsync"/> l'écrit,
    /// <see cref="ObtenirCheminsExportAsync"/> le relit pour le téléchargement.
    /// </summary>
    private static (string XmlZipPath, string ExcelPath) CalculerCheminsExport(DeclarationEntete declaration, string dossierSortie)
    {
        var periodeStr = declaration.Type == Declaration.Application.Entities.TypePeriode.Mensuelle
            ? $"M{declaration.Periode}"
            : $"T{declaration.Periode}";
        var baseFileName = $"{declaration.Numero}-{declaration.Exercice}-{periodeStr}";
        return (
            Path.Combine(dossierSortie, $"{baseFileName}.zip"),
            Path.Combine(dossierSortie, $"{baseFileName}-Checkup.xlsx")
        );
    }

    /// <summary>
    /// TASK-155 : dossier de sortie des exports — même pattern que <see cref="JournaliserValorisation"/>
    /// (<c>AppContext.BaseDirectory</c>, jamais un chemin absolu codé en dur, le service tournant en
    /// Windows Service).
    /// </summary>
    public static string ObtenirDossierExports() => Path.Combine(AppContext.BaseDirectory, "exports");

    /// <summary>
    /// TASK-155 : chemins des fichiers d'export d'une déclaration (générés ou non — l'appelant
    /// teste leur existence sur disque). Utilisé par <c>GET {id}/fichiers/{type}</c>.
    /// </summary>
    public async Task<(string XmlZipPath, string ExcelPath)> ObtenirCheminsExportAsync(Guid declarationId)
    {
        var declaration = await _repository.GetByIdAsync(declarationId);
        if (declaration == null) throw new ArgumentException("Déclaration introuvable");
        return CalculerCheminsExport(declaration, ObtenirDossierExports());
    }

    /// <summary>
    /// TASK-155 : câblage réel de <c>POST {id}/generation</c> — construit le modèle (point ci-dessus)
    /// puis appelle <see cref="Declaration.Export.Xml.DeclarationXmlExporter.GenererXml"/> et
    /// <see cref="Declaration.Export.Excel.Exporter.ExporterExcel"/> vers le même dossier
    /// déterministe. Le contrôle d'existence Excel est fait AVANT l'appel XML (qui a son propre
    /// contrôle interne) pour ne jamais écrire un XML si l'Excel existe déjà — au pire une seule
    /// écriture partielle reste possible (échec disque après XML, avant Excel), non traité comme
    /// une transaction (hors périmètre, cf. cartographie TASK-155 point 7 : pas de nouvel état
    /// persisté pour ce seul besoin).
    /// </summary>
    /// <exception cref="ArgumentException">Déclaration introuvable.</exception>
    /// <exception cref="InvalidOperationException">
    /// Déclaration non Clôturée, ou identifiant fiscal société absent (cf.
    /// <see cref="ConstruireModeleExportAsync"/>).
    /// </exception>
    /// <exception cref="ApplicationException">
    /// Fichier déjà existant, aucune ligne à exporter, ou IF/ICE tiers invalide (levées par
    /// l'exporter XML lui-même, TASK-137 — non dupliquées ici).
    /// </exception>
    public async Task<(string XmlZipPath, string ExcelPath)> GenererFichiersExportAsync(Guid declarationId)
    {
        var declaration = await _repository.GetByIdAsync(declarationId);
        if (declaration == null) throw new ArgumentException("Déclaration introuvable");
        if (declaration.Statut != StatutDeclaration.Cloturee)
            throw new InvalidOperationException("Seule une déclaration Clôturée peut être générée.");

        var modele = await ConstruireModeleExportAsync(declarationId);

        var dossierSortie = ObtenirDossierExports();
        Directory.CreateDirectory(dossierSortie);

        var (_, excelPath) = CalculerCheminsExport(declaration, dossierSortie);
        if (File.Exists(excelPath))
            throw new ApplicationException($"Le fichier {Path.GetFileName(excelPath)} existe déjà.");

        var xmlExporter = new Declaration.Export.Xml.DeclarationXmlExporter();
        var xmlZipPath = xmlExporter.GenererXml(modele, dossierSortie);

        new Declaration.Export.Excel.Exporter().ExporterExcel(modele, excelPath);

        JournaliserValorisation($"[TASK-155] Déclaration {declaration.Numero} ({declarationId}) — fichiers générés : {Path.GetFileName(xmlZipPath)}, {Path.GetFileName(excelPath)}.");

        return (xmlZipPath, excelPath);
    }

    /// <summary>
    /// TASK-144 : diagnostic explicatif en ligne d'une ligne en anomalie. LECTURE SEULE STRICTE —
    /// aucune écriture, aucun recalcul de valorisation, aucune NOUVELLE lecture OM Sage (le motif
    /// d'échec déjà tenté est relu depuis le cache DM_VENTILATION_SAGE_CACHE). Le seul accès Sage
    /// est un SELECT sur F_DOCREGL (contrôle collision DO_Numero), base résolue dynamiquement par
    /// SO_Id (TASK-118). Retourne null si l'échéance est introuvable pour cette déclaration.
    /// </summary>
    public async Task<DiagnosticLigneResultat?> DiagnostiquerLigneAsync(Guid declarationId, int ecId)
    {
        var declaration = await _repository.GetByIdAsync(declarationId);
        if (declaration == null) throw new ArgumentException("Déclaration introuvable");
        if (ecId <= 0) return null;

        var soId = declaration.SocieteId;

        // Bloc 1 — identité de l'échéance consultée (RT_ECHEANCE, base GRF, lecture seule).
        var echeance = await _repository.GetEcheanceDiagnosticAsync(soId, ecId);
        if (echeance == null) return null;

        // Bloc 2 — motif de l'échec de valorisation OM déjà persisté (cache), jamais relu côté Sage.
        var motifCache = await _repository.GetMotifErreurCacheAsync(soId, ecId);

        // Motif technique de la ligne elle-même (message posé au recalcul), pour la traduction métier.
        var motifLigne = await ResoudreMotifLigneAsync(declarationId, ecId);
        var traduction = DiagnosticMotifMetier.Traduire(motifLigne, motifCache);

        var resultat = new DiagnosticLigneResultat
        {
            EC_Id = echeance.EC_Id,
            EC_No = echeance.EC_No,
            DoNumero = echeance.DO_Numero,
            TiersCode = echeance.CT_Code,
            TiersIntitule = echeance.CT_Intitule,
            MontantDevise = echeance.EC_MtDevise,
            Origine = ReglementRapprochementRow.LibelleEcType(echeance.EC_Type),
            MotifTechnique = string.IsNullOrWhiteSpace(motifLigne) ? (motifCache ?? "") : motifLigne,
            MotifErreurCache = motifCache,
            ExplicationMetier = traduction.Explication,
            ActionRecommandee = traduction.Action,
            CodeMotifReconnu = traduction.Code
        };

        // Bloc 3 — contrôle collision DO_Numero, INDÉPENDANT du bloc 2. On ne le présente jamais
        // comme la cause automatique de l'anomalie (cf. cas réel FA2600106, TASK-143).
        var memeNumero = await _repository.GetEcheancesMemeDoNumeroAsync(soId, echeance.DO_Numero);
        if (memeNumero.Count > 1)
        {
            resultat.CollisionDetectee = true;

            // Verdict F_DOCREGL : lecture seule sur la base Sage résolue dynamiquement par SO_Id.
            var sageInfo = await _connectionFactory.GetSageConnectionInfoAsync(soId);
            var ecNos = memeNumero.Select(e => e.EC_No).ToList();
            var docs = await _repository.GetDocumentsReglementSageAsync(sageInfo.ConnectionString, ecNos);

            foreach (var e in memeNumero)
            {
                docs.TryGetValue(e.EC_No, out var doc);
                resultat.Collisions.Add(new DiagnosticCollisionEcheance
                {
                    EC_Id = e.EC_Id,
                    EC_No = e.EC_No,
                    TiersCode = e.CT_Code,
                    TiersIntitule = e.CT_Intitule,
                    ADocumentSage = doc != null,
                    DoPieceSage = doc?.DO_Piece,
                    DateDocSage = doc?.DR_Date,
                    EstLigneConsultee = e.EC_Id == ecId
                });
            }

            var consultee = resultat.Collisions.First(c => c.EstLigneConsultee);
            var nbAutresAvecDoc = resultat.Collisions.Count(c => !c.EstLigneConsultee && c.ADocumentSage);
            var nbOrphelins = resultat.Collisions.Count(c => !c.ADocumentSage);
            resultat.CollisionCommentaire = DiagnosticMotifMetier.CommentaireCollision(
                consultee.ADocumentSage, nbAutresAvecDoc, nbOrphelins);
        }

        // Bloc 4 (TASK-147) — cache PÉRIMÉ : Sage a relu cette pièce APRÈS la création de la
        // déclaration, avec succès (MotifErreur NULL), alors que la ligne affiche encore le motif
        // de rejet figé à la création. Lecture seule stricte (aucune nouvelle lecture Sage) —
        // réutilise la même dernière lecture déjà relue pour le bloc 2 ci-dessus.
        var derniereLecture = await _repository.GetDerniereLectureCacheAsync(soId, ecId);
        if (derniereLecture != null
            && derniereLecture.MotifErreur == null
            && derniereLecture.DateLecture > declaration.DateCreation)
        {
            resultat.CachePerime = true;
            resultat.CacheDateLecture = derniereLecture.DateLecture;
            resultat.CachePerimeCommentaire =
                $"Sage a relu cette facture avec succès le {derniereLecture.DateLecture:dd/MM/yyyy HH:mm} — "
                + "après la création de cette déclaration. Un recalcul de cette ligne devrait résoudre l'anomalie.";
        }

        return resultat;
    }

    /// <summary>
    /// TASK-147 : recalcule UNE ligne Proposee dont le cache de ventilation a été relu avec succès
    /// APRÈS la création de la déclaration (cache PÉRIMÉ, cf. <see cref="DiagnostiquerLigneAsync"/>
    /// bloc 4). Ne redéclenche AUCUNE nouvelle lecture OM Sage — reconstruit la (les) ligne(s)
    /// candidate(s) directement depuis les buckets déjà en cache, à l'identique de la branche
    /// "taxesLines" de <see cref="MapLignesCandidates"/> (aucune règle de valorisation dupliquée,
    /// seule la source du bucket change : cache déjà persisté au lieu d'un run d'orchestrateur).
    /// Garde-fou : n'écrit RIEN si le cache n'est pas effectivement plus récent que la déclaration
    /// et sans erreur (mêmes conditions que le diagnostic) — jamais de redéclenchement OM ici.
    /// </summary>
    public async Task<(bool Trouvee, bool Recalculee, string Message)> RecalculerLigneDepuisCacheAsync(Guid declarationId, int ecId)
    {
        var declaration = await _repository.GetByIdAsync(declarationId);
        if (declaration == null) return (false, false, "Déclaration introuvable.");
        if (ecId <= 0) return (false, false, "EC_Id invalide.");

        var lignesDec = await _repository.GetLignesAsync(declarationId, "Decaissement", 1, int.MaxValue, null, null);
        var lignesEnc = await _repository.GetLignesAsync(declarationId, "Encaissement", 1, int.MaxValue, null, null);
        var lignes = lignesDec.Concat(lignesEnc).Where(l => l.EC_Id == ecId).ToList();
        if (lignes.Count == 0) return (false, false, $"Aucune ligne trouvée pour EC_Id={ecId} sur cette déclaration.");

        // TASK-147 : action bornée aux lignes encore Proposee (pas Integree/Exclue/Reportée/Ecartée
        // — un recalcul sur une ligne déjà figée sortirait du périmètre de cette TASK, cf. garde-fou).
        if (lignes.Any(l => l.Etat != EtatLigne.Proposee))
            return (false, false, "Cette action ne s'applique qu'à une ligne encore à l'état 'Proposée'.");

        var soId = declaration.SocieteId;
        var derniereLecture = await _repository.GetDerniereLectureCacheAsync(soId, ecId);
        if (derniereLecture == null || derniereLecture.MotifErreur != null || derniereLecture.DateLecture <= declaration.DateCreation)
            return (false, false, "Le cache n'est pas plus récent (ou toujours en erreur) — rien à recalculer sans nouvelle lecture Sage.");

        var buckets = await _repository.GetBucketsCacheAsync(soId, ecId);
        if (buckets.Count == 0)
            return (false, false, "Le cache est marqué à jour sans erreur mais ne porte aucun bucket de taux exploitable — incohérence à signaler, aucune écriture effectuée.");

        // Reconstruction : les colonnes non financières (facture/tiers/paiement/source) sont
        // conservées à l'identique de la ligne existante, seuls HT/Taux/TVA/TTC/Etat/MotifRejet
        // changent — même forme que la branche "taxesLines" de MapLignesCandidates.
        var reference = lignes[0];
        var nouvellesLignes = buckets.Select(b => new LigneCandidate
        {
            Id = Guid.NewGuid(),
            DeclarationId = declarationId,
            Etat = EtatLigne.Proposee,
            Domaine = reference.Domaine,
            MotifRejet = "",
            NumeroFacture = reference.NumeroFacture,
            NumeroRapprochement = reference.NumeroRapprochement,
            TiersNom = reference.TiersNom,
            TiersIdentifiantFiscal = reference.TiersIdentifiantFiscal,
            TiersICE = reference.TiersICE,
            HT = b.HT,
            Taux = b.Taux,
            TVA = b.Tva,
            TTC = b.TTC,
            Prorata = 0,
            MontantAffecte = reference.MontantAffecte,
            ModePaiement = reference.ModePaiement,
            DatePaiement = reference.DatePaiement,
            DateFacture = reference.DateFacture,
            Source = reference.Source,
            EcType = reference.EcType,
            EC_Id = reference.EC_Id,
            MV_Id = reference.MV_Id,
            // TASK-161 : CodeActivite est une colonne non-financière (identité tiers, pas un
            // montant recalculé depuis le cache) — conservée à l'identique de la ligne existante,
            // même principe que TiersNom/IF/ICE/ModePaiement/Source ci-dessus. Contrairement à
            // IncoherenceValidee (remis à false ailleurs sur resynchronisation, TASK-078), rien
            // ici ne rend une éventuelle surcharge manuelle caduque : seuls les montants de taxe
            // ont changé, pas l'identité du tiers ni sa surcharge.
            CodeActivite = reference.CodeActivite,
            CodeActiviteModifieManuellement = reference.CodeActiviteModifieManuellement,
            CodeActiviteModifiePar = reference.CodeActiviteModifiePar,
            CodeActiviteModifieLe = reference.CodeActiviteModifieLe
        }).ToList();

        await _repository.SupprimerLignesParEcIdAsync(declarationId, ecId);
        await _repository.SaveLignesCandidatesAsync(nouvellesLignes);

        return (true, true, $"Ligne recalculée depuis le cache (lecture du {derniereLecture.DateLecture:dd/MM/yyyy HH:mm}) — {nouvellesLignes.Count} bucket(s) de taux.");
    }

    /// <summary>
    /// TASK-144 : retrouve le message de motif (MotifRejet) porté par la ligne candidate de cette
    /// déclaration pour l'EC_Id donné, tous domaines confondus. Lecture seule.
    /// </summary>
    private async Task<string?> ResoudreMotifLigneAsync(Guid declarationId, int ecId)
    {
        var dec = await _repository.GetLignesAsync(declarationId, "Decaissement", 1, int.MaxValue, null, null);
        var enc = await _repository.GetLignesAsync(declarationId, "Encaissement", 1, int.MaxValue, null, null);
        var ligne = dec.Concat(enc).FirstOrDefault(l => l.EC_Id == ecId && !string.IsNullOrWhiteSpace(l.MotifRejet));
        return ligne?.MotifRejet;
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
        var toutesDec = await _repository.GetLignesAsync(declarationId, "Decaissement", 1, int.MaxValue, null, null);
        var toutesEnc = await _repository.GetLignesAsync(declarationId, "Encaissement", 1, int.MaxValue, null, null);
        var toutes = toutesDec.Concat(toutesEnc).ToList();
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

        // ── TASK-094 (Option B) : copie la même valeur sur DM_ENTTVA.DT_Id — posée
        // inconditionnellement (même sans affectation à tamponner), pour que le diagnostic
        // (DiagnostiquerDtIdAsync) dispose toujours de la valeur RÉELLEMENT posée par cet appel,
        // sans dépendre d'un recalcul de DeriveDtId potentiellement exécuté par un runtime .NET
        // différent (cf. incident 14/07/2026).
        await _repository.SetDtIdDeclarationAsync(declarationId, dtId);
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

        // ── TASK-094 (Option B) : efface DM_ENTTVA.DT_Id inconditionnellement, symétrique de la
        // pose — même si aucune affectation n'a pu être détamponnée (ex. tampon RT_AFFECTATION
        // déjà absent avant réouverture), la déclaration n'est plus Cloturee et ne doit plus
        // porter un DT_Id qui ne correspond plus à rien.
        await _repository.SetDtIdDeclarationAsync(declarationId, null);

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
        var lignesDec = await _repository.GetLignesAsync(
            declarationId, "Decaissement", 1, int.MaxValue, null, null);
        var lignesEnc = await _repository.GetLignesAsync(
            declarationId, "Encaissement", 1, int.MaxValue, null, null);
        var lignes = lignesDec.Concat(lignesEnc).ToList();
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
    /// Réutilisé tel quel (TASK-094) par <see cref="DiagnostiquerDtIdAsync"/> — aucune seconde
    /// implémentation du calcul.
    /// </summary>
    private static int DeriveDtId(Guid id) => id.GetHashCode() & int.MaxValue;

    /// <summary>
    /// TASK-094 — diagnostic lecture seule d'un ou plusieurs tampons DT_Id observés sur
    /// RT_AFFECTATION : identifie, pour chaque DT_Id, la déclaration correspondante ou un
    /// <see cref="DiagnosticDtIdResultat.Orphelin"/> explicite si aucune ne matche (déclaration
    /// disparue, donnée de test, ou tout autre écart — cf. incident 14/07/2026 `TVA1-2026-01`).
    /// Transforme en requête reproductible l'archéologie SQL manuelle faite lors de cet incident.
    /// Priorité (Option B) à la valeur RÉELLEMENT posée <see cref="DeclarationEntete.DT_Id"/> quand
    /// elle est renseignée (immunise contre toute divergence de <see cref="DeriveDtId"/> entre
    /// runtimes .NET, cf. incident) ; à défaut (déclaration close avant la migration TASK-094),
    /// repli sur le recalcul (Option A) — best-effort documenté, pas une garantie.
    /// Aucune écriture : ni sur RT_AFFECTATION/RT_MOUVEMENT, ni sur DM_ENTTVA.
    /// </summary>
    /// <param name="dtIds">
    /// DT_Id à diagnostiquer. Si null/vide, diagnostique TOUTES les valeurs DT_Id distinctes
    /// actuellement présentes sur RT_AFFECTATION (périmètre complet, usage incident/audit).
    /// </param>
    public async Task<IReadOnlyList<DiagnosticDtIdResultat>> DiagnostiquerDtIdAsync(IEnumerable<int>? dtIds = null)
    {
        var cibles = dtIds?.Distinct().ToList() ?? new List<int>();
        if (cibles.Count == 0)
            cibles = (await _repository.GetDistinctDtIdsAffectationsAsync()).Distinct().ToList();

        var declarations = await _repository.GetToutesDeclarationsAsync();

        // Regroupement défensif (collision DeriveDtId théoriquement possible, TASK-028) — ne
        // masque jamais un DT_Id ambigu derrière une seule correspondance choisie arbitrairement.
        var parHash = declarations
            .GroupBy(d => d.DT_Id ?? DeriveDtId(d.Id))
            .ToDictionary(g => g.Key, g => g.ToList());

        return cibles.Select(dtId =>
        {
            if (parHash.TryGetValue(dtId, out var matches) && matches.Count > 0)
            {
                var d = matches[0];
                return new DiagnosticDtIdResultat
                {
                    DtId = dtId,
                    Orphelin = false,
                    DeclarationId = d.Id,
                    DeclarationNumero = d.Numero,
                    DeclarationStatut = d.Statut
                };
            }
            return new DiagnosticDtIdResultat { DtId = dtId, Orphelin = true };
        }).ToList();
    }

    /// <summary>
    /// Numéros de rapprochement distincts des lignes intégrées de la déclaration
    /// (socle du tamponnage/dé-tamponnage RT_AFFECTATION).
    /// </summary>
    private async Task<List<string>> GetNumerosRapprochementIntegresAsync(Guid declarationId)
    {
        var lignesDec = await _repository.GetLignesAsync(
            declarationId, "Decaissement", 1, int.MaxValue, null, null);
        var lignesEnc = await _repository.GetLignesAsync(
            declarationId, "Encaissement", 1, int.MaxValue, null, null);
        var lignes = lignesDec.Concat(lignesEnc).ToList();
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

/// <summary>
/// TASK-176 : retour agrégé d'une resynchronisation en masse (une pièce EC_Id resynchronisée par
/// itération, séquentiellement). Permet un affichage synthétique côté front sans corréler N réponses.
/// </summary>
public class ResynchroBulkResultat
{
    /// <summary>Nombre de pièces (EC_Id distincts) résolues par la sélection.</summary>
    public int TotalSelection { get; set; }
    /// <summary>Pièces effectivement relues depuis Sage (verrou acquis, relecture faite).</summary>
    public int Traitees { get; set; }
    /// <summary>Pièces désormais résolues (plus en erreur après relecture).</summary>
    public int Resolues { get; set; }
    /// <summary>Pièces sélectionnées mais introuvables sur la déclaration (ignorées, non bloquant).</summary>
    public int NonTrouvees { get; set; }
    /// <summary>Pièces toujours en anomalie après relecture, avec leur motif (relu du cache).</summary>
    public List<ResynchroLigneAnomalie> ToujoursEnAnomalie { get; set; } = new();
    /// <summary>true si un verrou soId concurrent a interrompu la boucle avant la fin (TASK-156).</summary>
    public bool Interrompu { get; set; }
    /// <summary>Message d'interruption (verrou concurrent) le cas échéant.</summary>
    public string? MessageInterruption { get; set; }
}

/// <summary>TASK-176 : une pièce encore en anomalie après resynchronisation en masse.</summary>
public class ResynchroLigneAnomalie
{
    public int EcId { get; set; }
    public string NumeroFacture { get; set; } = "";
    public string Motif { get; set; } = "";
}
