using System;

namespace Declaration.Application.Entities;

/// <summary>
/// TASK-128 : paramètre "date de mise en route" du module Délai de Paiement Maroc, par société.
/// Table neuve <c>DM_PARAM_DELAIPAIEMENT_SOCIETE</c>, propriété exclusive GRF — AUCUNE colonne
/// ajoutée à <c>P_SOCIETE</c> (contrainte PO : table partagée avec l'app legacy). Une ligne =
/// une société ayant configuré sa bascule ; ABSENCE de ligne = "pas encore configuré" (jamais une
/// date par défaut arbitraire, cf. TASK-128 §Étapes 2).
/// </summary>
public sealed class ParametrageDelaiPaiementSociete
{
    public int SoId { get; set; }
    public DateTime DateMiseEnRoute { get; set; }

    /// <summary>Utilisateur ayant saisi/modifié la date (P_UTILISATEUR.UT_Id, claim JWT "UT_Id"). Null si inconnu.</summary>
    public int? UtId { get; set; }
    public DateTime DateSaisie { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// TASK-128 : reprise manuelle ponctuelle "retard déjà connu/déclaré jusqu'au [date]" pour UNE
/// échéance précise antérieure à la mise en route de sa société et sans historique dans
/// <c>RT_DECLARATIONDELAISPAIEMENTLG</c>. Table neuve <c>DM_REPRISE_DELAIPAIEMENT</c>. Équivalent
/// d'un solde d'ouverture comptable : initialise la borne du calcul incrémental futur (TASK-131)
/// pour CETTE échéance uniquement, sans jamais recalculer/estimer silencieusement un retard cumulé
/// depuis la date de facture.
/// </summary>
public sealed class RepriseDelaiPaiement
{
    public int SoId { get; set; }
    public int EcId { get; set; }
    public DateTime DateDejaDeclareeJusquau { get; set; }

    /// <summary>Utilisateur ayant saisi la reprise (P_UTILISATEUR.UT_Id, claim JWT "UT_Id"). Null si inconnu.</summary>
    public int? UtId { get; set; }
    public DateTime DateSaisie { get; set; } = DateTime.UtcNow;
}
