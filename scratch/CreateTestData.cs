using System;
using Microsoft.Data.SqlClient;
using Dapper;
using System.Threading.Tasks;

class Program {
    static async Task Main() {
        var connStr = ""Server=IHEB-PC\\SQL2022;Database=GR_EMA_DISTRIBUTION;Integrated Security=True;TrustServerCertificate=True;"";
        using var conn = new SqlConnection(connStr);
        await conn.OpenAsync();
        
        // Find if our test data exists
        var checkSql = ""SELECT COUNT(*) FROM RT_MOUVEMENT WHERE MV_Identifiant = 'IF_TEST_017'"";
        var count = await conn.ExecuteScalarAsync<int>(checkSql);
        
        if (count == 0) {
            Console.WriteLine(""Inserting test data..."");
            
            // Insert ECHEANCE
            var sqlEcheance = @""INSERT INTO RT_ECHEANCE (EC_Point, EC_DatePoint, DO_Numero, DO_Type, DO_Domaine, DO_Date, EC_Montant, EC_Solde)
                VALUES (1, @Dt, 'FC2600578', 16, 1, @Dt, 1000, 0); SELECT SCOPE_IDENTITY();"";
            var ecId = await conn.ExecuteScalarAsync<int>(sqlEcheance, new { Dt = new DateTime(2026, 1, 15) });
            
            // Insert MOUVEMENT
            var sqlMvt = @""INSERT INTO RT_MOUVEMENT (SO_Id, MV_Domaine, MV_Point, MV_PointDate, MV_Date, MV_DECAISSE, MV_Compta, MV_Annule, MV_Impaye, MV_Type, CT_Code, CT_Intitule, MV_Identifiant, MV_Ice)
                VALUES (1, 2, 1, @Dt, @Dt, 1, 1, 0, 0, 1, 'F001', 'FOURNISSEUR TEST', 'IF_TEST_017', 'ICE123'); SELECT SCOPE_IDENTITY();"";
            var mvId = await conn.ExecuteScalarAsync<int>(sqlMvt, new { Dt = new DateTime(2026, 1, 15) });
            
            // Insert AFFECTATION
            var sqlAff = @""INSERT INTO RT_AFFECTATION (AF_No, AF_Date, AF_Montant, MV_Id, EC_Id, DT_Id)
                VALUES (999, @Dt, 1000, @MvId, @EcId, NULL);"";
            await conn.ExecuteAsync(sqlAff, new { Dt = new DateTime(2026, 1, 15), MvId = mvId, EcId = ecId });
            
            Console.WriteLine(""Test data inserted successfully."");
        } else {
            Console.WriteLine(""Test data already exists."");
            // Free it up just in case
            await conn.ExecuteAsync(""UPDATE RT_AFFECTATION SET DT_Id = NULL WHERE MV_Id IN (SELECT MV_Id FROM RT_MOUVEMENT WHERE MV_Identifiant = 'IF_TEST_017')"");
        }
    }
}
