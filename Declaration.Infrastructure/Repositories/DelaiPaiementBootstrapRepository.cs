using System;
using System.Threading.Tasks;
using Dapper;
using Declaration.Application.Entities;
using Declaration.Application.Interfaces;

namespace Declaration.Infrastructure.Repositories;

/// <summary>
/// TASK-128 : accès aux deux tables neuves du bootstrap Délai de Paiement Maroc
/// (<c>DM_PARAM_DELAIPAIEMENT_SOCIETE</c>, <c>DM_REPRISE_DELAIPAIEMENT</c>) — propriété exclusive
/// GRF, base de persistance (<see cref="IDbConnectionFactory.CreatePersistenceConnection"/>, même
/// connexion que DM_ENTTVA/DM_LGTVA). Upsert idempotent via <c>MERGE</c> (même pattern que
/// <c>VentilationSageCacheRepository.UpsertEntries</c>, TASK-072/076).
/// </summary>
public sealed class DelaiPaiementBootstrapRepository :
    IParametrageDelaiPaiementSocieteRepository,
    IRepriseDelaiPaiementRepository
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

    public async Task<RepriseDelaiPaiement?> GetAsync(int societeId, int ecId)
    {
        using var connection = _connectionFactory.CreatePersistenceConnection();
        return await connection.QuerySingleOrDefaultAsync<RepriseDelaiPaiement>(
            @"SELECT SO_Id AS SoId, EC_Id AS EcId, DateDejaDeclareeJusquau, UT_Id AS UtId, DateSaisie
              FROM DM_REPRISE_DELAIPAIEMENT WHERE SO_Id = @SocieteId AND EC_Id = @EcId",
            new { SocieteId = societeId, EcId = ecId });
    }

    public async Task SetAsync(int societeId, int ecId, DateTime dateDejaDeclareeJusquau, int? utilisateurId)
    {
        using var connection = _connectionFactory.CreatePersistenceConnection();
        await connection.ExecuteAsync(
            @"MERGE DM_REPRISE_DELAIPAIEMENT AS target
              USING (SELECT @SocieteId AS SO_Id, @EcId AS EC_Id) AS source
                 ON target.SO_Id = source.SO_Id AND target.EC_Id = source.EC_Id
              WHEN MATCHED THEN
                  UPDATE SET DateDejaDeclareeJusquau = @DateDejaDeclareeJusquau, UT_Id = @UtilisateurId, DateSaisie = SYSUTCDATETIME()
              WHEN NOT MATCHED THEN
                  INSERT (SO_Id, EC_Id, DateDejaDeclareeJusquau, UT_Id, DateSaisie)
                  VALUES (@SocieteId, @EcId, @DateDejaDeclareeJusquau, @UtilisateurId, SYSUTCDATETIME());",
            new { SocieteId = societeId, EcId = ecId, DateDejaDeclareeJusquau = dateDejaDeclareeJusquau, UtilisateurId = utilisateurId });
    }
}
