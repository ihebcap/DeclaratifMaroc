using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Declaration.Core;
using Declaration.Core.Model;

namespace Declaration.Orchestration.Tests
{
    /// <summary>
    /// TASK-198 : Distinguer les taux de TVA par code taxe (TA_Code), pas seulement par pourcentage.
    /// Deux lignes au même taux (ex: 20%) mais avec des codes taxe différents doivent générer deux buckets distincts dans RecapsParTaux.
    /// </summary>
    public class Task198CodeTaxeGroupingTests
    {
        [Fact]
        public void MemeTaux_CodesTaxeDifferents_GenereDeuxBucketsDistinctsDansRecapsParTaux()
        {
            var affectation1 = new AffectationADeclarer
            {
                NumeroFacture = "F-ACHAT-001",
                NumeroRapprochement = "REG-001",
                Sens = SensAffectation.Achat,
                Source = SourceAffectation.Decaissement,
                MontantAffecte = 1200m,
                ValorisationDirecteTaux = 20m,
                ValorisationDirecteTva = 200m,
                ValorisationDirecteCodeTaxe = "TA_20_ACHAT"
            };

            var affectation2 = new AffectationADeclarer
            {
                NumeroFacture = "F-IMMO-002",
                NumeroRapprochement = "REG-002",
                Sens = SensAffectation.Achat,
                Source = SourceAffectation.Decaissement,
                MontantAffecte = 2400m,
                ValorisationDirecteTaux = 20m,
                ValorisationDirecteTva = 400m,
                ValorisationDirecteCodeTaxe = "TA_20_IMMO"
            };

            var affectations = new List<AffectationADeclarer> { affectation1, affectation2 };

            var modele = ConstructeurDeclaration.ConstruireDeclaration(
                affectations,
                aff => new SageTaxReader.Contracts.DocumentTaxesInfo
                {
                    NumeroPiece = aff.NumeroFacture,
                    Sens = "Achat",
                    TotalHT = (double)(aff.MontantAffecte - (aff.ValorisationDirecteTva ?? 0m)),
                    TotalTva = (double)(aff.ValorisationDirecteTva ?? 0m),
                    TotalTtc = (double)aff.MontantAffecte,
                    LignesTaxe = new List<SageTaxReader.Contracts.TaxeDetail>
                    {
                        new SageTaxReader.Contracts.TaxeDetail
                        {
                            BaseHT = (double)(aff.MontantAffecte - (aff.ValorisationDirecteTva ?? 0m)),
                            Taux = (double)(aff.ValorisationDirecteTaux ?? 20m),
                            MontantTva = (double)(aff.ValorisationDirecteTva ?? 0m),
                            TTC = (double)aff.MontantAffecte,
                            Code = aff.ValorisationDirecteCodeTaxe ?? "",
                            Type = "TaxeTypeTVA"
                        }
                    }
                },
                2
            );

            // Doit contenir 2 buckets séparés dans RecapsParTaux pour le taux 20%, un pour TA_20_ACHAT et un pour TA_20_IMMO
            Assert.Equal(2, modele.RecapsParTaux.Count);
            
            var bucketAchat = modele.RecapsParTaux.FirstOrDefault(r => r.CodeTaxe == "TA_20_ACHAT");
            Assert.NotNull(bucketAchat);
            Assert.Equal(20m, bucketAchat.Taux);
            Assert.Equal(1000m, bucketAchat.TotalHT);
            Assert.Equal(200m, bucketAchat.TotalTva);

            var bucketImmo = modele.RecapsParTaux.FirstOrDefault(r => r.CodeTaxe == "TA_20_IMMO");
            Assert.NotNull(bucketImmo);
            Assert.Equal(20m, bucketImmo.Taux);
            Assert.Equal(2000m, bucketImmo.TotalHT);
            Assert.Equal(400m, bucketImmo.TotalTva);
        }
    }
}
