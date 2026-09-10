using System;
using System.Threading.Tasks;
using Dapper;
using Declaration.Application.Entities;
using Declaration.Application.Interfaces;

namespace Declaration.Infrastructure.Repositories;

/// <summary>
/// TASK-128 : accès à la table <c>DM_PARAM_DELAIPAIEMENT_SOCIETE</c> (date de mise en route par
/// société) — propriété exclusive GRF, base de persistance
/// (<see cref="IDbConnectionFactory.CreatePersistenceConnection"/>, même connexion que
/// DM_ENTTVA/DM_LGTVA). Upsert idempotent via <c>MERGE</c> (même pattern que
/// <c>VentilationSageCacheRepository.UpsertEntries</c>, TASK-072/076).
///
/// Ne porte plus (depuis TASK-220) la reprise manuelle par échéance
/// (<c>DM_REPRISE_DELAIPAIEMENT</c>, ex-<c>IRepriseDelaiPaiementRepository</c>) : le mécanisme de
/// reprise manuelle est supprimé, la table reste en base (aucune modification de schéma/donnée
/// autorisée) mais devient fonctionnellement inutilisée.
/// </summary>
public sealed class DelaiPaiementBootstrapRepository : IParametrageDelaiPaiementSocieteRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DelaiPaiementBootstrapRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<ParametrageDelaiPaiementSociete?> GetAsync(int societeId)
    {
        using var connection = _connectionFactory.CreatePersistenceConnection();
        return await connection.QuerySingleOrDefaultAsync<ParametrageDelaiPaiementSociete>(
            @"SELECT SO_Id AS SoId, DateMiseEnRoute, UT_Id AS UtId, DateSaisie
              FROM DM_PARAM_DELAIPAIEMENT_SOCIETE WHERE SO_Id = @SocieteId",
            new { SocieteId = societeId });
    }

    public async Task SetAsync(int societeId, DateTime dateMiseEnRoute, int? utilisateurId)
    {
        using var connection = _connectionFactory.CreatePersistenceConnection();
        await connection.ExecuteAsync(
            @"MERGE DM_PARAM_DELAIPAIEMENT_SOCIETE AS target
              USING (SELECT @SocieteId AS SO_Id) AS source
                 ON target.SO_Id = source.SO_Id
              WHEN MATCHED THEN
                  UPDATE SET DateMiseEnRoute = @DateMiseEnRoute, UT_Id = @UtilisateurId, DateSaisie = SYSUTCDATETIME()
              WHEN NOT MATCHED THEN
                  INSERT (SO_Id, DateMiseEnRoute, UT_Id, DateSaisie)
                  VALUES (@SocieteId, @DateMiseEnRoute, @UtilisateurId, SYSUTCDATETIME());",
            new { SocieteId = societeId, DateMiseEnRoute = dateMiseEnRoute, UtilisateurId = utilisateurId });
    }
}
