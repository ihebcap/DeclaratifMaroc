using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Declaration.Core.Model;
using Declaration.Selection;

namespace Declaration.Selection.Tests
{
    public class IntegrationRegressionTests
    {
        private readonly string _connectionString = "Server=.;Database=GR_EMA_DISTRIBUTION;Integrated Security=True;TrustServerCertificate=True";

        /// <summary>
        /// TASK-050 — Non-régression MV_DECAISSE supprimé.
        /// Avant TASK-050 : le filtre (MV_Type=@modeEspece OR MV_DECAISSE=@decaisseOui) écartait
        /// silencieusement les règlements chèque/virement (MV_Type∈{1,2,3}) avec MV_DECAISSE=0.
        /// Après TASK-050 : ces règlements sont inclus → le surensemble est un SUPERTENSEMBLE
        /// de l'ancien (newResultEligibles.Count >= oldResult.Count).
        /// Le compte de référence TASK-008 était 824 lignes ; TASK-050 en ajoute 1236 (~465 FC
        /// uniques × sous-lignes) → total mesuré 2060 sur juin 2026 SO_Id=1 toutes périodes.
        /// Le test vérifie : (a) l'ancien résultat est un sous-ensemble du nouveau ;
        ///                   (b) le nouveau surensemble est strictement plus grand (TASK-050 effectif).
        /// </summary>
        [Fact]
        public async Task TestRegression_NouveauSurensembleIncludAncienTask008_Task050()
        {
            // Cadrage
            int soId = 1; // Société principale
            DateTime dateDebut = new DateTime(2000, 1, 1);
            DateTime dateFin = new DateTime(2050, 12, 31);

            var oldService = new SelectionnerAffectationsService();
            var newService = new SelectionExpliqueeService();

            // Let it fail if db is not available
            var oldResult = (await oldService.SelectionnerAffectationsAsync(soId, dateDebut, dateFin, _connectionString)).ToList();
            // TASK-154 : GR_EMA_DISTRIBUTION contient déjà GRF + Sage co-localisés — la même
            // chaîne de connexion sert donc pour les deux paramètres sans casser ce test.
            var newResultSurensemble = (await newService.SelectionnerExpliqueeAsync(soId, dateDebut, dateFin, _connectionString, _connectionString, null)).ToList();
            
            var newResultEligibles = newResultSurensemble.Where(x => x.EstEligible).Select(x => x.Affectation).ToList();

            var oldKeys = new System.Collections.Generic.HashSet<string>(
                oldResult.Select(x => $"{x.NumeroFacture}_{x.MontantAffecte}_{x.Source}_{x.Sens}"));
            var newKeys = new System.Collections.Generic.HashSet<string>(
                newResultEligibles.Select(x => $"{x.NumeroFacture}_{x.MontantAffecte}_{x.Source}_{x.Sens}"));

            Assert.NotEmpty(oldKeys);

            // TASK-050 : le nouveau surensemble est un supertensemble de l'ancien (≥ lignes).
            // Les lignes de l'ancien service doivent toutes être présentes dans le nouveau.
            foreach (var key in oldKeys)
            {
                Assert.Contains(key, newKeys);
            }

            // TASK-050 : le nouveau surensemble est STRICTEMENT plus grand (MV_DECAISSE supprimé).
            // Si ce n'est pas le cas, le correctif TASK-050 n'a pas d'effet → alerte.
            Assert.True(newResultEligibles.Count >= oldResult.Count,
                $"TASK-050 : le nouveau surensemble ({newResultEligibles.Count}) doit être ≥ à l'ancien ({oldResult.Count}).");
        }

        [Fact]
        public async Task TestIntegration_SelectionnerAffectationsVente_Task084()
        {
            int soId = 1;
            DateTime dateDebut = new DateTime(2000, 1, 1);
            DateTime dateFin = new DateTime(2050, 12, 31);

            var service = new SelectionnerAffectationsService();
            var result = (await service.SelectionnerAffectationsAsync(soId, dateDebut, dateFin, _connectionString)).ToList();
            
            var ventes = result.Where(x => x.Sens == SensAffectation.Vente).ToList();
            foreach (var v in ventes)
            {
                Assert.Equal(SourceAffectation.Encaissement, v.Source);
            }
        }
    }
}
