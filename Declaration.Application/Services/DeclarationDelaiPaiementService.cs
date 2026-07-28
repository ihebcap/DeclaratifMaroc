using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Declaration.Application.Entities;
using Declaration.Application.Interfaces;
using Declaration.Core;

namespace Declaration.Application.Services;

/// <summary>
/// TASK-132 : requête de création d'une déclaration Délai de Paiement. Reproduit les entrées de
/// l'objectif TASK-132 (« société, exercice, type, trimestre si applicable, libellé ») —
/// <b>volontairement PAS de numéro ni de bornes de période</b> : le numéro est attribué par le
/// serveur (<see cref="NumerotationDeclarationDelaiPaiement"/>) et les bornes sont CALCULÉES
/// (<see cref="DeclarationDelaiPaiementCycleDeVie.CalculerPeriode"/>), jamais saisies.
/// </summary>
public sealed class CreerDeclarationDelaiPaiementRequest
{
    public required int SocieteId { get; init; }
    public required int Exercice { get; init; }
    public required TypeDeclarationDelaiPaiement Type { get; init; }

    /// <summary>Obligatoire si et seulement si <see cref="Type"/> = Trimestrielle.</summary>
    public TrimestreDelaiPaiement? Trimestre { get; init; }

    public string? Libelle { get; init; }

    /// <summary><c>DDP_Date</c> ; <c>null</c> ⇒ maintenant (comportement du formulaire legacy).</summary>
    public DateTime? Date { get; init; }

    /// <summary>
    /// <c>UT_Id</c> créateur (<c>P_UTILISATEUR.UT_Id</c>, claim JWT « UT_Id » posé au login).
    /// Obligatoire et strictement positif : les colonnes <c>UT_Id</c>/<c>UT_IdModif</c> sont NOT NULL
    /// et l'audit trail est obligatoire (ARCHITECTURE §5) — jamais de 0 « utilisateur inconnu » écrit
    /// en base.
    /// </summary>
    public required int UtilisateurId { get; init; }
}

/// <summary>
/// TASK-132 — Cycle de vie complet de la déclaration Délai de Paiement Maroc + contrôle IF/ICE
/// bloquant, sur les tables EXISTANTES <c>RT_DECLARATIONDELAISPAIEMENT</c>/<c>…LG</c>.
///
/// Reproduit <c>SocieteManager.Complement.cs:601-969</c> (création / update libellé / clôture /
/// déclôture / dépôt / génération fichier / annulation génération / suppression / ajout-suppression de
/// ligne). Tout le métier PUR (bornes de période, gardes de transition, numérotation, contrôle IF/ICE)
/// est délégué à <c>Declaration.Core</c> (<see cref="DeclarationDelaiPaiementCycleDeVie"/>,
/// <see cref="NumerotationDeclarationDelaiPaiement"/>,
/// <see cref="ControleIdentiteFiscaleDelaiPaiement"/>) : ce service n'apporte que l'orchestration des
/// lectures/écritures.
///
/// L'intégration de lignes consomme <see cref="ISelectionDelaiPaiementService"/> (TASK-131) et
/// n'intègre QUE ses <c>Lignes</c> — les <c>LignesRepriseManuelleRequise</c> sont REFUSÉES à
/// l'intégration (aucun <c>Depassement</c> calculé, décision PO) tout en restant comptées dans le
/// compte rendu pour l'écran (TASK-134).
///
/// <b>Hors périmètre STRICT et donc absents ici</b> : la génération XML/ZIP elle-même (TASK-133 —
/// ce service se contente d'AUTORISER puis de poser <c>DDP_IsGeneretedFile</c>) et l'UI/les endpoints
/// (TASK-134).
/// </summary>
public interface IDeclarationDelaiPaiementService
{
    // ─── CRUD entête ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Création : bornes calculées automatiquement, contrôle d'unicité de période, numéro attribué par
    /// le serveur. Retourne le <c>DDP_Id</c> créé.
    /// </summary>
    Task<int> CreerAsync(CreerDeclarationDelaiPaiementRequest request);

    Task<DeclarationDelaiPaiement?> GetAsync(int ddpId);

    Task<IReadOnlyList<DeclarationDelaiPaiementListItem>> GetAllAsync(int soId);

    /// <summary>
    /// TASK-134 : paramétrage société lu en LECTURE SEULE sur <c>P_SOCIETE</c> — utilisé par l'écran
    /// pour proposer le type de déclaration par défaut (<c>SO_TypeDecDP</c>, CDC §7.1). Ajout
    /// strictement additif : aucun contrôle métier n'en dépend (le type effectif reste choisi par
    /// l'utilisateur puis validé par <see cref="DeclarationDelaiPaiementCycleDeVie.CalculerPeriode"/>).
    /// </summary>
    Task<SocieteDelaiPaiementInfo> GetParametrageSocieteAsync(int soId);

    /// <summary>Seule modification autorisée après création (legacy <c>Update</c>) : le libellé.</summary>
    Task ModifierLibelleAsync(int ddpId, string? libelle, int utilisateurId);

    /// <summary>Suppression — garde EnCours + AUCUNE ligne intégrée (legacy <c>Delete</c>).</summary>
    Task SupprimerAsync(int ddpId, int utilisateurId);

    // ─── Lignes ────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Intègre les lignes sélectionnées par TASK-131 pour la période de la déclaration.
    /// <paramref name="selection"/> = <c>null</c> ⇒ toutes les lignes candidates ; sinon uniquement les
    /// clés demandées. Une clé en « reprise manuelle requise » est REFUSÉE et reportée, jamais écrite.
    /// </summary>
    Task<ResultatIntegrationLignesDelaiPaiement> IntegrerLignesAsync(
        int ddpId,
        int utilisateurId,
        IReadOnlyCollection<CleLigneDelaiPaiement>? selection = null);

    Task<IReadOnlyList<LigneDeclarationDelaiPaiement>> GetLignesAsync(int ddpId);

    /// <summary>Retire UNE ligne intégrée (legacy <c>DeleteLigne</c>) — nécessaire pour pouvoir supprimer une déclaration.</summary>
    Task SupprimerLigneAsync(int ddpId, int ddplId, int utilisateurId);

    // ─── Cycle de vie ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Clôture — garde « au moins une ligne intégrée » (legacy <c>Cloture</c>).</summary>
    Task CloturerAsync(int ddpId, int utilisateurId);

    /// <summary>Déclôture — possible tant que le fichier n'est pas généré (legacy <c>AnnulerCloture</c>).</summary>
    Task AnnulerClotureAsync(int ddpId, int utilisateurId);

    /// <summary>
    /// Contrôle IF/ICE de TOUTES les lignes de la déclaration, SANS effet de bord.
    /// <b>Point d'entrée réutilisable par TASK-133</b> : à appeler avant d'écrire quoi que ce soit.
    /// </summary>
    Task<ResultatControleIdentiteFiscaleDelaiPaiement> ControlerIdentiteFiscaleAsync(int ddpId);

    /// <summary>
    /// Vérifie que la génération de fichier est autorisée : garde d'état (legacy) PUIS contrôle IF/ICE
    /// bloquant. Lève une <see cref="InvalidOperationException"/> avec le message listant les
    /// fournisseurs fautifs si le contrôle échoue. <b>TASK-133 doit appeler ceci en premier</b> —
    /// aucun octet ne doit être écrit avant un retour sans exception.
    /// </summary>
    Task<ResultatControleIdentiteFiscaleDelaiPaiement> VerifierGenerationFichierAutoriseeAsync(int ddpId);

    /// <summary>
    /// Pose <c>DDP_IsGeneretedFile</c> APRÈS écriture réussie du fichier par TASK-133. Re-valide
    /// intégralement (état + IF/ICE) avant d'écrire le flag — défense en profondeur.
    /// </summary>
    Task MarquerFichierGenereAsync(int ddpId, int utilisateurId);

    /// <summary>Annule le flag « fichier généré » (legacy <c>FichierAnnulerGeneration</c>).</summary>
    Task AnnulerGenerationFichierAsync(int ddpId, int utilisateurId);

    /// <summary>
    /// Dépôt = FLAG MANUEL <c>DDP_IsDepose</c> posé après export (décision PO §5.A-2 du 19/07/2026) :
    /// AUCUN appel à une plateforme externe n'est fait ici, ni ailleurs dans ce périmètre.
    /// </summary>
    Task MarquerDeposeAsync(int ddpId, int utilisateurId);
}

/// <inheritdoc cref="IDeclarationDelaiPaiementService"/>
public sealed class DeclarationDelaiPaiementService : IDeclarationDelaiPaiementService
{
    private readonly IDeclarationDelaiPaiementRepository _repository;
    private readonly ISelectionDelaiPaiementService _selection;

    public DeclarationDelaiPaiementService(
        IDeclarationDelaiPaiementRepository repository,
        ISelectionDelaiPaiementService selection)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _selection = selection ?? throw new ArgumentNullException(nameof(selection));
    }

    // ─── CRUD entête ───────────────────────────────────────────────────────────────────────────────

    public async Task<int> CreerAsync(CreerDeclarationDelaiPaiementRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        ExigerUtilisateur(request.UtilisateurId);
        if (request.SocieteId <= 0) throw new ArgumentException("La société est obligatoire.", nameof(request.SocieteId));

        // Bornes CALCULÉES (jamais saisies) — legacy l.617-653.
        var periode = DeclarationDelaiPaiementCycleDeVie.CalculerPeriode(request.Exercice, request.Type, request.Trimestre);
        var periodePersistee = DeclarationDelaiPaiementCycleDeVie.ResoudrePeriodePersistee(request.Type, request.Trimestre);

        // Unicité de période (legacy l.655 + l.658-660), comparaison au JOUR.
        var existantes = await _repository.GetAllParExerciceAsync(request.SocieteId, request.Exercice);
        var conflit = DeclarationDelaiPaiementCycleDeVie.TrouverPeriodeEnConflit(
            request.Exercice,
            periode,
            existantes.Select(d => new DeclarationDelaiPaiementExistanteResume
            {
                DdpId = d.DdpId,
                Numero = d.Numero,
                Exercice = d.Exercice,
                DateDebut = d.DateDebut,
                DateFin = d.DateFin
            }));

        if (conflit != null)
            throw new InvalidOperationException(
                $"Déclaration existe déjà pour la même période (déclaration [{conflit.Numero}], " +
                $"{conflit.DateDebut:dd/MM/yyyy} - {conflit.DateFin:dd/MM/yyyy}).");

        // Numéro attribué par le serveur, à partir du paramétrage société existant (P_SOCIETE).
        var date = request.Date ?? DateTime.Now;
        var configuration = await _repository.GetConfigurationNumerotationAsync(request.SocieteId);
        var pattern = NumerotationDeclarationDelaiPaiement.ConstruirePatternLike(configuration, date);
        var dernierNumero = await _repository.GetDernierNumeroAsync(request.SocieteId, pattern);
        var numero = NumerotationDeclarationDelaiPaiement.ResoudreProchainNumero(configuration, date, dernierNumero);

        // Legacy l.610 : garde d'unicité du numéro (aucun index unique disponible — table winform,
        // ajout de contrainte interdit ; cf. VERIFY §Limites).
        if (await _repository.ExisteNumeroAsync(request.SocieteId, numero))
            throw new InvalidOperationException($"La déclaration [{numero}] existe déjà.");

        var maintenant = DateTime.Now;

        return await _repository.CreerEnteteAsync(new DeclarationDelaiPaiement
        {
            Numero = numero,
            SocieteId = request.SocieteId,
            Date = date,
            Exercice = request.Exercice,
            Type = request.Type,
            DateDebut = periode.DateDebut.Date,
            // Convention legacy reproduite (l.674) : fin de journée du dernier jour de période.
            DateFin = BorneFinDeJournee(periode.DateFin),
            Statut = StatutDeclarationDelaiPaiement.EnCours,
            DateCreation = maintenant,
            CreateurId = request.UtilisateurId,
            DateModification = maintenant,
            ModificateurId = request.UtilisateurId,
            EstDeposee = false,
            Libelle = request.Libelle,
            FichierGenere = false,
            Periode = periodePersistee
        });
    }

    public Task<DeclarationDelaiPaiement?> GetAsync(int ddpId) => _repository.GetEnteteAsync(ddpId);

    public Task<IReadOnlyList<DeclarationDelaiPaiementListItem>> GetAllAsync(int soId) => _repository.GetAllAsync(soId);

    public Task<SocieteDelaiPaiementInfo> GetParametrageSocieteAsync(int soId) => _repository.GetSocieteInfoAsync(soId);

    public async Task ModifierLibelleAsync(int ddpId, string? libelle, int utilisateurId)
    {
        ExigerUtilisateur(utilisateurId);
        var (entete, etat) = await ChargerAsync(ddpId);
        DeclarationDelaiPaiementCycleDeVie.ValiderModificationLibelle(etat);

        await _repository.MettreAJourEtatAsync(
            ddpId, entete.Statut, entete.EstDeposee, entete.FichierGenere, libelle, utilisateurId, DateTime.Now);
    }

    public async Task SupprimerAsync(int ddpId, int utilisateurId)
    {
        ExigerUtilisateur(utilisateurId);
        var (_, etat) = await ChargerAsync(ddpId);
        DeclarationDelaiPaiementCycleDeVie.ValiderSuppression(etat);

        // Le repository revérifie « 0 ligne » DANS la transaction du DELETE (course entre utilisateurs).
        await _repository.SupprimerEnteteAsync(ddpId);
    }

    // ─── Lignes ────────────────────────────────────────────────────────────────────────────────────

    public async Task<ResultatIntegrationLignesDelaiPaiement> IntegrerLignesAsync(
        int ddpId,
        int utilisateurId,
        IReadOnlyCollection<CleLigneDelaiPaiement>? selection = null)
    {
        ExigerUtilisateur(utilisateurId);
        var (entete, etat) = await ChargerAsync(ddpId);
        DeclarationDelaiPaiementCycleDeVie.ValiderIntegrationLignes(etat);

        // TASK-131 : les bornes viennent TOUJOURS de la déclaration (jamais d'une plage libre).
        var resultatSelection = await _selection.SelectionnerAsync(entete.SocieteId, entete.DateDebut, entete.DateFin);

        var candidatesParCle = new Dictionary<CleLigneDelaiPaiement, LigneSelectionDelaiPaiement>();
        foreach (var ligne in resultatSelection.Lignes)
        {
            // Dédoublonnage défensif : la première occurrence gagne, aucune ligne écrite deux fois.
            candidatesParCle.TryAdd(new CleLigneDelaiPaiement(ligne.EcId, ligne.AfId), ligne);
        }

        var clesRepriseManuelle = new HashSet<CleLigneDelaiPaiement>(
            resultatSelection.LignesRepriseManuelleRequise.Select(l => new CleLigneDelaiPaiement(l.EcId, l.AfId)));

        var dejaIntegrees = new HashSet<CleLigneDelaiPaiement>(await _repository.GetClesLignesAsync(ddpId));

        var aIntegrer = new List<LigneSelectionDelaiPaiement>();
        var clesDejaIntegrees = new List<CleLigneDelaiPaiement>();
        var clesRefusees = new List<CleLigneDelaiPaiement>();
        var clesIntrouvables = new List<CleLigneDelaiPaiement>();

        if (selection == null)
        {
            foreach (var paire in candidatesParCle)
            {
                if (dejaIntegrees.Contains(paire.Key)) clesDejaIntegrees.Add(paire.Key);
                else aIntegrer.Add(paire.Value);
            }
        }
        else
        {
            foreach (var cle in selection.Distinct())
            {
                if (dejaIntegrees.Contains(cle)) { clesDejaIntegrees.Add(cle); continue; }

                // Garde-fou TASK-128/131 : jamais intégrable sans Depassement calculé (décision PO).
                if (clesRepriseManuelle.Contains(cle)) { clesRefusees.Add(cle); continue; }

                if (candidatesParCle.TryGetValue(cle, out var ligne)) aIntegrer.Add(ligne);
                else clesIntrouvables.Add(cle);
            }
        }

        var lignesAEcrire = aIntegrer
            .Select(l => new LigneAIntegrerDelaiPaiement
            {
                EcId = l.EcId,
                AfId = l.AfId,
                // Invariant TASK-131 : une ligne candidate porte TOUJOURS un Depassement > 0.
                Depassement = l.Depassement
                    ?? throw new InvalidOperationException(
                        $"Ligne candidate sans dépassement calculé ({new CleLigneDelaiPaiement(l.EcId, l.AfId)}) : " +
                        "intégration refusée (invariant TASK-131 violé)."),
                EcheanceLegale = l.EcheanceLegale
            })
            .ToList();

        var nombreIntegrees = await _repository.AjouterLignesAsync(ddpId, lignesAEcrire);

        if (nombreIntegrees > 0)
        {
            // Audit trail (ARCHITECTURE §5) : le legacy ne touchait PAS aux tampons de modification en
            // ajoutant une ligne — ajout assumé, documenté en VERIFY (aucune autre colonne touchée).
            await _repository.MettreAJourEtatAsync(
                ddpId, entete.Statut, entete.EstDeposee, entete.FichierGenere, entete.Libelle, utilisateurId, DateTime.Now);
        }

        return new ResultatIntegrationLignesDelaiPaiement
        {
            DdpId = ddpId,
            NombreCandidates = candidatesParCle.Count,
            NombreIntegrees = nombreIntegrees,
            ClesDejaIntegrees = clesDejaIntegrees,
            ClesRefuseesRepriseManuelleRequise = clesRefusees,
            ClesIntrouvablesDansSelection = clesIntrouvables,
            NombreRepriseManuelleRequiseDisponibles = resultatSelection.LignesRepriseManuelleRequise.Count,
            DateMiseEnRouteSociete = resultatSelection.DateMiseEnRouteSociete
        };
    }

    public Task<IReadOnlyList<LigneDeclarationDelaiPaiement>> GetLignesAsync(int ddpId) => _repository.GetLignesAsync(ddpId);

    public async Task SupprimerLigneAsync(int ddpId, int ddplId, int utilisateurId)
    {
        ExigerUtilisateur(utilisateurId);
        var (entete, etat) = await ChargerAsync(ddpId);
        DeclarationDelaiPaiementCycleDeVie.ValiderSuppressionLigne(etat);

        var supprimees = await _repository.SupprimerLigneAsync(ddpId, ddplId);
        if (supprimees == 0)
            throw new InvalidOperationException(
                $"Impossible de charger la ligne [{ddplId}] de la déclaration [{entete.Numero}].");

        await _repository.MettreAJourEtatAsync(
            ddpId, entete.Statut, entete.EstDeposee, entete.FichierGenere, entete.Libelle, utilisateurId, DateTime.Now);
    }

    // ─── Cycle de vie ─────────────────────────────────────────────────────────────────────────────

    public async Task CloturerAsync(int ddpId, int utilisateurId)
    {
        ExigerUtilisateur(utilisateurId);
        var (entete, etat) = await ChargerAsync(ddpId);
        DeclarationDelaiPaiementCycleDeVie.ValiderCloture(etat);

        await _repository.MettreAJourEtatAsync(
            ddpId, StatutDeclarationDelaiPaiement.Cloture, entete.EstDeposee, entete.FichierGenere,
            entete.Libelle, utilisateurId, DateTime.Now);
    }

    public async Task AnnulerClotureAsync(int ddpId, int utilisateurId)
    {
        ExigerUtilisateur(utilisateurId);
        var (entete, etat) = await ChargerAsync(ddpId);
        DeclarationDelaiPaiementCycleDeVie.ValiderAnnulationCloture(etat);

        await _repository.MettreAJourEtatAsync(
            ddpId, StatutDeclarationDelaiPaiement.EnCours, entete.EstDeposee, entete.FichierGenere,
            entete.Libelle, utilisateurId, DateTime.Now);
    }

    public async Task<ResultatControleIdentiteFiscaleDelaiPaiement> ControlerIdentiteFiscaleAsync(int ddpId)
    {
        var (entete, _) = await ChargerAsync(ddpId);
        var lignes = await _repository.GetLignesAsync(ddpId);

        var codes = lignes
            .Select(l => (l.TiersCode ?? string.Empty).Trim())
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var identitesErp = await _repository.GetIdentitesFiscalesTiersAsync(entete.SocieteId, codes);

        var identites = lignes
            .GroupBy(l => (l.TiersCode ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(groupe =>
            {
                var premiere = groupe.First();
                var code = groupe.Key;

                // Code tiers absent sur l'échéance, ou tiers absent du référentiel ERP : les DEUX sont
                // bloquants et signalés comme « introuvable dans le référentiel » — on ne prétend pas
                // savoir si l'IF/ICE est vide ou mal formé (aucune donnée lue).
                IdentiteFiscaleTiersErp? identiteErp = null;
                var trouve = code.Length > 0 && identitesErp.TryGetValue(code, out identiteErp);

                return new IdentiteFiscaleFournisseurDeclare
                {
                    TiersNo = premiere.TiersNo,
                    TiersCode = code,
                    TiersIntitule = premiere.TiersIntitule,
                    IdentifiantFiscal = identiteErp?.IdentifiantFiscal,
                    Ice = identiteErp?.Ice,
                    TrouveDansReferentiel = trouve,
                    NombreLignes = groupe.Count()
                };
            })
            .ToList();

        return ControleIdentiteFiscaleDelaiPaiement.Controler(identites);
    }

    public async Task<ResultatControleIdentiteFiscaleDelaiPaiement> VerifierGenerationFichierAutoriseeAsync(int ddpId)
    {
        var (_, etat) = await ChargerAsync(ddpId);

        // 1) Garde d'ÉTAT (legacy FichierGenerer l.790-827).
        DeclarationDelaiPaiementCycleDeVie.ValiderGenerationFichier(etat);

        // 2) Contrôle IF/ICE sur TOUTES les lignes AVANT toute écriture (correction de l'anomalie
        //    §4.4 du CDC : le legacy validait ligne par ligne pendant l'écriture ⇒ fichier partiel).
        var controle = await ControlerIdentiteFiscaleAsync(ddpId);
        if (!controle.EstConforme)
            throw new InvalidOperationException(controle.MessageBloquant);

        return controle;
    }

    public async Task MarquerFichierGenereAsync(int ddpId, int utilisateurId)
    {
        ExigerUtilisateur(utilisateurId);

        // Re-validation complète (état + IF/ICE) : le flag ne peut JAMAIS être posé sur une
        // déclaration qui ne passerait pas le contrôle bloquant.
        await VerifierGenerationFichierAutoriseeAsync(ddpId);

        var (entete, _) = await ChargerAsync(ddpId);
        await _repository.MettreAJourEtatAsync(
            ddpId, entete.Statut, entete.EstDeposee, true, entete.Libelle, utilisateurId, DateTime.Now);
    }

    public async Task AnnulerGenerationFichierAsync(int ddpId, int utilisateurId)
    {
        ExigerUtilisateur(utilisateurId);
        var (entete, etat) = await ChargerAsync(ddpId);
        DeclarationDelaiPaiementCycleDeVie.ValiderAnnulationGenerationFichier(etat);

        await _repository.MettreAJourEtatAsync(
            ddpId, entete.Statut, entete.EstDeposee, false, entete.Libelle, utilisateurId, DateTime.Now);
    }

    public async Task MarquerDeposeAsync(int ddpId, int utilisateurId)
    {
        ExigerUtilisateur(utilisateurId);
        var (entete, etat) = await ChargerAsync(ddpId);
        DeclarationDelaiPaiementCycleDeVie.ValiderDepot(etat);

        // Flag MANUEL : aucun appel réseau, aucune plateforme externe (décision PO §5.A-2).
        await _repository.MettreAJourEtatAsync(
            ddpId, entete.Statut, true, entete.FichierGenere, entete.Libelle, utilisateurId, DateTime.Now);
    }

    // ─── Utilitaires ──────────────────────────────────────────────────────────────────────────────

    private async Task<(DeclarationDelaiPaiement Entete, EtatDeclarationDelaiPaiement Etat)> ChargerAsync(int ddpId)
    {
        var entete = await _repository.GetEnteteAsync(ddpId)
            ?? throw new InvalidOperationException("Impossible de charger la déclaration.");

        var nombreLignes = await _repository.CompterLignesAsync(ddpId);

        return (entete, new EtatDeclarationDelaiPaiement
        {
            Statut = entete.Statut,
            EstDeposee = entete.EstDeposee,
            FichierGenere = entete.FichierGenere,
            NombreLignes = nombreLignes
        });
    }

    /// <summary>Convention legacy (l.674) : <c>DDP_DateFin</c> = dernier jour de période à 23:59:59.</summary>
    private static DateTime BorneFinDeJournee(DateTime dernierJour) => dernierJour.Date.AddDays(1).AddSeconds(-1);

    private static void ExigerUtilisateur(int utilisateurId)
    {
        if (utilisateurId <= 0)
            throw new ArgumentException(
                "L'utilisateur courant (UT_Id) est obligatoire : les colonnes UT_Id/UT_IdModif de " +
                "RT_DECLARATIONDELAISPAIEMENT sont NOT NULL et l'action doit être traçable.",
                nameof(utilisateurId));
    }
}
