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
    public class Task102NumeroReglementAnomaliesFactureTests
    {
        private static (DeclarationWorkflowService service, FakeDeclarationRepository repo, Guid declarationId) CreerService(
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
        public async Task RevaliderLignesFigeesAsync_IncoherenceSageSurLigneExclue_AvecReglement_InclutReglementDansMessage()
        {
            // Arrange
            var l = new LigneCandidate
            {
                Etat = EtatLigne.Exclue,
                NumeroFacture = "FC2501717",
                NumeroRapprochement = "RF26040040",
                EC_Id = 21473,
                MV_Id = 555
            };
            var (service, repo, declarationId) = CreerService(new[] { l });
            repo.EcIdsEnErreur.Add(21473);

            // Act
            var alertes = await service.RevaliderLignesFigeesAsync(declarationId, "Decaissement");

            // Assert
            var alerte = Assert.Single(alertes, a => a.Code == "LIGNE_FIGEE_A_REVERIFIER");
            Assert.Contains("Facture FC2501717, règlement RF26040040 exclue de la valorisation", alerte.Message);
        }

        [Fact]
        public async Task RevaliderLignesFigeesAsync_IncoherenceSageSurLigneExclue_SansReglement_ConserveMessageOriginal()
        {
            // Arrange
            var l = new LigneCandidate
            {
                Etat = EtatLigne.Exclue,
                NumeroFacture = "FC2501717",
                NumeroRapprochement = "",
                EC_Id = 21473,
                MV_Id = 555
            };
            var (service, repo, declarationId) = CreerService(new[] { l });
            repo.EcIdsEnErreur.Add(21473);

            // Act
            var alertes = await service.RevaliderLignesFigeesAsync(declarationId, "Decaissement");

            // Assert
            var alerte = Assert.Single(alertes, a => a.Code == "LIGNE_FIGEE_A_REVERIFIER");
            Assert.Contains("Facture FC2501717 exclue de la valorisation", alerte.Message);
            Assert.DoesNotContain("règlement", alerte.Message);
        }

        [Fact]
        public async Task GetCheckupAsync_FactureNonVentilee_AvecReglement_InclutReglementDansMessage()
        {
            // Arrange
            var l = new LigneCandidate
            {
                Etat = EtatLigne.Proposee,
                NumeroFacture = "FC200",
                NumeroRapprochement = "RC300",
                MotifRejet = "Facture introuvable"
            };
            var (service, repo, declarationId) = CreerService(new[] { l });

            // Act
            var model = await service.GetCheckupAsync(declarationId);

            // Assert
            var alerte = Assert.Single(model.Alertes, a => a.Code == "FACTURE_NON_VENTILEE");
            Assert.Equal("Ligne en anomalie de recalcul (facture FC200, règlement RC300) : Facture introuvable", alerte.Message);
        }

        [Fact]
        public async Task GetCheckupAsync_FactureNonVentilee_SansReglement_ConserveMessageOriginal()
        {
            // Arrange
            var l = new LigneCandidate
            {
                Etat = EtatLigne.Proposee,
                NumeroFacture = "FC200",
                NumeroRapprochement = "",
                MotifRejet = "Facture introuvable"
            };
            var (service, repo, declarationId) = CreerService(new[] { l });

            // Act
            var model = await service.GetCheckupAsync(declarationId);

            // Assert
            var alerte = Assert.Single(model.Alertes, a => a.Code == "FACTURE_NON_VENTILEE");
            Assert.Equal("Ligne en anomalie de recalcul : Facture introuvable", alerte.Message);
            Assert.DoesNotContain("règlement", alerte.Message);
        }

        [Fact]
        public async Task GetCheckupAsync_LigneIncoherenceValidee_NeGenerePasAlerteFactureNonVentilee()
        {
            // TASK-177 : une incohérence déjà validée explicitement par le PO (IncoherenceValidee=1,
            // même pattern que RevaliderLignesFigeesAsync) ne doit plus déclencher l'alerte bloquante
            // FACTURE_NON_VENTILEE — cas réel cité par le PO (FC2501717/RF26040040, FC2501667/RF26030075).
            var l = new LigneCandidate
            {
                Etat = EtatLigne.Proposee,
                NumeroFacture = "FC2501717",
                NumeroRapprochement = "RF26040040",
                MotifRejet = "Incohérence Sage HT/TVA/TTC",
                IncoherenceValidee = true
            };
            var (service, repo, declarationId) = CreerService(new[] { l });

            // Act
            var model = await service.GetCheckupAsync(declarationId);

            // Assert
            Assert.DoesNotContain(model.Alertes, a => a.Code == "FACTURE_NON_VENTILEE");
        }

        [Fact]
        public async Task GetCheckupAsync_LigneIncoherenceNonValidee_GenereToujoursAlerteFactureNonVentilee()
        {
            // Non-régression : une ligne identique mais NON validée doit continuer à bloquer.
            var l = new LigneCandidate
            {
                Etat = EtatLigne.Proposee,
                NumeroFacture = "FC2501667",
                NumeroRapprochement = "RF26030075",
                MotifRejet = "Incohérence Sage HT/TVA/TTC",
                IncoherenceValidee = false
            };
            var (service, repo, declarationId) = CreerService(new[] { l });

            // Act
            var model = await service.GetCheckupAsync(declarationId);

            // Assert
            Assert.Contains(model.Alertes, a => a.Code == "FACTURE_NON_VENTILEE");
        }

        private class FakeDeclarationRepository : IDeclarationRepository
        {
            public Dictionary<Guid, DeclarationEntete> Declarations { get; } = new();
            public List<LigneCandidate> Lignes { get; } = new();
            public HashSet<int> EcIdsEnErreur { get; } = new();
            public Dictionary<int, int?> MvPoints { get; } = new();

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

            public Task<FactureInterrogationDistincts> GetFacturesInterrogationDistinctsAsync(int soId, DateTime dateDebut, DateTime dateFin, string? rechercheNumero = null, string? rechercheReference = null) => throw new NotImplementedException();
            public Task<Dictionary<int, DateTime?>> GetDernieresDatesRapprochementAsync(int soId, IEnumerable<int> ecIds) => throw new NotImplementedException(); // TASK-135 (stub, non exercé par ces tests)

            public Task TamponnerAffectationsAsync(int dtId, IEnumerable<string> numerosRapprochement) => Task.CompletedTask;

            public Task DetamponnerAffectationsAsync(int dtId, IEnumerable<string> numerosRapprochement) => Task.CompletedTask;

            public Task<HashSet<int>> GetEcIdsEnErreurAsync(int soId, IEnumerable<int> ecIds) =>
                Task.FromResult(new HashSet<int>(ecIds.Where(id => EcIdsEnErreur.Contains(id))));

            public Task<Dictionary<int, int?>> GetMvPointsActuelsAsync(IEnumerable<int> mvIds)
            {
                var result = new Dictionary<int, int?>();
                foreach (var id in mvIds)
                    if (MvPoints.TryGetValue(id, out var p)) result[id] = p;
                return Task.FromResult(result);
            }

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
