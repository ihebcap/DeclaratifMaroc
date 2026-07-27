using System;
using System.Threading.Tasks;
using Declaration.Application.Entities;
using Declaration.Application.Interfaces;
using Declaration.Core;

namespace Declaration.Application.Services;

/// <summary>
/// TASK-128 : service d'orchestration du paramètre "date de mise en route" + de la reprise
/// manuelle par échéance. Aucune logique métier dupliquée : la classification est déléguée au
/// calculateur pur <see cref="DelaiPaiementBootstrapGuard"/> (Declaration.Core), ce service ne fait
/// que résoudre les deux lectures nécessaires (paramétrage société + reprise éventuelle) avant de
/// lui déléguer. Destiné aux consommateurs TASK-131 (sélection des lignes hors délai) et
/// TASK-134 (écran de contrôle).
/// </summary>
public interface IDelaiPaiementBootstrapService
{
    /// <summary>Date de mise en route de la société, ou null si "pas encore configuré".</summary>
    Task<DateTime?> GetDateMiseEnRouteAsync(int societeId);

    /// <summary>Saisie (une fois, par société) de la date de mise en route.</summary>
    Task SetDateMiseEnRouteAsync(int societeId, DateTime dateMiseEnRoute, int? utilisateurId);

    /// <summary>Reprise manuelle saisie pour cette échéance précise, ou null si aucune.</summary>
    Task<RepriseDelaiPaiement?> GetRepriseAsync(int societeId, int ecId);

    /// <summary>Saisie de la reprise manuelle "retard déjà connu/déclaré jusqu'au [date]" pour une échéance précise.</summary>
    Task SetRepriseAsync(int societeId, int ecId, DateTime dateDejaDeclareeJusquau, int? utilisateurId);

    /// <summary>
    /// Résout le statut de bascule d'une échéance déjà résolue (échéance légale connue), en
    /// combinant le paramétrage société et l'éventuelle reprise manuelle. L'indicateur d'historique
    /// legacy (<c>RT_DECLARATIONDELAISPAIEMENTLG</c>) est fourni par l'appelant (TASK-131) — sa
    /// lecture relève de l'algorithme de sélection, hors périmètre STRICT de TASK-128.
    /// </summary>
    Task<ResultatBasculeEcheance> ResoudreBasculeAsync(
        int societeId,
        int ecId,
        DateTime echeanceLegale,
        bool aHistoriqueDeclarationLegacy);
}

/// <inheritdoc cref="IDelaiPaiementBootstrapService"/>
public sealed class DelaiPaiementBootstrapService : IDelaiPaiementBootstrapService
{
    private readonly IParametrageDelaiPaiementSocieteRepository _parametrage;
    private readonly IRepriseDelaiPaiementRepository _reprise;

    public DelaiPaiementBootstrapService(
        IParametrageDelaiPaiementSocieteRepository parametrage,
        IRepriseDelaiPaiementRepository reprise)
    {
        _parametrage = parametrage ?? throw new ArgumentNullException(nameof(parametrage));
        _reprise = reprise ?? throw new ArgumentNullException(nameof(reprise));
    }

    public async Task<DateTime?> GetDateMiseEnRouteAsync(int societeId)
    {
        var parametrage = await _parametrage.GetAsync(societeId);
        return parametrage?.DateMiseEnRoute;
    }

    public Task SetDateMiseEnRouteAsync(int societeId, DateTime dateMiseEnRoute, int? utilisateurId)
        => _parametrage.SetAsync(societeId, dateMiseEnRoute, utilisateurId);

    public Task<RepriseDelaiPaiement?> GetRepriseAsync(int societeId, int ecId)
        => _reprise.GetAsync(societeId, ecId);

    public Task SetRepriseAsync(int societeId, int ecId, DateTime dateDejaDeclareeJusquau, int? utilisateurId)
        => _reprise.SetAsync(societeId, ecId, dateDejaDeclareeJusquau, utilisateurId);

    public async Task<ResultatBasculeEcheance> ResoudreBasculeAsync(
        int societeId,
        int ecId,
        DateTime echeanceLegale,
        bool aHistoriqueDeclarationLegacy)
    {
        var dateMiseEnRoute = await GetDateMiseEnRouteAsync(societeId);
        var reprise = await _reprise.GetAsync(societeId, ecId);

        return DelaiPaiementBootstrapGuard.Resoudre(
            echeanceLegale,
            dateMiseEnRoute,
            aHistoriqueDeclarationLegacy,
            reprise?.DateDejaDeclareeJusquau);
    }
}
