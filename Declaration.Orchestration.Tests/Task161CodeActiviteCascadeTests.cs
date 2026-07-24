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
using Declaration.Core.Model;
using Declaration.Selection;

namespace Declaration.Orchestration.Tests
{
    /// <summary>
    /// TASK-161/TASK-179 : cascade de résolution du code activité TVA (surcharge ligne > colonne
    /// Sage CT_APE > vide), gap TASK-155/TASK-160 comblé, cas confirmé PO (une facture, deux
    /// codes activité différents via surcharge manuelle) et stabilité de la persistance après
    /// clôture.
    /// </summary>
    public class Task161CodeActiviteCascadeTests
    {
        private static AffectationCandidate CreerCandidat(
            string numeroFacture, string tiersNumero, string tiersNom, string codeActiviteSage)
        {
            return new AffectationCandidate
            {
                Affectation = new AffectationADeclarer
                {
                    NumeroFacture = numeroFacture,
                    NumeroRapprochement = "MV-" + numeroFacture,
                    Sens = SensAffectation.Achat,
                    Source = SourceAffectation.Decaissement,
                    MontantAffecte = 1200m,
                    Tiers = new TiersInfo { Numero = tiersNumero, Nom = tiersNom, CodeActivite = codeActiviteSage }
                },
                Motif = MotifRejet.Eligible
            };
        }

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

        // ─── Cascade réduite (TASK-179) exercée au travers de MapLignesCandidates ──────────────

        [Fact]
        public void MapLignesCandidates_CodeActiviteSageRenseigne_ResoutViaCT_APE()
        {
            var candidat = CreerCandidat("FA002", tiersNumero: "F9999", tiersNom: "INCONNU", codeActiviteSage: "81");
            var modele = ModeleAvecUneLigne("FA002", 20m);

            var lignes = DeclarationWorkflowService.MapLignesCandidates(
                Guid.NewGuid(), "Decaissement", new[] { candidat }, modele);

            Assert.Equal("81", Assert.Single(lignes).CodeActivite);
        }

        [Fact]
        public void MapLignesCandidates_RienNeResout_CodeActiviteVideNonBloquant()
        {
            var candidat = CreerCandidat("FA003", tiersNumero: "F9999", tiersNom: "INCONNU", codeActiviteSage: "");
            var modele = ModeleAvecUneLigne("FA003", 20m);

            var lignes = DeclarationWorkflowService.MapLignesCandidates(
                Guid.NewGuid(), "Decaissement", new[] { candidat }, modele);

            var ligne = Assert.Single(lignes);
            Assert.Equal("", ligne.CodeActivite);
            // Non bloquant : la ligne reste Proposee, aucune exception, aucun rejet lié au code activité.
            Assert.Equal(EtatLigne.Proposee, ligne.Etat);
        }

        // ─── Cas confirmé PO : une facture, deux lignes de taux différents, une seule modifiée ──

        [Fact]
        public async Task ModifierCodeActiviteLigne_FactureAvecDeuxTaux_NeModifieQueLaLigneCiblee()
        {
            var declarationId = Guid.NewGuid();
            var repo = new FakeDeclarationRepositoryTask161();
            repo.Declarations[declarationId] = new DeclarationEntete
            {
                Id = declarationId, Statut = StatutDeclaration.EnCours, SocieteId = 1,
                Exercice = 2026, Periode = 1, Numero = "TVA1-2026-01"
            };
            var ligneTaux20 = new LigneCandidate { Id = Guid.NewGuid(), DeclarationId = declarationId, Domaine = "Decaissement", NumeroFacture = "FA004", EC_Id = 500, Taux = 20m, CodeActivite = "80" };
            var ligneTaux10 = new LigneCandidate { Id = Guid.NewGuid(), DeclarationId = declarationId, Domaine = "Decaissement", NumeroFacture = "FA004", EC_Id = 500, Taux = 10m, CodeActivite = "80" };
            repo.Lignes.Add(ligneTaux20);
            repo.Lignes.Add(ligneTaux10);

            var workflowService = new DeclarationWorkflowService(
                repo, selectionService: null!, connectionFactory: null!, configuration: null!,
                logger: NullLogger<DeclarationWorkflowService>.Instance);

            // La ligne au taux 10% (ex. mélange achat/vente sur la même facture) est repositionnée
            // manuellement sur une autre activité — l'autre ligne (même EC_Id) doit rester intacte.
            await workflowService.ModifierCodeActiviteLigneAsync(declarationId, ligneTaux10.Id, "93", "jdupont");

            Assert.Equal("80", ligneTaux20.CodeActivite);
            Assert.False(ligneTaux20.CodeActiviteModifieManuellement);
            Assert.Equal("93", ligneTaux10.CodeActivite);
            Assert.True(ligneTaux10.CodeActiviteModifieManuellement);
            Assert.Equal("jdupont", ligneTaux10.CodeActiviteModifiePar);
            Assert.NotNull(ligneTaux10.CodeActiviteModifieLe);
        }

        [Fact]
        public async Task ModifierCodeActiviteLigne_DeclarationCloturee_RefuseLaModification()
        {
            var declarationId = Guid.NewGuid();
            var repo = new FakeDeclarationRepositoryTask161();
            repo.Declarations[declarationId] = new DeclarationEntete
            {
                Id = declarationId, Statut = StatutDeclaration.Cloturee, SocieteId = 1,
                Exercice = 2026, Periode = 1, Numero = "TVA1-2026-01"
            };
            var ligne = new LigneCandidate { Id = Guid.NewGuid(), DeclarationId = declarationId, Domaine = "Decaissement", CodeActivite = "80" };
            repo.Lignes.Add(ligne);

            var workflowService = new DeclarationWorkflowService(
                repo, selectionService: null!, connectionFactory: null!, configuration: null!,
                logger: NullLogger<DeclarationWorkflowService>.Instance);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => workflowService.ModifierCodeActiviteLigneAsync(declarationId, ligne.Id, "93", "jdupont"));

            // Stable : ni la valeur ni la trace ne changent après un rejet.
            Assert.Equal("80", ligne.CodeActivite);
            Assert.False(ligne.CodeActiviteModifieManuellement);
        }

        // ─── TASK-173 correctif (rejet architecte 24/07/2026) : le bulk par LigneIds ne doit
        // jamais écrire sur une ligne n'appartenant pas à la déclaration de l'URL — sinon des
        // IDs d'une autre déclaration (y compris Clôturée) contournent le garde-fou de clôture,
        // qui ne vérifie que la déclaration de l'URL. ──────────────────────────────────────────

        [Fact]
        public async Task ModifierCodeActiviteLignesBulkAsync_LigneIdsDeclarationClotureeMalicieuse_AucuneEcritureNiErreur()
        {
            var declarationEnCoursId = Guid.NewGuid();
            var declarationClotureeId = Guid.NewGuid();
            var repo = new FakeDeclarationRepositoryTask161();
            repo.Declarations[declarationEnCoursId] = new DeclarationEntete
            {
                Id = declarationEnCoursId, Statut = StatutDeclaration.EnCours, SocieteId = 1,
                Exercice = 2026, Periode = 1, Numero = "TVA1-2026-01"
            };
            repo.Declarations[declarationClotureeId] = new DeclarationEntete
            {
                Id = declarationClotureeId, Statut = StatutDeclaration.Cloturee, SocieteId = 1,
                Exercice = 2026, Periode = 1, Numero = "TVA1-2025-12"
            };
            // Ligne appartenant à la déclaration CLÔTURÉE — l'appelant fournit son Id dans
            // LigneIds tout en visant l'URL de la déclaration EN COURS.
            var ligneEtrangere = new LigneCandidate
            {
                Id = Guid.NewGuid(), DeclarationId = declarationClotureeId, Domaine = "Decaissement", CodeActivite = "80"
            };
            repo.Lignes.Add(ligneEtrangere);

            var workflowService = new DeclarationWorkflowService(
                repo, selectionService: null!, connectionFactory: null!, configuration: null!,
                logger: NullLogger<DeclarationWorkflowService>.Instance);

            // Aucune ligne ne correspond (pour cette déclaration) parmi les IDs fournis : le
            // repli est le même que "aucune ligne trouvée", pas une écriture silencieuse.
            await workflowService.ModifierCodeActiviteLignesBulkAsync(
                declarationEnCoursId, new List<Guid> { ligneEtrangere.Id }, domaine: null, filter: null,
                codeActivite: "93", utilisateur: "jdupont");

            Assert.Equal("80", ligneEtrangere.CodeActivite);
            Assert.False(ligneEtrangere.CodeActiviteModifieManuellement);
            Assert.Null(ligneEtrangere.CodeActiviteModifiePar);
        }

        [Fact]
        public async Task ModifierCodeActiviteLignesBulkAsync_SelectionMelangeeDeclarations_NeModifieQueLesLignesDeLUrl()
        {
            var declarationEnCoursId = Guid.NewGuid();
            var declarationAutreId = Guid.NewGuid();
            var repo = new FakeDeclarationRepositoryTask161();
            repo.Declarations[declarationEnCoursId] = new DeclarationEntete
            {
                Id = declarationEnCoursId, Statut = StatutDeclaration.EnCours, SocieteId = 1,
                Exercice = 2026, Periode = 1, Numero = "TVA1-2026-01"
            };
            repo.Declarations[declarationAutreId] = new DeclarationEntete
            {
                Id = declarationAutreId, Statut = StatutDeclaration.Cloturee, SocieteId = 1,
                Exercice = 2025, Periode = 12, Numero = "TVA1-2025-12"
            };
            var ligneAppartenante = new LigneCandidate
            {
                Id = Guid.NewGuid(), DeclarationId = declarationEnCoursId, Domaine = "Decaissement", CodeActivite = "80"
            };
            var ligneEtrangere = new LigneCandidate
            {
                Id = Guid.NewGuid(), DeclarationId = declarationAutreId, Domaine = "Decaissement", CodeActivite = "80"
            };
            repo.Lignes.Add(ligneAppartenante);
            repo.Lignes.Add(ligneEtrangere);

            var workflowService = new DeclarationWorkflowService(
                repo, selectionService: null!, connectionFactory: null!, configuration: null!,
                logger: NullLogger<DeclarationWorkflowService>.Instance);

            await workflowService.ModifierCodeActiviteLignesBulkAsync(
                declarationEnCoursId, new List<Guid> { ligneAppartenante.Id, ligneEtrangere.Id }, domaine: null, filter: null,
                codeActivite: "93", utilisateur: "jdupont");

            Assert.Equal("93", ligneAppartenante.CodeActivite);
            Assert.True(ligneAppartenante.CodeActiviteModifieManuellement);
            // La ligne de l'autre déclaration reste intacte malgré sa présence dans LigneIds.
            Assert.Equal("80", ligneEtrangere.CodeActivite);
            Assert.False(ligneEtrangere.CodeActiviteModifieManuellement);
        }

        // ─── Non-régression TASK-160 : le recap par activité doit refléter des valeurs réelles ──

        [Fact]
        public async Task ConstruireModeleControleAsync_LignesActivitesDifferentes_RecapReelParActivite()
        {
            var declarationId = Guid.NewGuid();
            var repo = new FakeDeclarationRepositoryTask161();
            repo.Declarations[declarationId] = new DeclarationEntete
            {
                Id = declarationId, Statut = StatutDeclaration.EnCours, SocieteId = 1,
                Exercice = 2026, Periode = 1, Numero = "TVA1-2026-01"
            };
            repo.Lignes.Add(new LigneCandidate
            {
                Id = Guid.NewGuid(), DeclarationId = declarationId, Domaine = "Decaissement",
                Etat = EtatLigne.Integree, NumeroFacture = "FA005", Source = "Decaissement",
                HT = 1000m, Taux = 20m, TVA = 200m, TTC = 1200m, CodeActivite = "80"
            });
            repo.Lignes.Add(new LigneCandidate
            {
                Id = Guid.NewGuid(), DeclarationId = declarationId, Domaine = "Decaissement",
                Etat = EtatLigne.Proposee, NumeroFacture = "FA006", Source = "Decaissement",
                HT = 500m, Taux = 10m, TVA = 50m, TTC = 550m, CodeActivite = "" // non résolu, décision PO
            });

            var workflowService = new DeclarationWorkflowService(
                repo, selectionService: null!, connectionFactory: null!, configuration: null!,
                logger: NullLogger<DeclarationWorkflowService>.Instance);

            var modele = await workflowService.ConstruireModeleControleAsync(declarationId);

            Assert.Equal(2, modele.RecapsParActivite.Count);
            var groupe80 = modele.RecapsParActivite.Single(r => r.CodeActivite == "80");
            Assert.Equal(1000m, groupe80.TotalHT);
            var groupeVide = modele.RecapsParActivite.Single(r => r.CodeActivite == "");
            Assert.Equal(500m, groupeVide.TotalHT);
        }

        // ─── Persistance après figeage : le code activité déjà figé reste stable ──

        [Fact]
        public async Task ChargerCandidatesSiNecessaire_LigneDejaFigee_CodeActiviteInchange()
        {
            var declarationId = Guid.NewGuid();
            var repo = new FakeDeclarationRepositoryTask161();
            repo.Declarations[declarationId] = new DeclarationEntete
            {
                Id = declarationId, Statut = StatutDeclaration.EnCours, SocieteId = 1,
                Exercice = 2026, Periode = 1, Numero = "TVA1-2026-01"
            };
            var ligneFigee = new LigneCandidate
            {
                Id = Guid.NewGuid(), DeclarationId = declarationId, Domaine = "Decaissement",
                Etat = EtatLigne.Proposee, NumeroFacture = "FA007", CodeActivite = "80", EC_Id = 0
            };
            repo.Lignes.Add(ligneFigee);

            var workflowService = new DeclarationWorkflowService(
                repo, selectionService: null!, connectionFactory: null!, configuration: null!,
                logger: NullLogger<DeclarationWorkflowService>.Instance);

            await workflowService.ChargerCandidatesSiNecessaireAsync(declarationId, "Decaissement");

            Assert.Equal("80", ligneFigee.CodeActivite);
        }

        /// <summary>Fake minimal dédié TASK-161 — même squelette que les autres FakeDeclarationRepository du dossier.</summary>
        private class FakeDeclarationRepositoryTask161 : IDeclarationRepository
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
            public Task UpdateLigneEtatAsync(Guid ligneId, EtatLigne nouvelEtat) => throw new NotImplementedException();
            public Task UpdateLignesEtatBulkAsync(Guid declarationId, string domaine, string? filter, EtatLigne nouvelEtat) => throw new NotImplementedException();
            public Task UpdateLignesEtatBulkByIdsAsync(IEnumerable<Guid> ligneIds, EtatLigne nouvelEtat) => throw new NotImplementedException();

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
            public Task<CacheLectureRow?> GetDerniereLectureCacheAsync(int soId, int ecId) => throw new NotImplementedException();
            public Task<IReadOnlyList<CacheBucketRow>> GetBucketsCacheAsync(int soId, int ecId) => throw new NotImplementedException();
            public Task SupprimerLignesParEcIdAsync(Guid declarationId, int ecId) => Task.CompletedTask;
            public Task<string?> GetIdentifiantFiscalSocieteAsync(int soId) => Task.FromResult<string?>("12345678");

            public Task<IReadOnlyList<CodeActiviteReferentielRow>> GetReferentielCodesActiviteAsync(string? domaine = null) =>
                Task.FromResult<IReadOnlyList<CodeActiviteReferentielRow>>(new List<CodeActiviteReferentielRow>());

            // TASK-172 §4 : toutes les lignes de ce fake sont Domaine="Decaissement" (2) — tout code
            // activité est considéré compatible (pas de référentiel réel simulé ici, hors périmètre
            // de ce fake dédié à la cascade TASK-161).
            public Task<int?> GetDomaineCodeActiviteAsync(string codeActivite) => Task.FromResult<int?>(2);

            public Task<string?> GetDomaineLigneAsync(Guid ligneId) =>
                Task.FromResult(Lignes.FirstOrDefault(l => l.Id == ligneId)?.Domaine);

            public Task UpdateCodeActiviteLigneAsync(Guid ligneId, string codeActivite, string utilisateur)
            {
                var ligne = Lignes.First(l => l.Id == ligneId);
                ligne.CodeActivite = codeActivite;
                ligne.CodeActiviteModifieManuellement = true;
                ligne.CodeActiviteModifiePar = utilisateur;
                ligne.CodeActiviteModifieLe = DateTime.UtcNow;
                return Task.CompletedTask;
            }

            public Task<IReadOnlyList<string>> GetDomainesDistinctsLignesAsync(Guid declarationId, IEnumerable<Guid> ligneIds)
            {
                var ids = ligneIds.ToHashSet();
                IReadOnlyList<string> distincts = Lignes.Where(l => l.DeclarationId == declarationId && ids.Contains(l.Id)).Select(l => l.Domaine).Distinct().ToList();
                return Task.FromResult(distincts);
            }

            public Task UpdateCodeActiviteBulkByIdsAsync(Guid declarationId, IEnumerable<Guid> ligneIds, string codeActivite, string utilisateur)
            {
                var ids = ligneIds.ToHashSet();
                foreach (var ligne in Lignes.Where(l => l.DeclarationId == declarationId && ids.Contains(l.Id)))
                {
                    ligne.CodeActivite = codeActivite;
                    ligne.CodeActiviteModifieManuellement = true;
                    ligne.CodeActiviteModifiePar = utilisateur;
                    ligne.CodeActiviteModifieLe = DateTime.UtcNow;
                }
                return Task.CompletedTask;
            }

            public Task UpdateCodeActiviteBulkAsync(Guid declarationId, string domaine, string? filter, string codeActivite, string utilisateur)
            {
                foreach (var ligne in Lignes.Where(l => l.DeclarationId == declarationId && l.Domaine == domaine))
                {
                    ligne.CodeActivite = codeActivite;
                    ligne.CodeActiviteModifieManuellement = true;
                    ligne.CodeActiviteModifiePar = utilisateur;
                    ligne.CodeActiviteModifieLe = DateTime.UtcNow;
                }
                return Task.CompletedTask;
            }
        }
    }
}
