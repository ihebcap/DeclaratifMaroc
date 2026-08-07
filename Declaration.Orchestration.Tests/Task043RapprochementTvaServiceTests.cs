using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Declaration.Application.Entities;
using Declaration.Application.Interfaces;
using Declaration.Application.Services;
using Declaration.Core;
using Declaration.Orchestration;
using SageTaxReader.Contracts;
using Xunit;

namespace Declaration.Orchestration.Tests
{
    public class Task043RapprochementTvaServiceTests
    {
        private class StubRepository : UnusedDeclarationRepository
        {
            public List<AffectationDetailRow> AffectationsToReturn { get; set; } = new();

            public override Task<IEnumerable<AffectationDetailRow>> GetAffectationDetailsRapprochementAsync(int soId, IEnumerable<string> mvNumeros)
            {
                return Task.FromResult<IEnumerable<AffectationDetailRow>>(AffectationsToReturn);
            }
        }

        private class StubLecteurFgr : ILecteurTvaFgr
        {
            public DocumentTaxesInfo? DocumentToReturn { get; set; }

            public DocumentTaxesInfo? LireTvaFgr(int ecId, string numeroFacture, string connectionString, string sageConnectionString)
            {
                return DocumentToReturn;
            }
        }

        private class StubCacheRepository : IVentilationSageCacheRepository
        {
            public Dictionary<int, List<VentilationSageCacheEntry>> Cache { get; set; } = new();

            public IReadOnlyDictionary<int, IReadOnlyList<VentilationSageCacheEntry>> GetEntriesBatch(int soId, IEnumerable<int> ecIds, string persistenceConnectionString)
            {
                var result = new Dictionary<int, IReadOnlyList<VentilationSageCacheEntry>>();
                foreach (var id in ecIds)
                {
                    if (Cache.TryGetValue(id, out var entries))
                        result[id] = entries;
                }
                return result;
            }

            public IReadOnlyList<VentilationSageCacheEntry> GetEntries(int soId, int ecId, string persistenceConnectionString) => Cache.GetValueOrDefault(ecId, new List<VentilationSageCacheEntry>());
            public PaiementToken? GetCurrentPaiementToken(int ecId, string grfConnectionString) => throw new NotImplementedException();
            public void UpsertEntries(IEnumerable<VentilationSageCacheEntry> entries, string persistenceConnectionString) => throw new NotImplementedException();
            public decimal? GetEcheanceMontantDevise(int ecId, string grfConnectionString) => throw new NotImplementedException();
            public void MarquerEnErreur(int soId, int ecId, string motif, string persistenceConnectionString, MontantsBrutsErreur? montantsBruts = null) => throw new NotImplementedException();
            public void SupprimerEntrees(int soId, int ecId, string persistenceConnectionString) => throw new NotImplementedException();
            public IReadOnlyDictionary<int, PaiementToken?> GetCurrentPaiementTokensBatch(IEnumerable<int> ecIds, string grfConnectionString) => throw new NotImplementedException();
            public IReadOnlyDictionary<int, decimal?> GetEcheanceMontantsDeviseBatch(IEnumerable<int> ecIds, string grfConnectionString) => throw new NotImplementedException();
        }

        [Fact]
        public async Task SansAffectation_SetEtatNonApplicable()
        {
            var repo = new StubRepository();
            var service = new RapprochementTvaService(repo, null, null);

            var rows = new List<ReglementRapprochementRow>
            {
                new ReglementRapprochementRow { MvNumero = "REG1", MvMontant = 1000m, NbAffectations = 0 }
            };

            await service.EnrichirTvaAsync(1, rows);

            Assert.Null(rows[0].MontantTva);
            Assert.Equal("NonApplicable", rows[0].EtatValorisation);
        }

        [Fact]
        public async Task FgrMonoFacture_CalculeTvaEtProrata_SetEtatValorisee()
        {
            var repo = new StubRepository
            {
                AffectationsToReturn = new List<AffectationDetailRow>
                {
                    new AffectationDetailRow { MvNumero = "REG2", MvId = 10, AfMontant = 1000m, EcId = 100, EcType = 111, EcMontant = 1000m, EcNumero = "FF260070" }
                }
            };
            var fgr = new StubLecteurFgr
            {
                DocumentToReturn = new DocumentTaxesInfo
                {
                    TotalTva = 200.0,
                    MontantsBrutsDisponibles = true
                }
            };

            var service = new RapprochementTvaService(repo, null, fgr);
            var rows = new List<ReglementRapprochementRow>
            {
                new ReglementRapprochementRow { MvNumero = "REG2", MvMontant = 1000m, MontantAffecte = 1000m, NbAffectations = 1, EcTypeMin = 111, EcTypeMax = 111 }
            };

            await service.EnrichirTvaAsync(1, rows);

            Assert.Equal(200m, rows[0].MontantTva);
            Assert.Equal("Valorisee", rows[0].EtatValorisation);
        }

        [Fact]
        public async Task SageCacheHit_CalculeTva_SetEtatValorisee()
        {
            var repo = new StubRepository
            {
                AffectationsToReturn = new List<AffectationDetailRow>
                {
                    new AffectationDetailRow { MvNumero = "REG3", MvId = 20, AfMontant = 1200m, EcId = 200, EcType = 0, EcMontant = 1200m, EcNumero = "FAC001" }
                }
            };
            var cache = new StubCacheRepository
            {
                Cache = new Dictionary<int, List<VentilationSageCacheEntry>>
                {
                    [200] = new List<VentilationSageCacheEntry>
                    {
                        new VentilationSageCacheEntry { EC_Id = 200, MontantTva = 200.0, Taux = 20 }
                    }
                }
            };

            var service = new RapprochementTvaService(repo, cache, null);
            var rows = new List<ReglementRapprochementRow>
            {
                new ReglementRapprochementRow { MvNumero = "REG3", MvMontant = 1200m, MontantAffecte = 1200m, NbAffectations = 1, EcTypeMin = 0, EcTypeMax = 0 }
            };

            await service.EnrichirTvaAsync(1, rows);

            Assert.Equal(200m, rows[0].MontantTva);
            Assert.Equal("Valorisee", rows[0].EtatValorisation);
        }

        [Fact]
        public async Task SageCacheMiss_SetEtatIndisponible()
        {
            var repo = new StubRepository
            {
                AffectationsToReturn = new List<AffectationDetailRow>
                {
                    new AffectationDetailRow { MvNumero = "REG4", MvId = 30, AfMontant = 1200m, EcId = 300, EcType = 0, EcMontant = 1200m, EcNumero = "FAC002" }
                }
            };
            var cache = new StubCacheRepository();

            var service = new RapprochementTvaService(repo, cache, null);
            var rows = new List<ReglementRapprochementRow>
            {
                new ReglementRapprochementRow { MvNumero = "REG4", MvMontant = 1200m, MontantAffecte = 1200m, NbAffectations = 1, EcTypeMin = 0, EcTypeMax = 0 }
            };

            await service.EnrichirTvaAsync(1, rows);

            Assert.Null(rows[0].MontantTva);
            Assert.Equal("Indisponible", rows[0].EtatValorisation);
        }

        [Fact]
        public async Task PaiementPartiel_SetEtatPartielle()
        {
            var repo = new StubRepository
            {
                AffectationsToReturn = new List<AffectationDetailRow>
                {
                    new AffectationDetailRow { MvNumero = "REG5", MvId = 40, AfMontant = 500m, EcId = 400, EcType = 0, EcMontant = 1000m, EcNumero = "FAC003" }
                }
            };
            var cache = new StubCacheRepository
            {
                Cache = new Dictionary<int, List<VentilationSageCacheEntry>>
                {
                    [400] = new List<VentilationSageCacheEntry>
                    {
                        new VentilationSageCacheEntry { EC_Id = 400, MontantTva = 200.0, Taux = 20 }
                    }
                }
            };

            var service = new RapprochementTvaService(repo, cache, null);
            var rows = new List<ReglementRapprochementRow>
            {
                // Payment = 1000, assigned = 500 => ResteAAffecter = 500 (partial)
                new ReglementRapprochementRow { MvNumero = "REG5", MvMontant = 1000m, MontantAffecte = 500m, NbAffectations = 1, EcTypeMin = 0, EcTypeMax = 0 }
            };

            await service.EnrichirTvaAsync(1, rows);

            // TVA prorata = 200 * (500 / 1000) = 100
            Assert.Equal(100m, rows[0].MontantTva);
            Assert.Equal("Partielle", rows[0].EtatValorisation);
        }
    }
}
