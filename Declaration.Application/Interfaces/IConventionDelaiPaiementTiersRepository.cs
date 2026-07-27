using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Declaration.Application.Entities;
using Declaration.Core;

namespace Declaration.Application.Interfaces;

/// <summary>
/// TASK-129 (Délai de Paiement Maroc — Convention par tiers) : contrat CRUD complet sur
/// <c>RT_CONVENTIONTIERS</c> — distinct de <see cref="IConventionDelaiPaiementRepository"/> (TASK-127,
/// lecture SEULE filtrée par domaine, consommée uniquement par le calculateur/service de résolution du
/// délai). Ce contrat-ci sert la création/consultation/clôture anticipée/suppression des conventions.
/// </summary>
public interface IConventionDelaiPaiementTiersRepository
{
    /// <summary>Crée la convention et retourne le CP_Id généré (IDENTITY).</summary>
    Task<int> CreateAsync(ConventionDelaiPaiementTiers convention);

    /// <summary>Charge une convention par CP_Id. Null si introuvable.</summary>
    Task<ConventionDelaiPaiementTiers?> GetAsync(int cpId);

    /// <summary>Toutes les conventions (tous types) d'une société pour un domaine. Jamais null.</summary>
    Task<IReadOnlyList<ConventionDelaiPaiementTiers>> GetAllAsync(int societeId, DomaineDelaiPaiement domaine);

    /// <summary>
    /// Toutes les conventions (tous types) d'un tiers précis, société + domaine — utilisé pour les
    /// contrôles de chevauchement/unicité facture à la création (reproduit legacy
    /// <c>GetAll(societeNo, tiersNo, domaine)</c>). Jamais null.
    /// </summary>
    Task<IReadOnlyList<ConventionDelaiPaiementTiers>> GetAllForTiersAsync(int societeId, int tiersNo, DomaineDelaiPaiement domaine);

    /// <summary>Reproduit legacy l.495-496 : une convention de même numéro existe-t-elle déjà pour ce tiers/domaine ?</summary>
    Task<bool> ExisteNumeroAsync(int societeId, int tiersNo, string numero, DomaineDelaiPaiement domaine);

    /// <summary>
    /// Reproduit legacy l.523-524 (<c>EcheanceGetAll(erpDomaine, tiersNo, Etat.NonPaye)?.FirstOrDefault(x
    /// =&gt; x.No == factureNo.Value)</c>) : l'échéance <paramref name="factureNo"/> (RT_ECHEANCE.EC_Id)
    /// existe-t-elle pour ce tiers/domaine et est-elle NonPayée (EC_Etat=0) ?
    /// </summary>
    Task<bool> EcheanceNonPayeeExisteAsync(int societeId, int tiersNo, DomaineDelaiPaiement domaine, int factureNo);

    /// <summary>Clôture anticipée (Terminer, legacy l.564-589) : met à jour uniquement CP_DateFin.</summary>
    Task UpdateDateFinAsync(int cpId, DateTime nouvelleDateFin);

    /// <summary>Suppression directe, sans garde (reproduit legacy l.554-562 à l'identique, décision assumée).</summary>
    Task DeleteAsync(int cpId);
}
