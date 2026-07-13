using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Declaration.Application.Entities;
using Declaration.Application.Interfaces;
using Declaration.Application.Services;

namespace Declaration.Orchestration.Tests
{
    /// <summary>
    /// TASK-071 : vérifie que CloturerDeclarationAsync pose Integree sur les lignes Proposee
    /// éligibles avant figeage, et que GetCheckupAsync ne bloque plus sur AUCUNE_LIGNE_INTEGREE
    /// tant que des lignes Proposee valorisées existent (pré-flight ④ inclus).
    /// </summary>
    public class Task071DeblocageIntegrationTests
    {
        private static (DeclarationWorkflowService service, FakeDeclarationRepository repo, Guid declarationId) CreerService(
            IEnumerable<LigneCandidate> lignes)
        {
            var declarationId = Guid.NewGuid();
            var repo = new FakeDeclarationRepository();
            repo.Declarations[declarationId] = new DeclarationEntete
            {
                Id = declarationId,
                Statut = StatutDeclaration.EnCours,
                SocieteId = 1,
                Numero = "TVA-TEST-001"
            };
            foreach (var l in lignes)
            {
                l.DeclarationId = declarationId;
                l.Domaine = "Decaissement";
                repo.Lignes.Add(l);
            }

            var service = new DeclarationWorkflowService(
                repo,
                selectionService: null!,
                connectionFactory: null!,
                configuration: null!,
                logger: NullLogger<DeclarationWorkflowService>.Instance);

            return (service, repo, declarationId);
        }

        [Fact]
        public async Task GetCheckupAsync_NeBloquePasSurAucuneLigneIntegree_QuandDesLignesProposeeExistent()
        {
            var (service, _, declarationId) = CreerService(new[]
            {
                new LigneCandidate { Etat = EtatLigne.Proposee, HT = 1000m, TVA = 200m, TTC = 1200m, TiersICE = "ICE1", NumeroFacture = "F1" }
            });

            var checkup = await service.GetCheckupAsync(declarationId);

            Assert.DoesNotContain(checkup.Alertes, a => a.Code == "AUCUNE_LIGNE_INTEGREE");
        }

        [Fact]
        public async Task CloturerDeclarationAsync_PoseIntegreeSurLesLignesProposeeEligibles_PuisCloture()
        {
            var (service, repo, declarationId) = CreerService(new[]
            {
                new LigneCandidate { Etat = EtatLigne.Proposee, HT = 1000m, TVA = 200m, TTC = 1200m, TiersICE = "ICE1", NumeroFacture = "F1", NumeroRapprochement = "REG-1" },
                new LigneCandidate { Etat = EtatLigne.Proposee, HT = 500m, TVA = 100m, TTC = 600m, TiersICE = "ICE2", NumeroFacture = "F2", NumeroRapprochement = "REG-2" },
            });

            await service.CloturerDeclarationAsync(declarationId);

            Assert.All(repo.Lignes, l => Assert.Equal(EtatLigne.Integree, l.Etat));
            Assert.Equal(StatutDeclaration.Cloturee, repo.Declarations[declarationId].Statut);
        }

        [Fact]
        public async Task CloturerDeclarationAsync_RefuseLaClotureEtNePoseAucunEtat_SiAlerteBloquantePersiste()
        {
            var (service, repo, declarationId) = CreerService(new[]
            {
                // Tiers sans ICE => TIERS_SANS_ICE reste bloquant même sur ligne éligible.
                new LigneCandidate { Etat = EtatLigne.Proposee, HT = 1000m, TVA = 200m, TTC = 1200m, TiersICE = "", NumeroFacture = "F1", NumeroRapprochement = "REG-1" },
            });

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CloturerDeclarationAsync(declarationId));

            Assert.All(repo.Lignes, l => Assert.Equal(EtatLigne.Proposee, l.Etat));
            Assert.Equal(StatutDeclaration.EnCours, repo.Declarations[declarationId].Statut);
        }

        private class FakeDeclarationRepository : IDeclarationRepository
        {
            public Dictionary<Guid, DeclarationEntete> Declarations { get; } = new();
            public List<LigneCandidate> Lignes { get; } = new();

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

            public Task<bool> ExistsAsync(int societeId, int exercice, int periode, TypePeriode type) => throw new NotImplementedException();
            public Task SaveLignesCandidatesAsync(IEnumerable<LigneCandidate> lignes) => throw new NotImplementedException();

            public Task<IEnumerable<LigneCandidate>> GetLignesAsync(Guid declarationId, string domaine, int page, int pageSize, string? sort, string? filter) =>
                Task.FromResult(Lignes.Where(l => l.DeclarationId == declarationId && l.Domaine == domaine));

            public Task<int> GetLignesCountAsync(Guid declarationId, string domaine, string? filter) => throw new NotImplementedException();
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

            public Task<FactureInterrogationDistincts> GetFacturesInterrogationDistinctsAsync(int soId, DateTime dateDebut, DateTime dateFin) => throw new NotImplementedException();

            public Task TamponnerAffectationsAsync(int dtId, IEnumerable<string> numerosRapprochement) => Task.CompletedTask;

            public Task DetamponnerAffectationsAsync(int dtId, IEnumerable<string> numerosRapprochement) => Task.CompletedTask;

            // TASK-077 : GetCheckupAsync exerce désormais la revalidation légère — ce fake n'a
            // aucune ligne avec EC_Id/MV_Id renseigné (hors périmètre de ces tests TASK-071),
            // donc rien à signaler ni à backfiller ; réponses vides plutôt que NotImplementedException.
            public Task<HashSet<int>> GetEcIdsEnErreurAsync(IEnumerable<int> ecIds) => Task.FromResult(new HashSet<int>());

            public Task<Dictionary<int, int?>> GetMvPointsActuelsAsync(IEnumerable<int> mvIds) => Task.FromResult(new Dictionary<int, int?>());

            public Task UpdateLigneClesAsync(Guid ligneId, int ecId, int mvId) => Task.CompletedTask;

            public Task ValiderIncoherenceAsync(Guid declarationId, int ecId, string utilisateur) => Task.CompletedTask;

            public Task ReinitialiserValidationIncoherenceAsync(Guid declarationId, int ecId) => Task.CompletedTask;

            public Task DeleteAsync(Guid declarationId) => throw new NotImplementedException();

            public Task<Dictionary<Guid, (int NbLignes, decimal MontantTva)>> GetAgregatsListeAsync(IEnumerable<Guid> declarationIds) => throw new NotImplementedException();

            public Task<int> CountAffectationsTamponneesAsync(IEnumerable<string> numerosRapprochement) => Task.FromResult(0);

            // TASK-080 : aucun conflit inter-déclaration dans ces tests (hors périmètre TASK-071).
            public Task<Dictionary<string, string>> GetConflitsAutreDeclarationAsync(int societeId, Guid declarationIdExclue) =>
                Task.FromResult(new Dictionary<string, string>());

            public Task DeleteLignesAsync(IEnumerable<Guid> ligneIds)
            {
                var ids = new HashSet<Guid>(ligneIds);
                Lignes.RemoveAll(l => ids.Contains(l.Id));
                return Task.CompletedTask;
            }
        }
    }
}
