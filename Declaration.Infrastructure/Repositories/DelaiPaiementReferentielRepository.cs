using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Declaration.Application.Interfaces;

namespace Declaration.Infrastructure.Repositories;

/// <summary>
/// TASK-127 (socle Délai de Paiement Maroc) : lectures de référentiel GRF nécessaires au calcul du
/// délai de paiement — jours de repos (<c>P_JOURSREPOS</c>) et paramétrage société
/// (<c>P_SOCIETE.SO_NbJoursDelaiPaiement</c>). LECTURE SEULE stricte : uniquement des <c>SELECT</c>,
/// AUCUN INSERT/UPDATE/DELETE, AUCUNE modification de schéma (tables possédées par apbs-gr_winform).
/// Ces deux tables vivent dans la base GRF (mêmes tables que celles déjà exploitées ailleurs) :
/// connexion résolue via <see cref="IDbConnectionFactory.CreateGrfConnection"/>.
/// </summary>
public sealed class DelaiPaiementReferentielRepository : IJoursReposRepository, IDelaiPaiementParametrageRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DelaiPaiementReferentielRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<IReadOnlyList<DateTime>> GetJoursReposAsync(int societeId)
    {
        using var connection = _connectionFactory.CreateGrfConnection();
        var dates = await connection.QueryAsync<DateTime>(
            "SELECT JR_Date FROM P_JOURSREPOS WHERE SO_Id = @SocieteId",
            new { SocieteId = societeId });
        return dates.ToList();
    }

    public async Task<int> GetNombreJoursDelaiDefautAsync(int societeId)
    {
        using var connection = _connectionFactory.CreateGrfConnection();
        // ISNULL(...,60) : repli sur l'amorçage legacy si la colonne est NULL (aucune ligne société
        // introuvable ne doit pas non plus figer le calcul — QuerySingleOrDefault renvoie 0 → repli 60).
        var valeur = await connection.QuerySingleOrDefaultAsync<int?>(
            "SELECT SO_NbJoursDelaiPaiement FROM P_SOCIETE WHERE SO_Id = @SocieteId",
            new { SocieteId = societeId });
        return valeur ?? 60;
    }
}
