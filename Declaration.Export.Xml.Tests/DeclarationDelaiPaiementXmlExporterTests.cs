using System;
using System.Collections.Generic;
using System.IO;
using Declaration.Core.Model;
using Xunit;

namespace Declaration.Export.Xml.Tests;

/// <summary>
/// TASK-133 : structure XML exacte (tags/ordre), codes <c>modePaiement</c>, cas EnProcedure/normal,
/// garde « fichier déjà existant » — reproduction de
/// <c>DeclarationDelaisPaiementFileGenerator.cs</c> (legacy).
/// </summary>
public class DeclarationDelaiPaiementXmlExporterTests : IDisposable
{
    private readonly string _testDir;
    private readonly DeclarationDelaiPaiementXmlExporter _exporter;

    public DeclarationDelaiPaiementXmlExporterTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDir);
        _exporter = new DeclarationDelaiPaiementXmlExporter();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir)) Directory.Delete(_testDir, true);
    }

    private static EnTeteDeclarationDelaiPaiementXml EnTeteNormal(string numero = "DDP26070001") => new()
    {
        Numero = numero,
        Exercice = 2026,
        PeriodeXml = 1,
        PeriodeFichier = "T1",
        IdentifiantFiscalSociete = "123456",
        ActiviteMarrocCode = 1, // Normal
        ChiffreAffaire = 1500000.5m
    };

    private static FactureHorsDelaiXml FactureStandard() => new()
    {
        IdentifiantFiscalFournisseur = "87654321",
        NumRc = "RC-99",
        AdresseSiegeSocial = "12 rue des Fleurs, Casablanca",
        NumFacture = "FAC001",
        DateEmission = new DateTime(2025, 12, 1),
        DateLivraisonMarchandise = new DateTime(2025, 12, 1),
        DateConvenuePaiementFacture = new DateTime(2026, 1, 15),
        MontantFactureTtc = 5000.75m,
        MontantNonEncorePaye = 2000m,
        MontantPayeHorsDelai = 3000.75m,
        PayeeHorsDelaiPeriode = true,
        DatePaiementHorsDelai = new DateTime(2026, 2, 10),
        ModePaiement = DeclarationDelaiPaiementLigneCalculator.CodeVirement,
        ReferencePaiement = "RF26020010"
    };

    [Fact]
    public void GenererXml_StructureNormale_TagsEtOrdreExacts()
    {
        var modele = new DeclarationDelaiPaiementXmlModele
        {
            EnTete = EnTeteNormal(),
            Factures = new List<FactureHorsDelaiXml> { FactureStandard() }
        };

        var zipPath = _exporter.GenererXml(modele, _testDir);

        Assert.True(File.Exists(zipPath));
        var xmlPath = Path.Combine(_testDir, "DDP26070001-2026-T1.xml");
        Assert.True(File.Exists(xmlPath));

        var xml = File.ReadAllText(xmlPath);

        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>", xml);
        Assert.Contains("<DeclarationDelaiPaiement>", xml);
        Assert.Contains("<identifiantFiscal>123456</identifiantFiscal>", xml);
        Assert.Contains("<annee>2026</annee>", xml);
        Assert.Contains("<periode>1</periode>", xml);
        Assert.Contains("<activite>1</activite>", xml);
        // Normal (activite=1) : AUCUNE balise dateJugementOuvrProc.
        Assert.DoesNotContain("<dateJugementOuvrProc>", xml);
        Assert.Contains("<chiffreAffaire>1500000.5</chiffreAffaire>", xml);
        Assert.Contains("<listeFacturesHorsDelai>", xml);

        Assert.Contains("<FactureHorsDelai>", xml);
        Assert.Contains("<identifiantFiscal>87654321</identifiantFiscal>", xml);
        Assert.Contains("<numRC>RC-99</numRC>", xml);
        Assert.Contains("<adresseSiegeSocial>12 rue des Fleurs, Casablanca</adresseSiegeSocial>", xml);
        Assert.Contains("<numFacture>FAC001</numFacture>", xml);
        Assert.Contains("<dateEmission>2025-12-01</dateEmission>", xml);
        Assert.Contains("<natureMarchandise></natureMarchandise>", xml);
        Assert.Contains("<dateLivraisonMarchandise>2025-12-01</dateLivraisonMarchandise>", xml);
        Assert.Contains("<dateConvenuePaiementFacture>2026-01-15</dateConvenuePaiementFacture>", xml);
        Assert.Contains("<montantFactureTtc>5000.75</montantFactureTtc>", xml);
        Assert.Contains("<montantNonEncorePaye>2000</montantNonEncorePaye>", xml);
        Assert.Contains("<montantPayeHorsDelai>3000.75</montantPayeHorsDelai>", xml);
        Assert.Contains("<datePaiementHorsDelai>2026-02-10</datePaiementHorsDelai>", xml);
        Assert.Contains("<modePaiement>4</modePaiement>", xml);
        Assert.Contains("<referencePaiement>RF26020010</referencePaiement>", xml);
        Assert.Contains("</FactureHorsDelai>", xml);
        Assert.Contains("</listeFacturesHorsDelai>", xml);
        Assert.Contains("</DeclarationDelaiPaiement>", xml);

        // Ordre des tags racine (identifiantFiscal avant annee avant periode avant activite avant chiffreAffaire).
        var iIf = xml.IndexOf("<identifiantFiscal>", StringComparison.Ordinal);
        var iAn = xml.IndexOf("<annee>", StringComparison.Ordinal);
        var iPer = xml.IndexOf("<periode>", StringComparison.Ordinal);
        var iAct = xml.IndexOf("<activite>", StringComparison.Ordinal);
        var iCa = xml.IndexOf("<chiffreAffaire>", StringComparison.Ordinal);
        Assert.True(iIf < iAn && iAn < iPer && iPer < iAct && iAct < iCa);

        // Le XML produit doit rester bien formé.
        System.Xml.Linq.XDocument.Parse(xml);
    }

    [Fact]
    public void GenererXml_CasEnProcedure_TagDateJugementPresentEtPositionne()
    {
        var enTete = EnTeteNormal("DDP26070002");
        var enProcedure = new EnTeteDeclarationDelaiPaiementXml
        {
            Numero = enTete.Numero,
            Exercice = enTete.Exercice,
            PeriodeXml = enTete.PeriodeXml,
            PeriodeFichier = enTete.PeriodeFichier,
            IdentifiantFiscalSociete = enTete.IdentifiantFiscalSociete,
            ActiviteMarrocCode = 2, // EnProcedure
            DateJugement = new DateTime(2024, 5, 20),
            ChiffreAffaire = enTete.ChiffreAffaire
        };

        var modele = new DeclarationDelaiPaiementXmlModele { EnTete = enProcedure, Factures = new List<FactureHorsDelaiXml>() };

        _exporter.GenererXml(modele, _testDir);
        var xml = File.ReadAllText(Path.Combine(_testDir, "DDP26070002-2026-T1.xml"));

        Assert.Contains("<activite>2</activite>", xml);
        Assert.Contains("<dateJugementOuvrProc>2024-05-20</dateJugementOuvrProc>", xml);

        // La balise doit être entre <activite> et <chiffreAffaire> (position legacy).
        var iAct = xml.IndexOf("<activite>", StringComparison.Ordinal);
        var iJug = xml.IndexOf("<dateJugementOuvrProc>", StringComparison.Ordinal);
        var iCa = xml.IndexOf("<chiffreAffaire>", StringComparison.Ordinal);
        Assert.True(iAct < iJug && iJug < iCa);
    }

    [Fact]
    public void GenererXml_CasNormal_TagDateJugementAbsent()
    {
        var modele = new DeclarationDelaiPaiementXmlModele { EnTete = EnTeteNormal("DDP26070003"), Factures = new List<FactureHorsDelaiXml>() };

        _exporter.GenererXml(modele, _testDir);
        var xml = File.ReadAllText(Path.Combine(_testDir, "DDP26070003-2026-T1.xml"));

        Assert.DoesNotContain("dateJugementOuvrProc", xml);
    }

    [Fact]
    public void GenererXml_FactureNonPayeeDansLaPeriode_TroisBalisesOptionnellesAbsentes()
    {
        var facture = FactureStandard();
        var nonPayee = new FactureHorsDelaiXml
        {
            IdentifiantFiscalFournisseur = facture.IdentifiantFiscalFournisseur,
            NumRc = facture.NumRc,
            AdresseSiegeSocial = facture.AdresseSiegeSocial,
            NumFacture = facture.NumFacture,
            DateEmission = facture.DateEmission,
            DateLivraisonMarchandise = facture.DateLivraisonMarchandise,
            DateConvenuePaiementFacture = facture.DateConvenuePaiementFacture,
            MontantFactureTtc = facture.MontantFactureTtc,
            MontantNonEncorePaye = facture.MontantNonEncorePaye,
            MontantPayeHorsDelai = 0m,
            PayeeHorsDelaiPeriode = false,
            DatePaiementHorsDelai = null,
            ModePaiement = null,
            ReferencePaiement = null
        };

        var modele = new DeclarationDelaiPaiementXmlModele { EnTete = EnTeteNormal("DDP26070004"), Factures = new List<FactureHorsDelaiXml> { nonPayee } };
        _exporter.GenererXml(modele, _testDir);
        var xml = File.ReadAllText(Path.Combine(_testDir, "DDP26070004-2026-T1.xml"));

        Assert.DoesNotContain("<datePaiementHorsDelai>", xml);
        Assert.DoesNotContain("<modePaiement>", xml);
        Assert.DoesNotContain("<referencePaiement>", xml);
        Assert.Contains("<montantPayeHorsDelai>0</montantPayeHorsDelai>", xml);
    }

    [Theory]
    [InlineData(DeclarationDelaiPaiementLigneCalculator.CodeEspece, "1")]
    [InlineData(DeclarationDelaiPaiementLigneCalculator.CodeCheque, "2")]
    [InlineData(DeclarationDelaiPaiementLigneCalculator.CodeVirement, "4")]
    [InlineData(DeclarationDelaiPaiementLigneCalculator.CodeTraite, "5")]
    public void GenererXml_CodesModePaiement_Cdc(int code, string attendu)
    {
        var facture = FactureStandard();
        var avecCode = new FactureHorsDelaiXml
        {
            IdentifiantFiscalFournisseur = facture.IdentifiantFiscalFournisseur,
            NumRc = facture.NumRc,
            AdresseSiegeSocial = facture.AdresseSiegeSocial,
            NumFacture = facture.NumFacture,
            DateEmission = facture.DateEmission,
            DateLivraisonMarchandise = facture.DateLivraisonMarchandise,
            DateConvenuePaiementFacture = facture.DateConvenuePaiementFacture,
            MontantFactureTtc = facture.MontantFactureTtc,
            MontantNonEncorePaye = facture.MontantNonEncorePaye,
            MontantPayeHorsDelai = facture.MontantPayeHorsDelai,
            PayeeHorsDelaiPeriode = true,
            DatePaiementHorsDelai = facture.DatePaiementHorsDelai,
            ModePaiement = code,
            ReferencePaiement = facture.ReferencePaiement
        };

        var numero = "DDP-MODE-" + code;
        var modele = new DeclarationDelaiPaiementXmlModele
        {
            EnTete = EnTeteNormal(numero),
            Factures = new List<FactureHorsDelaiXml> { avecCode }
        };

        _exporter.GenererXml(modele, _testDir);
        var xml = File.ReadAllText(Path.Combine(_testDir, $"{numero}-2026-T1.xml"));
        Assert.Contains($"<modePaiement>{attendu}</modePaiement>", xml);
    }

    [Fact]
    public void GenererXml_ModePaiementNonMappe_BaliseVide()
    {
        var facture = FactureStandard();
        var sansMode = new FactureHorsDelaiXml
        {
            IdentifiantFiscalFournisseur = facture.IdentifiantFiscalFournisseur,
            NumRc = facture.NumRc,
            AdresseSiegeSocial = facture.AdresseSiegeSocial,
            NumFacture = facture.NumFacture,
            DateEmission = facture.DateEmission,
            DateLivraisonMarchandise = facture.DateLivraisonMarchandise,
            DateConvenuePaiementFacture = facture.DateConvenuePaiementFacture,
            MontantFactureTtc = facture.MontantFactureTtc,
            MontantNonEncorePaye = facture.MontantNonEncorePaye,
            MontantPayeHorsDelai = facture.MontantPayeHorsDelai,
            PayeeHorsDelaiPeriode = true,
            DatePaiementHorsDelai = facture.DatePaiementHorsDelai,
            ModePaiement = null,
            ReferencePaiement = facture.ReferencePaiement
        };

        var modele = new DeclarationDelaiPaiementXmlModele { EnTete = EnTeteNormal("DDP-MODE-VIDE"), Factures = new List<FactureHorsDelaiXml> { sansMode } };
        _exporter.GenererXml(modele, _testDir);
        var xml = File.ReadAllText(Path.Combine(_testDir, "DDP-MODE-VIDE-2026-T1.xml"));

        Assert.Contains("<modePaiement></modePaiement>", xml);
    }

    [Fact]
    public void GenererXml_FichierXmlExiste_LanceExceptionExplicite()
    {
        var modele = new DeclarationDelaiPaiementXmlModele { EnTete = EnTeteNormal("DDP-EXIST"), Factures = new List<FactureHorsDelaiXml>() };
        File.WriteAllText(Path.Combine(_testDir, "DDP-EXIST-2026-T1.xml"), "dummy");

        var ex = Assert.Throws<ApplicationException>(() => _exporter.GenererXml(modele, _testDir));
        Assert.Contains("existe déja", ex.Message);
    }

    [Fact]
    public void GenererXml_FichierZipExiste_LanceExceptionExplicite()
    {
        var modele = new DeclarationDelaiPaiementXmlModele { EnTete = EnTeteNormal("DDP-EXISTZIP"), Factures = new List<FactureHorsDelaiXml>() };
        File.WriteAllText(Path.Combine(_testDir, "DDP-EXISTZIP-2026-T1.zip"), "dummy");

        var ex = Assert.Throws<ApplicationException>(() => _exporter.GenererXml(modele, _testDir));
        Assert.Contains("existe déja", ex.Message);
    }

    [Fact]
    public void GenererXml_NomFichier_TrimestrielleEtAnnuelle()
    {
        var trimestrielle = new DeclarationDelaiPaiementXmlModele { EnTete = EnTeteNormal("DDP-NOM-T"), Factures = new List<FactureHorsDelaiXml>() };
        _exporter.GenererXml(trimestrielle, _testDir);
        Assert.True(File.Exists(Path.Combine(_testDir, "DDP-NOM-T-2026-T1.xml")));

        var annuelle = new EnTeteDeclarationDelaiPaiementXml
        {
            Numero = "DDP-NOM-A",
            Exercice = 2026,
            PeriodeXml = 5,
            PeriodeFichier = "A",
            IdentifiantFiscalSociete = "123456",
            ActiviteMarrocCode = 1
        };
        _exporter.GenererXml(new DeclarationDelaiPaiementXmlModele { EnTete = annuelle, Factures = new List<FactureHorsDelaiXml>() }, _testDir);
        Assert.True(File.Exists(Path.Combine(_testDir, "DDP-NOM-A-2026-A.xml")));
        Assert.Contains("<periode>5</periode>", File.ReadAllText(Path.Combine(_testDir, "DDP-NOM-A-2026-A.xml")));
    }

    [Fact]
    public void GenererXml_AdresseAvecEsperluette_EchappeeCorrectement_XmlBienForme()
    {
        var facture = FactureStandard();
        var avecEsperluette = new FactureHorsDelaiXml
        {
            IdentifiantFiscalFournisseur = facture.IdentifiantFiscalFournisseur,
            NumRc = facture.NumRc,
            AdresseSiegeSocial = "Société A & B, Rabat",
            NumFacture = facture.NumFacture,
            DateEmission = facture.DateEmission,
            DateLivraisonMarchandise = facture.DateLivraisonMarchandise,
            DateConvenuePaiementFacture = facture.DateConvenuePaiementFacture,
            MontantFactureTtc = facture.MontantFactureTtc,
            MontantNonEncorePaye = facture.MontantNonEncorePaye,
            MontantPayeHorsDelai = 0m,
            PayeeHorsDelaiPeriode = false
        };

        var modele = new DeclarationDelaiPaiementXmlModele { EnTete = EnTeteNormal("DDP-AMP"), Factures = new List<FactureHorsDelaiXml> { avecEsperluette } };
        _exporter.GenererXml(modele, _testDir);
        var xml = File.ReadAllText(Path.Combine(_testDir, "DDP-AMP-2026-T1.xml"));

        Assert.Contains("Société A &amp; B, Rabat", xml);
        System.Xml.Linq.XDocument.Parse(xml); // ne lève pas : bien formé.
    }
}
