using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.Data.SqlClient;
using Dapper;
using Declaration.Orchestration;
using Declaration.Core;
using SageTaxReader.Contracts;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        string grfConnectionString = "Server=.\\sql2022;Database=GR_EMA_DISTRIBUTION;User Id=sa;Password=1234;TrustServerCertificate=True;";
        
        using (var connection = new SqlConnection(grfConnectionString))
        {
            var ecTypeStats = connection.Query(@"
                SELECT EC_Type, COUNT(*) as Count 
                FROM RT_ECHEANCE 
                GROUP BY EC_Type").ToList();
            
            Console.WriteLine("Stats EC_Type dans RT_ECHEANCE :");
            foreach(var stat in ecTypeStats)
            {
                Console.WriteLine($"EC_Type: {stat.EC_Type} => {stat.Count} factures");
            }
        }
        
        var lecteur = new LecteurTvaFgr();
        
        // FGR = 111, FF260070
        string numeroFacture = "FF260070";
        int ecId = 0;
        int ecIdMulti = 0;
        int ecIdExo = 0;
        using (var connection = new SqlConnection(grfConnectionString))
        {
            ecId = connection.QueryFirstOrDefault<int>("SELECT EC_Id FROM RT_ECHEANCE WHERE DO_Numero = @Ref AND EC_Type = 111", new { Ref = numeroFacture });
            
            // Insert Fake Multi-Taux if not exist
            ecIdMulti = connection.QueryFirstOrDefault<int>("SELECT EC_Id FROM RT_ECHEANCE WHERE DO_Numero = 'FF_MULTI'");
            if (ecIdMulti == 0)
            {
                ecIdMulti = connection.QuerySingle<int>("INSERT INTO RT_ECHEANCE (DO_Numero, EC_Type) OUTPUT INSERTED.EC_Id VALUES ('FF_MULTI', 111)");
                connection.Execute("INSERT INTO RT_HISTCOMPTA (MV_Id, HC_Indice, HC_Montant, HC_TaxeCode) VALUES (@id, 1, 1550, '')", new { id = ecIdMulti });
                connection.Execute("INSERT INTO RT_HISTCOMPTA (MV_Id, HC_Indice, HC_Montant, HC_TaxeCode) VALUES (@id, 2, 1000, 'D20')", new { id = ecIdMulti });
                connection.Execute("INSERT INTO RT_HISTCOMPTA (MV_Id, HC_Indice, HC_Montant, HC_TaxeCode) VALUES (@id, 2, 200, '')", new { id = ecIdMulti });
                connection.Execute("INSERT INTO RT_HISTCOMPTA (MV_Id, HC_Indice, HC_Montant, HC_TaxeCode) VALUES (@id, 3, 300, 'D10')", new { id = ecIdMulti });
                connection.Execute("INSERT INTO RT_HISTCOMPTA (MV_Id, HC_Indice, HC_Montant, HC_TaxeCode) VALUES (@id, 3, 50, '')", new { id = ecIdMulti });
            }

            // Insert Fake Exonere if not exist
            ecIdExo = connection.QueryFirstOrDefault<int>("SELECT EC_Id FROM RT_ECHEANCE WHERE DO_Numero = 'FF_EXO'");
            if (ecIdExo == 0)
            {
                ecIdExo = connection.QuerySingle<int>("INSERT INTO RT_ECHEANCE (DO_Numero, EC_Type) OUTPUT INSERTED.EC_Id VALUES ('FF_EXO', 111)");
                connection.Execute("INSERT INTO RT_HISTCOMPTA (MV_Id, HC_Indice, HC_Montant, HC_TaxeCode) VALUES (@id, 1, 500, '')", new { id = ecIdExo });
                connection.Execute("INSERT INTO RT_HISTCOMPTA (MV_Id, HC_Indice, HC_Montant, HC_TaxeCode) VALUES (@id, 2, 500, '')", new { id = ecIdExo });
            }
        }
        
        TestFgr(ecId, numeroFacture, grfConnectionString, lecteur);
        TestFgr(ecIdMulti, "FF_MULTI", grfConnectionString, lecteur);
        TestFgr(ecIdExo, "FF_EXO", grfConnectionString, lecteur);
    }

    static void TestFgr(int ecId, string numeroFacture, string connStr, ILecteurTvaFgr lecteur)
    {
        if(ecId != 0)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var doc = lecteur.LireTvaFgrAsync(ecId, numeroFacture, connStr).GetAwaiter().GetResult();
            sw.Stop();

            Console.WriteLine($"\nVentilation FGR {numeroFacture} (via SQL) - Temps : {sw.ElapsedMilliseconds} ms");
            if(doc != null)
            {
                foreach (var l in doc.LignesTaxe)
                {
                    Console.WriteLine($"Base HT: {l.BaseHT}, Taux: {l.Taux}, Montant TVA: {l.MontantTva}");
                }
                Console.WriteLine($"Total HT: {doc.TotalHT}, Total TVA: {doc.TotalTva}, Total TTC: {doc.TotalTtc}");
            }
            else
            {
                Console.WriteLine("Result is null!");
            }
        }
        else
        {
            Console.WriteLine($"\nFGR {numeroFacture} not found in DB.");
        }
    }
}
