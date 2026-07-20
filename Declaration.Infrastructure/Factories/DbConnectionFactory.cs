using System.Data;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Declaration.Application.Interfaces;

namespace Declaration.Infrastructure.Factories;

public class DbConnectionFactory : IDbConnectionFactory
{
    private readonly IConfiguration _configuration;

    public DbConnectionFactory(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public IDbConnection CreateGrfConnection()
    {
        var cs = _configuration.GetConnectionString("GrfConnection");
        if (string.IsNullOrEmpty(cs))
            throw new InvalidOperationException("Chaîne de connexion 'GrfConnection' non configurée (connections.json / appsettings.json).");
        return new SqlConnection(cs);
    }

    public string GetGrfConnectionString()
    {
        var cs = _configuration.GetConnectionString("GrfConnection");
        if (string.IsNullOrEmpty(cs))
            throw new InvalidOperationException("Chaîne de connexion 'GrfConnection' non configurée (connections.json / appsettings.json).");
        return cs;
    }

    public IDbConnection CreatePersistenceConnection()
    {
        var cs = _configuration.GetConnectionString("PersistenceConnection");
        if (string.IsNullOrEmpty(cs))
            throw new InvalidOperationException("Chaîne de connexion 'PersistenceConnection' non configurée (connections.json / appsettings.json).");
        return new SqlConnection(cs);
    }

    private class SocieteErpRow
    {
        public string? SO_ErpDb { get; set; }
        public string? SO_ErpUserApp { get; set; }
        public string? SO_ErpPasswdApp { get; set; }
    }

    /// <summary>
    /// TASK-118 : Server/User/Password proviennent TOUJOURS de GrfConnection (exigence PO
    /// explicite, 18/07/2026) — seule Database est remplacée par P_SOCIETE.SO_ErpDb. Les colonnes
    /// SO_ErpServer/SO_ErpUser/SO_ErpPasswd/SO_ErpAuth/SO_UseObjetMetier ne sont ni lues ni
    /// utilisées (hors périmètre GRF, tranché PO).
    /// </summary>
    public async Task<SageConnectionInfo> GetSageConnectionInfoAsync(int soId)
    {
        var grfCs = GetGrfConnectionString();

        SocieteErpRow? row;
        using (var conn = new SqlConnection(grfCs))
        {
            row = await conn.QuerySingleOrDefaultAsync<SocieteErpRow>(
                "SELECT SO_ErpDb, SO_ErpUserApp, SO_ErpPasswdApp FROM P_SOCIETE WHERE SO_Id = @SoId",
                new { SoId = soId });
        }

        if (row == null)
            throw new InvalidOperationException(
                $"Résolution de la connexion Sage impossible : société SO_Id={soId} introuvable dans P_SOCIETE.");
        if (string.IsNullOrWhiteSpace(row.SO_ErpDb))
            throw new InvalidOperationException(
                $"Résolution de la connexion Sage impossible : société SO_Id={soId} n'a pas de SO_ErpDb renseigné dans P_SOCIETE.");

        var builder = new SqlConnectionStringBuilder(grfCs) { InitialCatalog = row.SO_ErpDb };

        return new SageConnectionInfo
        {
            ConnectionString = builder.ConnectionString,
            OmUser = row.SO_ErpUserApp,
            OmPassword = row.SO_ErpPasswdApp
        };
    }
}
