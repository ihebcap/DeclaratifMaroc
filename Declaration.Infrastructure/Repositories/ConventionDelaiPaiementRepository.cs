using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Declaration.Application.Entities;
using Declaration.Application.Interfaces;
using Declaration.Core;

namespace Declaration.Infrastructure.Repositories;

/// <summary>
/// TASK-129 (Délai de Paiement Maroc — Convention par tiers) : repository Dapper sur la table
/// existante <c>RT_CONVENTIONTIERS</c> (propriété GRF — aucune migration, aucune modification de
/// schéma). Implémente DEUX contrats :
/// <list type="bullet">
/// <item><see cref="IConventionDelaiPaiementRepository"/> (TASK-127, lecture seule filtrée par domaine,
/// consommée par <c>DelaiPaiementService</c>/<c>EcheanceLegaleCalculator</c>).</item>
/// <item><see cref="IConventionDelaiPaiementTiersRepository"/> (TASK-129, CRUD complet).</item>
/// </list>
///
/// Mapping <see cref="DomaineDelaiPaiement"/> ↔ colonnes réelles — DEUX mappings DIFFÉRENTS,
/// vérifiés en code legacy (ne pas les confondre) :
/// <list type="bullet">
/// <item><c>RT_CONVENTIONTIERS.CP_Domaine</c> : mapping DIRECT — Achat=0/Fournisseur=0,
/// Vente=1/Client=1 (confirmé <c>Tresorerie.Core/Models/ConventionDelaisPaiementTiers.cs:25-29</c>,
/// enum <c>DomaineConvention</c>).</item>
/// <item><c>RT_ECHEANCE.DO_Domaine</c> (utilisé UNIQUEMENT par le contrôle "facture NonPayée") :
/// mapping INVERSÉ — Vente=0/Achat=1 (enum legacy <c>ErpDomaine</c>,
/// <c>Tresorerie.Core/Enum/IErpDomaine.cs:5-6</c ; confirmé par
/// <c>SocieteManager.Complement.cs:520</c> : <c>domaine == DomaineConvention.Client ? ErpDomaine.Vente
/// : ErpDomaine.Achat</c> — Fournisseur(0)→Achat(1), Client(1)→Vente(0)). Cf. <see cref="ToDoDomaineErp"/>.</item>
/// </list>
///
/// <c>CP_FactureNo</c> référence <c>RT_ECHEANCE.EC_Id</c> (PAS <c>EC_No</c>) — confirmé
/// <c>ConventionDelaisPaiementTiersRepository.Script.cs:24</c> (<c>LEFT JOIN RT_ECHEANCE e ON e.EC_Id =
/// c.CP_FactureNo</c>) et <c>SocieteManager.Complement.cs:523</c> (<c>x.No == factureNo.Value</c>, où
/// <c>Echeance.No</c> est mappé sur <c>EC_Id</c> — <c>Echeance.cs:12-14</c>).
///
/// LECTURE/ÉCRITURE strictement confinées à cette classe (ARCHITECTURE §5 : pas de SQL inline hors
/// couche repository). Aucune modification de schéma, aucune table créée.
/// </summary>
public sealed class ConventionDelaiPaiementRepository :
    IConventionDelaiPaiementRepository,
    IConventionDelaiPaiementTiersRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ConventionDelaiPaiementRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <summary>CP_Domaine : mapping DIRECT avec <see cref="DomaineDelaiPaiement"/> (Achat=0/Vente=1).</summary>
    private static int ToCpDomaine(DomaineDelaiPaiement domaine) => (int)domaine;

    /// <summary>
    /// DO_Domaine (RT_ECHEANCE, enum legacy ErpDomaine Vente=0/Achat=1) : mapping INVERSÉ de
    /// <see cref="DomaineDelaiPaiement"/> — voir le résumé de classe pour la preuve en code legacy.
    /// </summary>
    private static int ToDoDomaineErp(DomaineDelaiPaiement domaine) => domaine == DomaineDelaiPaiement.Achat ? 1 : 0;

    private const string SelectColonnes = @"
        c.CP_Id             AS CpId,
        c.SO_Id             AS SocieteId,
        c.CT_No             AS TiersNo,
        c.CT_Code           AS TiersCode,
        c.CP_Date           AS Date,
        c.CP_Numero         AS Numero,
        c.CP_DateDebut      AS DateDebut,
        c.CP_DateFin        AS DateFin,
        c.CP_FileName       AS FileName,
        c.CP_File           AS File,
        c.CP_DelaisPaiement AS NombreJoursDelaisPaiement,
        c.CP_Domaine        AS Domaine,
        c.CP_Type           AS Type,
        c.CP_FactureNo      AS FactureNo";

    // ─── IConventionDelaiPaiementRepository (TASK-127 : lecture seule pour le calculateur) ───────

    /// <summary>
    /// Retourne TOUTES les conventions (tous types) de la société pour le domaine donné — comme le
    /// legacy <c>ConventionDelaisPaiementTiersGetAll(domaine)</c>, sans filtre de validité
    /// supplémentaire : c'est le calculateur pur (<c>EcheanceLegaleCalculator</c>) qui départage
    /// Facture exacte / Convention (intervalle) / défaut. FactureNumero résolu par jointure
    /// RT_ECHEANCE (CP_FactureNo = EC_Id), comme le legacy.
    /// </summary>
    public async Task<IReadOnlyList<ConventionDelaiPaiement>> GetConventionsActivesAsync(int societeId, DomaineDelaiPaiement domaine)
    {
        using var connection = _connectionFactory.CreateGrfConnection();
        var rows = await connection.QueryAsync<ConventionDelaiPaiement>(@"
            SELECT
                c.CT_No             AS TiersNo,
                e.DO_Numero         AS FactureNumero,
                c.CP_DateDebut      AS DateDebut,
                c.CP_DateFin        AS DateFin,
                c.CP_DelaisPaiement AS NombreJoursDelaisPaiement
            FROM RT_CONVENTIONTIERS c
            LEFT JOIN RT_ECHEANCE e ON e.EC_Id = c.CP_FactureNo
            WHERE c.SO_Id = @SocieteId AND c.CP_Domaine = @Domaine",
            new { SocieteId = societeId, Domaine = ToCpDomaine(domaine) });

        return rows.ToList();
    }

    // ─── IConventionDelaiPaiementTiersRepository (TASK-129 : CRUD) ─────────────────────────────

    public async Task<int> CreateAsync(ConventionDelaiPaiementTiers convention)
    {
        using var connection = _connectionFactory.CreateGrfConnection();
        var cpId = await connection.QuerySingleAsync<int>(@"
            INSERT INTO RT_CONVENTIONTIERS
                (SO_Id, CT_No, CT_Code, CP_Date, CP_Numero, CP_DateDebut, CP_DateFin,
                 CP_FileName, CP_File, CP_DelaisPaiement, CP_Domaine, CP_Type, CP_FactureNo)
            VALUES
                (@SocieteId, @TiersNo, @TiersCode, @Date, @Numero, @DateDebut, @DateFin,
                 @FileName, @File, @NombreJoursDelaisPaiement, @Domaine, @Type, @FactureNo);
            SELECT CAST(SCOPE_IDENTITY() AS int);",
            new
            {
                convention.SocieteId,
                convention.TiersNo,
                convention.TiersCode,
                convention.Date,
                convention.Numero,
                convention.DateDebut,
                convention.DateFin,
                convention.FileName,
                convention.File,
                convention.NombreJoursDelaisPaiement,
                Domaine = ToCpDomaine(convention.Domaine),
                Type = (int)convention.Type,
                convention.FactureNo
            });

        return cpId;
    }

    public async Task<ConventionDelaiPaiementTiers?> GetAsync(int cpId)
    {
        using var connection = _connectionFactory.CreateGrfConnection();
        return await connection.QuerySingleOrDefaultAsync<ConventionDelaiPaiementTiers>(
            $"SELECT {SelectColonnes} FROM RT_CONVENTIONTIERS c WHERE c.CP_Id = @CpId",
            new { CpId = cpId });
    }

    public async Task<IReadOnlyList<ConventionDelaiPaiementTiers>> GetAllAsync(int societeId, DomaineDelaiPaiement domaine)
    {
        using var connection = _connectionFactory.CreateGrfConnection();
        var rows = await connection.QueryAsync<ConventionDelaiPaiementTiers>(
            $"SELECT {SelectColonnes} FROM RT_CONVENTIONTIERS c WHERE c.SO_Id = @SocieteId AND c.CP_Domaine = @Domaine",
            new { SocieteId = societeId, Domaine = ToCpDomaine(domaine) });
        return rows.ToList();
    }

    public async Task<IReadOnlyList<ConventionDelaiPaiementTiers>> GetAllForTiersAsync(int societeId, int tiersNo, DomaineDelaiPaiement domaine)
    {
        using var connection = _connectionFactory.CreateGrfConnection();
        var rows = await connection.QueryAsync<ConventionDelaiPaiementTiers>(
            $@"SELECT {SelectColonnes} FROM RT_CONVENTIONTIERS c
               WHERE c.SO_Id = @SocieteId AND c.CT_No = @TiersNo AND c.CP_Domaine = @Domaine",
            new { SocieteId = societeId, TiersNo = tiersNo, Domaine = ToCpDomaine(domaine) });
        return rows.ToList();
    }

    public async Task<bool> ExisteNumeroAsync(int societeId, int tiersNo, string numero, DomaineDelaiPaiement domaine)
    {
        using var connection = _connectionFactory.CreateGrfConnection();
        var count = await connection.QuerySingleAsync<int>(@"
            SELECT COUNT(1) FROM RT_CONVENTIONTIERS
            WHERE SO_Id = @SocieteId AND CT_No = @TiersNo AND CP_Numero = @Numero AND CP_Domaine = @Domaine",
            new { SocieteId = societeId, TiersNo = tiersNo, Numero = numero, Domaine = ToCpDomaine(domaine) });
        return count > 0;
    }

    /// <summary>
    /// Reproduit legacy <c>EcheanceGetAll(erpDomaine, tiersNo, Etat.NonPaye)?.FirstOrDefault(x => x.No
    /// == factureNo.Value)</c> : EC_Etat = 0 (Etat.NonPaye), DO_Domaine mappé via
    /// <see cref="ToDoDomaineErp"/> (INVERSÉ par rapport à CP_Domaine — voir résumé de classe),
    /// EC_Id = factureNo (PAS EC_No).
    /// </summary>
    public async Task<bool> EcheanceNonPayeeExisteAsync(int societeId, int tiersNo, DomaineDelaiPaiement domaine, int factureNo)
    {
        using var connection = _connectionFactory.CreateGrfConnection();
        var count = await connection.QuerySingleAsync<int>(@"
            SELECT COUNT(1) FROM RT_ECHEANCE
            WHERE SO_Id = @SocieteId AND CT_No = @TiersNo AND DO_Domaine = @DoDomaine
              AND EC_Etat = 0 AND EC_Id = @FactureNo",
            new { SocieteId = societeId, TiersNo = tiersNo, DoDomaine = ToDoDomaineErp(domaine), FactureNo = factureNo });
        return count > 0;
    }

    public async Task UpdateDateFinAsync(int cpId, DateTime nouvelleDateFin)
    {
        using var connection = _connectionFactory.CreateGrfConnection();
        await connection.ExecuteAsync(
            "UPDATE RT_CONVENTIONTIERS SET CP_DateFin = @NouvelleDateFin WHERE CP_Id = @CpId",
            new { CpId = cpId, NouvelleDateFin = nouvelleDateFin });
    }

    /// <summary>Suppression DIRECTE, sans garde — reproduit legacy à l'identique (décision assumée, cf. VERIFY TASK-129).</summary>
    public async Task DeleteAsync(int cpId)
    {
        using var connection = _connectionFactory.CreateGrfConnection();
        await connection.ExecuteAsync("DELETE FROM RT_CONVENTIONTIERS WHERE CP_Id = @CpId", new { CpId = cpId });
    }
}
