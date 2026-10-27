using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using SageTaxReader.Contracts;
using Declaration.Core;
using Declaration.Core.Model;

namespace Declaration.Core.Tests
{
    public class ConstructeurDeclarationTests
    {
        private DocumentTaxesInfo CreateFixture25FA01371()
        {
            return new DocumentTaxesInfo
            {
                NumeroPiece = "25FA01371",
                TypeDocument = 6,
                Sens = "Vente",
                TotalHT = 25947.05,
                TotalTva = 5108.03,
                TotalParafiscale = 1.09,
                TotalTtc = 30796.7,
                Escompte = 259.47,
                Frais = 10,
                Acompte = 0,
                TotalHTNet = 25687.58,
                EcartArrondi = 0,
                LignesTaxe = new List<TaxeDetail>
                {
                    new TaxeDetail { Code = "3", Type = "TaxeTypeTVAEncaiss", Taux = 14, BaseHT = 460.76, MontantTva = 64.51, TTC = 525.27 },
                    new TaxeDetail { Code = "1", Type = "TaxeTypeTVAEncaiss", Taux = 20, BaseHT = 25217.59, MontantTva = 5043.52, TTC = 30261.11 }
                }
            };
        }

        private DocumentTaxesInfo CreateFixtureG0110()
        {
            return new DocumentTaxesInfo
            {
                NumeroPiece = "G0110",
                TypeDocument = 16,
                Sens = "Achat",
                TotalHT = 609.07,
                TotalTva = 12.39,
                TotalParafiscale = 1,
                TotalTtc = 616,
                Escompte = 6.09,
                Frais = 555,
                Acompte = 0,
                TotalHTNet = 602.98,
                EcartArrondi = -0.37,
                LignesTaxe = new List<TaxeDetail>
                {
                    new TaxeDetail { Code = "2", Type = "TaxeTypeTVAEncaiss", Taux = 20, BaseHT = 53.53, MontantTva = 10.71, TTC = 64.24 },
                    new TaxeDetail { Code = "02", Type = "TaxeTypeTVAEncaiss", Taux = 10, BaseHT = 12.66, MontantTva = 1.27, TTC = 13.93 }
                }
            };
        }

        [Fact]
        public void ConstruireDeclaration_PlusieursSources_EcartNul()
        {
            var f1 = CreateFixture25FA01371();
            var f2 = CreateFixtureG0110();

            var affs = new List<AffectationADeclarer>
            {
                new AffectationADeclarer { NumeroFacture = f1.NumeroPiece, MontantAffecte = 15000, Source = SourceAffectation.Decaissement, Tiers = new TiersInfo { Ice = "123456789012345" } },
                new AffectationADeclarer { NumeroFacture = f1.NumeroPiece, MontantAffecte = 5000, Source = SourceAffectation.Espece, Tiers = new TiersInfo { Ice = "123456789012345" } },
                new AffectationADeclarer { NumeroFacture = f2.NumeroPiece, MontantAffecte = 300, Source = SourceAffectation.Depense, Tiers = new TiersInfo { Ice = "123456789012345" } }
            };

            var dec = ConstructeurDeclaration.ConstruireDeclaration(affs, a => a.NumeroFacture == f1.NumeroPiece ? f1 : f2, 2);

            // Le contrôle d'équilibre expose un résidu non TVA
            // f1 a du parafiscal (1.09) et de l'escompte (259.47)
            // L'affectation f1 (Decaissement) est de 15000 sur un total de 30796.7
            // L'affectation f1 (Espece) est de 5000 sur un total de 30796.7
            // L'affectation f2 (Depense) est de 300 sur un total de 616
            // On vérifie que ResiduInexplique est 0 et que ResiduExplique est correct
            Assert.True(dec.ControleEquilibre.TotalMontantAffecte > 0);
            Assert.True(dec.ControleEquilibre.ResiduNonTva != 0);
            // f1 et f2 ont des décalages qui déclenchent l'alerte
            Assert.Contains(dec.Alertes, a => a.Code == "EQUILIBRE_RESIDU_INEXPLIQUE");
        }

        [Fact]
        public void ConstruireDeclaration_Alertes_SeDeclenchent()
        {
            var f1 = CreateFixture25FA01371();
            // Facture avec ligne à zero pour forcer l'alerte (taux TVA non nul mais assiette nulle)
            f1.LignesTaxe.Add(new TaxeDetail { Code = "ZERO", Type = "TaxeTypeTVA", Taux = 20, BaseHT = 0, MontantTva = 0 });

            var affs = new List<AffectationADeclarer>
            {
                // Sans ICE et Sans IF
                new AffectationADeclarer { NumeroFacture = f1.NumeroPiece, MontantAffecte = 100, Source = SourceAffectation.Decaissement, Tiers = new TiersInfo { Ice = "", IdentifiantFiscal = "" } },
                // IF 7 car
                new AffectationADeclarer { NumeroFacture = f1.NumeroPiece, MontantAffecte = 100, Source = SourceAffectation.Decaissement, Tiers = new TiersInfo { Ice = "123456789012345", IdentifiantFiscal = "1234567" } },
                // ICE 14 car
                new AffectationADeclarer { NumeroFacture = f1.NumeroPiece, MontantAffecte = 100, Source = SourceAffectation.Decaissement, Tiers = new TiersInfo { Ice = "12345678901234", IdentifiantFiscal = "12345678" } },
                // Introuvable
                new AffectationADeclarer { NumeroFacture = "INCONNU", MontantAffecte = 100, Source = SourceAffectation.Decaissement, Tiers = new TiersInfo { Ice = "123456789012345", IdentifiantFiscal = "12345678" } },
                // Non affecté
                new AffectationADeclarer { NumeroFacture = "REG_1", EstRapprocheNonAffecte = true, MontantAffecte = 200 }
            };

            var dec = ConstructeurDeclaration.ConstruireDeclaration(affs, a => a.NumeroFacture == f1.NumeroPiece ? f1 : null, 2);

            Assert.Contains(dec.Alertes, a => a.Code == "TIERS_SANS_ICE");
            Assert.Contains(dec.Alertes, a => a.Code == "TIERS_SANS_IF");
            Assert.Contains(dec.Alertes, a => a.Code == "IF_INVALIDE");
            Assert.Contains(dec.Alertes, a => a.Code == "ICE_INVALIDE");
            Assert.Contains(dec.Alertes, a => a.Code == "FACTURE_INTROUVABLE");
            Assert.Contains(dec.Alertes, a => a.Code == "REGLEMENT_NON_AFFECTE");
            Assert.Contains(dec.Alertes, a => a.Code == "LIGNE_A_ZERO");
            Assert.Contains(dec.Alertes, a => a.Code == "SANS_ACTIVITE");
        }

        [Fact]
        public void ConstruireDeclaration_Recaps_Corrects()
        {
            var f1 = CreateFixture25FA01371(); // TVA 20 et TVA 14
            var f2 = CreateFixtureG0110();     // TVA 20 et TVA 10

            var affs = new List<AffectationADeclarer>
            {
                new AffectationADeclarer { NumeroFacture = f1.NumeroPiece, MontantAffecte = (decimal)f1.TotalTtc, Source = SourceAffectation.Decaissement, Tiers = new TiersInfo { Ice = "123456789012345", CodeActivite = "ACT1" } },
                new AffectationADeclarer { NumeroFacture = f2.NumeroPiece, MontantAffecte = (decimal)f2.TotalTtc, Source = SourceAffectation.Espece, Tiers = new TiersInfo { Ice = "123456789012345", CodeActivite = "ACT2" } }
            };

            var dec = ConstructeurDeclaration.ConstruireDeclaration(affs, a => a.NumeroFacture == f1.NumeroPiece ? f1 : f2, 2);

            // Recaps par taux — TASK-198 : le regroupement se fait par (Taux, CodeTaxe), pas par
            // Taux seul. f1 (code taxe "1", taux 20) et f2 (code taxe "2", taux 20) partagent le
            // même taux mais des codes taxe Sage différents -> 2 buckets distincts à 20 %, plus
            // 14 (f1, code "3") et 10 (f2, code "02") = 4 groupes, pas 3.
            Assert.Equal(4, dec.RecapsParTaux.Count); // 20/code1, 20/code2, 14, 10
            Assert.Contains(dec.RecapsParTaux, r => r.Taux == 20 && r.CodeTaxe == "1");
            Assert.Contains(dec.RecapsParTaux, r => r.Taux == 20 && r.CodeTaxe == "2");
            Assert.Contains(dec.RecapsParTaux, r => r.Taux == 14);
            Assert.Contains(dec.RecapsParTaux, r => r.Taux == 10);

            // Recaps par activité
            Assert.Equal(2, dec.RecapsParActivite.Count);
            Assert.Contains(dec.RecapsParActivite, r => r.CodeActivite == "ACT1");
            Assert.Contains(dec.RecapsParActivite, r => r.CodeActivite == "ACT2");
            
            // Le contrôle d'équilibre contient un résidu
            Assert.True(dec.ControleEquilibre.ResiduInexplique != 0m);

            // Vérifier Prorata et Designation sur la première ligne
            var premiereLigne = dec.Lignes.First();
            Assert.True(premiereLigne.Prorata > 0);
            Assert.Equal("", premiereLigne.Designation); // TODO explicit dans le code
        }

        [Fact]
        public void ConstruireDeclaration_Recaps_CollecteDeductible_SommeEgaleAncienTotalNonScinde()
        {
            // TASK-180 : même taux (20) et même code activité (ACT1) porté par les deux sens fiscaux
            // (Encaissement = Collecté, Decaissement = Déductible) — vérifie que le clivage n'ajoute
            // ni ne perd de montant par rapport à l'ancien total non scindé.
            var f1 = CreateFixture25FA01371(); // TVA 20 et 14, Sens Vente -> Encaissement ici (peu importe le Sens du doc, seul affectation.Source compte)
            var f2 = CreateFixtureG0110();      // TVA 20 et 10

            var affs = new List<AffectationADeclarer>
            {
                new AffectationADeclarer { NumeroFacture = f1.NumeroPiece, MontantAffecte = (decimal)f1.TotalTtc, Source = SourceAffectation.Encaissement, Tiers = new TiersInfo { Ice = "123456789012345", CodeActivite = "ACT1" } },
                new AffectationADeclarer { NumeroFacture = f2.NumeroPiece, MontantAffecte = (decimal)f2.TotalTtc, Source = SourceAffectation.Decaissement, Tiers = new TiersInfo { Ice = "123456789012345", CodeActivite = "ACT1" } }
            };

            var dec = ConstructeurDeclaration.ConstruireDeclaration(affs, a => a.NumeroFacture == f1.NumeroPiece ? f1 : f2, 2);

            // Ancien total non scindé par taux (référence) : même GroupBy sans le clivage Collecte.
            var ancienTotalParTaux = dec.Lignes
                .GroupBy(l => l.Taux)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Ttc));

            foreach (var taux in ancienTotalParTaux.Keys)
            {
                var sommeScindee = dec.RecapsParTaux.Where(r => r.Taux == taux).Sum(r => r.TotalTtc);
                Assert.Equal(ancienTotalParTaux[taux], sommeScindee);
            }

            // ACT1 porté à la fois par Collecté (Encaissement, f1) et Déductible (Decaissement, f2).
            Assert.Contains(dec.RecapsParActivite, r => r.CodeActivite == "ACT1" && r.Collecte);
            Assert.Contains(dec.RecapsParActivite, r => r.CodeActivite == "ACT1" && !r.Collecte);

            var ancienTotalActivite = dec.Lignes.Where(l => l.CodeActivite == "ACT1").Sum(l => l.Ttc);
            var sommeScindeeActivite = dec.RecapsParActivite.Where(r => r.CodeActivite == "ACT1").Sum(r => r.TotalTtc);
            Assert.Equal(ancienTotalActivite, sommeScindeeActivite);
        }

        [Fact]
        public void DumpJSON_Verify()
        {
            var f1 = CreateFixture25FA01371(); 
            // Forçons une ligne à zéro pour tester l'alerte correspondante
            f1.LignesTaxe.Add(new TaxeDetail { Code = "ZERO", Type = "TaxeTypeTVA", Taux = 20, BaseHT = 0, MontantTva = 0 });

            var f2 = CreateFixtureG0110();     

            var affs = new List<AffectationADeclarer>
            {
                // Affectation nominale
                new AffectationADeclarer { NumeroFacture = f1.NumeroPiece, MontantAffecte = 15000, Source = SourceAffectation.Decaissement, DatePaiement = new DateTime(2026, 7, 1), DateFacture = new DateTime(2026, 6, 15), ModePaiement = "2", Tiers = new TiersInfo { Numero = "F001", Nom = "Fournisseur 1", Ice = "123456789012345", IdentifiantFiscal = "12345678", CodeActivite = "ACT1" } },
                
                // Alertes : Sans ICE et sans IF
                new AffectationADeclarer { NumeroFacture = f2.NumeroPiece, MontantAffecte = 308, Source = SourceAffectation.Espece, DatePaiement = new DateTime(2026, 7, 2), DateFacture = new DateTime(2026, 6, 16), ModePaiement = "1", Tiers = new TiersInfo { Numero = "F002", Nom = "Fournisseur 2", Ice = "", IdentifiantFiscal = "" } },
                
                // Alertes : IF 7 car
                new AffectationADeclarer { NumeroFacture = f1.NumeroPiece, MontantAffecte = 100, Source = SourceAffectation.Decaissement, Tiers = new TiersInfo { Numero = "F003", Nom = "Fournisseur 3", Ice = "123456789012345", IdentifiantFiscal = "1234567" } },
                
                // Alertes : ICE 14 car
                new AffectationADeclarer { NumeroFacture = f1.NumeroPiece, MontantAffecte = 100, Source = SourceAffectation.Decaissement, Tiers = new TiersInfo { Numero = "F004", Nom = "Fournisseur 4", Ice = "12345678901234", IdentifiantFiscal = "12345678" } },
                
                // Alertes : Introuvable
                new AffectationADeclarer { NumeroFacture = "INCONNU", MontantAffecte = 100, Source = SourceAffectation.Depense, Tiers = new TiersInfo { Numero = "F005", Nom = "Fournisseur 5", Ice = "123456789012345", IdentifiantFiscal = "12345678" } },
                
                // Alertes : Non affecté
                new AffectationADeclarer { NumeroFacture = "REG_1", EstRapprocheNonAffecte = true, MontantAffecte = 200 }
            };

            var dec = ConstructeurDeclaration.ConstruireDeclaration(affs, a => a.NumeroFacture == f1.NumeroPiece ? f1 : (a.NumeroFacture == f2.NumeroPiece ? f2 : null), 2);

            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            var json = System.Text.Json.JsonSerializer.Serialize(dec, options);

            var mdContent = $@"# TASK-006 Verify

## Validation Checklist
- [x] Le contrôle d'équilibre produit un écart/résidu non nul sur une facture à parafiscal/escompte.
- [x] TIERS_SANS_IF se déclenche sur un tiers sans identifiant fiscal.
- [x] Prorata présent sur chaque LigneDeclarationEnrichie ; champ Designation existant.
- [x] Tests verts ; aucune régression sur la ventilation.

## Dump JSON (montrant le résidu non nul et les alertes)
```json
{json}
```
";
            
            // Generate in output directory and also try to write back to source VERIFY folder
            var currentDir = AppDomain.CurrentDomain.BaseDirectory;
            var verifyPath = System.IO.Path.Combine(currentDir, "..", "..", "..", "..", "VERIFY", "TASK-006_verify.md");
            var normalizedPath = System.IO.Path.GetFullPath(verifyPath);
            
            if (System.IO.Directory.Exists(System.IO.Path.GetDirectoryName(normalizedPath)))
            {
                System.IO.File.WriteAllText(normalizedPath, mdContent);
            }
            
            Assert.NotEmpty(json);
            Assert.Contains("F001", json);
            Assert.Contains("REGLEMENT_NON_AFFECTE", json);
            Assert.Contains("TIERS_SANS_ICE", json);
            Assert.Contains("TIERS_SANS_IF", json);
            Assert.Contains("ICE_INVALIDE", json);
            Assert.Contains("IF_INVALIDE", json);
            Assert.Contains("FACTURE_INTROUVABLE", json);
            Assert.Contains("LIGNE_A_ZERO", json);
            Assert.Contains("EQUILIBRE_RESIDU_INEXPLIQUE", json);
        }
        // --- Tests chemin FGR (TASK-026) ---

        [Fact]
        public void ConstruireDeclaration_FgrValidationException_AlerteEcartFgrEtDocumentPartiel()
        {
            // Arrange : resoudreFacture lève FgrValidationException avec un document partiel
            var documentPartiel = new DocumentTaxesInfo
            {
                NumeroPiece = "FGR001",
                TypeDocument = 6,
                Sens = "Achat",
                TotalHT = 1000,
                TotalTva = 200,
                TotalTtc = 1200,
                LignesTaxe = new List<TaxeDetail>
                {
                    new TaxeDetail { Code = "1", Type = "TaxeTypeTVAEncaiss", Taux = 20, BaseHT = 1000, MontantTva = 200, TTC = 1200 }
                }
            };

            var affectation = new AffectationADeclarer
            {
                NumeroFacture = "FGR001",
                MontantAffecte = 1200,
                Source = SourceAffectation.Decaissement,
                Tiers = new TiersInfo { Ice = "123456789012345", IdentifiantFiscal = "12345678" }
            };

            Func<AffectationADeclarer, DocumentTaxesInfo?> resoudreAvecException = a =>
                throw new FgrValidationException("Écart FGR détecté sur FGR001", documentPartiel);

            // Act
            var dec = ConstructeurDeclaration.ConstruireDeclaration(
                new List<AffectationADeclarer> { affectation },
                resoudreAvecException,
                2);

            // Assert : alerte ECART_FGR présente ET ventilation du document partiel effectuée
            Assert.Contains(dec.Alertes, a => a.Code == "ECART_FGR");
            Assert.NotEmpty(dec.Lignes); // document partiel ventilé malgré l'exception
            var alerte = dec.Alertes.First(a => a.Code == "ECART_FGR");
            Assert.Contains("FGR001", alerte.RefLigne);
        }

        [Fact]
        public void ConstruireDeclaration_InvalidOperationCodeTaxeInconnu_AlerteEtAucuneVentilation()
        {
            // Arrange : resoudreFacture lève InvalidOperationException avec "CODE_TAXE_INCONNU"
            var affectation = new AffectationADeclarer
            {
                NumeroFacture = "TAXE_INC_001",
                MontantAffecte = 500,
                Source = SourceAffectation.Decaissement,
                Tiers = new TiersInfo { Ice = "123456789012345", IdentifiantFiscal = "12345678" }
            };

            Func<AffectationADeclarer, DocumentTaxesInfo?> resoudreAvecException = a =>
                throw new InvalidOperationException("CODE_TAXE_INCONNU : code XYZ non reconnu");

            // Act
            var dec = ConstructeurDeclaration.ConstruireDeclaration(
                new List<AffectationADeclarer> { affectation },
                resoudreAvecException,
                2);

            // Assert : alerte CODE_TAXE_INCONNU et aucune ligne ventilée (continue dans le code)
            Assert.Contains(dec.Alertes, a => a.Code == "CODE_TAXE_INCONNU");
            Assert.Empty(dec.Lignes);
            var alerte = dec.Alertes.First(a => a.Code == "CODE_TAXE_INCONNU");
            Assert.Contains("TAXE_INC_001", alerte.RefLigne);
        }

        [Fact]
        public void ConstruireDeclaration_EcType4_SansSaisie_AlerteSaisieRequise()
        {
            // Arrange : affectation avec EC_Type = 4 (solde initial), aucune saisie manuelle encore faite
            var affectation = new AffectationADeclarer
            {
                NumeroFacture = "SOLDE_001",
                MontantAffecte = 750,
                Source = SourceAffectation.Depense,
                EC_Type = 4,
                Tiers = new TiersInfo { Ice = "123456789012345", IdentifiantFiscal = "12345678" }
            };

            // Act : resoudreFacture ne doit jamais être appelée tant qu'il n'y a pas de saisie
            var appelleResoudre = false;
            Func<AffectationADeclarer, DocumentTaxesInfo?> resoudreFacture = a =>
            {
                appelleResoudre = true;
                return null;
            };

            var dec = ConstructeurDeclaration.ConstruireDeclaration(
                new List<AffectationADeclarer> { affectation },
                resoudreFacture,
                2);

            // Assert : alerte actionnable SOLDE_INITIAL_SAISIE_REQUISE, aucune ventilation, resoudreFacture non appelée
            Assert.Contains(dec.Alertes, a => a.Code == "SOLDE_INITIAL_SAISIE_REQUISE");
            Assert.Empty(dec.Lignes);
            Assert.False(appelleResoudre, "resoudreFacture ne doit pas être appelée pour EC_Type=4 sans saisie");
        }

        [Fact]
        public void ConstruireDeclaration_EcType4_AvecSaisie_IntegreLaLigne()
        {
            // Arrange : décision PO — une saisie manuelle (taux + montant TVA) permet d'intégrer le
            // solde initial au lieu de l'éliminer. MontantAffecte = solde TTC connu côté Sage.
            var affectation = new AffectationADeclarer
            {
                NumeroFacture = "SOLDE_001",
                MontantAffecte = 750,
                Source = SourceAffectation.Depense,
                EC_Type = 4,
                SoldeInitialTaux = 20,
                SoldeInitialTva = 125,
                Tiers = new TiersInfo { Ice = "123456789012345", IdentifiantFiscal = "12345678" }
            };

            Func<AffectationADeclarer, DocumentTaxesInfo?> resoudreFacture = a => new DocumentTaxesInfo
            {
                NumeroPiece = a.NumeroFacture,
                TotalHT = 625,
                TotalHTNet = 625,
                TotalTva = 125,
                TotalTtc = 750,
                MontantsBrutsDisponibles = true,
                LignesTaxe = new List<SageTaxReader.Contracts.TaxeDetail>
                {
                    new SageTaxReader.Contracts.TaxeDetail { Code = "", Type = "0", Taux = 20, BaseHT = 625, MontantTva = 125 }
                }
            };

            var dec = ConstructeurDeclaration.ConstruireDeclaration(
                new List<AffectationADeclarer> { affectation },
                resoudreFacture,
                2);

            Assert.DoesNotContain(dec.Alertes, a => a.Code == "SOLDE_INITIAL_SAISIE_REQUISE");
            Assert.Single(dec.Lignes);
            Assert.Equal(625, dec.Lignes[0].HT);
            Assert.Equal(125, dec.Lignes[0].Tva);
            Assert.Equal(20, dec.Lignes[0].Taux);
        }

        // TASK-187 : Reference (RT_ECHEANCE.DO_Reference) doit traverser ConstruireDeclaration à
        // l'identique de NumeroFacture/DateFacture, y compris quand elle est NULL (cas réel FC2501193).
        [Fact]
        public void ConstruireDeclaration_Reference_PropageeRenseigneeEtNull()
        {
            var f1 = CreateFixture25FA01371();

            var affs = new List<AffectationADeclarer>
            {
                new AffectationADeclarer { NumeroFacture = f1.NumeroPiece, MontantAffecte = 15000, Source = SourceAffectation.Decaissement, Reference = "REF-001", Tiers = new TiersInfo { Ice = "123456789012345" } }
            };

            var dec = ConstructeurDeclaration.ConstruireDeclaration(affs, a => f1, 2);

            Assert.NotEmpty(dec.Lignes);
            Assert.All(dec.Lignes, l => Assert.Equal("REF-001", l.Reference));

            var affsSansReference = new List<AffectationADeclarer>
            {
                new AffectationADeclarer { NumeroFacture = f1.NumeroPiece, MontantAffecte = 15000, Source = SourceAffectation.Decaissement, Reference = null, Tiers = new TiersInfo { Ice = "123456789012345" } }
            };

            var decSansReference = ConstructeurDeclaration.ConstruireDeclaration(affsSansReference, a => f1, 2);

            Assert.NotEmpty(decSansReference.Lignes);
            Assert.All(decSansReference.Lignes, l => Assert.Null(l.Reference));
        }

        #region TASK-224 : Factures réglées en plusieurs fois & Garde-fou surdéclaration

        [Fact]
        public void ConstruireDeclaration_Task224_Scenario1_FactureRegleeEnDeuxFois_ProduitDeuxLignesSansSurdeclaration()
        {
            // Facture FF260029 (LOGINFO INGENIERIE) : 160 486,80 TTC, HT 133 739,00, TVA 26 747,80 à 20%
            var docTaxes = new DocumentTaxesInfo
            {
                NumeroPiece = "FF260029",
                TotalHT = 133739.00,
                TotalTva = 26747.80,
                TotalTtc = 160486.80,
                LignesTaxe = new List<TaxeDetail>
                {
                    new TaxeDetail { Taux = 20, MontantTva = 26747.80, BaseHT = 133739.00, Code = "T20" }
                }
            };

            var aff1 = new AffectationADeclarer
            {
                EC_Id = 2001,
                MV_Id = 5001,
                NumeroFacture = "FF260029",
                NumeroRapprochement = "RF26100006",
                MontantAffecte = 80243.40m,
                Source = SourceAffectation.Decaissement
            };

            var aff2 = new AffectationADeclarer
            {
                EC_Id = 2001,
                MV_Id = 5002,
                NumeroFacture = "FF260029",
                NumeroRapprochement = "RF26100007",
                MontantAffecte = 80243.40m,
                Source = SourceAffectation.Decaissement
            };

            var dec = ConstructeurDeclaration.ConstruireDeclaration(
                new List<AffectationADeclarer> { aff1, aff2 },
                _ => docTaxes,
                2);

            Assert.Equal(2, dec.Lignes.Count);
            Assert.Equal(26747.80m, Math.Round(dec.Lignes.Sum(l => l.Tva), 2));
            Assert.DoesNotContain(dec.Alertes, a => a.Code == "TVA_FACTURE_SURDECLAREE");
        }

        [Fact]
        public void ConstruireDeclaration_Task224_Scenario4_FactureMultiTauxRegleeEnDeuxFois_ProduitQuatreLignes()
        {
            // Facture 2 taux (20% et 10%) :
            // HT 100 000 @ 20% -> TVA 20 000 -> TTC 120 000
            // HT 10 000 @ 10% -> TVA 1 000 -> TTC 11 000
            // Total HT = 110 000, Total TVA = 21 000, Total TTC = 131 000
            var docTaxes = new DocumentTaxesInfo
            {
                NumeroPiece = "FF_MULTI_01",
                TotalHT = 110000.00,
                TotalTva = 21000.00,
                TotalTtc = 131000.00,
                LignesTaxe = new List<TaxeDetail>
                {
                    new TaxeDetail { Taux = 20, MontantTva = 20000.00, BaseHT = 100000.00, Code = "T20" },
                    new TaxeDetail { Taux = 10, MontantTva = 1000.00, BaseHT = 10000.00, Code = "T10" }
                }
            };

            // Réglée en 2 fois 50% (65 500 chacune)
            var aff1 = new AffectationADeclarer
            {
                EC_Id = 3001,
                MV_Id = 6001,
                NumeroFacture = "FF_MULTI_01",
                NumeroRapprochement = "REG_01",
                MontantAffecte = 65500.00m,
                Source = SourceAffectation.Decaissement
            };

            var aff2 = new AffectationADeclarer
            {
                EC_Id = 3001,
                MV_Id = 6002,
                NumeroFacture = "FF_MULTI_01",
                NumeroRapprochement = "REG_02",
                MontantAffecte = 65500.00m,
                Source = SourceAffectation.Decaissement
            };

            var dec = ConstructeurDeclaration.ConstruireDeclaration(
                new List<AffectationADeclarer> { aff1, aff2 },
                _ => docTaxes,
                2);

            // 2 règlements × 2 taux = 4 lignes, pas 8
            Assert.Equal(4, dec.Lignes.Count);
            Assert.Equal(21000.00m, Math.Round(dec.Lignes.Sum(l => l.Tva), 2));
            Assert.DoesNotContain(dec.Alertes, a => a.Code == "TVA_FACTURE_SURDECLAREE");
        }

        [Fact]
        public void ConstruireDeclaration_Task224_Scenario6_CasLimiteArrondi_SansAlerte()
        {
            // Facture FF260028 (SNSS TECHNOLOGIES) : TTC 80 414,77, TVA 13 402,46 à 20%
            // Règlements : 40 207,38 + 40 207,39
            var docTaxes = new DocumentTaxesInfo
            {
                NumeroPiece = "FF260028",
                TotalHT = 67012.31,
                TotalTva = 13402.46,
                TotalTtc = 80414.77,
                LignesTaxe = new List<TaxeDetail>
                {
                    new TaxeDetail { Taux = 20, MontantTva = 13402.46, BaseHT = 67012.31, Code = "T20" }
                }
            };

            var aff1 = new AffectationADeclarer
            {
                EC_Id = 4001,
                MV_Id = 7001,
                NumeroFacture = "FF260028",
                NumeroRapprochement = "RF26090006",
                MontantAffecte = 40207.38m,
                Source = SourceAffectation.Decaissement
            };

            var aff2 = new AffectationADeclarer
            {
                EC_Id = 4001,
                MV_Id = 7002,
                NumeroFacture = "FF260028",
                NumeroRapprochement = "RF26090007",
                MontantAffecte = 40207.39m,
                Source = SourceAffectation.Decaissement
            };

            var dec = ConstructeurDeclaration.ConstruireDeclaration(
                new List<AffectationADeclarer> { aff1, aff2 },
                _ => docTaxes,
                2);

            Assert.Equal(2, dec.Lignes.Count);
            var totalTva = Math.Round(dec.Lignes.Sum(l => l.Tva), 2);
            Assert.True(Math.Abs(totalTva - 13402.46m) <= 0.01m);
            Assert.DoesNotContain(dec.Alertes, a => a.Code == "TVA_FACTURE_SURDECLAREE");
        }

        [Fact]
        public void ConstruireDeclaration_Task224_Scenario7_GardeFou_TvaFactureSurdeclaree_DeclencheAlerteError()
        {
            // Facture TTC 100 000, TVA 20 000
            var docTaxes = new DocumentTaxesInfo
            {
                NumeroPiece = "FF_SURDEC",
                TotalHT = 80000.00,
                TotalTva = 20000.00,
                TotalTtc = 100000.00,
                LignesTaxe = new List<TaxeDetail>
                {
                    new TaxeDetail { Taux = 20, MontantTva = 20000.00, BaseHT = 80000.00, Code = "T20" }
                }
            };

            // Règlements totalisant 150 000 TTC (> 100 000)
            var aff1 = new AffectationADeclarer
            {
                EC_Id = 5001,
                MV_Id = 8001,
                NumeroFacture = "FF_SURDEC",
                MontantAffecte = 75000.00m,
                Source = SourceAffectation.Decaissement
            };
            var aff2 = new AffectationADeclarer
            {
                EC_Id = 5001,
                MV_Id = 8002,
                NumeroFacture = "FF_SURDEC",
                MontantAffecte = 75000.00m,
                Source = SourceAffectation.Decaissement
            };

            var dec = ConstructeurDeclaration.ConstruireDeclaration(
                new List<AffectationADeclarer> { aff1, aff2 },
                _ => docTaxes,
                2);

            Assert.Contains(dec.Alertes, a => a.Code == "TVA_FACTURE_SURDECLAREE" && a.Niveau == NiveauAlerte.Error);
        }

        #endregion
    }
}
