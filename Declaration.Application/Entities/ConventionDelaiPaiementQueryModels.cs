using System;
using Declaration.Core;

namespace Declaration.Application.Entities;

/// <summary>
/// TASK-130 (Délai de Paiement Maroc — Convention par tiers, FRONT) : projection de LISTE pour
/// l'écran conventions — complète <see cref="ConventionDelaiPaiementTiers"/> (TASK-129, CRUD) avec
/// deux champs dérivés nécessaires à l'affichage grille (colonnes « n° facture » et « pièce jointe »
/// de la TASK) sans jamais charger le contenu binaire (<c>CP_File</c>) dans une liste :
/// <list type="bullet">
/// <item><see cref="FactureNumero"/> : résolu par jointure <c>RT_ECHEANCE.EC_Id = CP_FactureNo</c>
/// (même pattern que <c>ConventionDelaiPaiementRepository.GetConventionsActivesAsync</c>, TASK-127),
/// non nul uniquement pour le type Facture.</item>
/// <item><see cref="HasFile"/> : simple présence de <c>CP_File</c> (calculée en SQL, <c>CASE WHEN
/// ... IS NOT NULL</c>), jamais le contenu — la liste ne charge aucun binaire (perf).</item>
/// </list>
/// </summary>
public sealed class ConventionDelaiPaiementListItem
{
    public int CpId { get; set; }
    public int SocieteId { get; set; }
    public int TiersNo { get; set; }
    public string TiersCode { get; set; } = string.Empty;
    public string TiersIntitule { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Numero { get; set; } = string.Empty;
    public DateTime? DateDebut { get; set; }
    public DateTime? DateFin { get; set; }
    public int NombreJoursDelaisPaiement { get; set; }
    public DomaineDelaiPaiement Domaine { get; set; }
    public TypeConventionDelaiPaiement Type { get; set; }
    public int? FactureNo { get; set; }
    public string? FactureNumero { get; set; }
    public bool HasFile { get; set; }
}

/// <summary>
/// TASK-130 : candidat pour la sélection « facture non payée du tiers » du formulaire de création
/// (type Facture). Lecture seule sur <c>RT_ECHEANCE</c> — mêmes critères que
/// <c>EcheanceNonPayeeExisteAsync</c> (TASK-129 : <c>EC_Etat=0</c>, <c>DO_Domaine</c> mappé INVERSÉ,
/// cf. <c>ConventionDelaiPaiementRepository.ToDoDomaineErp</c>), mais retourne la liste des candidats
/// au lieu d'un simple booléen d'existence.
/// </summary>
public sealed class FactureNonPayeeItem
{
    public int EcId { get; set; }
    public string DoNumero { get; set; } = string.Empty;
    public DateTime DoDate { get; set; }
    public decimal Montant { get; set; }
    public decimal Solde { get; set; }
}

/// <summary>
/// TASK-130 : résultat de recherche tiers pour le formulaire de création (sélection du tiers
/// concerné par la convention). Source : valeurs distinctes de <c>RT_ECHEANCE</c> (CT_No/CT_Code/
/// CT_Intitule, déjà dénormalisées sur cette table GRF — pas de jointure Sage <c>F_COMPTET</c>,
/// cohérent avec la leçon TASK-154 : jamais de JOIN SQL trois-parties vers Sage). Limite assumée
/// (documentée en VERIFY) : seuls les tiers ayant au moins une échéance dans ce domaine sont
/// trouvables par cette recherche.
/// </summary>
public sealed class TiersRechercheItem
{
    public int TiersNo { get; set; }
    public string TiersCode { get; set; } = string.Empty;
    public string TiersIntitule { get; set; } = string.Empty;
}
