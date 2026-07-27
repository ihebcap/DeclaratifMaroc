using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Declaration.Application.Entities;
using Declaration.Application.Interfaces;
using Declaration.Application.Services;
using Declaration.Selection;
using Declaration.Core.Model;
using Microsoft.Extensions.Configuration;

namespace Declaration.Orchestration.Tests
{
    public class Task100ReglementEctypeInconnuTests
    {
        private static (DeclarationWorkflowService service, FakeDeclarationRepository repo, Guid declarationId) CreerService(
            FakeSelectionExpliqueeService selectionService,
            IEnumerable<string> selection = null)
        {
            var declarationId = Guid.NewGuid();
            var repo = new FakeDeclarationRepository();
            repo.Declarations[declarationId] = new DeclarationEntete
            {
                Id = declarationId,
                Statut = StatutDeclaration.EnCours,
                SocieteId = 1,
                Exercice = 2026,
                Periode = 1,
                Numero = "TVA1-2026-01"
            };
            if (selection != null)
            {
                repo.Selection.AddRange(selection);
            }

            var service = new DeclarationWorkflowService(
                repo,
                selectionService: selectionService,
                connectionFactory: new FakeConnectionFactory(),
                configuration: FakeConfiguration.Vide(),
                logger: NullLogger<DeclarationWorkflowService>.Instance);

            return (service, repo, declarationId);
        }

        private static AffectationCandidate NouveauCandidat(
            string numeroFacture, string numeroRapprochement, MotifRejet motif, int ecType)
        {
            return new AffectationCandidate
            {
                Motif = motif,
                Affectation = new AffectationADeclarer
                {
                    NumeroFacture = numeroFacture,
                    NumeroRapprochement = numeroRapprochement,
                    Sens = SensAffectation.Achat,
                    Source = SourceAffectation.Decaissement,
                    MontantAffecte = 13053.66m,
                    DatePaiement = new DateTime(2026, 1, 15),
                    DateFacture = new DateTime(2026, 1, 5),
                    ModePaiement = "Virement",
                    Tiers = new TiersInfo { Numero = "T1", Nom = "BH CATERING", IdentifiantFiscal = "1", Ice = "1" },
                    EC_Type = ecType,
                    MV_Id = 101,
                    EC_Id = 202
                }
            };
        }

        [Fact]
        public async Task GetCheckupAsync_ReglementImpayeEctype1_LeveAlerteCorrecte()
        {
            var candidat = NouveauCandidat("F1", "RC26040045", MotifRejet.EcTypeHorsPerimetre, ecType: 1);
            var selection = new FakeSelectionExpliqueeService(new[] { candidat });
            var (service, repo, declarationId) = CreerService(selection, new[] { "RC26040045" });

            var model = await service.GetCheckupAsync(declarationId);

            var alerte = model.Alertes.FirstOrDefault(a => a.Code == "REGLEMENT_EXCLU");
            Assert.NotNull(alerte);
            Assert.Equal(NiveauAlerte.Warning, alerte.Niveau);
            Assert.Contains("Règlement impayé — non déclarable (à traiter phase 2) : RC26040045", alerte.Message);
            Assert.Contains("BH CATERING", alerte.Message);
            // TASK-166 : le montant est désormais formaté à 2 décimales (fr-FR, cohérent avec
            // formatMoney() côté front) au lieu de l'échelle brute decimal — comparaison construite
            // avec la MÊME culture plutôt qu'un littéral figé, insensible au séparateur de milliers
            // exact utilisé par l'ICU de la machine (espace normale/insécable/fine selon la version).
            Assert.Contains(13053.66m.ToString("N2", CultureInfo.GetCultureInfo("fr-FR")), alerte.Message);
        }

        [Fact]
        public async Task GetCheckupAsync_ReglementHorsPerimetreEctype99_LeveAlerteCorrecte()
        {
            var candidat = NouveauCandidat("F1", "RC26040045", MotifRejet.EcTypeHorsPerimetre, ecType: 99);
            var selection = new FakeSelectionExpliqueeService(new[] { candidat });
            var (service, repo, declarationId) = CreerService(selection, new[] { "RC26040045" });

            var model = await service.GetCheckupAsync(declarationId);

            var alerte = model.Alertes.FirstOrDefault(a => a.Code == "REGLEMENT_EXCLU");
            Assert.NotNull(alerte);
            Assert.Equal(NiveauAlerte.Warning, alerte.Niveau);
            Assert.Contains("Règlement hors périmètre (Autre (99)) — non déclarable : RC26040045", alerte.Message);
        }

        [Fact]
        public async Task GetCheckupAsync_ReglementImpayeMotifRejet_LeveAlerteCorrecte()
        {
            var candidat = NouveauCandidat("F1", "RC26040045", MotifRejet.Impaye, ecType: 0);
            var selection = new FakeSelectionExpliqueeService(new[] { candidat });
            var (service, repo, declarationId) = CreerService(selection, new[] { "RC26040045" });

            var model = await service.GetCheckupAsync(declarationId);

            var alerte = model.Alertes.FirstOrDefault(a => a.Code == "REGLEMENT_EXCLU");
            Assert.NotNull(alerte);
            Assert.Equal(NiveauAlerte.Warning, alerte.Niveau);
            Assert.Contains("Règlement impayé — non déclarable : RC26040045", alerte.Message);
        }

        [Fact]
        public async Task GetCheckupAsync_CandidatEligibleSelectionne_NeLevePasAlerte()
        {
            var candidat = NouveauCandidat("F1", "RC26040045", MotifRejet.Eligible, ecType: 0);
            var selection = new FakeSelectionExpliqueeService(new[] { candidat });
            var (service, repo, declarationId) = CreerService(selection, new[] { "RC26040045" });

            var model = await service.GetCheckupAsync(declarationId);

            var alerte = model.Alertes.FirstOrDefault(a => a.Code == "REGLEMENT_EXCLU");
            Assert.Null(alerte);
        }

        [Fact]
        public async Task GetCheckupAsync_ReglementNonSelectionne_NeLevePasAlerte()
        {
            var candidat = NouveauCandidat("F1", "RC26040045", MotifRejet.EcTypeHorsPerimetre, ecType: 1);
            var selection = new FakeSelectionExpliqueeService(new[] { candidat });
            var (service, repo, declarationId) = CreerService(selection, Enumerable.Empty<string>());

            var model = await service.GetCheckupAsync(declarationId);

            var alerte = model.Alertes.FirstOrDefault(a => a.Code == "REGLEMENT_EXCLU");
            Assert.Null(alerte);
        }

        private class FakeSelectionExpliqueeService : ISelectionExpliqueeService
        {
            private readonly List<AffectationCandidate> _candidats;
            public FakeSelectionExpliqueeService(IEnumerable<AffectationCandidate>? candidats = null) =>
                _candidats = candidats?.ToList() ?? new List<AffectationCandidate>();

            public Task<IEnumerable<AffectationCandidate>> SelectionnerExpliqueeAsync(
                int soId, DateTime dateDebut, DateTime dateFin, string connectionString, string sageConnectionString,
                Func<string, SensAffectation, Task<(bool Existe, bool TaxeOk)>>? verifierFacture = null) =>
                Task.FromResult<IEnumerable<AffectationCandidate>>(_candidats);

            public Task<IEnumerable<AffectationCandidate>> LireFacturesDepuisPeriodeAsync(
                int soId, DateTime dateDebut, DateTime dateFin, string connectionString, string sageConnectionString) =>
                Task.FromResult<IEnumerable<AffectationCandidate>>(_candidats);
        }

        private class FakeConnectionFactory : IDbConnectionFactory
        {
            public System.Data.IDbConnection CreateGrfConnection() => throw new NotImplementedException();
            public string GetGrfConnectionString() => "fake-grf";
            public System.Data.IDbConnection CreatePersistenceConnection() => throw new NotImplementedException();
            public Task<Declaration.Application.Interfaces.SageConnectionInfo> GetSageConnectionInfoAsync(int soId) =>
                Task.FromResult(new Declaration.Application.Interfaces.SageConnectionInfo { ConnectionString = "" });
        }

        private sealed class FakeConfiguration : IConfiguration, IConfigurationSection
        {
            private readonly Dictionary<string, string?> _values;
            private readonly string _path;

            private FakeConfiguration(Dictionary<string, string?> values, string path)
            {
                _values = values;
                _path = path;
            }

            public static FakeConfiguration Vide() => new(new Dictionary<string, string?>(), "");

            private string Combine(string key) => string.IsNullOrEmpty(_path) ? key : $"{_path}:{key}";

            public string? this[string key]
            {
                get => _values.TryGetValue(Combine(key), out var v) ? v : null;
                set => _values[Combine(key)] = value;
            }

            public string Key => _path.Split(':').LastOrDefault() ?? "";
            public string Path => _path;
            public string? Value { get => this[""]; set => this[""] = value; }

            public IEnumerable<IConfigurationSection> GetChildren() => Enumerable.Empty<IConfigurationSection>();
            public IConfigurationSection GetSection(string key) => new FakeConfiguration(_values, Combine(key));
            public Microsoft.Extensions.Primitives.IChangeToken GetReloadToken() => throw new NotImplementedException();
        }

        private class FakeDeclarationRepository : IDeclarationRepository
        {
            public Dictionary<Guid, DeclarationEntete> Declarations { get; } = new();
            public List<LigneCandidate> Lignes { get; } = new();
            public List<string> Selection { get; } = new();

            public Task<DeclarationEntete?> GetByIdAsync(Guid id) =>
                Task.FromResult(Declarations.TryGetValue(id, out var d) ? d : null);

            public Task<DeclarationEntete?> GetByNumeroAsync(string numero) => throw new NotImplementedException();
            public Task<IEnumerable<DeclarationEntete>> GetAllAsync(int societeId, int? exercice, StatutDeclaration? statut) => throw new NotImplementedException();
            public Task CreateAsync(DeclarationEntete declaration) => throw new NotImplementedException();

            public Task UpdateStatutAsync(Guid id, StatutDeclaration statut) => throw new NotImplementedException();
            public Task<bool> ExistsAsync(int societeId, int exercice, int periode, Declaration.Application.Entities.TypePeriode type) => throw new NotImplementedException();
            public Task SaveLignesCandidatesAsync(IEnumerable<LigneCandidate> lignes) => throw new NotImplementedException();

            public Task<IEnumerable<LigneCandidate>> GetLignesAsync(Guid declarationId, string domaine, int page, int pageSize, string? sort, string? filter) =>
                Task.FromResult<IEnumerable<LigneCandidate>>(Lignes.Where(l => l.DeclarationId == declarationId && l.Domaine == domaine));

            public Task<int> GetLignesCountAsync(Guid declarationId, string domaine, string? filter) =>
                Task.FromResult(Lignes.Count(l => l.DeclarationId == declarationId && l.Domaine == domaine));

            public Task<Dictionary<string, List<string>>> GetLignesDistinctsAsync(Guid declarationId, string domaine) => throw new NotImplementedException();

            public Task UpdateLigneEtatAsync(Guid ligneId, EtatLigne nouvelEtat) => throw new NotImplementedException();
            public Task UpdateLignesEtatBulkAsync(Guid declarationId, string domaine, string? filter, EtatLigne nouvelEtat) => throw new NotImplementedException();
            public Task UpdateLignesEtatBulkByIdsAsync(IEnumerable<Guid> ligneIds, EtatLigne nouvelEtat) => throw new NotImplementedException();

            public Task<IEnumerable<ReglementRapprochementRow>> GetReglementsRapprochementAsync(
                int soId, DateTime dateDebut, DateTime dateFin, RapprochementFilter filter, int page, int size, string? sort) => throw new NotImplementedException();

            public Task<int> GetReglementsRapprochementCountAsync(int soId, DateTime dateDebut, DateTime dateFin, RapprochementFilter filter) => throw new NotImplementedException();

            public Task<ReglementRapprochementDistincts> GetReglementsRapprochementDistinctsAsync(int soId, DateTime dateDebut, DateTime dateFin) => throw new NotImplementedException();

            public Task<IEnumerable<FactureInterrogationRow>> GetFacturesInterrogationAsync(
                int soId, DateTime dateDebut, DateTime dateFin,
                IReadOnlyList<string>? numero, string? fournisseur, IReadOnlyList<string>? reference,
                IReadOnlyList<string>? origines, IReadOnlyList<string>? statuts,
                int page, int size, string? sort) => throw new NotImplementedException();

            public Task<int> GetFacturesInterrogationCountAsync(
                int soId, DateTime dateDebut, DateTime dateFin,
                IReadOnlyList<string>? numero, string? fournisseur, IReadOnlyList<string>? reference,
                IReadOnlyList<string>? origines, IReadOnlyList<string>? statuts) => throw new NotImplementedException();

            public Task<FactureInterrogationDistincts> GetFacturesInterrogationDistinctsAsync(int soId, DateTime dateDebut, DateTime dateFin, string? rechercheNumero = null, string? rechercheReference = null) => throw new NotImplementedException();
            public Task<Dictionary<int, DateTime?>> GetDernieresDatesRapprochementAsync(int soId, IEnumerable<int> ecIds) => throw new NotImplementedException(); // TASK-135 (stub, non exercé par ces tests)

            public Task TamponnerAffectationsAsync(int dtId, IEnumerable<string> numerosRapprochement) => Task.CompletedTask;
            public Task DetamponnerAffectationsAsync(int dtId, IEnumerable<string> numerosRapprochement) => Task.CompletedTask;

            public Task<HashSet<int>> GetEcIdsEnErreurAsync(int soId, IEnumerable<int> ecIds) => Task.FromResult(new HashSet<int>());
            public Task<Dictionary<int, int?>> GetMvPointsActuelsAsync(IEnumerable<int> mvIds) => Task.FromResult(new Dictionary<int, int?>());

            public Task UpdateLigneClesAsync(Guid ligneId, int ecId, int mvId) => Task.CompletedTask;
            public Task ValiderIncoherenceAsync(Guid declarationId, int ecId, string utilisateur) => Task.CompletedTask;
            public Task ReinitialiserValidationIncoherenceAsync(Guid declarationId, int ecId) => Task.CompletedTask;

            public Task DeleteAsync(Guid declarationId) => throw new NotImplementedException();
            public Task<Dictionary<Guid, (int NbLignes, decimal MontantTva)>> GetAgregatsListeAsync(IEnumerable<Guid> declarationIds) => throw new NotImplementedException();

            public Task<int> CountAffectationsTamponneesAsync(IEnumerable<string> numerosRapprochement) => Task.FromResult(0);

            public Task<Dictionary<string, string>> GetConflitsAutreDeclarationAsync(int societeId, Guid declarationIdExclue) =>
                Task.FromResult(new Dictionary<string, string>());

            public Task DeleteLignesAsync(IEnumerable<Guid> ligneIds) => Task.CompletedTask;
            public Task<IEnumerable<DeclarationEntete>> GetToutesDeclarationsAsync() => throw new NotImplementedException();
            public Task<IEnumerable<int>> GetDistinctDtIdsAffectationsAsync() => throw new NotImplementedException();
            public Task SetDtIdDeclarationAsync(Guid declarationId, int? dtId) => throw new NotImplementedException();

            public Task SaveSelectionReglementsAsync(Guid declarationId, IEnumerable<string> selectedNumeroReglements) => Task.CompletedTask;
            public Task<List<string>> GetSelectionReglementsAsync(Guid declarationId) => Task.FromResult(Selection);


            public Task<EcheanceDiagnosticRow?> GetEcheanceDiagnosticAsync(int soId, int ecId)
                => throw new NotImplementedException();

            public Task<IReadOnlyList<EcheanceCollisionRow>> GetEcheancesMemeDoNumeroAsync(int soId, string doNumero)
                => throw new NotImplementedException();

            public Task<string?> GetMotifErreurCacheAsync(int soId, int ecId)
                => throw new NotImplementedException();

            public Task<IReadOnlyDictionary<int, DocumentReglementSageRow>> GetDocumentsReglementSageAsync(
                string sageConnectionString, IEnumerable<int> ecNos)
                => throw new NotImplementedException();

            public Task<CacheLectureRow?> GetDerniereLectureCacheAsync(int soId, int ecId)
                => throw new NotImplementedException();

            public Task<IReadOnlyList<CacheBucketRow>> GetBucketsCacheAsync(int soId, int ecId)
                => throw new NotImplementedException();

            public Task SupprimerLignesParEcIdAsync(Guid declarationId, int ecId)
                => Task.CompletedTask;

            public Task<string?> GetIdentifiantFiscalSocieteAsync(int soId) => Task.FromResult<string?>("12345678");
            public Task<IReadOnlyList<CodeActiviteReferentielRow>> GetReferentielCodesActiviteAsync(string? domaine = null) => Task.FromResult<IReadOnlyList<CodeActiviteReferentielRow>>(new List<CodeActiviteReferentielRow>());
            public Task<int?> GetDomaineCodeActiviteAsync(string codeActivite) => Task.FromResult<int?>(null);
            public Task<string?> GetDomaineLigneAsync(Guid ligneId) => Task.FromResult<string?>(null);
            public Task<IReadOnlyList<string>> GetDomainesDistinctsLignesAsync(Guid declarationId, IEnumerable<Guid> ligneIds) => Task.FromResult<IReadOnlyList<string>>(new List<string>());
            public Task UpdateCodeActiviteBulkByIdsAsync(Guid declarationId, IEnumerable<Guid> ligneIds, string codeActivite, string utilisateur) => Task.CompletedTask;
            public Task UpdateCodeActiviteBulkAsync(Guid declarationId, string domaine, string? filter, string codeActivite, string utilisateur) => Task.CompletedTask;
            public Task UpdateCodeActiviteLigneAsync(Guid ligneId, string codeActivite, string utilisateur) => Task.CompletedTask;
        }
    }
}
