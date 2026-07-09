using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;

namespace Declaration.Controle;

public class GrfnDeclarationRepository : IGrfnDeclarationRepository
{
    private readonly Func<IDbConnection> _connectionFactory;

    public GrfnDeclarationRepository(Func<IDbConnection> connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<DeclarationGrfnInfo?> GetDeclarationInfoAsync(int dtId)
    {
        using var connection = _connectionFactory();
        var sql = @"SELECT DT_Id, SO_Id AS SocieteId, DT_DateDebut AS DateDebut, DT_DateFin AS DateFin 
                    FROM RT_DeclarationTva 
                    WHERE DT_Id = @DtId";
        return await connection.QuerySingleOrDefaultAsync<DeclarationGrfnInfo>(sql, new { DtId = dtId });
    }

    public async Task<IEnumerable<LigneDeclarationGrfn>> GetLignesDeclarationAsync(int dtId)
    {
        using var connection = _connectionFactory();
        var sql = @"SELECT 
                        DTL_Id, DTL_Assiette, DTL_Taux, DTL_Montant, DTL_TiersCode, 
                        DTL_DocNumero, DTL_DocDate, DTL_MvNumero, DTL_MvDate, 
                        DTL_TypePayement, DTL_EntityType, DTL_Domaine, DTL_Prorata 
                    FROM RT_LigneDeclarationTva 
                    WHERE DT_Id = @DtId";
        return await connection.QueryAsync<LigneDeclarationGrfn>(sql, new { DtId = dtId });
    }
}
