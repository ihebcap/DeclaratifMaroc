using System;
using Xunit;
using Declaration.Core;
using Declaration.Core.Model;

namespace Declaration.Core.Tests
{
    public class ValidationIdentiteFiscaleTests
    {
        [Theory]
        [InlineData("12345678", "123456789012345", EtatConformite.Conforme)]
        [InlineData(null, "123456789012345", EtatConformite.IfManquant)]
        [InlineData("", "123456789012345", EtatConformite.IfManquant)]
        [InlineData("  ", "123456789012345", EtatConformite.IfManquant)]
        [InlineData("1234567", "123456789012345", EtatConformite.FormatInvalide)] // 7 chars
        [InlineData("123456789", "123456789012345", EtatConformite.FormatInvalide)] // 9 chars
        [InlineData("1234 678", "123456789012345", EtatConformite.FormatInvalide)] // space
        [InlineData("12345678", null, EtatConformite.IceManquant)]
        [InlineData("12345678", "", EtatConformite.IceManquant)]
        [InlineData("12345678", "  ", EtatConformite.IceManquant)]
        [InlineData("12345678", "12345678901234", EtatConformite.FormatInvalide)] // 14 chars
        [InlineData("12345678", "1234567890123456", EtatConformite.FormatInvalide)] // 16 chars
        [InlineData("12345678", "12345678 012345", EtatConformite.FormatInvalide)] // space
        public void Evaluer_DonneEtatConformiteCorrect(string ifiscal, string ice, EtatConformite expected)
        {
            var result = ValidationIdentiteFiscale.Evaluer(ifiscal, ice);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ValiderPourExport_Conforme_NeLanceRien()
        {
            ValidationIdentiteFiscale.ValiderPourExport("12345678", "123456789012345", 1);
        }

        // F4 / CDC §4.8 : ValiderPourExport ne bloque plus sur une longueur ≠ 8/15,
        // uniquement sur vide (après nettoyage) ou espace/tabulation résiduel.
        [Fact]
        public void ValiderPourExport_IfVide_LanceApplicationException()
        {
            var ex = Assert.Throws<ApplicationException>(() => ValidationIdentiteFiscale.ValiderPourExport("", "123456789012345", 1));
            Assert.Contains("L'identifiant fiscal du tiers est invalide", ex.Message);
        }

        [Fact]
        public void ValiderPourExport_IfAvecEspace_LanceApplicationException()
        {
            var ex = Assert.Throws<ApplicationException>(() => ValidationIdentiteFiscale.ValiderPourExport("12 45", "123456789012345", 1));
            Assert.Contains("L'identifiant fiscal du tiers est invalide", ex.Message);
        }

        [Fact]
        public void ValiderPourExport_IceVideOuAvecEspace_LanceApplicationException()
        {
            Assert.Throws<ApplicationException>(() => ValidationIdentiteFiscale.ValiderPourExport("12345678", "", 2));
            Assert.Throws<ApplicationException>(() => ValidationIdentiteFiscale.ValiderPourExport("12345678", "12345 78901234", 2));
        }

        [Fact]
        public void ValiderPourExport_IfSeptChiffresEtIceNonStandard_NeLancePas()
        {
            // Cas réel du fichier accepté : IF à 7 chiffres, longueurs variables (CDC §5.2)
            ValidationIdentiteFiscale.ValiderPourExport("1084334", "001544256000053", 1);
            ValidationIdentiteFiscale.ValiderPourExport("1234567", "12345", 2);
        }
    }
}
