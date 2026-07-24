using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using Declaration.Core;
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
            // TASK-162 : "N° Règlement" ajouté juste après "N° Facture" (regroupement logique
            // facture+règlement) — décale tous les index suivants de +1.
            string[] headers = {
                "N° Facture", "N° Règlement", "Désignation", "Tiers Nom", "Tiers IF", "Tiers ICE",
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
                ws.Cell(row, 2).Value = ligne.NumeroRapprochement;
                ws.Cell(row, 3).Value = ligne.Designation;
                ws.Cell(row, 4).Value = ligne.Tiers?.Nom;
                ws.Cell(row, 5).Value = ligne.Tiers?.IdentifiantFiscal;
                ws.Cell(row, 6).Value = ligne.Tiers?.Ice;
                ws.Cell(row, 7).Value = ligne.CodeActivite;
                ws.Cell(row, 8).Value = ligne.HT;
                ws.Cell(row, 9).Value = ligne.Taux;
                ws.Cell(row, 10).Value = ligne.Tva;
                ws.Cell(row, 11).Value = ligne.Ttc;
                ws.Cell(row, 12).Value = ligne.Prorata;
                ws.Cell(row, 13).Value = ModePaiementLibelle.LibelleModePaiementSimplTVA(ligne.ModePaiement);
                if (ligne.DatePaiement.HasValue) { ws.Cell(row, 14).Value = ligne.DatePaiement.Value; ws.Cell(row, 14).Style.DateFormat.Format = FormatDateSeule; }
                if (ligne.DateFacture.HasValue) { ws.Cell(row, 15).Value = ligne.DateFacture.Value; ws.Cell(row, 15).Style.DateFormat.Format = FormatDateSeule; }
                ws.Cell(row, 16).Value = ligne.Source.ToString();

                row++;
            }

            ws.Columns().AdjustToContents();
        }

        // TASK-180 : format explicite date-seule, appliqué à toutes les cellules date de l'export
        // (masque l'heure quelle que soit la valeur réellement portée par AF_Date/DO_Date en base).
        private const string FormatDateSeule = "dd/mm/yyyy";

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

            // TASK-180 : détail Collecté/Déductible — deux tableaux séparés par sens fiscal,
            // cohérence avec le rendu déjà validé PO côté front pour TASK-174.
            row = EcrireBlocRecapParTaux(ws, row, "Totaux par taux — Collecté", modele.RecapsParTaux.Where(r => r.Collecte));
            row++;
            row = EcrireBlocRecapParTaux(ws, row, "Totaux par taux — Déductible", modele.RecapsParTaux.Where(r => !r.Collecte));
            row++;

            row = EcrireBlocRecapParActivite(ws, row, "Totaux par code activité — Collecté", modele.RecapsParActivite.Where(r => r.Collecte));
            row++;
            row = EcrireBlocRecapParActivite(ws, row, "Totaux par code activité — Déductible", modele.RecapsParActivite.Where(r => !r.Collecte));
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

        // TASK-180 : blocs réutilisés par CreerFeuilleRecap (export dépôt) et CreerFeuilleDetailTva
        // (export contrôle) — un bloc = un titre + un tableau Taux/Activité déjà filtré par sens.
        // TASK-184 : afficherDomaineActivite ajoute une colonne "Domaine Activité" (Achats/Ventes/
        // Non résolu) — par défaut false pour ne rien changer à CreerFeuilleRecap (feuille "Récap",
        // export dépôt, hors périmètre TASK-184), activé uniquement par CreerFeuilleDetailTva.
        private int EcrireBlocRecapParTaux(IXLWorksheet ws, int row, string titre, IEnumerable<RecapParTaux> recaps, bool afficherDomaineActivite = false)
        {
            ws.Cell(row, 1).Value = titre;
            ws.Cell(row, 1).Style.Font.Bold = true;
            row++;
            int col = 1;
            ws.Cell(row, col++).Value = "Taux";
            if (afficherDomaineActivite)
                ws.Cell(row, col++).Value = "Domaine Activité";
            ws.Cell(row, col++).Value = "Total HT";
            ws.Cell(row, col++).Value = "Total TVA";
            ws.Cell(row, col++).Value = "Total TTC";
            ws.Range(row, 1, row, col - 1).Style.Font.Bold = true;
            row++;
            foreach (var recap in recaps)
            {
                col = 1;
                ws.Cell(row, col++).Value = recap.Taux;
                if (afficherDomaineActivite)
                    ws.Cell(row, col++).Value = recap.Domaine;
                ws.Cell(row, col++).Value = recap.TotalHT;
                ws.Cell(row, col++).Value = recap.TotalTva;
                ws.Cell(row, col++).Value = recap.TotalTtc;
                row++;
            }
            return row;
        }

        private int EcrireBlocRecapParActivite(IXLWorksheet ws, int row, string titre, IEnumerable<RecapParActivite> recaps)
        {
            ws.Cell(row, 1).Value = titre;
            ws.Cell(row, 1).Style.Font.Bold = true;
            row++;
            ws.Cell(row, 1).Value = "Code Activité";
            ws.Cell(row, 2).Value = "Total HT";
            ws.Cell(row, 3).Value = "Total TVA";
            ws.Cell(row, 4).Value = "Total TTC";
            ws.Range(row, 1, row, 4).Style.Font.Bold = true;
            row++;
            foreach (var recap in recaps)
            {
                ws.Cell(row, 1).Value = recap.CodeActivite;
                ws.Cell(row, 2).Value = recap.TotalHT;
                ws.Cell(row, 3).Value = recap.TotalTva;
                ws.Cell(row, 4).Value = recap.TotalTtc;
                row++;
            }
            return row;
        }

        /// <summary>
        /// TASK-160 : export de contrôle ad-hoc, généré à la volée dans un <see cref="Stream"/>
        /// (aucun fichier disque, à la différence de <see cref="ExporterExcel"/>) — 3 feuilles :
        /// règlements sélectionnés, factures à déclarer, détail TVA.
        /// </summary>
        public void ExporterExcelControle(ModeleControle modele, Stream stream)
        {
            using var workbook = new XLWorkbook();

            var wsReglements = workbook.Worksheets.Add("Règlements sélectionnés");
            CreerFeuilleReglementsSelectionnes(wsReglements, modele);

            var wsFactures = workbook.Worksheets.Add("Factures à déclarer");
            CreerFeuilleFacturesControle(wsFactures, modele);

            var wsDetailTva = workbook.Worksheets.Add("Détail TVA");
            CreerFeuilleDetailTva(wsDetailTva, modele);

            workbook.SaveAs(stream);
        }

        private void CreerFeuilleReglementsSelectionnes(IXLWorksheet ws, ModeleControle modele)
        {
            // TASK-180 : "Date rapprochement" en colonne dédiée (7) — plus jamais concaténée dans
            // "État pointage" (6), qui reste un statut court exploitable.
            string[] headers = { "Numéro", "Date", "Montant", "Tiers", "Mode", "État pointage", "Date rapprochement" };
            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
                ws.Cell(1, i + 1).Style.Font.Bold = true;
            }

            int row = 2;
            foreach (var r in modele.ReglementsSelectionnes)
            {
                ws.Cell(row, 1).Value = r.Numero;
                if (r.Date.HasValue) { ws.Cell(row, 2).Value = r.Date.Value; ws.Cell(row, 2).Style.DateFormat.Format = FormatDateSeule; }
                ws.Cell(row, 3).Value = r.Montant;
                ws.Cell(row, 4).Value = r.Tiers;
                ws.Cell(row, 5).Value = r.Mode;
                ws.Cell(row, 6).Value = r.EtatPointage;
                if (r.DateRapprochement.HasValue) { ws.Cell(row, 7).Value = r.DateRapprochement.Value; ws.Cell(row, 7).Style.DateFormat.Format = FormatDateSeule; }
                row++;
            }

            ws.Columns().AdjustToContents();
        }

        private void CreerFeuilleFacturesControle(IXLWorksheet ws, ModeleControle modele)
        {
            // TASK-162 : "N° Règlement" ajouté juste après "N° Facture", même insertion que
            // CreerFeuilleDetail (code auparavant dupliqué à l'identique, cf. TASK-160).
            string[] headers = {
                "N° Facture", "N° Règlement", "Désignation", "Tiers Nom", "Tiers IF", "Tiers ICE",
                "Code Activité", "HT", "Taux", "TVA", "TTC", "Prorata", "Mode Paiement",
                "Date Paiement", "Date Facture", "Source"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
                ws.Cell(1, i + 1).Style.Font.Bold = true;
            }

            int row = 2;
            foreach (var ligne in modele.Lignes)
            {
                ws.Cell(row, 1).Value = ligne.NumeroFacture;
                ws.Cell(row, 2).Value = ligne.NumeroRapprochement;
                ws.Cell(row, 3).Value = ligne.Designation;
                ws.Cell(row, 4).Value = ligne.Tiers?.Nom;
                ws.Cell(row, 5).Value = ligne.Tiers?.IdentifiantFiscal;
                ws.Cell(row, 6).Value = ligne.Tiers?.Ice;
                ws.Cell(row, 7).Value = ligne.CodeActivite;
                ws.Cell(row, 8).Value = ligne.HT;
                ws.Cell(row, 9).Value = ligne.Taux;
                ws.Cell(row, 10).Value = ligne.Tva;
                ws.Cell(row, 11).Value = ligne.Ttc;
                ws.Cell(row, 12).Value = ligne.Prorata;
                ws.Cell(row, 13).Value = ModePaiementLibelle.LibelleModePaiementSimplTVA(ligne.ModePaiement);
                if (ligne.DatePaiement.HasValue) { ws.Cell(row, 14).Value = ligne.DatePaiement.Value; ws.Cell(row, 14).Style.DateFormat.Format = FormatDateSeule; }
                if (ligne.DateFacture.HasValue) { ws.Cell(row, 15).Value = ligne.DateFacture.Value; ws.Cell(row, 15).Style.DateFormat.Format = FormatDateSeule; }
                ws.Cell(row, 16).Value = ligne.Source.ToString();

                row++;
            }

            ws.Columns().AdjustToContents();
        }

        private void CreerFeuilleDetailTva(IXLWorksheet ws, ModeleControle modele)
        {
            int row = 1;

            // TASK-180 : détail Collecté/Déductible, même principe que CreerFeuilleRecap.
            // TASK-184 : colonne Domaine Activité affichée ici uniquement (export de contrôle) —
            // afficherDomaineActivite=true réservé à ce chemin, CreerFeuilleRecap (export dépôt,
            // feuille "Récap") reste inchangé via la valeur par défaut du paramètre.
            row = EcrireBlocRecapParTaux(ws, row, "Totaux par taux — Collecté", modele.RecapsParTaux.Where(r => r.Collecte), afficherDomaineActivite: true);
            row++;
            row = EcrireBlocRecapParTaux(ws, row, "Totaux par taux — Déductible", modele.RecapsParTaux.Where(r => !r.Collecte), afficherDomaineActivite: true);
            row++;

            row = EcrireBlocRecapParActivite(ws, row, "Totaux par code activité — Collecté", modele.RecapsParActivite.Where(r => r.Collecte));
            row++;
            row = EcrireBlocRecapParActivite(ws, row, "Totaux par code activité — Déductible", modele.RecapsParActivite.Where(r => !r.Collecte));
            row++;

            ws.Columns().AdjustToContents();
        }
    }
}
