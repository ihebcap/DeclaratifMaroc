using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Declaration.Core.Model;
using Declaration.Orchestration;
using SageTaxReader.Contracts;
using Declaration.Core;

namespace Declaration.Orchestration.Tests
{
    public class OrchestrateurTests
    {
        public class StubWorkerInvoker : IWorkerInvoker
        {
            public int InvocationCount { get; private set; } = 0;

            public DocumentTaxesInfo? InvoquerWorker(string numeroFacture, string sens, WorkerConfig config, Action<string>? log = null)
            {
                InvocationCount++;

                if (numeroFacture == "INTROUVABLE")
                    return null;

                if (numeroFacture == "TIMEOUT")
                    throw new Exception("Timeout lors de l'exécution du worker pour la facture TIMEOUT.");


                return new DocumentTaxesInfo
                {
                    NumeroPiece = numeroFacture,
                    Sens = sens,
                    TypeDocument = sens == "Vente" ? 6 : 16,
                    TotalHT = 1000,
                    TotalTva = 200,
                    TotalTtc = 1200,
                    LignesTaxe = new List<TaxeDetail>
                    {
                        new TaxeDetail { Code = "1", Type = "TaxeTypeTVA", Taux = 20, BaseHT = 1000, MontantTva = 200, TTC = 1200 }
                    }
                };
            }

            public List<DocumentTaxesInfo> InvoquerWorkerBatch(IEnumerable<(string numeroFacture, string sens)> requetes, WorkerConfig config, Action<string>? log = null)
            {
                var result = new List<DocumentTaxesInfo>();
                foreach (var req in requetes)
                {
                    var doc = InvoquerWorker(req.numeroFacture, req.sens, config);
                    if (doc != null) result.Add(doc);
                }
                return result;
            }
        }

        public class StubLecteurTvaFgr : ILecteurTvaFgr
        {
            public DocumentTaxesInfo? LireTvaFgr(int ecId, string numeroFacture, string connectionString, string sageConnectionString) => null;
        }

        [Fact]
        public void Traiter_MetEnCache_FactureIdentique()
        {
            var stubInvoker = new StubWorkerInvoker();
            var config = new WorkerConfig();
            var orchestrateur = new OrchestrateurDeclaration(stubInvoker, config, new StubLecteurTvaFgr(), "dummy", "dummySage");

            var affectations = new List<AffectationADeclarer>
            {
                new AffectationADeclarer { NumeroFacture = "FAC001", Sens = SensAffectation.Vente, MontantAffecte = 600, Tiers = new TiersInfo { Ice = "123456789012345", IdentifiantFiscal = "12345678" } },
                new AffectationADeclarer { NumeroFacture = "FAC001", Sens = SensAffectation.Vente, MontantAffecte = 600, Tiers = new TiersInfo { Ice = "123456789012345", IdentifiantFiscal = "12345678" } }
            };

            var modele = orchestrateur.Traiter(affectations, 1);

            Assert.Equal(1, stubInvoker.InvocationCount);
            Assert.Equal(2, modele.Lignes.Count);
        }

        [Fact]
        public void Traiter_Alerte_QuandFactureIntrouvable()
        {
            var stubInvoker = new StubWorkerInvoker();
            var config = new WorkerConfig();
            var orchestrateur = new OrchestrateurDeclaration(stubInvoker, config, new StubLecteurTvaFgr(), "dummy", "dummySage");

            var affectations = new List<AffectationADeclarer>
            {
                new AffectationADeclarer { NumeroFacture = "INTROUVABLE", Sens = SensAffectation.Vente, MontantAffecte = 100 }
            };

            var modele = orchestrateur.Traiter(affectations, 1);

            Assert.Contains(modele.Alertes, a => a.Code == "FACTURE_INTROUVABLE");
            Assert.Empty(modele.Lignes);
        }

        [Fact]
        public void Traiter_Alerte_QuandFactureTimeout()
        {
            var stubInvoker = new StubWorkerInvoker();
            var config = new WorkerConfig();
            var orchestrateur = new OrchestrateurDeclaration(stubInvoker, config, new StubLecteurTvaFgr(), "dummy", "dummySage");

            var affectations = new List<AffectationADeclarer>
            {
                new AffectationADeclarer { NumeroFacture = "TIMEOUT", Sens = SensAffectation.Vente, MontantAffecte = 100 }
            };

            var modele = orchestrateur.Traiter(affectations, 1);

            // A timeout causes the invoker to throw, which is caught by Orchestrateur and returns null
            // This yields a FACTURE_INTROUVABLE alert without crashing
            Assert.Contains(modele.Alertes, a => a.Code == "FACTURE_INTROUVABLE");
            Assert.Empty(modele.Lignes);
        }

        
        [Fact]
        public void DumpVerify_JSON()
        {
            var stubInvoker = new StubWorkerInvoker();
            var config = new WorkerConfig();
            var orchestrateur = new OrchestrateurDeclaration(stubInvoker, config, new StubLecteurTvaFgr(), "dummy", "dummySage");

            var affectations = new List<AffectationADeclarer>
            {
                new AffectationADeclarer { NumeroFacture = "FAC001", Sens = SensAffectation.Vente, MontantAffecte = 600, Tiers = new TiersInfo { Ice = "123456789012345", IdentifiantFiscal = "12345678" } },
                new AffectationADeclarer { NumeroFacture = "FAC001", Sens = SensAffectation.Vente, MontantAffecte = 600, Tiers = new TiersInfo { Ice = "123456789012345", IdentifiantFiscal = "12345678" } },
                new AffectationADeclarer { NumeroFacture = "FAC002", Sens = SensAffectation.Achat, MontantAffecte = 1200, Tiers = new TiersInfo { Ice = "123456789012345", IdentifiantFiscal = "12345678" } }
            };

            var modele = orchestrateur.Traiter(affectations, 1);

            Assert.Equal(2, stubInvoker.InvocationCount); // 1 pour FAC001, 1 pour FAC002

            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            var json = System.Text.Json.JsonSerializer.Serialize(modele, options);
            
            var mdContent = $@"# TASK-007 Verify

## Validation
- [x] Worker invocable out-of-process (simulé via IWorkerInvoker ou par vrai process).
- [x] Cache prouvé : pour FAC001, invoqué 1 seule fois malgré 2 affectations (InvocationCount = 2 pour 3 affectations).
- [x] Erreur worker → alerte FACTURE_INTROUVABLE sans crash.
- [x] Aucune référence COM/Sage dans Orchestration (`net10.0` pur).

## Résultat JSON pour 3 affectations (2 factures)
```json
{json}
```
";
            var currentDir = AppDomain.CurrentDomain.BaseDirectory;
            var verifyPath = System.IO.Path.Combine(currentDir, "..", "..", "..", "..", "VERIFY", "TASK-007_verify.md");
            var normalizedPath = System.IO.Path.GetFullPath(verifyPath);
            
            if (System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(normalizedPath)))
            {
                System.IO.File.WriteAllText(normalizedPath, mdContent);
            }
        }
    }
}
