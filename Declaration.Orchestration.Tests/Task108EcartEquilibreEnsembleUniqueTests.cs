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
    // TASK-108 : les trois termes du contrôle d'équilibre (DeclarationsController.GetCheckup)
    // doivent agréger le MÊME ensemble de lignes (Integree || Proposee). Avant correctif,
    // totalTva ne prenait que les lignes Integree alors que TotalDeclareTtc/TotalMontantAffecte
    // couvrent Integree||Proposee : sur une déclaration EnCours (0 Integree), l'écart valait
    // ΣTVA Proposee en entier — la TVA totale mal étiquetée « écart détecté ».
    public class Task108EcartEquilibreEnsembleUniqueTests
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

        private static (bool isValid, decimal ecart) LireEquilibre(IActionResult result)
        {
            var ok = Assert.IsType<OkObjectResult>(result);
            var value = ok.Value!;
            var equilibre = value.GetType().GetProperty("equilibre")!.GetValue(value)!;
            var isValid = (bool)equilibre.GetType().GetProperty("isValid")!.GetValue(equilibre)!;
            var ecart = (decimal)equilibre.GetType().GetProperty("ecart")!.GetValue(equilibre)!;
            return (isValid, ecart);
        }

        [Fact]
        public async Task GetCheckup_DeclarationEnCoursLignesProposeeEquilibrees_EcartQuasiNul()
        {
            // Cas réel TVA1-2026-06/-07 : déclaration EnCours, 0 ligne Integree, lignes
            // valorisées Proposee avec TTC=HT+TVA (aucune anomalie) — l'écart affiché ne doit
            // plus être la TVA totale (faux positif) mais ~0.
            var l1 = new LigneCandidate
            {
                Etat = EtatLigne.Proposee,
                Domaine = "Decaissement",
                NumeroFacture = "FC100",
                Source = "SourceA",
                Taux = 20m,
                HT = 1000m,
                TVA = 200m,
                TTC = 1200m
            };
            var l2 = new LigneCandidate
            {
                Etat = EtatLigne.Proposee,
                Domaine = "Decaissement",
                NumeroFacture = "FC101",
                Source = "SourceB",
                Taux = 10m,
                HT = 500m,
                TVA = 50m,
                TTC = 550m
            };
            var (controller, declarationId) = CreerController(new[] { l1, l2 });

            var result = await controller.GetCheckup(declarationId);

            var (isValid, ecart) = LireEquilibre(result);
            Assert.True(isValid, $"écart attendu ~0, obtenu {ecart}");
            Assert.True(Math.Abs(ecart) < 0.01m);
        }

        [Fact]
        public async Task GetCheckup_DeclarationClotureeLignesIntegreeEquilibrees_EcartQuasiNulInchange()
        {
            // Non-régression écran ⑤ : toutes les lignes Integree, ensembles déjà coïncidents
            // avant/après correctif — doit rester à écart ~0.
            var l1 = new LigneCandidate
            {
                Etat = EtatLigne.Integree,
                Domaine = "Decaissement",
                NumeroFacture = "FC200",
                Source = "SourceA",
                Taux = 20m,
                HT = 300m,
                TVA = 60m,
                TTC = 360m
            };
            var (controller, declarationId) = CreerController(new[] { l1 }, StatutDeclaration.Cloturee);

            var result = await controller.GetCheckup(declarationId);

            var (isValid, ecart) = LireEquilibre(result);
            Assert.True(isValid, $"écart attendu ~0, obtenu {ecart}");
            Assert.True(Math.Abs(ecart) < 0.01m);
        }

        [Fact]
        public async Task GetCheckup_LigneReellementIncoherenteTtcDifferentDeHtPlusTva_EcartNonNulPersiste()
        {
            // Un vrai résidu TTC != HT+TVA (anomalie de valorisation) doit toujours déclencher
            // le contrôle — la correction TASK-108 ne masque pas les vraies anomalies.
            var l1 = new LigneCandidate
            {
                Etat = EtatLigne.Proposee,
                Domaine = "Decaissement",
                NumeroFacture = "FC300",
                Source = "SourceA",
                Taux = 20m,
                HT = 1000m,
                TVA = 200m,
                TTC = 1300m // incohérent : devrait être 1200
            };
            var (controller, declarationId) = CreerController(new[] { l1 });

            var result = await controller.GetCheckup(declarationId);

            var (isValid, ecart) = LireEquilibre(result);
            Assert.False(isValid);
            Assert.Equal(100m, ecart);
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
            public Task<Dictionary<int, (DateTime? DoDate, string? DoReference)>> GetDatesFacturesEtReferencesAsync(int soId, IEnumerable<int> ecIds) => throw new NotImplementedException();
            public Task MettreAJourDateFactureEtReferenceAsync(IEnumerable<(Guid LigneId, DateTime? DateFacture, string? Reference)> lignesAMettreAJour) => throw new NotImplementedException();

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
            public Task<Dictionary<int, DateTime?>> GetDernieresDatesRapprochementAsync(int soId, IEnumerable<int> ecIds) => throw new NotImplementedException(); // TASK-135 (stub, non exercé par ces tests)

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
