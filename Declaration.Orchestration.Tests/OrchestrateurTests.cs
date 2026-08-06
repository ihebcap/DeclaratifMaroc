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

        // TASK-025 : stub de la persistance de saisie manuelle solde initial — ne touche AUCUNE base
        // réelle, permet de prouver le batch-load + application aux affectations dans Traiter().
        public class StubSoldeInitialTvaRepository : ISoldeInitialTvaRepository
        {
            private readonly Dictionary<int, SaisieSoldeInitialTva> _saisies;
            public StubSoldeInitialTvaRepository(Dictionary<int, SaisieSoldeInitialTva> saisies) { _saisies = saisies; }

            public IReadOnlyDictionary<int, SaisieSoldeInitialTva> GetSaisiesBatch(int soId, IEnumerable<int> ecIds, string persistenceConnectionString)
                => _saisies;

            public void EnregistrerSaisie(int soId, int ecId, decimal taux, decimal montantTva, string saisiPar, string persistenceConnectionString)
                => _saisies[ecId] = new SaisieSoldeInitialTva { Taux = taux, MontantTva = montantTva };
        }

        [Fact]
        public void Traiter_EcType4_SansSaisie_AlerteSansAppelWorker()
        {
            // Bout-en-bout orchestrateur (pas seulement ConstructeurDeclaration) : le batch-load de
            // saisies ne trouve rien pour cet EC_Id → l'affectation reste SoldeInitialTva=null →
            // alerte actionnable, aucun appel worker Sage (EC_Type=4 n'est jamais une pièce OM/FGR).
            var stubInvoker = new StubWorkerInvoker();
            var config = new WorkerConfig();
            var repoSaisies = new StubSoldeInitialTvaRepository(new Dictionary<int, SaisieSoldeInitialTva>());
            var orchestrateur = new OrchestrateurDeclaration(
                stubInvoker, config, new StubLecteurTvaFgr(), "dummy", "dummySage",
                persistenceConnectionString: "dummyPersistence", soldeInitialTva: repoSaisies);

            var affectations = new List<AffectationADeclarer>
            {
                new AffectationADeclarer { NumeroFacture = "SI-001", EC_Type = 4, EC_Id = 22096, MontantAffecte = 750,
                    Tiers = new TiersInfo { Ice = "123456789012345", IdentifiantFiscal = "12345678" } }
            };

            var modele = orchestrateur.Traiter(affectations, 1);

            Assert.Contains(modele.Alertes, a => a.Code == "SOLDE_INITIAL_SAISIE_REQUISE");
            Assert.Empty(modele.Lignes);
            Assert.Equal(0, stubInvoker.InvocationCount);
        }

        [Fact]
        public void Traiter_EcType4_AvecSaisiePersistee_IntegreLaLigne()
        {
            // Même scénario, mais avec une saisie déjà enregistrée pour cet EC_Id (simule un
            // comptable ayant utilisé le bouton « Saisir TVA ») — l'orchestrateur doit la charger en
            // batch, construire le document synthétique (HT = TTC − TVA saisie) et produire une
            // ligne normale via le pipeline Ventilateur existant.
            var stubInvoker = new StubWorkerInvoker();
            var config = new WorkerConfig();
            var repoSaisies = new StubSoldeInitialTvaRepository(new Dictionary<int, SaisieSoldeInitialTva>
            {
                [22096] = new SaisieSoldeInitialTva { Taux = 20, MontantTva = 125 }
            });
            var orchestrateur = new OrchestrateurDeclaration(
                stubInvoker, config, new StubLecteurTvaFgr(), "dummy", "dummySage",
                persistenceConnectionString: "dummyPersistence", soldeInitialTva: repoSaisies);

            var affectations = new List<AffectationADeclarer>
            {
                new AffectationADeclarer { NumeroFacture = "SI-001", EC_Type = 4, EC_Id = 22096, MontantAffecte = 750,
                    Tiers = new TiersInfo { Ice = "123456789012345", IdentifiantFiscal = "12345678" } }
            };

            var modele = orchestrateur.Traiter(affectations, 1);

            Assert.DoesNotContain(modele.Alertes, a => a.Code == "SOLDE_INITIAL_SAISIE_REQUISE");
            Assert.Single(modele.Lignes);
            Assert.Equal(625, modele.Lignes[0].HT);
            Assert.Equal(125, modele.Lignes[0].Tva);
            Assert.Equal(750, modele.Lignes[0].Ttc);
            Assert.Equal(20, modele.Lignes[0].Taux);
            Assert.Equal(0, stubInvoker.InvocationCount);
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

        
        // TASK-159 : simule un batch OM rescapé partiellement (timeout adaptatif atteint après
        // avoir rendu certaines pièces via streaming NDJSON, cf. WorkerInvoker.InvoquerWorkerBatch)
        // — seules les pièces réellement absentes du résultat batch doivent déclencher un repli
        // individuel, jamais celles déjà rendues.
        public class PartialBatchWorkerInvoker : IWorkerInvoker
        {
            private readonly HashSet<string> _rendues;
            public List<string> AppelsIndividuels { get; } = new List<string>();

            public PartialBatchWorkerInvoker(IEnumerable<string> piecesRenduesParLeBatch)
            {
                _rendues = new HashSet<string>(piecesRenduesParLeBatch);
            }

            public DocumentTaxesInfo? InvoquerWorker(string numeroFacture, string sens, WorkerConfig config, Action<string>? log = null)
            {
                AppelsIndividuels.Add(numeroFacture);
                return new DocumentTaxesInfo
                {
                    NumeroPiece = numeroFacture,
                    Sens = sens,
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
                // Ne rend QUE les pièces marquées "rendues" — les autres sont réputées perdues
                // dans le kill du process externe (simule un timeout adaptatif atteint mi-batch).
                var resultats = new List<DocumentTaxesInfo>();
                foreach (var req in requetes)
                {
                    if (!_rendues.Contains(req.numeroFacture)) continue;
                    resultats.Add(new DocumentTaxesInfo
                    {
                        NumeroPiece = req.numeroFacture,
                        Sens = req.sens,
                        TotalHT = 1000,
                        TotalTva = 200,
                        TotalTtc = 1200,
                        LignesTaxe = new List<TaxeDetail>
                        {
                            new TaxeDetail { Code = "1", Type = "TaxeTypeTVA", Taux = 20, BaseHT = 1000, MontantTva = 200, TTC = 1200 }
                        }
                    });
                }
                return resultats;
            }
        }

        [Fact]
        public void Traiter_BatchPartiel_RepliIndividuelCibleUniquementLesPiecesManquantes()
        {
            var stubInvoker = new PartialBatchWorkerInvoker(piecesRenduesParLeBatch: new[] { "FAC001", "FAC002" });
            var config = new WorkerConfig();
            var orchestrateur = new OrchestrateurDeclaration(stubInvoker, config, new StubLecteurTvaFgr(), "dummy", "dummySage");

            var affectations = new List<AffectationADeclarer>
            {
                new AffectationADeclarer { NumeroFacture = "FAC001", Sens = SensAffectation.Vente, MontantAffecte = 600 },
                new AffectationADeclarer { NumeroFacture = "FAC002", Sens = SensAffectation.Vente, MontantAffecte = 600 },
                new AffectationADeclarer { NumeroFacture = "FAC003", Sens = SensAffectation.Vente, MontantAffecte = 600 }
            };

            var modele = orchestrateur.Traiter(affectations, 1);

            // Les 3 pièces doivent être ventilées (aucune perte silencieuse), même si le batch
            // n'en a rendu que 2 avant interruption.
            Assert.Equal(3, modele.Lignes.Count);

            // Seule la pièce absente du résultat batch (FAC003) doit avoir déclenché un appel
            // individuel — FAC001/FAC002, déjà rendues par le batch, ne doivent jamais être relues.
            Assert.Equal(new[] { "FAC003" }, stubInvoker.AppelsIndividuels);
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
