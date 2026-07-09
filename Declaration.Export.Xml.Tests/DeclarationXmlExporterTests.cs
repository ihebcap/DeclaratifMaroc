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
            
            // Pas de déclaration XML
            Assert.False(xmlContent.StartsWith("<?xml"));
            
            // Format décimal et date
            Assert.Contains("<mht>1000.50</mht>", xmlContent);
            Assert.Contains("<tva>200.10</tva>", xmlContent);
            Assert.Contains("<ttc>1200.60</ttc>", xmlContent);
            Assert.Contains("<tx>20.00</tx>", xmlContent);
            Assert.Contains("<prorata>100.00</prorata>", xmlContent);
            Assert.Contains("<dpai>2023-10-15</dpai>", xmlContent);
            Assert.Contains("<dfac>2023-10-10</dfac>", xmlContent);
            
            // Mapping mode virement = 4
            Assert.Contains("<mp><id>4</id></mp>", xmlContent);
            
            // Ordre et structure (partiel)
            Assert.Contains("<ord>1</ord>", xmlContent);
            Assert.Contains("<des>Achat matériel</des>", xmlContent);
            Assert.Contains("<refF><if>12345678</if><nom>Fournisseur A</nom><ice>123456789012345</ice></refF>", xmlContent);
            
            // IsReport = true a été exclu
            Assert.DoesNotContain("F-002", xmlContent);
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
                        Tiers = new TiersInfo { IdentifiantFiscal = "123", Ice = "123456789012345" }
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
    }
}
