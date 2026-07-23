using System.Collections.Generic;
using Xunit;
using Declaration.Core;

namespace Declaration.Core.Tests
{
    /// <summary>
    /// TASK-161 : les 4 niveaux de la cascade de résolution du code activité, isolés un par un,
    /// puis en combinaison pour vérifier l'ORDRE de priorité strict (surcharge > tiers numéro >
    /// tiers nom > Sage > vide).
    /// </summary>
    public class CodeActiviteResolverTests
    {
        [Fact]
        public void Resoudre_SurchargeManuelle_EstPrioritaireSurTout()
        {
            var parNumero = new Dictionary<string, string> { ["T001"] = "80" };
            var parNom = new Dictionary<string, string> { ["ACME"] = "81" };

            var resultat = CodeActiviteResolver.Resoudre(
                surchargeManuelle: "93",
                tiersNumero: "T001",
                tiersNom: "ACME",
                codeActiviteSage: "82",
                mappingParNumero: parNumero,
                mappingParNom: parNom);

            Assert.Equal("93", resultat);
        }

        [Fact]
        public void Resoudre_SansSurcharge_DefautTiersParNumero_Prioritaire()
        {
            var parNumero = new Dictionary<string, string> { ["T001"] = "80" };
            var parNom = new Dictionary<string, string> { ["ACME"] = "81" };

            var resultat = CodeActiviteResolver.Resoudre(
                surchargeManuelle: null,
                tiersNumero: "T001",
                tiersNom: "ACME",
                codeActiviteSage: "82",
                mappingParNumero: parNumero,
                mappingParNom: parNom);

            Assert.Equal("80", resultat);
        }

        [Fact]
        public void Resoudre_NumeroTiersAbsentDuMapping_ReplieSurLeNomDuTiers()
        {
            var parNumero = new Dictionary<string, string> { ["AUTRE"] = "80" };
            var parNom = new Dictionary<string, string> { ["ACME"] = "81" };

            var resultat = CodeActiviteResolver.Resoudre(
                surchargeManuelle: null,
                tiersNumero: "T001", // absent du mapping par numéro
                tiersNom: "ACME",
                codeActiviteSage: "82",
                mappingParNumero: parNumero,
                mappingParNom: parNom);

            Assert.Equal("81", resultat);
        }

        [Fact]
        public void Resoudre_AucunMappingTiers_ReplieSurColonneSage()
        {
            var resultat = CodeActiviteResolver.Resoudre(
                surchargeManuelle: null,
                tiersNumero: "T999",
                tiersNom: "INCONNU",
                codeActiviteSage: "82",
                mappingParNumero: new Dictionary<string, string>(),
                mappingParNom: new Dictionary<string, string>());

            Assert.Equal("82", resultat);
        }

        [Fact]
        public void Resoudre_RienNeResout_RetourneVideJamaisNull()
        {
            var resultat = CodeActiviteResolver.Resoudre(
                surchargeManuelle: null,
                tiersNumero: null,
                tiersNom: null,
                codeActiviteSage: null,
                mappingParNumero: null,
                mappingParNom: null);

            Assert.Equal("", resultat);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void Resoudre_SurchargeVideOuBlanche_NEstPasConsidereeCommeUneSurcharge(string? surcharge)
        {
            var resultat = CodeActiviteResolver.Resoudre(
                surchargeManuelle: surcharge,
                tiersNumero: null,
                tiersNom: null,
                codeActiviteSage: "82",
                mappingParNumero: null,
                mappingParNom: null);

            Assert.Equal("82", resultat);
        }

        [Fact]
        public void Resoudre_SansMappingFourni_NeLevePasEtReplieSurSage()
        {
            // TASK-161 : les évaluateurs de sélection (SelectionExpliqueeEvaluator,
            // SelectionnerAffectationsService) n'ont pas accès au mapping tiers — mappingParNumero/
            // mappingParNom restent null (valeur par défaut des paramètres optionnels).
            var resultat = CodeActiviteResolver.Resoudre(
                surchargeManuelle: null,
                tiersNumero: "T001",
                tiersNom: "ACME",
                codeActiviteSage: "82");

            Assert.Equal("82", resultat);
        }
    }
}
