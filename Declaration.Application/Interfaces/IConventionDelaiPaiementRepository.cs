using System.Collections.Generic;
using System.Threading.Tasks;
using Declaration.Core;

namespace Declaration.Application.Interfaces;

/// <summary>
/// TASK-127 (socle Délai de Paiement Maroc) : contrat de lecture des conventions de délai de paiement
/// actives d'une société, pour un domaine donné (Achat/Vente), déjà filtrées par domaine — comme le
/// legacy <c>SocieteManager.ConventionDelaisPaiementTiersGetAll(domaine)</c>.
///
/// INTERFACE SEULE ici : l'implémentation (lecture <c>RT_CONVENTIONTIERS</c>) est livrée par TASK-129.
/// Le socle TASK-127 la définit pour que le calcul partagé soit consommable, mais ne l'implémente ni
/// ne l'enregistre en DI (aucun repli/mock silencieux en production — décision documentée dans le
/// VERIFY TASK-127). Le câblage DI complet (ce repository + le service) est finalisé par TASK-129.
/// </summary>
public interface IConventionDelaiPaiementRepository
{
    /// <summary>
    /// Retourne les conventions actives de la société <paramref name="societeId"/> pour le domaine
    /// <paramref name="domaine"/>. Liste éventuellement vide, jamais null.
    /// </summary>
    Task<IReadOnlyList<ConventionDelaiPaiement>> GetConventionsActivesAsync(int societeId, DomaineDelaiPaiement domaine);
}
