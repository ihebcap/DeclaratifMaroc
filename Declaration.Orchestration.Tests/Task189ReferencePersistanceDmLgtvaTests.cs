using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Declaration.Application.Entities;
using Declaration.Application.Interfaces;
using Declaration.Application.Services;
using Declaration.Core.Model;
using Declaration.Selection;

namespace Declaration.Orchestration.Tests
{
    /// <summary>
    /// TASK-189 : comble le trou laissé par TASK-187 — Reference (RT_ECHEANCE.DO_Reference) est
    /// désormais persistée sur DM_LGTVA (LigneCandidate.Reference) et relue jusqu'aux deux méthodes
    /// réellement câblées côté API (ConstruireModeleExportAsync/ConstruireModeleControleAsync), pas
    /// seulement jusqu'à ConstructeurDeclaration/Exporter.cs comme le faisait TASK-187.
    ///
    /// Ces tests simulent le cycle complet figeage → persistance → relecture → export au niveau
    /// applicatif (FakeRepository en mémoire, même patron que Task161CodeActiviteCascadeTests) —
    /// pas un aller-retour SQL Server réel (voir VERIFY/TASK-189_verify.md pour le rejeu sqlcmd réel
    /// sur GR_EMA_DISTRIBUTION, qui couvre la partie que ce test ne peut pas couvrir : la colonne SQL
    /// elle-même, l'idempotence de la migration, et la lecture des lignes déjà existantes).
    /// </summary>
    public class Task189ReferencePersistanceDmLgtvaTests
    {
        private static AffectationCandidate CreerCandidat(string numeroFacture, string? reference) => new()
        {
            Affectation = new AffectationADeclarer
            {
                NumeroFacture = numeroFacture,
                NumeroRapprochement = "MV-" + numeroFacture,
                Sens = SensAffectation.Achat,
                Source = SourceAffectation.Decaissement,
                MontantAffecte = 1200m,
                Reference = reference,
                Tiers = new TiersInfo { Numero = "F001", Nom = "Fournisseur Test", CodeActivite = "" }
            },
            Motif = MotifRejet.Eligible
        };

        private static DeclarationModele ModeleAvecUneLigne(string numeroFacture, decimal taux)
        {
            var modele = new DeclarationModele();
            modele.Lignes.Add(new LigneDeclarationEnrichie
            {
                NumeroFacture = numeroFacture,
                HT = 1000m,
                Taux = taux,
                Tva = 1000m * taux / 100m,
                Ttc = 1200m,
                Prorata = 1m
            });
            return modele;
        }

        // ─── MapLignesCandidates : les DEUX branches de construction doivent propager Reference ──

        [Fact]
        public void MapLignesCandidates_BrancheTaxesLignes_ReferencePropagee()
        {
            var candidat = CreerCandidat("FA189-1", reference: "FCN-189-001");
            var modele = ModeleAvecUneLigne("FA189-1", 20m);

            var lignes = DeclarationWorkflowService.MapLignesCandidates(
                Guid.NewGuid(), "Decaissement", new[] { candidat }, modele);

            Assert.Equal("FCN-189-001", Assert.Single(lignes).Reference);
        }

        [Fact]
        public void MapLignesCandidates_BrancheFactureIntrouvable_ReferencePropagee()
        {
            // Aucune ligne de taxe pour cette facture dans le modèle -> bascule sur la branche
            // "facture introuvable ou non ventilée" (MapLignesCandidates, 2ᵉ branche).
            var candidat = CreerCandidat("FA189-INTROUVABLE", reference: "FCN-189-002");
            var modele = new DeclarationModele(); // aucune ligne de taxe

            var lignes = DeclarationWorkflowService.MapLignesCandidates(
                Guid.NewGuid(), "Decaissement", new[] { candidat }, modele);

            var ligne = Assert.Single(lignes);
            Assert.Equal("FCN-189-002", ligne.Reference);
            Assert.NotEqual("", ligne.MotifRejet); // confirme qu'on est bien passé par cette branche
        }

        [Fact]
        public void MapLignesCandidates_ReferenceNull_AucuneExceptionEtRestNull()
        {
            var candidat = CreerCandidat("FA189-3", reference: null);
            var modele = ModeleAvecUneLigne("FA189-3", 20m);

            var lignes = DeclarationWorkflowService.MapLignesCandidates(
                Guid.NewGuid(), "Decaissement", new[] { candidat }, modele);

            Assert.Null(Assert.Single(lignes).Reference);
        }

        // ─── Cycle complet : figeage -> persistance (simulée) -> relecture -> export ────────────

        [Fact]
        public async Task CycleComplet_LigneNouvellementFigee_ReferenceVisibleDansConstruireModeleControleAsync()
        {
            var declarationId = Guid.NewGuid();
            var repo = new FakeRepositoryTask189();
            repo.Declarations[declarationId] = new DeclarationEntete
            {
                Id = declarationId, Statut = StatutDeclaration.EnCours, SocieteId = 1,
                Exercice = 2026, Periode = 7, Numero = "TVA1-2026-07"
            };

            // 1. Figeage : MapLignesCandidates construit la LigneCandidate depuis l'affectation
            //    sélectionnée (chemin réel : DeclarationWorkflowService.GenererDeclaration -> ce
            //    même appel statique).
            var candidat = CreerCandidat("FA189-CYCLE", reference: "FCN-189-CYCLE");
            var modele = ModeleAvecUneLigne("FA189-CYCLE", 20m);
            var nouvellesLignes = DeclarationWorkflowService.MapLignesCandidates(
                declarationId, "Decaissement", new[] { candidat }, modele);
            foreach (var l in nouvellesLignes) l.Etat = EtatLigne.Proposee;

            // 2. Persistance : exactement l'appel réel (SaveLignesCandidatesAsync), simulé en
            //    mémoire par le FakeRepository (le rejeu SQL réel de la colonne elle-même est fait
            //    séparément via sqlcmd, cf. VERIFY).
            await repo.SaveLignesCandidatesAsync(nouvellesLignes);

            // 3. Relecture + export : les deux méthodes réellement câblées côté API.
            var workflowService = new DeclarationWorkflowService(
                repo, selectionService: null!, connectionFactory: null!, configuration: null!,
                logger: NullLogger<DeclarationWorkflowService>.Instance);

            var modeleControle = await workflowService.ConstruireModeleControleAsync(declarationId);

            var ligneExportee = Assert.Single(modeleControle.Lignes, l => l.NumeroFacture == "FA189-CYCLE");
            Assert.Equal("FCN-189-CYCLE", ligneExportee.Reference);
        }

        [Fact]
        public async Task CycleComplet_LigneNouvellementFigeeIntegree_ReferenceVisibleDansConstruireModeleExportAsync()
        {
            var declarationId = Guid.NewGuid();
            var repo = new FakeRepositoryTask189();
            repo.Declarations[declarationId] = new DeclarationEntete
            {
                Id = declarationId, Statut = StatutDeclaration.Cloturee, SocieteId = 1,
                Exercice = 2026, Periode = 7, Numero = "TVA1-2026-07"
            };

            var candidat = CreerCandidat("FA189-EXPORT", reference: "FCN-189-EXPORT");
            var modele = ModeleAvecUneLigne("FA189-EXPORT", 20m);
            var nouvellesLignes = DeclarationWorkflowService.MapLignesCandidates(
                declarationId, "Decaissement", new[] { candidat }, modele);
            // ConstruireModeleExportAsync (export officiel) ne prend que les lignes Integree.
            foreach (var l in nouvellesLignes) l.Etat = EtatLigne.Integree;

            await repo.SaveLignesCandidatesAsync(nouvellesLignes);

            var workflowService = new DeclarationWorkflowService(
                repo, selectionService: null!, connectionFactory: null!, configuration: null!,
                logger: NullLogger<DeclarationWorkflowService>.Instance);

            var modeleExport = await workflowService.ConstruireModeleExportAsync(declarationId);

            var ligneExportee = Assert.Single(modeleExport.Lignes, l => l.NumeroFacture == "FA189-EXPORT");
            Assert.Equal("FCN-189-EXPORT", ligneExportee.Reference);
        }

        [Fact]
        public async Task CycleComplet_LigneSansReference_RestNullSansException()
        {
            var declarationId = Guid.NewGuid();
            var repo = new FakeRepositoryTask189();
            repo.Declarations[declarationId] = new DeclarationEntete
            {
                Id = declarationId, Statut = StatutDeclaration.EnCours, SocieteId = 1,
                Exercice = 2026, Periode = 7, Numero = "TVA1-2026-07"
            };

            var candidat = CreerCandidat("FA189-SANSREF", reference: null);
            var modele = ModeleAvecUneLigne("FA189-SANSREF", 20m);
            var nouvellesLignes = DeclarationWorkflowService.MapLignesCandidates(
                declarationId, "Decaissement", new[] { candidat }, modele);
            foreach (var l in nouvellesLignes) l.Etat = EtatLigne.Proposee;

            await repo.SaveLignesCandidatesAsync(nouvellesLignes);

            var workflowService = new DeclarationWorkflowService(
                repo, selectionService: null!, connectionFactory: null!, configuration: null!,
                logger: NullLogger<DeclarationWorkflowService>.Instance);

            var modeleControle = await workflowService.ConstruireModeleControleAsync(declarationId);

            var ligneExportee = Assert.Single(modeleControle.Lignes, l => l.NumeroFacture == "FA189-SANSREF");
            Assert.Null(ligneExportee.Reference);
        }

        // ─── Non-régression TASK-147 : un recalcul depuis le cache ne doit pas effacer Reference ──

        [Fact]
        public async Task RecalculerLigneDepuisCacheAsync_LigneAvecReference_ReferenceConservee()
        {
            var declarationId = Guid.NewGuid();
            var repo = new FakeRepositoryTask189();
            repo.Declarations[declarationId] = new DeclarationEntete
            {
                Id = declarationId, Statut = StatutDeclaration.EnCours, SocieteId = 1,
                Exercice = 2026, Periode = 7, Numero = "TVA1-2026-07",
                DateCreation = new DateTime(2026, 7, 1)
            };
            var ligneExistante = new LigneCandidate
            {
                Id = Guid.NewGuid(), DeclarationId = declarationId, Domaine = "Decaissement",
                Etat = EtatLigne.Proposee, NumeroFacture = "FA189-RECALC", EC_Id = 999,
                Reference = "FCN-189-RECALC", TiersNom = "Fournisseur Test"
            };
            repo.Lignes.Add(ligneExistante);
            repo.CacheLecture[999] = new CacheLectureRow { DateLecture = new DateTime(2026, 7, 15), MotifErreur = null };
            repo.CacheBuckets[999] = new List<CacheBucketRow> { new CacheBucketRow { Taux = 20m, HT = 1000m, Tva = 200m, TTC = 1200m } };

            var workflowService = new DeclarationWorkflowService(
                repo, selectionService: null!, connectionFactory: null!, configuration: null!,
                logger: NullLogger<DeclarationWorkflowService>.Instance);

            var (trouvee, recalculee, _) = await workflowService.RecalculerLigneDepuisCacheAsync(declarationId, 999);

            Assert.True(trouvee);
            Assert.True(recalculee);
            var ligneRecalculee = Assert.Single(repo.Lignes, l => l.NumeroFacture == "FA189-RECALC");
            Assert.Equal("FCN-189-RECALC", ligneRecalculee.Reference);
        }

        /// <summary>Fake minimal dédié TASK-189 — même squelette que FakeDeclarationRepositoryTask161,
        /// mais SaveLignesCandidatesAsync/GetLignesAsync sont de vraies implémentations en mémoire
        /// (pas des stubs) pour permettre le cycle complet figeage -> persistance -> relecture.</summary>
        private class FakeRepositoryTask189 : IDeclarationRepository
        {
            public Dictionary<Guid, DeclarationEntete> Declarations { get; } = new();
            public List<LigneCandidate> Lignes { get; } = new();
            public Dictionary<int, CacheLectureRow> CacheLecture { get; } = new();
            public Dictionary<int, List<CacheBucketRow>> CacheBuckets { get; } = new();

            public Task<DeclarationEntete?> GetByIdAsync(Guid id) =>
                Task.FromResult(Declarations.TryGetValue(id, out var d) ? d : null);

            public Task<DeclarationEntete?> GetByNumeroAsync(string numero) => throw new NotImplementedException();
            public Task<IEnumerable<DeclarationEntete>> GetAllAsync(int societeId, int? exercice, StatutDeclaration? statut) => throw new NotImplementedException();
            public Task CreateAsync(DeclarationEntete declaration) => throw new NotImplementedException();
            public Task UpdateStatutAsync(Guid id, StatutDeclaration statut) => throw new NotImplementedException();
            public Task<bool> ExistsAsync(int societeId, int exercice, int periode, Declaration.Application.Entities.TypePeriode type) => throw new NotImplementedException();
            public Task DeleteAsync(Guid declarationId) => throw new NotImplementedException();
            public Task<Dictionary<Guid, (int NbLignes, decimal MontantTva)>> GetAgregatsListeAsync(IEnumerable<Guid> declarationIds) => throw new NotImplementedException();
            public Task<int> CountAffectationsTamponneesAsync(IEnumerable<string> numerosRapprochement) => Task.FromResult(0);

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
            public Task<Dictionary<int, (DateTime? DoDate, string? DoReference)>> GetDatesFacturesEtReferencesAsync(int soId, IEnumerable<int> ecIds) => throw new NotImplementedException();
            public Task MettreAJourDateFactureEtReferenceAsync(IEnumerable<(Guid LigneId, DateTime? DateFacture, string? Reference)> lignesAMettreAJour) => throw new NotImplementedException();
            public Task UpdateLigneEtatAsync(Guid ligneId, EtatLigne nouvelEtat) => throw new NotImplementedException();
            public Task UpdateLignesEtatBulkAsync(Guid declarationId, string domaine, string? filter, EtatLigne nouvelEtat) => throw new NotImplementedException();
            public Task UpdateLignesEtatBulkByIdsAsync(IEnumerable<Guid> ligneIds, EtatLigne nouvelEtat) => throw new NotImplementedException();

            public Task<IEnumerable<ReglementRapprochementRow>> GetReglementsRapprochementAsync(
                int soId, DateTime dateDebut, DateTime dateFin, RapprochementFilter filter, int page, int size, string? sort) =>
                Task.FromResult(Enumerable.Empty<ReglementRapprochementRow>());
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
            public Task<Dictionary<int, DateTime?>> GetDernieresDatesRapprochementAsync(int soId, IEnumerable<int> ecIds) => throw new NotImplementedException();

            public Task TamponnerAffectationsAsync(int dtId, IEnumerable<string> numerosRapprochement) => Task.CompletedTask;
            public Task DetamponnerAffectationsAsync(int dtId, IEnumerable<string> numerosRapprochement) => Task.CompletedTask;
            public Task<HashSet<int>> GetEcIdsEnErreurAsync(int soId, IEnumerable<int> ecIds) => Task.FromResult(new HashSet<int>());
            public Task<Dictionary<int, int?>> GetMvPointsActuelsAsync(IEnumerable<int> mvIds) => Task.FromResult(new Dictionary<int, int?>());
            public Task UpdateLigneClesAsync(Guid ligneId, int ecId, int mvId) => throw new NotImplementedException();
            public Task ValiderIncoherenceAsync(Guid declarationId, int ecId, string utilisateur) => throw new NotImplementedException();
            public Task ReinitialiserValidationIncoherenceAsync(Guid declarationId, int ecId) => throw new NotImplementedException();
            public Task<Dictionary<string, string>> GetConflitsAutreDeclarationAsync(int societeId, Guid declarationIdExclue) => Task.FromResult(new Dictionary<string, string>());
            public Task DeleteLignesAsync(IEnumerable<Guid> ligneIds) => throw new NotImplementedException();
            public Task<IEnumerable<DeclarationEntete>> GetToutesDeclarationsAsync() => throw new NotImplementedException();
            public Task<IEnumerable<int>> GetDistinctDtIdsAffectationsAsync() => throw new NotImplementedException();
            public Task SetDtIdDeclarationAsync(Guid declarationId, int? dtId) => throw new NotImplementedException();
            public Task SaveSelectionReglementsAsync(Guid declarationId, IEnumerable<string> selectedNumeroReglements) => Task.CompletedTask;
            public Task<List<string>> GetSelectionReglementsAsync(Guid declarationId) => Task.FromResult(new List<string>());
            public Task<EcheanceDiagnosticRow?> GetEcheanceDiagnosticAsync(int soId, int ecId) => throw new NotImplementedException();
            public Task<IReadOnlyList<EcheanceCollisionRow>> GetEcheancesMemeDoNumeroAsync(int soId, string doNumero) => throw new NotImplementedException();
            public Task<string?> GetMotifErreurCacheAsync(int soId, int ecId) => throw new NotImplementedException();
            public Task<IReadOnlyDictionary<int, DocumentReglementSageRow>> GetDocumentsReglementSageAsync(string sageConnectionString, IEnumerable<int> ecNos) => throw new NotImplementedException();

            public Task<CacheLectureRow?> GetDerniereLectureCacheAsync(int soId, int ecId) =>
                Task.FromResult(CacheLecture.TryGetValue(ecId, out var c) ? c : null);
            public Task<IReadOnlyList<CacheBucketRow>> GetBucketsCacheAsync(int soId, int ecId) =>
                Task.FromResult<IReadOnlyList<CacheBucketRow>>(CacheBuckets.TryGetValue(ecId, out var b) ? b : new List<CacheBucketRow>());
            public Task SupprimerLignesParEcIdAsync(Guid declarationId, int ecId)
            {
                Lignes.RemoveAll(l => l.DeclarationId == declarationId && l.EC_Id == ecId);
                return Task.CompletedTask;
            }
            public Task<string?> GetIdentifiantFiscalSocieteAsync(int soId) => Task.FromResult<string?>("12345678");

            public Task<IReadOnlyList<CodeActiviteReferentielRow>> GetReferentielCodesActiviteAsync(string? domaine = null) =>
                Task.FromResult<IReadOnlyList<CodeActiviteReferentielRow>>(new List<CodeActiviteReferentielRow>());
            public Task<int?> GetDomaineCodeActiviteAsync(string codeActivite) => Task.FromResult<int?>(2);
            public Task<string?> GetDomaineLigneAsync(Guid ligneId) => Task.FromResult(Lignes.FirstOrDefault(l => l.Id == ligneId)?.Domaine);
            public Task<IReadOnlyList<string>> GetDomainesDistinctsLignesAsync(Guid declarationId, IEnumerable<Guid> ligneIds)
            {
                var ids = ligneIds.ToHashSet();
                IReadOnlyList<string> distincts = Lignes.Where(l => l.DeclarationId == declarationId && ids.Contains(l.Id)).Select(l => l.Domaine).Distinct().ToList();
                return Task.FromResult(distincts);
            }
            public Task UpdateCodeActiviteBulkByIdsAsync(Guid declarationId, IEnumerable<Guid> ligneIds, string codeActivite, string utilisateur) => Task.CompletedTask;
            public Task UpdateCodeActiviteBulkAsync(Guid declarationId, string domaine, string? filter, string codeActivite, string utilisateur) => Task.CompletedTask;
            public Task UpdateCodeActiviteLigneAsync(Guid ligneId, string codeActivite, string utilisateur) => Task.CompletedTask;
        }
    }
}
