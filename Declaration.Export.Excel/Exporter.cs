using System;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using Declaration.Core.Model;

namespace Declaration.Export.Excel
{
    public class Exporter
    {
        public void ExporterExcel(DeclarationModele modele, string cheminSortie)
        {
            using var workbook = new XLWorkbook();
            
            // Feuille Détail
            var wsDetail = workbook.Worksheets.Add("Détail");
            CreerFeuilleDetail(wsDetail, modele);
            
            // Feuille Récap
            var wsRecap = workbook.Worksheets.Add("Récap");
            CreerFeuilleRecap(wsRecap, modele);
            
            workbook.SaveAs(cheminSortie);
        }

        private void CreerFeuilleDetail(IXLWorksheet ws, DeclarationModele modele)
        {
            // En-têtes
            string[] headers = {
                "N° Facture", "Désignation", "Tiers Nom", "Tiers IF", "Tiers ICE",
                "Code Activité", "HT", "Taux", "TVA", "TTC", "Prorata", "Mode Paiement",
                "Date Paiement", "Date Facture", "Source"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
                ws.Cell(1, i + 1).Style.Font.Bold = true;
            }

            // Données
            int row = 2;
            foreach (var ligne in modele.Lignes)
            {
                ws.Cell(row, 1).Value = ligne.NumeroFacture;
                ws.Cell(row, 2).Value = ligne.Designation;
                ws.Cell(row, 3).Value = ligne.Tiers?.Nom;
                ws.Cell(row, 4).Value = ligne.Tiers?.IdentifiantFiscal;
                ws.Cell(row, 5).Value = ligne.Tiers?.Ice;
                ws.Cell(row, 6).Value = ligne.CodeActivite;
                ws.Cell(row, 7).Value = ligne.HT;
                ws.Cell(row, 8).Value = ligne.Taux;
                ws.Cell(row, 9).Value = ligne.Tva;
                ws.Cell(row, 10).Value = ligne.Ttc;
                ws.Cell(row, 11).Value = ligne.Prorata;
                ws.Cell(row, 12).Value = ligne.ModePaiement;
                if (ligne.DatePaiement.HasValue) ws.Cell(row, 13).Value = ligne.DatePaiement.Value;
                if (ligne.DateFacture.HasValue) ws.Cell(row, 14).Value = ligne.DateFacture.Value;
                ws.Cell(row, 15).Value = ligne.Source.ToString();
                
                row++;
            }

            ws.Columns().AdjustToContents();
        }

        private void CreerFeuilleRecap(IXLWorksheet ws, DeclarationModele modele)
        {
            int row = 1;

            // Totaux par source
            ws.Cell(row, 1).Value = "Totaux par source";
            ws.Cell(row, 1).Style.Font.Bold = true;
            row++;
            ws.Cell(row, 1).Value = "Source";
            ws.Cell(row, 2).Value = "Total HT";
            ws.Cell(row, 3).Value = "Total TVA";
            ws.Cell(row, 4).Value = "Total TTC";
            ws.Range(row, 1, row, 4).Style.Font.Bold = true;
            row++;
            foreach (var recap in modele.RecapsParSource)
            {
                ws.Cell(row, 1).Value = recap.Source.ToString();
                ws.Cell(row, 2).Value = recap.TotalHT;
                ws.Cell(row, 3).Value = recap.TotalTva;
                ws.Cell(row, 4).Value = recap.TotalTtc;
                row++;
            }

            row++;

            // Totaux par taux
            ws.Cell(row, 1).Value = "Totaux par taux";
            ws.Cell(row, 1).Style.Font.Bold = true;
            row++;
            ws.Cell(row, 1).Value = "Taux";
            ws.Cell(row, 2).Value = "Total HT";
            ws.Cell(row, 3).Value = "Total TVA";
            ws.Cell(row, 4).Value = "Total TTC";
            ws.Range(row, 1, row, 4).Style.Font.Bold = true;
            row++;
            foreach (var recap in modele.RecapsParTaux)
            {
                ws.Cell(row, 1).Value = recap.Taux;
                ws.Cell(row, 2).Value = recap.TotalHT;
                ws.Cell(row, 3).Value = recap.TotalTva;
                ws.Cell(row, 4).Value = recap.TotalTtc;
                row++;
            }

            row++;

            // Totaux par code activité
            ws.Cell(row, 1).Value = "Totaux par code activité";
            ws.Cell(row, 1).Style.Font.Bold = true;
            row++;
            ws.Cell(row, 1).Value = "Code Activité";
            ws.Cell(row, 2).Value = "Total HT";
            ws.Cell(row, 3).Value = "Total TVA";
            ws.Cell(row, 4).Value = "Total TTC";
            ws.Range(row, 1, row, 4).Style.Font.Bold = true;
            row++;
            foreach (var recap in modele.RecapsParActivite)
            {
                ws.Cell(row, 1).Value = recap.CodeActivite;
                ws.Cell(row, 2).Value = recap.TotalHT;
                ws.Cell(row, 3).Value = recap.TotalTva;
                ws.Cell(row, 4).Value = recap.TotalTtc;
                row++;
            }

            row++;

            // Contrôle d'équilibre
            ws.Cell(row, 1).Value = "Contrôle d'équilibre";
            ws.Cell(row, 1).Style.Font.Bold = true;
            row++;
            ws.Cell(row, 1).Value = "Total Montant Affecté";
            ws.Cell(row, 2).Value = modele.ControleEquilibre.TotalMontantAffecte;
            row++;
            ws.Cell(row, 1).Value = "Total Déclaré TTC";
            ws.Cell(row, 2).Value = modele.ControleEquilibre.TotalDeclareTtc;
            row++;
            ws.Cell(row, 1).Value = "Résidu Non TVA";
            ws.Cell(row, 2).Value = modele.ControleEquilibre.ResiduNonTva;
            row++;
            ws.Cell(row, 1).Value = "Résidu Expliqué";
            ws.Cell(row, 2).Value = modele.ControleEquilibre.ResiduExplique;
            row++;
            ws.Cell(row, 1).Value = "Résidu Inexpliqué";
            ws.Cell(row, 2).Value = modele.ControleEquilibre.ResiduInexplique;
            row++;

            row++;

            // Alertes
            ws.Cell(row, 1).Value = "Alertes";
            ws.Cell(row, 1).Style.Font.Bold = true;
            row++;
            ws.Cell(row, 1).Value = "Niveau";
            ws.Cell(row, 2).Value = "Code";
            ws.Cell(row, 3).Value = "Message";
            ws.Cell(row, 4).Value = "Réf Ligne";
            ws.Range(row, 1, row, 4).Style.Font.Bold = true;
            row++;
            foreach (var alerte in modele.Alertes)
            {
                ws.Cell(row, 1).Value = alerte.Niveau.ToString();
                ws.Cell(row, 2).Value = alerte.Code;
                ws.Cell(row, 3).Value = alerte.Message;
                ws.Cell(row, 4).Value = alerte.RefLigne;
                row++;
            }

            ws.Columns().AdjustToContents();
        }
    }
}
