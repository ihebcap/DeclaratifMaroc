using Xunit;
using Declaration.Core;

namespace Declaration.Core.Tests
{
    /// <summary>
    /// TASK-179 : la cascade de résolution du code activité, réduite à 3 niveaux (surcharge
    /// manuelle > colonne Sage CT_APE > vide) après retrait du niveau "défaut par tiers".
    /// </summary>
    public class CodeActiviteResolverTests
    {
        [Fact]
        public void Resoudre_SurchargeManuelle_EstPrioritaireSurSage()
        {
            var resultat = CodeActiviteResolver.Resoudre(
                surchargeManuelle: "93",
                codeActiviteSage: "82");

            Assert.Equal("93", resultat);
        }

        [Fact]
        public void Resoudre_SansSurcharge_ReplieSurColonneSage()
        {
            var resultat = CodeActiviteResolver.Resoudre(
                surchargeManuelle: null,
                codeActiviteSage: "82");

            Assert.Equal("82", resultat);
        }

        [Fact]
        public void Resoudre_RienNeResout_RetourneVideJamaisNull()
        {
            var resultat = CodeActiviteResolver.Resoudre(
                surchargeManuelle: null,
                codeActiviteSage: null);

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
                codeActiviteSage: "82");

            Assert.Equal("82", resultat);
        }
    }
}
