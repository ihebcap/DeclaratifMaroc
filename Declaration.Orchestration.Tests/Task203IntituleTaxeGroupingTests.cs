using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Declaration.Core;
using Declaration.Core.Model;

namespace Declaration.Orchestration.Tests
{
    /// <summary>
    /// TASK-203 : Ajouter l'intitulé de taxe (F_TAXE.TA_Intitule) au récap et à l'export de contrôle.
    /// Vérifie que l'intitulé est bien propagé dans LigneDeclarationEnrichie et RecapParTaux.
    /// </summary>
    public class Task203IntituleTaxeGroupingTests
    {
        [Fact]
        public void MemeTaux_CodesTaxeEtIntitulesDifferents_PropagementIntituleCorrect()
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
                ValorisationDirecteCodeTaxe = "TA_20_ACHAT",
                ValorisationDirecteIntituleTaxe = "TVA Achats 20%"
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
                ValorisationDirecteCodeTaxe = "TA_20_IMMO",
                ValorisationDirecteIntituleTaxe = "TVA Immo 20%"
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
                            Intitule = aff.ValorisationDirecteIntituleTaxe ?? "",
                            Type = "TaxeTypeTVA"
                        }
                    }
                },
                2
            );

            Assert.Equal(2, modele.RecapsParTaux.Count);
            
            var bucketAchat = modele.RecapsParTaux.FirstOrDefault(r => r.CodeTaxe == "TA_20_ACHAT");
            Assert.NotNull(bucketAchat);
            Assert.Equal("TVA Achats 20%", bucketAchat.IntituleTaxe);
            Assert.Equal(20m, bucketAchat.Taux);

            var bucketImmo = modele.RecapsParTaux.FirstOrDefault(r => r.CodeTaxe == "TA_20_IMMO");
            Assert.NotNull(bucketImmo);
            Assert.Equal("TVA Immo 20%", bucketImmo.IntituleTaxe);
            Assert.Equal(20m, bucketImmo.Taux);
        }
    }
}
