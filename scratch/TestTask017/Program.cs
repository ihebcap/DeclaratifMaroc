using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Declaration.Core.Model;
using Declaration.Application.Interfaces;
using Declaration.Application.Services;
using Declaration.Infrastructure.Factories;
using Declaration.Infrastructure.Repositories;
using Declaration.Selection;
using Dapper;

namespace TestTask017 {
    class Program {
        static async Task Main() {
            var inMemoryConfig = new Dictionary<string, string?> {
                {"ConnectionStrings:SageConnection", "Server=IHEB-PC\\SQL2022;Database=NEW_EMA DISTRIBUTION;Integrated Security=True;TrustServerCertificate=True;"},
                {"WorkerConfig:WorkerExePath", @"D:\_vibe\GRF\SageTaxReader\SageTaxReader.Console\bin\Debug\net48\SageTaxReader.Console.exe"}
            };
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
            services.AddSingleton<IDbConnectionFactory>(new DbConnectionFactory("Data Source=tva_test.db", "Server=IHEB-PC\\SQL2022;Database=GR_EMA_DISTRIBUTION;Integrated Security=True;TrustServerCertificate=True;"));
            services.AddScoped<IDeclarationRepository, DeclarationRepository>();
            services.AddScoped<ISelectionExpliqueeService, SelectionnerAffectationsService>();
            services.AddScoped<DeclarationWorkflowService>();
            var sp = services.BuildServiceProvider();

            // Init DB
            var factory = sp.GetRequiredService<IDbConnectionFactory>();
            using (var c = factory.CreatePersistenceConnection()) {
                c.Open();
                c.Execute(@"
                    CREATE TABLE IF NOT EXISTS DeclarationEntete (Id TEXT PRIMARY KEY, Numero TEXT UNIQUE, SocieteId TEXT, Exercice INTEGER, Type INTEGER, Periode INTEGER, Statut INTEGER, DateCreation TEXT, DateCloture TEXT);
                    CREATE TABLE IF NOT EXISTS LigneCandidate (Id TEXT PRIMARY KEY, DeclarationId TEXT, Etat INTEGER, Domaine TEXT, MotifRejet TEXT, NumeroFacture TEXT, TiersNom TEXT, TiersIdentifiantFiscal TEXT, TiersICE TEXT, HT REAL, Taux REAL, TVA REAL, TTC REAL, ModePaiement TEXT, DatePaiement TEXT, DateFacture TEXT, Source TEXT, FOREIGN KEY (DeclarationId) REFERENCES DeclarationEntete(Id));
                ");
            }

            var workflow = sp.GetRequiredService<DeclarationWorkflowService>();
            var decl = await workflow.CreerDeclarationAsync("1", 2026, 6, Declaration.Application.Entities.TypePeriode.Mensuelle);
            Console.WriteLine($"Declaration created: {decl.Id}");

            await workflow.ChargerCandidatesSiNecessaireAsync(decl.Id, "Client");

            using (var c = factory.CreatePersistenceConnection()) {
                var lignes = await c.QueryAsync<Declaration.Application.Entities.LigneCandidate>("SELECT * FROM LigneCandidate WHERE DeclarationId = @Id", new { Id = decl.Id.ToString() });
                Console.WriteLine($"Total candidates: {lignes.Count()}");
                
                var eligibles = lignes.Where(l => l.Etat == Declaration.Application.Entities.EtatLigne.Proposee);
                Console.WriteLine($"Eligibles: {eligibles.Count()}");
                foreach(var e in eligibles) {
                    Console.WriteLine($"Facture: {e.NumeroFacture} HT: {e.HT} TVA: {e.TVA} TTC: {e.TTC} Taux: {e.Taux}");
                }
                
                var exclues = lignes.Where(l => l.Etat == Declaration.Application.Entities.EtatLigne.Exclue);
                Console.WriteLine($"Exclues: {exclues.Count()} (Not 'Déjà déclaré' shown below):");
                foreach(var e in exclues.Where(ex => !ex.MotifRejet.Contains("Déjà déclaré")).Take(10)) {
                    Console.WriteLine($"Facture: {e.NumeroFacture} Motif: {e.MotifRejet}");
                }

                // Simulate integration
                c.Execute($"UPDATE LigneCandidate SET Etat = 2 WHERE DeclarationId = '{decl.Id}' AND Etat = 0");
            }

            var checkup = await workflow.GetCheckupAsync(decl.Id);
            Console.WriteLine($"Alerts: {checkup.Alertes.Count}");
            foreach(var a in checkup.Alertes) Console.WriteLine($"{a.Niveau} - {a.Message} ({a.RefLigne})");
            Console.WriteLine($"Residu: {checkup.ControleEquilibre.ResiduInexplique}");

            if (!checkup.Alertes.Any(a => a.Niveau == NiveauAlerte.Error)) {
                await workflow.CloturerDeclarationAsync(decl.Id);
                Console.WriteLine("Cloture OK.");
            }
        }
    }
}
