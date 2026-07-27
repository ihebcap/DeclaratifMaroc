using System;
using System.Collections.Generic;
using System.Linq;
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
    /// TASK-190 — backfill rétroactif DateFacture/Reference sur les lignes DM_LGTVA déjà figées
    /// des déclarations EnCours (jamais Clôturée/Déposée), depuis les valeurs réelles de
    /// RT_ECHEANCE (base GRF). Couvre les 4 scénarios explicitement demandés par la TASK (ligne
    /// déjà correcte → pas d'UPDATE ; ligne à corriger → UPDATE avec les bonnes valeurs ; EC_Id
    /// introuvable → signalé, aucune exception ; déclaration Clôturée/Déposée → jamais incluse
    /// dans le périmètre scanné), plus l'idempotence (2e passage = 0 ligne) et le batching par
    /// société (TASK-118 : un EC_Id peut collisionner entre deux sociétés distinctes).
    /// </summary>
    public class Task190BackfillDateFactureReferenceTests
    {
        private static DeclarationWorkflowService CreerService(FakeDeclarationRepositoryTask190 repo)
        {
            return new DeclarationWorkflowService(
                repo,
                selectionService: new FakeSelectionExpliqueeService(),
                connectionFactory: new FakeConnectionFactory(),
                configuration: FakeConfiguration.Vide(),
                logger: NullLogger<DeclarationWorkflowService>.Instance);
        }

        private static LigneCandidate CreerLigne(Guid declarationId, string domaine, int ecId, DateTime? dateFacture, string? reference)
        {
            return new LigneCandidate
            {
                Id = Guid.NewGuid(),
                DeclarationId = declarationId,
                Domaine = domaine,
                EC_Id = ecId,
                DateFacture = dateFacture,
                Reference = reference,
                NumeroFacture = $"FCN-{ecId}"
            };
        }

        [Fact]
        public async Task BackfillDateFactureEtReferenceAsync_LigneDejaCorrecte_AucunUpdateEtComptabiliseeDejaCorrecte()
        {
            var declarationId = Guid.NewGuid();
            var dateReelle = new DateTime(2025, 7, 18);
            var repo = new FakeDeclarationRepositoryTask190
            {
                Declarations = { new DeclarationEntete { Id = declarationId, SocieteId = 1, Statut = StatutDeclaration.EnCours } },
                Lignes = { CreerLigne(declarationId, "Decaissement", 100, dateReelle, "REF-100") },
                ValeursReellesParSociete = { [1] = new() { [100] = (dateReelle, "REF-100") } }
            };
            var service = CreerService(repo);

            var rapport = await service.BackfillDateFactureEtReferenceAsync();

            Assert.Equal(1, rapport.LignesScannees);
            Assert.Equal(0, rapport.LignesMisesAJour);
            Assert.Equal(1, rapport.LignesDejaCorrectes);
            Assert.Empty(rapport.EcIdsIntrouvables);
            Assert.Equal(0, repo.NbAppelsMiseAJour);
        }

        [Fact]
        public async Task BackfillDateFactureEtReferenceAsync_LigneDifferente_EstMiseAJourAvecLesVraiesValeurs()
        {
            var declarationId = Guid.NewGuid();
            var ancienneDate = new DateTime(2026, 4, 15, 11, 58, 31);
            var vraieDate = new DateTime(2025, 7, 18);
            var ligne = CreerLigne(declarationId, "Decaissement", 21466, ancienneDate, null);
            var repo = new FakeDeclarationRepositoryTask190
            {
                Declarations = { new DeclarationEntete { Id = declarationId, SocieteId = 1, Statut = StatutDeclaration.EnCours } },
                Lignes = { ligne },
                ValeursReellesParSociete = { [1] = new() { [21466] = (vraieDate, "FCN-REF") } }
            };
            var service = CreerService(repo);

            var rapport = await service.BackfillDateFactureEtReferenceAsync();

            Assert.Equal(1, rapport.LignesMisesAJour);
            Assert.Equal(0, rapport.LignesDejaCorrectes);
            Assert.Equal(1, repo.NbAppelsMiseAJour);
            Assert.Equal(vraieDate, ligne.DateFacture);
            Assert.Equal("FCN-REF", ligne.Reference);
        }

        [Fact]
        public async Task BackfillDateFactureEtReferenceAsync_ReferenceRealDoDateNull_NeReecrasePasParUneValeurInventee()
        {
            // Cas réel FC2501193 (TASK-190) : DO_Reference vide pour cette échéance précise —
            // NULL attendu après backfill, pas un échec.
            var declarationId = Guid.NewGuid();
            var vraieDate = new DateTime(2025, 7, 18);
            var ligne = CreerLigne(declarationId, "Decaissement", 21466, new DateTime(2026, 4, 15), "ANCIENNE-REF-FAUSSE");
            var repo = new FakeDeclarationRepositoryTask190
            {
                Declarations = { new DeclarationEntete { Id = declarationId, SocieteId = 1, Statut = StatutDeclaration.EnCours } },
                Lignes = { ligne },
                ValeursReellesParSociete = { [1] = new() { [21466] = (vraieDate, null) } }
            };
            var service = CreerService(repo);

            var rapport = await service.BackfillDateFactureEtReferenceAsync();

            Assert.Equal(1, rapport.LignesMisesAJour);
            Assert.Equal(vraieDate, ligne.DateFacture);
            Assert.Null(ligne.Reference);
        }

        [Fact]
        public async Task BackfillDateFactureEtReferenceAsync_EcIdIntrouvableDansRtEcheance_SignaleSansEcraserEtSansException()
        {
            var declarationId = Guid.NewGuid();
            var ligne = CreerLigne(declarationId, "Decaissement", 999999, new DateTime(2026, 1, 1), "REF-ANCIENNE");
            var repo = new FakeDeclarationRepositoryTask190
            {
                Declarations = { new DeclarationEntete { Id = declarationId, SocieteId = 1, Statut = StatutDeclaration.EnCours } },
                Lignes = { ligne },
                ValeursReellesParSociete = { [1] = new() } // 999999 absent : facture supprimée côté Sage
            };
            var service = CreerService(repo);

            var rapport = await service.BackfillDateFactureEtReferenceAsync();

            Assert.Equal(1, rapport.LignesScannees);
            Assert.Equal(0, rapport.LignesMisesAJour);
            Assert.Equal(0, rapport.LignesDejaCorrectes);
            Assert.Equal(0, repo.NbAppelsMiseAJour);
            Assert.Contains(999999, rapport.EcIdsIntrouvables);
            // Valeur d'origine strictement inchangée (jamais écrasée par une valeur inventée).
            Assert.Equal(new DateTime(2026, 1, 1), ligne.DateFacture);
            Assert.Equal("REF-ANCIENNE", ligne.Reference);
        }

        [Fact]
        public async Task BackfillDateFactureEtReferenceAsync_EcIdInvalideOuZero_EstExcluEtSignaleSeparementSansException()
        {
            var declarationId = Guid.NewGuid();
            var ligne = CreerLigne(declarationId, "Decaissement", 0, new DateTime(2026, 1, 1), null);
            var repo = new FakeDeclarationRepositoryTask190
            {
                Declarations = { new DeclarationEntete { Id = declarationId, SocieteId = 1, Statut = StatutDeclaration.EnCours } },
                Lignes = { ligne }
            };
            var service = CreerService(repo);

            var rapport = await service.BackfillDateFactureEtReferenceAsync();

            Assert.Equal(1, rapport.LignesScannees);
            Assert.Equal(1, rapport.LignesEcIdInvalide);
            Assert.Equal(0, rapport.LignesMisesAJour);
            Assert.Equal(0, rapport.LignesDejaCorrectes);
            Assert.Empty(rapport.EcIdsIntrouvables);
        }

        [Theory]
        [InlineData(StatutDeclaration.Cloturee)]
        [InlineData(StatutDeclaration.Deposee)]
        public async Task BackfillDateFactureEtReferenceAsync_DeclarationClotureeOuDeposee_JamaisIncluseDansLePerimetre(StatutDeclaration statut)
        {
            var declarationEnCoursId = Guid.NewGuid();
            var declarationFermeeId = Guid.NewGuid();
            var vraieDate = new DateTime(2025, 7, 18);

            var ligneEnCours = CreerLigne(declarationEnCoursId, "Decaissement", 1, new DateTime(2026, 1, 1), null);
            var ligneFermee = CreerLigne(declarationFermeeId, "Decaissement", 2, new DateTime(2026, 1, 1), null);

            var repo = new FakeDeclarationRepositoryTask190
            {
                Declarations =
                {
                    new DeclarationEntete { Id = declarationEnCoursId, SocieteId = 1, Statut = StatutDeclaration.EnCours },
                    new DeclarationEntete { Id = declarationFermeeId, SocieteId = 1, Statut = statut },
                },
                Lignes = { ligneEnCours, ligneFermee },
                ValeursReellesParSociete = { [1] = new() { [1] = (vraieDate, "REF-1"), [2] = (vraieDate, "REF-2") } }
            };
            var service = CreerService(repo);

            var rapport = await service.BackfillDateFactureEtReferenceAsync();

            // Seule la ligne EnCours est scannée/mise à jour — la ligne de la déclaration
            // fermée n'est ni comptée ni écrite (vérifié explicitement, pas juste "résultat cohérent").
            Assert.Equal(1, rapport.LignesScannees);
            Assert.Equal(1, rapport.LignesMisesAJour);
            Assert.Equal(vraieDate, ligneEnCours.DateFacture);
            Assert.NotEqual(vraieDate, ligneFermee.DateFacture);
            Assert.Null(ligneFermee.Reference);
        }

        [Fact]
        public async Task BackfillDateFactureEtReferenceAsync_DeuxSocietesDistinctes_UnSeulAppelGrfParSocieteEtAucuneCollisionEcId()
        {
            // TASK-118 : EC_Id=500 existe dans les DEUX sociétés avec des valeurs réelles
            // DIFFÉRENTES — le batch GRF doit être scopé par soId, jamais un IN global inter-sociétés.
            var declarationSociete1 = Guid.NewGuid();
            var declarationSociete2 = Guid.NewGuid();
            var dateSociete1 = new DateTime(2025, 1, 10);
            var dateSociete2 = new DateTime(2025, 6, 20);

            var ligneSociete1 = CreerLigne(declarationSociete1, "Decaissement", 500, new DateTime(2026, 1, 1), null);
            var ligneSociete2 = CreerLigne(declarationSociete2, "Decaissement", 500, new DateTime(2026, 1, 1), null);

            var repo = new FakeDeclarationRepositoryTask190
            {
                Declarations =
                {
                    new DeclarationEntete { Id = declarationSociete1, SocieteId = 1, Statut = StatutDeclaration.EnCours },
                    new DeclarationEntete { Id = declarationSociete2, SocieteId = 2, Statut = StatutDeclaration.EnCours },
                },
                Lignes = { ligneSociete1, ligneSociete2 },
                ValeursReellesParSociete =
                {
                    [1] = new() { [500] = (dateSociete1, "REF-SO1") },
                    [2] = new() { [500] = (dateSociete2, "REF-SO2") },
                }
            };
            var service = CreerService(repo);

            var rapport = await service.BackfillDateFactureEtReferenceAsync();

            Assert.Equal(2, rapport.LignesMisesAJour);
            Assert.Equal(dateSociete1, ligneSociete1.DateFacture);
            Assert.Equal("REF-SO1", ligneSociete1.Reference);
            Assert.Equal(dateSociete2, ligneSociete2.DateFacture);
            Assert.Equal("REF-SO2", ligneSociete2.Reference);

            // Un seul aller-retour GRF par société (jamais un aller-retour par ligne).
            Assert.Equal(2, repo.AppelsGetDatesFacturesEtReferences.Count);
            Assert.Contains(repo.AppelsGetDatesFacturesEtReferences, a => a.SoId == 1);
            Assert.Contains(repo.AppelsGetDatesFacturesEtReferences, a => a.SoId == 2);
        }

        [Fact]
        public async Task BackfillDateFactureEtReferenceAsync_ReexecutionApresPremierPassageReussi_ZeroLigneMiseAJour()
        {
            var declarationId = Guid.NewGuid();
            var vraieDate = new DateTime(2025, 7, 18);
            var ligne = CreerLigne(declarationId, "Decaissement", 21466, new DateTime(2026, 4, 15), null);
            var repo = new FakeDeclarationRepositoryTask190
            {
                Declarations = { new DeclarationEntete { Id = declarationId, SocieteId = 1, Statut = StatutDeclaration.EnCours } },
                Lignes = { ligne },
                ValeursReellesParSociete = { [1] = new() { [21466] = (vraieDate, "FCN-REF") } }
            };
            var service = CreerService(repo);

            var premierRapport = await service.BackfillDateFactureEtReferenceAsync();
            Assert.Equal(1, premierRapport.LignesMisesAJour);

            var deuxiemeRapport = await service.BackfillDateFactureEtReferenceAsync();

            Assert.Equal(0, deuxiemeRapport.LignesMisesAJour);
            Assert.Equal(1, deuxiemeRapport.LignesDejaCorrectes);
        }

        private class FakeDeclarationRepositoryTask190 : IDeclarationRepository
        {
            public List<DeclarationEntete> Declarations { get; init; } = new();
            public List<LigneCandidate> Lignes { get; init; } = new();
            public Dictionary<int, Dictionary<int, (DateTime? DoDate, string? DoReference)>> ValeursReellesParSociete { get; init; } = new();
            public List<(int SoId, List<int> EcIds)> AppelsGetDatesFacturesEtReferences { get; } = new();
            public int NbAppelsMiseAJour { get; private set; }

            public Task<IEnumerable<DeclarationEntete>> GetToutesDeclarationsAsync() =>
                Task.FromResult<IEnumerable<DeclarationEntete>>(Declarations);

            public Task<IEnumerable<LigneCandidate>> GetLignesAsync(Guid declarationId, string domaine, int page, int pageSize, string? sort, string? filter) =>
                Task.FromResult(Lignes.Where(l => l.DeclarationId == declarationId && l.Domaine == domaine));

            public Task<Dictionary<int, (DateTime? DoDate, string? DoReference)>> GetDatesFacturesEtReferencesAsync(int soId, IEnumerable<int> ecIds)
            {
                var ids = ecIds.Distinct().ToList();
                AppelsGetDatesFacturesEtReferences.Add((soId, ids));

                var result = new Dictionary<int, (DateTime? DoDate, string? DoReference)>();
                if (ValeursReellesParSociete.TryGetValue(soId, out var valeurs))
                {
                    foreach (var id in ids)
                        if (valeurs.TryGetValue(id, out var v)) result[id] = v;
                }
                return Task.FromResult(result);
            }

            public Task MettreAJourDateFactureEtReferenceAsync(IEnumerable<(Guid LigneId, DateTime? DateFacture, string? Reference)> lignesAMettreAJour)
            {
                NbAppelsMiseAJour++;
                foreach (var (ligneId, dateFacture, reference) in lignesAMettreAJour)
                {
                    var ligne = Lignes.First(l => l.Id == ligneId);
                    ligne.DateFacture = dateFacture;
                    ligne.Reference = reference;
                }
                return Task.CompletedTask;
            }

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
            public Task<FactureInterrogationDistincts> GetFacturesInterrogationDistinctsAsync(int soId, DateTime dateDebut, DateTime dateFin, string? rechercheNumero = null, string? rechercheReference = null) => throw new NotImplementedException();
            public Task<Dictionary<int, DateTime?>> GetDernieresDatesRapprochementAsync(int soId, IEnumerable<int> ecIds) => throw new NotImplementedException();

            public Task TamponnerAffectationsAsync(int dtId, IEnumerable<string> numerosRapprochement) => throw new NotImplementedException();
            public Task DetamponnerAffectationsAsync(int dtId, IEnumerable<string> numerosRapprochement) => throw new NotImplementedException();

            public Task<HashSet<int>> GetEcIdsEnErreurAsync(int soId, IEnumerable<int> ecIds) => throw new NotImplementedException();
            public Task<Dictionary<int, int?>> GetMvPointsActuelsAsync(IEnumerable<int> mvIds) => throw new NotImplementedException();
            public Task UpdateLigneClesAsync(Guid ligneId, int ecId, int mvId) => throw new NotImplementedException();

            public Task ValiderIncoherenceAsync(Guid declarationId, int ecId, string utilisateur) => throw new NotImplementedException();
            public Task ReinitialiserValidationIncoherenceAsync(Guid declarationId, int ecId) => throw new NotImplementedException();

            public Task<Dictionary<string, string>> GetConflitsAutreDeclarationAsync(int societeId, Guid declarationIdExclue) => throw new NotImplementedException();
            public Task DeleteLignesAsync(IEnumerable<Guid> ligneIds) => throw new NotImplementedException();
            public Task<IEnumerable<int>> GetDistinctDtIdsAffectationsAsync() => throw new NotImplementedException();
            public Task SetDtIdDeclarationAsync(Guid declarationId, int? dtId) => throw new NotImplementedException();

            public Task SaveSelectionReglementsAsync(Guid declarationId, IEnumerable<string> selectedNumeroReglements) => Task.CompletedTask;
            public Task<List<string>> GetSelectionReglementsAsync(Guid declarationId) => Task.FromResult(new List<string>());

            public Task<EcheanceDiagnosticRow?> GetEcheanceDiagnosticAsync(int soId, int ecId) => throw new NotImplementedException();
            public Task<IReadOnlyList<EcheanceCollisionRow>> GetEcheancesMemeDoNumeroAsync(int soId, string doNumero) => throw new NotImplementedException();
            public Task<string?> GetMotifErreurCacheAsync(int soId, int ecId) => throw new NotImplementedException();
            public Task<IReadOnlyDictionary<int, DocumentReglementSageRow>> GetDocumentsReglementSageAsync(string sageConnectionString, IEnumerable<int> ecNos) => throw new NotImplementedException();

            public Task<CacheLectureRow?> GetDerniereLectureCacheAsync(int soId, int ecId) => throw new NotImplementedException();
            public Task<IReadOnlyList<CacheBucketRow>> GetBucketsCacheAsync(int soId, int ecId) => throw new NotImplementedException();
            public Task SupprimerLignesParEcIdAsync(Guid declarationId, int ecId) => throw new NotImplementedException();

            public Task<string?> GetIdentifiantFiscalSocieteAsync(int soId) => throw new NotImplementedException();

            public Task<IReadOnlyList<CodeActiviteReferentielRow>> GetReferentielCodesActiviteAsync(string? domaine = null) => throw new NotImplementedException();
            public Task<int?> GetDomaineCodeActiviteAsync(string codeActivite) => throw new NotImplementedException();
            public Task<string?> GetDomaineLigneAsync(Guid ligneId) => throw new NotImplementedException();
            public Task UpdateCodeActiviteLigneAsync(Guid ligneId, string codeActivite, string utilisateur) => throw new NotImplementedException();
            public Task<IReadOnlyList<string>> GetDomainesDistinctsLignesAsync(Guid declarationId, IEnumerable<Guid> ligneIds) => throw new NotImplementedException();
            public Task UpdateCodeActiviteBulkByIdsAsync(Guid declarationId, IEnumerable<Guid> ligneIds, string codeActivite, string utilisateur) => throw new NotImplementedException();
            public Task UpdateCodeActiviteBulkAsync(Guid declarationId, string domaine, string? filter, string codeActivite, string utilisateur) => throw new NotImplementedException();
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
    }
}
