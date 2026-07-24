using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Declaration.API.Controllers;
using Declaration.Application.Entities;
using Declaration.Application.Interfaces;
using Declaration.Application.Services;
using Declaration.Core.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Declaration.Orchestration.Tests
{
    // ═══════════════════════════════════════════════════════════════════════════════
    // TASK-175 — 500 au chargement des lignes (GET {id}/lignes) quand les deux domaines
    // se chargent en parallèle pour la MÊME société.
    //
    // Reproduit RÉELLEMENT la course signalée par le PO en production : deux appels
    // concurrents à DeclarationsController.GetLignes (Decaissement puis Encaissement, MÊME
    // déclaration/soId) pendant que le premier tient encore le verrou anti-chevauchement
    // soId (TASK-156, ExecuterAvecVerrouOMAsync). Exerce le VRAI DeclarationWorkflowService
    // + le VRAI DeclarationsController (pas une réimplémentation du verrou), avec le même
    // dispositif de blocage déterministe (TaskCompletionSource, pas de Task.Delay/polling)
    // que Task156ContentionValorisationTests.cs, réutilisé tel quel (BlockingSelectionService/
    // FakeDbConnectionFactory/NoOpLogger, internes au projet de test, mêmes classes).
    //
    // AVANT le correctif TASK-175 : le second appel laissait fuiter l'InvalidOperationException
    // hors du contrôleur → ASP.NET Core répond 500 générique (reproduit ici par le fait que
    // GetLignes() aurait lancé l'exception au lieu de retourner un ConflictObjectResult).
    // APRÈS : le contrôleur catch explicitement et retourne 409 (Conflict), jamais un 500.
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Stub minimal de IDeclarationRepository pour exercer réellement
    /// DeclarationsController.GetLignes → ChargerCandidatesSiNecessaireAsync →
    /// ConstruireLignesFigeesAsync (premier figeage, déclaration neuve, aucune ligne
    /// existante) — même principe que MinimalDeclarationRepository de
    /// Task156ContentionValorisationTests.cs, dupliqué ici plutôt que réutilisé pour ne
    /// modifier aucun fichier existant hors du périmètre de TASK-175.
    /// </summary>
    internal class Task175MinimalRepository : IDeclarationRepository
    {
        public Dictionary<Guid, DeclarationEntete> Declarations { get; } = new();

        private static Exception NotUsed([System.Runtime.CompilerServices.CallerMemberName] string? membre = null) =>
            new InvalidOperationException($"{membre} ne devrait pas être sollicité dans ce test (verrou soId doit rejeter avant, ou méthode non exercée par GetLignes).");

        public Task<DeclarationEntete?> GetByIdAsync(Guid id) =>
            Task.FromResult(Declarations.TryGetValue(id, out var d) ? d : null);

        public Task<int> GetLignesCountAsync(Guid declarationId, string domaine, string? filter) => Task.FromResult(0);
        public Task<List<string>> GetSelectionReglementsAsync(Guid declarationId) => Task.FromResult(new List<string>());
        public Task<Dictionary<string, string>> GetConflitsAutreDeclarationAsync(int societeId, Guid declarationIdExclue) =>
            Task.FromResult(new Dictionary<string, string>());
        public Task SaveLignesCandidatesAsync(IEnumerable<LigneCandidate> lignes) => Task.CompletedTask;
        public Task<IEnumerable<LigneCandidate>> GetLignesAsync(Guid declarationId, string domaine, int page, int pageSize, string? sort, string? filter) =>
            Task.FromResult(Enumerable.Empty<LigneCandidate>());
        public Task<Dictionary<string, List<string>>> GetLignesDistinctsAsync(Guid declarationId, string domaine) =>
            Task.FromResult(new Dictionary<string, List<string>>());
        public Task<IReadOnlyList<CodeActiviteTiersMappingRow>> GetMappingCodeActiviteTiersAsync(int soId) =>
            Task.FromResult<IReadOnlyList<CodeActiviteTiersMappingRow>>(new List<CodeActiviteTiersMappingRow>());

        public Task<DeclarationEntete?> GetByNumeroAsync(string numero) => throw NotUsed();
        public Task<IEnumerable<DeclarationEntete>> GetAllAsync(int societeId, int? exercice, StatutDeclaration? statut) => throw NotUsed();
        public Task CreateAsync(DeclarationEntete declaration) => throw NotUsed();
        public Task UpdateStatutAsync(Guid id, StatutDeclaration statut) => throw NotUsed();
        public Task<bool> ExistsAsync(int societeId, int exercice, int periode, Declaration.Application.Entities.TypePeriode type) => throw NotUsed();
        public Task DeleteAsync(Guid declarationId) => throw NotUsed();
        public Task<Dictionary<Guid, (int NbLignes, decimal MontantTva)>> GetAgregatsListeAsync(IEnumerable<Guid> declarationIds) => throw NotUsed();
        public Task<int> CountAffectationsTamponneesAsync(IEnumerable<string> numerosRapprochement) => throw NotUsed();
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

    public class Task175ConflitVerrouGetLignesTests
    {
        private static (DeclarationsController Controller, BlockingSelectionService Selection) Build(Task175MinimalRepository repo)
        {
            var selection = new BlockingSelectionService();
            var connectionFactory = new FakeDbConnectionFactory();
            var service = new DeclarationWorkflowService(
                repo,
                selection,
                connectionFactory,
                new ConfigurationBuilder().Build(),
                new NoOpLogger<DeclarationWorkflowService>());
            var controller = new DeclarationsController(repo, service, connectionFactory);
            return (controller, selection);
        }

        [Fact]
        public async Task DeuxAppelsConcurrents_GetLignes_MemeSoId_DomainesDifferents_SecondRecoit409PasException()
        {
            const int soId = 375;
            var declarationId = Guid.NewGuid();
            var repo = new Task175MinimalRepository();
            repo.Declarations[declarationId] = new DeclarationEntete
            {
                Id = declarationId, SocieteId = soId, Exercice = 2026, Periode = 7,
                Statut = StatutDeclaration.EnCours, Numero = "TVA375-2026-07"
            };
            var (controller, selection) = Build(repo);

            // Premier appel — GET {id}/lignes?domaine=Decaissement — démarre le premier figeage
            // et reste "en cours" (verrou soId tenu) jusqu'à ce que le test le débloque.
            var task1 = controller.GetLignes(declarationId, "Decaissement");
            await selection.AttendreEntree(soId);

            // Second appel CONCURRENT — GET {id}/lignes?domaine=Encaissement — MÊME déclaration/
            // soId, exactement le scénario du PO (front chargeant les deux domaines en //).
            // AVANT le correctif TASK-175 : ceci aurait lancé une InvalidOperationException NON
            // interceptée (donc un 500 ASP.NET Core générique). Le test échouerait alors avec
            // cette exception au lieu d'obtenir un IActionResult.
            var result2 = await controller.GetLignes(declarationId, "Encaissement");

            var conflict = Assert.IsType<ConflictObjectResult>(result2);
            Assert.Equal(409, conflict.StatusCode);

            // Le premier appel n'a jamais été perturbé par le rejet du second — le débloquer
            // maintenant doit le laisser aboutir normalement (200 OK).
            selection.Release(soId);
            var result1 = await task1;
            Assert.IsType<OkObjectResult>(result1);
        }
    }
}
