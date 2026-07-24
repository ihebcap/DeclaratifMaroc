using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
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
    // TASK-155 : câblage réel des endpoints de génération/téléchargement des fichiers de dépôt.
    // Couvre ConstruireModeleExportAsync/GenererFichiersExportAsync (DeclarationWorkflowService)
    // et les deux endpoints du contrôleur (mock repository, aucune vraie base — cf. VERIFY).
    public class Task155GenerationExportTests : IDisposable
    {
        private readonly List<string> _fichiersACreer = new();

        public void Dispose()
        {
            foreach (var f in _fichiersACreer)
            {
                try { if (File.Exists(f)) File.Delete(f); } catch { /* best-effort cleanup */ }
            }
        }

        private static DeclarationEntete NouvelleDeclarationCloturee(string numero, int soId = 1) => new()
        {
            Id = Guid.NewGuid(),
            Numero = numero,
            SocieteId = soId,
            Exercice = 2026,
            Periode = 7,
            Type = TypePeriode.Mensuelle,
            Statut = StatutDeclaration.Cloturee
        };

        private static LigneCandidate LigneIntegree(Guid declarationId, string numeroFacture = "FC001") => new()
        {
            DeclarationId = declarationId,
            Etat = EtatLigne.Integree,
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
            Source = "Decaissement"
        };

        // ─── ConstruireModeleExportAsync ───────────────────────────────────────

        [Fact]
        public async Task ConstruireModeleExportAsync_SeulesLignesIntegreesMappees()
        {
            var declarationId = Guid.NewGuid();
            var declaration = NouvelleDeclarationCloturee("TVA1-2026-07");
            declaration.Id = declarationId;

            var repo = new FakeDeclarationRepository { IdentifiantFiscalSociete = "99999999" };
            repo.Declarations[declarationId] = declaration;
            repo.Lignes.Add(LigneIntegree(declarationId, "FC-INTEGREE"));
            repo.Lignes.Add(new LigneCandidate { DeclarationId = declarationId, Etat = EtatLigne.Proposee, Domaine = "Decaissement", NumeroFacture = "FC-PROPOSEE", Source = "Decaissement" });
            repo.Lignes.Add(new LigneCandidate { DeclarationId = declarationId, Etat = EtatLigne.Exclue, Domaine = "Decaissement", NumeroFacture = "FC-EXCLUE", Source = "Decaissement" });

            var service = CreerService(repo);
            var modele = await service.ConstruireModeleExportAsync(declarationId);

            var ligne = Assert.Single(modele.Lignes);
            Assert.Equal("FC-INTEGREE", ligne.NumeroFacture);
        }

        [Fact]
        public async Task ConstruireModeleExportAsync_MappeChampsEtEnTeteCorrectement()
        {
            var declarationId = Guid.NewGuid();
            var declaration = NouvelleDeclarationCloturee("TVA1-2026-07");
            declaration.Id = declarationId;

            var repo = new FakeDeclarationRepository { IdentifiantFiscalSociete = "99999999" };
            repo.Declarations[declarationId] = declaration;
            repo.Lignes.Add(LigneIntegree(declarationId));

            var service = CreerService(repo);
            var modele = await service.ConstruireModeleExportAsync(declarationId);

            Assert.Equal("99999999", modele.EnTete.IdentifiantSociete);
            Assert.Equal(2026, modele.EnTete.Exercice);
            Assert.Equal("TVA1-2026-07", modele.EnTete.Numero);
            Assert.Equal(Declaration.Core.Model.TypePeriode.Mensuelle, modele.EnTete.Type);
            Assert.Equal(7, modele.EnTete.MoisPeriode);
            Assert.Null(modele.EnTete.TrimestrePeriode);

            var ligne = Assert.Single(modele.Lignes);
            Assert.Equal("FC001", ligne.NumeroFacture);
            Assert.Equal("REG001", ligne.NumeroRapprochement);
            Assert.Equal("", ligne.Designation);
            Assert.Equal("Fournisseur Test", ligne.Tiers.Nom);
            Assert.Equal("12345678", ligne.Tiers.IdentifiantFiscal);
            Assert.Equal("123456789012345", ligne.Tiers.Ice);
            Assert.Equal(1000m, ligne.HT);
            Assert.Equal(20m, ligne.Taux);
            Assert.Equal(200m, ligne.Tva);
            Assert.Equal(1200m, ligne.Ttc);
            Assert.Equal(100m, ligne.Prorata);
            Assert.Equal("Virement", ligne.ModePaiement);
            Assert.Equal(new DateTime(2026, 7, 15), ligne.DatePaiement);
            Assert.Equal(new DateTime(2026, 7, 10), ligne.DateFacture);
            Assert.Equal(Declaration.Core.Model.SourceAffectation.Decaissement, ligne.Source);
            Assert.False(ligne.IsReport);
        }

        [Fact]
        public async Task ConstruireModeleExportAsync_TypeTrimestrielle_TrimestrePeriodeRenseigne()
        {
            var declarationId = Guid.NewGuid();
            var declaration = NouvelleDeclarationCloturee("TVA1-2026-T2");
            declaration.Id = declarationId;
            declaration.Type = TypePeriode.Trimestrielle;
            declaration.Periode = 2;

            var repo = new FakeDeclarationRepository { IdentifiantFiscalSociete = "99999999" };
            repo.Declarations[declarationId] = declaration;
            repo.Lignes.Add(LigneIntegree(declarationId));

            var service = CreerService(repo);
            var modele = await service.ConstruireModeleExportAsync(declarationId);

            Assert.Equal(Declaration.Core.Model.TypePeriode.Trimestrielle, modele.EnTete.Type);
            Assert.Equal(2, modele.EnTete.TrimestrePeriode);
            Assert.Null(modele.EnTete.MoisPeriode);
        }

        [Fact]
        public async Task ConstruireModeleExportAsync_IdentifiantFiscalSocieteAbsent_InvalidOperationException()
        {
            var declarationId = Guid.NewGuid();
            var declaration = NouvelleDeclarationCloturee("TVA1-2026-07");
            declaration.Id = declarationId;

            var repo = new FakeDeclarationRepository { IdentifiantFiscalSociete = null };
            repo.Declarations[declarationId] = declaration;
            repo.Lignes.Add(LigneIntegree(declarationId));

            var service = CreerService(repo);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConstruireModeleExportAsync(declarationId));
            Assert.Contains("SO_Identifiant", ex.Message);
        }

        [Fact]
        public async Task ConstruireModeleExportAsync_DeclarationIntrouvable_ArgumentException()
        {
            var repo = new FakeDeclarationRepository();
            var service = CreerService(repo);

            await Assert.ThrowsAsync<ArgumentException>(() => service.ConstruireModeleExportAsync(Guid.NewGuid()));
        }

        // ─── GenererFichiersExportAsync ────────────────────────────────────────

        [Fact]
        public async Task GenererFichiersExportAsync_DeclarationNonCloturee_InvalidOperationException()
        {
            var declarationId = Guid.NewGuid();
            var declaration = NouvelleDeclarationCloturee("TVA1-2026-07");
            declaration.Id = declarationId;
            declaration.Statut = StatutDeclaration.EnCours;

            var repo = new FakeDeclarationRepository { IdentifiantFiscalSociete = "99999999" };
            repo.Declarations[declarationId] = declaration;

            var service = CreerService(repo);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GenererFichiersExportAsync(declarationId));
            Assert.Contains("Clôturée", ex.Message);
        }

        [Fact]
        public async Task GenererFichiersExportAsync_Succes_CreeXmlEtExcelSurDisque()
        {
            var declarationId = Guid.NewGuid();
            var numero = $"TVA-TEST-{Guid.NewGuid():N}".Substring(0, 20);
            var declaration = NouvelleDeclarationCloturee(numero);
            declaration.Id = declarationId;

            var repo = new FakeDeclarationRepository { IdentifiantFiscalSociete = "99999999" };
            repo.Declarations[declarationId] = declaration;
            repo.Lignes.Add(LigneIntegree(declarationId));

            var service = CreerService(repo);
            var (xmlZipPath, excelPath) = await service.GenererFichiersExportAsync(declarationId);
            _fichiersACreer.Add(xmlZipPath);
            _fichiersACreer.Add(excelPath);
            _fichiersACreer.Add(Path.ChangeExtension(xmlZipPath, ".xml"));

            Assert.True(File.Exists(xmlZipPath), $"XML zip attendu absent : {xmlZipPath}");
            Assert.True(File.Exists(excelPath), $"Excel attendu absent : {excelPath}");
            Assert.StartsWith(DeclarationWorkflowService.ObtenirDossierExports(), xmlZipPath);
        }

        [Fact]
        public async Task GenererFichiersExportAsync_FichierExcelDejaExistant_ApplicationExceptionExisteDeja()
        {
            var declarationId = Guid.NewGuid();
            var numero = $"TVA-TEST-{Guid.NewGuid():N}".Substring(0, 20);
            var declaration = NouvelleDeclarationCloturee(numero);
            declaration.Id = declarationId;

            var repo = new FakeDeclarationRepository { IdentifiantFiscalSociete = "99999999" };
            repo.Declarations[declarationId] = declaration;
            repo.Lignes.Add(LigneIntegree(declarationId));

            var dossier = DeclarationWorkflowService.ObtenirDossierExports();
            Directory.CreateDirectory(dossier);
            var periodeStr = $"M{declaration.Periode}";
            var excelPath = Path.Combine(dossier, $"{numero}-{declaration.Exercice}-{periodeStr}-Checkup.xlsx");
            File.WriteAllText(excelPath, "deja-la");
            _fichiersACreer.Add(excelPath);

            var service = CreerService(repo);

            var ex = await Assert.ThrowsAsync<ApplicationException>(() => service.GenererFichiersExportAsync(declarationId));
            Assert.Contains("existe déjà", ex.Message);
        }

        // ─── Contrôleur : POST {id}/generation / GET {id}/fichiers/{type} ──────

        [Fact]
        public async Task Generation_DeclarationIntrouvable_NotFound()
        {
            var repo = new FakeDeclarationRepository();
            var controller = CreerController(repo, admin: true);

            var result = await controller.Generation(Guid.NewGuid());

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Generation_SocieteNonAutorisee_Forbid()
        {
            var declarationId = Guid.NewGuid();
            var declaration = NouvelleDeclarationCloturee("TVA1-2026-07", soId: 42);
            declaration.Id = declarationId;

            var repo = new FakeDeclarationRepository { IdentifiantFiscalSociete = "99999999" };
            repo.Declarations[declarationId] = declaration;

            // Utilisateur autorisé uniquement pour la société 1, pas 42.
            var controller = CreerController(repo, admin: false, societesAutorisees: "1");

            var result = await controller.Generation(declarationId);

            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task Generation_DeclarationNonCloturee_BadRequest()
        {
            var declarationId = Guid.NewGuid();
            var declaration = NouvelleDeclarationCloturee("TVA1-2026-07");
            declaration.Id = declarationId;
            declaration.Statut = StatutDeclaration.EnCours;

            var repo = new FakeDeclarationRepository { IdentifiantFiscalSociete = "99999999" };
            repo.Declarations[declarationId] = declaration;

            var controller = CreerController(repo, admin: true);

            var result = await controller.Generation(declarationId);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            var message = (string)bad.Value!.GetType().GetProperty("Message")!.GetValue(bad.Value)!;
            Assert.Contains("Clôturée", message);
        }

        [Fact]
        public async Task Generation_IdentifiantFiscalSocieteAbsent_BadRequest()
        {
            var declarationId = Guid.NewGuid();
            var declaration = NouvelleDeclarationCloturee("TVA1-2026-07");
            declaration.Id = declarationId;

            var repo = new FakeDeclarationRepository { IdentifiantFiscalSociete = "  " };
            repo.Declarations[declarationId] = declaration;
            repo.Lignes.Add(LigneIntegree(declarationId));

            var controller = CreerController(repo, admin: true);

            var result = await controller.Generation(declarationId);

            var bad = Assert.IsType<BadRequestObjectResult>(result);
            var message = (string)bad.Value!.GetType().GetProperty("Message")!.GetValue(bad.Value)!;
            Assert.Contains("SO_Identifiant", message);
        }

        [Fact]
        public async Task Generation_Succes_RetourneUrlsFichiersEtDownloadFonctionne()
        {
            var declarationId = Guid.NewGuid();
            var numero = $"TVA-TEST-{Guid.NewGuid():N}".Substring(0, 20);
            var declaration = NouvelleDeclarationCloturee(numero);
            declaration.Id = declarationId;

            var repo = new FakeDeclarationRepository { IdentifiantFiscalSociete = "99999999" };
            repo.Declarations[declarationId] = declaration;
            repo.Lignes.Add(LigneIntegree(declarationId));

            var controller = CreerController(repo, admin: true);

            var result = await controller.Generation(declarationId);

            var ok = Assert.IsType<OkObjectResult>(result);
            var fichiers = ok.Value!.GetType().GetProperty("fichiers")!.GetValue(ok.Value)!;
            var xmlUrl = (string)fichiers.GetType().GetProperty("xmlDecaissement")!.GetValue(fichiers)!;
            var excelUrl = (string)fichiers.GetType().GetProperty("excelCheckup")!.GetValue(fichiers)!;
            Assert.Equal($"/declarations/{declarationId}/fichiers/xml", xmlUrl);
            Assert.Equal($"/declarations/{declarationId}/fichiers/excel", excelUrl);

            // Nettoyage : retrouve les fichiers réellement écrits sur disque pour les supprimer.
            var dossier = DeclarationWorkflowService.ObtenirDossierExports();
            var periodeStr = $"M{declaration.Periode}";
            var baseFileName = $"{numero}-{declaration.Exercice}-{periodeStr}";
            _fichiersACreer.Add(Path.Combine(dossier, $"{baseFileName}.zip"));
            _fichiersACreer.Add(Path.Combine(dossier, $"{baseFileName}.xml"));
            _fichiersACreer.Add(Path.Combine(dossier, $"{baseFileName}-Checkup.xlsx"));

            // Enchaîne sur le téléchargement réel (même contrôleur, même déclaration).
            var xmlDownload = await controller.DownloadFichier(declarationId, "xml");
            var physicalXml = Assert.IsType<PhysicalFileResult>(xmlDownload);
            Assert.Equal("application/zip", physicalXml.ContentType);
            Assert.True(File.Exists(physicalXml.FileName));

            var excelDownload = await controller.DownloadFichier(declarationId, "excel");
            var physicalExcel = Assert.IsType<PhysicalFileResult>(excelDownload);
            Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", physicalExcel.ContentType);
            Assert.True(File.Exists(physicalExcel.FileName));
        }

        [Fact]
        public async Task DownloadFichier_TypeInconnu_BadRequest()
        {
            var declarationId = Guid.NewGuid();
            var declaration = NouvelleDeclarationCloturee("TVA1-2026-07");
            declaration.Id = declarationId;

            var repo = new FakeDeclarationRepository { IdentifiantFiscalSociete = "99999999" };
            repo.Declarations[declarationId] = declaration;

            var controller = CreerController(repo, admin: true);

            var result = await controller.DownloadFichier(declarationId, "pdf");

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task DownloadFichier_FichierNonGenere_NotFound()
        {
            var declarationId = Guid.NewGuid();
            var declaration = NouvelleDeclarationCloturee($"TVA-JAMAIS-{Guid.NewGuid():N}".Substring(0, 20));
            declaration.Id = declarationId;

            var repo = new FakeDeclarationRepository { IdentifiantFiscalSociete = "99999999" };
            repo.Declarations[declarationId] = declaration;

            var controller = CreerController(repo, admin: true);

            var result = await controller.DownloadFichier(declarationId, "xml");

            Assert.IsType<NotFoundObjectResult>(result);
        }

        // ─── Fixtures ───────────────────────────────────────────────────────────

        private static DeclarationWorkflowService CreerService(FakeDeclarationRepository repo) =>
            new(repo, selectionService: null!, connectionFactory: null!, configuration: null!, logger: NullLogger<DeclarationWorkflowService>.Instance);

        private static DeclarationsController CreerController(FakeDeclarationRepository repo, bool admin, string? societesAutorisees = null)
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

        private class FakeDeclarationRepository : IDeclarationRepository
        {
            public Dictionary<Guid, DeclarationEntete> Declarations { get; } = new();
            public List<LigneCandidate> Lignes { get; } = new();
            public string? IdentifiantFiscalSociete { get; set; }

            public Task<DeclarationEntete?> GetByIdAsync(Guid id) =>
                Task.FromResult(Declarations.TryGetValue(id, out var d) ? d : null);

            public Task<string?> GetIdentifiantFiscalSocieteAsync(int soId) => Task.FromResult(IdentifiantFiscalSociete);
            public Task<IReadOnlyList<CodeActiviteReferentielRow>> GetReferentielCodesActiviteAsync(string? domaine = null) => Task.FromResult<IReadOnlyList<CodeActiviteReferentielRow>>(new List<CodeActiviteReferentielRow>());
            public Task<int?> GetDomaineCodeActiviteAsync(string codeActivite) => Task.FromResult<int?>(null);
            public Task<string?> GetDomaineLigneAsync(Guid ligneId) => Task.FromResult<string?>(null);
            public Task<IReadOnlyList<string>> GetDomainesDistinctsLignesAsync(Guid declarationId, IEnumerable<Guid> ligneIds) => Task.FromResult<IReadOnlyList<string>>(new List<string>());
            public Task UpdateCodeActiviteBulkByIdsAsync(Guid declarationId, IEnumerable<Guid> ligneIds, string codeActivite, string utilisateur) => Task.CompletedTask;
            public Task UpdateCodeActiviteBulkAsync(Guid declarationId, string domaine, string? filter, string codeActivite, string utilisateur) => Task.CompletedTask;
            public Task UpdateCodeActiviteLigneAsync(Guid ligneId, string codeActivite, string utilisateur) => Task.CompletedTask;

            public Task<IEnumerable<LigneCandidate>> GetLignesAsync(Guid declarationId, string domaine, int page, int pageSize, string? sort, string? filter) =>
                Task.FromResult(Lignes.Where(l => l.DeclarationId == declarationId && l.Domaine == domaine));

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
            public Task<List<string>> GetSelectionReglementsAsync(Guid declarationId) => Task.FromResult(new List<string>());
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
