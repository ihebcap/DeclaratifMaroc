using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Declaration.Core.Model;
using Declaration.Core;

namespace Declaration.Export.Xml
{
    public class DeclarationXmlExporter
    {
        public string GenererXml(DeclarationModele modele, string dossierSortie)
        {
            if (modele == null) throw new ArgumentNullException(nameof(modele));
            if (modele.EnTete == null) throw new ArgumentException("L'en-tête de la déclaration est manquant.");
            
            var lignesFiltrees = modele.Lignes.Where(l => !l.IsReport).ToList();
            if (!lignesFiltrees.Any()) throw new ApplicationException("Aucune ligne à exporter.");

            // Fichier : {Numero}-{Exercice}-{M|T}{période}.xml
            string periodeStr = modele.EnTete.Type == TypePeriode.Mensuelle 
                ? $"M{modele.EnTete.MoisPeriode}" 
                : $"T{modele.EnTete.TrimestrePeriode}";
            
            string baseFileName = $"{modele.EnTete.Numero}-{modele.EnTete.Exercice}-{periodeStr}";
            string xmlPath = Path.Combine(dossierSortie, $"{baseFileName}.xml");
            string zipPath = Path.Combine(dossierSortie, $"{baseFileName}.zip");

            if (File.Exists(xmlPath) || File.Exists(zipPath))
            {
                throw new ApplicationException($"Le fichier {baseFileName} existe déjà.");
            }

            var sb = new StringBuilder();
            sb.Append("\r\n<DeclarationReleveDeduction>\r\n");
            sb.Append($"<identifiantFiscal>{modele.EnTete.IdentifiantSociete}</identifiantFiscal>\r\n");
            sb.Append($"<annee>{modele.EnTete.Exercice}</annee>\r\n");
            
            int periode = modele.EnTete.Type == TypePeriode.Mensuelle 
                ? (modele.EnTete.MoisPeriode ?? 0) 
                : (modele.EnTete.TrimestrePeriode ?? 0);
            sb.Append($"<periode>{periode}</periode>\r\n");
            sb.Append($"<regime>{(modele.EnTete.Type == TypePeriode.Mensuelle ? 1 : 2)}</regime>\r\n");
            sb.Append("<releveDeductions>\r\n");

            int ord = 1;
            foreach (var ligne in lignesFiltrees)
            {
                // Validation Maroc
                ValidationIdentiteFiscale.ValiderPourExport(ligne.Tiers?.IdentifiantFiscal, ligne.Tiers?.Ice, ord);

                string modeId = MapModePaiement(ligne.ModePaiement);

                // Arrondi: Ne PAS re-arrondir, formatage seul
                // Décimal: NumberFormatInfo InvariantCulture pour le "."
                NumberFormatInfo nfi = new CultureInfo("en-US", false).NumberFormat;

                sb.Append("<rd>\r\n");
                sb.Append($"<ord>{ord}</ord>\r\n");
                sb.Append($"<num>{EscapeXml(ligne.NumeroFacture)}</num>\r\n");
                sb.Append($"<des>{EscapeXml(ligne.Designation)}</des>\r\n");
                sb.Append($"<mht>{ligne.HT.ToString("0.00", nfi)}</mht>\r\n");
                sb.Append($"<tva>{ligne.Tva.ToString("0.00", nfi)}</tva>\r\n");
                sb.Append($"<ttc>{ligne.Ttc.ToString("0.00", nfi)}</ttc>\r\n");
                sb.Append("<refF>");
                sb.Append($"<if>{EscapeXml(ligne.Tiers.IdentifiantFiscal)}</if>");
                sb.Append($"<nom>{EscapeXml(ligne.Tiers.Nom)}</nom>");
                sb.Append($"<ice>{EscapeXml(ligne.Tiers.Ice)}</ice>");
                sb.Append("</refF>\r\n");
                sb.Append($"<tx>{ligne.Taux.ToString("0.00", nfi)}</tx>\r\n");
                sb.Append($"<prorata>{ligne.Prorata.ToString("0.00", nfi)}</prorata>\r\n");
                sb.Append($"<mp><id>{modeId}</id></mp>\r\n");
                sb.Append($"<dpai>{(ligne.DatePaiement.HasValue ? ligne.DatePaiement.Value.ToString("yyyy-MM-dd") : "")}</dpai>\r\n");
                sb.Append($"<dfac>{(ligne.DateFacture.HasValue ? ligne.DateFacture.Value.ToString("yyyy-MM-dd") : "")}</dfac>\r\n");
                sb.Append("</rd>\r\n");

                ord++;
            }

            sb.Append("</releveDeductions>\r\n");
            sb.Append("</DeclarationReleveDeduction>");

            // utf-8 sans BOM
            var utf8WithoutBom = new UTF8Encoding(false);
            File.WriteAllText(xmlPath, sb.ToString(), utf8WithoutBom);

            using (var zipStream = new FileStream(zipPath, FileMode.Create))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                archive.CreateEntryFromFile(xmlPath, $"{baseFileName}.xml", CompressionLevel.Optimal);
            }

            return zipPath;
        }

        private string MapModePaiement(string mode)
        {
            if (string.IsNullOrWhiteSpace(mode)) return "7"; // Autre par défaut
            
            // Espèce=1, Chèque=2, Virement=4, Traite=5, Autre=7 ; OperationBancaire=3
            mode = mode.ToLowerInvariant();
            if (mode.Contains("espèce") || mode.Contains("espece") || mode == "1") return "1";
            if (mode.Contains("chèque") || mode.Contains("cheque") || mode == "2") return "2";
            if (mode.Contains("opération bancaire") || mode.Contains("operation bancaire") || mode.Contains("operationbancaire") || mode == "3") return "3";
            if (mode.Contains("virement") || mode == "4") return "4";
            if (mode.Contains("traite") || mode == "5") return "5";
            
            return "7";
        }

        private string EscapeXml(string unescaped)
        {
            if (string.IsNullOrEmpty(unescaped)) return "";
            return System.Security.SecurityElement.Escape(unescaped);
        }
    }
}
