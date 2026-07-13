using Declaration.Application.Entities;
using Xunit;

namespace Declaration.Orchestration.Tests
{
    // ═══════════════════════════════════════════════════════════════════════════════
    // TASK-076 — FactureInterrogationRow.AppliquerCacheB : montants bruts Sage (audit)
    //
    // Une facture exclue (incohérence HT/TVA/TTC, TASK-072) doit exposer les montants Sage
    // BRUTS (tels que lus, AVANT exclusion) pour investigation manuelle côté ERP, SANS jamais
    // réintroduire ces montants dans les colonnes valorisées (Ht/Tva) utilisées par les
    // sous-totaux ③/④ de l'écran Calcul TVA — ni pour une facture exclue, ni pour une saine.
    // ═══════════════════════════════════════════════════════════════════════════════

    public class FactureInterrogationRowTests
    {
        private static FactureInterrogationRow NouvelleLigne(int ecId = 1, int ecType = 0)
            => new() { EcId = ecId, EcType = ecType, DoNumero = "FAC001" };

        [Fact]
        public void FactureExclue_MontantsBrutsExposes_MaisHtTvaValorisesRestentNull()
        {
            var row = NouvelleLigne();

            row.AppliquerCacheB(
                totalHt: null, totalTva: null,
                motifErreurSpecifique: "Incohérence Sage détectée rétroactivement en cache : ...",
                htBrut: 1720251.20m, tvaBrut: 344050.24m, parafiscaleBrut: 0m, ttcBrut: 20700.00m);

            // Montants bruts (audit) : présents, tels que fournis, jamais recalculés.
            Assert.Equal(1720251.20m, row.HtBrut);
            Assert.Equal(344050.24m, row.TvaBrut);
            Assert.Equal(0m, row.ParafiscaleBrut);
            Assert.Equal(20700.00m, row.TtcBrut);

            // Non-régression ABSOLUE (contrainte TASK-076) : les colonnes valorisées/déclarables
            // restent null/exclues — jamais réintroduites via les montants bruts.
            Assert.Null(row.Ht);
            Assert.Null(row.Tva);
            Assert.False(row.ValoriseeB);
            Assert.Equal("Incohérence Sage détectée rétroactivement en cache : ...", row.MotifValorisation);
        }

        [Fact]
        public void FactureExclue_SansMontantsBrutsCaptures_RestentNull_AucuneValeurInventee()
        {
            // Chemin de détection ne portant pas les montants bruts (ex. capture indisponible) :
            // AppliquerCacheB ne doit jamais fabriquer une valeur — tout reste null.
            var row = NouvelleLigne();

            row.AppliquerCacheB(null, null, "Non valorisé : facture absente du cache de ventilation (OM non lue)");

            Assert.Null(row.HtBrut);
            Assert.Null(row.TvaBrut);
            Assert.Null(row.ParafiscaleBrut);
            Assert.Null(row.TtcBrut);
            Assert.Null(row.Ht);
            Assert.Null(row.Tva);
            Assert.False(row.ValoriseeB);
        }

        [Fact]
        public void FactureSaine_Valorisee_AucunMontantBrut_NonRegression()
        {
            // Facture normalement valorisée (cache réel présent) : Ht/Tva renseignés, et les
            // colonnes brutes (audit) restent null — aucune donnée d'audit sur une facture saine.
            var row = NouvelleLigne();

            row.AppliquerCacheB(totalHt: 1000m, totalTva: 200m);

            Assert.Equal(1000m, row.Ht);
            Assert.Equal(200m, row.Tva);
            Assert.True(row.ValoriseeB);
            Assert.Equal("", row.MotifValorisation);

            Assert.Null(row.HtBrut);
            Assert.Null(row.TvaBrut);
            Assert.Null(row.ParafiscaleBrut);
            Assert.Null(row.TtcBrut);
        }
    }
}
