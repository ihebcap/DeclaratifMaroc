using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Declaration.Controle;
using Declaration.Selection;
using Declaration.Orchestration;
using Microsoft.Data.SqlClient;

namespace Declaration.Controle.Tests
{
    public class ComparateurTests
    {
        private readonly ITestOutputHelper _output;

        public ComparateurTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task GenererRapportVerification()
        {
            // Set up dependencies
            var connString = "Server=.\\sql2022;Database=GR_EMA_DISTRIBUTION;User Id=sa;Password=1234;TrustServerCertificate=True;";
            var sageConnString = "Server=.\\sql2022;Database=NEW_EMA DISTRIBUTION;User Id=sa;Password=1234;TrustServerCertificate=True;";
            var grfnRepo = new GrfnDeclarationRepository(() => new SqlConnection(connString));
            var selService = new SelectionnerAffectationsService();
            
            var config = new WorkerConfig
            {
                WorkerExePath = @"D:\_vibe\GRF\SageTaxReader\SageTaxReader.Console\bin\Debug\net48\SageTaxReader.Console.exe",
                Server = ".\\sql2022",
                Database = "NEW_EMA DISTRIBUTION",
                User = "sa",
                Password = "1234"
            };
            var invoker = new WorkerInvoker();
            var lecteur = new Declaration.Orchestration.LecteurTvaFgr();
            var orch = new OrchestrateurDeclaration(invoker, config, lecteur, connString, sageConnString);

            var comp = new ComparateurDeclarationService(grfnRepo, selService, orch);

            var rapport = await comp.Comparer(66, connString); // Test with DT_Id 66

            _output.WriteLine("Concordants: " + rapport.NbConcordants);
            _output.WriteLine("Ecarts de montant: " + rapport.NbEcartsMontant);
            _output.WriteLine("Manquants GRFN: " + rapport.NbManquantsGRFN);
            _output.WriteLine("Manquants Recalcul: " + rapport.NbManquantsRecalcul);
            _output.WriteLine("Total Ecart TVA: " + rapport.TotalEcartTVA);

            foreach (var l in rapport.Lignes)
            {
                if (l.Type != TypeEcart.Concordant)
                {
                    _output.WriteLine($"Type: {l.Type} | Facture: {l.NumeroFacture} | Taux: {l.Taux} | Rapproch: {l.NumeroRapprochement} | EcartAssiette: {l.EcartAssiette} | EcartTVA: {l.EcartTVA} | Cause: {l.CauseHypothetique}");
                }
            }
        }
    }
}
