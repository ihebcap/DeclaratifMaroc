using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
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
        return string.IsNullOrEmpty(cs) ? new SqliteConnection("Data Source=tva.db") : new SqlConnection(cs);
    }

    public string GetGrfConnectionString()
    {
        return _configuration.GetConnectionString("GrfConnection") ?? "Data Source=tva.db";
    }

    public IDbConnection CreateSageConnection()
    {
        var cs = _configuration.GetConnectionString("SageConnection");
        return string.IsNullOrEmpty(cs) ? new SqliteConnection("Data Source=tva.db") : new SqlConnection(cs);
    }

    public IDbConnection CreatePersistenceConnection()
    {
        var cs = _configuration.GetConnectionString("PersistenceConnection");
        if (string.IsNullOrEmpty(cs)) return new SqliteConnection("Data Source=tva.db");
        if (cs.Contains(".db") || (cs.Contains("Data Source=") && !cs.Contains("Server=") && !cs.Contains("Initial Catalog=")))
            return new SqliteConnection(cs);
        return new SqlConnection(cs);
    }
}
