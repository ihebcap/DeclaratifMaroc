using System;
using System.Collections.Generic;
using Declaration.Core;

namespace Declaration.Application.Entities;

/// <summary>
/// TASK-132 : entête de déclaration Délai de Paiement Maroc — projection 1:1 de la table EXISTANTE
/// <c>RT_DECLARATIONDELAISPAIEMENT</c> (propriété GRF, réutilisée telle quelle : aucune colonne
/// ajoutée, aucune migration). Mapping colonne ↔ propriété identique au legacy
/// (<c>DeclarationDelaisPaiementRepository.Script.cs:7-26</c>).
///
/// Seules <c>DDP_Statut</c>, <c>UT_IdModif</c>, <c>DDP_DateModif</c>, <c>DDP_IsDepose</c>,
/// <c>DDP_Libelle</c>, <c>DDP_IsGeneretedFile</c> sont modifiables après création (legacy UPDATE
/// l.95-104) — le repository n'expose aucune écriture sur les autres.
/// </summary>
public sealed class DeclarationDelaiPaiement
{
    /// <summary><c>DDP_Id</c> (identity).</summary>
    public int DdpId { get; init; }

    /// <summary><c>DDP_Numero</c> (nvarchar(30), NOT NULL) — attribué par le serveur, cf. <see cref="NumerotationDeclarationDelaiPaiement"/>.</summary>
    public string Numero { get; init; } = string.Empty;

    /// <summary><c>SO_Id</c>.</summary>
    public int SocieteId { get; init; }

    /// <summary><c>DDP_Date</c> : date de la déclaration (saisie), distincte des bornes de période.</summary>
    public DateTime Date { get; init; }

    /// <summary><c>DDP_Exercice</c>.</summary>
    public int Exercice { get; init; }

    /// <summary><c>DDP_Type</c> : 1 = Annuelle, 2 = Trimestrielle.</summary>
    public TypeDeclarationDelaiPaiement Type { get; init; }

    /// <summary><c>DDP_DateDebut</c> (minuit du premier jour de période).</summary>
    public DateTime DateDebut { get; init; }

    /// <summary>
    /// <c>DDP_DateFin</c> tel que stocké : dernier jour de période à <c>23:59:59</c> (convention
    /// legacy reproduite à l'identique — cf. VERIFY TASK-132 §Décisions).
    /// </summary>
    public DateTime DateFin { get; init; }

    /// <summary><c>DDP_Statut</c> : 0 = EnCours, 1 = Clôturé.</summary>
    public StatutDeclarationDelaiPaiement Statut { get; init; }

    /// <summary><c>DDP_DateCreation</c>.</summary>
    public DateTime DateCreation { get; init; }

    /// <summary><c>UT_Id</c> : créateur (<c>P_UTILISATEUR.UT_Id</c>, claim JWT « UT_Id »).</summary>
    public int CreateurId { get; init; }

    /// <summary><c>DDP_DateModif</c>.</summary>
    public DateTime DateModification { get; init; }

    /// <summary><c>UT_IdModif</c> : dernier modificateur.</summary>
    public int ModificateurId { get; init; }

    /// <summary><c>DDP_IsDepose</c> : flag MANUEL posé après export (aucune plateforme externe).</summary>
    public bool EstDeposee { get; init; }

    /// <summary><c>DDP_Libelle</c> (nullable).</summary>
    public string? Libelle { get; init; }

    /// <summary><c>DDP_IsGeneretedFile</c>.</summary>
    public bool FichierGenere { get; init; }

    /// <summary>
    /// <c>DDP_Periode</c> : trimestre (1..4) pour une trimestrielle,
    /// <see cref="DeclarationDelaiPaiementCycleDeVie.PeriodeNonApplicable"/> (0) pour une annuelle.
    /// </summary>
    public int Periode { get; init; }

    /// <summary>Trimestre typé, <c>null</c> pour une annuelle (ou une valeur hors 1..4 en base legacy).</summary>
    public TrimestreDelaiPaiement? Trimestre =>
        Periode >= 1 && Periode <= 4 ? (TrimestreDelaiPaiement)Periode : null;
}

/// <summary>
/// TASK-132 : entête + compte de lignes, pour la liste (le compte conditionne clôture/suppression —
/// jamais recalculé côté UI).
/// </summary>
public sealed class DeclarationDelaiPaiementListItem
{
    public DeclarationDelaiPaiement Entete { get; init; } = new();
    public int NombreLignes { get; init; }
}

/// <summary>
/// TASK-132 : ligne à INSÉRER dans <c>RT_DECLARATIONDELAISPAIEMENTLG</c>. Exactement les 5 colonnes
/// écrites par le legacy (<c>LigneDeclarationDelaisPaiementRepository.Script.cs:58-76</c>) :
/// <c>DDP_Id</c>, <c>EC_Id</c>, <c>AF_Id</c>, <c>DDPL_Depassement</c>, <c>DDPL_EcheanceLegale</c>.
/// </summary>
public sealed class LigneAIntegrerDelaiPaiement
{
    public int EcId { get; init; }

    /// <summary><c>AF_Id</c> — NULL pour les buckets « part non affectée » de TASK-131.</summary>
    public int? AfId { get; init; }

    /// <summary>Dépassement INCRÉMENTAL en jours (TASK-131) ; <c>DDPL_Depassement</c> est un decimal(24,6).</summary>
    public int Depassement { get; init; }

    /// <summary>Échéance légale résolue (TASK-127) → <c>DDPL_EcheanceLegale</c>.</summary>
    public DateTime EcheanceLegale { get; init; }
}

/// <summary>
/// TASK-132 : clé fonctionnelle d'une ligne de sélection (TASK-131) — sert (a) au filtrage
/// d'intégration partielle demandé depuis l'écran (TASK-134) et (b) au contrôle
/// anti-double-intégration dans une même déclaration.
/// </summary>
public readonly struct CleLigneDelaiPaiement : IEquatable<CleLigneDelaiPaiement>
{
    public CleLigneDelaiPaiement(int ecId, int? afId)
    {
        EcId = ecId;
        AfId = afId;
    }

    public int EcId { get; }
    public int? AfId { get; }

    public bool Equals(CleLigneDelaiPaiement autre) => EcId == autre.EcId && AfId == autre.AfId;
    public override bool Equals(object? obj) => obj is CleLigneDelaiPaiement autre && Equals(autre);
    public override int GetHashCode() => HashCode.Combine(EcId, AfId);
    public override string ToString() => AfId.HasValue ? $"EC_Id={EcId}/AF_Id={AfId}" : $"EC_Id={EcId}/sans affectation";
}

/// <summary>
/// TASK-132 : compte rendu EXHAUSTIF d'une intégration de lignes — aucune ligne écartée
/// silencieusement (principe TASK-131 repris). Ce que l'appelant a demandé, ce qui a été écrit, ce
/// qui a été refusé et pourquoi.
/// </summary>
public sealed class ResultatIntegrationLignesDelaiPaiement
{
    public int DdpId { get; init; }

    /// <summary>Lignes candidates exploitables renvoyées par TASK-131 (<c>Lignes</c>).</summary>
    public int NombreCandidates { get; init; }

    /// <summary>Lignes réellement insérées dans <c>RT_DECLARATIONDELAISPAIEMENTLG</c>.</summary>
    public int NombreIntegrees { get; init; }

    /// <summary>
    /// Lignes ignorées car DÉJÀ présentes dans CETTE déclaration (garde anti-double-intégration
    /// ajoutée par TASK-132 — le legacy n'en avait aucune).
    /// </summary>
    public IReadOnlyList<CleLigneDelaiPaiement> ClesDejaIntegrees { get; init; } = Array.Empty<CleLigneDelaiPaiement>();

    /// <summary>
    /// Lignes explicitement demandées mais REFUSÉES car en « reprise manuelle requise » (garde-fou
    /// TASK-128/131 : aucun <c>Depassement</c> calculable, décision PO).
    /// </summary>
    public IReadOnlyList<CleLigneDelaiPaiement> ClesRefuseesRepriseManuelleRequise { get; init; } = Array.Empty<CleLigneDelaiPaiement>();

    /// <summary>Lignes explicitement demandées mais absentes de la sélection courante (périmées / déjà déclarées ailleurs).</summary>
    public IReadOnlyList<CleLigneDelaiPaiement> ClesIntrouvablesDansSelection { get; init; } = Array.Empty<CleLigneDelaiPaiement>();

    /// <summary>
    /// Lignes visibles en « reprise manuelle requise » pour cette période (information, TASK-134) —
    /// jamais intégrables tant que la reprise n'est pas saisie.
    /// </summary>
    public int NombreRepriseManuelleRequiseDisponibles { get; init; }

    /// <summary>Date de mise en route de la société (TASK-128) ; <c>null</c> = société non configurée ⇒ 0 candidate.</summary>
    public DateTime? DateMiseEnRouteSociete { get; init; }
}

/// <summary>
/// TASK-132 : ligne intégrée relue, avec l'identification de l'échéance/du règlement (jointures
/// <c>RT_ECHEANCE</c>/<c>RT_AFFECTATION</c>/<c>RT_MOUVEMENT</c>, comme le legacy
/// <c>LigneDeclarationDelaisPaiementRepository.Script.cs:7-48</c>). Consommée par le contrôle IF/ICE
/// et par TASK-133 (génération).
/// </summary>
public sealed class LigneDeclarationDelaiPaiement
{
    public int DdplId { get; init; }
    public int DdpId { get; init; }
    public int EcId { get; init; }
    public int? AfId { get; init; }

    /// <summary><c>DDPL_Depassement</c> (jours ; decimal en base).</summary>
    public decimal Depassement { get; init; }

    /// <summary><c>DDPL_EcheanceLegale</c>.</summary>
    public DateTime EcheanceLegale { get; init; }

    // ── Échéance (RT_ECHEANCE) ──
    public int TiersNo { get; init; }
    public string? TiersCode { get; init; }
    public string? TiersIntitule { get; init; }
    public string? DoNumero { get; init; }
    public DateTime DoDate { get; init; }
    public string? DoReference { get; init; }
    public DateTime EcheanceContractuelle { get; init; }
    public decimal MontantEcheance { get; init; }
    public decimal SoldeEcheance { get; init; }
    public int DeviseId { get; init; }

    // ── Règlement (RT_AFFECTATION ⋈ RT_MOUVEMENT), null pour les buckets « part non affectée » ──
    public decimal? MontantAffecte { get; init; }
    public int? MvId { get; init; }
    public string? ReglementNumero { get; init; }
    public string? ReglementPiece { get; init; }
    public int? ReglementType { get; init; }
    public DateTime? ReglementDate { get; init; }
    public bool? ReglementRapproche { get; init; }
    public DateTime? ReglementDateRapprochement { get; init; }
    public int? ReglementModeId { get; init; }
}

/// <summary>
/// TASK-132 : identité fiscale d'un tiers telle que lue sur le MAÎTRE TIERS ERP Sage
/// (<c>F_COMPTET</c>), colonnes IF/ICE désignées par société dans <c>P_SOCIETE</c>
/// (<c>SO_DecTvaColNameIdentifiantFrs</c>/<c>SO_DecTvaColNameIceFrs</c>). Jamais lues sur
/// <c>RT_MOUVEMENT</c> (snapshot souvent vide, cf. TASK-048) ni via une jointure cross-base depuis
/// la connexion GRF (leçon TASK-154).
/// </summary>
public sealed class IdentiteFiscaleTiersErp
{
    /// <summary><c>F_COMPTET.CT_Num</c> = <c>RT_ECHEANCE.CT_Code</c>.</summary>
    public string TiersCode { get; init; } = string.Empty;

    public string? IdentifiantFiscal { get; init; }
    public string? Ice { get; init; }

    /// <summary>
    /// TASK-133 : n° de registre de commerce fournisseur (<c>F_COMPTET</c>, colonne configurable par
    /// société — <c>P_SOCIETE.SO_ColValueNumRegistreCommerceFournisseur</c>, legacy
    /// <c>FournisseurErpHelper.GetAllInfoFournisseur</c> + <c>ErpTiersIceRepository.GetAllTiersIceToMaroc</c>).
    /// <c>null</c> quand la colonne n'est pas configurée pour la société (legacy : émet une chaîne
    /// vide dans ce cas, jamais une erreur — le n° RC ne fait PAS partie du contrôle bloquant IF/ICE
    /// de TASK-132). Absent du contrôle IF/ICE : uniquement utilisé pour le tag XML <c>&lt;numRC&gt;</c>.
    /// </summary>
    public string? NumRc { get; init; }

    /// <summary>
    /// TASK-133 : adresse siège social fournisseur (<c>F_COMPTET.CT_Adresse</c> — colonne FIXE, pas
    /// configurable par société, contrairement à l'IF/l'ICE/le n° RC ; legacy <c>IErpFournisseur.Adresse</c>).
    /// </summary>
    public string? Adresse { get; init; }
}

/// <summary>
/// TASK-191 : valeurs réelles « nature marchandise »/« date livraison marchandise » d'UN document
/// facture fournisseur, lues sur <c>F_DOCENTETE</c> (Sage, lecture seule) via les 2 colonnes
/// configurables par société (<c>P_SOCIETE.SO_ColValueNatureMarchandise</c>/
/// <c>SO_ColValueDateLivraisonMarchandise</c>) — même famille que <see cref="IdentiteFiscaleTiersErp.NumRc"/>
/// (TASK-133). <c>null</c> sur l'un des 2 champs ⇔ colonne non configurée pour la société OU valeur
/// absente sur le document trouvé : dans les deux cas, l'appelant applique le même repli qu'aujourd'hui
/// (jamais une erreur liée à l'absence de configuration/valeur).
/// </summary>
public sealed class ValeursMarchandiseErp
{
    /// <summary><c>F_DOCENTETE.[SO_ColValueNatureMarchandise]</c> — <c>null</c> si non configurée ou vide sur le document.</summary>
    public string? NatureMarchandise { get; init; }

    /// <summary><c>F_DOCENTETE.[SO_ColValueDateLivraisonMarchandise]</c> — <c>null</c> si non configurée ou vide sur le document.</summary>
    public DateTime? DateLivraisonMarchandise { get; init; }
}

/// <summary>
/// TASK-133 : champs société nécessaires à l'en-tête du fichier XML de dépôt Délai de Paiement,
/// lecture seule sur <c>P_SOCIETE</c> (table EXISTANTE, propriété <c>apbs-gr_winform</c> — aucune
/// modification de schéma, uniquement des <c>SELECT</c>).
/// </summary>
public sealed class SocieteDelaiPaiementInfo
{
    /// <summary><c>P_SOCIETE.SO_Identifiant</c> (IF de la société, DISTINCT de <c>SO_Id</c> — même colonne que TASK-155/TVA).</summary>
    public string? IdentifiantFiscal { get; init; }

    /// <summary><c>P_SOCIETE.SO_ActiviteMarroc</c> : 1 = Normal, 2 = EntrepriseEnCourDeProcedure.</summary>
    public int ActiviteMarrocCode { get; init; }

    /// <summary><c>P_SOCIETE.SO_DateJugement</c> — pertinent uniquement si <see cref="ActiviteMarrocCode"/> = 2.</summary>
    public DateTime? DateJugement { get; init; }

    /// <summary><c>P_SOCIETE.SO_ChiffreAffaire</c>.</summary>
    public decimal ChiffreAffaire { get; init; }

    /// <summary>
    /// TASK-134 : <c>P_SOCIETE.SO_TypeDecDP</c> — type de déclaration Délai de Paiement PAR DÉFAUT de
    /// la société (CDC §7.1, 1 = Annuelle, 2 = Trimestrielle). Lecture seule, colonne EXISTANTE
    /// (vérifiée en base réelle) : sert UNIQUEMENT à pré-remplir le formulaire de création et le
    /// filtre de période raisonné de l'écran de contrôle — le type effectif reste toujours choisi par
    /// l'utilisateur et validé par <c>DeclarationDelaiPaiementCycleDeVie.CalculerPeriode</c>. Une
    /// valeur hors 1..2 en base (société non paramétrée) est restituée telle quelle : c'est l'appelant
    /// qui décide du repli, jamais un silence ici.
    /// </summary>
    public int TypeDeclarationParDefautCode { get; init; }
}
