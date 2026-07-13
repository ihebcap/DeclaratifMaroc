using Xunit;
using Declaration.Selection;

namespace Declaration.Selection.Tests
{
    public class IdentiteFiscaleFournisseurConfigTests
    {
        [Fact]
        public void Creer_ColonnesValides_ProduitExpressionsQuotees()
        {
            var config = IdentiteFiscaleFournisseurConfig.Creer("ICE", "IF", soId: 1);

            Assert.Equal("T.[ICE]", config.SelectIceExpression("T"));
            Assert.Equal("T.[IF]", config.SelectIdentifiantExpression("T"));
        }

        [Fact]
        public void Creer_ColonnesDejaQuotees_SontTolereesEtReQuotees()
        {
            var config = IdentiteFiscaleFournisseurConfig.Creer("[ICE]", "[IF]", soId: 1);

            Assert.Equal("T.[ICE]", config.SelectIceExpression("T"));
            Assert.Equal("T.[IF]", config.SelectIdentifiantExpression("T"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Creer_ColonneIceAbsente_Bloque(string colIce)
        {
            var ex = Assert.Throws<ConfigurationIdentiteFiscaleException>(
                () => IdentiteFiscaleFournisseurConfig.Creer(colIce, "IF", soId: 7));

            Assert.Contains("ICE", ex.Message);
            Assert.Contains("7", ex.Message);
        }

        [Fact]
        public void Creer_ColonneIfAbsente_Bloque()
        {
            var ex = Assert.Throws<ConfigurationIdentiteFiscaleException>(
                () => IdentiteFiscaleFournisseurConfig.Creer("ICE", "", soId: 7));

            Assert.Contains("identifiant fiscal", ex.Message);
        }

        [Theory]
        [InlineData("ICE; DROP TABLE F_COMPTET--")]
        [InlineData("ICE] ; SELECT 1 --")]
        [InlineData("ICE OR 1=1")]
        [InlineData("IF WHERE")]
        [InlineData("col-name")]
        [InlineData("col name")]
        public void Creer_NomColonneAvecInjection_EstRejete(string colInjectee)
        {
            Assert.Throws<ConfigurationIdentiteFiscaleException>(
                () => IdentiteFiscaleFournisseurConfig.Creer(colInjectee, "IF", soId: 1));
        }

        [Fact]
        public void Creer_NomColonneAlphaNumUnderscore_EstAccepte()
        {
            var config = IdentiteFiscaleFournisseurConfig.Creer("CT_Info_Libre_1", "IF_Fournisseur", soId: 1);

            Assert.Equal("M.[CT_Info_Libre_1]", config.SelectIceExpression("M"));
            Assert.Equal("M.[IF_Fournisseur]", config.SelectIdentifiantExpression("M"));
        }
    }
}
