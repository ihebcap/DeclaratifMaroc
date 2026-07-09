using System.Data;

namespace Declaration.Application.Interfaces;

public interface IDbConnectionFactory
{
    IDbConnection CreateGrfConnection();
    string GetGrfConnectionString();
    IDbConnection CreateSageConnection();
    IDbConnection CreatePersistenceConnection();
}
