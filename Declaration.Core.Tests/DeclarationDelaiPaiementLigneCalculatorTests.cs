using System;
using Declaration.Core;
using Declaration.Core.Model;
using Xunit;

namespace Declaration.Core.Tests;

/// <summary>
/// TASK-133 : calcul PUR par ligne (solde restant / montant payé / code modePaiement / 3 balises
/// optionnelles), reproduisant <c>DeclarationDelaisPaiementFileGenerator.cs:108-160</c> (legacy).
/// </summary>
public class DeclarationDelaiPaiementLigneCalculatorTests
{
    private static readonly DateTime FinPeriode = new(2026, 3, 31, 23, 59, 59); // convention DDP_DateFin (TASK-132)

    private static FactureHorsDelaiXml Calculer(
        bool? reglementRapproche = null,
        DateTime? dateRapprochement = null,
        decimal solde = 1000m,
        decimal? montantAffecte = null,
        TypeModeReglementDelaiPaiement? typeMode = null,
        string? reference = "RF001",
        string? natureMarchandiseReelle = null,
        DateTime? dateLivraisonMarchandiseReelle = null)
        => DeclarationDelaiPaiementLigneCalculator.Calculer(
            dateFinPeriode: FinPeriode,
            identifiantFiscalFournisseur: "12345678",
            numRc: "RC-1",
            adresseSiegeSocial: "12 rue Test",
            numFacture: "FAC001",
            dateEmission: new DateTime(2025, 12, 1),
            dateConvenuePaiementFacture: new DateTime(2026, 1, 15),
            montantFactureTtc: 5000m,
            soldeEcheance: solde,
            montantAffecte: montantAffecte,
            reglementRapproche: reglementRapproche,
            dateRapprochement: dateRapprochement,
            referencePaiement: reference,
            typeModeReglement: typeMode,
            natureMarchandiseReelle: natureMarchandiseReelle,
            dateLivraisonMarchandiseReelle: dateLivraisonMarchandiseReelle);

    [Fact]
    public void FactureNonPayee_MontantNonEncorePayeEstLeSoldeSeul()
    {
        var facture = Calculer(reglementRapproche: null, montantAffecte: null, solde: 1000m);

        Assert.False(facture.PayeeHorsDelaiPeriode);
        Assert.Equal(1000m, facture.MontantNonEncorePaye);
        Assert.Equal(0m, facture.MontantPayeHorsDelai);
        Assert.Null(facture.DatePaiementHorsDelai);
        Assert.Null(facture.ModePaiement);
        Assert.Null(facture.ReferencePaiement);
    }

    [Fact]
    public void RapprocheeHorsPeriode_TraiteeCommeNonPayeeDansLaPeriode()
    {
        // Rapprochée, mais APRÈS la fin de la période de la déclaration → le montant affecté n'a pas
        // encore réduit le solde AU REGARD de cette déclaration (legacy : solde + montant affecté).
        var facture = Calculer(
            reglementRapproche: true,
            dateRapprochement: FinPeriode.AddDays(5),
            montantAffecte: 400m,
            solde: 600m);

        Assert.False(facture.PayeeHorsDelaiPeriode);
        Assert.Equal(1000m, facture.MontantNonEncorePaye); // 600 + 400
        Assert.Equal(0m, facture.MontantPayeHorsDelai);
        Assert.Null(facture.DatePaiementHorsDelai);
        Assert.Null(facture.ModePaiement);
        Assert.Null(facture.ReferencePaiement);
    }

    [Fact]
    public void RapprocheeDansLaPeriode_MontantNonEncorePayeEstLeSoldeSeul_MontantPayeExpose()
    {
        var dateRapprochement = new DateTime(2026, 2, 10);
        var facture = Calculer(
            reglementRapproche: true,
            dateRapprochement: dateRapprochement,
            montantAffecte: 400m,
            solde: 600m,
            typeMode: TypeModeReglementDelaiPaiement.Virement);

        Assert.True(facture.PayeeHorsDelaiPeriode);
        Assert.Equal(600m, facture.MontantNonEncorePaye);       // solde SEUL
        Assert.Equal(400m, facture.MontantPayeHorsDelai);
        Assert.Equal(dateRapprochement, facture.DatePaiementHorsDelai);
        Assert.Equal(DeclarationDelaiPaiementLigneCalculator.CodeVirement, facture.ModePaiement);
        Assert.Equal("RF001", facture.ReferencePaiement);
    }

    [Fact]
    public void RapprocheeExactementAuDernierJourDeLaPeriode_EstDansLaPeriode()
    {
        // Comparaison au JOUR (partie heure 23:59:59 ignorée), même convention que TASK-132.
        var facture = Calculer(
            reglementRapproche: true,
            dateRapprochement: FinPeriode.Date, // même jour que FinPeriode, à minuit
            montantAffecte: 100m,
            solde: 900m);

        Assert.True(facture.PayeeHorsDelaiPeriode);
    }

    [Fact]
    public void RapprocheeSansDateDeRapprochement_DurcissementAssume_TraiteeCommeNonPayee()
    {
        // Durcissement documenté (VERIFY) : le legacy aurait déréférencé Value sur un Nullable vide
        // (crash potentiel). Ici : jamais d'exception, traité comme non payé dans la période.
        var facture = Calculer(reglementRapproche: true, dateRapprochement: null, montantAffecte: 250m, solde: 750m);

        Assert.False(facture.PayeeHorsDelaiPeriode);
        Assert.Equal(1000m, facture.MontantNonEncorePaye);
        Assert.Equal(0m, facture.MontantPayeHorsDelai);
    }

    [Theory]
    [InlineData(TypeModeReglementDelaiPaiement.Espece, DeclarationDelaiPaiementLigneCalculator.CodeEspece)]
    [InlineData(TypeModeReglementDelaiPaiement.Cheque, DeclarationDelaiPaiementLigneCalculator.CodeCheque)]
    [InlineData(TypeModeReglementDelaiPaiement.Virement, DeclarationDelaiPaiementLigneCalculator.CodeVirement)]
    [InlineData(TypeModeReglementDelaiPaiement.Traite, DeclarationDelaiPaiementLigneCalculator.CodeTraite)]
    public void CodesModePaiement_ConformesAuCdc(TypeModeReglementDelaiPaiement type, int codeAttendu)
    {
        var facture = Calculer(reglementRapproche: true, dateRapprochement: new DateTime(2026, 2, 1), typeMode: type);
        Assert.Equal(codeAttendu, facture.ModePaiement);
    }

    [Fact]
    public void ModeAutre_NonMappe_ModePaiementNull()
    {
        var facture = Calculer(reglementRapproche: true, dateRapprochement: new DateTime(2026, 2, 1), typeMode: TypeModeReglementDelaiPaiement.Autre);
        Assert.Null(facture.ModePaiement);
    }

    [Fact]
    public void ModeAbsent_PayeeDansLaPeriode_ModePaiementNull_MaisMontantsExposes()
    {
        var facture = Calculer(reglementRapproche: true, dateRapprochement: new DateTime(2026, 2, 1), typeMode: null, montantAffecte: 300m, solde: 700m);

        Assert.True(facture.PayeeHorsDelaiPeriode);
        Assert.Null(facture.ModePaiement);
        Assert.Equal(300m, facture.MontantPayeHorsDelai);
    }

    [Fact]
    public void DateLivraisonMarchandise_EgaleDateEmission_DetteAssumee()
    {
        var facture = Calculer();
        Assert.Equal(new DateTime(2025, 12, 1), facture.DateLivraisonMarchandise);
        Assert.Equal(facture.DateEmission, facture.DateLivraisonMarchandise);
    }

    // ── TASK-191 : câblage nature marchandise / date livraison marchandise ──────────────────────────

    [Fact]
    public void NatureEtDateLivraisonReellesAbsentes_RepliIdentiqueALaDetteAssumee_NonRegression()
    {
        // Cas 1/3 (TASK-191) : société non configurée (ou dictionnaire de valeurs vide) — équivalent à
        // ne PAS fournir les 2 nouveaux paramètres optionnels. Comportement STRICTEMENT inchangé.
        var facture = Calculer(natureMarchandiseReelle: null, dateLivraisonMarchandiseReelle: null);

        Assert.Equal(string.Empty, facture.NatureMarchandise);
        Assert.Equal(facture.DateEmission, facture.DateLivraisonMarchandise);
    }

    [Fact]
    public void NatureEtDateLivraisonReelles_PresentesEtNonNulles_ReflèteLesValeursReelles()
    {
        // Cas 3/3 (TASK-191) : société configurée ET valeur réelle trouvée sur F_DOCENTETE — le modèle
        // doit porter la VRAIE valeur, jamais le repli (dette assumée).
        var dateLivraisonReelle = new DateTime(2025, 12, 15);
        var facture = Calculer(natureMarchandiseReelle: "Materiel informatique", dateLivraisonMarchandiseReelle: dateLivraisonReelle);

        Assert.Equal("Materiel informatique", facture.NatureMarchandise);
        Assert.Equal(dateLivraisonReelle, facture.DateLivraisonMarchandise);
        Assert.NotEqual(facture.DateEmission, facture.DateLivraisonMarchandise);
    }

    [Fact]
    public void NatureMarchandiseSeulePresente_DateLivraisonReplieSurDateEmission()
    {
        // Cas 2/3 (TASK-191) : société configurée pour la nature mais valeur de date absente sur le
        // document (ou colonne date non configurée) — chaque champ se replie INDÉPENDAMMENT, jamais
        // d'exception.
        var facture = Calculer(natureMarchandiseReelle: "Alimentaire", dateLivraisonMarchandiseReelle: null);

        Assert.Equal("Alimentaire", facture.NatureMarchandise);
        Assert.Equal(facture.DateEmission, facture.DateLivraisonMarchandise);
    }

    [Fact]
    public void ChampsFournisseurEtFacture_ProjetesTelsQuels()
    {
        var facture = Calculer();
        Assert.Equal("12345678", facture.IdentifiantFiscalFournisseur);
        Assert.Equal("RC-1", facture.NumRc);
        Assert.Equal("12 rue Test", facture.AdresseSiegeSocial);
        Assert.Equal("FAC001", facture.NumFacture);
        Assert.Equal(5000m, facture.MontantFactureTtc);
        Assert.Equal(new DateTime(2026, 1, 15), facture.DateConvenuePaiementFacture);
    }
}

/// <summary>TASK-133 : résolution PURE des valeurs XML de période (annexe à <c>DeclarationDelaiPaiementCycleDeVieTests</c>).</summary>
public class DeclarationDelaiPaiementPeriodeXmlTests
{
    [Theory]
    [InlineData(TrimestreDelaiPaiement.T1, 1, "T1")]
    [InlineData(TrimestreDelaiPaiement.T2, 2, "T2")]
    [InlineData(TrimestreDelaiPaiement.T3, 3, "T3")]
    [InlineData(TrimestreDelaiPaiement.T4, 4, "T4")]
    public void Trimestrielle_PeriodeXmlEtFichier(TrimestreDelaiPaiement trimestre, int periodeXmlAttendue, string periodeFichierAttendue)
    {
        Assert.Equal(periodeXmlAttendue, DeclarationDelaiPaiementCycleDeVie.ResoudrePeriodeXml(TypeDeclarationDelaiPaiement.Trimestrielle, trimestre));
        Assert.Equal(periodeFichierAttendue, DeclarationDelaiPaiementCycleDeVie.ResoudrePeriodeFichier(TypeDeclarationDelaiPaiement.Trimestrielle, trimestre));
    }

    [Fact]
    public void Annuelle_PeriodeXmlEstToujoursCinq_PeriodeFichierEstA()
    {
        // Décision TASK-132 §Décisions n°5 : NE JAMAIS émettre DDP_Periode brut (0) — le générateur
        // legacy écrit la valeur littérale 5 pour une annuelle.
        Assert.Equal(5, DeclarationDelaiPaiementCycleDeVie.ResoudrePeriodeXml(TypeDeclarationDelaiPaiement.Annuelle, null));
        Assert.Equal("A", DeclarationDelaiPaiementCycleDeVie.ResoudrePeriodeFichier(TypeDeclarationDelaiPaiement.Annuelle, null));
    }

    [Fact]
    public void Trimestrielle_SansTrimestre_Leve()
    {
        Assert.Throws<InvalidOperationException>(() =>
            DeclarationDelaiPaiementCycleDeVie.ResoudrePeriodeXml(TypeDeclarationDelaiPaiement.Trimestrielle, null));
        Assert.Throws<InvalidOperationException>(() =>
            DeclarationDelaiPaiementCycleDeVie.ResoudrePeriodeFichier(TypeDeclarationDelaiPaiement.Trimestrielle, null));
    }
}
