using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Declaration.Application.Entities;
using Declaration.Application.Interfaces;
using Declaration.Application.Services;
using Declaration.Core;

namespace Declaration.Orchestration.Tests;

/// <summary>
/// TASK-132 : cycle de vie COMPLET de la déclaration Délai de Paiement, exercé de bout en bout HORS
/// BASE (création → intégration des lignes TASK-131 → clôture → contrôle IF/ICE → génération →
/// dépôt), sur un repository en mémoire et un faux <see cref="ISelectionDelaiPaiementService"/>.
///
/// Le but n'est PAS de retester les règles pures (couvertes dans <c>Declaration.Core.Tests</c>) mais
/// l'ORCHESTRATION : quelles lignes sont réellement écrites, quelles colonnes sont mises à jour, et
/// dans quel ordre les blocages tombent.
/// </summary>
public class Task132CycleDeVieDeclarationDelaiPaiementTests
{
    private const int SoId = 1;
    private const int UtId = 7;
    private const string IfValide = "12345678";
    private const string IceValide = "001234567890123";

    // ─── Cycle de vie complet ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CycleDeVieComplet_CreationIntegrationClotureControleGenerationDepot()
    {
        var repository = new FauxRepository();
        var selection = new FausseSelection();
        var service = new DeclarationDelaiPaiementService(repository, selection);

        // ── 1. Création : bornes calculées, numéro attribué par le serveur, statut EnCours.
        var ddpId = await service.CreerAsync(RequeteT1());

        var entete = await service.GetAsync(ddpId);
        Assert.NotNull(entete);
        Assert.Equal("DDP26070001", entete!.Numero);
        Assert.Equal(StatutDeclarationDelaiPaiement.EnCours, entete.Statut);
        Assert.Equal(new DateTime(2026, 1, 1), entete.DateDebut);
        Assert.Equal(new DateTime(2026, 3, 31, 23, 59, 59), entete.DateFin);   // convention legacy
        Assert.Equal((int)TrimestreDelaiPaiement.T1, entete.Periode);
        Assert.False(entete.EstDeposee);
        Assert.False(entete.FichierGenere);

        // ── 2. Clôture refusée tant qu'aucune ligne n'est intégrée.
        var sansLigne = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CloturerAsync(ddpId, UtId));
        Assert.Equal("La déclaration ne contient aucune ligne.", sansLigne.Message);

        // ── 3. Intégration : 2 candidates écrites, la ligne « reprise manuelle requise » jamais écrite.
        selection.Candidates.Add(Candidate(ecId: 100, afId: 500, depassement: 12));
        selection.Candidates.Add(Candidate(ecId: 101, afId: null, depassement: 30));
        selection.RepriseManuelle.Add(Candidate(ecId: 102, afId: null, depassement: null));

        var integration = await service.IntegrerLignesAsync(ddpId, UtId);

        Assert.Equal(2, integration.NombreCandidates);
        Assert.Equal(2, integration.NombreIntegrees);
        Assert.Equal(1, integration.NombreRepriseManuelleRequiseDisponibles);
        Assert.Empty(integration.ClesDejaIntegrees);

        // Les bornes passées à TASK-131 sont celles de la déclaration, jamais une plage libre.
        Assert.Equal((SoId, new DateTime(2026, 1, 1), new DateTime(2026, 3, 31, 23, 59, 59)), selection.DernierAppel);

        // Persistance conforme au contrat TASK-131 (EC_Id, AF_Id nullable, Depassement, EcheanceLegale).
        var lignes = repository.Lignes.Where(l => l.DdpId == ddpId).OrderBy(l => l.EcId).ToList();
        Assert.Equal(2, lignes.Count);
        Assert.Equal(500, lignes[0].AfId);
        Assert.Equal(12m, lignes[0].Depassement);
        Assert.Null(lignes[1].AfId);
        Assert.Equal(30m, lignes[1].Depassement);
        Assert.All(lignes, l => Assert.Equal(new DateTime(2025, 12, 1), l.EcheanceLegale));

        // La ligne 102 (reprise manuelle requise) n'a JAMAIS été écrite.
        Assert.DoesNotContain(repository.Lignes, l => l.EcId == 102);

        // ── 4. Rejeu de l'intégration : rien de neuf (garde anti-double-intégration).
        var rejeu = await service.IntegrerLignesAsync(ddpId, UtId);
        Assert.Equal(0, rejeu.NombreIntegrees);
        Assert.Equal(2, rejeu.ClesDejaIntegrees.Count);
        Assert.Equal(2, repository.Lignes.Count(l => l.DdpId == ddpId));

        // ── 5. Clôture désormais possible.
        await service.CloturerAsync(ddpId, UtId);
        Assert.Equal(StatutDeclarationDelaiPaiement.Cloture, (await service.GetAsync(ddpId))!.Statut);

        // ── 6. Contrôle IF/ICE BLOQUANT : F001 sans IF, F002 avec un ICE trop court.
        repository.IdentitesErp["F001"] = new IdentiteFiscaleTiersErp { TiersCode = "F001", IdentifiantFiscal = null, Ice = IceValide };
        repository.IdentitesErp["F002"] = new IdentiteFiscaleTiersErp { TiersCode = "F002", IdentifiantFiscal = IfValide, Ice = "0012" };

        var controle = await service.ControlerIdentiteFiscaleAsync(ddpId);
        Assert.False(controle.EstConforme);
        Assert.Equal(2, controle.FournisseursFautifs.Count);

        var bloque = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.VerifierGenerationFichierAutoriseeAsync(ddpId));
        Assert.Contains("F001", bloque.Message);
        Assert.Contains("F002", bloque.Message);

        // Le flag DDP_IsGeneretedFile n'a PAS pu être posé.
        var refus = await Assert.ThrowsAsync<InvalidOperationException>(() => service.MarquerFichierGenereAsync(ddpId, UtId));
        Assert.Contains("identité fiscale invalide", refus.Message);
        Assert.False((await service.GetAsync(ddpId))!.FichierGenere);

        // ── 7. IF/ICE corrigés côté ERP → contrôle conforme, génération autorisée.
        repository.IdentitesErp["F001"] = new IdentiteFiscaleTiersErp { TiersCode = "F001", IdentifiantFiscal = IfValide, Ice = IceValide };
        repository.IdentitesErp["F002"] = new IdentiteFiscaleTiersErp { TiersCode = "F002", IdentifiantFiscal = IfValide, Ice = IceValide };

        var conforme = await service.VerifierGenerationFichierAutoriseeAsync(ddpId);
        Assert.True(conforme.EstConforme);
        Assert.Equal(2, conforme.NombreFournisseursExamines);

        await service.MarquerFichierGenereAsync(ddpId, UtId);
        Assert.True((await service.GetAsync(ddpId))!.FichierGenere);

        // ── 8. Dépôt : flag MANUEL, aucun appel externe.
        await service.MarquerDeposeAsync(ddpId, UtId);
        var deposee = await service.GetAsync(ddpId);
        Assert.True(deposee!.EstDeposee);
        Assert.Equal(UtId, deposee.ModificateurId);

        // ── 9. Après dépôt : plus rien n'est modifiable (déclôture, suppression, libellé, lignes).
        Assert.Equal("La déclaration est déposée.",
            (await Assert.ThrowsAsync<InvalidOperationException>(() => service.AnnulerClotureAsync(ddpId, UtId))).Message);
        Assert.Equal("La déclaration est déposée.",
            (await Assert.ThrowsAsync<InvalidOperationException>(() => service.SupprimerAsync(ddpId, UtId))).Message);
        Assert.Equal("La déclaration est déposée.",
            (await Assert.ThrowsAsync<InvalidOperationException>(() => service.ModifierLibelleAsync(ddpId, "x", UtId))).Message);
        Assert.Equal("La déclaration est déposée.",
            (await Assert.ThrowsAsync<InvalidOperationException>(() => service.IntegrerLignesAsync(ddpId, UtId))).Message);

        // Les colonnes IMMUABLES n'ont jamais bougé pendant tout le cycle.
        Assert.Equal("DDP26070001", deposee.Numero);
        Assert.Equal(new DateTime(2026, 1, 1), deposee.DateDebut);
        Assert.Equal(new DateTime(2026, 3, 31, 23, 59, 59), deposee.DateFin);
        Assert.Equal(2026, deposee.Exercice);
        Assert.Equal(TypeDeclarationDelaiPaiement.Trimestrielle, deposee.Type);
        Assert.Equal(UtId, deposee.CreateurId);
    }

    // ─── Création ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Creation_MemePeriode_Refusee_EtCiteLaDeclarationExistante()
    {
        var repository = new FauxRepository();
        var service = new DeclarationDelaiPaiementService(repository, new FausseSelection());

        await service.CreerAsync(RequeteT1());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreerAsync(RequeteT1()));

        Assert.Contains("même période", ex.Message);
        Assert.Contains("DDP26070001", ex.Message);
        Assert.Single(repository.Entetes);
    }

    [Fact]
    public async Task Creation_TrimestresDifferents_Acceptee_EtNumerosIncrementes()
    {
        var repository = new FauxRepository();
        var service = new DeclarationDelaiPaiementService(repository, new FausseSelection());

        var t1 = await service.CreerAsync(RequeteT1());
        var t2 = await service.CreerAsync(RequeteT1().Avec(TrimestreDelaiPaiement.T2));

        Assert.Equal("DDP26070001", (await service.GetAsync(t1))!.Numero);
        Assert.Equal("DDP26070002", (await service.GetAsync(t2))!.Numero);
        Assert.Equal(new DateTime(2026, 4, 1), (await service.GetAsync(t2))!.DateDebut);
    }

    [Fact]
    public async Task Creation_SansUtilisateur_Refusee_AuditTrailObligatoire()
    {
        var service = new DeclarationDelaiPaiementService(new FauxRepository(), new FausseSelection());

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreerAsync(new CreerDeclarationDelaiPaiementRequest
        {
            SocieteId = SoId,
            Exercice = 2026,
            Type = TypeDeclarationDelaiPaiement.Annuelle,
            UtilisateurId = 0
        }));
    }

    [Fact]
    public async Task Creation_Annuelle_PeriodePersisteeNonApplicable()
    {
        var repository = new FauxRepository();
        var service = new DeclarationDelaiPaiementService(repository, new FausseSelection());

        var ddpId = await service.CreerAsync(new CreerDeclarationDelaiPaiementRequest
        {
            SocieteId = SoId,
            Exercice = 2026,
            Type = TypeDeclarationDelaiPaiement.Annuelle,
            Date = new DateTime(2026, 7, 28),
            UtilisateurId = UtId
        });

        var entete = await service.GetAsync(ddpId);
        Assert.Equal(DeclarationDelaiPaiementCycleDeVie.PeriodeNonApplicable, entete!.Periode);
        Assert.Null(entete.Trimestre);
        Assert.Equal(new DateTime(2026, 12, 31, 23, 59, 59), entete.DateFin);
    }

    // ─── Intégration sélective ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Integration_SelectionExplicite_RefuseLesLignesEnRepriseManuelleRequise()
    {
        var repository = new FauxRepository();
        var selection = new FausseSelection();
        var service = new DeclarationDelaiPaiementService(repository, selection);
        var ddpId = await service.CreerAsync(RequeteT1());

        selection.Candidates.Add(Candidate(ecId: 100, afId: null, depassement: 5));
        selection.RepriseManuelle.Add(Candidate(ecId: 200, afId: null, depassement: null));

        var resultat = await service.IntegrerLignesAsync(ddpId, UtId, new[]
        {
            new CleLigneDelaiPaiement(100, null),
            new CleLigneDelaiPaiement(200, null),   // reprise manuelle requise → refusée
            new CleLigneDelaiPaiement(999, null)    // absente de la sélection → signalée
        });

        Assert.Equal(1, resultat.NombreIntegrees);
        Assert.Equal(new[] { new CleLigneDelaiPaiement(200, null) }, resultat.ClesRefuseesRepriseManuelleRequise);
        Assert.Equal(new[] { new CleLigneDelaiPaiement(999, null) }, resultat.ClesIntrouvablesDansSelection);
        Assert.Single(repository.Lignes);
        Assert.Equal(100, repository.Lignes[0].EcId);
    }

    [Fact]
    public async Task Integration_SocieteNonConfiguree_AucuneLigneEcrite_MaisInformationRestituee()
    {
        // Comportement AMONT de TASK-131 (garde-fou TASK-128) : sans date de mise en route, toutes les
        // lignes sont en « reprise manuelle requise » ⇒ 0 candidate. Documenté, pas corrigé ici.
        var repository = new FauxRepository();
        var selection = new FausseSelection { DateMiseEnRoute = null };
        var service = new DeclarationDelaiPaiementService(repository, selection);
        var ddpId = await service.CreerAsync(RequeteT1());

        selection.RepriseManuelle.Add(Candidate(ecId: 300, afId: null, depassement: null));

        var resultat = await service.IntegrerLignesAsync(ddpId, UtId);

        Assert.Equal(0, resultat.NombreIntegrees);
        Assert.Equal(0, resultat.NombreCandidates);
        Assert.Equal(1, resultat.NombreRepriseManuelleRequiseDisponibles);
        Assert.Null(resultat.DateMiseEnRouteSociete);
        Assert.Empty(repository.Lignes);
    }

    // ─── Suppression ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Suppression_AvecLignes_Refusee_PuisAutoriseeApresRetraitDesLignes()
    {
        var repository = new FauxRepository();
        var selection = new FausseSelection();
        var service = new DeclarationDelaiPaiementService(repository, selection);
        var ddpId = await service.CreerAsync(RequeteT1());

        selection.Candidates.Add(Candidate(ecId: 100, afId: null, depassement: 5));
        await service.IntegrerLignesAsync(ddpId, UtId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SupprimerAsync(ddpId, UtId));
        Assert.Equal("La déclaration contient des lignes.", ex.Message);

        var ddplId = repository.Lignes.Single().DdplId;
        await service.SupprimerLigneAsync(ddpId, ddplId, UtId);

        await service.SupprimerAsync(ddpId, UtId);
        Assert.Empty(repository.Entetes);
    }

    [Fact]
    public async Task SuppressionLigne_LigneInexistante_ErreurExplicite()
    {
        var repository = new FauxRepository();
        var service = new DeclarationDelaiPaiementService(repository, new FausseSelection());
        var ddpId = await service.CreerAsync(RequeteT1());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SupprimerLigneAsync(ddpId, 4242, UtId));
        Assert.Contains("Impossible de charger la ligne", ex.Message);
    }

    // ─── Contrôle IF/ICE : fournisseur absent du référentiel ERP ──────────────────────────────────

    [Fact]
    public async Task ControleIfIce_FournisseurAbsentDuReferentielErp_Bloque()
    {
        var repository = new FauxRepository();
        var selection = new FausseSelection();
        var service = new DeclarationDelaiPaiementService(repository, selection);
        var ddpId = await service.CreerAsync(RequeteT1());

        selection.Candidates.Add(Candidate(ecId: 100, afId: null, depassement: 5));
        await service.IntegrerLignesAsync(ddpId, UtId);
        // repository.IdentitesErp reste VIDE : aucun tiers résolu.

        var controle = await service.ControlerIdentiteFiscaleAsync(ddpId);

        Assert.False(controle.EstConforme);
        Assert.Contains(
            MotifIdentiteFiscaleDelaiPaiement.FournisseurIntrouvableDansReferentiel,
            controle.FournisseursFautifs.Single().Motifs);
    }

    [Fact]
    public async Task Generation_DeclarationEnCours_Bloquee_AvantMemeLeControleIfIce()
    {
        var repository = new FauxRepository();
        var selection = new FausseSelection();
        var service = new DeclarationDelaiPaiementService(repository, selection);
        var ddpId = await service.CreerAsync(RequeteT1());

        selection.Candidates.Add(Candidate(ecId: 100, afId: null, depassement: 5));
        await service.IntegrerLignesAsync(ddpId, UtId);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.VerifierGenerationFichierAutoriseeAsync(ddpId));

        Assert.Equal("La déclaration n'est pas clôturée.", ex.Message);
        Assert.Equal(0, repository.AppelsIdentitesErp);   // aucune lecture Sage inutile
    }

    [Fact]
    public async Task Declaration_Inexistante_ErreurExplicite()
    {
        var service = new DeclarationDelaiPaiementService(new FauxRepository(), new FausseSelection());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CloturerAsync(999, UtId));
        Assert.Equal("Impossible de charger la déclaration.", ex.Message);
    }

    // ─── Utilitaires de test ──────────────────────────────────────────────────────────────────────

    private static CreerDeclarationDelaiPaiementRequest RequeteT1() => new()
    {
        SocieteId = SoId,
        Exercice = 2026,
        Type = TypeDeclarationDelaiPaiement.Trimestrielle,
        Trimestre = TrimestreDelaiPaiement.T1,
        Libelle = "DDP T1 2026",
        Date = new DateTime(2026, 7, 28),
        UtilisateurId = UtId
    };

    private static LigneSelectionDelaiPaiement Candidate(int ecId, int? afId, int? depassement) => new()
    {
        EcId = ecId,
        AfId = afId,
        Statut = depassement.HasValue ? StatutLigneDelaiPaiement.Candidate : StatutLigneDelaiPaiement.RepriseManuelleRequise,
        Depassement = depassement,
        EcheanceLegale = new DateTime(2025, 12, 1),
        TiersNo = ecId,
        TiersCode = ecId % 2 == 0 ? "F001" : "F002",
        TiersIntitule = "FOURNISSEUR " + ecId
    };

    private sealed class FausseSelection : ISelectionDelaiPaiementService
    {
        public List<LigneSelectionDelaiPaiement> Candidates { get; } = new();
        public List<LigneSelectionDelaiPaiement> RepriseManuelle { get; } = new();
        public DateTime? DateMiseEnRoute { get; init; } = new DateTime(2020, 1, 1);
        public (int SoId, DateTime Debut, DateTime Fin)? DernierAppel { get; private set; }

        public Task<ResultatSelectionDelaiPaiement> SelectionnerAsync(int soId, DateTime dateDebutPeriode, DateTime dateFinPeriode)
        {
            DernierAppel = (soId, dateDebutPeriode, dateFinPeriode);
            return Task.FromResult(new ResultatSelectionDelaiPaiement
            {
                DateDebutPeriode = dateDebutPeriode,
                DateFinPeriode = dateFinPeriode,
                DateMiseEnRouteSociete = DateMiseEnRoute,
                Lignes = Candidates.ToList(),
                LignesRepriseManuelleRequise = RepriseManuelle.ToList(),
                NombreEcheancesExaminees = Candidates.Count + RepriseManuelle.Count
            });
        }
    }

    /// <summary>Repository en mémoire : reproduit le comportement observable, y compris les gardes de dernier recours.</summary>
    private sealed class FauxRepository : IDeclarationDelaiPaiementRepository
    {
        public List<DeclarationDelaiPaiement> Entetes { get; } = new();
        public List<LigneStockee> Lignes { get; } = new();
        public Dictionary<string, IdentiteFiscaleTiersErp> IdentitesErp { get; } = new(StringComparer.OrdinalIgnoreCase);
        public int AppelsIdentitesErp { get; private set; }

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
        }

        public Task<int> CreerEnteteAsync(DeclarationDelaiPaiement entete)
        {
            var ddpId = _prochainDdpId++;
            Entetes.Add(Cloner(entete, ddpId));
            return Task.FromResult(ddpId);
        }

        public Task<DeclarationDelaiPaiement?> GetEnteteAsync(int ddpId)
            => Task.FromResult(Entetes.FirstOrDefault(e => e.DdpId == ddpId));

        public Task<IReadOnlyList<DeclarationDelaiPaiementListItem>> GetAllAsync(int soId)
            => Task.FromResult<IReadOnlyList<DeclarationDelaiPaiementListItem>>(Entetes
                .Where(e => e.SocieteId == soId)
                .Select(e => new DeclarationDelaiPaiementListItem
                {
                    Entete = e,
                    NombreLignes = Lignes.Count(l => l.DdpId == e.DdpId)
                }).ToList());

        public Task<IReadOnlyList<DeclarationDelaiPaiement>> GetAllParExerciceAsync(int soId, int exercice)
            => Task.FromResult<IReadOnlyList<DeclarationDelaiPaiement>>(
                Entetes.Where(e => e.SocieteId == soId && e.Exercice == exercice).ToList());

        public Task<bool> ExisteNumeroAsync(int soId, string numero)
            => Task.FromResult(Entetes.Any(e => e.SocieteId == soId && e.Numero == numero));

        public Task<string?> GetDernierNumeroAsync(int soId, string patternLike)
        {
            // Simulation du LIKE 'racine[0-9][0-9]…' : racine = tout avant le premier '['.
            var racine = patternLike.Contains('[') ? patternLike.Substring(0, patternLike.IndexOf('[')) : patternLike;
            var chiffres = (patternLike.Length - racine.Length) / 5;

            var numeros = Entetes
                .Where(e => e.SocieteId == soId
                            && e.Numero.Length == racine.Length + chiffres
                            && e.Numero.StartsWith(racine, StringComparison.Ordinal)
                            && e.Numero.Substring(racine.Length).All(char.IsDigit))
                .Select(e => e.Numero)
                .ToList();

            return Task.FromResult(numeros.Count == 0 ? null : numeros.Max());
        }

        public Task<ConfigurationNumerotationDelaiPaiement> GetConfigurationNumerotationAsync(int soId)
            => Task.FromResult(new ConfigurationNumerotationDelaiPaiement
            {
                // Paramétrage réel de GR_EMA_DISTRIBUTION (SO_Id=1).
                Prefixe = "DDP",
                InclureAnnee = true,
                InclureMois = true,
                NombreChiffres = 4
            });

        public Task MettreAJourEtatAsync(
            int ddpId,
            StatutDeclarationDelaiPaiement statut,
            bool estDeposee,
            bool fichierGenere,
            string? libelle,
            int modificateurId,
            DateTime dateModification)
        {
            var index = Entetes.FindIndex(e => e.DdpId == ddpId);
            if (index < 0) throw new InvalidOperationException($"Mise à jour impossible : DDP_Id={ddpId} introuvable.");

            var actuel = Entetes[index];
            Entetes[index] = new DeclarationDelaiPaiement
            {
                // Colonnes IMMUABLES recopiées telles quelles : le contrat ne permet pas de les changer.
                DdpId = actuel.DdpId,
                Numero = actuel.Numero,
                SocieteId = actuel.SocieteId,
                Date = actuel.Date,
                Exercice = actuel.Exercice,
                Type = actuel.Type,
                DateDebut = actuel.DateDebut,
                DateFin = actuel.DateFin,
                DateCreation = actuel.DateCreation,
                CreateurId = actuel.CreateurId,
                Periode = actuel.Periode,
                // Colonnes mutables.
                Statut = statut,
                EstDeposee = estDeposee,
                FichierGenere = fichierGenere,
                Libelle = libelle,
                ModificateurId = modificateurId,
                DateModification = dateModification
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
            => Task.FromResult<IReadOnlyList<CleLigneDelaiPaiement>>(
                Lignes.Where(l => l.DdpId == ddpId).Select(l => new CleLigneDelaiPaiement(l.EcId, l.AfId)).ToList());

        public Task<int> AjouterLignesAsync(int ddpId, IReadOnlyCollection<LigneAIntegrerDelaiPaiement> lignes)
        {
            foreach (var ligne in lignes)
            {
                Lignes.Add(new LigneStockee
                {
                    DdplId = _prochainDdplId++,
                    DdpId = ddpId,
                    EcId = ligne.EcId,
                    AfId = ligne.AfId,
                    Depassement = ligne.Depassement,
                    EcheanceLegale = ligne.EcheanceLegale,
                    TiersCode = ligne.EcId % 2 == 0 ? "F001" : "F002"
                });
            }
            return Task.FromResult(lignes.Count);
        }

        public Task<IReadOnlyList<LigneDeclarationDelaiPaiement>> GetLignesAsync(int ddpId)
            => Task.FromResult<IReadOnlyList<LigneDeclarationDelaiPaiement>>(Lignes
                .Where(l => l.DdpId == ddpId)
                .Select(l => new LigneDeclarationDelaiPaiement
                {
                    DdplId = l.DdplId,
                    DdpId = l.DdpId,
                    EcId = l.EcId,
                    AfId = l.AfId,
                    Depassement = l.Depassement,
                    EcheanceLegale = l.EcheanceLegale,
                    TiersNo = l.EcId,
                    TiersCode = l.TiersCode,
                    TiersIntitule = "FOURNISSEUR " + l.TiersCode
                }).ToList());

        public Task<int> SupprimerLigneAsync(int ddpId, int ddplId)
            => Task.FromResult(Lignes.RemoveAll(l => l.DdpId == ddpId && l.DdplId == ddplId));

        public Task<IReadOnlyDictionary<string, IdentiteFiscaleTiersErp>> GetIdentitesFiscalesTiersAsync(
            int soId, IReadOnlyCollection<string> tiersCodes)
        {
            AppelsIdentitesErp++;
            var resultat = new Dictionary<string, IdentiteFiscaleTiersErp>(StringComparer.OrdinalIgnoreCase);
            foreach (var code in tiersCodes)
                if (IdentitesErp.TryGetValue(code, out var identite)) resultat[code] = identite;

            return Task.FromResult<IReadOnlyDictionary<string, IdentiteFiscaleTiersErp>>(resultat);
        }

        // ── TASK-133 : stubs non exercés par les tests TASK-132 (aucune assertion de ce périmètre ici,
        // couverts par Task133GenerationFichierDelaiPaiementTests). ────────────────────────────────

        public SocieteDelaiPaiementInfo? SocieteInfo { get; set; }

        public Task<SocieteDelaiPaiementInfo> GetSocieteInfoAsync(int soId)
            => Task.FromResult(SocieteInfo ?? throw new InvalidOperationException($"Société SO_Id={soId} introuvable."));

        public Dictionary<int, Declaration.Core.Model.TypeModeReglementDelaiPaiement> TypesModeReglement { get; } = new();

        public Task<IReadOnlyDictionary<int, Declaration.Core.Model.TypeModeReglementDelaiPaiement>> GetTypesModeReglementAsync(
            IReadOnlyCollection<int> modeIds)
        {
            var resultat = new Dictionary<int, Declaration.Core.Model.TypeModeReglementDelaiPaiement>();
            foreach (var id in modeIds ?? Array.Empty<int>())
                if (TypesModeReglement.TryGetValue(id, out var type)) resultat[id] = type;

            return Task.FromResult<IReadOnlyDictionary<int, Declaration.Core.Model.TypeModeReglementDelaiPaiement>>(resultat);
        }

        private static DeclarationDelaiPaiement Cloner(DeclarationDelaiPaiement source, int ddpId) => new()
        {
            DdpId = ddpId,
            Numero = source.Numero,
            SocieteId = source.SocieteId,
            Date = source.Date,
            Exercice = source.Exercice,
            Type = source.Type,
            DateDebut = source.DateDebut,
            DateFin = source.DateFin,
            Statut = source.Statut,
            DateCreation = source.DateCreation,
            CreateurId = source.CreateurId,
            DateModification = source.DateModification,
            ModificateurId = source.ModificateurId,
            EstDeposee = source.EstDeposee,
            Libelle = source.Libelle,
            FichierGenere = source.FichierGenere,
            Periode = source.Periode
        };
    }
}

/// <summary>Petit utilitaire de test : clone une requête de création avec un autre trimestre.</summary>
internal static class RequeteDeclarationDelaiPaiementExtensions
{
    public static CreerDeclarationDelaiPaiementRequest Avec(
        this CreerDeclarationDelaiPaiementRequest source, TrimestreDelaiPaiement trimestre) => new()
    {
        SocieteId = source.SocieteId,
        Exercice = source.Exercice,
        Type = source.Type,
        Trimestre = trimestre,
        Libelle = source.Libelle,
        Date = source.Date,
        UtilisateurId = source.UtilisateurId
    };
}
