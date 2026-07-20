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
    /// TASK-081 : ChargerCandidatesSiNecessaireAsync doit émettre l'alerte LIGNE_FIGEE_A_REVERIFIER
    /// dès le TOUT PREMIER appel (figeage frais, GetLignesCountAsync == 0 au départ), pas seulement
    /// au second appel (branche « déjà figé » couverte par TASK-077). Avant ce correctif, le
    /// premier figeage retournait inconditionnellement Array.Empty&lt;Alerte&gt;() sans relire la
    /// sentinelle CodeTaxe='ERREUR' qu'il venait pourtant d'écrire en cache.
    /// </summary>
    public class Task081PremierFigeageBandeauTests
    {
        /// <summary>
        /// Sous-classe de test : substitue le pipeline de figeage (sélection + orchestrateur Sage,
        /// non mockable simplement — worker externe réel) par des lignes déjà déterminées, pour
        /// isoler la seule chose que TASK-081 corrige : la relecture post-figeage.
        /// </summary>
        private class ServiceAvecFigeageSimule : DeclarationWorkflowService
        {
            private readonly List<LigneCandidate> _lignesAFiger;

            public ServiceAvecFigeageSimule(IDeclarationRepository repository, List<LigneCandidate> lignesAFiger)
                : base(repository, selectionService: null!, connectionFactory: null!, configuration: null!,
                       logger: NullLogger<DeclarationWorkflowService>.Instance)
            {
                _lignesAFiger = lignesAFiger;
            }

            protected override Task<List<LigneCandidate>> ConstruireLignesFigeesAsync(
                Guid declarationId, DeclarationEntete declaration, string domaine)
            {
                foreach (var l in _lignesAFiger)
                {
                    l.DeclarationId = declarationId;
                    l.Domaine = domaine;
                }
                return Task.FromResult(_lignesAFiger);
            }
        }

        [Fact]
        public async Task ChargerCandidatesSiNecessaireAsync_PremierFigeage_EmetAlerteDesLePremierAppel()
        {
            // Cas réel PO : FC2501717/EC_Id=21473, recréée (TASK-079) — au moment du figeage frais,
            // la sentinelle CodeTaxe='ERREUR' est DÉJÀ en cache (posée par un cycle antérieur du
            // pipeline TASK-072/076) pour cet EC_Id.
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
            repo.EcIdsEnErreur.Add(21473);
            repo.MvPoints[555] = 1;

            var ligneAFiger = new LigneCandidate
            {
                Etat = EtatLigne.Proposee, HT = 20000m, TVA = 344050.24m, TTC = 20700m,
                NumeroFacture = "FC2501717", NumeroRapprochement = "RF26040040",
                TiersICE = "ICE1", EC_Id = 21473, MV_Id = 555
            };

            var service = new ServiceAvecFigeageSimule(repo, new List<LigneCandidate> { ligneAFiger });

            // Aucune ligne encore figée : GetLignesCountAsync == 0 → branche premier figeage.
            Assert.Equal(0, await repo.GetLignesCountAsync(declarationId, "Decaissement", null));

            var alertes = await service.ChargerCandidatesSiNecessaireAsync(declarationId, "Decaissement");

            Assert.Contains(alertes, a => a.Code == "LIGNE_FIGEE_A_REVERIFIER" && a.RefLigne == "FC2501717");
            // Lecture seule stricte : la ligne venant d'être figée reste inchangée.
            var ligne = repo.Lignes.Single();
            Assert.Equal(EtatLigne.Proposee, ligne.Etat);
            Assert.Equal(344050.24m, ligne.TVA);
        }

        [Fact]
        public async Task ChargerCandidatesSiNecessaireAsync_PremierFigeageSansIncoherence_AucuneAlerte()
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
            repo.MvPoints[200] = 1; // toujours pointé, pas de sentinelle ERREUR pour EC_Id=100

            var ligneAFiger = new LigneCandidate
            {
                Etat = EtatLigne.Proposee, HT = 1000m, TVA = 200m, TTC = 1200m,
                NumeroFacture = "F1", NumeroRapprochement = "REG-1",
                TiersICE = "ICE1", EC_Id = 100, MV_Id = 200
            };

            var service = new ServiceAvecFigeageSimule(repo, new List<LigneCandidate> { ligneAFiger });

            var alertes = await service.ChargerCandidatesSiNecessaireAsync(declarationId, "Decaissement");

            Assert.Empty(alertes);
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

            public Task<FactureInterrogationDistincts> GetFacturesInterrogationDistinctsAsync(int soId, DateTime dateDebut, DateTime dateFin) => throw new NotImplementedException();

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

            public Task UpdateLigneClesAsync(Guid ligneId, int ecId, int mvId)
            {
                var ligne = Lignes.FirstOrDefault(l => l.Id == ligneId);
                if (ligne != null) { ligne.EC_Id = ecId; ligne.MV_Id = mvId; }
                return Task.CompletedTask;
            }

            public Task ValiderIncoherenceAsync(Guid declarationId, int ecId, string utilisateur)
            {
                foreach (var l in Lignes.Where(l => l.DeclarationId == declarationId && l.EC_Id == ecId))
                {
                    l.IncoherenceValidee = true;
                    l.IncoherenceValideePar = utilisateur;
                    l.IncoherenceValideeLe = DateTime.UtcNow;
                }
                return Task.CompletedTask;
            }

            public Task ReinitialiserValidationIncoherenceAsync(Guid declarationId, int ecId)
            {
                foreach (var l in Lignes.Where(l => l.DeclarationId == declarationId && l.EC_Id == ecId))
                {
                    l.IncoherenceValidee = false;
                    l.IncoherenceValideePar = null;
                    l.IncoherenceValideeLe = null;
                }
                return Task.CompletedTask;
            }

            public Task DeleteAsync(Guid declarationId) => throw new NotImplementedException();

            public Task<Dictionary<Guid, (int NbLignes, decimal MontantTva)>> GetAgregatsListeAsync(IEnumerable<Guid> declarationIds) => throw new NotImplementedException();

            public Task<int> CountAffectationsTamponneesAsync(IEnumerable<string> numerosRapprochement) => Task.FromResult(0);

            // TASK-080 : aucun conflit inter-déclaration dans ces tests (hors périmètre TASK-081).
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
        }
    }
}
