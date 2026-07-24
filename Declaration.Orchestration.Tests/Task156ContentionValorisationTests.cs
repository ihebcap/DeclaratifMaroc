using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Declaration.Application.Entities;
using Declaration.Application.Interfaces;
using Declaration.Application.Services;
using Declaration.Core.Model;
using Declaration.Selection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Declaration.Orchestration.Tests
{
    // ═══════════════════════════════════════════════════════════════════════════════
    // TASK-156 — Contention du rafraîchissement de valorisation OM
    //
    // Correctif A : verrou anti-chevauchement par soId sur RafraichirValorisationAsync,
    // décision PO 23/07/2026 = REJET IMMÉDIAT (pas d'attente silencieuse). Deux soId distincts
    // doivent rester totalement indépendants (jamais de blocage croisé multi-société).
    //
    // Ces tests exercent RÉELLEMENT DeclarationWorkflowService.RafraichirValorisationAsync (pas
    // une réimplémentation du verrou) — le reste du pipeline (sélection/orchestrateur Sage) est
    // stubbé pour rester 100% in-process (aucune connexion réseau/Sage réelle), avec un point de
    // blocage contrôlé par le test (TaskCompletionSource) pour simuler un cycle "en cours" de
    // façon déterministe, sans Task.Delay ni polling.
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Stub complet de IDeclarationRepository — RafraichirValorisationAsync ne l'utilise pas du
    /// tout (vérifié en lecture du code) ; chaque membre lève explicitement s'il est appelé par
    /// erreur, pour qu'un usage inattendu échoue bruyamment plutôt que de fausser silencieusement
    /// le test.
    /// </summary>
    internal class UnusedDeclarationRepository : IDeclarationRepository
    {
        private static Exception NotUsed() => new InvalidOperationException(
            "IDeclarationRepository ne doit pas être sollicité par RafraichirValorisationAsync.");

        public Task<DeclarationEntete?> GetByIdAsync(Guid id) => throw NotUsed();
        public Task<DeclarationEntete?> GetByNumeroAsync(string numero) => throw NotUsed();
        public Task<IEnumerable<DeclarationEntete>> GetAllAsync(int societeId, int? exercice, StatutDeclaration? statut) => throw NotUsed();
        public Task CreateAsync(DeclarationEntete declaration) => throw NotUsed();
        public Task UpdateStatutAsync(Guid id, StatutDeclaration statut) => throw NotUsed();
        public Task<bool> ExistsAsync(int societeId, int exercice, int periode, Declaration.Application.Entities.TypePeriode type) => throw NotUsed();
        public Task DeleteAsync(Guid declarationId) => throw NotUsed();
        public Task<Dictionary<Guid, (int NbLignes, decimal MontantTva)>> GetAgregatsListeAsync(IEnumerable<Guid> declarationIds) => throw NotUsed();
        public Task<int> CountAffectationsTamponneesAsync(IEnumerable<string> numerosRapprochement) => throw NotUsed();
        public Task SaveLignesCandidatesAsync(IEnumerable<LigneCandidate> lignes) => throw NotUsed();
        public Task<IEnumerable<LigneCandidate>> GetLignesAsync(Guid declarationId, string domaine, int page, int pageSize, string? sort, string? filter) => throw NotUsed();
        public Task<int> GetLignesCountAsync(Guid declarationId, string domaine, string? filter) => throw NotUsed();
        public Task<Dictionary<string, List<string>>> GetLignesDistinctsAsync(Guid declarationId, string domaine) => throw NotUsed();
        public Task UpdateLigneEtatAsync(Guid ligneId, EtatLigne nouvelEtat) => throw NotUsed();
        public Task UpdateLignesEtatBulkAsync(Guid declarationId, string domaine, string? filter, EtatLigne nouvelEtat) => throw NotUsed();
        public Task UpdateLignesEtatBulkByIdsAsync(IEnumerable<Guid> ligneIds, EtatLigne nouvelEtat) => throw NotUsed();
        public Task<IEnumerable<ReglementRapprochementRow>> GetReglementsRapprochementAsync(int soId, DateTime dateDebut, DateTime dateFin, RapprochementFilter filter, int page, int size, string? sort) => throw NotUsed();
        public Task<int> GetReglementsRapprochementCountAsync(int soId, DateTime dateDebut, DateTime dateFin, RapprochementFilter filter) => throw NotUsed();
        public Task<ReglementRapprochementDistincts> GetReglementsRapprochementDistinctsAsync(int soId, DateTime dateDebut, DateTime dateFin) => throw NotUsed();
        public Task<IEnumerable<FactureInterrogationRow>> GetFacturesInterrogationAsync(int soId, DateTime dateDebut, DateTime dateFin, IReadOnlyList<string>? numero, string? fournisseur, IReadOnlyList<string>? reference, IReadOnlyList<string>? origines, IReadOnlyList<string>? statuts, int page, int size, string? sort) => throw NotUsed();
        public Task<int> GetFacturesInterrogationCountAsync(int soId, DateTime dateDebut, DateTime dateFin, IReadOnlyList<string>? numero, string? fournisseur, IReadOnlyList<string>? reference, IReadOnlyList<string>? origines, IReadOnlyList<string>? statuts) => throw NotUsed();
        public Task<FactureInterrogationDistincts> GetFacturesInterrogationDistinctsAsync(int soId, DateTime dateDebut, DateTime dateFin, string? rechercheNumero = null, string? rechercheReference = null) => throw NotUsed();
        public Task TamponnerAffectationsAsync(int dtId, IEnumerable<string> numerosRapprochement) => throw NotUsed();
        public Task DetamponnerAffectationsAsync(int dtId, IEnumerable<string> numerosRapprochement) => throw NotUsed();
        public Task<HashSet<int>> GetEcIdsEnErreurAsync(int soId, IEnumerable<int> ecIds) => throw NotUsed();
        public Task<Dictionary<int, int?>> GetMvPointsActuelsAsync(IEnumerable<int> mvIds) => throw NotUsed();
        public Task UpdateLigneClesAsync(Guid ligneId, int ecId, int mvId) => throw NotUsed();
        public Task ValiderIncoherenceAsync(Guid declarationId, int ecId, string utilisateur) => throw NotUsed();
        public Task ReinitialiserValidationIncoherenceAsync(Guid declarationId, int ecId) => throw NotUsed();
        public Task<Dictionary<string, string>> GetConflitsAutreDeclarationAsync(int societeId, Guid declarationIdExclue) => throw NotUsed();
        public Task DeleteLignesAsync(IEnumerable<Guid> ligneIds) => throw NotUsed();
        public Task<IEnumerable<DeclarationEntete>> GetToutesDeclarationsAsync() => throw NotUsed();
        public Task<IEnumerable<int>> GetDistinctDtIdsAffectationsAsync() => throw NotUsed();
        public Task SetDtIdDeclarationAsync(Guid declarationId, int? dtId) => throw NotUsed();
        public Task SaveSelectionReglementsAsync(Guid declarationId, IEnumerable<string> selectedNumeroReglements) => throw NotUsed();
        public Task<List<string>> GetSelectionReglementsAsync(Guid declarationId) => throw NotUsed();
        public Task<EcheanceDiagnosticRow?> GetEcheanceDiagnosticAsync(int soId, int ecId) => throw NotUsed();
        public Task<IReadOnlyList<EcheanceCollisionRow>> GetEcheancesMemeDoNumeroAsync(int soId, string doNumero) => throw NotUsed();
        public Task<string?> GetMotifErreurCacheAsync(int soId, int ecId) => throw NotUsed();
        public Task<IReadOnlyDictionary<int, DocumentReglementSageRow>> GetDocumentsReglementSageAsync(string sageConnectionString, IEnumerable<int> ecNos) => throw NotUsed();
        public Task<CacheLectureRow?> GetDerniereLectureCacheAsync(int soId, int ecId) => throw NotUsed();
        public Task<IReadOnlyList<CacheBucketRow>> GetBucketsCacheAsync(int soId, int ecId) => throw NotUsed();
        public Task SupprimerLignesParEcIdAsync(Guid declarationId, int ecId) => throw NotUsed();
        public Task<string?> GetIdentifiantFiscalSocieteAsync(int soId) => throw NotUsed();
        public Task<IReadOnlyList<CodeActiviteReferentielRow>> GetReferentielCodesActiviteAsync(string? domaine = null) => throw NotUsed();
        public Task<int?> GetDomaineCodeActiviteAsync(string codeActivite) => throw NotUsed();
        public Task<string?> GetDomaineLigneAsync(Guid ligneId) => throw NotUsed();
        public Task UpdateCodeActiviteLigneAsync(Guid ligneId, string codeActivite, string utilisateur) => throw NotUsed();
        public Task<IReadOnlyList<string>> GetDomainesDistinctsLignesAsync(Guid declarationId, IEnumerable<Guid> ligneIds) => throw NotUsed();
        public Task UpdateCodeActiviteBulkByIdsAsync(Guid declarationId, IEnumerable<Guid> ligneIds, string codeActivite, string utilisateur) => throw NotUsed();
        public Task UpdateCodeActiviteBulkAsync(Guid declarationId, string domaine, string? filter, string codeActivite, string utilisateur) => throw NotUsed();
    }

    /// <summary>
    /// Stub minimal de IDbConnectionFactory : RafraichirValorisationAsync n'a besoin que de
    /// GetGrfConnectionString et GetSageConnectionInfoAsync (BuildOrchestrateurAsync). Les deux
    /// autres membres ne sont jamais appelés par ce chemin.
    /// </summary>
    internal class FakeDbConnectionFactory : IDbConnectionFactory
    {
        public string GetGrfConnectionString() => "fake-grf";

        public Task<SageConnectionInfo> GetSageConnectionInfoAsync(int soId) =>
            Task.FromResult(new SageConnectionInfo
            {
                ConnectionString = "Server=fake;Database=fake;",
                OmUser = "fake-user",
                OmPassword = "fake-pwd"
            });

        public System.Data.IDbConnection CreateGrfConnection() =>
            throw new InvalidOperationException("Non utilisé par ce test.");
        public System.Data.IDbConnection CreatePersistenceConnection() =>
            throw new InvalidOperationException("Non utilisé par ce test.");
    }

    /// <summary>
    /// ISelectionExpliqueeService dont LireFacturesDepuisPeriodeAsync bloque, PAR soId, jusqu'à
    /// ce que le test appelle explicitement <see cref="Release"/> — permet de tenir le verrou de
    /// RafraichirValorisationAsync "ouvert" un temps contrôlé (aucun Task.Delay/polling, donc
    /// aucune source de flakiness) pour prouver le comportement de rejet immédiat / indépendance
    /// multi-société. Retourne toujours une liste vide (aucune affectation valorisable) une fois
    /// débloqué : le reste du pipeline (BuildOrchestrateurAsync → Traiter) n'a alors aucune
    /// lecture Sage/OM réelle à faire, restant 100% in-process.
    /// </summary>
    internal class BlockingSelectionService : ISelectionExpliqueeService
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<int, TaskCompletionSource<bool>> _entered = new();
        private readonly System.Collections.Concurrent.ConcurrentDictionary<int, TaskCompletionSource<bool>> _release = new();

        private TaskCompletionSource<bool> Entered(int soId) =>
            _entered.GetOrAdd(soId, _ => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));
        private TaskCompletionSource<bool> ReleaseGate(int soId) =>
            _release.GetOrAdd(soId, _ => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));

        /// <summary>Se termine dès que RafraichirValorisationAsync(soId) est entré dans l'appel bloquant.</summary>
        public Task AttendreEntree(int soId) => Entered(soId).Task;

        /// <summary>Débloque l'appel en cours (ou en attente) pour ce soId.</summary>
        public void Release(int soId) => ReleaseGate(soId).TrySetResult(true);

        // TASK-156 (extension périmètre) : bloque également, PAR soId — même mécanisme que
        // LireFacturesDepuisPeriodeAsync ci-dessous — pour permettre de tenir "ouvert" le chemin
        // ConstruireLignesFigeesAsync (qui appelle CETTE méthode, pas LireFacturesDepuisPeriodeAsync)
        // le temps de prouver le test croisé (chemin déclaration bloque le chemin Factures).
        public async Task<IEnumerable<AffectationCandidate>> SelectionnerExpliqueeAsync(
            int soId, DateTime dateDebut, DateTime dateFin, string connectionString, string sageConnectionString,
            Func<string, SensAffectation, Task<(bool Existe, bool TaxeOk)>>? verifierFacture = null)
        {
            Entered(soId).TrySetResult(true);
            await ReleaseGate(soId).Task;
            return Array.Empty<AffectationCandidate>();
        }

        public async Task<IEnumerable<AffectationCandidate>> LireFacturesDepuisPeriodeAsync(
            int soId, DateTime dateDebut, DateTime dateFin, string connectionString, string sageConnectionString)
        {
            Entered(soId).TrySetResult(true);
            await ReleaseGate(soId).Task;
            return Array.Empty<AffectationCandidate>();
        }
    }

    /// <summary>
    /// Stub minimal de IDeclarationRepository pour exercer RÉELLEMENT
    /// ChargerCandidatesSiNecessaireAsync → ConstruireLignesFigeesAsync (premier figeage, aucune
    /// ligne existante). Deux usages selon le test :
    ///  - quand le verrou soId REJETTE le second appel : seuls GetByIdAsync/GetLignesCountAsync
    ///    sont sollicités avant le rejet, tout le reste n'est jamais atteint ;
    ///  - quand le chemin déclaration va au bout (test "réciproque", pas rejeté) : la sélection
    ///    (stubbée vide par BlockingSelectionService), GetSelectionReglementsAsync,
    ///    GetConflitsAutreDeclarationAsync, SaveLignesCandidatesAsync et GetLignesAsync (pour
    ///    RevaliderLignesFigeesAsync) sont sollicités avec des retours vides, sans jamais toucher
    ///    l'orchestrateur/OM réel (aucune affectation à traiter). Le reste n'est jamais atteint
    ///    dans ces deux scénarios et lève explicitement si sollicité par erreur.
    /// </summary>
    internal class MinimalDeclarationRepository : IDeclarationRepository
    {
        public Dictionary<Guid, DeclarationEntete> Declarations { get; } = new();

        private static Exception NotUsed([System.Runtime.CompilerServices.CallerMemberName] string? membre = null) =>
            new InvalidOperationException($"{membre} ne devrait pas être sollicité dans ce test (verrou soId doit rejeter avant).");

        public Task<DeclarationEntete?> GetByIdAsync(Guid id) =>
            Task.FromResult(Declarations.TryGetValue(id, out var d) ? d : null);

        public Task<int> GetLignesCountAsync(Guid declarationId, string domaine, string? filter) => Task.FromResult(0);

        public Task<List<string>> GetSelectionReglementsAsync(Guid declarationId) => Task.FromResult(new List<string>());
        public Task<Dictionary<string, string>> GetConflitsAutreDeclarationAsync(int societeId, Guid declarationIdExclue) =>
            Task.FromResult(new Dictionary<string, string>());
        public Task SaveLignesCandidatesAsync(IEnumerable<LigneCandidate> lignes) => Task.CompletedTask;
        public Task<IEnumerable<LigneCandidate>> GetLignesAsync(Guid declarationId, string domaine, int page, int pageSize, string? sort, string? filter) =>
            Task.FromResult(Enumerable.Empty<LigneCandidate>());

        public Task<DeclarationEntete?> GetByNumeroAsync(string numero) => throw NotUsed();
        public Task<IEnumerable<DeclarationEntete>> GetAllAsync(int societeId, int? exercice, StatutDeclaration? statut) => throw NotUsed();
        public Task CreateAsync(DeclarationEntete declaration) => throw NotUsed();
        public Task UpdateStatutAsync(Guid id, StatutDeclaration statut) => throw NotUsed();
        public Task<bool> ExistsAsync(int societeId, int exercice, int periode, Declaration.Application.Entities.TypePeriode type) => throw NotUsed();
        public Task DeleteAsync(Guid declarationId) => throw NotUsed();
        public Task<Dictionary<Guid, (int NbLignes, decimal MontantTva)>> GetAgregatsListeAsync(IEnumerable<Guid> declarationIds) => throw NotUsed();
        public Task<int> CountAffectationsTamponneesAsync(IEnumerable<string> numerosRapprochement) => throw NotUsed();
        public Task<Dictionary<string, List<string>>> GetLignesDistinctsAsync(Guid declarationId, string domaine) => throw NotUsed();
        public Task UpdateLigneEtatAsync(Guid ligneId, EtatLigne nouvelEtat) => throw NotUsed();
        public Task UpdateLignesEtatBulkAsync(Guid declarationId, string domaine, string? filter, EtatLigne nouvelEtat) => throw NotUsed();
        public Task UpdateLignesEtatBulkByIdsAsync(IEnumerable<Guid> ligneIds, EtatLigne nouvelEtat) => throw NotUsed();
        public Task<IEnumerable<ReglementRapprochementRow>> GetReglementsRapprochementAsync(int soId, DateTime dateDebut, DateTime dateFin, RapprochementFilter filter, int page, int size, string? sort) => throw NotUsed();
        public Task<int> GetReglementsRapprochementCountAsync(int soId, DateTime dateDebut, DateTime dateFin, RapprochementFilter filter) => throw NotUsed();
        public Task<ReglementRapprochementDistincts> GetReglementsRapprochementDistinctsAsync(int soId, DateTime dateDebut, DateTime dateFin) => throw NotUsed();
        public Task<IEnumerable<FactureInterrogationRow>> GetFacturesInterrogationAsync(int soId, DateTime dateDebut, DateTime dateFin, IReadOnlyList<string>? numero, string? fournisseur, IReadOnlyList<string>? reference, IReadOnlyList<string>? origines, IReadOnlyList<string>? statuts, int page, int size, string? sort) => throw NotUsed();
        public Task<int> GetFacturesInterrogationCountAsync(int soId, DateTime dateDebut, DateTime dateFin, IReadOnlyList<string>? numero, string? fournisseur, IReadOnlyList<string>? reference, IReadOnlyList<string>? origines, IReadOnlyList<string>? statuts) => throw NotUsed();
        public Task<FactureInterrogationDistincts> GetFacturesInterrogationDistinctsAsync(int soId, DateTime dateDebut, DateTime dateFin, string? rechercheNumero = null, string? rechercheReference = null) => throw NotUsed();
        public Task TamponnerAffectationsAsync(int dtId, IEnumerable<string> numerosRapprochement) => throw NotUsed();
        public Task DetamponnerAffectationsAsync(int dtId, IEnumerable<string> numerosRapprochement) => throw NotUsed();
        public Task<HashSet<int>> GetEcIdsEnErreurAsync(int soId, IEnumerable<int> ecIds) => throw NotUsed();
        public Task<Dictionary<int, int?>> GetMvPointsActuelsAsync(IEnumerable<int> mvIds) => throw NotUsed();
        public Task UpdateLigneClesAsync(Guid ligneId, int ecId, int mvId) => throw NotUsed();
        public Task ValiderIncoherenceAsync(Guid declarationId, int ecId, string utilisateur) => throw NotUsed();
        public Task ReinitialiserValidationIncoherenceAsync(Guid declarationId, int ecId) => throw NotUsed();
        public Task DeleteLignesAsync(IEnumerable<Guid> ligneIds) => throw NotUsed();
        public Task<IEnumerable<DeclarationEntete>> GetToutesDeclarationsAsync() => throw NotUsed();
        public Task<IEnumerable<int>> GetDistinctDtIdsAffectationsAsync() => throw NotUsed();
        public Task SetDtIdDeclarationAsync(Guid declarationId, int? dtId) => throw NotUsed();
        public Task SaveSelectionReglementsAsync(Guid declarationId, IEnumerable<string> selectedNumeroReglements) => throw NotUsed();
        public Task<EcheanceDiagnosticRow?> GetEcheanceDiagnosticAsync(int soId, int ecId) => throw NotUsed();
        public Task<IReadOnlyList<EcheanceCollisionRow>> GetEcheancesMemeDoNumeroAsync(int soId, string doNumero) => throw NotUsed();
        public Task<string?> GetMotifErreurCacheAsync(int soId, int ecId) => throw NotUsed();
        public Task<IReadOnlyDictionary<int, DocumentReglementSageRow>> GetDocumentsReglementSageAsync(string sageConnectionString, IEnumerable<int> ecNos) => throw NotUsed();
        public Task<CacheLectureRow?> GetDerniereLectureCacheAsync(int soId, int ecId) => throw NotUsed();
        public Task<IReadOnlyList<CacheBucketRow>> GetBucketsCacheAsync(int soId, int ecId) => throw NotUsed();
        public Task SupprimerLignesParEcIdAsync(Guid declarationId, int ecId) => throw NotUsed();
        public Task<string?> GetIdentifiantFiscalSocieteAsync(int soId) => throw NotUsed();
        public Task<IReadOnlyList<CodeActiviteReferentielRow>> GetReferentielCodesActiviteAsync(string? domaine = null) => throw NotUsed();
        public Task<int?> GetDomaineCodeActiviteAsync(string codeActivite) => throw NotUsed();
        public Task<string?> GetDomaineLigneAsync(Guid ligneId) => throw NotUsed();
        public Task UpdateCodeActiviteLigneAsync(Guid ligneId, string codeActivite, string utilisateur) => throw NotUsed();
        public Task<IReadOnlyList<string>> GetDomainesDistinctsLignesAsync(Guid declarationId, IEnumerable<Guid> ligneIds) => throw NotUsed();
        public Task UpdateCodeActiviteBulkByIdsAsync(Guid declarationId, IEnumerable<Guid> ligneIds, string codeActivite, string utilisateur) => throw NotUsed();
        public Task UpdateCodeActiviteBulkAsync(Guid declarationId, string domaine, string? filter, string codeActivite, string utilisateur) => throw NotUsed();
    }

    /// <summary>ILogger&lt;T&gt; no-op — aucune dépendance externe (Microsoft.Extensions.Logging.Abstractions
    /// non listée comme PackageReference direct du projet de test).</summary>
    internal class NoOpLogger<T> : ILogger<T>
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoOpDisposable.Instance;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }

        private class NoOpDisposable : IDisposable
        {
            public static readonly NoOpDisposable Instance = new();
            public void Dispose() { }
        }
    }

    public class Task156ContentionValorisationTests
    {
        private static (DeclarationWorkflowService Service, BlockingSelectionService Selection) BuildService(
            IDeclarationRepository? repository = null)
        {
            var selection = new BlockingSelectionService();
            var service = new DeclarationWorkflowService(
                repository ?? new UnusedDeclarationRepository(),
                selection,
                new FakeDbConnectionFactory(),
                new ConfigurationBuilder().Build(),
                new NoOpLogger<DeclarationWorkflowService>());
            return (service, selection);
        }

        // ── Correctif A : rejet immédiat sur le MÊME soId ────────────────────────────────

        [Fact]
        public async Task DeuxAppelsConcurrents_MemeSoId_SecondRejeteImmediatement()
        {
            var (service, selection) = BuildService();
            var debut = new DateTime(2026, 7, 1);
            var fin = new DateTime(2026, 7, 31);

            // Démarre un premier cycle pour soId=100 — il reste "en cours" tant que le test ne
            // débloque pas explicitement la sélection (aucune horloge, aucun sleep).
            var task1 = service.RafraichirValorisationAsync(100, debut, fin);
            await selection.AttendreEntree(100); // le premier appel a acquis le verrou et tourne

            // Second appel concurrent, MÊME soId : doit être rejeté IMMÉDIATEMENT (décision PO
            // rejet immédiat, pas d'attente) — sans jamais relâcher/débloquer le premier cycle.
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.RafraichirValorisationAsync(100, debut, fin));
            Assert.Contains("100", ex.Message);
            Assert.Contains("en cours", ex.Message, StringComparison.OrdinalIgnoreCase);

            // Le premier cycle, lui, n'a jamais été perturbé par le rejet du second — le débloquer
            // maintenant doit le laisser se terminer normalement (aucune exception).
            selection.Release(100);
            var rapport1 = await task1;
            Assert.NotNull(rapport1);

            // Le verrou est bien relâché après la fin du premier cycle : un troisième appel pour
            // le MÊME soId, une fois le premier terminé, doit réussir normalement (pas de verrou
            // resté bloqué indéfiniment).
            selection.Release(100); // no-op si déjà consommé, prépare le prochain WaitAsync
            var rapport2 = await service.RafraichirValorisationAsync(100, debut, fin);
            Assert.NotNull(rapport2);
        }

        // ── Correctif A : deux soId distincts restent indépendants ───────────────────────

        [Fact]
        public async Task DeuxAppelsConcurrents_SoIdDifferents_AucunBlocageCroise()
        {
            var (service, selection) = BuildService();
            var debut = new DateTime(2026, 7, 1);
            var fin = new DateTime(2026, 7, 31);

            // soId=201 démarre et reste bloqué "en cours" (verrou tenu).
            var task1 = service.RafraichirValorisationAsync(201, debut, fin);
            await selection.AttendreEntree(201);

            // soId=202, distinct, doit pouvoir démarrer et ENTRER dans son propre traitement sans
            // attendre la libération du verrou de soId=201 — sinon "AttendreEntree(202)" ne se
            // terminerait jamais (deadlock), ce que le timeout ci-dessous détecte explicitement.
            var task2 = service.RafraichirValorisationAsync(202, debut, fin);
            var entree202 = selection.AttendreEntree(202);
            var gagnant = await Task.WhenAny(entree202, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.Same(entree202, gagnant); // soId=202 est bien entré, sans blocage croisé avec soId=201

            // Libère les deux — les deux cycles doivent se terminer normalement, en parallèle.
            selection.Release(202);
            selection.Release(201);
            var rapports = await Task.WhenAll(task1, task2);
            Assert.All(rapports, r => Assert.NotNull(r));
        }

        // ── Correctif A (périmètre corrigé 23/07/2026) : test CROISÉ entre deux CHEMINS
        // DIFFÉRENTS parmi les 4 recensés — pas seulement deux appels de la même méthode. C'est
        // exactement le scénario qui a échappé au périmètre initial : l'incident réel du
        // 23/07/2026 14:20 a été déclenché depuis une déclaration (ConstruireLignesFigeesAsync,
        // via ChargerCandidatesSiNecessaireAsync — « Passer au calcul ») pendant qu'un
        // RafraichirValorisationAsync (bouton Factures) aurait pu tourner sur le même soId, sans
        // qu'aucun verrou partagé ne les lie avant ce correctif étendu.

        [Fact]
        public async Task CheminFactures_PuisCheminDeclaration_MemeSoId_CheminDeclarationRejeteImmediatement()
        {
            const int soId = 301;
            var declarationId = Guid.NewGuid();
            var repo = new MinimalDeclarationRepository();
            repo.Declarations[declarationId] = new DeclarationEntete
            {
                Id = declarationId, SocieteId = soId, Exercice = 2026, Periode = 7,
                Statut = StatutDeclaration.EnCours, Numero = "TVA301-2026-07"
            };
            var (service, selection) = BuildService(repo);
            var debut = new DateTime(2026, 7, 1);
            var fin = new DateTime(2026, 7, 31);

            // Chemin 1 (bouton « Rafraîchir » écran Factures) démarre et reste "en cours" pour soId=301.
            var task1 = service.RafraichirValorisationAsync(soId, debut, fin);
            await selection.AttendreEntree(soId);

            // Chemin 2 (déclaration, « Passer au calcul »/« Détail des lignes »), MÊME soId : doit
            // être rejeté IMMÉDIATEMENT — c'est précisément le chemin qui a échappé au périmètre
            // initial du correctif (verrou local à RafraichirValorisationAsync uniquement).
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ChargerCandidatesSiNecessaireAsync(declarationId, "Decaissement"));
            Assert.Contains(soId.ToString(), ex.Message);
            Assert.Contains("en cours", ex.Message, StringComparison.OrdinalIgnoreCase);

            selection.Release(soId);
            var rapport1 = await task1;
            Assert.NotNull(rapport1);
        }

        [Fact]
        public async Task CheminDeclaration_PuisCheminFactures_MemeSoId_CheminFacturesRejeteImmediatement()
        {
            const int soId = 302;
            var declarationId = Guid.NewGuid();
            var repo = new MinimalDeclarationRepository();
            repo.Declarations[declarationId] = new DeclarationEntete
            {
                Id = declarationId, SocieteId = soId, Exercice = 2026, Periode = 7,
                Statut = StatutDeclaration.EnCours, Numero = "TVA302-2026-07"
            };
            var (service, selection) = BuildService(repo);
            var debut = new DateTime(2026, 7, 1);
            var fin = new DateTime(2026, 7, 31);

            // Chemin 2 (déclaration) démarre en premier et reste "en cours" (bloqué dans
            // SelectionnerExpliqueeAsync, appelé par ConstruireLignesFigeesAsync) pour soId=302.
            var task2 = service.ChargerCandidatesSiNecessaireAsync(declarationId, "Decaissement");
            await selection.AttendreEntree(soId);

            // Chemin 1 (bouton Factures), MÊME soId : doit être rejeté IMMÉDIATEMENT — réciproque
            // du test précédent, prouve que le verrou est symétrique quel que soit l'ordre des
            // 2 chemins combinés.
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.RafraichirValorisationAsync(soId, debut, fin));
            Assert.Contains(soId.ToString(), ex.Message);
            Assert.Contains("en cours", ex.Message, StringComparison.OrdinalIgnoreCase);

            selection.Release(soId);
            var alertes = await task2;
            Assert.NotNull(alertes);
        }
    }
}
