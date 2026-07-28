using System;
using System.Linq;
using Declaration.Core;

namespace Declaration.Core.Tests;

/// <summary>
/// TASK-132 : contrôle IF/ICE fournisseur BLOQUANT avant génération du fichier (décision PO §5.A-5).
/// Couvre le cas valide, le cas invalide avec liste des fautifs, le cas multi-fournisseurs fautifs,
/// et l'invariant de non-divergence avec <see cref="ValidationIdentiteFiscale"/>.
/// </summary>
public class ControleIdentiteFiscaleDelaiPaiementTests
{
    private const string IfValide = "12345678";              // 8 caractères
    private const string IceValide = "001234567890123";      // 15 caractères

    private static IdentiteFiscaleFournisseurDeclare Fournisseur(
        string code,
        string? identifiantFiscal = IfValide,
        string? ice = IceValide,
        bool trouve = true,
        int nombreLignes = 1,
        string? intitule = "FOURNISSEUR")
        => new()
        {
            TiersNo = 1,
            TiersCode = code,
            TiersIntitule = intitule,
            IdentifiantFiscal = identifiantFiscal,
            Ice = ice,
            TrouveDansReferentiel = trouve,
            NombreLignes = nombreLignes
        };

    // ─── Cas valide ───────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Controler_TousConformes_AutoriseLaGeneration()
    {
        var resultat = ControleIdentiteFiscaleDelaiPaiement.Controler(new[]
        {
            Fournisseur("F001"),
            Fournisseur("F002")
        });

        Assert.True(resultat.EstConforme);
        Assert.Empty(resultat.FournisseursFautifs);
        Assert.Equal(string.Empty, resultat.MessageBloquant);
        Assert.Equal(2, resultat.NombreFournisseursExamines);
    }

    [Fact]
    public void Controler_AucuneLigne_EstConforme_SansMessage()
    {
        var resultat = ControleIdentiteFiscaleDelaiPaiement.Controler(Array.Empty<IdentiteFiscaleFournisseurDeclare>());

        Assert.True(resultat.EstConforme);
        Assert.Equal(0, resultat.NombreFournisseursExamines);
    }

    // ─── Cas invalides : identifiant fiscal ───────────────────────────────────────────────────────

    [Fact]
    public void Controler_IdentifiantFiscalAbsent_Bloque_EtCiteLeFournisseur()
    {
        var resultat = ControleIdentiteFiscaleDelaiPaiement.Controler(new[]
        {
            Fournisseur("F001", identifiantFiscal: null, intitule: "ACIERS DU SUD")
        });

        Assert.False(resultat.EstConforme);
        var fautif = Assert.Single(resultat.FournisseursFautifs);
        Assert.Equal("F001", fautif.TiersCode);
        Assert.Contains(MotifIdentiteFiscaleDelaiPaiement.IdentifiantFiscalAbsent, fautif.Motifs);
        Assert.Contains("F001", resultat.MessageBloquant);
        Assert.Contains("ACIERS DU SUD", resultat.MessageBloquant);
        Assert.Contains("identifiant fiscal absent", resultat.MessageBloquant);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Controler_IdentifiantFiscalVideOuBlanc_MotifAbsent(string valeur)
    {
        var motifs = ControleIdentiteFiscaleDelaiPaiement.EvaluerMotifs(Fournisseur("F001", identifiantFiscal: valeur));

        Assert.Contains(MotifIdentiteFiscaleDelaiPaiement.IdentifiantFiscalAbsent, motifs);
        Assert.DoesNotContain(MotifIdentiteFiscaleDelaiPaiement.IdentifiantFiscalLongueurInvalide, motifs);
    }

    [Theory]
    [InlineData("1234567")]     // 7
    [InlineData("123456789")]   // 9
    public void Controler_IdentifiantFiscalLongueurInvalide_Bloque_AvecLongueurReelleDansLeMessage(string valeur)
    {
        var resultat = ControleIdentiteFiscaleDelaiPaiement.Controler(new[] { Fournisseur("F001", identifiantFiscal: valeur) });

        Assert.False(resultat.EstConforme);
        Assert.Contains(MotifIdentiteFiscaleDelaiPaiement.IdentifiantFiscalLongueurInvalide, resultat.FournisseursFautifs[0].Motifs);
        Assert.Contains($"{valeur.Length} caractère(s) au lieu de 8", resultat.MessageBloquant);
    }

    [Fact]
    public void Controler_IdentifiantFiscalAvecEspace_Bloque()
    {
        // 8 caractères MAIS contenant un espace : le legacy testait les deux règles séparément.
        var resultat = ControleIdentiteFiscaleDelaiPaiement.Controler(new[] { Fournisseur("F001", identifiantFiscal: "1234 678") });

        Assert.False(resultat.EstConforme);
        Assert.Contains(MotifIdentiteFiscaleDelaiPaiement.IdentifiantFiscalAvecEspace, resultat.FournisseursFautifs[0].Motifs);
        Assert.DoesNotContain(MotifIdentiteFiscaleDelaiPaiement.IdentifiantFiscalLongueurInvalide, resultat.FournisseursFautifs[0].Motifs);
    }

    // ─── Cas invalides : ICE ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Controler_IceAbsent_Bloque()
    {
        var resultat = ControleIdentiteFiscaleDelaiPaiement.Controler(new[] { Fournisseur("F001", ice: null) });

        Assert.False(resultat.EstConforme);
        Assert.Contains(MotifIdentiteFiscaleDelaiPaiement.IceAbsent, resultat.FournisseursFautifs[0].Motifs);
        Assert.Contains("ICE absent", resultat.MessageBloquant);
    }

    [Fact]
    public void Controler_IceLongueurInvalide_Bloque()
    {
        var resultat = ControleIdentiteFiscaleDelaiPaiement.Controler(new[] { Fournisseur("F001", ice: "0012345678901") });

        Assert.False(resultat.EstConforme);
        Assert.Contains(MotifIdentiteFiscaleDelaiPaiement.IceLongueurInvalide, resultat.FournisseursFautifs[0].Motifs);
        Assert.Contains("13 caractère(s) au lieu de 15", resultat.MessageBloquant);
    }

    [Fact]
    public void Controler_IceAvecEspace_Bloque()
    {
        var resultat = ControleIdentiteFiscaleDelaiPaiement.Controler(new[] { Fournisseur("F001", ice: "00123456789 123") });

        Assert.False(resultat.EstConforme);
        Assert.Contains(MotifIdentiteFiscaleDelaiPaiement.IceAvecEspace, resultat.FournisseursFautifs[0].Motifs);
    }

    // ─── Cumul de motifs et fournisseur introuvable ───────────────────────────────────────────────

    [Fact]
    public void Controler_IfEtIceInvalides_ListeLesDeuxMotifs_AucunMotifSilencieux()
    {
        var resultat = ControleIdentiteFiscaleDelaiPaiement.Controler(new[] { Fournisseur("F001", identifiantFiscal: "12", ice: "34") });

        var fautif = Assert.Single(resultat.FournisseursFautifs);
        Assert.Equal(2, fautif.Motifs.Count);
        Assert.Contains(MotifIdentiteFiscaleDelaiPaiement.IdentifiantFiscalLongueurInvalide, fautif.Motifs);
        Assert.Contains(MotifIdentiteFiscaleDelaiPaiement.IceLongueurInvalide, fautif.Motifs);
        Assert.Equal(fautif.Motifs.Count, fautif.MotifsLibelles.Count);
    }

    [Fact]
    public void Controler_FournisseurIntrouvableDansReferentiel_MotifUniqueEtExplicite()
    {
        // Le legacy levait « Impossible de charger les informations fournisseur [CODE] ». Cas distinct
        // d'un IF/ICE vide : on ne prétend PAS savoir ce que contient une fiche qu'on n'a pas lue.
        var resultat = ControleIdentiteFiscaleDelaiPaiement.Controler(new[]
        {
            Fournisseur("F001", identifiantFiscal: null, ice: null, trouve: false)
        });

        var fautif = Assert.Single(resultat.FournisseursFautifs);
        Assert.Equal(new[] { MotifIdentiteFiscaleDelaiPaiement.FournisseurIntrouvableDansReferentiel }, fautif.Motifs);
        Assert.Contains("introuvable dans le référentiel", resultat.MessageBloquant);
    }

    // ─── Multi-fournisseurs ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void Controler_PlusieursFournisseursFautifs_TousCites_TriesParCode()
    {
        var resultat = ControleIdentiteFiscaleDelaiPaiement.Controler(new[]
        {
            Fournisseur("F003", ice: null),
            Fournisseur("F001", identifiantFiscal: null),
            Fournisseur("F002"),                                // conforme : ne doit PAS apparaître
            Fournisseur("F004", identifiantFiscal: "1", ice: "2")
        });

        Assert.False(resultat.EstConforme);
        Assert.Equal(new[] { "F001", "F003", "F004" }, resultat.FournisseursFautifs.Select(f => f.TiersCode).ToArray());
        Assert.Equal(4, resultat.NombreFournisseursExamines);
        Assert.Contains("3 fournisseurs ont une identité fiscale invalide", resultat.MessageBloquant);
        Assert.DoesNotContain("F002", resultat.MessageBloquant);
    }

    [Fact]
    public void Controler_MemeFournisseurSurPlusieursLignes_CiteUneSeuleFois_AvecLeCumulDeLignes()
    {
        var resultat = ControleIdentiteFiscaleDelaiPaiement.Controler(new[]
        {
            Fournisseur("F001", identifiantFiscal: null, nombreLignes: 3),
            Fournisseur("F001", identifiantFiscal: null, nombreLignes: 2)
        });

        var fautif = Assert.Single(resultat.FournisseursFautifs);
        Assert.Equal(5, fautif.NombreLignes);
        Assert.Equal(1, resultat.NombreFournisseursExamines);
        Assert.Contains("5 lignes concernées", resultat.MessageBloquant);
    }

    [Fact]
    public void Controler_UnSeulFautif_MessageAuSingulier()
    {
        var resultat = ControleIdentiteFiscaleDelaiPaiement.Controler(new[] { Fournisseur("F001", ice: null) });

        Assert.Contains("1 fournisseur a une identité fiscale invalide", resultat.MessageBloquant);
        Assert.Contains("1 ligne concernée", resultat.MessageBloquant);
    }

    [Fact]
    public void Controler_IntituleAbsent_MessageResteLisible()
    {
        var resultat = ControleIdentiteFiscaleDelaiPaiement.Controler(new[]
        {
            Fournisseur("F001", ice: null, intitule: null)
        });

        Assert.Contains("(intitulé non renseigné)", resultat.MessageBloquant);
    }

    // ─── Invariant : aucun risque de divergence avec ValidationIdentiteFiscale ────────────────────

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("12345678", "001234567890123")]
    [InlineData("1234567", "001234567890123")]
    [InlineData("123456789", "001234567890123")]
    [InlineData("1234 678", "001234567890123")]
    [InlineData("12345678", "00123456789012")]
    [InlineData("12345678", "0012345678901234")]
    [InlineData("12345678", "00123456789 123")]
    [InlineData("12345678", null)]
    [InlineData(null, "001234567890123")]
    public void Invariant_AucunMotif_EquivautA_EstIfValide_ET_EstIceValide(string? identifiantFiscal, string? ice)
    {
        // Garantit qu'il n'existe PAS deux sources de vérité : la liste de motifs (explicative) et
        // ValidationIdentiteFiscale (autorité 8/15 sans espace) ne peuvent pas diverger.
        var motifs = ControleIdentiteFiscaleDelaiPaiement.EvaluerMotifs(
            Fournisseur("F001", identifiantFiscal: identifiantFiscal, ice: ice));

        var attenduConforme = ValidationIdentiteFiscale.EstIfValide(identifiantFiscal)
                              && ValidationIdentiteFiscale.EstIceValide(ice);

        Assert.Equal(attenduConforme, motifs.Count == 0);
    }

    [Fact]
    public void Controler_RefuseUneEntreeNull()
    {
        Assert.Throws<ArgumentNullException>(() => ControleIdentiteFiscaleDelaiPaiement.Controler(null!));
        Assert.Throws<ArgumentNullException>(() => ControleIdentiteFiscaleDelaiPaiement.EvaluerMotifs(null!));
    }
}
