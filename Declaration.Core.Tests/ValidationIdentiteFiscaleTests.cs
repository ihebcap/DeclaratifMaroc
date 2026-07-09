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

        [Fact]
        public void ValiderPourExport_IfInvalide_LanceApplicationException()
        {
            var ex = Assert.Throws<ApplicationException>(() => ValidationIdentiteFiscale.ValiderPourExport("123", "123456789012345", 1));
            Assert.Contains("L'identifiant fiscal du tiers est invalide", ex.Message);
        }

        [Fact]
        public void ValiderPourExport_IceInvalide_LanceApplicationException()
        {
            var ex = Assert.Throws<ApplicationException>(() => ValidationIdentiteFiscale.ValiderPourExport("12345678", "123", 2));
            Assert.Contains("L'ICE du tiers est invalide", ex.Message);
        }
    }
}
