using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Declaration.Application.Entities;
using Declaration.Application.Services;
using Declaration.Core;
using Declaration.Core.Model;
using Declaration.Selection;
using SageTaxReader.Contracts;

namespace Declaration.Orchestration.Tests
{
    /// <summary>
    /// TASK-224 : Reproduction du bug de duplication des lignes pour les factures
    /// réglées en plusieurs fois (surdéclaration de 64 908,46 MAD sur TVA1-2026-03-T).
    /// </summary>
    public class Task224ReproductionDoublonsTests
    {
        [Fact]
        public void ConstructeurDeclaration_SurJeuSimulation_ProduitExactement15LignesEtTvaSaine()
        {
            // Arrange
            var factures = Task224DonneesSimulation.CreerFactures();
            var candidats = Task224DonneesSimulation.CreerCandidats();
            var affectations = candidats.Select(c => c.Affectation).ToList();

            // Act
            var modele = ConstructeurDeclaration.ConstruireDeclaration(
                affectations,
                aff => factures.TryGetValue(aff.NumeroFacture, out var doc) ? doc : null,
                2);

            // Assert : ConstructeurDeclaration est sain
            Assert.Equal(15, modele.Lignes.Count);
            var totalTva = Math.Round(modele.Lignes.Sum(l => l.Tva), 2);
            Assert.True(Math.Abs(totalTva - 68281.46m) <= 0.01m, $"Total TVA attendu ~68281.46 mais obtenu {totalTva}");

            // Vérification par facture scindée
            var lignesFF29 = modele.Lignes.Where(l => l.NumeroFacture == "FF260029").ToList();
            Assert.Equal(2, lignesFF29.Count);
            Assert.Equal(26747.80m, Math.Round(lignesFF29.Sum(l => l.Tva), 2));

            var lignesFF27 = modele.Lignes.Where(l => l.NumeroFacture == "FF260027").ToList();
            Assert.Equal(2, lignesFF27.Count);
            Assert.Equal(24758.20m, Math.Round(lignesFF27.Sum(l => l.Tva), 2));

            var lignesFF28 = modele.Lignes.Where(l => l.NumeroFacture == "FF260028").ToList();
            Assert.Equal(2, lignesFF28.Count);
            Assert.Equal(13402.46m, Math.Round(lignesFF28.Sum(l => l.Tva), 2));
        }

        [Fact]
        public void MapLignesCandidates_SurFacturesRegleesEnPlusieursFois_NeDoitPasDupliquerLesLignes()
        {
            // Arrange
            var declarationId = Guid.NewGuid();
            var factures = Task224DonneesSimulation.CreerFactures();
            var candidats = Task224DonneesSimulation.CreerCandidats();
            var affectations = candidats.Select(c => c.Affectation).ToList();

            var modele = ConstructeurDeclaration.ConstruireDeclaration(
                affectations,
                aff => factures.TryGetValue(aff.NumeroFacture, out var doc) ? doc : null,
                2);

            // Act
            var lignes = DeclarationWorkflowService.MapLignesCandidates(declarationId, "Decaissement", candidats, modele);

            // Assert attendu (15 lignes, 68 281,46 MAD, 0 doublon)
            // CE TEST EST ROUGE AVANT LE CORRECTIF (produit 21 lignes, total 133 189,91 MAD).
            Assert.Equal(15, lignes.Count);
            var totalTva = Math.Round(lignes.Sum(l => l.TVA), 2);
            Assert.True(Math.Abs(totalTva - 68281.46m) <= 0.01m, $"Total TVA attendu ~68281.46 mais obtenu {totalTva}");

            // Aucun groupe (facture, règlement, taux) présent plusieurs fois
            var doublons = lignes
                .GroupBy(l => new { l.NumeroFacture, l.NumeroRapprochement, l.Taux })
                .Where(g => g.Count() > 1)
                .ToList();
            Assert.Empty(doublons);

            // Chacune des 3 factures réglées en 2 fois doit avoir exactement 2 lignes (1 par règlement)
            var ff29 = lignes.Where(l => l.NumeroFacture == "FF260029").ToList();
            Assert.Equal(2, ff29.Count);
            Assert.Contains(ff29, l => l.NumeroRapprochement == "RF26100006");
            Assert.Contains(ff29, l => l.NumeroRapprochement == "RF26100007");

            var ff27 = lignes.Where(l => l.NumeroFacture == "FF260027").ToList();
            Assert.Equal(2, ff27.Count);

            var ff28 = lignes.Where(l => l.NumeroFacture == "FF260028").ToList();
            Assert.Equal(2, ff28.Count);
        }

        [Fact]
        public void Scenario2_OrdreInverseDesReglements_PasDeDoublon()
        {
            // Inverse l'ordre des candidats pour vérifier l'absence d'impact de l'ordre d'arrivée
            var declarationId = Guid.NewGuid();
            var factures = Task224DonneesSimulation.CreerFactures();
            var candidats = Task224DonneesSimulation.CreerCandidats();
            candidats.Reverse();

            var affectations = candidats.Select(c => c.Affectation).ToList();

            var modele = ConstructeurDeclaration.ConstruireDeclaration(
                affectations,
                aff => factures.TryGetValue(aff.NumeroFacture, out var doc) ? doc : null,
                2);

            var lignes = DeclarationWorkflowService.MapLignesCandidates(declarationId, "Decaissement", candidats, modele);

            Assert.Equal(15, lignes.Count);
            var totalTva = Math.Round(lignes.Sum(l => l.TVA), 2);
            Assert.True(Math.Abs(totalTva - 68281.46m) <= 0.01m);

            var doublons = lignes
                .GroupBy(l => new { l.NumeroFacture, l.NumeroRapprochement, l.Taux })
                .Where(g => g.Count() > 1)
                .ToList();
            Assert.Empty(doublons);
        }

        [Fact]
        public void Scenario5_FactureRegleeEnTroisFois_DontUneHorsPeriode_SeulsReglementsDansPeriodeDeclares()
        {
            // Facture TTC 150 000 (TVA 25 000 à 20%) réglée en 3 échéances de 50 000
            // Seules 2 échéances sont sélectionnées dans la déclaration (1 échéance hors période)
            var declarationId = Guid.NewGuid();
            var docTaxes = new DocumentTaxesInfo
            {
                NumeroPiece = "FF_3FOIS",
                TotalHT = 125000.00,
                TotalTva = 25000.00,
                TotalTtc = 150000.00,
                LignesTaxe = new List<TaxeDetail>
                {
                    new TaxeDetail { Taux = 20, MontantTva = 25000.00, BaseHT = 125000.00, Code = "T20" }
                }
            };

            var candidats = new List<AffectationCandidate>
            {
                new AffectationCandidate
                {
                    Affectation = new AffectationADeclarer
                    {
                        EC_Id = 9001,
                        MV_Id = 9101,
                        NumeroFacture = "FF_3FOIS",
                        NumeroRapprochement = "REG_P1",
                        MontantAffecte = 50000.00m,
                        Source = SourceAffectation.Decaissement
                    }
                },
                new AffectationCandidate
                {
                    Affectation = new AffectationADeclarer
                    {
                        EC_Id = 9001,
                        MV_Id = 9102,
                        NumeroFacture = "FF_3FOIS",
                        NumeroRapprochement = "REG_P2",
                        MontantAffecte = 50000.00m,
                        Source = SourceAffectation.Decaissement
                    }
                }
            };

            var modele = ConstructeurDeclaration.ConstruireDeclaration(
                candidats.Select(c => c.Affectation).ToList(),
                _ => docTaxes,
                2);

            var lignes = DeclarationWorkflowService.MapLignesCandidates(declarationId, "Decaissement", candidats, modele);

            // 2 lignes déclarées pour les 2 règlements de la période
            Assert.Equal(2, lignes.Count);
            // TVA totale déclarée = 2/3 de 25 000 = ~16 666,67
            var totalTva = Math.Round(lignes.Sum(l => l.TVA), 2);
            Assert.True(totalTva < 25000.00m);
            Assert.DoesNotContain(modele.Alertes, a => a.Code == "TVA_FACTURE_SURDECLAREE");
        }

        [Fact]
        public async Task DetecterDoublonsAsync_SurLignesDupliquees_DetecteEtGroupeCorrectement()
        {
            var declarationId = Guid.NewGuid();
            var repo = new FakeRepoTask224();
            repo.Declarations[declarationId] = new DeclarationEntete { Id = declarationId, Numero = "TVA1-2026-03-T" };

            // Injecte artificiellement 2 lignes dupliquées (même facture, même règlement, même taux)
            repo.Lignes.Add(new LigneCandidate
            {
                Id = Guid.NewGuid(),
                DeclarationId = declarationId,
                Domaine = "Decaissement",
                NumeroFacture = "FF_DUP",
                NumeroRapprochement = "RF_DUP",
                Taux = 20,
                TVA = 1000m,
                HT = 5000m,
                TTC = 6000m,
                MontantAffecte = 6000m,
                TiersNom = "TIERS TEST"
            });
            repo.Lignes.Add(new LigneCandidate
            {
                Id = Guid.NewGuid(),
                DeclarationId = declarationId,
                Domaine = "Decaissement",
                NumeroFacture = "FF_DUP",
                NumeroRapprochement = "RF_DUP",
                Taux = 20,
                TVA = 1000m,
                HT = 5000m,
                TTC = 6000m,
                MontantAffecte = 6000m,
                TiersNom = "TIERS TEST"
            });
            // Et 1 ligne saine (non dupliquée)
            repo.Lignes.Add(new LigneCandidate
            {
                Id = Guid.NewGuid(),
                DeclarationId = declarationId,
                Domaine = "Decaissement",
                NumeroFacture = "FF_SAINE",
                NumeroRapprochement = "RF_SAIN",
                Taux = 20,
                TVA = 500m,
                HT = 2500m,
                TTC = 3000m,
                MontantAffecte = 3000m,
                TiersNom = "TIERS SAIN"
            });

            var workflow = new DeclarationWorkflowService(
                repo, selectionService: null!, connectionFactory: null!, configuration: null!,
                logger: Microsoft.Extensions.Logging.Abstractions.NullLogger<DeclarationWorkflowService>.Instance);

            var diag = await workflow.DetecterDoublonsAsync(declarationId);

            Assert.Equal(1, diag.TotalGroupesEnDoublon);
            Assert.Equal(1, diag.TotalLignesEnTrop);
            var groupe = Assert.Single(diag.Groupes);
            Assert.Equal("FF_DUP", groupe.NumeroFacture);
            Assert.Equal("RF_DUP", groupe.NumeroRapprochement);
            Assert.Equal(20, groupe.Taux);
            Assert.Equal(2, groupe.NombreLignes);
            Assert.Equal(2000m, groupe.TvaCumulee);
        }

        [Fact]
        public async Task DetecterDoublonsAsync_DeclarationSaine_RetourneZeroDoublon()
        {
            var declarationId = Guid.NewGuid();
            var repo = new FakeRepoTask224();
            repo.Declarations[declarationId] = new DeclarationEntete { Id = declarationId, Numero = "TVA1-2026-03-T" };

            repo.Lignes.Add(new LigneCandidate
            {
                Id = Guid.NewGuid(),
                DeclarationId = declarationId,
                Domaine = "Decaissement",
                NumeroFacture = "FF_SAINE_1",
                NumeroRapprochement = "RF_SAIN_1",
                Taux = 20,
                TVA = 500m,
                HT = 2500m,
                TTC = 3000m,
                MontantAffecte = 3000m,
                TiersNom = "TIERS 1"
            });
            repo.Lignes.Add(new LigneCandidate
            {
                Id = Guid.NewGuid(),
                DeclarationId = declarationId,
                Domaine = "Decaissement",
                NumeroFacture = "FF_SAINE_2",
                NumeroRapprochement = "RF_SAIN_2",
                Taux = 20,
                TVA = 600m,
                HT = 3000m,
                TTC = 3600m,
                MontantAffecte = 3600m,
                TiersNom = "TIERS 2"
            });

            var workflow = new DeclarationWorkflowService(
                repo, selectionService: null!, connectionFactory: null!, configuration: null!,
                logger: Microsoft.Extensions.Logging.Abstractions.NullLogger<DeclarationWorkflowService>.Instance);

            var diag = await workflow.DetecterDoublonsAsync(declarationId);

            Assert.Equal(0, diag.TotalGroupesEnDoublon);
            Assert.Equal(0, diag.TotalLignesEnTrop);
            Assert.Empty(diag.Groupes);
        }

        private class FakeRepoTask224 : Declaration.Application.Interfaces.IDeclarationRepository
        {
            public Dictionary<Guid, DeclarationEntete> Declarations { get; } = new();
            public List<LigneCandidate> Lignes { get; } = new();

            public Task<DeclarationEntete?> GetByIdAsync(Guid id) =>
                Task.FromResult(Declarations.TryGetValue(id, out var d) ? d : null);
            public Task<DeclarationEntete?> GetByNumeroAsync(string numero) => throw new NotImplementedException();
            public Task<IEnumerable<DeclarationEntete>> GetAllAsync(int societeId, int? exercice, StatutDeclaration? statut) => throw new NotImplementedException();
            public Task CreateAsync(DeclarationEntete declaration) => throw new NotImplementedException();
            public Task UpdateStatutAsync(Guid id, StatutDeclaration statut) => throw new NotImplementedException();
            public Task<bool> ExistsAsync(int societeId, int exercice, int periode, Declaration.Application.Entities.TypePeriode type) => throw new NotImplementedException();
            public Task DeleteAsync(Guid declarationId) => throw new NotImplementedException();
            public Task<Dictionary<Guid, (int NbLignes, int NbFactures, decimal MontantTva)>> GetAgregatsListeAsync(IEnumerable<Guid> declarationIds) => throw new NotImplementedException();
            public Task<int> CountAffectationsTamponneesAsync(IEnumerable<string> numerosRapprochement) => Task.FromResult(0);
            public Task SaveLignesCandidatesAsync(IEnumerable<LigneCandidate> lignes)
            {
                Lignes.AddRange(lignes);
                return Task.CompletedTask;
            }
            public Task<IEnumerable<LigneCandidate>> GetLignesAsync(Guid declarationId, string domaine, int page, int pageSize, string? sort, string? filter) =>
                Task.FromResult(Lignes.Where(l => l.DeclarationId == declarationId && (string.IsNullOrEmpty(domaine) || l.Domaine == domaine)));
            public Task<int> GetLignesCountAsync(Guid declarationId, string domaine, string? filter) =>
                Task.FromResult(Lignes.Count(l => l.DeclarationId == declarationId && (string.IsNullOrEmpty(domaine) || l.Domaine == domaine)));
            public Task<Dictionary<string, List<string>>> GetLignesDistinctsAsync(Guid declarationId, string domaine) => throw new NotImplementedException();
            public Task<Dictionary<int, (DateTime? DoDate, string? DoReference)>> GetDatesFacturesEtReferencesAsync(int soId, IEnumerable<int> ecIds) => throw new NotImplementedException();
            public Task MettreAJourDateFactureEtReferenceAsync(IEnumerable<(Guid LigneId, DateTime? DateFacture, string? Reference)> lignesAMettreAJour) => throw new NotImplementedException();
            public Task UpdateLigneEtatAsync(Guid ligneId, EtatLigne nouvelEtat) => throw new NotImplementedException();
            public Task UpdateLignesEtatBulkAsync(Guid declarationId, string domaine, string? filter, EtatLigne nouvelEtat) => throw new NotImplementedException();
            public Task UpdateLignesEtatBulkByIdsAsync(IEnumerable<Guid> ligneIds, EtatLigne nouvelEtat) => throw new NotImplementedException();
            public Task<IEnumerable<ReglementRapprochementRow>> GetReglementsRapprochementAsync(int soId, DateTime dateDebut, DateTime dateFin, RapprochementFilter filter, int page, int size, string? sort) => throw new NotImplementedException();
            public Task<int> GetReglementsRapprochementCountAsync(int soId, DateTime dateDebut, DateTime dateFin, RapprochementFilter filter) => throw new NotImplementedException();
            public Task<ReglementRapprochementDistincts> GetReglementsRapprochementDistinctsAsync(int soId, DateTime dateDebut, DateTime dateFin) => throw new NotImplementedException();
            public Task<IEnumerable<FactureInterrogationRow>> GetFacturesInterrogationAsync(int soId, DateTime dateDebut, DateTime dateFin, IReadOnlyList<string>? numero, string? fournisseur, IReadOnlyList<string>? reference, IReadOnlyList<string>? origines, IReadOnlyList<string>? statuts, int page, int size, string? sort) => throw new NotImplementedException();
            public Task<int> GetFacturesInterrogationCountAsync(int soId, DateTime dateDebut, DateTime dateFin, IReadOnlyList<string>? numero, string? fournisseur, IReadOnlyList<string>? reference, IReadOnlyList<string>? origines, IReadOnlyList<string>? statuts) => throw new NotImplementedException();
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
            public Task<IEnumerable<DeclarationEntete>> GetToutesDeclarationsAsync() => Task.FromResult<IEnumerable<DeclarationEntete>>(new List<DeclarationEntete>());
            public Task<IEnumerable<int>> GetDistinctDtIdsAffectationsAsync() => Task.FromResult<IEnumerable<int>>(new List<int>());
            public Task SetDtIdDeclarationAsync(Guid declarationId, int? dtId) => Task.CompletedTask;
            public Task SaveSelectionReglementsAsync(Guid declarationId, IEnumerable<string> selectedNumeroReglements) => Task.CompletedTask;
            public Task<List<string>> GetSelectionReglementsAsync(Guid declarationId) => Task.FromResult(new List<string>());
            public Task<EcheanceDiagnosticRow?> GetEcheanceDiagnosticAsync(int soId, int ecId) => Task.FromResult<EcheanceDiagnosticRow?>(null);
            public Task<IReadOnlyList<EcheanceCollisionRow>> GetEcheancesMemeDoNumeroAsync(int soId, string doNumero) => Task.FromResult<IReadOnlyList<EcheanceCollisionRow>>(new List<EcheanceCollisionRow>());
            public Task<string?> GetMotifErreurCacheAsync(int soId, int ecId) => Task.FromResult<string?>(null);
            public Task<IReadOnlyDictionary<int, DocumentReglementSageRow>> GetDocumentsReglementSageAsync(string sageConnectionString, IEnumerable<int> ecNos) => Task.FromResult<IReadOnlyDictionary<int, DocumentReglementSageRow>>(new Dictionary<int, DocumentReglementSageRow>());
            public Task<CacheLectureRow?> GetDerniereLectureCacheAsync(int soId, int ecId) => Task.FromResult<CacheLectureRow?>(null);
            public Task<IReadOnlyList<CacheBucketRow>> GetBucketsCacheAsync(int soId, int ecId) => Task.FromResult<IReadOnlyList<CacheBucketRow>>(new List<CacheBucketRow>());
            public Task SupprimerLignesParEcIdAsync(Guid declarationId, int ecId) => Task.CompletedTask;
            public Task<string?> GetIdentifiantFiscalSocieteAsync(int soId) => Task.FromResult<string?>("12345678");
            public Task<IReadOnlyList<CodeActiviteReferentielRow>> GetReferentielCodesActiviteAsync(string? domaine = null) => Task.FromResult<IReadOnlyList<CodeActiviteReferentielRow>>(new List<CodeActiviteReferentielRow>());
            public Task<int?> GetDomaineCodeActiviteAsync(string codeActivite) => Task.FromResult<int?>(2);
            public Task<string?> GetDomaineLigneAsync(Guid ligneId) => Task.FromResult<string?>(null);
            public Task<IReadOnlyList<string>> GetDomainesDistinctsLignesAsync(Guid declarationId, IEnumerable<Guid> ligneIds) => Task.FromResult<IReadOnlyList<string>>(new List<string>());
            public Task UpdateCodeActiviteBulkByIdsAsync(Guid declarationId, IEnumerable<Guid> ligneIds, string codeActivite, string utilisateur) => Task.CompletedTask;
            public Task UpdateCodeActiviteBulkAsync(Guid declarationId, string domaine, string? filter, string codeActivite, string utilisateur) => Task.CompletedTask;
            public Task UpdateCodeActiviteLigneAsync(Guid ligneId, string codeActivite, string utilisateur) => Task.CompletedTask;
        }
    }
}
