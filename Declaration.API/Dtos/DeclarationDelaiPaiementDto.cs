using System;
using System.Collections.Generic;
using System.Linq;
using Declaration.Application.Entities;
using Declaration.Application.Services;
using Declaration.Core;

namespace Declaration.API.Dtos;

/// <summary>
/// TASK-134 (Délai de Paiement Maroc — front liste/fiche/sélection/contrôle) : projections JSON du
/// domaine « déclaration DDP ». <b>Aucune logique métier ici</b> : ces types ne font que traduire les
/// entités/résultats déjà produits par TASK-131 (sélection), TASK-132 (cycle de vie + contrôle
/// IF/ICE) et TASK-133 (génération) — les enums sont émis en texte pour que le front n'ait jamais à
/// connaître les codes numériques persistés, et les statuts calculés côté serveur (jamais recalculés
/// par l'UI, ARCHITECTURE §5).
/// </summary>
public sealed class DeclarationDelaiPaiementListItemDto
{
    public int DdpId { get; init; }
    public string Numero { get; init; } = string.Empty;
    public int SoId { get; init; }
    public DateTime Date { get; init; }
    public int Exercice { get; init; }

    /// <summary>« Annuelle » ou « Trimestrielle » (jamais le code 1/2 persisté).</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>1..4, ou <c>null</c> pour une annuelle (<c>DDP_Periode = 0</c> = non applicable).</summary>
    public int? Trimestre { get; init; }

    public DateTime DateDebut { get; init; }

    /// <summary><c>DDP_DateFin</c> tel que stocké (dernier jour de période à 23:59:59, convention legacy).</summary>
    public DateTime DateFin { get; init; }

    /// <summary>« EnCours » ou « Cloture ».</summary>
    public string Statut { get; init; } = string.Empty;

    public bool EstDeposee { get; init; }
    public bool FichierGenere { get; init; }
    public string? Libelle { get; init; }
    public int NombreLignes { get; init; }

    public DateTime DateCreation { get; init; }
    public DateTime DateModification { get; init; }

    /// <summary>
    /// Transitions autorisées, CALCULÉES CÔTÉ SERVEUR à partir des mêmes gardes que TASK-132 — l'UI
    /// n'invente jamais l'état d'un bouton (et le serveur revalide de toute façon).
    /// </summary>
    public ActionsDeclarationDelaiPaiementDto Actions { get; init; } = new();

    public static DeclarationDelaiPaiementListItemDto From(DeclarationDelaiPaiement entete, int nombreLignes) => new()
    {
        DdpId = entete.DdpId,
        Numero = entete.Numero,
        SoId = entete.SocieteId,
        Date = entete.Date,
        Exercice = entete.Exercice,
        Type = entete.Type.ToString(),
        Trimestre = entete.Trimestre.HasValue ? (int)entete.Trimestre.Value : null,
        DateDebut = entete.DateDebut,
        DateFin = entete.DateFin,
        Statut = entete.Statut.ToString(),
        EstDeposee = entete.EstDeposee,
        FichierGenere = entete.FichierGenere,
        Libelle = entete.Libelle,
        NombreLignes = nombreLignes,
        DateCreation = entete.DateCreation,
        DateModification = entete.DateModification,
        Actions = ActionsDeclarationDelaiPaiementDto.From(entete, nombreLignes),
    };

    public static DeclarationDelaiPaiementListItemDto From(DeclarationDelaiPaiementListItem item)
        => From(item.Entete, item.NombreLignes);
}

/// <summary>
/// TASK-134 : transitions autorisées d'une déclaration, dérivées des gardes PURES de TASK-132
/// (<c>DeclarationDelaiPaiementCycleDeVie.Valider*</c>) — <b>même source de vérité, aucune règle
/// dupliquée</b> : chaque drapeau est obtenu en appelant la garde correspondante et en observant si
/// elle lève. Purement indicatif pour l'UI : le serveur rejoue systématiquement la garde à
/// l'exécution de l'action.
/// </summary>
public sealed class ActionsDeclarationDelaiPaiementDto
{
    public bool PeutModifierLibelle { get; init; }
    public bool PeutIntegrerLignes { get; init; }
    public bool PeutCloturer { get; init; }
    public bool PeutAnnulerCloture { get; init; }
    public bool PeutGenererFichier { get; init; }
    public bool PeutAnnulerGeneration { get; init; }
    public bool PeutDeposer { get; init; }
    public bool PeutSupprimer { get; init; }

    public static ActionsDeclarationDelaiPaiementDto From(DeclarationDelaiPaiement entete, int nombreLignes)
    {
        var etat = new EtatDeclarationDelaiPaiement
        {
            Statut = entete.Statut,
            EstDeposee = entete.EstDeposee,
            FichierGenere = entete.FichierGenere,
            NombreLignes = nombreLignes,
        };

        return new ActionsDeclarationDelaiPaiementDto
        {
            PeutModifierLibelle = Autorise(() => DeclarationDelaiPaiementCycleDeVie.ValiderModificationLibelle(etat)),
            PeutIntegrerLignes = Autorise(() => DeclarationDelaiPaiementCycleDeVie.ValiderIntegrationLignes(etat)),
            PeutCloturer = Autorise(() => DeclarationDelaiPaiementCycleDeVie.ValiderCloture(etat)),
            PeutAnnulerCloture = Autorise(() => DeclarationDelaiPaiementCycleDeVie.ValiderAnnulationCloture(etat)),
            PeutGenererFichier = Autorise(() => DeclarationDelaiPaiementCycleDeVie.ValiderGenerationFichier(etat)),
            PeutAnnulerGeneration = Autorise(() => DeclarationDelaiPaiementCycleDeVie.ValiderAnnulationGenerationFichier(etat)),
            PeutDeposer = Autorise(() => DeclarationDelaiPaiementCycleDeVie.ValiderDepot(etat)),
            PeutSupprimer = Autorise(() => DeclarationDelaiPaiementCycleDeVie.ValiderSuppression(etat)),
        };
    }

    /// <summary>
    /// Les gardes TASK-132 signalent un refus par <see cref="InvalidOperationException"/> (message
    /// utilisateur français). On ne réécrit pas leur logique : on l'INTERROGE.
    /// </summary>
    private static bool Autorise(Action garde)
    {
        try
        {
            garde();
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}

/// <summary>TASK-134 : clé fonctionnelle d'une ligne de sélection, telle qu'échangée avec l'écran.</summary>
public sealed class CleLigneDelaiPaiementDto
{
    public int EcId { get; init; }
    public int? AfId { get; init; }

    public static CleLigneDelaiPaiementDto From(CleLigneDelaiPaiement cle) => new() { EcId = cle.EcId, AfId = cle.AfId };
}

/// <summary>
/// TASK-134 : une ligne candidate (ou « reprise manuelle requise ») renvoyée par la sélection
/// TASK-131. <see cref="Depassement"/> est <c>null</c> — et JAMAIS 0 — pour une ligne en reprise
/// manuelle requise : l'écran doit afficher un badge, pas un chiffre.
/// </summary>
public sealed class LigneSelectionDelaiPaiementDto
{
    public int EcId { get; init; }
    public int? AfId { get; init; }

    /// <summary>Cas de figure ayant produit la ligne (traçabilité TASK-131).</summary>
    public string Bucket { get; init; } = string.Empty;

    /// <summary>« Candidate » ou « RepriseManuelleRequise ».</summary>
    public string Statut { get; init; } = string.Empty;

    public DateTime EcheanceLegale { get; init; }
    public int NombreJoursDelaiApplique { get; init; }

    /// <summary>« ConventionFacture », « Convention » ou « Defaut » (TASK-127).</summary>
    public string OrigineDelai { get; init; } = string.Empty;

    public DateTime BorneActuelle { get; init; }
    public DateTime? BorneReference { get; init; }

    /// <summary>« DerniereDeclaration », « EcheanceLegale », « RepriseManuelle » ou « Indeterminee ».</summary>
    public string OrigineBorneReference { get; init; } = string.Empty;

    /// <summary>Dépassement INCRÉMENTAL en jours ; <c>null</c> si reprise manuelle requise.</summary>
    public int? Depassement { get; init; }

    public decimal MontantLigne { get; init; }

    public string? DoNumero { get; init; }
    public DateTime DoDate { get; init; }
    public string? DoReference { get; init; }
    public DateTime EcheanceContractuelle { get; init; }
    public decimal MontantEcheance { get; init; }
    public decimal SoldeEcheance { get; init; }

    public int TiersNo { get; init; }
    public string? TiersCode { get; init; }
    public string? TiersIntitule { get; init; }

    public string? TypeReglement { get; init; }
    public DateTime? DateReglement { get; init; }
    public DateTime? DateRapprochement { get; init; }
    public string? ReglementNumero { get; init; }
    public string? ReglementPiece { get; init; }

    public static LigneSelectionDelaiPaiementDto From(LigneSelectionDelaiPaiement ligne) => new()
    {
        EcId = ligne.EcId,
        AfId = ligne.AfId,
        Bucket = ligne.Bucket.ToString(),
        Statut = ligne.Statut.ToString(),
        EcheanceLegale = ligne.EcheanceLegale,
        NombreJoursDelaiApplique = ligne.NombreJoursDelaiApplique,
        OrigineDelai = ligne.OrigineDelai.ToString(),
        BorneActuelle = ligne.BorneActuelle,
        BorneReference = ligne.BorneReference,
        OrigineBorneReference = ligne.OrigineBorneReference.ToString(),
        Depassement = ligne.Depassement,
        MontantLigne = ligne.MontantLigne,
        DoNumero = ligne.DoNumero,
        DoDate = ligne.DoDate,
        DoReference = ligne.DoReference,
        EcheanceContractuelle = ligne.EcheanceContractuelle,
        MontantEcheance = ligne.MontantEcheance,
        SoldeEcheance = ligne.SoldeEcheance,
        TiersNo = ligne.TiersNo,
        TiersCode = ligne.TiersCode,
        TiersIntitule = ligne.TiersIntitule,
        TypeReglement = ligne.TypeReglement?.ToString(),
        DateReglement = ligne.DateReglement,
        DateRapprochement = ligne.DateRapprochement,
        ReglementNumero = ligne.ReglementNumero,
        ReglementPiece = ligne.ReglementPiece,
    };
}

/// <summary>
/// TASK-134 : résultat complet d'une sélection TASK-131 pour UNE période — <b>les bornes sont
/// toujours renvoyées</b> pour que l'écran les AFFICHE au lieu de les faire saisir (exigence PO du
/// 19/07/2026 : jamais de plage de dates libre).
/// </summary>
public sealed class SelectionDelaiPaiementDto
{
    public DateTime DateDebutPeriode { get; init; }
    public DateTime DateFinPeriode { get; init; }

    /// <summary><c>null</c> = société non configurée (TASK-128) ⇒ 0 candidate, tout en reprise manuelle requise.</summary>
    public DateTime? DateMiseEnRouteSociete { get; init; }

    public int NombreEcheancesExaminees { get; init; }

    /// <summary>
    /// Échéances de la période DÉJÀ portées par une déclaration antérieure : permet à l'écran de
    /// distinguer « aucun retard » de « déjà déclaré » (jamais un zéro silencieux).
    /// </summary>
    public int NombreEcheancesDejaDeclarees { get; init; }

    /// <summary>Borne la plus récente déjà déclarée (max <c>DDP_DateFin</c>) ; <c>null</c> si aucune.</summary>
    public DateTime? DerniereBorneDejaDeclaree { get; init; }

    public IReadOnlyList<LigneSelectionDelaiPaiementDto> Lignes { get; init; } = Array.Empty<LigneSelectionDelaiPaiementDto>();

    /// <summary>Lignes « antérieures à la mise en route — retard réel inconnu » : visibles, jamais intégrables.</summary>
    public IReadOnlyList<LigneSelectionDelaiPaiementDto> LignesRepriseManuelleRequise { get; init; } = Array.Empty<LigneSelectionDelaiPaiementDto>();

    public static SelectionDelaiPaiementDto From(ResultatSelectionDelaiPaiement resultat) => new()
    {
        DateDebutPeriode = resultat.DateDebutPeriode,
        DateFinPeriode = resultat.DateFinPeriode,
        DateMiseEnRouteSociete = resultat.DateMiseEnRouteSociete,
        NombreEcheancesExaminees = resultat.NombreEcheancesExaminees,
        NombreEcheancesDejaDeclarees = resultat.NombreEcheancesDejaDeclarees,
        DerniereBorneDejaDeclaree = resultat.DerniereBorneDejaDeclaree,
        Lignes = resultat.Lignes.Select(LigneSelectionDelaiPaiementDto.From).ToList(),
        LignesRepriseManuelleRequise = resultat.LignesRepriseManuelleRequise.Select(LigneSelectionDelaiPaiementDto.From).ToList(),
    };
}

/// <summary>TASK-134 : une ligne DÉJÀ intégrée à la déclaration (relue depuis <c>…LG</c>).</summary>
public sealed class LigneDeclarationDelaiPaiementDto
{
    public int DdplId { get; init; }
    public int EcId { get; init; }
    public int? AfId { get; init; }
    public decimal Depassement { get; init; }
    public DateTime EcheanceLegale { get; init; }

    public int TiersNo { get; init; }
    public string? TiersCode { get; init; }
    public string? TiersIntitule { get; init; }
    public string? DoNumero { get; init; }
    public DateTime DoDate { get; init; }
    public string? DoReference { get; init; }
    public DateTime EcheanceContractuelle { get; init; }
    public decimal MontantEcheance { get; init; }
    public decimal SoldeEcheance { get; init; }
    public decimal? MontantAffecte { get; init; }
    public string? ReglementNumero { get; init; }
    public string? ReglementPiece { get; init; }
    public DateTime? ReglementDate { get; init; }
    public bool? ReglementRapproche { get; init; }
    public DateTime? ReglementDateRapprochement { get; init; }

    public static LigneDeclarationDelaiPaiementDto From(LigneDeclarationDelaiPaiement ligne) => new()
    {
        DdplId = ligne.DdplId,
        EcId = ligne.EcId,
        AfId = ligne.AfId,
        Depassement = ligne.Depassement,
        EcheanceLegale = ligne.EcheanceLegale,
        TiersNo = ligne.TiersNo,
        TiersCode = ligne.TiersCode,
        TiersIntitule = ligne.TiersIntitule,
        DoNumero = ligne.DoNumero,
        DoDate = ligne.DoDate,
        DoReference = ligne.DoReference,
        EcheanceContractuelle = ligne.EcheanceContractuelle,
        MontantEcheance = ligne.MontantEcheance,
        SoldeEcheance = ligne.SoldeEcheance,
        MontantAffecte = ligne.MontantAffecte,
        ReglementNumero = ligne.ReglementNumero,
        ReglementPiece = ligne.ReglementPiece,
        ReglementDate = ligne.ReglementDate,
        ReglementRapproche = ligne.ReglementRapproche,
        ReglementDateRapprochement = ligne.ReglementDateRapprochement,
    };
}

/// <summary>
/// TASK-134 : compte rendu EXHAUSTIF d'une intégration (TASK-132) — repris tel quel pour que l'écran
/// puisse dire précisément ce qui a été écrit, ce qui était déjà là, ce qui a été refusé et pourquoi.
/// Aucune ligne écartée silencieusement.
/// </summary>
public sealed class ResultatIntegrationLignesDelaiPaiementDto
{
    public int DdpId { get; init; }
    public int NombreCandidates { get; init; }
    public int NombreIntegrees { get; init; }
    public IReadOnlyList<CleLigneDelaiPaiementDto> ClesDejaIntegrees { get; init; } = Array.Empty<CleLigneDelaiPaiementDto>();
    public IReadOnlyList<CleLigneDelaiPaiementDto> ClesRefuseesRepriseManuelleRequise { get; init; } = Array.Empty<CleLigneDelaiPaiementDto>();
    public IReadOnlyList<CleLigneDelaiPaiementDto> ClesIntrouvablesDansSelection { get; init; } = Array.Empty<CleLigneDelaiPaiementDto>();
    public int NombreRepriseManuelleRequiseDisponibles { get; init; }
    public DateTime? DateMiseEnRouteSociete { get; init; }

    public static ResultatIntegrationLignesDelaiPaiementDto From(ResultatIntegrationLignesDelaiPaiement r) => new()
    {
        DdpId = r.DdpId,
        NombreCandidates = r.NombreCandidates,
        NombreIntegrees = r.NombreIntegrees,
        ClesDejaIntegrees = r.ClesDejaIntegrees.Select(CleLigneDelaiPaiementDto.From).ToList(),
        ClesRefuseesRepriseManuelleRequise = r.ClesRefuseesRepriseManuelleRequise.Select(CleLigneDelaiPaiementDto.From).ToList(),
        ClesIntrouvablesDansSelection = r.ClesIntrouvablesDansSelection.Select(CleLigneDelaiPaiementDto.From).ToList(),
        NombreRepriseManuelleRequiseDisponibles = r.NombreRepriseManuelleRequiseDisponibles,
        DateMiseEnRouteSociete = r.DateMiseEnRouteSociete,
    };
}

/// <summary>
/// TASK-134 : verdict du contrôle IF/ICE bloquant (TASK-132) tel quel. <b>Le front n'applique AUCUNE
/// validation de longueur de son côté</b> (la règle 8/15 est un point ouvert PO/fiscaliste, cf. VERIFY
/// TASK-132 §8 n°9) : il affiche <see cref="MessageBloquant"/> et
/// <see cref="FournisseursFautifs"/> tels que renvoyés.
/// </summary>
public sealed class ControleIdentiteFiscaleDelaiPaiementDto
{
    public bool EstConforme { get; init; }
    public int NombreFournisseursExamines { get; init; }
    public int NombreLignesExaminees { get; init; }
    public string MessageBloquant { get; init; } = string.Empty;
    public IReadOnlyList<FournisseurIdentiteFiscaleFautifDto> FournisseursFautifs { get; init; } = Array.Empty<FournisseurIdentiteFiscaleFautifDto>();

    public static ControleIdentiteFiscaleDelaiPaiementDto From(ResultatControleIdentiteFiscaleDelaiPaiement r) => new()
    {
        EstConforme = r.EstConforme,
        NombreFournisseursExamines = r.NombreFournisseursExamines,
        NombreLignesExaminees = r.NombreLignesExaminees,
        MessageBloquant = r.MessageBloquant,
        FournisseursFautifs = r.FournisseursFautifs.Select(f => new FournisseurIdentiteFiscaleFautifDto
        {
            TiersNo = f.TiersNo,
            TiersCode = f.TiersCode,
            TiersIntitule = f.TiersIntitule,
            IdentifiantFiscal = f.IdentifiantFiscal,
            Ice = f.Ice,
            NombreLignes = f.NombreLignes,
            MotifsLibelles = f.MotifsLibelles.ToList(),
        }).ToList(),
    };
}

/// <inheritdoc cref="ControleIdentiteFiscaleDelaiPaiementDto"/>
public sealed class FournisseurIdentiteFiscaleFautifDto
{
    public int TiersNo { get; init; }
    public string TiersCode { get; init; } = string.Empty;
    public string? TiersIntitule { get; init; }
    public string? IdentifiantFiscal { get; init; }
    public string? Ice { get; init; }
    public int NombreLignes { get; init; }

    /// <summary>Motifs déjà libellés en français par TASK-132 — repris tels quels, jamais reformulés.</summary>
    public IReadOnlyList<string> MotifsLibelles { get; init; } = Array.Empty<string>();
}
