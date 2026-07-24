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
    /// TASK-080 : un règlement/affectation déjà Proposee/Integree dans une autre déclaration de la
    /// même société ne doit pas devenir éligible ailleurs (exclusion à la sélection, motif explicite
    /// DejaEnCoursAilleurs) — et doit redevenir sélectionnable dès que le conflit disparaît (autre
    /// déclaration supprimée ou ligne concurrente changée d'état), sans attendre la clôture.
    /// </summary>
    public class Task080ExclusiviteInterDeclarationTests
    {
        private static DeclarationWorkflowService CreerService(
            FakeDeclarationRepository repo,
            FakeSelectionExpliqueeService? selectionService = null)
        {
            return new DeclarationWorkflowService(
                repo,
                selectionService: selectionService ?? new FakeSelectionExpliqueeService(),
                connectionFactory: new FakeConnectionFactory(),
                configuration: FakeConfiguration.Vide(),
                logger: NullLogger<DeclarationWorkflowService>.Instance);
        }

        private static async Task AppliquerExclusiviteAsync(
            DeclarationWorkflowService service, IEnumerable<AffectationCandidate> candidates, int societeId, Guid declarationId)
        {
            var methode = typeof(DeclarationWorkflowService).GetMethod(
                "AppliquerExclusiviteInterDeclarationAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
            await (Task)methode.Invoke(service, new object[] { candidates, societeId, declarationId })!;
        }

        [Fact]
        public async Task AppliquerExclusiviteInterDeclarationAsync_CandidatEnConflit_DevientDejaEnCoursAilleurs()
        {
            var declarationId = Guid.NewGuid();
            var repo = new FakeDeclarationRepository();
            repo.Conflits["F1|REG-1"] = "TVA2-2026-01";
            var service = CreerService(repo);

            var enConflit = NouveauCandidatEligible("F1", "REG-1");
            var sansConflit = NouveauCandidatEligible("F2", "REG-2");
            var dejaRejete = NouveauCandidatEligible("F3", "REG-3");
            dejaRejete.Motif = MotifRejet.NonRapproche;

            var candidats = new[] { enConflit, sansConflit, dejaRejete };
            await AppliquerExclusiviteAsync(service, candidats, societeId: 1, declarationId);

            Assert.Equal(MotifRejet.DejaEnCoursAilleurs, enConflit.Motif);
            Assert.Equal("TVA2-2026-01", enConflit.ConflitDeclarationNumero);

            // Aucun conflit connu pour F2 : reste Eligible, inchangé.
            Assert.Equal(MotifRejet.Eligible, sansConflit.Motif);

            // Un motif de rejet déjà posé (NonRapproche) n'est jamais écrasé par ce garde-fou additionnel.
            Assert.Equal(MotifRejet.NonRapproche, dejaRejete.Motif);
        }

        [Fact]
        public void MapLignesCandidates_MotifDejaEnCoursAilleurs_NeGeneresAucuneLigne()
        {
            var declarationId = Guid.NewGuid();
            var candidat = NouveauCandidatEligible("F1", "REG-1");
            candidat.Motif = MotifRejet.DejaEnCoursAilleurs;
            candidat.ConflitDeclarationNumero = "TVA2-2026-01";

            var lignes = DeclarationWorkflowService.MapLignesCandidates(
                declarationId, "Decaissement", new[] { candidat }, new DeclarationModele());

            Assert.Empty(lignes);
        }

        [Fact]
        public async Task ChargerCandidatesSiNecessaireAsync_FigeageInitial_ExclutLeReglementDejaEnCoursAilleurs()
        {
            var declarationId = Guid.NewGuid();
            var repo = new FakeDeclarationRepository();
            repo.Declarations[declarationId] = new DeclarationEntete
            {
                Id = declarationId, Statut = StatutDeclaration.EnCours,
                SocieteId = 1, Exercice = 2026, Periode = 1, Numero = "TVA1-2026-01"
            };
            repo.Conflits["F1|REG-1"] = "TVA2-2026-01";
            // TASK-097 : le filtre de sélection s'applique désormais inconditionnellement — la
            // sélection persistée doit donc inclure les deux règlements du scénario pour que ce
            // test continue à isoler UNIQUEMENT le garde-fou d'exclusivité TASK-080.
            repo.Selection = new List<string> { "REG-1", "REG-2" };

            var selection = new FakeSelectionExpliqueeService(new[]
            {
                NouveauCandidatEligible("F1", "REG-1", ecType: 4), // en conflit
                NouveauCandidatEligible("F2", "REG-2", ecType: 4)  // libre
            });

            var service = CreerService(repo, selection);

            await service.ChargerCandidatesSiNecessaireAsync(declarationId, "Decaissement");

            var lignes = repo.Lignes.Where(l => l.DeclarationId == declarationId).ToList();

            // F1 est en conflit et n'est pas éligible : il ne produit aucune ligne du tout
            Assert.DoesNotContain(lignes, l => l.NumeroFacture == "F1");

            // F2 n'est pas en conflit : il passe et produit une ligne
            var ligneF2 = lignes.Single(l => l.NumeroFacture == "F2");
            Assert.DoesNotContain("Déjà pris en compte", ligneF2.MotifRejet ?? "");
        }

        [Fact]
        public async Task ChargerCandidatesSiNecessaireAsync_SelectionJamaisPersistee_NeFigeAucunCandidat()
        {
            // TASK-097 (régression VERIFY) : si /lignes est appelé avant tout POST /selection
            // (aucune ligne DM_SELECTION_REGLEMENT pour cette déclaration — repo.Selection reste
            // au défaut `null`), le figeage doit produire ZÉRO ligne, jamais l'intégralité des
            // candidats éligibles du mois. Avant correctif, `selection.Any()` faux ⇒ filtre
            // ignoré ⇒ tous les candidats étaient figés (bug d'origine que TASK-097 devait corriger).
            var declarationId = Guid.NewGuid();
            var repo = new FakeDeclarationRepository();
            repo.Declarations[declarationId] = new DeclarationEntete
            {
                Id = declarationId, Statut = StatutDeclaration.EnCours,
                SocieteId = 1, Exercice = 2026, Periode = 1, Numero = "TVA1-2026-01"
            };
            Assert.Null(repo.Selection); // sélection jamais persistée

            var selection = new FakeSelectionExpliqueeService(new[]
            {
                NouveauCandidatEligible("F1", "REG-1", ecType: 4),
                NouveauCandidatEligible("F2", "REG-2", ecType: 4)
            });
            var service = CreerService(repo, selection);

            await service.ChargerCandidatesSiNecessaireAsync(declarationId, "Decaissement");

            var lignes = repo.Lignes.Where(l => l.DeclarationId == declarationId).ToList();
            Assert.Empty(lignes);
        }

        [Fact]
        public async Task RevaliderLignesFigeesAsync_ConflitResolu_ReintegreLeReglement()
        {
            var declarationId = Guid.NewGuid();
            var repo = new FakeDeclarationRepository();
            repo.Declarations[declarationId] = new DeclarationEntete
            {
                Id = declarationId, Statut = StatutDeclaration.EnCours,
                SocieteId = 1, Exercice = 2026, Periode = 1, Numero = "TVA1-2026-01"
            };
            // Ligne Exclue posée par un figeage antérieur, motif TASK-080 (conflit désormais résolu :
            // aucune entrée dans repo.Conflits — l'autre déclaration a par ex. été supprimée TASK-079).
            var ligneExclue = new LigneCandidate
            {
                DeclarationId = declarationId, Domaine = "Decaissement", Etat = EtatLigne.Exclue,
                NumeroFacture = "F1", NumeroRapprochement = "REG-1",
                MotifRejet = "Déjà pris en compte dans la déclaration TVA2-2026-01"
            };
            repo.Lignes.Add(ligneExclue);

            var selection = new FakeSelectionExpliqueeService(new[]
            {
                NouveauCandidatEligible("F1", "REG-1", ecType: 4)
            });
            var service = CreerService(repo, selection);

            var alertes = await service.RevaliderLignesFigeesAsync(declarationId, "Decaissement");

            Assert.Contains(alertes, a => a.Code == "REGLEMENT_LIBERE_REINTEGRE");
            // L'ancienne ligne Exclue a été retirée et remplacée par le résultat du pipeline complet
            // (même clé NumeroFacture/NumeroRapprochement, mais Id différent).
            Assert.DoesNotContain(repo.Lignes, l => l.Id == ligneExclue.Id);
            Assert.Contains(repo.Lignes, l => l.NumeroFacture == "F1" && l.NumeroRapprochement == "REG-1");
        }

        private static AffectationCandidate NouveauCandidatEligible(string numeroFacture, string numeroRapprochement, int ecType = 0)
        {
            return new AffectationCandidate
            {
                Motif = MotifRejet.Eligible,
                Affectation = new AffectationADeclarer
                {
                    NumeroFacture = numeroFacture,
                    NumeroRapprochement = numeroRapprochement,
                    Sens = SensAffectation.Achat,
                    Source = SourceAffectation.Decaissement,
                    MontantAffecte = 100m,
                    DatePaiement = new DateTime(2026, 1, 10),
                    DateFacture = new DateTime(2026, 1, 5),
                    ModePaiement = "Virement",
                    Tiers = new TiersInfo { Numero = "T1", Nom = "Tiers", IdentifiantFiscal = "1", Ice = "1" },
                    EC_Type = ecType
                }
            };
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
            public Dictionary<Guid, DeclarationEntete> Declarations { get; } = new();
            public List<LigneCandidate> Lignes { get; } = new();

            /// <summary>Clé "NumeroFacture|NumeroRapprochement" -> numéro de la déclaration concurrente.</summary>
            public Dictionary<string, string> Conflits { get; } = new();

            /// <summary>
            /// TASK-097 : sélection de règlements persistée (null par défaut = "jamais sauvegardée",
            /// ce que <see cref="GetSelectionReglementsAsync"/> traduit en liste vide — reproduit
            /// fidèlement l'invariant serveur : sélection absente ⇒ zéro candidat figé, jamais tous).
            /// </summary>
            public List<string>? Selection { get; set; }

            public Task<DeclarationEntete?> GetByIdAsync(Guid id) =>
                Task.FromResult(Declarations.TryGetValue(id, out var d) ? d : null);

            public Task<DeclarationEntete?> GetByNumeroAsync(string numero) => throw new NotImplementedException();
            public Task<IEnumerable<DeclarationEntete>> GetAllAsync(int societeId, int? exercice, StatutDeclaration? statut) => throw new NotImplementedException();
            public Task CreateAsync(DeclarationEntete declaration) => throw new NotImplementedException();

            public Task UpdateStatutAsync(Guid id, StatutDeclaration statut)
            {
                Declarations[id].Statut = statut;
                return Task.CompletedTask;
            }

            public Task<bool> ExistsAsync(int societeId, int exercice, int periode, Declaration.Application.Entities.TypePeriode type) => throw new NotImplementedException();

            public Task SaveLignesCandidatesAsync(IEnumerable<LigneCandidate> lignes)
            {
                Lignes.AddRange(lignes);
                return Task.CompletedTask;
            }

            public Task<IEnumerable<LigneCandidate>> GetLignesAsync(Guid declarationId, string domaine, int page, int pageSize, string? sort, string? filter) =>
                Task.FromResult(Lignes.Where(l => l.DeclarationId == declarationId && l.Domaine == domaine));

            public Task<int> GetLignesCountAsync(Guid declarationId, string domaine, string? filter) =>
                Task.FromResult(Lignes.Count(l => l.DeclarationId == declarationId && l.Domaine == domaine));

            public Task<Dictionary<string, List<string>>> GetLignesDistinctsAsync(Guid declarationId, string domaine) => throw new NotImplementedException();

            public Task UpdateLigneEtatAsync(Guid ligneId, EtatLigne nouvelEtat) => throw new NotImplementedException();
            public Task UpdateLignesEtatBulkAsync(Guid declarationId, string domaine, string? filter, EtatLigne nouvelEtat) => throw new NotImplementedException();

            public Task UpdateLignesEtatBulkByIdsAsync(IEnumerable<Guid> ligneIds, EtatLigne nouvelEtat)
            {
                var ids = new HashSet<Guid>(ligneIds);
                foreach (var l in Lignes.Where(l => ids.Contains(l.Id)))
                    l.Etat = nouvelEtat;
                return Task.CompletedTask;
            }

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
                Task.FromResult(new Dictionary<string, string>(Conflits));

            public Task DeleteLignesAsync(IEnumerable<Guid> ligneIds)
            {
                var ids = new HashSet<Guid>(ligneIds);
                Lignes.RemoveAll(l => ids.Contains(l.Id));
                return Task.CompletedTask;
            }

            public Task<IEnumerable<DeclarationEntete>> GetToutesDeclarationsAsync() => throw new NotImplementedException();
            public Task<IEnumerable<int>> GetDistinctDtIdsAffectationsAsync() => throw new NotImplementedException();
            public Task SetDtIdDeclarationAsync(Guid declarationId, int? dtId) => throw new NotImplementedException();

            public Task SaveSelectionReglementsAsync(Guid declarationId, IEnumerable<string> selectedNumeroReglements) => Task.CompletedTask;
            public Task<List<string>> GetSelectionReglementsAsync(Guid declarationId) => Task.FromResult(Selection ?? new List<string>());


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
            public Task<IReadOnlyList<string>> GetDomainesDistinctsLignesAsync(Guid declarationId, IEnumerable<Guid> ligneIds) => Task.FromResult<IReadOnlyList<string>>(new List<string>());
            public Task UpdateCodeActiviteBulkByIdsAsync(Guid declarationId, IEnumerable<Guid> ligneIds, string codeActivite, string utilisateur) => Task.CompletedTask;
            public Task UpdateCodeActiviteBulkAsync(Guid declarationId, string domaine, string? filter, string codeActivite, string utilisateur) => Task.CompletedTask;
            public Task UpdateCodeActiviteLigneAsync(Guid ligneId, string codeActivite, string utilisateur) => Task.CompletedTask;
        }
    }
}
