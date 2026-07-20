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
    /// TASK-077 : ChargerCandidatesSiNecessaireAsync revalide, à CHAQUE appel, les lignes déjà
    /// figées dans DM_LGTVA — signalement seul (alerte LIGNE_FIGEE_A_REVERIFIER), jamais de
    /// recalcul ni de modification de ligne/total. Couvre :
    ///  (a) incohérence Sage détectée après le figeage (sentinelle cache),
    ///  (b) règlement dépointé depuis le figeage (MV_Point courant),
    ///  (c) déclaration clôturée → aucune revalidation exécutée,
    ///  (d) ligne figée toujours cohérente → aucune alerte, aucune régression.
    /// </summary>
    public class Task077RevalidationLignesFigeesTests
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
        public async Task ChargerCandidatesSiNecessaireAsync_LigneFigeeIncoherenteApresCoup_LeveAlerteSansModifierLaLigne()
        {
            // Cas réel PO : EC_Id=21473/FC2501717, figé AVANT que TASK-072 ne détecte l'incohérence.
            var (service, repo, declarationId) = CreerService(new[]
            {
                new LigneCandidate
                {
                    Etat = EtatLigne.Proposee, HT = 20000m, TVA = 344050.24m, TTC = 20700m,
                    NumeroFacture = "FC2501717", NumeroRapprochement = "RF26040040",
                    TiersICE = "ICE1", EC_Id = 21473, MV_Id = 555
                }
            });
            repo.EcIdsEnErreur.Add(21473);
            repo.MvPoints[555] = 1; // règlement toujours pointé — seule l'incohérence Sage est en cause

            var alertes = await service.ChargerCandidatesSiNecessaireAsync(declarationId, "Decaissement");

            Assert.Contains(alertes, a => a.Code == "LIGNE_FIGEE_A_REVERIFIER" && a.RefLigne == "FC2501717");
            // Ligne et totaux strictement inchangés — aucun recalcul silencieux.
            var ligne = repo.Lignes.Single();
            Assert.Equal(EtatLigne.Proposee, ligne.Etat);
            Assert.Equal(344050.24m, ligne.TVA);
            Assert.Equal(20700m, ligne.TTC);
        }

        [Fact]
        public async Task ChargerCandidatesSiNecessaireAsync_ReglementDepointeDepuisFigeage_LeveAlerte()
        {
            var (service, repo, declarationId) = CreerService(new[]
            {
                new LigneCandidate
                {
                    Etat = EtatLigne.Proposee, HT = 1000m, TVA = 200m, TTC = 1200m,
                    NumeroFacture = "F1", NumeroRapprochement = "REG-1",
                    TiersICE = "ICE1", EC_Id = 100, MV_Id = 200
                }
            });
            // Aucune entrée dans repo.MvPoints => MV_Id introuvable/dépointé (TryGetValue échoue).

            var alertes = await service.ChargerCandidatesSiNecessaireAsync(declarationId, "Decaissement");

            Assert.Contains(alertes, a => a.Code == "LIGNE_FIGEE_A_REVERIFIER" && a.RefLigne == "F1");
            Assert.Equal(EtatLigne.Proposee, repo.Lignes.Single().Etat);
        }

        [Fact]
        public async Task ChargerCandidatesSiNecessaireAsync_DeclarationCloturee_NeRevalideRien()
        {
            var (service, repo, declarationId) = CreerService(new[]
            {
                new LigneCandidate
                {
                    Etat = EtatLigne.Integree, HT = 1000m, TVA = 200m, TTC = 1200m,
                    NumeroFacture = "F1", NumeroRapprochement = "REG-1",
                    TiersICE = "ICE1", EC_Id = 999, MV_Id = 999
                }
            }, statut: StatutDeclaration.Cloturee);
            // Configuré pour être incohérent des deux façons — la revalidation ne doit
            // même pas s'exécuter (aucun appel aux méthodes de vérification).
            repo.EcIdsEnErreur.Add(999);

            var alertes = await service.ChargerCandidatesSiNecessaireAsync(declarationId, "Decaissement");

            Assert.Empty(alertes);
            Assert.False(repo.GetEcIdsEnErreurAppele, "Aucune requête de revalidation ne doit être exécutée sur une déclaration clôturée.");
            Assert.False(repo.GetMvPointsActuelsAppele, "Aucune requête de revalidation ne doit être exécutée sur une déclaration clôturée.");
        }

        [Fact]
        public async Task ChargerCandidatesSiNecessaireAsync_LigneFigeeToujoursCoherente_AucuneAlerte()
        {
            var (service, repo, declarationId) = CreerService(new[]
            {
                new LigneCandidate
                {
                    Etat = EtatLigne.Proposee, HT = 1000m, TVA = 200m, TTC = 1200m,
                    NumeroFacture = "F1", NumeroRapprochement = "REG-1",
                    TiersICE = "ICE1", EC_Id = 100, MV_Id = 200
                }
            });
            repo.MvPoints[200] = 1; // toujours pointé, pas de sentinelle ERREUR pour EC_Id=100

            var alertes = await service.ChargerCandidatesSiNecessaireAsync(declarationId, "Decaissement");

            Assert.Empty(alertes);
            var ligne = repo.Lignes.Single();
            Assert.Equal(EtatLigne.Proposee, ligne.Etat);
            Assert.Equal(200m, ligne.TVA);
        }

        private class FakeDeclarationRepository : IDeclarationRepository
        {
            public Dictionary<Guid, DeclarationEntete> Declarations { get; } = new();
            public List<LigneCandidate> Lignes { get; } = new();
            public HashSet<int> EcIdsEnErreur { get; } = new();
            public Dictionary<int, int?> MvPoints { get; } = new();
            public bool GetEcIdsEnErreurAppele { get; private set; }
            public bool GetMvPointsActuelsAppele { get; private set; }

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

            public Task<HashSet<int>> GetEcIdsEnErreurAsync(int soId, IEnumerable<int> ecIds)
            {
                GetEcIdsEnErreurAppele = true;
                return Task.FromResult(new HashSet<int>(ecIds.Where(id => EcIdsEnErreur.Contains(id))));
            }

            public Task<Dictionary<int, int?>> GetMvPointsActuelsAsync(IEnumerable<int> mvIds)
            {
                GetMvPointsActuelsAppele = true;
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

            // TASK-080 : aucun conflit inter-déclaration dans ces tests (hors périmètre TASK-077).
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
