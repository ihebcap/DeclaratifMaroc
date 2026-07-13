using Xunit;
using SageTaxReader.Contracts;

namespace Declaration.Core.Tests
{
    // TASK-072 : garde-fou sur l'incohérence Σ(HT+TVA+Parafiscale) vs TTC document.
    // Cas réel reproduit sur GR_EMA_DISTRIBUTION : pièce Sage FC2501717 (EC_Id=21473,
    // GHALI WHEELS LOGITRANS) — HT=1 720 251,20 / TVA=344 050,24 / TTC=20 700,00 (écart ×99,72).
    public class IncoherenceHtTvaTtcTests
    {
        [Fact]
        public void CasReel_FC2501717_EstDetecteIncoherent()
        {
            bool incoherent = IncoherenceHtTvaTtc.EstIncoherent(
                totalHTNet: 1720251.20,
                totalTva: 344050.24,
                totalParafiscale: 0,
                totalTtc: 20700.00,
                ecart: out double ecart);

            Assert.True(incoherent);
            Assert.Equal(-2043601.44, ecart, precision: 2);
        }

        [Fact]
        public void EcartArrondiLegitime_G0110_NEstPasExclu()
        {
            // Cas de fixture existant (VentilateurTests/ConstructeurDeclarationTests) : petit
            // écart d'arrondi/escompte réel, toléré au niveau de la déclaration entière — ne doit
            // jamais déclencher l'exclusion de la pièce.
            bool incoherent = IncoherenceHtTvaTtc.EstIncoherent(
                totalHTNet: 100,
                totalTva: 20,
                totalParafiscale: 0,
                totalTtc: 119.63,
                ecart: out _);

            Assert.False(incoherent);
        }

        [Fact]
        public void EcartNul_NEstPasExclu()
        {
            bool incoherent = IncoherenceHtTvaTtc.EstIncoherent(
                totalHTNet: 1000, totalTva: 200, totalParafiscale: 0, totalTtc: 1200, ecart: out _);

            Assert.False(incoherent);
        }

        [Fact]
        public void EcartRelatifSignificatifSurPetitMontant_EstExclu()
        {
            // Petit montant mais écart proportionnellement énorme (>1% ET >5 MAD) → exclu.
            bool incoherent = IncoherenceHtTvaTtc.EstIncoherent(
                totalHTNet: 100, totalTva: 20, totalParafiscale: 0, totalTtc: 20, ecart: out _);

            Assert.True(incoherent);
        }
    }
}
