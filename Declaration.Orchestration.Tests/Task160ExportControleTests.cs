using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Declaration.API.Controllers;
using Declaration.Application.Entities;
using Declaration.Application.Interfaces;
using Declaration.Application.Services;

namespace Declaration.Orchestration.Tests
{
    // TASK-160 : export Excel de contrôle ad-hoc (règlements sélectionnés + factures à déclarer +
    // détail TVA), disponible dès qu'une déclaration existe (EnCours ou Clôturée) — distinct de
    // ConstruireModeleExportAsync/GenererFichiersExportAsync (TASK-155, réservés à Clôturée).
    public class Task160ExportControleTests
    {
        private static DeclarationEntete NouvelleDeclarationEnCours(string numero, int soId = 1) => new()
        {
            Id = Guid.NewGuid(),
            Numero = numero,
            SocieteId = soId,
            Exercice = 2026,
            Periode = 7,
            Type = TypePeriode.Mensuelle,
            Statut = StatutDeclaration.EnCours
        };

        private static LigneCandidate Ligne(Guid declarationId, EtatLigne etat, string numeroFacture, string source = "Decaissement") => new()
        {
            DeclarationId = declarationId,
            Etat = etat,
            Domaine = "Decaissement",
            NumeroFacture = numeroFacture,
            NumeroRapprochement = "REG001",
            TiersNom = "Fournisseur Test",
            TiersIdentifiantFiscal = "12345678",
            TiersICE = "123456789012345",
            HT = 1000m,
            Taux = 20m,
            TVA = 200m,
            TTC = 1200m,
            Prorata = 100m,
            ModePaiement = "Virement",
            DatePaiement = new DateTime(2026, 7, 15),
            DateFacture = new DateTime(2026, 7, 10),
            Source = source
        };

        private static ReglementRapprochementRow Reglement(string numero, decimal montant, int mvType = 3, int mvPoint = 1) => new()
        {
            MvNumero = numero,
            MvType = mvType,
            MvDomaine = 1,
            MvPoint = mvPoint,
            MvDate = new DateTime(2026, 7, 5),
            MvPointDate = mvPoint == 1 ? new DateTime(2026, 7, 8) : null,
            MvMontant = montant,
            Tiers = "Fournisseur Test",
            // TASK-188 : RT_MOUVEMENT.MV_Piece / MV_Echeance.
            MvPiece = "CB AUTO",
            MvEcheance = new DateTime(2026, 8, 1)
        };

        // ─── ConstruireModeleControleAsync ─────────────────────────────────────

        [Fact]
        public async Task ConstruireModeleControleAsync_DeclarationIntrouvable_ArgumentException()
        {
            var repo = new FakeRepository();
            var service = CreerService(repo);

            await Assert.ThrowsAsync<ArgumentException>(() => service.ConstruireModeleControleAsync(Guid.NewGuid()));
        }

        [Fact]
        public async Task ConstruireModeleControleAsync_FonctionneSurDeclarationEnCours_SansErreur()
        {
            var declarationId = Guid.NewGuid();
            var declaration = NouvelleDeclarationEnCours("TVA1-2026-07");
            declaration.Id = declarationId;

            var repo = new FakeRepository();
            repo.Declarations[declarationId] = declaration;

            var service = CreerService(repo);
            var modele = await service.ConstruireModeleControleAsync(declarationId);

            Assert.NotNull(modele);
            Assert.Equal("TVA1-2026-07", modele.EnTete.Numero);
        }

        [Fact]
        public async Task ConstruireModeleControleAsync_AucunCalculLance_FeuilleFacturesVideSansErreur()
        {
            var declarationId = Guid.NewGuid();
            var declaration = NouvelleDeclarationEnCours("TVA1-2026-07");
            declaration.Id = declarationId;

            var repo = new FakeRepository();
            repo.Declarations[declarationId] = declaration;
            // Aucune ligne, aucune sélection.

            var service = CreerService(repo);
            var modele = await service.ConstruireModeleControleAsync(declarationId);

            Assert.Empty(modele.Lignes);
            Assert.Empty(modele.ReglementsSelectionnes);
            Assert.Empty(modele.RecapsParTaux);
            Assert.Empty(modele.RecapsParActivite);
            Assert.Equal(0m, modele.ControleEquilibre.TotalMontantAffecte);
            Assert.Equal(0m, modele.ControleEquilibre.TotalDeclareTtc);
        }

        [Fact]
        public async Task ConstruireModeleControleAsync_LignesIntegreeEtProposee_InclusesExclueRejetee()
        {
            var declarationId = Guid.NewGuid();
            var declaration = NouvelleDeclarationEnCours("TVA1-2026-07");
            declaration.Id = declarationId;

            var repo = new FakeRepository();
            repo.Declarations[declarationId] = declaration;
            repo.Lignes.Add(Ligne(declarationId, EtatLigne.Integree, "FC-INTEGREE"));
            repo.Lignes.Add(Ligne(declarationId, EtatLigne.Proposee, "FC-PROPOSEE"));
            repo.Lignes.Add(Ligne(declarationId, EtatLigne.Exclue, "FC-EXCLUE"));

            var service = CreerService(repo);
            var modele = await service.ConstruireModeleControleAsync(declarationId);

            Assert.Equal(2, modele.Lignes.Count);
            Assert.Contains(modele.Lignes, l => l.NumeroFacture == "FC-INTEGREE");
            Assert.Contains(modele.Lignes, l => l.NumeroFacture == "FC-PROPOSEE");
            Assert.DoesNotContain(modele.Lignes, l => l.NumeroFacture == "FC-EXCLUE");

            // Détail TVA cohérent avec les 2 lignes retenues (2000 HT / 400 TVA / 2400 TTC).
            var recapTaux = Assert.Single(modele.RecapsParTaux);
            Assert.Equal(20m, recapTaux.Taux);
            Assert.Equal(2000m, recapTaux.TotalHT);
            Assert.Equal(400m, recapTaux.TotalTva);
            Assert.Equal(2400m, recapTaux.TotalTtc);
            Assert.Equal(2000m, modele.ControleEquilibre.TotalMontantAffecte);
            Assert.Equal(2400m, modele.ControleEquilibre.TotalDeclareTtc);
        }

        [Fact]
        public async Task ConstruireModeleControleAsync_ReglementsSelectionnes_JointureSelectionEtRapprochement()
        {
            var declarationId = Guid.NewGuid();
            var declaration = NouvelleDeclarationEnCours("TVA1-2026-07");
            declaration.Id = declarationId;

            var repo = new FakeRepository();
            repo.Declarations[declarationId] = declaration;
            repo.Selection[declarationId] = new List<string> { "REG-001" };
            repo.Reglements.Add(Reglement("REG-001", 1200m));
            repo.Reglements.Add(Reglement("REG-002", 500m)); // pas sélectionné — ne doit pas apparaître

            var service = CreerService(repo);
            var modele = await service.ConstruireModeleControleAsync(declarationId);

            var r = Assert.Single(modele.ReglementsSelectionnes);
            Assert.Equal("REG-001", r.Numero);
            Assert.Equal(1200m, r.Montant);
            Assert.Equal("Fournisseur Test", r.Tiers);
            Assert.Equal("Virement", r.Mode);
            // TASK-180 : EtatPointage reste un statut court (jamais de date concaténée) — la date
            // vit désormais dans son propre champ DateRapprochement.
            Assert.Equal("Rapproché", r.EtatPointage);
            Assert.Equal(new DateTime(2026, 7, 8), r.DateRapprochement);
            // TASK-188 : MvPiece/MvEcheance propagés jusqu'à ReglementSelectionneInfo.
            Assert.Equal("CB AUTO", r.Piece);
            Assert.Equal(new DateTime(2026, 8, 1), r.Echeance);
        }

        [Fact]
        public async Task ConstruireModeleControleAsync_ReglementSelectionneNonRapproche_EtatPointageExplicite()
        {
            var declarationId = Guid.NewGuid();
            var declaration = NouvelleDeclarationEnCours("TVA1-2026-07");
            declaration.Id = declarationId;

            var repo = new FakeRepository();
            repo.Declarations[declarationId] = declaration;
            repo.Selection[declarationId] = new List<string> { "REG-003" };
            repo.Reglements.Add(Reglement("REG-003", 800m, mvType: 3, mvPoint: 0));

            var service = CreerService(repo);
            var modele = await service.ConstruireModeleControleAsync(declarationId);

            var r = Assert.Single(modele.ReglementsSelectionnes);
            Assert.Equal("Non rapproché", r.EtatPointage);
            // TASK-180 : colonne dédiée vide (pas de date) pour un règlement non rapproché.
            Assert.Null(r.DateRapprochement);
        }

        // TASK-188 : MV_Piece vide (~7,5% des lignes réelles, cf. VERIFY) et MV_Echeance NULL —
        // aucune exception, colonnes vides plutôt qu'une valeur inventée.
        [Fact]
        public async Task ConstruireModeleControleAsync_PieceVideEtEcheanceNull_AucuneException()
        {
            var declarationId = Guid.NewGuid();
            var declaration = NouvelleDeclarationEnCours("TVA1-2026-07");
            declaration.Id = declarationId;

            var repo = new FakeRepository();
            repo.Declarations[declarationId] = declaration;
            repo.Selection[declarationId] = new List<string> { "REG-004" };
            var reglementSansPieceNiEcheance = Reglement("REG-004", 300m);
            reglementSansPieceNiEcheance.MvPiece = "";
            reglementSansPieceNiEcheance.MvEcheance = null;
            repo.Reglements.Add(reglementSansPieceNiEcheance);

            var service = CreerService(repo);
            var modele = await service.ConstruireModeleControleAsync(declarationId);

            var r = Assert.Single(modele.ReglementsSelectionnes);
            Assert.Equal("", r.Piece);
            Assert.Null(r.Echeance);
        }

        [Fact]
        public async Task ConstruireModeleControleAsync_RecapsParTauxEtActivite_ClivageCollecteDeductible()
        {
            // TASK-180 : Encaissement -> Collecté, Decaissement -> Déductible, même Taux/CodeActivite
            // des deux côtés (Ligne() fixe Taux=20/HT=1000/TVA=200/TTC=1200 par défaut) — vérifie le
            // clivage ET que la somme Collecté+Déductible reconstitue l'ancien total non scindé.
            var declarationId = Guid.NewGuid();
            var declaration = NouvelleDeclarationEnCours("TVA1-2026-07");
            declaration.Id = declarationId;

            var repo = new FakeRepository();
            repo.Declarations[declarationId] = declaration;
            repo.Lignes.Add(Ligne(declarationId, EtatLigne.Integree, "FC-COLLECTE", source: "Encaissement"));
            repo.Lignes.Add(Ligne(declarationId, EtatLigne.Integree, "FC-DEDUCTIBLE", source: "Decaissement"));

            var service = CreerService(repo);
            var modele = await service.ConstruireModeleControleAsync(declarationId);

            Assert.Equal(2, modele.RecapsParTaux.Count);
            var collecte = Assert.Single(modele.RecapsParTaux, r => r.Collecte);
            var deductible = Assert.Single(modele.RecapsParTaux, r => !r.Collecte);
            Assert.Equal(20m, collecte.Taux);
            Assert.Equal(20m, deductible.Taux);
            // Ancien total non scindé (une seule ligne par facture, TTC=1200 chacune) : 2400.
            Assert.Equal(2400m, collecte.TotalTtc + deductible.TotalTtc);

            Assert.Equal(2, modele.RecapsParActivite.Count);
            Assert.Single(modele.RecapsParActivite, r => r.Collecte);
            Assert.Single(modele.RecapsParActivite, r => !r.Collecte);
        }

        // ─── GenererExcelControleAsync ──────────────────────────────────────────

        [Fact]
        public async Task GenererExcelControleAsync_ProduitUnClasseurValide()
        {
            var declarationId = Guid.NewGuid();
            var declaration = NouvelleDeclarationEnCours("TVA1-2026-07");
            declaration.Id = declarationId;

            var repo = new FakeRepository();
            repo.Declarations[declarationId] = declaration;
            repo.Lignes.Add(Ligne(declarationId, EtatLigne.Proposee, "FC001"));

            var service = CreerService(repo);
            var bytes = await service.GenererExcelControleAsync(declarationId);

            Assert.True(bytes.Length > 0);
            using var ms = new MemoryStream(bytes);
            using var workbook = new XLWorkbook(ms);
            Assert.Equal(3, workbook.Worksheets.Count);
        }

        // ─── Contrôleur : GET {id}/export-controle ─────────────────────────────

        [Fact]
        public async Task ExportControle_DeclarationIntrouvable_NotFound()
        {
            var repo = new FakeRepository();
            var controller = CreerController(repo, admin: true);

            var result = await controller.ExportControle(Guid.NewGuid());

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task ExportControle_SocieteNonAutorisee_Forbid()
        {
            var declarationId = Guid.NewGuid();
            var declaration = NouvelleDeclarationEnCours("TVA1-2026-07", soId: 42);
            declaration.Id = declarationId;

            var repo = new FakeRepository();
            repo.Declarations[declarationId] = declaration;

            var controller = CreerController(repo, admin: false, societesAutorisees: "1");

            var result = await controller.ExportControle(declarationId);

            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task ExportControle_DeclarationEnCours_RetourneFichierXlsx()
        {
            var declarationId = Guid.NewGuid();
            var declaration = NouvelleDeclarationEnCours("TVA1-2026-07");
            declaration.Id = declarationId;

            var repo = new FakeRepository();
            repo.Declarations[declarationId] = declaration;
            repo.Lignes.Add(Ligne(declarationId, EtatLigne.Proposee, "FC001"));

            var controller = CreerController(repo, admin: true);

            var result = await controller.ExportControle(declarationId);

            var file = Assert.IsType<FileContentResult>(result);
            Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file.ContentType);
            Assert.True(file.FileContents.Length > 0);
        }

        // ─── Fixtures ───────────────────────────────────────────────────────────

        private static DeclarationWorkflowService CreerService(FakeRepository repo) =>
            new(repo, selectionService: null!, connectionFactory: null!, configuration: null!, logger: NullLogger<DeclarationWorkflowService>.Instance);

        private static DeclarationsController CreerController(FakeRepository repo, bool admin, string? societesAutorisees = null)
        {
            var service = CreerService(repo);
            var controller = new DeclarationsController(repo, service, connectionFactory: null!);

            var claims = new List<Claim>();
            if (admin) claims.Add(new Claim("UT_Admin", "1"));
            if (societesAutorisees != null) claims.Add(new Claim("Societes", societesAutorisees));
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var user = new ClaimsPrincipal(identity);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
            return controller;
        }

        private class FakeRepository : IDeclarationRepository
        {
            public Dictionary<Guid, DeclarationEntete> Declarations { get; } = new();
            public List<LigneCandidate> Lignes { get; } = new();
            public Dictionary<Guid, List<string>> Selection { get; } = new();
            public List<ReglementRapprochementRow> Reglements { get; } = new();

            public Task<DeclarationEntete?> GetByIdAsync(Guid id) =>
                Task.FromResult(Declarations.TryGetValue(id, out var d) ? d : null);

            public Task<IEnumerable<LigneCandidate>> GetLignesAsync(Guid declarationId, string domaine, int page, int pageSize, string? sort, string? filter) =>
                Task.FromResult(Lignes.Where(l => l.DeclarationId == declarationId && l.Domaine == domaine));

            public Task<List<string>> GetSelectionReglementsAsync(Guid declarationId) =>
                Task.FromResult(Selection.TryGetValue(declarationId, out var s) ? s : new List<string>());

            public Task<IEnumerable<ReglementRapprochementRow>> GetReglementsRapprochementAsync(
                int soId, DateTime dateDebut, DateTime dateFin, RapprochementFilter filter, int page, int size, string? sort) =>
                Task.FromResult(Reglements.AsEnumerable());

            public Task<string?> GetIdentifiantFiscalSocieteAsync(int soId) => throw new NotImplementedException();
            public Task<IReadOnlyList<CodeActiviteReferentielRow>> GetReferentielCodesActiviteAsync(string? domaine = null) => Task.FromResult<IReadOnlyList<CodeActiviteReferentielRow>>(new List<CodeActiviteReferentielRow>());
            public Task<int?> GetDomaineCodeActiviteAsync(string codeActivite) => Task.FromResult<int?>(null);
            public Task<string?> GetDomaineLigneAsync(Guid ligneId) => Task.FromResult<string?>(null);
            public Task<IReadOnlyList<string>> GetDomainesDistinctsLignesAsync(Guid declarationId, IEnumerable<Guid> ligneIds) => Task.FromResult<IReadOnlyList<string>>(new List<string>());
            public Task UpdateCodeActiviteBulkByIdsAsync(Guid declarationId, IEnumerable<Guid> ligneIds, string codeActivite, string utilisateur) => Task.CompletedTask;
            public Task UpdateCodeActiviteBulkAsync(Guid declarationId, string domaine, string? filter, string codeActivite, string utilisateur) => Task.CompletedTask;
            public Task UpdateCodeActiviteLigneAsync(Guid ligneId, string codeActivite, string utilisateur) => Task.CompletedTask;
            public Task<DeclarationEntete?> GetByNumeroAsync(string numero) => throw new NotImplementedException();
            public Task<IEnumerable<DeclarationEntete>> GetAllAsync(int societeId, int? exercice, StatutDeclaration? statut) => throw new NotImplementedException();
            public Task CreateAsync(DeclarationEntete declaration) => throw new NotImplementedException();
            public Task UpdateStatutAsync(Guid id, StatutDeclaration statut) => throw new NotImplementedException();
            public Task<bool> ExistsAsync(int societeId, int exercice, int periode, TypePeriode type) => throw new NotImplementedException();
            public Task DeleteAsync(Guid declarationId) => throw new NotImplementedException();
            public Task<Dictionary<Guid, (int NbLignes, decimal MontantTva)>> GetAgregatsListeAsync(IEnumerable<Guid> declarationIds) => throw new NotImplementedException();
            public Task<int> CountAffectationsTamponneesAsync(IEnumerable<string> numerosRapprochement) => throw new NotImplementedException();
            public Task SaveLignesCandidatesAsync(IEnumerable<LigneCandidate> lignes) => throw new NotImplementedException();
            public Task<int> GetLignesCountAsync(Guid declarationId, string domaine, string? filter) => throw new NotImplementedException();
            public Task<Dictionary<string, List<string>>> GetLignesDistinctsAsync(Guid declarationId, string domaine) => throw new NotImplementedException();
            public Task UpdateLigneEtatAsync(Guid ligneId, EtatLigne nouvelEtat) => throw new NotImplementedException();
            public Task UpdateLignesEtatBulkAsync(Guid declarationId, string domaine, string? filter, EtatLigne nouvelEtat) => throw new NotImplementedException();
            public Task UpdateLignesEtatBulkByIdsAsync(IEnumerable<Guid> ligneIds, EtatLigne nouvelEtat) => throw new NotImplementedException();
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
            public Task TamponnerAffectationsAsync(int dtId, IEnumerable<string> numerosRapprochement) => throw new NotImplementedException();
            public Task DetamponnerAffectationsAsync(int dtId, IEnumerable<string> numerosRapprochement) => throw new NotImplementedException();
            public Task<HashSet<int>> GetEcIdsEnErreurAsync(int soId, IEnumerable<int> ecIds) => throw new NotImplementedException();
            public Task<Dictionary<int, int?>> GetMvPointsActuelsAsync(IEnumerable<int> mvIds) => throw new NotImplementedException();
            public Task UpdateLigneClesAsync(Guid ligneId, int ecId, int mvId) => throw new NotImplementedException();
            public Task ValiderIncoherenceAsync(Guid declarationId, int ecId, string utilisateur) => throw new NotImplementedException();
            public Task ReinitialiserValidationIncoherenceAsync(Guid declarationId, int ecId) => throw new NotImplementedException();
            public Task<Dictionary<string, string>> GetConflitsAutreDeclarationAsync(int societeId, Guid declarationIdExclue) => throw new NotImplementedException();
            public Task DeleteLignesAsync(IEnumerable<Guid> ligneIds) => throw new NotImplementedException();
            public Task<IEnumerable<DeclarationEntete>> GetToutesDeclarationsAsync() => throw new NotImplementedException();
            public Task<IEnumerable<int>> GetDistinctDtIdsAffectationsAsync() => throw new NotImplementedException();
            public Task SetDtIdDeclarationAsync(Guid declarationId, int? dtId) => throw new NotImplementedException();
            public Task SaveSelectionReglementsAsync(Guid declarationId, IEnumerable<string> selectedNumeroReglements) => throw new NotImplementedException();
            public Task<EcheanceDiagnosticRow?> GetEcheanceDiagnosticAsync(int soId, int ecId) => throw new NotImplementedException();
            public Task<IReadOnlyList<EcheanceCollisionRow>> GetEcheancesMemeDoNumeroAsync(int soId, string doNumero) => throw new NotImplementedException();
            public Task<string?> GetMotifErreurCacheAsync(int soId, int ecId) => throw new NotImplementedException();
            public Task<IReadOnlyDictionary<int, DocumentReglementSageRow>> GetDocumentsReglementSageAsync(string sageConnectionString, IEnumerable<int> ecNos) => throw new NotImplementedException();
            public Task<CacheLectureRow?> GetDerniereLectureCacheAsync(int soId, int ecId) => throw new NotImplementedException();
            public Task<IReadOnlyList<CacheBucketRow>> GetBucketsCacheAsync(int soId, int ecId) => throw new NotImplementedException();
            public Task SupprimerLignesParEcIdAsync(Guid declarationId, int ecId) => throw new NotImplementedException();
        }
    }
}
