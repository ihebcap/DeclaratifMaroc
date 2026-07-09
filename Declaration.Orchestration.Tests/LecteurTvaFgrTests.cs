using System;
using System.Collections.Generic;
using Xunit;
using Declaration.Orchestration;
using Declaration.Core;
using Declaration.Core.Model;

namespace Declaration.Orchestration.Tests
{
    public class LecteurTvaFgrTests
    {
        private class TestableLecteurTvaFgr : LecteurTvaFgr
        {
            private readonly IEnumerable<HistoComptaRow> _lignes;

            public TestableLecteurTvaFgr(IEnumerable<HistoComptaRow> lignes)
            {
                _lignes = lignes;
            }

            protected override IEnumerable<HistoComptaRow> GetLignes(int ecId, string connectionString)
            {
                return _lignes;
            }

            protected override Dictionary<string, double> GetTaxes(string sageConnectionString)
            {
                return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
                {
                    { "D20", 20 },
                    { "D10", 10 },
                    { "C20", 20 }
                };
            }
        }

        [Fact]
        public void LireTvaFgr_MonoTaux_Succes()
        {
            // Mono-taux 20%
            var lignes = new List<LecteurTvaFgr.HistoComptaRow>
            {
                new LecteurTvaFgr.HistoComptaRow { HC_Indice = 1, HC_Montant = 1200m, HC_TaxeCode = "" },
                new LecteurTvaFgr.HistoComptaRow { HC_Indice = 2, HC_Montant = 1000m, HC_TaxeCode = "D20" },
                new LecteurTvaFgr.HistoComptaRow { HC_Indice = 2, HC_Montant = 200m, HC_TaxeCode = "" }
            };

            var lecteur = new TestableLecteurTvaFgr(lignes);
            var doc = lecteur.LireTvaFgr(1, "FF260070", "dummy", "dummySage");

            Assert.NotNull(doc);
            Assert.Equal(1200, doc.TotalTtc);
            Assert.Equal(1000, doc.TotalHT);
            Assert.Equal(200, doc.TotalTva);
            Assert.Single(doc.LignesTaxe);
            Assert.Equal(20, doc.LignesTaxe[0].Taux);
        }

        [Fact]
        public void LireTvaFgr_MultiTaux_Succes()
        {
            // Multi-taux: 20% et 10%
            var lignes = new List<LecteurTvaFgr.HistoComptaRow>
            {
                new LecteurTvaFgr.HistoComptaRow { HC_Indice = 1, HC_Montant = 2300m, HC_TaxeCode = "" },
                
                new LecteurTvaFgr.HistoComptaRow { HC_Indice = 2, HC_Montant = 1000m, HC_TaxeCode = "D20" },
                new LecteurTvaFgr.HistoComptaRow { HC_Indice = 2, HC_Montant = 200m, HC_TaxeCode = "" },
                
                new LecteurTvaFgr.HistoComptaRow { HC_Indice = 3, HC_Montant = 1000m, HC_TaxeCode = "D10" },
                new LecteurTvaFgr.HistoComptaRow { HC_Indice = 3, HC_Montant = 100m, HC_TaxeCode = "" }
            };

            var lecteur = new TestableLecteurTvaFgr(lignes);
            var doc = lecteur.LireTvaFgr(1, "FAC-MULTI", "dummy", "dummySage");

            Assert.Equal(2300, doc.TotalTtc);
            Assert.Equal(2000, doc.TotalHT);
            Assert.Equal(300, doc.TotalTva);
            Assert.Equal(2, doc.LignesTaxe.Count);
        }

        [Fact]
        public void LireTvaFgr_Exonere_Succes()
        {
            // Exonéré (1 ligne sans code pour le bucket)
            var lignes = new List<LecteurTvaFgr.HistoComptaRow>
            {
                new LecteurTvaFgr.HistoComptaRow { HC_Indice = 1, HC_Montant = 500m, HC_TaxeCode = "" },
                new LecteurTvaFgr.HistoComptaRow { HC_Indice = 2, HC_Montant = 500m, HC_TaxeCode = "" }
            };

            var lecteur = new TestableLecteurTvaFgr(lignes);
            var doc = lecteur.LireTvaFgr(1, "FAC-EXO", "dummy", "dummySage");

            Assert.Equal(500, doc.TotalHT);
            Assert.Equal(0, doc.TotalTva);
            Assert.Equal(500, doc.TotalTtc);
            Assert.Single(doc.LignesTaxe);
            Assert.Equal(0, doc.LignesTaxe[0].Taux);
            Assert.Equal("EXO", doc.LignesTaxe[0].Code);
        }

        [Fact]
        public void LireTvaFgr_EcartFgr_LancedFgrValidationException()
        {
            // Σ≠TTC
            var lignes = new List<LecteurTvaFgr.HistoComptaRow>
            {
                new LecteurTvaFgr.HistoComptaRow { HC_Indice = 1, HC_Montant = 1500m, HC_TaxeCode = "" }, // 1500 au lieu de 1200
                new LecteurTvaFgr.HistoComptaRow { HC_Indice = 2, HC_Montant = 1000m, HC_TaxeCode = "D20" },
                new LecteurTvaFgr.HistoComptaRow { HC_Indice = 2, HC_Montant = 200m, HC_TaxeCode = "" }
            };

            var lecteur = new TestableLecteurTvaFgr(lignes);
            var ex = Assert.Throws<FgrValidationException>(() => lecteur.LireTvaFgr(1, "FAC-ERR", "dummy", "dummySage"));
            
            Assert.Contains("différent du TTC", ex.Message);
            Assert.NotNull(ex.Document); // On vérifie que le document partiel est bien là
        }

        [Fact]
        public void LireTvaFgr_BucketIllisible_LancedInvalidOperationException()
        {
            // Bucket illisible (3 lignes pour l'indice 2)
            var lignes = new List<LecteurTvaFgr.HistoComptaRow>
            {
                new LecteurTvaFgr.HistoComptaRow { HC_Indice = 1, HC_Montant = 1200m, HC_TaxeCode = "" },
                new LecteurTvaFgr.HistoComptaRow { HC_Indice = 2, HC_Montant = 1000m, HC_TaxeCode = "D20" },
                new LecteurTvaFgr.HistoComptaRow { HC_Indice = 2, HC_Montant = 100m, HC_TaxeCode = "" },
                new LecteurTvaFgr.HistoComptaRow { HC_Indice = 2, HC_Montant = 100m, HC_TaxeCode = "" }
            };

            var lecteur = new TestableLecteurTvaFgr(lignes);
            var ex = Assert.Throws<InvalidOperationException>(() => lecteur.LireTvaFgr(1, "FAC-ERR", "dummy", "dummySage"));
            
            Assert.Contains("Structure de bucket invalide", ex.Message);
        }

        [Fact]
        public void LireTvaFgr_CodeC20_ResoluViaFTaxe()
        {
            // Code C20 : taux résolu via le mapping F_TAXE (TA_Code -> TA_Taux), plus aucun regex/Substring
            var lignes = new List<LecteurTvaFgr.HistoComptaRow>
            {
                new LecteurTvaFgr.HistoComptaRow { HC_Indice = 1, HC_Montant = 120m, HC_TaxeCode = "" },
                new LecteurTvaFgr.HistoComptaRow { HC_Indice = 2, HC_Montant = 100m, HC_TaxeCode = "C20" },
                new LecteurTvaFgr.HistoComptaRow { HC_Indice = 2, HC_Montant = 20m, HC_TaxeCode = "" }
            };

            var lecteur = new TestableLecteurTvaFgr(lignes);
            var doc = lecteur.LireTvaFgr(1, "FAC-C20", "dummy", "dummySage");

            Assert.Single(doc.LignesTaxe);
            Assert.Equal(20, doc.LignesTaxe[0].Taux);
            Assert.Equal("C20", doc.LignesTaxe[0].Code);
        }

        [Fact]
        public void LireTvaFgr_CodeInconnu_LancedInvalidOperationException()
        {
            // Code inconnu sans chiffre (ex: XYZ)
            var lignes = new List<LecteurTvaFgr.HistoComptaRow>
            {
                new LecteurTvaFgr.HistoComptaRow { HC_Indice = 1, HC_Montant = 120m, HC_TaxeCode = "" },
                new LecteurTvaFgr.HistoComptaRow { HC_Indice = 2, HC_Montant = 100m, HC_TaxeCode = "XYZ" },
                new LecteurTvaFgr.HistoComptaRow { HC_Indice = 2, HC_Montant = 20m, HC_TaxeCode = "" }
            };

            var lecteur = new TestableLecteurTvaFgr(lignes);
            var ex = Assert.Throws<InvalidOperationException>(() => lecteur.LireTvaFgr(1, "FAC-ERR", "dummy", "dummySage"));
            
            Assert.Contains("CODE_TAXE_INCONNU", ex.Message);
            Assert.Contains("XYZ", ex.Message); // Le code brut doit être dans l'erreur
        }
    }
}
