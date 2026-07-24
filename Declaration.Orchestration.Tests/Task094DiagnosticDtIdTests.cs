using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Xunit;
using Declaration.Application.Entities;
using Declaration.Application.Interfaces;
using Declaration.Application.Services;
using Declaration.Core.Model;
using Declaration.Selection;

namespace Declaration.Orchestration.Tests
{
    /// <summary>
    /// TASK-094 — diagnostic lecture seule d'un tampon DT_Id : recalcule DeriveDtId pour toutes
    /// les déclarations existantes et qualifie chaque DT_Id observé de « correspondance trouvée »
    /// ou d'« orphelin » explicite (aucune déclaration existante ne matche son hash). Reproduit,
    /// sous forme de test, l'archéologie manuelle faite lors de l'incident 14/07/2026
    /// (`TVA1-2026-01`, 610 affectations tamponnées par des DT_Id sans lien avec aucune
    /// déclaration existante).
    /// </summary>
    public class Task094DiagnosticDtIdTests
    {
        private static DeclarationWorkflowService CreerService(FakeDeclarationRepository repo)
        {
            return new DeclarationWorkflowService(
                repo,
                selectionService: new FakeSelectionExpliqueeService(),
                connectionFactory: new FakeConnectionFactory(),
                configuration: FakeConfiguration.Vide(),
                logger: NullLogger<DeclarationWorkflowService>.Instance);
        }

        /// <summary>Reflète le calcul privé DeriveDtId (source unique, non dupliqué dans le test).</summary>
        private static int DeriveDtId(Guid id)
        {
            var methode = typeof(DeclarationWorkflowService).GetMethod(
                "DeriveDtId", BindingFlags.NonPublic | BindingFlags.Static)!;
            return (int)methode.Invoke(null, new object[] { id })!;
        }

        [Fact]
        public void DeriveDtId_MemeGuid_ProduitToujoursLeMemeHashPositif()
        {
            var id = Guid.NewGuid();
            var h1 = DeriveDtId(id);
            var h2 = DeriveDtId(id);
            Assert.Equal(h1, h2);
            Assert.InRange(h1, 0, int.MaxValue);
        }

        [Fact]
        public async Task DiagnostiquerDtIdAsync_DtIdCorrespondAUneDeclarationExistante_RestitueLaDeclaration()
        {
            var declaration = new DeclarationEntete
            {
                Id = Guid.NewGuid(), Numero = "TVA1-2026-01", SocieteId = 1,
                Exercice = 2026, Periode = 1, Statut = StatutDeclaration.Cloturee
            };
            var dtId = DeriveDtId(declaration.Id);

            var repo = new FakeDeclarationRepository { Declarations = { declaration } };
            var service = CreerService(repo);

            var resultats = await service.DiagnostiquerDtIdAsync(new[] { dtId });

            var r = Assert.Single(resultats);
            Assert.False(r.Orphelin);
            Assert.Equal(declaration.Id, r.DeclarationId);
            Assert.Equal("TVA1-2026-01", r.DeclarationNumero);
            Assert.Equal(StatutDeclaration.Cloturee, r.DeclarationStatut);
        }

        [Fact]
        public async Task DiagnostiquerDtIdAsync_DtIdSansAucuneDeclarationExistante_EstQualifieOrphelin()
        {
            var declaration = new DeclarationEntete
            {
                Id = Guid.NewGuid(), Numero = "TVA1-2026-02", SocieteId = 1,
                Exercice = 2026, Periode = 2, Statut = StatutDeclaration.EnCours
            };
            var repo = new FakeDeclarationRepository { Declarations = { declaration } };
            var service = CreerService(repo);

            // Valeur reconstituée de l'incident réel : ne correspond à aucune déclaration.
            var dtIdOrphelin = 425466408 + 1;
            while (dtIdOrphelin == DeriveDtId(declaration.Id))
                dtIdOrphelin++;

            var resultats = await service.DiagnostiquerDtIdAsync(new[] { dtIdOrphelin });

            var r = Assert.Single(resultats);
            Assert.True(r.Orphelin);
            Assert.Null(r.DeclarationId);
            Assert.Null(r.DeclarationNumero);
        }

        [Fact]
        public async Task DiagnostiquerDtIdAsync_AucunDtIdFourni_DiagnostiqueToutesLesValeursDistinctesDeRtAffectation()
        {
            var declaration = new DeclarationEntete
            {
                Id = Guid.NewGuid(), Numero = "TVA1-2026-03", SocieteId = 1,
                Exercice = 2026, Periode = 3, Statut = StatutDeclaration.Cloturee
            };
            var dtIdConnu = DeriveDtId(declaration.Id);
            var dtIdOrphelin = dtIdConnu + 1;

            var repo = new FakeDeclarationRepository
            {
                Declarations = { declaration },
                DtIdsPresents = { dtIdConnu, dtIdOrphelin }
            };
            var service = CreerService(repo);

            var resultats = await service.DiagnostiquerDtIdAsync(null);

            Assert.Equal(2, resultats.Count);
            Assert.Contains(resultats, r => r.DtId == dtIdConnu && !r.Orphelin);
            Assert.Contains(resultats, r => r.DtId == dtIdOrphelin && r.Orphelin);
        }

        /// <summary>
        /// TASK-094 (Option B) — la valeur persistée (DeclarationEntete.DT_Id, posée à la clôture)
        /// doit primer sur le recalcul DeriveDtId. Reproduit le scénario de l'incident 14/07/2026 :
        /// un recalcul (ex. runtime .NET différent) donnerait une valeur X ≠ à la valeur réellement
        /// posée en base — sans priorité à la valeur persistée, X serait faussement qualifiée
        /// correspondante et la vraie valeur posée faussement orpheline.
        /// </summary>
        [Fact]
        public async Task DiagnostiquerDtIdAsync_ValeurDtIdPersistee_PrimeSurLeRecalculDeriveDtId()
        {
            var declaration = new DeclarationEntete
            {
                Id = Guid.NewGuid(), Numero = "TVA1-2026-06", SocieteId = 1,
                Exercice = 2026, Periode = 6, Statut = StatutDeclaration.Cloturee,
                DT_Id = 999999 // valeur réellement posée à la clôture, DIFFÉRENTE de DeriveDtId(Id).
            };
            var valeurRecalculeeNaive = DeriveDtId(declaration.Id);
            Assert.NotEqual(999999, valeurRecalculeeNaive); // pré-condition du scénario

            var repo = new FakeDeclarationRepository { Declarations = { declaration } };
            var service = CreerService(repo);

            var resultats = await service.DiagnostiquerDtIdAsync(new[] { 999999, valeurRecalculeeNaive });

            Assert.Contains(resultats, r => r.DtId == 999999 && !r.Orphelin && r.DeclarationNumero == "TVA1-2026-06");
            Assert.Contains(resultats, r => r.DtId == valeurRecalculeeNaive && r.Orphelin);
        }

        private class FakeSelectionExpliqueeService : ISelectionExpliqueeService
        {
            public Task<IEnumerable<AffectationCandidate>> SelectionnerExpliqueeAsync(
                int soId, DateTime dateDebut, DateTime dateFin, string connectionString, string sageConnectionString,
                Func<string, SensAffectation, Task<(bool Existe, bool TaxeOk)>>? verifierFacture = null) =>
                Task.FromResult(Enumerable.Empty<AffectationCandidate>());

            public Task<IEnumerable<AffectationCandidate>> LireFacturesDepuisPeriodeAsync(
                int soId, DateTime dateDebut, DateTime dateFin, string connectionString, string sageConnectionString) =>
                Task.FromResult(Enumerable.Empty<AffectationCandidate>());
        }

        private class FakeConnectionFactory : IDbConnectionFactory
        {
            public System.Data.IDbConnection CreateGrfConnection() => throw new NotImplementedException();
            public string GetGrfConnectionString() => "fake-grf";
            public System.Data.IDbConnection CreatePersistenceConnection() => throw new NotImplementedException();
            public Task<Declaration.Application.Interfaces.SageConnectionInfo> GetSageConnectionInfoAsync(int soId) =>
                Task.FromResult(new Declaration.Application.Interfaces.SageConnectionInfo { ConnectionString = "" });
        }

        /// <summary>Implémentation minimale d'IConfiguration en mémoire (aucun package additionnel requis).</summary>
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

            public IConfigurationSection GetSection(string key) => new FakeConfiguration(_values, Combine(key));
            public IEnumerable<IConfigurationSection> GetChildren() => Array.Empty<IConfigurationSection>();
            public IChangeToken GetReloadToken() => NullChangeToken.Instance;

            public string Key => _path.Contains(':') ? _path[(_path.LastIndexOf(':') + 1)..] : _path;
            public string Path => _path;
            public string? Value
            {
                get => _values.TryGetValue(_path, out var v) ? v : null;
                set => _values[_path] = value;
            }

            private sealed class NullChangeToken : IChangeToken
            {
                public static readonly NullChangeToken Instance = new();
                public bool HasChanged => false;
                public bool ActiveChangeCallbacks => false;
                public IDisposable RegisterChangeCallback(Action<object?> callback, object? state) => NullDisposable.Instance;
            }

            private sealed class NullDisposable : IDisposable
            {
                public static readonly NullDisposable Instance = new();
                public void Dispose() { }
            }
        }

        private class FakeDeclarationRepository : IDeclarationRepository
        {
            public List<DeclarationEntete> Declarations { get; init; } = new();
            public List<int> DtIdsPresents { get; init; } = new();

            public Task<IEnumerable<DeclarationEntete>> GetToutesDeclarationsAsync() =>
                Task.FromResult<IEnumerable<DeclarationEntete>>(Declarations);

            public Task<IEnumerable<int>> GetDistinctDtIdsAffectationsAsync() =>
                Task.FromResult<IEnumerable<int>>(DtIdsPresents);

            public Task<DeclarationEntete?> GetByIdAsync(Guid id) => throw new NotImplementedException();
            public Task<DeclarationEntete?> GetByNumeroAsync(string numero) => throw new NotImplementedException();
            public Task<IEnumerable<DeclarationEntete>> GetAllAsync(int societeId, int? exercice, StatutDeclaration? statut) => throw new NotImplementedException();
            public Task CreateAsync(DeclarationEntete declaration) => throw new NotImplementedException();
            public Task UpdateStatutAsync(Guid id, StatutDeclaration statut) => throw new NotImplementedException();
            public Task<bool> ExistsAsync(int societeId, int exercice, int periode, Declaration.Application.Entities.TypePeriode type) => throw new NotImplementedException();
            public Task DeleteAsync(Guid declarationId) => throw new NotImplementedException();
            public Task<Dictionary<Guid, (int NbLignes, decimal MontantTva)>> GetAgregatsListeAsync(IEnumerable<Guid> declarationIds) => throw new NotImplementedException();
            public Task<int> CountAffectationsTamponneesAsync(IEnumerable<string> numerosRapprochement) => throw new NotImplementedException();
            public Task SaveLignesCandidatesAsync(IEnumerable<LigneCandidate> lignes) => throw new NotImplementedException();
            public Task<IEnumerable<LigneCandidate>> GetLignesAsync(Guid declarationId, string domaine, int page, int pageSize, string? sort, string? filter) => throw new NotImplementedException();
            public Task<int> GetLignesCountAsync(Guid declarationId, string domaine, string? filter) => throw new NotImplementedException();
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
            public Task<FactureInterrogationDistincts> GetFacturesInterrogationDistinctsAsync(int soId, DateTime dateDebut, DateTime dateFin) => throw new NotImplementedException();

            public Task TamponnerAffectationsAsync(int dtId, IEnumerable<string> numerosRapprochement) => throw new NotImplementedException();
            public Task DetamponnerAffectationsAsync(int dtId, IEnumerable<string> numerosRapprochement) => throw new NotImplementedException();

            public Task<HashSet<int>> GetEcIdsEnErreurAsync(int soId, IEnumerable<int> ecIds) => throw new NotImplementedException();
            public Task<Dictionary<int, int?>> GetMvPointsActuelsAsync(IEnumerable<int> mvIds) => throw new NotImplementedException();
            public Task UpdateLigneClesAsync(Guid ligneId, int ecId, int mvId) => throw new NotImplementedException();

            public Task ValiderIncoherenceAsync(Guid declarationId, int ecId, string utilisateur) => throw new NotImplementedException();
            public Task ReinitialiserValidationIncoherenceAsync(Guid declarationId, int ecId) => throw new NotImplementedException();

            public Task<Dictionary<string, string>> GetConflitsAutreDeclarationAsync(int societeId, Guid declarationIdExclue) => throw new NotImplementedException();
            public Task DeleteLignesAsync(IEnumerable<Guid> ligneIds) => throw new NotImplementedException();
            public Task SetDtIdDeclarationAsync(Guid declarationId, int? dtId) => throw new NotImplementedException();

            public Task SaveSelectionReglementsAsync(Guid declarationId, IEnumerable<string> selectedNumeroReglements) => Task.CompletedTask;
            public Task<List<string>> GetSelectionReglementsAsync(Guid declarationId) => Task.FromResult(new List<string>());


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
            public Task<IReadOnlyList<CodeActiviteTiersMappingRow>> GetMappingCodeActiviteTiersAsync(int soId) => Task.FromResult<IReadOnlyList<CodeActiviteTiersMappingRow>>(new List<CodeActiviteTiersMappingRow>());
            public Task<IReadOnlyList<CodeActiviteReferentielRow>> GetReferentielCodesActiviteAsync(string? domaine = null) => Task.FromResult<IReadOnlyList<CodeActiviteReferentielRow>>(new List<CodeActiviteReferentielRow>());
            public Task<int?> GetDomaineCodeActiviteAsync(string codeActivite) => Task.FromResult<int?>(null);
            public Task<string?> GetDomaineLigneAsync(Guid ligneId) => Task.FromResult<string?>(null);
            public Task UpdateCodeActiviteLigneAsync(Guid ligneId, string codeActivite, string utilisateur) => Task.CompletedTask;
        }
    }
}
