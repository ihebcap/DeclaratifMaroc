using System;
using System.Linq;
using Xunit;
using SageTaxReader.Contracts;
using Declaration.Core;
using System.Collections.Generic;

namespace Declaration.Core.Tests
{
    public class VentilateurTests
    {
        private DocumentTaxesInfo CreateFixture25FA01371()
        {
            return new DocumentTaxesInfo
            {
                NumeroPiece = "25FA01371",
                TypeDocument = 6,
                Sens = "Vente",
                TotalHT = 25947.049999999999,
                TotalTva = 5108.0299999999997,
                TotalParafiscale = 1.0900000000000001,
                TotalTtc = 30796.700000000001,
                Escompte = 259.47000000000003,
                Frais = 10,
                Acompte = 0,
                TotalHTNet = 25687.580000000002,
                EcartArrondi = 0,
                LignesTaxe = new List<TaxeDetail>
                {
                    new TaxeDetail { Code = "FODEC", Type = "TaxeTypeTPHT", Taux = 1, BaseHT = 67.00, MontantTva = 0.67, TTC = 67.67 },
                    new TaxeDetail { Code = "3", Type = "TaxeTypeTVAEncaiss", Taux = 14, BaseHT = 460.76, MontantTva = 64.51, TTC = 525.27 },
                    new TaxeDetail { Code = "1", Type = "TaxeTypeTVAEncaiss", Taux = 20, BaseHT = 25217.59, MontantTva = 5043.52, TTC = 30261.11 },
                    new TaxeDetail { Code = "TF", Type = "TaxeTypeTPTTC", Taux = 0.25, BaseHT = 168.05, MontantTva = 0.42, TTC = 168.47 }
                }
            };
        }

        private DocumentTaxesInfo CreateFixtureG0110()
        {
            return new DocumentTaxesInfo
            {
                NumeroPiece = "G0110",
                TypeDocument = 16,
                Sens = "Achat",
                TotalHT = 609.07000000000005,
                TotalTva = 12.390000000000001,
                TotalParafiscale = 1,
                TotalTtc = 616,
                Escompte = 6.0899999999999999,
                Frais = 555,
                Acompte = 0,
                TotalHTNet = 602.98000000000002,
                EcartArrondi = -0.37,
                LignesTaxe = new List<TaxeDetail>
                {
                    new TaxeDetail { Code = "2", Type = "TaxeTypeTVAEncaiss", Taux = 20, BaseHT = 53.53, MontantTva = 10.71, TTC = 64.24 },
                    new TaxeDetail { Code = "02", Type = "TaxeTypeTVAEncaiss", Taux = 10, BaseHT = 12.66, MontantTva = 1.27, TTC = 13.93 },
                    new TaxeDetail { Code = "4", Type = "TaxeTypeTVADebit", Taux = 7, BaseHT = 5.85, MontantTva = 0.41, TTC = 6.26 },
                    new TaxeDetail { Code = "TTN", Type = "TaxeTypeTPTTC", Taux = 0, BaseHT = 0.00, MontantTva = 1.00, TTC = 1.00 }
                }
            };
        }

        [Fact]
        public void PaiementTotal_25FA01371_RedonneValeursInitiales()
        {
            var facture = CreateFixture25FA01371();
            decimal montantAffecte = (decimal)facture.TotalTtc;

            var result = Ventilateur.Ventiler(facture, montantAffecte, 2);

            Assert.Equal(2, result.Lignes.Count); // Seulement les taxes TVA sont conservées
            
            var tva20 = result.Lignes.First(l => l.CodeTaxe == "1");
            Assert.Equal(25217.59m, tva20.Assiette);
            Assert.Equal(5043.52m, tva20.Tva);
            Assert.Equal(100m, tva20.Prorata);

            var tva14 = result.Lignes.First(l => l.CodeTaxe == "3");
            Assert.Equal(460.76m, tva14.Assiette);
            Assert.Equal(64.51m, tva14.Tva);

            Assert.DoesNotContain(result.Lignes, l => l.CodeTaxe == "FODEC");
            Assert.DoesNotContain(result.Lignes, l => l.CodeTaxe == "TF");
        }

        [Fact]
        public void PaiementTotal_G0110_RedonneValeursInitiales()
        {
            var facture = CreateFixtureG0110();
            decimal montantAffecte = (decimal)facture.TotalTtc;

            var result = Ventilateur.Ventiler(facture, montantAffecte, 2);

            Assert.Equal(3, result.Lignes.Count); // TTN est parafiscale

            var tva20 = result.Lignes.First(l => l.CodeTaxe == "2");
            Assert.Equal(53.53m, tva20.Assiette);
            Assert.Equal(10.71m, tva20.Tva);
            Assert.Equal(100m, tva20.Prorata);

            var tva10 = result.Lignes.First(l => l.CodeTaxe == "02");
            Assert.Equal(12.66m, tva10.Assiette);
            Assert.Equal(1.27m, tva10.Tva);

            var tva7 = result.Lignes.First(l => l.CodeTaxe == "4");
            Assert.Equal(5.85m, tva7.Assiette);
            Assert.Equal(0.41m, tva7.Tva);

            Assert.DoesNotContain(result.Lignes, l => l.CodeTaxe == "TTN");
        }

        [Fact]
        public void PaiementPartiel_PasDeDerive()
        {
            var facture = CreateFixtureG0110();
            decimal montantAffecte = 308m; // 50% de 616

            var result = Ventilateur.Ventiler(facture, montantAffecte, 2);

            var tva20 = result.Lignes.First(l => l.CodeTaxe == "2");
            // BaseHT: 53.53 * 0.5 = 26.765 -> 26.77
            Assert.Equal(26.77m, tva20.Assiette);
            // TVA: 10.71 * 0.5 = 5.355 -> 5.36
            Assert.Equal(5.36m, tva20.Tva);
            Assert.Equal(50m, tva20.Prorata);
        }

        [Fact]
        public void PaiementPartiel_ArrondiAwayFromZero()
        {
            var facture = new DocumentTaxesInfo
            {
                NumeroPiece = "TEST_ARRONDI",
                TotalTtc = 200.0,
                LignesTaxe = new List<TaxeDetail>
                {
                    // 20.01 * 0.5 = 10.005
                    new TaxeDetail { Code = "TVA20", Type = "TaxeTypeTVA", Taux = 20, BaseHT = 20.01, MontantTva = 4.01 }
                }
            };

            decimal montantAffecte = 100m; // 0.5 proration
            // BaseHT * 0.5 = 10.005 -> rounded to 2 decimals AwayFromZero = 10.01
            // TVA * 0.5 = 2.005 -> rounded = 2.01

            var result = Ventilateur.Ventiler(facture, montantAffecte, 2);

            Assert.Single(result.Lignes);
            var ligne = result.Lignes.First();
            Assert.Equal(10.01m, ligne.Assiette);
            Assert.Equal(2.01m, ligne.Tva);
            Assert.Equal(12.02m, ligne.Ttc);
        }
        
        [Fact]
        public void PaiementPartiel_Bug1_EcartRatio_Documente()
        {
            // Bug 1 : l'ancienne formule arrondit le ratio à 6 décimales avant de diviser la base par ce ratio.
            var facture = CreateFixture25FA01371();
            
            // Montant partiel quelconque
            decimal montantAffecte = 12345.67m; 
            decimal totalTtc = (decimal)facture.TotalTtc;
            
            // --- Ancienne Formule GRFN ---
            decimal ratioGrfn = Math.Round(totalTtc / montantAffecte, 6, MidpointRounding.AwayFromZero);
            decimal baseHt20Grfn = (decimal)facture.LignesTaxe.First(x => x.Code == "1").BaseHT;
            decimal assietteGrfn = baseHt20Grfn / ratioGrfn;
            // On peut arrondir l'assiette pour voir l'effet
            decimal assietteArrondieGrfn = Math.Round(assietteGrfn, 2, MidpointRounding.AwayFromZero);

            // --- Nouvelle Formule (Ventilateur) ---
            var result = Ventilateur.Ventiler(facture, montantAffecte, 2);
            var tva20 = result.Lignes.First(l => l.CodeTaxe == "1");

            // On vérifie que la nouvelle formule donne un résultat différent (correct)
            Assert.NotEqual(assietteArrondieGrfn, tva20.Assiette);
            
            // Explicitement, on vérifie la valeur mathématique exacte de la nouvelle proration
            decimal prorationExacte = montantAffecte / totalTtc;
            decimal expectedAssiette = Math.Round(baseHt20Grfn * prorationExacte, 2, MidpointRounding.AwayFromZero);
            
            Assert.Equal(expectedAssiette, tva20.Assiette);
        }
    }
}
