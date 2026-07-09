using System;
using System.Data;
using Microsoft.Data.SqlClient;
using Dapper;

var connStr = "Server=IHEB-PC\\SQL2022;Database=DISTRI_DEMO;Integrated Security=True;TrustServerCertificate=True;";
try
{
    using var conn = new SqlConnection(connStr);
    conn.Open();

    var dossier = conn.QueryFirstOrDefault("SELECT * FROM P_DOSSIER");
    Console.WriteLine("=== P_DOSSIER ===");
    foreach (var kvp in (System.Collections.Generic.IDictionary<string, object>)dossier)
    {
        if (kvp.Key.Contains("Devise") || kvp.Key.Contains("Format") || kvp.Key.Contains("Dec"))
            Console.WriteLine($"{kvp.Key}: {kvp.Value}");
    }

    Console.WriteLine("\n=== P_DEVISE ===");
    var devises = conn.Query("SELECT * FROM P_DEVISE");
    int i = 1;
    foreach (var devise in devises)
    {
        var dict = (System.Collections.Generic.IDictionary<string, object>)devise;
        Console.WriteLine($"Devise {i++}: cbIndice={dict["cbIndice"]} | {dict["D_Intitule"]} | {dict["D_Format"]} | Monnaie: {dict["D_Monnaie"]}");
    }
}
catch (Exception ex)
{
    Console.WriteLine(ex.ToString());
}
