using System.Data;
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

    public IDbConnection CreateSageConnection()
    {
        var cs = _configuration.GetConnectionString("SageConnection");
        if (string.IsNullOrEmpty(cs))
            throw new InvalidOperationException("Chaîne de connexion 'SageConnection' non configurée (connections.json / appsettings.json).");
        return new SqlConnection(cs);
    }

    public IDbConnection CreatePersistenceConnection()
    {
        var cs = _configuration.GetConnectionString("PersistenceConnection");
        if (string.IsNullOrEmpty(cs))
            throw new InvalidOperationException("Chaîne de connexion 'PersistenceConnection' non configurée (connections.json / appsettings.json).");
        return new SqlConnection(cs);
    }
}
