using System;
using Declaration.Core;

namespace Declaration.Core.Tests;

/// <summary>
/// TASK-132 : numérotation des déclarations Délai de Paiement (reproduction du legacy
/// <c>SocieteRepository.GetNumeroPieceCourante</c>/<c>IncrementNumero</c>), avec les durcissements
/// assumés là où le legacy produisait silencieusement des numéros cassés.
/// </summary>
public class NumerotationDeclarationDelaiPaiementTests
{
    /// <summary>Paramétrage réel constaté en base (<c>SO_Id=1</c> de GR_EMA_DISTRIBUTION).</summary>
    private static readonly ConfigurationNumerotationDelaiPaiement ConfigurationReelle = new()
    {
        Prefixe = "DDP",
        InclureAnnee = true,
        InclureMois = true,
        NombreChiffres = 4
    };

    private static readonly DateTime Reference = new(2026, 7, 28);

    [Fact]
    public void Racine_PrefixeAnneeMois()
    {
        Assert.Equal("DDP2607", NumerotationDeclarationDelaiPaiement.ConstruireRacine(ConfigurationReelle, Reference));
    }

    [Fact]
    public void Racine_SansAnneeNiMois_ResteLePrefixeSeul()
    {
        var configuration = new ConfigurationNumerotationDelaiPaiement { Prefixe = "DDP", NombreChiffres = 5 };

        Assert.Equal("DDP", NumerotationDeclarationDelaiPaiement.ConstruireRacine(configuration, Reference));
    }

    [Fact]
    public void PatternLike_UneClasseDeChiffresParPositionDuCompteur()
    {
        Assert.Equal(
            "DDP2607[0-9][0-9][0-9][0-9]",
            NumerotationDeclarationDelaiPaiement.ConstruirePatternLike(ConfigurationReelle, Reference));
    }

    [Fact]
    public void ProchainNumero_AucunNumeroExistant_CommenceA1_ZeroPade()
    {
        Assert.Equal(
            "DDP26070001",
            NumerotationDeclarationDelaiPaiement.ResoudreProchainNumero(ConfigurationReelle, Reference, null));
    }

    [Theory]
    [InlineData("DDP26070001", "DDP26070002")]
    [InlineData("DDP26070009", "DDP26070010")]
    [InlineData("DDP26070099", "DDP26070100")]
    [InlineData("DDP26079998", "DDP26079999")]
    public void ProchainNumero_IncrementeLeCompteurEnConservantLaRacine(string dernier, string attendu)
    {
        Assert.Equal(attendu, NumerotationDeclarationDelaiPaiement.ResoudreProchainNumero(ConfigurationReelle, Reference, dernier));
    }

    [Fact]
    public void ProchainNumero_ChangementDeMois_RepartA1()
    {
        // Le motif LIKE inclut le mois : le MAX lu pour août ne contient aucun numéro de juillet.
        var aout = new DateTime(2026, 8, 1);

        Assert.Equal("DDP26080001", NumerotationDeclarationDelaiPaiement.ResoudreProchainNumero(ConfigurationReelle, aout, null));
    }

    [Fact]
    public void ProchainNumero_CompteurSature_BloqueExplicitement()
    {
        // Le legacy produisait 5 chiffres (« DDP260710000 ») : le motif LIKE ne matchait plus et la
        // numérotation repartait silencieusement à 1, générant un doublon.
        var ex = Assert.Throws<InvalidOperationException>(() =>
            NumerotationDeclarationDelaiPaiement.ResoudreProchainNumero(ConfigurationReelle, Reference, "DDP26079999"));

        Assert.Contains("compteur a atteint son maximum", ex.Message);
    }

    [Fact]
    public void ProchainNumero_SuffixeNonNumerique_BloqueExplicitement()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            NumerotationDeclarationDelaiPaiement.ResoudreProchainNumero(ConfigurationReelle, Reference, "DDP2607ABCD"));

        Assert.Contains("n'est pas numérique", ex.Message);
    }

    [Fact]
    public void ProchainNumero_DernierNumeroPlusCourtQueLeCompteur_BloqueExplicitement()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            NumerotationDeclarationDelaiPaiement.ResoudreProchainNumero(ConfigurationReelle, Reference, "12"));

        Assert.Contains("plus court que le compteur configuré", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10)]
    public void Configuration_NombreDeChiffresInvalide_BloqueAuLieuDeProduireUnNumeroCasse(int nombreChiffres)
    {
        var configuration = new ConfigurationNumerotationDelaiPaiement { Prefixe = "DDP", NombreChiffres = nombreChiffres };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            NumerotationDeclarationDelaiPaiement.ResoudreProchainNumero(configuration, Reference, null));

        Assert.Contains("SO_DecDPNumCount", ex.Message);
    }

    [Fact]
    public void ProchainNumero_DepassementDes30Caracteres_BloqueExplicitement()
    {
        var configuration = new ConfigurationNumerotationDelaiPaiement
        {
            Prefixe = new string('P', 28),
            NombreChiffres = 4
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            NumerotationDeclarationDelaiPaiement.ResoudreProchainNumero(configuration, Reference, null));

        Assert.Contains("dépasse 30 caractères", ex.Message);
    }

    [Fact]
    public void Configuration_Null_Refusee()
    {
        Assert.Throws<ArgumentNullException>(() =>
            NumerotationDeclarationDelaiPaiement.ResoudreProchainNumero(null!, Reference, null));
    }
}
