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
    /// TASK-082 : bandeau absent en écran ② Affectations pour une ligne exclue DÈS le figeage
    /// (détection TASK-072/076 pendant la lecture du cache — cas réel PO : facture FC2501717,
    /// EC_Id=21473, ligne créée directement Etat=Exclue/MotifRejet renseigné via
    /// DeclarationWorkflowService.MapLignesCandidates, JAMAIS Etat=Proposee suivie d'une
    /// détection a posteriori). Avant ce correctif, RevaliderLignesFigeesAsync ne filtrait que
    /// Etat=Proposee/Integree (TASK-077/078), donc cette ligne n'était jamais soumise au contrôle
    /// GetEcIdsEnErreurAsync et l'alerte LIGNE_FIGEE_A_REVERIFIER n'était jamais émise pour elle —
    /// alors que Synthèse/Contrôle (GetCheckupAsync) affichait déjà le même motif via l'alerte
    /// indépendante LIGNE_EXCLUE. Le front (AffectationsDrill.tsx) ne filtre QUE sur le code
    /// LIGNE_FIGEE_A_REVERIFIER pour peupler son badge « N ligne(s) incohérente(s) », d'où le
    /// bandeau silencieux en écran ② malgré la ligne bien présente dans la grille.
    /// </summary>
    public class Task082LigneExclueDesLeFigeageTests
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
        public async Task ChargerCandidatesSiNecessaireAsync_LigneExclueDesLeFigeage_LeveAlerteIncoherence()
        {
            // Reproduit EXACTEMENT le cas réel PO : ligne jamais Proposee, créée Exclue dès la
            // construction (MapLignesCandidates, branche facture.EnErreur), EC_Id déjà connu.
            var (service, repo, declarationId) = CreerService(new[]
            {
                new LigneCandidate
                {
                    Etat = EtatLigne.Exclue,
                    MotifRejet = "Incohérence Sage détectée rétroactivement en cache : "
                        + "Σ(HT+TVA)=344070.24 ≠ TTC=20700.00 (EC_Id=21473) — pièce exclue de la valorisation.",
                    HT = 20000m, TVA = 0m, TTC = 0m,
                    NumeroFacture = "FC2501717", NumeroRapprochement = "RF26040040",
                    TiersICE = "ICE1", EC_Id = 21473, MV_Id = 555
                }
            });
            repo.EcIdsEnErreur.Add(21473);
            repo.MvPoints[555] = 1; // règlement pointé — seule l'incohérence Sage est en cause

            var alertes = await service.ChargerCandidatesSiNecessaireAsync(declarationId, "Decaissement");

            Assert.Contains(alertes, a => a.Code == "LIGNE_FIGEE_A_REVERIFIER" && a.RefLigne == "FC2501717");

            // Lecture seule stricte : la ligne exclue reste inchangée (aucun recalcul silencieux).
            var ligne = repo.Lignes.Single();
            Assert.Equal(EtatLigne.Exclue, ligne.Etat);
            Assert.Equal(0m, ligne.TVA);
            Assert.Contains("EC_Id=21473", ligne.MotifRejet);
        }

        [Fact]
        public async Task ChargerCandidatesSiNecessaireAsync_LigneExclueSansIncoherenceCache_AucuneAlerte()
        {
            // Ligne exclue pour un AUTRE motif (ex. non rapproché) — pas de sentinelle ERREUR pour
            // son EC_Id : aucune alerte de revalidation ne doit être fabriquée à tort.
            var (service, repo, declarationId) = CreerService(new[]
            {
                new LigneCandidate
                {
                    Etat = EtatLigne.Exclue,
                    MotifRejet = "Règlement non rapproché.",
                    NumeroFacture = "F2", NumeroRapprochement = "REG-2",
                    TiersICE = "ICE1", EC_Id = 200, MV_Id = 300
                }
            });
            // repo.EcIdsEnErreur reste vide pour EC_Id=200.

            var alertes = await service.ChargerCandidatesSiNecessaireAsync(declarationId, "Decaissement");

            Assert.Empty(alertes);
        }

        [Fact]
        public async Task ChargerCandidatesSiNecessaireAsync_LigneExclueIncoherenceValidee_NePasResignaler()
        {
            // TASK-078 étendu aux lignes Exclue : une incohérence déjà validée par le PO n'est plus
            // resignalée à chaque chargement.
            var (service, repo, declarationId) = CreerService(new[]
            {
                new LigneCandidate
                {
                    Etat = EtatLigne.Exclue,
                    MotifRejet = "Incohérence Sage (EC_Id=21473) — pièce exclue de la valorisation.",
                    NumeroFacture = "FC2501717", NumeroRapprochement = "RF26040040",
                    TiersICE = "ICE1", EC_Id = 21473, MV_Id = 555,
                    IncoherenceValidee = true
                }
            });
            repo.EcIdsEnErreur.Add(21473);
            repo.MvPoints[555] = 1;

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
            public Task<IReadOnlyList<string>> GetDomainesDistinctsLignesAsync(Guid declarationId, IEnumerable<Guid> ligneIds) => Task.FromResult<IReadOnlyList<string>>(new List<string>());
            public Task UpdateCodeActiviteBulkByIdsAsync(Guid declarationId, IEnumerable<Guid> ligneIds, string codeActivite, string utilisateur) => Task.CompletedTask;
            public Task UpdateCodeActiviteBulkAsync(Guid declarationId, string domaine, string? filter, string codeActivite, string utilisateur) => Task.CompletedTask;
            public Task UpdateCodeActiviteLigneAsync(Guid ligneId, string codeActivite, string utilisateur) => Task.CompletedTask;
        }
    }
}
