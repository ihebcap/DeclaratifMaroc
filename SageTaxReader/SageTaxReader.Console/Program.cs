using System;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SageTaxReader.Core;
using SageTaxReader.Contracts;

namespace SageTaxReader.ConsoleApp;


public class BatchRequest
{
    public string NumeroPiece { get; set; } = "";
    public string Sens { get; set; } = "";
}

class Program
{
    static int Main(string[] args)
    {
        
        if (args.Length > 0 && args[0].Equals("batch", StringComparison.OrdinalIgnoreCase))
        {
            // Mode BATCH JSON
            // La liste JSON { NumeroPiece, Sens } est lue sur stdin (pas d'argument :
            // évite la limite de longueur de ligne de commande Windows sur gros volumes).
            string jsonInput = Console.In.ReadToEnd();

            string server = "";
            string database = "";
            string user = "";
            string pwd = "";
            if (args.Length >= 5)
            {
                server = args[1];
                database = args[2];
                user = args[3];
                pwd = args[4];
            }
            else
            {
                var builder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: true);
                var config = builder.Build();
                server = config["SageConnection:Server"] ?? "";
                database = config["SageConnection:Database"] ?? "";
                user = config["SageConnection:User"] ?? "";
                pwd = config["SageConnection:Password"] ?? "";
            }
            
            try
            {
                var requetes = JsonSerializer.Deserialize<List<BatchRequest>>(jsonInput);
                if (requetes == null) return 1;
                
                var requetesTuples = new List<(string, string)>();
                foreach (var req in requetes)
                {
                    requetesTuples.Add((req.NumeroPiece, req.Sens));
                }
                
                // TASK-159 : streaming NDJSON — une ligne JSON par pièce, flushée immédiatement,
                // au lieu d'un seul tableau JSON écrit à la toute fin. Permet à WorkerInvoker
                // (process appelant) de récupérer les pièces déjà rendues si ce process est tué
                // sur timeout avant la fin du lot (gros volume), au lieu de tout perdre.
                var service = new SageTaxReaderService(server, database, user, pwd);
                service.LireFactures(requetesTuples, (key, doc) =>
                {
                    Console.Out.Write(JsonSerializer.Serialize(doc));
                    Console.Out.Write('\n');
                    Console.Out.Flush();
                });

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                return 3;
            }
        }
        if (args.Length >= 2)
        {
            // Mode JSON
            string numero = args[0];
            string sens = args[1]; // "Vente" ou "Achat"
            
            string server = "";
            string database = "";
            string user = "";
            string pwd = "";

            if (args.Length >= 6)
            {
                server = args[2];
                database = args[3];
                user = args[4];
                pwd = args[5];
            }
            else
            {
                var builder = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: true);
                var config = builder.Build();
                server = config["SageConnection:Server"] ?? "";
                database = config["SageConnection:Database"] ?? "";
                user = config["SageConnection:User"] ?? "";
                pwd = config["SageConnection:Password"] ?? "";
            }

            try
            {
                var service = new SageTaxReaderService(server, database, user, pwd);
                DocumentTaxesInfo? result = null;

                if (sens.Equals("Vente", StringComparison.OrdinalIgnoreCase))
                {
                    result = service.LireFactureVente(numero);
                }
                else if (sens.Equals("Achat", StringComparison.OrdinalIgnoreCase))
                {
                    result = service.LireFactureAchat(numero);
                }
                else
                {
                    Console.Error.WriteLine($"Sens inconnu: {sens}");
                    return 1;
                }

                if (result != null)
                {
                    var json = JsonSerializer.Serialize(result);
                    Console.WriteLine(json);
                    return 0;
                }
                else
                {
                    Console.Error.WriteLine($"Facture introuvable: {numero} ({sens})");
                    return 2;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                return 3;
            }
        }

        // Mode POC Interactif
        var pocBuilder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false);

        IConfiguration pocConfig = pocBuilder.Build();

        string pocServer = pocConfig["SageConnection:Server"] ?? "";
        string pocDatabase = pocConfig["SageConnection:Database"] ?? "";
        string pocUser = pocConfig["SageConnection:User"] ?? "";
        string pocPwd = pocConfig["SageConnection:Password"] ?? "";

        string facVente = pocConfig["PocData:FactureVente"] ?? "";
        string facAchat = pocConfig["PocData:FactureAchat"] ?? "";

        string[] passwords = { "", "GocomXYZ", "admin", "sage", "1234", "distri", "demo", "administrateur", "sage100", "bijou" };
        foreach (var p in passwords)
        {
            var serviceTest = new SageTaxReaderService(pocServer, pocDatabase, pocUser, p);
            try
            {
                serviceTest.VerifierDisponibilite();
                Console.WriteLine($"[SUCCES] Connecté avec : '{p}'");
                pocPwd = p;
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ECHEC] Mdp '{p}' : {ex.Message}");
            }
        }
        var pocService = new SageTaxReaderService(pocServer, pocDatabase, pocUser, pocPwd);
        try
        {
            Console.WriteLine("Vérification de la disponibilité de la base...");
            pocService.VerifierDisponibilite();
            
            var ventes = pocService.LireFactureVente(facVente);
            AfficherResultat(ventes);

            var achats = pocService.LireFactureAchat(facAchat);
            AfficherResultat(achats);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur: {ex.Message}");
            if (ex.InnerException != null)
                Console.WriteLine($"Details: {ex.InnerException.Message}");
        }

        return 0;
    }

    static void AfficherResultat(DocumentTaxesInfo info)
    {
        Console.WriteLine(new string('-', 50));
        Console.WriteLine($"DO_Piece: {info.NumeroPiece} | Type: {info.TypeDocument} | Sens: {info.Sens}");
        Console.WriteLine(new string('-', 50));
        
        foreach (var t in info.LignesTaxe)
        {
            Console.WriteLine($"Taxe: {t.Code} ({t.Type}) | Taux: {t.Taux}% | BaseHT: {t.BaseHT:F2} | MontantTVA: {t.MontantTva:F2} | TTC: {t.TTC:F2}");
        }
        
        Console.WriteLine(new string('-', 50));
        Console.WriteLine($"Totaux OM  -> Total HT document: {info.TotalHT:F2} (dont Frais: {info.Frais:F2})");
        Console.WriteLine($"Escompte   -> {info.Escompte:F2}");
        Console.WriteLine($"Total HT Net -> {info.TotalHTNet:F2}");
        Console.WriteLine($"TVA Réelle -> {info.TotalTva:F2}");
        Console.WriteLine($"Parafiscale-> {info.TotalParafiscale:F2}");
        Console.WriteLine($"Arrondi    -> {info.EcartArrondi:F2}");
        Console.WriteLine($"Total TTC  -> {info.TotalTtc:F2}");
        
        if (Math.Abs(info.EcartArrondi) > 0.01)
        {
            Console.WriteLine($"[ATTENTION] Écart d'arrondi détecté : {info.EcartArrondi:F2}");
        }
        else
        {
            Console.WriteLine("[OK] L'écart est entièrement décomposé et justifié.");
        }

        Console.WriteLine("JSON Dump:");
        Console.WriteLine(JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine();
    }
}


