using System;
using Microsoft.Data.SqlClient;
using Dapper;

class Program {
    static void Main() {
        var connStr = ""Server=IHEB-PC\\SQL2022;Database=GR_EMA_DISTRIBUTION;Integrated Security=True;TrustServerCertificate=True;"";
        using var conn = new SqlConnection(connStr);
        conn.Open();
        var date = conn.QueryFirstOrDefault<DateTime?>(""SELECT MAX(M.MV_PointDate) FROM RT_MOUVEMENT M INNER JOIN RT_AFFECTATION A ON M.MV_Id = A.MV_Id WHERE M.MV_Domaine = 2 AND M.MV_DECAISSE = 1 AND M.MV_Point = 1 AND A.AF_Id IS NOT NULL AND A.DT_Id IS NULL"");
        Console.WriteLine(""Max PointDate with DT_Id IS NULL: "" + date);
    }
}
