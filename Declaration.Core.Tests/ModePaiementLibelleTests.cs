using Xunit;
using Declaration.Core;

namespace Declaration.Core.Tests
{
    /// <summary>
    /// TASK-163 : libellé métier du code Simpl-TVA de mode de paiement (affichage Excel), les 6
    /// codes connus (1 à 6) puis le fallback code brut pour toute valeur inconnue/vide/nulle.
    /// </summary>
    public class ModePaiementLibelleTests
    {
        [Theory]
        [InlineData("1", "Espèce")]
        [InlineData("2", "Chèque")]
        [InlineData("3", "Virement")]
        [InlineData("4", "Effet")]
        [InlineData("5", "Compensation")]
        [InlineData("6", "Autres")]
        public void LibelleModePaiementSimplTVA_CodeConnu_RetourneLeLibelle(string code, string libelleAttendu)
        {
            Assert.Equal(libelleAttendu, ModePaiementLibelle.LibelleModePaiementSimplTVA(code));
        }

        [Theory]
        [InlineData("7")]
        [InlineData("0")]
        [InlineData("abc")]
        [InlineData("")]
        public void LibelleModePaiementSimplTVA_CodeInconnuOuVide_RetourneLeCodeBrut(string code)
        {
            Assert.Equal(code, ModePaiementLibelle.LibelleModePaiementSimplTVA(code));
        }

        [Fact]
        public void LibelleModePaiementSimplTVA_CodeNull_NeLevePasEtRetourneChaineVide()
        {
            Assert.Equal("", ModePaiementLibelle.LibelleModePaiementSimplTVA(null));
        }
    }
}
