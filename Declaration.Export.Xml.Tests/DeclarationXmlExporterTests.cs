using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Xunit;
using Declaration.Core.Model;
using Declaration.Export.Xml;
using System.Collections.Generic;

namespace Declaration.Export.Xml.Tests
{
    public class DeclarationXmlExporterTests : IDisposable
    {
        private readonly string _testDir;
        private readonly DeclarationXmlExporter _exporter;

        public DeclarationXmlExporterTests()
        {
            _testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDir);
            _exporter = new DeclarationXmlExporter();
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDir))
            {
                Directory.Delete(_testDir, true);
            }
        }

        [Fact]
        public void GenererXml_Succes_CreeXmlEtZipAvecBonFormat()
        {
            // Arrange
            var modele = new DeclarationModele
            {
                EnTete = new EnTeteDeclaration
                {
                    IdentifiantSociete = "12345678",
                    Exercice = 2023,
                    Type = TypePeriode.Mensuelle,
                    MoisPeriode = 10,
                    Numero = "DEC-001"
                },
                Lignes = new List<LigneDeclarationEnrichie>
                {
                    new LigneDeclarationEnrichie
                    {
                        NumeroFacture = "F-001",
                        Designation = "Achat matériel",
                        Tiers = new TiersInfo { IdentifiantFiscal = "12345678", Ice = "123456789012345", Nom = "Fournisseur A" },
                        HT = 1000.50m,
                        Tva = 200.10m,
                        Ttc = 1200.60m,
                        Prorata = 100m,
                        Taux = 20m,
                        ModePaiement = "Virement", // 4
                        DatePaiement = new DateTime(2023, 10, 15),
                        DateFacture = new DateTime(2023, 10, 10),
                        IsReport = false
                    },
                    new LigneDeclarationEnrichie
                    {
                        NumeroFacture = "F-002",
                        IsReport = true // Doit être exclu
                    }
                }
            };

            // Act
            string zipPath = _exporter.GenererXml(modele, _testDir);

            // Assert
            Assert.True(File.Exists(zipPath));
            string xmlPath = Path.Combine(_testDir, "DEC-001-2023-M10.xml");
            Assert.True(File.Exists(xmlPath));

            string xmlContent = File.ReadAllText(xmlPath);

            // Prolog XML + xmlns:xsi obligatoires (CDC §2.1)
            Assert.StartsWith("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>", xmlContent);
            Assert.Contains("<DeclarationReleveDeduction xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">", xmlContent);

            // Aucune balise <prorata> (CDC §2.2)
            Assert.DoesNotContain("<prorata>", xmlContent);

            // Format décimal sans padding de zéros (CDC §2.5) et date
            Assert.Contains("<mht>1000.5</mht>", xmlContent);
            Assert.Contains("<tva>200.1</tva>", xmlContent);
            Assert.Contains("<ttc>1200.6</ttc>", xmlContent);
            // tx en fraction décimale (0.2 pour 20 %) (CDC §2.2/§2.5)
            Assert.Contains("<tx>0.2</tx>", xmlContent);
            Assert.Contains("<dpai>2023-10-15</dpai>", xmlContent);
            Assert.Contains("<dfac>2023-10-10</dfac>", xmlContent);
            
            // Mapping mode virement = 4
            Assert.Contains("<mp><id>4</id></mp>", xmlContent);
            
            // Ordre et structure (partiel)
            Assert.Contains("<ord>1</ord>", xmlContent);
            Assert.Contains("<des>Achat marchandise</des>", xmlContent);
            Assert.Contains("<refF><if>12345678</if><nom>Fournisseur A</nom><ice>123456789012345</ice></refF>", xmlContent);
            
            // IsReport = true a été exclu
            Assert.DoesNotContain("F-002", xmlContent);
        }

        [Fact]
        public void GenererXml_ExclutEncaissementClient()
        {
            // Arrange
            var modele = new DeclarationModele
            {
                EnTete = new EnTeteDeclaration
                {
                    IdentifiantSociete = "12345678",
                    Exercice = 2023,
                    Type = TypePeriode.Mensuelle,
                    MoisPeriode = 10,
                    Numero = "DEC-002"
                },
                Lignes = new List<LigneDeclarationEnrichie>
                {
                    new LigneDeclarationEnrichie
                    {
                        NumeroFacture = "F-001",
                        Designation = "Achat matériel",
                        Tiers = new TiersInfo { IdentifiantFiscal = "12345678", Ice = "123456789012345", Nom = "Fournisseur A" },
                        HT = 1000.50m,
                        Tva = 200.10m,
                        Ttc = 1200.60m,
                        Prorata = 100m,
                        Taux = 20m,
                        ModePaiement = "Virement",
                        DatePaiement = new DateTime(2023, 10, 15),
                        DateFacture = new DateTime(2023, 10, 10),
                        Source = SourceAffectation.Decaissement,
                        IsReport = false
                    },
                    new LigneDeclarationEnrichie
                    {
                        NumeroFacture = "F-003",
                        Designation = "Vente client",
                        Tiers = new TiersInfo { IdentifiantFiscal = "12345678", Ice = "123456789012345", Nom = "Client B" },
                        HT = 500m,
                        Tva = 100m,
                        Ttc = 600m,
                        Prorata = 100m,
                        Taux = 20m,
                        ModePaiement = "Virement",
                        DatePaiement = new DateTime(2023, 10, 16),
                        DateFacture = new DateTime(2023, 10, 11),
                        Source = SourceAffectation.Encaissement,
                        IsReport = false
                    }
                }
            };

            // Act
            string zipPath = _exporter.GenererXml(modele, _testDir);

            // Assert
            Assert.True(File.Exists(zipPath));
            string xmlPath = Path.Combine(_testDir, "DEC-002-2023-M10.xml");
            Assert.True(File.Exists(xmlPath));

            string xmlContent = File.ReadAllText(xmlPath);
            Assert.Contains("F-001", xmlContent);
            Assert.DoesNotContain("F-003", xmlContent);
        }

        [Fact]
        public void GenererXml_IfInvalide_LanceException()
        {
            var modele = new DeclarationModele
            {
                EnTete = new EnTeteDeclaration(),
                Lignes = new List<LigneDeclarationEnrichie>
                {
                    new LigneDeclarationEnrichie
                    {
                        Tiers = new TiersInfo { IdentifiantFiscal = "12 45", Ice = "123456789012345" } // Contient un espace
                    }
                }
            };

            var ex = Assert.Throws<ApplicationException>(() => _exporter.GenererXml(modele, _testDir));
            Assert.Contains("L'identifiant fiscal du tiers est invalide", ex.Message);
        }

        [Fact]
        public void GenererXml_IceInvalide_LanceException()
        {
            var modele = new DeclarationModele
            {
                EnTete = new EnTeteDeclaration(),
                Lignes = new List<LigneDeclarationEnrichie>
                {
                    new LigneDeclarationEnrichie
                    {
                        Tiers = new TiersInfo { IdentifiantFiscal = "12345678", Ice = "123 45678901234" } // Contient un espace
                    }
                }
            };

            var ex = Assert.Throws<ApplicationException>(() => _exporter.GenererXml(modele, _testDir));
            Assert.Contains("L'ICE du tiers est invalide", ex.Message);
        }

        [Fact]
        public void GenererXml_FichierExiste_LanceException()
        {
            var modele = new DeclarationModele
            {
                EnTete = new EnTeteDeclaration { Exercice = 2023, Type = TypePeriode.Mensuelle, MoisPeriode = 10, Numero = "D01" },
                Lignes = new List<LigneDeclarationEnrichie>
                {
                    new LigneDeclarationEnrichie { Tiers = new TiersInfo { IdentifiantFiscal = "12345678", Ice = "123456789012345" } }
                }
            };

            string expectedPath = Path.Combine(_testDir, "D01-2023-M10.xml");
            File.WriteAllText(expectedPath, "dummy");

            var ex = Assert.Throws<ApplicationException>(() => _exporter.GenererXml(modele, _testDir));
            Assert.Contains("existe déjà", ex.Message);
        }

        private static DeclarationModele ModeleAvecLigne(LigneDeclarationEnrichie ligne, string numero)
        {
            return new DeclarationModele
            {
                EnTete = new EnTeteDeclaration
                {
                    IdentifiantSociete = "12345678",
                    Exercice = 2025,
                    Type = TypePeriode.Mensuelle,
                    MoisPeriode = 3,
                    Numero = numero
                },
                Lignes = new List<LigneDeclarationEnrichie> { ligne }
            };
        }

        private static LigneDeclarationEnrichie LigneStandard()
        {
            return new LigneDeclarationEnrichie
            {
                NumeroFacture = "FACZ001",
                Designation = "Marchandises",
                Tiers = new TiersInfo { IdentifiantFiscal = "12345678", Ice = "123456789012345", Nom = "Fournisseur A" },
                HT = 1000m,
                Tva = 200m,
                Ttc = 1200m,
                Taux = 20m,
                ModePaiement = "Virement",
                DatePaiement = new DateTime(2025, 3, 9),
                DateFacture = new DateTime(2025, 3, 9),
                IsReport = false
            };
        }

        private string LireXml(DeclarationModele modele, string numero)
        {
            _exporter.GenererXml(modele, _testDir);
            return File.ReadAllText(Path.Combine(_testDir, $"{numero}-2025-M3.xml"));
        }

        [Theory]
        [InlineData(20, "<tx>0.2</tx>")]
        [InlineData(10, "<tx>0.1</tx>")]
        [InlineData(8, "<tx>0.08</tx>")]
        public void GenererXml_Taux_ExporteEnFractionDecimale(int taux, string attendu)
        {
            var ligne = LigneStandard();
            ligne.Taux = taux;
            var xml = LireXml(ModeleAvecLigne(ligne, "TX-" + taux), "TX-" + taux);
            Assert.Contains(attendu, xml);
        }

        [Fact]
        public void GenererXml_MontantEntier_SansDecimale()
        {
            var ligne = LigneStandard();
            ligne.HT = 1082.5m;
            ligne.Tva = 216.5m;
            ligne.Ttc = 1299m; // entier
            var xml = LireXml(ModeleAvecLigne(ligne, "ENT-1"), "ENT-1");
            Assert.Contains("<mht>1082.5</mht>", xml);
            Assert.Contains("<tva>216.5</tva>", xml);
            Assert.Contains("<ttc>1299</ttc>", xml); // pas 1299.00
        }

        [Fact]
        public void GenererXml_IfSeptChiffresEtIceNonStandard_NeLancePas()
        {
            var ligne = LigneStandard();
            ligne.Tiers = new TiersInfo { IdentifiantFiscal = "1084334", Ice = "001544256000053", Nom = "KITEA" };
            var xml = LireXml(ModeleAvecLigne(ligne, "IF7-1"), "IF7-1");
            Assert.Contains("<if>1084334</if>", xml);
            Assert.Contains("<ice>001544256000053</ice>", xml);
        }

        [Fact]
        public void GenererXml_Avoir_MontantsNegatifsPropagesSansTransformation()
        {
            var ligne = LigneStandard();
            ligne.NumeroFacture = "AV2404269";
            ligne.HT = -15840m;
            ligne.Tva = -3168m;
            ligne.Ttc = -19008m;
            ligne.Taux = 20m;
            var xml = LireXml(ModeleAvecLigne(ligne, "AV-1"), "AV-1");
            Assert.Contains("<mht>-15840</mht>", xml);
            Assert.Contains("<tva>-3168</tva>", xml);
            Assert.Contains("<ttc>-19008</ttc>", xml);
            Assert.Contains("<tx>0.2</tx>", xml); // le taux reste positif
        }

        [Fact]
        public void GenererXml_RaisonSocialeAvecEsperluette_EchappeeCorrectement()
        {
            var ligne = LigneStandard();
            ligne.Tiers = new TiersInfo { IdentifiantFiscal = "12345678", Ice = "123456789012345", Nom = "A & B SARL" };
            var xml = LireXml(ModeleAvecLigne(ligne, "AMP-1"), "AMP-1");
            Assert.Contains("<nom>A &amp; B SARL</nom>", xml);
            Assert.DoesNotContain("<nom>A & B SARL</nom>", xml);
            // Le XML produit doit rester bien formé
            System.Xml.Linq.XDocument.Parse(xml);
        }

        [Fact]
        public void GenererXml_EspaceResiduel_EstTrimme()
        {
            var ligne = LigneStandard();
            ligne.NumeroFacture = "  FACZ001 ";
            ligne.Designation = " Marchandises ";
            var xml = LireXml(ModeleAvecLigne(ligne, "TRIM-1"), "TRIM-1");
            Assert.Contains("<num>FACZ001</num>", xml);
            Assert.Contains("<des>Achat marchandise</des>", xml);
        }
    }
}
