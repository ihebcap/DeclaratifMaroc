using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Declaration.Application.Interfaces;

/// <summary>
/// TASK-127 (socle Délai de Paiement Maroc) : lecture SEULE des jours de repos d'une société
/// (<c>P_JOURSREPOS.JR_Date</c>, filtré par <c>SO_Id</c>). Table déjà existante, possédée par
/// <c>apbs-gr_winform</c> — AUCUNE modification de schéma. Équivalent GRF de
/// <c>SocieteManager.JoursReposGetAll()</c> du legacy.
/// </summary>
public interface IJoursReposRepository
{
    /// <summary>
    /// Retourne les dates de jours de repos (fériés/chômés) de la société <paramref name="societeId"/>.
    /// Liste éventuellement vide, jamais null.
    /// </summary>
    Task<IReadOnlyList<DateTime>> GetJoursReposAsync(int societeId);
}
