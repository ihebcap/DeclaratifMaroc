using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Declaration.Selection;

namespace Declaration.Selection.Tests
{
    public class IntegrationRegressionTests
    {
        private readonly string _connectionString = "Server=.;Database=GR_EMA_DISTRIBUTION;Integrated Security=True;TrustServerCertificate=True";

        [Fact]
        public async Task TestRegression_EligibleEgaleSelectionTask008()
        {
            // Cadrage
            int soId = 1; // Société principale
            DateTime dateDebut = new DateTime(2000, 1, 1);
            DateTime dateFin = new DateTime(2050, 12, 31);

            var oldService = new SelectionnerAffectationsService();
            var newService = new SelectionExpliqueeService();

            // Let it fail if db is not available
            var oldResult = (await oldService.SelectionnerAffectationsAsync(soId, dateDebut, dateFin, _connectionString)).ToList();
            var newResultSurensemble = (await newService.SelectionnerExpliqueeAsync(soId, dateDebut, dateFin, _connectionString, null)).ToList();
            
            var newResultEligibles = newResultSurensemble.Where(x => x.EstEligible).Select(x => x.Affectation).ToList();

            var oldKeys = oldResult.Select(x => $"{x.NumeroFacture}_{x.DatePaiement}_{x.MontantAffecte}_{x.Source}_{x.Sens}").OrderBy(k => k).ToList();
            var newKeys = newResultEligibles.Select(x => $"{x.NumeroFacture}_{x.DatePaiement}_{x.MontantAffecte}_{x.Source}_{x.Sens}").OrderBy(k => k).ToList();

            Assert.NotEmpty(oldKeys);
            Assert.Equal(oldKeys.Count, newKeys.Count);

            for (int i = 0; i < oldKeys.Count; i++)
            {
                Assert.Equal(oldKeys[i], newKeys[i]);
            }
        }
    }
}
