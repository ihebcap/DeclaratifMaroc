using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Declaration.Application.Entities;
using Declaration.Application.Interfaces;
using Declaration.Application.Services;
using Declaration.Core;
using Declaration.Core.Model;
using Xunit;

namespace Declaration.Orchestration.Tests;

/// <summary>
/// TASK-133 : orchestration de la génération du fichier XML/ZIP Délai de Paiement — HORS BASE
/// (repository/sélection en mémoire), sur la chaîne RÉELLE
/// <see cref="DeclarationDelaiPaiementService"/> (TASK-132) + <see cref="DeclarationDelaiPaiementGenerationService"/>
/// (TASK-133). Le but n'est PAS de retester le calcul par ligne (couvert dans
/// <c>Declaration.Core.Tests</c>) ni la structure XML (couverte dans
/// <c>Declaration.Export.Xml.Tests</c>) mais l'ENCHAÎNEMENT : le contrôle IF/ICE est appelé AVANT
/// toute écriture, le flag n'est posé qu'APRÈS écriture réussie, et l'annulation nettoie le disque.
/// </summary>
public class Task133GenerationFichierDelaiPaiementTests : IDisposable
{
    private const int SoId = 1;
    private const int UtId = 7;
    private readonly string _dossierExports;

    public Task133GenerationFichierDelaiPaiementTests()
    {
        // Isole chaque run dans un dossier temporaire dédié — jamais le dossier "exports" réel de
        // DeclarationWorkflowService.ObtenirDossierExports() (AppContext.BaseDirectory).
        _dossierExports = Path.Combine(Path.GetTempPath(), "task133-" + Guid.NewGuid());
        Directory.CreateDirectory(_dossierExports);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dossierExports)) Directory.Delete(_dossierExports, true);
    }

    [Fact]
    public async Task GenererFichierAsync_DeclarationNonCloturee_LeveAvantEcriture_AucunFichier()
    {
        var (generation, repository, _) = ConstruireChaine();
        var ddpId = await CreerDeclarationAvecUneLigne(repository, integrer: false);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => generation.GenererFichierAsync(ddpId, UtId));

        Assert.Equal("La déclaration n'est pas clôturée.", ex.Message);
        Assert.Empty(Directory.GetFiles(_dossierExports));
    }

    [Fact]
    public async Task GenererFichierAsync_IfIceInvalide_LeveAvantEcriture_AucunFichier_FlagResteFaux()
    {
        var (generation, repository, service) = ConstruireChaine();
        var ddpId = await CreerDeclarationAvecUneLigne(repository, integrer: true);
        await service.CloturerAsync(ddpId, UtId);

        repository.IdentitesErp["F001"] = new IdentiteFiscaleTiersErp { TiersCode = "F001", IdentifiantFiscal = null, Ice = "001234567890123" };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => generation.GenererFichierAsync(ddpId, UtId));

        Assert.Contains("identité fiscale invalide", ex.Message);
        Assert.Empty(Directory.GetFiles(_dossierExports));
        Assert.False((await service.GetAsync(ddpId))!.FichierGenere);
    }

    [Fact]
    public async Task GenererFichierAsync_Succes_EcritXmlEtZip_PoseLeFlag()
    {
        var (generation, repository, service) = ConstruireChaine();
        var ddpId = await CreerDeclarationAvecUneLigne(repository, integrer: true);
        await service.CloturerAsync(ddpId, UtId);
        RendreIdentiteConforme(repository);

        var zipPath = await generation.GenererFichierAsync(ddpId, UtId);

        Assert.True(File.Exists(zipPath));
        var numero = (await service.GetAsync(ddpId))!.Numero;
        var xmlPath = Path.Combine(_dossierExports, $"{numero}-2026-T1.xml");
        Assert.True(File.Exists(xmlPath));

        var xml = File.ReadAllText(xmlPath);
        Assert.Contains("<identifiantFiscal>123456</identifiantFiscal>", xml); // IF société
        Assert.Contains("<numFacture>FAC001</numFacture>", xml);
        Assert.Contains("<identifiantFiscal>12345678</identifiantFiscal>", xml); // IF fournisseur (même nom de tag, ligne)

        Assert.True((await service.GetAsync(ddpId))!.FichierGenere);
    }

    [Fact]
    public async Task GenererFichierAsync_SansConfigMarchandise_NatureVideEtDateLivraisonEgaleDateEmission_NonRegression()
    {
        // TASK-191, cas 1/3 : AUCUNE société configurée (ValeursMarchandise reste vide) — le
        // comportement legacy/TASK-133 (natureMarchandise vide, dateLivraisonMarchandise = dateEmission)
        // doit rester STRICTEMENT inchangé.
        var (generation, repository, service) = ConstruireChaine();
        var ddpId = await CreerDeclarationAvecUneLigne(repository, integrer: true);
        await service.CloturerAsync(ddpId, UtId);
        RendreIdentiteConforme(repository);

        var zipPath = await generation.GenererFichierAsync(ddpId, UtId);
        Assert.True(File.Exists(zipPath));
        var numero = (await service.GetAsync(ddpId))!.Numero;
        var xml = File.ReadAllText(Path.Combine(_dossierExports, $"{numero}-2026-T1.xml"));

        Assert.Contains("<natureMarchandise></natureMarchandise>", xml);
        Assert.Contains("<dateLivraisonMarchandise>2025-12-01</dateLivraisonMarchandise>", xml); // = dateEmission (DoDate de la ligne)
    }

    [Fact]
    public async Task GenererFichierAsync_ConfigPresenteMaisValeurAbsenteSurDocument_MemeRepliQuAvant()
    {
        // TASK-191, cas 2/3 : société configurée (colonnes désignées) mais le document Sage n'a pas de
        // valeur exploitable (dictionnaire renvoie une entrée avec des champs null) — le repli doit
        // rester identique à aujourd'hui (natureMarchandise vide, dateLivraisonMarchandise = dateEmission),
        // jamais une exception liée à une valeur absente.
        var (generation, repository, service) = ConstruireChaine();
        var ddpId = await CreerDeclarationAvecUneLigne(repository, integrer: true);
        await service.CloturerAsync(ddpId, UtId);
        RendreIdentiteConforme(repository);

        repository.ValeursMarchandise["FAC001"] = new ValeursMarchandiseErp
        {
            NatureMarchandise = null,
            DateLivraisonMarchandise = null
        };

        var zipPath = await generation.GenererFichierAsync(ddpId, UtId);
        Assert.True(File.Exists(zipPath));
        var numero = (await service.GetAsync(ddpId))!.Numero;
        var xml = File.ReadAllText(Path.Combine(_dossierExports, $"{numero}-2026-T1.xml"));

        Assert.Contains("<natureMarchandise></natureMarchandise>", xml);
        Assert.Contains("<dateLivraisonMarchandise>2025-12-01</dateLivraisonMarchandise>", xml);
    }

    [Fact]
    public async Task GenererFichierAsync_ConfigEtValeurReellesPresentes_XmlRefleteLaValeurReelle()
    {
        // TASK-191, cas 3/3 : société configurée ET valeur réelle présente sur le document Sage — le
        // XML doit refléter la VRAIE valeur, pas le repli.
        var (generation, repository, service) = ConstruireChaine();
        var ddpId = await CreerDeclarationAvecUneLigne(repository, integrer: true);
        await service.CloturerAsync(ddpId, UtId);
        RendreIdentiteConforme(repository);

        repository.ValeursMarchandise["FAC001"] = new ValeursMarchandiseErp
        {
            NatureMarchandise = "Materiel informatique",
            DateLivraisonMarchandise = new DateTime(2025, 12, 20)
        };

        var zipPath = await generation.GenererFichierAsync(ddpId, UtId);
        Assert.True(File.Exists(zipPath));
        var numero = (await service.GetAsync(ddpId))!.Numero;
        var xml = File.ReadAllText(Path.Combine(_dossierExports, $"{numero}-2026-T1.xml"));

        Assert.Contains("<natureMarchandise>Materiel informatique</natureMarchandise>", xml);
        Assert.Contains("<dateLivraisonMarchandise>2025-12-20</dateLivraisonMarchandise>", xml);
        Assert.DoesNotContain("<dateLivraisonMarchandise>2025-12-01</dateLivraisonMarchandise>", xml); // pas le fallback dateEmission
    }

    [Fact]
    public async Task GenererFichierAsync_FichierDejaExistant_Leve_FlagResteFaux()
    {
        var (generation, repository, service) = ConstruireChaine();
        var ddpId = await CreerDeclarationAvecUneLigne(repository, integrer: true);
        await service.CloturerAsync(ddpId, UtId);
        RendreIdentiteConforme(repository);

        var numero = (await service.GetAsync(ddpId))!.Numero;
        File.WriteAllText(Path.Combine(_dossierExports, $"{numero}-2026-T1.xml"), "dummy");

        var ex = await Assert.ThrowsAsync<ApplicationException>(() => generation.GenererFichierAsync(ddpId, UtId));

        Assert.Contains("existe déja", ex.Message);
        Assert.False((await service.GetAsync(ddpId))!.FichierGenere);
    }

    [Fact]
    public async Task GenererFichierAsync_MarquageEchoueApresEcriture_SupprimeLesFichiersEcrits()
    {
        var (generation, repository, service) = ConstruireChaine();
        var ddpId = await CreerDeclarationAvecUneLigne(repository, integrer: true);
        await service.CloturerAsync(ddpId, UtId);
        RendreIdentiteConforme(repository);

        // Simule un échec de la MISE À JOUR finale (ex. coupure DB), APRÈS l'écriture réussie du fichier.
        repository.SimulerEchecMiseAJourEtat = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => generation.GenererFichierAsync(ddpId, UtId));

        // Aucun fichier orphelin : la régénération doit rester possible (amélioration assumée §10.4).
        Assert.Empty(Directory.GetFiles(_dossierExports));
        Assert.False((await service.GetAsync(ddpId))!.FichierGenere);
    }

    [Fact]
    public async Task AnnulerGenerationFichierAsync_SupprimeLesFichiersPhysiques()
    {
        var (generation, repository, service) = ConstruireChaine();
        var ddpId = await CreerDeclarationAvecUneLigne(repository, integrer: true);
        await service.CloturerAsync(ddpId, UtId);
        RendreIdentiteConforme(repository);

        await generation.GenererFichierAsync(ddpId, UtId);
        Assert.NotEmpty(Directory.GetFiles(_dossierExports));

        await generation.AnnulerGenerationFichierAsync(ddpId, UtId);

        Assert.Empty(Directory.GetFiles(_dossierExports));
        Assert.False((await service.GetAsync(ddpId))!.FichierGenere);

        // Régénération immédiatement possible (plus de garde "fichier déjà existant" bloquante).
        var zipPath = await generation.GenererFichierAsync(ddpId, UtId);
        Assert.True(File.Exists(zipPath));
    }

    [Fact]
    public async Task AnnulerGenerationFichierAsync_FichiersDejaAbsents_Idempotent()
    {
        var (generation, repository, service) = ConstruireChaine();
        var ddpId = await CreerDeclarationAvecUneLigne(repository, integrer: true);
        await service.CloturerAsync(ddpId, UtId);
        RendreIdentiteConforme(repository);

        await generation.GenererFichierAsync(ddpId, UtId);
        // Suppression manuelle en amont (simulateur d'un fichier déjà nettoyé par ailleurs).
        foreach (var f in Directory.GetFiles(_dossierExports)) File.Delete(f);

        await generation.AnnulerGenerationFichierAsync(ddpId, UtId); // ne doit pas lever.
        Assert.False((await service.GetAsync(ddpId))!.FichierGenere);
    }

    [Fact]
    public async Task ObtenirCheminsFichiersAsync_CheminsDeterministes_AvantGeneration()
    {
        var (generation, repository, service) = ConstruireChaine();
        var ddpId = await CreerDeclarationAvecUneLigne(repository, integrer: true);
        var numero = (await service.GetAsync(ddpId))!.Numero;

        var (xmlPath, zipPath) = await generation.ObtenirCheminsFichiersAsync(ddpId);

        Assert.Equal(Path.Combine(_dossierExports, $"{numero}-2026-T1.xml"), xmlPath);
        Assert.Equal(Path.Combine(_dossierExports, $"{numero}-2026-T1.zip"), zipPath);
        Assert.False(File.Exists(xmlPath)); // pas encore généré
    }

    // ─── Construction de la chaîne réelle sur repository/sélection en mémoire ────────────────────

    private (IDeclarationDelaiPaiementGenerationService Generation, FakeRepository Repository, IDeclarationDelaiPaiementService Service) ConstruireChaine()
    {
        var repository = new FakeRepository();
        var selection = new FakeSelection();
        var service = new DeclarationDelaiPaiementService(repository, selection);
        // Override réservé aux tests (cf. ctor DeclarationDelaiPaiementGenerationService) : isole les
        // fichiers dans un répertoire temporaire jetable, jamais AppContext.BaseDirectory.
        var generation = new DeclarationDelaiPaiementGenerationService(service, repository, _dossierExports);
        return (generation, repository, service);
    }

    private static async Task<int> CreerDeclarationAvecUneLigne(FakeRepository repository, bool integrer)
    {
        var selection = new FakeSelection();
        var service = new DeclarationDelaiPaiementService(repository, selection);

        var ddpId = await service.CreerAsync(new CreerDeclarationDelaiPaiementRequest
        {
            SocieteId = SoId,
            Exercice = 2026,
            Type = TypeDeclarationDelaiPaiement.Trimestrielle,
            Trimestre = TrimestreDelaiPaiement.T1,
            Date = new DateTime(2026, 7, 28),
            UtilisateurId = UtId
        });

        repository.SocieteInfo = new SocieteDelaiPaiementInfo
        {
            IdentifiantFiscal = "123456",
            ActiviteMarrocCode = 1,
            ChiffreAffaire = 1000000m
        };
        repository.TypesModeReglement[4] = TypeModeReglementDelaiPaiement.Virement;

        repository.DetailsParEcId[100] = new LigneDeclarationDelaiPaiement
        {
            DoNumero = "FAC001",
            DoDate = new DateTime(2025, 12, 1),
            MontantEcheance = 5000m,
            SoldeEcheance = 5000m,
            MontantAffecte = null,
            ReglementRapproche = null,
            ReglementDateRapprochement = null,
            ReglementModeId = 4,
            ReglementPiece = null
        };

        if (!integrer) return ddpId;

        selection.Candidates.Add(new LigneSelectionDelaiPaiement
        {
            EcId = 100,
            AfId = null,
            Statut = StatutLigneDelaiPaiement.Candidate,
            Depassement = 15,
            EcheanceLegale = new DateTime(2026, 1, 15),
            TiersNo = 100,
            TiersCode = "F001",
            TiersIntitule = "FOURNISSEUR TEST"
        });

        await service.IntegrerLignesAsync(ddpId, UtId);
        return ddpId;
    }

    private static void RendreIdentiteConforme(FakeRepository repository)
    {
        repository.IdentitesErp["F001"] = new IdentiteFiscaleTiersErp
        {
            TiersCode = "F001",
            IdentifiantFiscal = "12345678",
            Ice = "001234567890123",
            NumRc = "RC-100",
            Adresse = "12 rue Test, Rabat"
        };
    }

    // ─── Repository/sélection en mémoire ──────────────────────────────────────────────────────────

    private sealed class FakeSelection : ISelectionDelaiPaiementService
    {
        public List<LigneSelectionDelaiPaiement> Candidates { get; } = new();
        public DateTime? DateMiseEnRoute { get; init; } = new DateTime(2020, 1, 1);

        public Task<ResultatSelectionDelaiPaiement> SelectionnerAsync(int soId, DateTime dateDebutPeriode, DateTime dateFinPeriode)
            => Task.FromResult(new ResultatSelectionDelaiPaiement
            {
                DateDebutPeriode = dateDebutPeriode,
                DateFinPeriode = dateFinPeriode,
                DateMiseEnRouteSociete = DateMiseEnRoute,
                Lignes = Candidates.ToList(),
                NombreEcheancesExaminees = Candidates.Count
            });
    }

    private sealed class FakeRepository : IDeclarationDelaiPaiementRepository
    {
        public List<DeclarationDelaiPaiement> Entetes { get; } = new();
        public List<LigneStockee> Lignes { get; } = new();
        public Dictionary<string, IdentiteFiscaleTiersErp> IdentitesErp { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// TASK-191 : simule le résultat de <c>GetValeursMarchandiseAsync</c> — vide par défaut (aucune
        /// société configurée, comportement actuel inchangé), une entrée AVEC des champs null simule
        /// « configurée mais valeur absente sur le document », une entrée avec des valeurs réelles
        /// simule le câblage effectif. Indexé par numéro de facture, comme le contrat réel.
        /// </summary>
        public Dictionary<string, ValeursMarchandiseErp> ValeursMarchandise { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<int, LigneDeclarationDelaiPaiement> DetailsParEcId { get; } = new();
        public Dictionary<int, TypeModeReglementDelaiPaiement> TypesModeReglement { get; } = new();
        public SocieteDelaiPaiementInfo? SocieteInfo { get; set; }
        public bool SimulerEchecMiseAJourEtat { get; set; }

        private int _prochainDdpId = 1;
        private int _prochainDdplId = 1;

        public sealed class LigneStockee
        {
            public int DdplId { get; set; }
            public int DdpId { get; set; }
            public int EcId { get; set; }
            public int? AfId { get; set; }
            public decimal Depassement { get; set; }
            public DateTime EcheanceLegale { get; set; }
            public string TiersCode { get; set; } = "";
            public int TiersNo { get; set; }
            public string TiersIntitule { get; set; } = "";
        }

        public Task<int> CreerEnteteAsync(DeclarationDelaiPaiement entete)
        {
            var ddpId = _prochainDdpId++;
            Entetes.Add(Cloner(entete, ddpId));
            return Task.FromResult(ddpId);
        }

        public Task<DeclarationDelaiPaiement?> GetEnteteAsync(int ddpId) => Task.FromResult(Entetes.FirstOrDefault(e => e.DdpId == ddpId));

        public Task<IReadOnlyList<DeclarationDelaiPaiementListItem>> GetAllAsync(int soId)
            => Task.FromResult<IReadOnlyList<DeclarationDelaiPaiementListItem>>(
                Entetes.Where(e => e.SocieteId == soId)
                    .Select(e => new DeclarationDelaiPaiementListItem { Entete = e, NombreLignes = Lignes.Count(l => l.DdpId == e.DdpId) })
                    .ToList());

        public Task<IReadOnlyList<DeclarationDelaiPaiement>> GetAllParExerciceAsync(int soId, int exercice)
            => Task.FromResult<IReadOnlyList<DeclarationDelaiPaiement>>(Entetes.Where(e => e.SocieteId == soId && e.Exercice == exercice).ToList());

        public Task<bool> ExisteNumeroAsync(int soId, string numero) => Task.FromResult(Entetes.Any(e => e.SocieteId == soId && e.Numero == numero));

        public Task<string?> GetDernierNumeroAsync(int soId, string patternLike)
        {
            var racine = patternLike.Contains('[') ? patternLike.Substring(0, patternLike.IndexOf('[')) : patternLike;
            var chiffres = (patternLike.Length - racine.Length) / 5;
            var numeros = Entetes
                .Where(e => e.SocieteId == soId && e.Numero.Length == racine.Length + chiffres
                            && e.Numero.StartsWith(racine, StringComparison.Ordinal)
                            && e.Numero.Substring(racine.Length).All(char.IsDigit))
                .Select(e => e.Numero).ToList();
            return Task.FromResult(numeros.Count == 0 ? null : numeros.Max());
        }

        public Task<ConfigurationNumerotationDelaiPaiement> GetConfigurationNumerotationAsync(int soId)
            => Task.FromResult(new ConfigurationNumerotationDelaiPaiement { Prefixe = "DDP", InclureAnnee = true, InclureMois = true, NombreChiffres = 4 });

        public Task MettreAJourEtatAsync(int ddpId, StatutDeclarationDelaiPaiement statut, bool estDeposee, bool fichierGenere, string? libelle, int modificateurId, DateTime dateModification)
        {
            if (SimulerEchecMiseAJourEtat && fichierGenere)
                throw new InvalidOperationException("Simulation : échec DB pendant la mise à jour finale.");

            var index = Entetes.FindIndex(e => e.DdpId == ddpId);
            if (index < 0) throw new InvalidOperationException($"Mise à jour impossible : DDP_Id={ddpId} introuvable.");
            var actuel = Entetes[index];
            Entetes[index] = new DeclarationDelaiPaiement
            {
                DdpId = actuel.DdpId, Numero = actuel.Numero, SocieteId = actuel.SocieteId, Date = actuel.Date,
                Exercice = actuel.Exercice, Type = actuel.Type, DateDebut = actuel.DateDebut, DateFin = actuel.DateFin,
                DateCreation = actuel.DateCreation, CreateurId = actuel.CreateurId, Periode = actuel.Periode,
                Statut = statut, EstDeposee = estDeposee, FichierGenere = fichierGenere, Libelle = libelle,
                ModificateurId = modificateurId, DateModification = dateModification
            };
            return Task.CompletedTask;
        }

        public Task SupprimerEnteteAsync(int ddpId)
        {
            if (Lignes.Any(l => l.DdpId == ddpId)) throw new InvalidOperationException("La déclaration contient des lignes.");
            Entetes.RemoveAll(e => e.DdpId == ddpId);
            return Task.CompletedTask;
        }

        public Task<int> CompterLignesAsync(int ddpId) => Task.FromResult(Lignes.Count(l => l.DdpId == ddpId));

        public Task<IReadOnlyList<CleLigneDelaiPaiement>> GetClesLignesAsync(int ddpId)
            => Task.FromResult<IReadOnlyList<CleLigneDelaiPaiement>>(Lignes.Where(l => l.DdpId == ddpId).Select(l => new CleLigneDelaiPaiement(l.EcId, l.AfId)).ToList());

        public Task<int> AjouterLignesAsync(int ddpId, IReadOnlyCollection<LigneAIntegrerDelaiPaiement> lignes)
        {
            foreach (var ligne in lignes)
            {
                var detail = DetailsParEcId.TryGetValue(ligne.EcId, out var d) ? d : null;
                Lignes.Add(new LigneStockee
                {
                    DdplId = _prochainDdplId++,
                    DdpId = ddpId,
                    EcId = ligne.EcId,
                    AfId = ligne.AfId,
                    Depassement = ligne.Depassement,
                    EcheanceLegale = ligne.EcheanceLegale,
                    TiersCode = "F001",
                    TiersNo = 100,
                    TiersIntitule = "FOURNISSEUR TEST"
                });
            }
            return Task.FromResult(lignes.Count);
        }

        public Task<IReadOnlyList<LigneDeclarationDelaiPaiement>> GetLignesAsync(int ddpId)
            => Task.FromResult<IReadOnlyList<LigneDeclarationDelaiPaiement>>(Lignes.Where(l => l.DdpId == ddpId).Select(l =>
            {
                var detail = DetailsParEcId.TryGetValue(l.EcId, out var d) ? d : new LigneDeclarationDelaiPaiement();
                return new LigneDeclarationDelaiPaiement
                {
                    DdplId = l.DdplId,
                    DdpId = l.DdpId,
                    EcId = l.EcId,
                    AfId = l.AfId,
                    Depassement = l.Depassement,
                    EcheanceLegale = l.EcheanceLegale,
                    TiersNo = l.TiersNo,
                    TiersCode = l.TiersCode,
                    TiersIntitule = l.TiersIntitule,
                    DoNumero = detail.DoNumero,
                    DoDate = detail.DoDate,
                    MontantEcheance = detail.MontantEcheance,
                    SoldeEcheance = detail.SoldeEcheance,
                    MontantAffecte = detail.MontantAffecte,
                    ReglementRapproche = detail.ReglementRapproche,
                    ReglementDateRapprochement = detail.ReglementDateRapprochement,
                    ReglementModeId = detail.ReglementModeId,
                    ReglementPiece = detail.ReglementPiece
                };
            }).ToList());

        public Task<int> SupprimerLigneAsync(int ddpId, int ddplId) => Task.FromResult(Lignes.RemoveAll(l => l.DdpId == ddpId && l.DdplId == ddplId));

        public Task<IReadOnlyDictionary<string, IdentiteFiscaleTiersErp>> GetIdentitesFiscalesTiersAsync(int soId, IReadOnlyCollection<string> tiersCodes)
        {
            var resultat = new Dictionary<string, IdentiteFiscaleTiersErp>(StringComparer.OrdinalIgnoreCase);
            foreach (var code in tiersCodes)
                if (IdentitesErp.TryGetValue(code, out var identite)) resultat[code] = identite;
            return Task.FromResult<IReadOnlyDictionary<string, IdentiteFiscaleTiersErp>>(resultat);
        }

        public Task<SocieteDelaiPaiementInfo> GetSocieteInfoAsync(int soId)
            => Task.FromResult(SocieteInfo ?? throw new InvalidOperationException($"Société SO_Id={soId} introuvable."));

        public Task<IReadOnlyDictionary<string, ValeursMarchandiseErp>> GetValeursMarchandiseAsync(
            int soId, IReadOnlyCollection<string> numerosFacture)
        {
            var resultat = new Dictionary<string, ValeursMarchandiseErp>(StringComparer.OrdinalIgnoreCase);
            foreach (var numero in numerosFacture)
                if (ValeursMarchandise.TryGetValue(numero, out var valeurs)) resultat[numero] = valeurs;
            return Task.FromResult<IReadOnlyDictionary<string, ValeursMarchandiseErp>>(resultat);
        }

        public Task<IReadOnlyDictionary<int, TypeModeReglementDelaiPaiement>> GetTypesModeReglementAsync(IReadOnlyCollection<int> modeIds)
        {
            var resultat = new Dictionary<int, TypeModeReglementDelaiPaiement>();
            foreach (var id in modeIds ?? Array.Empty<int>())
                if (TypesModeReglement.TryGetValue(id, out var type)) resultat[id] = type;
            return Task.FromResult<IReadOnlyDictionary<int, TypeModeReglementDelaiPaiement>>(resultat);
        }

        private static DeclarationDelaiPaiement Cloner(DeclarationDelaiPaiement source, int ddpId) => new()
        {
            DdpId = ddpId, Numero = source.Numero, SocieteId = source.SocieteId, Date = source.Date,
            Exercice = source.Exercice, Type = source.Type, DateDebut = source.DateDebut, DateFin = source.DateFin,
            Statut = source.Statut, DateCreation = source.DateCreation, CreateurId = source.CreateurId,
            DateModification = source.DateModification, ModificateurId = source.ModificateurId,
            EstDeposee = source.EstDeposee, Libelle = source.Libelle, FichierGenere = source.FichierGenere, Periode = source.Periode
        };
    }
}
