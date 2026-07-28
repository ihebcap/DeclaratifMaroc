using System;
using System.Collections.Generic;
using Declaration.Core;

namespace Declaration.Core.Tests;

/// <summary>
/// TASK-132 : cycle de vie PUR de la déclaration Délai de Paiement — bornes de période, unicité de
/// période, et TOUTES les gardes de transition (ordre d'évaluation legacy inclus).
/// </summary>
public class DeclarationDelaiPaiementCycleDeVieTests
{
    private static EtatDeclarationDelaiPaiement Etat(
        StatutDeclarationDelaiPaiement statut = StatutDeclarationDelaiPaiement.EnCours,
        bool deposee = false,
        bool fichierGenere = false,
        int nombreLignes = 0)
        => new()
        {
            Statut = statut,
            EstDeposee = deposee,
            FichierGenere = fichierGenere,
            NombreLignes = nombreLignes
        };

    // ─── Bornes de période (legacy l.617-653, reproduites à l'identique) ──────────────────────────

    [Fact]
    public void Periode_Annuelle_CouvreAnneeCivileComplete()
    {
        var periode = DeclarationDelaiPaiementCycleDeVie.CalculerPeriode(
            2026, TypeDeclarationDelaiPaiement.Annuelle, null);

        Assert.Equal(new DateTime(2026, 1, 1), periode.DateDebut);
        Assert.Equal(new DateTime(2026, 12, 31), periode.DateFin);
    }

    [Theory]
    [InlineData(TrimestreDelaiPaiement.T1, 1, 1, 3, 31)]
    [InlineData(TrimestreDelaiPaiement.T2, 4, 1, 6, 30)]
    [InlineData(TrimestreDelaiPaiement.T3, 7, 1, 9, 30)]
    [InlineData(TrimestreDelaiPaiement.T4, 10, 1, 12, 31)]
    public void Periode_Trimestrielle_BornesCalendairesExactes(
        TrimestreDelaiPaiement trimestre, int moisDebut, int jourDebut, int moisFin, int jourFin)
    {
        var periode = DeclarationDelaiPaiementCycleDeVie.CalculerPeriode(
            2026, TypeDeclarationDelaiPaiement.Trimestrielle, trimestre);

        Assert.Equal(new DateTime(2026, moisDebut, jourDebut), periode.DateDebut);
        Assert.Equal(new DateTime(2026, moisFin, jourFin), periode.DateFin);
    }

    [Fact]
    public void Periode_Trimestrielle_SansTrimestre_Bloque()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            DeclarationDelaiPaiementCycleDeVie.CalculerPeriode(2026, TypeDeclarationDelaiPaiement.Trimestrielle, null));

        Assert.Equal("Trimestre invalide.", ex.Message);
    }

    [Fact]
    public void Periode_TypeInconnu_Bloque()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            DeclarationDelaiPaiementCycleDeVie.CalculerPeriode(2026, (TypeDeclarationDelaiPaiement)99, null));

        Assert.Equal("Type déclaration invalide.", ex.Message);
    }

    [Fact]
    public void Periode_ExerciceHorsBornes_BloqueSansExceptionTechnique()
    {
        // Jamais une ArgumentOutOfRangeException brute de DateTime : message métier explicite.
        var ex = Assert.Throws<InvalidOperationException>(() =>
            DeclarationDelaiPaiementCycleDeVie.CalculerPeriode(0, TypeDeclarationDelaiPaiement.Annuelle, null));

        Assert.Contains("Exercice invalide", ex.Message);
    }

    [Fact]
    public void PeriodePersistee_Annuelle_EstNonApplicable_ZeroPasUnTrimestreTrompeur()
    {
        Assert.Equal(
            DeclarationDelaiPaiementCycleDeVie.PeriodeNonApplicable,
            DeclarationDelaiPaiementCycleDeVie.ResoudrePeriodePersistee(TypeDeclarationDelaiPaiement.Annuelle, TrimestreDelaiPaiement.T3));

        Assert.Equal(
            3,
            DeclarationDelaiPaiementCycleDeVie.ResoudrePeriodePersistee(TypeDeclarationDelaiPaiement.Trimestrielle, TrimestreDelaiPaiement.T3));
    }

    // ─── Unicité de période ───────────────────────────────────────────────────────────────────────

    private static DeclarationDelaiPaiementExistanteResume Existante(
        string numero, int exercice, DateTime debut, DateTime fin)
        => new() { DdpId = 1, Numero = numero, Exercice = exercice, DateDebut = debut, DateFin = fin };

    [Fact]
    public void Unicite_MemesBornes_DetecteLeConflit_MalgreHeure235959Stockee()
    {
        // Le legacy stockait DDP_DateFin à 23:59:59 mais interrogeait à minuit : sa propre requête
        // d'égalité exacte ne pouvait JAMAIS matcher. Ici la comparaison est faite au JOUR.
        var periode = DeclarationDelaiPaiementCycleDeVie.CalculerPeriode(
            2026, TypeDeclarationDelaiPaiement.Trimestrielle, TrimestreDelaiPaiement.T1);

        var existantes = new[]
        {
            Existante("DDP26010001", 2026, new DateTime(2026, 1, 1), new DateTime(2026, 3, 31, 23, 59, 59))
        };

        var conflit = DeclarationDelaiPaiementCycleDeVie.TrouverPeriodeEnConflit(2026, periode, existantes);

        Assert.NotNull(conflit);
        Assert.Equal("DDP26010001", conflit!.Numero);
    }

    [Fact]
    public void Unicite_AutreExercice_AucunConflit()
    {
        var periode = DeclarationDelaiPaiementCycleDeVie.CalculerPeriode(
            2026, TypeDeclarationDelaiPaiement.Trimestrielle, TrimestreDelaiPaiement.T1);

        var existantes = new[]
        {
            Existante("DDP25010001", 2025, new DateTime(2025, 1, 1), new DateTime(2025, 3, 31, 23, 59, 59))
        };

        Assert.Null(DeclarationDelaiPaiementCycleDeVie.TrouverPeriodeEnConflit(2026, periode, existantes));
    }

    [Fact]
    public void Unicite_TrimestreDifferent_AucunConflit()
    {
        var periode = DeclarationDelaiPaiementCycleDeVie.CalculerPeriode(
            2026, TypeDeclarationDelaiPaiement.Trimestrielle, TrimestreDelaiPaiement.T2);

        var existantes = new[]
        {
            Existante("DDP26010001", 2026, new DateTime(2026, 1, 1), new DateTime(2026, 3, 31, 23, 59, 59))
        };

        Assert.Null(DeclarationDelaiPaiementCycleDeVie.TrouverPeriodeEnConflit(2026, periode, existantes));
    }

    [Fact]
    public void Unicite_TrimestreDansUneAnnuelleExistante_Bloque()
    {
        // Contrôle n°2 legacy (période englobante) : reproduit tel quel.
        var periode = DeclarationDelaiPaiementCycleDeVie.CalculerPeriode(
            2026, TypeDeclarationDelaiPaiement.Trimestrielle, TrimestreDelaiPaiement.T3);

        var existantes = new[]
        {
            Existante("DDP26010001", 2026, new DateTime(2026, 1, 1), new DateTime(2026, 12, 31, 23, 59, 59))
        };

        var conflit = DeclarationDelaiPaiementCycleDeVie.TrouverPeriodeEnConflit(2026, periode, existantes);

        Assert.NotNull(conflit);
        Assert.Equal("DDP26010001", conflit!.Numero);
    }

    [Fact]
    public void Unicite_AnnuelleApresUnTrimestre_NonBloquee_AsymetrieLegacyDocumentee()
    {
        // ⚠ Comportement VOLONTAIREMENT identique au legacy, signalé en VERIFY pour arbitrage PO :
        // le contrôle « période englobante » est asymétrique. TASK-132 tranche explicitement qu'il
        // n'y a « pas de vrai risque de chevauchement à gérer ici » — on ne durcit donc pas seul.
        var periode = DeclarationDelaiPaiementCycleDeVie.CalculerPeriode(
            2026, TypeDeclarationDelaiPaiement.Annuelle, null);

        var existantes = new[]
        {
            Existante("DDP26010001", 2026, new DateTime(2026, 1, 1), new DateTime(2026, 3, 31, 23, 59, 59))
        };

        Assert.Null(DeclarationDelaiPaiementCycleDeVie.TrouverPeriodeEnConflit(2026, periode, existantes));
    }

    // ─── Gardes : intégration de lignes / modification du libellé ─────────────────────────────────

    [Fact]
    public void Integration_DeclarationEnCoursNonDeposee_Autorisee()
    {
        DeclarationDelaiPaiementCycleDeVie.ValiderIntegrationLignes(Etat());
        DeclarationDelaiPaiementCycleDeVie.ValiderModificationLibelle(Etat());
    }

    [Fact]
    public void Integration_DeclarationCloturee_Bloquee()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            DeclarationDelaiPaiementCycleDeVie.ValiderIntegrationLignes(Etat(StatutDeclarationDelaiPaiement.Cloture)));

        Assert.Equal("La déclaration est clôturée.", ex.Message);
    }

    [Fact]
    public void Integration_DeclarationDeposee_Bloquee_PrioriteSurLeStatut()
    {
        // Ordre d'évaluation legacy : le dépôt est testé AVANT le statut.
        var ex = Assert.Throws<InvalidOperationException>(() =>
            DeclarationDelaiPaiementCycleDeVie.ValiderIntegrationLignes(
                Etat(StatutDeclarationDelaiPaiement.Cloture, deposee: true)));

        Assert.Equal("La déclaration est déposée.", ex.Message);
    }

    // ─── Gardes : clôture / déclôture ─────────────────────────────────────────────────────────────

    [Fact]
    public void Cloture_SansLigne_Bloquee()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            DeclarationDelaiPaiementCycleDeVie.ValiderCloture(Etat(nombreLignes: 0)));

        Assert.Equal("La déclaration ne contient aucune ligne.", ex.Message);
    }

    [Fact]
    public void Cloture_AvecAuMoinsUneLigne_Autorisee()
    {
        DeclarationDelaiPaiementCycleDeVie.ValiderCloture(Etat(nombreLignes: 1));
    }

    [Fact]
    public void Cloture_DejaCloturee_Bloquee()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            DeclarationDelaiPaiementCycleDeVie.ValiderCloture(Etat(StatutDeclarationDelaiPaiement.Cloture, nombreLignes: 3)));

        Assert.Equal("La déclaration est clôturée.", ex.Message);
    }

    [Fact]
    public void Decloture_FichierNonGenere_Autorisee()
    {
        DeclarationDelaiPaiementCycleDeVie.ValiderAnnulationCloture(
            Etat(StatutDeclarationDelaiPaiement.Cloture, nombreLignes: 2));
    }

    [Fact]
    public void Decloture_FichierGenere_Bloquee()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            DeclarationDelaiPaiementCycleDeVie.ValiderAnnulationCloture(
                Etat(StatutDeclarationDelaiPaiement.Cloture, fichierGenere: true, nombreLignes: 2)));

        Assert.Equal("Le fichier de la déclaration est généré.", ex.Message);
    }

    [Fact]
    public void Decloture_DeclarationEnCours_Bloquee()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            DeclarationDelaiPaiementCycleDeVie.ValiderAnnulationCloture(Etat(nombreLignes: 2)));

        Assert.Equal("La déclaration n'est pas clôturée.", ex.Message);
    }

    // ─── Gardes : génération de fichier ───────────────────────────────────────────────────────────

    [Fact]
    public void GenerationFichier_ClotureeAvecLignes_Autorisee()
    {
        DeclarationDelaiPaiementCycleDeVie.ValiderGenerationFichier(
            Etat(StatutDeclarationDelaiPaiement.Cloture, nombreLignes: 5));
    }

    [Fact]
    public void GenerationFichier_DeclarationEnCours_Bloquee()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            DeclarationDelaiPaiementCycleDeVie.ValiderGenerationFichier(Etat(nombreLignes: 5)));

        Assert.Equal("La déclaration n'est pas clôturée.", ex.Message);
    }

    [Fact]
    public void GenerationFichier_DejaGenere_Bloquee()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            DeclarationDelaiPaiementCycleDeVie.ValiderGenerationFichier(
                Etat(StatutDeclarationDelaiPaiement.Cloture, fichierGenere: true, nombreLignes: 5)));

        Assert.Equal("Le fichier du déclaration est déjà généré.", ex.Message);
    }

    [Fact]
    public void AnnulationGenerationFichier_FichierNonGenere_Bloquee()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            DeclarationDelaiPaiementCycleDeVie.ValiderAnnulationGenerationFichier(
                Etat(StatutDeclarationDelaiPaiement.Cloture, nombreLignes: 5)));

        Assert.Equal("Le fichier du déclaration n'est pas généré.", ex.Message);
    }

    [Fact]
    public void AnnulationGenerationFichier_FichierGenere_Autorisee()
    {
        DeclarationDelaiPaiementCycleDeVie.ValiderAnnulationGenerationFichier(
            Etat(StatutDeclarationDelaiPaiement.Cloture, fichierGenere: true, nombreLignes: 5));
    }

    // ─── Gardes : dépôt (flag MANUEL, aucun appel externe) ────────────────────────────────────────

    [Fact]
    public void Depot_FichierNonGenere_Bloque()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            DeclarationDelaiPaiementCycleDeVie.ValiderDepot(
                Etat(StatutDeclarationDelaiPaiement.Cloture, nombreLignes: 5)));

        Assert.Equal("Le fichier du déclaration n'est pas généré.", ex.Message);
    }

    [Fact]
    public void Depot_ClotureeFichierGenereAvecLignes_Autorise()
    {
        DeclarationDelaiPaiementCycleDeVie.ValiderDepot(
            Etat(StatutDeclarationDelaiPaiement.Cloture, fichierGenere: true, nombreLignes: 5));
    }

    [Fact]
    public void Depot_DejaDeposee_Bloque()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            DeclarationDelaiPaiementCycleDeVie.ValiderDepot(
                Etat(StatutDeclarationDelaiPaiement.Cloture, deposee: true, fichierGenere: true, nombreLignes: 5)));

        Assert.Equal("La déclaration est déposée.", ex.Message);
    }

    // ─── Gardes : suppression ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void Suppression_EnCoursSansLigne_Autorisee()
    {
        DeclarationDelaiPaiementCycleDeVie.ValiderSuppression(Etat());
    }

    [Fact]
    public void Suppression_AvecLignes_Bloquee()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            DeclarationDelaiPaiementCycleDeVie.ValiderSuppression(Etat(nombreLignes: 1)));

        Assert.Equal("La déclaration contient des lignes.", ex.Message);
    }

    [Fact]
    public void Suppression_Cloturee_Bloquee()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            DeclarationDelaiPaiementCycleDeVie.ValiderSuppression(Etat(StatutDeclarationDelaiPaiement.Cloture)));

        Assert.Equal("La déclaration est clôturée.", ex.Message);
    }

    [Fact]
    public void ToutesLesGardes_RefusentUnEtatNull()
    {
        var actions = new List<Action<EtatDeclarationDelaiPaiement>>
        {
            DeclarationDelaiPaiementCycleDeVie.ValiderModificationLibelle,
            DeclarationDelaiPaiementCycleDeVie.ValiderIntegrationLignes,
            DeclarationDelaiPaiementCycleDeVie.ValiderSuppressionLigne,
            DeclarationDelaiPaiementCycleDeVie.ValiderCloture,
            DeclarationDelaiPaiementCycleDeVie.ValiderAnnulationCloture,
            DeclarationDelaiPaiementCycleDeVie.ValiderGenerationFichier,
            DeclarationDelaiPaiementCycleDeVie.ValiderAnnulationGenerationFichier,
            DeclarationDelaiPaiementCycleDeVie.ValiderDepot,
            DeclarationDelaiPaiementCycleDeVie.ValiderSuppression
        };

        foreach (var action in actions)
            Assert.Throws<ArgumentNullException>(() => action(null!));
    }
}
