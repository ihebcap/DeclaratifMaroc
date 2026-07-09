using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Dapper;

namespace TestTask008
{
    class Program
    {
        static async Task Main(string[] args)
        {
            string connectionString = "Server=IHEB-PC\\SQL2022;Database=GR_EMA_DISTRIBUTION;Integrated Security=True;TrustServerCertificate=True;";
            
            try 
            {
                using var conn = new SqlConnection(connectionString);
                await conn.OpenAsync();

                var sql = @"
                    SELECT COUNT(DISTINCT l.DTL_MvNumero)
                    FROM RT_LigneDeclarationTva l 
                    WHERE l.DTL_TypePayement = 0 AND l.DTL_Domaine = 2
                ";
                var rows = await conn.QueryAsync<int>(sql);
                
                Console.WriteLine($"Distinct DTL_MvNumero pour Especes declarees : {rows.FirstOrDefault()}");

                var sql2 = @"
                    SELECT COUNT(DISTINCT m.MV_Id)
                    FROM RT_MOUVEMENT m
                    INNER JOIN RT_LigneDeclarationTva l ON l.DTL_MvNumero = m.MV_Numero
                    WHERE l.DTL_TypePayement = 0 AND m.SO_Id = 1 AND m.MV_Domaine = 1
                ";
                var rows2 = await conn.QueryAsync<int>(sql2);
                
                Console.WriteLine($"Distinct MV_Id pour ces memes especes dans RT_MOUVEMENT : {rows2.FirstOrDefault()}");
            }
            catch(Exception ex)
            {
                Console.WriteLine("Erreur : " + ex.Message);
            }
        }
    }
}
