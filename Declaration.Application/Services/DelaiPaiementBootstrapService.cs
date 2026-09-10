using System;
using System.Threading.Tasks;
using Declaration.Application.Interfaces;

namespace Declaration.Application.Services;

/// <summary>
/// TASK-128 : service d'orchestration du paramètre "date de mise en route" du module Délai de
/// Paiement Maroc. Destiné aux consommateurs TASK-131 (sélection des lignes hors délai) et
/// TASK-134 (écran de contrôle).
///
/// Depuis TASK-220, ne porte plus la reprise manuelle par échéance (mécanisme supprimé : le
/// critère de bascule TASK-131 compare désormais directement <c>DoDate</c> à
/// <see cref="GetDateMiseEnRouteAsync"/>, sans état intermédiaire "reprise requise").
/// </summary>
public interface IDelaiPaiementBootstrapService
{
    /// <summary>Date de mise en route de la société, ou null si "pas encore configuré".</summary>
    Task<DateTime?> GetDateMiseEnRouteAsync(int societeId);

    /// <summary>Saisie (une fois, par société) de la date de mise en route.</summary>
    Task SetDateMiseEnRouteAsync(int societeId, DateTime dateMiseEnRoute, int? utilisateurId);
}

/// <inheritdoc cref="IDelaiPaiementBootstrapService"/>
public sealed class DelaiPaiementBootstrapService : IDelaiPaiementBootstrapService
{
    private readonly IParametrageDelaiPaiementSocieteRepository _parametrage;

    public DelaiPaiementBootstrapService(IParametrageDelaiPaiementSocieteRepository parametrage)
    {
        _parametrage = parametrage ?? throw new ArgumentNullException(nameof(parametrage));
    }

    public async Task<DateTime?> GetDateMiseEnRouteAsync(int societeId)
    {
        var parametrage = await _parametrage.GetAsync(societeId);
        return parametrage?.DateMiseEnRoute;
    }

    public Task SetDateMiseEnRouteAsync(int societeId, DateTime dateMiseEnRoute, int? utilisateurId)
        => _parametrage.SetAsync(societeId, dateMiseEnRoute, utilisateurId);
}
