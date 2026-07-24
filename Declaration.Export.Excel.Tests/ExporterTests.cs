using System;
using System.Collections.Generic;
using System.IO;
using ClosedXML.Excel;
using Declaration.Core.Model;
using Xunit;

namespace Declaration.Export.Excel.Tests
{
    public class ExporterTests
    {
        private DeclarationModele GetFixture()
        {
            return new DeclarationModele
            {
                Lignes = new List<LigneDeclarationEnrichie>
                {
                    new LigneDeclarationEnrichie
                    {
                        NumeroFacture = "F-001",
                        // TASK-162 : numéro de règlement, désormais exporté en 2ᵉ colonne.
                        NumeroRapprochement = "REG-100",
                        Designation = "Achat fournitures",
                        Tiers = new TiersInfo { Nom = "Fournisseur A", IdentifiantFiscal = "IF-A", Ice = "ICE-A", CodeActivite = "ACT-1" },
                        CodeActivite = "ACT-1",
                        HT = 1000m,
                        Taux = 20m,
                        Tva = 200m,
                        Ttc = 1200m,
                        Prorata = 100m,
                        ModePaiement = "3",
                        DatePaiement = new DateTime(2023, 1, 15),
                        DateFacture = new DateTime(2023, 1, 10),
                        Source = SourceAffectation.Decaissement
                    },
                    new LigneDeclarationEnrichie
                    {
                        NumeroFacture = "F-002",
                        // TASK-162 : règlement absent (cas "jamais d'exception" du modèle) — chaîne vide.
                        NumeroRapprochement = "",
                        Designation = "Achat matériel",
                        Tiers = new TiersInfo { Nom = "Fournisseur B", IdentifiantFiscal = "IF-B", Ice = "ICE-B", CodeActivite = "ACT-2" },
                        CodeActivite = "ACT-2",
                        HT = 2000m,
                        Taux = 20m,
                        Tva = 400m,
                        Ttc = 2400m,
                        Prorata = 100m,
                        ModePaiement = "2",
                        DatePaiement = new DateTime(2023, 1, 20),
                        DateFacture = new DateTime(2023, 1, 18),
                        Source = SourceAffectation.Decaissement
                    }
                },
                RecapsParSource = new List<RecapParSource>
                {
                    new RecapParSource { Source = SourceAffectation.Decaissement, TotalHT = 3000m, TotalTva = 600m, TotalTtc = 3600m }
                },
                RecapsParTaux = new List<RecapParTaux>
                {
                    new RecapParTaux { Taux = 20m, TotalHT = 3000m, TotalTva = 600m, TotalTtc = 3600m }
                },
                RecapsParActivite = new List<RecapParActivite>
                {
                    new RecapParActivite { CodeActivite = "ACT-1", TotalHT = 1000m, TotalTva = 200m, TotalTtc = 1200m },
                    new RecapParActivite { CodeActivite = "ACT-2", TotalHT = 2000m, TotalTva = 400m, TotalTtc = 2400m }
                },
                ControleEquilibre = new ControleEquilibre
                {
                    TotalMontantAffecte = 3600m,
                    TotalDeclareTtc = 3600m,
                    ResiduExplique = 0m
                },
                Alertes = new List<Alerte>
                {
                    new Alerte { Niveau = NiveauAlerte.Warning, Code = "WARN-001", Message = "Alerte de test", RefLigne = "F-001" }
                }
            };
        }

        [Fact]
        public void ExporterExcel_DevraitGenererFichierConforme()
        {
            var modele = GetFixture();
            var cheminFichier = Path.Combine(Path.GetTempPath(), $"TestExport_{Guid.NewGuid()}.xlsx");

            try
            {
                var exporter = new Exporter();
                exporter.ExporterExcel(modele, cheminFichier);

                Assert.True(File.Exists(cheminFichier));

                using var workbook = new XLWorkbook(cheminFichier);
                Assert.Equal(2, workbook.Worksheets.Count);

                var wsDetail = workbook.Worksheet("Détail");
                Assert.NotNull(wsDetail);

                // En-têtes
                Assert.Equal("N° Facture", wsDetail.Cell(1, 1).Value.ToString());
                // TASK-162 : "N° Règlement" juste après "N° Facture".
                Assert.Equal("N° Règlement", wsDetail.Cell(1, 2).Value.ToString());

                // Lignes de données
                Assert.Equal("F-001", wsDetail.Cell(2, 1).Value.ToString());
                Assert.Equal("REG-100", wsDetail.Cell(2, 2).Value.ToString());
                Assert.Equal("Achat fournitures", wsDetail.Cell(2, 3).Value.ToString());
                Assert.Equal("Fournisseur A", wsDetail.Cell(2, 4).Value.ToString());
                Assert.Equal("F-002", wsDetail.Cell(3, 1).Value.ToString());
                // Règlement absent : cellule vide, aucune exception.
                Assert.Equal("", wsDetail.Cell(3, 2).Value.ToString());

                // TASK-163 : colonne "Mode Paiement" (13, décalée par TASK-162) affiche le libellé
                // métier, jamais le code brut.
                Assert.Equal("Virement", wsDetail.Cell(2, 13).Value.ToString());
                Assert.Equal("Chèque", wsDetail.Cell(3, 13).Value.ToString());

                var wsRecap = workbook.Worksheet("Récap");
                Assert.NotNull(wsRecap);

                // Vérifier la présence de "Totaux par source" et des montants
                Assert.Equal("Totaux par source", wsRecap.Cell(1, 1).Value.ToString());
                Assert.Equal("Decaissement", wsRecap.Cell(3, 1).Value.ToString());
                Assert.Equal(3000m, (decimal)wsRecap.Cell(3, 2).Value.GetNumber());

                // Vérifier "Alertes"
                // Localiser la section "Alertes"
                int alertesRow = 1;
                while (alertesRow <= 100 && wsRecap.Cell(alertesRow, 1).Value.ToString() != "Alertes")
                {
                    alertesRow++;
                }

                Assert.True(alertesRow <= 100, "Section 'Alertes' introuvable");
                Assert.Equal("Warning", wsRecap.Cell(alertesRow + 2, 1).Value.ToString());
                Assert.Equal("WARN-001", wsRecap.Cell(alertesRow + 2, 2).Value.ToString());
                Assert.Equal("Alerte de test", wsRecap.Cell(alertesRow + 2, 3).Value.ToString());
            }
            finally
            {
                if (File.Exists(cheminFichier))
                {
                    File.Delete(cheminFichier);
                }
            }
        }

        // ─── TASK-160 : ExporterExcelControle ───────────────────────────────────

        private ModeleControle GetFixtureControle()
        {
            return new ModeleControle
            {
                ReglementsSelectionnes = new List<ReglementSelectionneInfo>
                {
                    new ReglementSelectionneInfo
                    {
                        Numero = "REG-001",
                        Date = new DateTime(2026, 7, 5),
                        Montant = 1200m,
                        Tiers = "Fournisseur A",
                        Mode = "Virement",
                        // TASK-180 : statut court + date en champ séparé (colonne dédiée).
                        EtatPointage = "Rapproché",
                        DateRapprochement = new DateTime(2026, 7, 8)
                    },
                    new ReglementSelectionneInfo
                    {
                        Numero = "REG-002",
                        Date = new DateTime(2026, 7, 6),
                        Montant = 500m,
                        Tiers = "Fournisseur B",
                        Mode = "Chèque",
                        EtatPointage = "Non rapproché"
                    }
                },
                Lignes = new List<LigneDeclarationEnrichie>
                {
                    new LigneDeclarationEnrichie
                    {
                        NumeroFacture = "F-001",
                        // TASK-162 : numéro de règlement, désormais exporté en 2ᵉ colonne.
                        NumeroRapprochement = "REG-001",
                        Designation = "",
                        Tiers = new TiersInfo { Nom = "Fournisseur A", IdentifiantFiscal = "IF-A", Ice = "ICE-A" },
                        HT = 1000m,
                        Taux = 20m,
                        Tva = 200m,
                        Ttc = 1200m,
                        Prorata = 100m,
                        ModePaiement = "9",
                        DatePaiement = new DateTime(2026, 7, 15),
                        DateFacture = new DateTime(2026, 7, 10),
                        Source = SourceAffectation.Decaissement
                    }
                },
                // TASK-180 : un couple Collecté/Déductible sur le même taux (20) pour couvrir les 2
                // blocs écrits par CreerFeuilleDetailTva.
                RecapsParTaux = new List<RecapParTaux>
                {
                    new RecapParTaux { Taux = 20m, Collecte = true, TotalHT = 500m, TotalTva = 100m, TotalTtc = 600m },
                    new RecapParTaux { Taux = 20m, Collecte = false, TotalHT = 1000m, TotalTva = 200m, TotalTtc = 1200m }
                },
                RecapsParActivite = new List<RecapParActivite>
                {
                    new RecapParActivite { CodeActivite = "", Collecte = false, TotalHT = 1000m, TotalTva = 200m, TotalTtc = 1200m }
                },
                ControleEquilibre = new ControleEquilibre
                {
                    TotalMontantAffecte = 1000m,
                    TotalDeclareTtc = 1200m,
                    ResiduExplique = 0m
                }
            };
        }

        [Fact]
        public void ExporterExcelControle_Genere3FeuillesConformes()
        {
            var modele = GetFixtureControle();
            using var stream = new MemoryStream();

            new Exporter().ExporterExcelControle(modele, stream);
            stream.Position = 0;

            using var workbook = new XLWorkbook(stream);
            Assert.Equal(3, workbook.Worksheets.Count);

            var wsReglements = workbook.Worksheet("Règlements sélectionnés");
            Assert.Equal("Numéro", wsReglements.Cell(1, 1).Value.ToString());
            // TASK-180 : nouvelle colonne "Date rapprochement" (7), État pointage (6) reste un statut court.
            Assert.Equal("Date rapprochement", wsReglements.Cell(1, 7).Value.ToString());
            Assert.Equal("REG-001", wsReglements.Cell(2, 1).Value.ToString());
            Assert.Equal(1200m, (decimal)wsReglements.Cell(2, 3).Value.GetNumber());
            Assert.Equal("Fournisseur A", wsReglements.Cell(2, 4).Value.ToString());
            Assert.Equal("Rapproché", wsReglements.Cell(2, 6).Value.ToString());
            Assert.Equal(new DateTime(2026, 7, 8), wsReglements.Cell(2, 7).GetDateTime());
            Assert.Equal("dd/mm/yyyy", wsReglements.Cell(2, 7).Style.DateFormat.Format);
            // Règlement non rapproché (REG-002) : colonne Date rapprochement vide, pas d'exception.
            Assert.Equal("Non rapproché", wsReglements.Cell(3, 6).Value.ToString());
            Assert.True(wsReglements.Cell(3, 7).IsEmpty());

            // TASK-180 : Date Paiement (13) / Date Facture (14) — format explicite date-seule.
            Assert.Equal("dd/mm/yyyy", wsReglements.Cell(2, 2).Style.DateFormat.Format);

            var wsFactures = workbook.Worksheet("Factures à déclarer");
            Assert.Equal("N° Facture", wsFactures.Cell(1, 1).Value.ToString());
            // TASK-162 : "N° Règlement" juste après "N° Facture".
            Assert.Equal("N° Règlement", wsFactures.Cell(1, 2).Value.ToString());
            Assert.Equal("F-001", wsFactures.Cell(2, 1).Value.ToString());
            Assert.Equal("REG-001", wsFactures.Cell(2, 2).Value.ToString());
            Assert.Equal(1000m, (decimal)wsFactures.Cell(2, 8).Value.GetNumber());
            Assert.Equal("dd/mm/yyyy", wsFactures.Cell(2, 14).Style.DateFormat.Format);
            Assert.Equal("dd/mm/yyyy", wsFactures.Cell(2, 15).Style.DateFormat.Format);

            // TASK-163 : code Simpl-TVA inconnu ("9") -> fallback code brut tel quel, jamais d'exception
            // (colonne 13, décalée par TASK-162).
            Assert.Equal("9", wsFactures.Cell(2, 13).Value.ToString());

            // TASK-180 : détail TVA scindé Collecté/Déductible — 4 blocs (taux x2, activité x2).
            // TASK-184 : bloc "Contrôle d'équilibre" retiré.
            // TASK-185 : 4 colonnes (plus de "Domaine Activité") + ligne "Total Collecté"/
            // "Total Deductible" en pied des blocs "Totaux par taux" uniquement.
            var wsDetailTva = workbook.Worksheet("Détail TVA");
            Assert.Equal("Totaux par taux — Collecté", wsDetailTva.Cell(1, 1).Value.ToString());
            Assert.Equal("Total HT", wsDetailTva.Cell(2, 2).Value.ToString());
            Assert.Equal(20m, (decimal)wsDetailTva.Cell(3, 1).Value.GetNumber());
            Assert.Equal(500m, (decimal)wsDetailTva.Cell(3, 2).Value.GetNumber());
            Assert.Equal(600m, (decimal)wsDetailTva.Cell(3, 4).Value.GetNumber());
            Assert.Equal("Total Collecté", wsDetailTva.Cell(4, 1).Value.ToString());
            Assert.Equal(500m, (decimal)wsDetailTva.Cell(4, 2).Value.GetNumber());
            Assert.Equal(100m, (decimal)wsDetailTva.Cell(4, 3).Value.GetNumber());
            Assert.Equal(600m, (decimal)wsDetailTva.Cell(4, 4).Value.GetNumber());

            Assert.Equal("Totaux par taux — Déductible", wsDetailTva.Cell(6, 1).Value.ToString());
            Assert.Equal(20m, (decimal)wsDetailTva.Cell(8, 1).Value.GetNumber());
            Assert.Equal(1200m, (decimal)wsDetailTva.Cell(8, 4).Value.GetNumber());
            Assert.Equal("Total Deductible", wsDetailTva.Cell(9, 1).Value.ToString());
            Assert.Equal(1000m, (decimal)wsDetailTva.Cell(9, 2).Value.GetNumber());
            Assert.Equal(200m, (decimal)wsDetailTva.Cell(9, 3).Value.GetNumber());
            Assert.Equal(1200m, (decimal)wsDetailTva.Cell(9, 4).Value.GetNumber());

            Assert.Equal("Totaux par code activité — Collecté", wsDetailTva.Cell(11, 1).Value.ToString());
            Assert.Equal("Totaux par code activité — Déductible", wsDetailTva.Cell(14, 1).Value.ToString());
            Assert.Equal(1200m, (decimal)wsDetailTva.Cell(16, 4).Value.GetNumber());

            // TASK-184 : bloc "Contrôle d'équilibre" supprimé — plus aucune occurrence dans la feuille.
            bool controleEquilibrePresent = false;
            for (int r = 1; r <= 30; r++)
            {
                if (wsDetailTva.Cell(r, 1).Value.ToString() == "Contrôle d'équilibre") controleEquilibrePresent = true;
            }
            Assert.False(controleEquilibrePresent, "Le bloc 'Contrôle d'équilibre' ne doit plus être présent dans la feuille Détail TVA.");
        }

        [Fact]
        public void ExporterExcelControle_AucuneEcritureSurDisque_SeulementStream()
        {
            var modele = new ModeleControle();
            using var stream = new MemoryStream();

            new Exporter().ExporterExcelControle(modele, stream);

            Assert.True(stream.Length > 0);
        }
    }
}
