using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Declaration.API.Controllers;
using Declaration.Application.Entities;
using Declaration.Application.Interfaces;
using Declaration.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Declaration.Orchestration.Tests
{
    // TASK-112 : le filtre `source` du drill « Cohérence des totaux déclarés » était tautologique
    // (DM_LGTVA.Source == domaine pour 100% des lignes du domaine affiché, TASK-112 § Contexte) —
    // le clic remontait l'intégralité des lignes du domaine au lieu des lignes qui composent
    // l'écart. Nouvel axe : `recapIncoherence`, qui groupe par (Domaine, TTC != HT+TVA). Vérifie
    // que l'écart d'équilibre se décompose exactement en la somme des résidus des lignes
    // incohérentes (`ecartExplique`), et que le filtre repository correspondant isole bien ces
    // lignes (non testé ici — cf. filtre `incoherente` dans DeclarationRepositoryTests si présent).
    public class Task112RecapIncoherenceTests
    {
        private static (DeclarationsController controller, Guid declarationId) CreerController(
            IEnumerable<LigneCandidate> lignes, StatutDeclaration statut = StatutDeclaration.EnCours)
        {
            var declarationId = Guid.NewGuid();
            var repo = new FakeDeclarationRepository();
            repo.Declarations[declarationId] = new DeclarationEntete
            {
                Id = declarationId,
                Statut = statut,
                SocieteId = 1,
                Exercice = 2026,
                Periode = 1,
                Numero = "TVA1-2026-01"
            };
            foreach (var l in lignes)
            {
                l.DeclarationId = declarationId;
                repo.Lignes.Add(l);
            }

            var workflowService = new DeclarationWorkflowService(
                repo,
                selectionService: null!,
                connectionFactory: null!,
                configuration: null!,
                logger: NullLogger<DeclarationWorkflowService>.Instance);

            var controller = new DeclarationsController(repo, workflowService, connectionFactory: null!);

            return (controller, declarationId);
        }

        private static object GetProp(object value, string name) => value.GetType().GetProperty(name)!.GetValue(value)!;

        [Fact]
        public async Task GetCheckup_LigneNonVentileeSeuleIncoherente_DrillIsoleExactementCetteLigne()
        {
            // Cas réel : facture affectée mais non retrouvée dans la ventilation Sage
            // (DeclarationWorkflowService.MapLignesCandidates — HT=MontantAffecte, Taux/TVA/TTC=0).
            // Une ligne cohérente (TTC=HT+TVA) ne doit pas apparaitre dans le groupe incohérent.
            var coherente = new LigneCandidate
            {
                Etat = EtatLigne.Proposee,
                Domaine = "Decaissement",
                NumeroFacture = "FC100",
                Source = "Decaissement",
                Taux = 20m,
                HT = 1000m,
                TVA = 200m,
                TTC = 1200m
            };
            var nonVentilee = new LigneCandidate
            {
                Etat = EtatLigne.Proposee,
                Domaine = "Decaissement",
                NumeroFacture = "FC999",
                Source = "Decaissement",
                Taux = 0m,
                HT = 500m,
                TVA = 0m,
                TTC = 0m
            };
            var (controller, declarationId) = CreerController(new[] { coherente, nonVentilee });

            var result = await controller.GetCheckup(declarationId);

            var ok = Assert.IsType<OkObjectResult>(result);
            var value = ok.Value!;

            var equilibre = GetProp(value, "equilibre");
            var ecart = (decimal)GetProp(equilibre, "ecart");
            var ecartExplique = (bool)GetProp(equilibre, "ecartExplique");

            // écart = Σ(TTC) - Σ(HT) - Σ(TVA) = (1200+0) - (1000+500) - (200+0) = -500
            Assert.Equal(-500m, ecart);
            Assert.True(ecartExplique, "l'écart doit être intégralement expliqué par la ligne non ventilée");

            var recapIncoherence = ((System.Collections.IEnumerable)GetProp(value, "recapIncoherence"))
                .Cast<object>().ToList();

            var incoherentes = recapIncoherence.Where(g => (bool)GetProp(g, "incoherente")).ToList();
            var groupeIncoherent = Assert.Single(incoherentes);
            Assert.Equal(1, (int)GetProp(groupeIncoherent, "nbLignes"));
            Assert.Equal(-500m, (decimal)GetProp(groupeIncoherent, "residu"));

            var coherentes = recapIncoherence.Where(g => !(bool)GetProp(g, "incoherente")).ToList();
            var groupeCoherent = Assert.Single(coherentes);
            Assert.Equal(1, (int)GetProp(groupeCoherent, "nbLignes"));
            Assert.Equal(0m, (decimal)GetProp(groupeCoherent, "residu"));
        }

        [Fact]
        public async Task GetCheckup_ToutesLignesCoherentes_AucunGroupeIncoherentEcartValide()
        {
            // Non-régression TASK-108 : déclaration équilibrée (TTC=HT+TVA partout) → pas de
            // groupe incohérent, équilibre valide.
            var l1 = new LigneCandidate
            {
                Etat = EtatLigne.Proposee,
                Domaine = "Decaissement",
                NumeroFacture = "FC1",
                Source = "Decaissement",
                Taux = 20m,
                HT = 100m,
                TVA = 20m,
                TTC = 120m
            };
            var l2 = new LigneCandidate
            {
                Etat = EtatLigne.Integree,
                Domaine = "Encaissement",
                NumeroFacture = "FC2",
                Source = "Encaissement",
                Taux = 10m,
                HT = 200m,
                TVA = 20m,
                TTC = 220m
            };
            var (controller, declarationId) = CreerController(new[] { l1, l2 });

            var result = await controller.GetCheckup(declarationId);

            var ok = Assert.IsType<OkObjectResult>(result);
            var value = ok.Value!;

            var equilibre = GetProp(value, "equilibre");
            Assert.True((bool)GetProp(equilibre, "isValid"));
            Assert.True((bool)GetProp(equilibre, "ecartExplique"));

            var recapIncoherence = ((System.Collections.IEnumerable)GetProp(value, "recapIncoherence"))
                .Cast<object>().ToList();
            Assert.DoesNotContain(recapIncoherence, g => (bool)GetProp(g, "incoherente"));
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

            public Task<FactureInterrogationDistincts> GetFacturesInterrogationDistinctsAsync(int soId, DateTime dateDebut, DateTime dateFin) => throw new NotImplementedException();

            public Task TamponnerAffectationsAsync(int dtId, IEnumerable<string> numerosRapprochement) => Task.CompletedTask;

            public Task DetamponnerAffectationsAsync(int dtId, IEnumerable<string> numerosRapprochement) => Task.CompletedTask;

            public Task<HashSet<int>> GetEcIdsEnErreurAsync(int soId, IEnumerable<int> ecIds) => Task.FromResult(new HashSet<int>());

            public Task<Dictionary<int, int?>> GetMvPointsActuelsAsync(IEnumerable<int> mvIds) => Task.FromResult(new Dictionary<int, int?>());

            public Task UpdateLigneClesAsync(Guid ligneId, int ecId, int mvId) => throw new NotImplementedException();

            public Task ValiderIncoherenceAsync(Guid declarationId, int ecId, string utilisateur) => throw new NotImplementedException();

            public Task ReinitialiserValidationIncoherenceAsync(Guid declarationId, int ecId) => throw new NotImplementedException();

            public Task DeleteAsync(Guid declarationId) => throw new NotImplementedException();

            public Task<Dictionary<Guid, (int NbLignes, decimal MontantTva)>> GetAgregatsListeAsync(IEnumerable<Guid> declarationIds) => throw new NotImplementedException();

            public Task<int> CountAffectationsTamponneesAsync(IEnumerable<string> numerosRapprochement) => Task.FromResult(0);

            public Task<Dictionary<string, string>> GetConflitsAutreDeclarationAsync(int societeId, Guid declarationIdExclue) =>
                Task.FromResult(new Dictionary<string, string>());

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
            public Task<IReadOnlyList<string>> GetDomainesDistinctsLignesAsync(IEnumerable<Guid> ligneIds) => Task.FromResult<IReadOnlyList<string>>(new List<string>());
            public Task UpdateCodeActiviteBulkByIdsAsync(IEnumerable<Guid> ligneIds, string codeActivite, string utilisateur) => Task.CompletedTask;
            public Task UpdateCodeActiviteBulkAsync(Guid declarationId, string domaine, string? filter, string codeActivite, string utilisateur) => Task.CompletedTask;
            public Task UpdateCodeActiviteLigneAsync(Guid ligneId, string codeActivite, string utilisateur) => Task.CompletedTask;
        }
    }
}
