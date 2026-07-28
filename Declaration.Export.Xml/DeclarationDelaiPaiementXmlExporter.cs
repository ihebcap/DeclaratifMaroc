using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using Declaration.Core.Model;

namespace Declaration.Export.Xml
{
    /// <summary>
    /// TASK-133 : générateur XML/ZIP de dépôt Délai de Paiement Maroc — reproduit à l'identique la
    /// structure de <c>Tresorerie.UIDeclarationTva/Infrastructures/DeclarationDelaisPaiementFileGenerator.cs</c>
    /// (legacy), décision PO §5.A-6 (19/07/2026) : reprendre la structure XML à l'identique, pas de
    /// refonte du format. Développement NEUF (aucune réutilisation de DLL/code WinForms) : seule la
    /// SÉMANTIQUE (tags, ordre, codes) est reproduite.
    ///
    /// Toute la LOGIQUE DE CALCUL (solde restant, montant payé, code mode de paiement) a déjà été
    /// appliquée en amont par <see cref="DeclarationDelaiPaiementLigneCalculator"/> — cette classe ne
    /// fait QUE sérialiser le modèle déjà calculé, comme <see cref="DeclarationXmlExporter"/> pour le
    /// module TVA.
    ///
    /// Différences volontaires par rapport au legacy (documentées VERIFY, aucune n'altère la
    /// structure) :
    /// <list type="bullet">
    /// <item>échappement XML des champs texte libres (<see cref="EscapeXml"/>) — le legacy interpolait
    /// les chaînes brutes, risque de fichier mal formé sur un caractère <c>&amp;</c>/<c>&lt;</c> dans
    /// une adresse ou un intitulé ;</item>
    /// <item>formatage décimal invariant sans arrondi forcé à un nombre de décimales de devise société
    /// (infrastructure absente de GRF web) — même convention que <see cref="DeclarationXmlExporter"/>
    /// (TVA), déjà approuvée pour ce dépôt.</item>
    /// </list>
    /// </summary>
    public sealed class DeclarationDelaiPaiementXmlExporter
    {
        /// <summary>
        /// Génère le XML + ZIP dans <paramref name="dossierSortie"/>. Échec EXPLICITE si l'un des deux
        /// fichiers existe déjà (legacy : "Le fichier XML/ZIP […] existe déja.") — ne jamais écraser
        /// silencieusement. Retourne le chemin du ZIP produit.
        /// </summary>
        public string GenererXml(DeclarationDelaiPaiementXmlModele modele, string dossierSortie)
        {
            if (modele == null) throw new ArgumentNullException(nameof(modele));
            if (modele.EnTete == null) throw new ArgumentException("L'en-tête de la déclaration est manquant.");
            if (string.IsNullOrWhiteSpace(dossierSortie)) throw new ArgumentException("Le dossier de sortie est obligatoire.", nameof(dossierSortie));

            var (baseFileName, xmlPath, zipPath) = CalculerChemins(modele.EnTete, dossierSortie);

            if (File.Exists(xmlPath))
                throw new ApplicationException($"Le fichier XML [{baseFileName}] existe déja.");
            if (File.Exists(zipPath))
                throw new ApplicationException($"Le fichier ZIP [{baseFileName}] existe déja.");

            var xml = ConstruireXml(modele);

            // UTF-8 SANS BOM, même convention que DeclarationXmlExporter (module TVA).
            var utf8SansBom = new UTF8Encoding(false);
            File.WriteAllText(xmlPath, xml, utf8SansBom);

            using (var zipStream = new FileStream(zipPath, FileMode.Create))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                // Le XML seul dans l'archive (legacy : un seul CreateEntry).
                archive.CreateEntryFromFile(xmlPath, $"{baseFileName}.xml", CompressionLevel.Optimal);
            }

            return zipPath;
        }

        /// <summary>
        /// Chemins déterministes (mêmes règles de nommage que la génération), pour permettre à
        /// l'appelant de tester leur existence / de piloter l'annulation SANS reconstruire le modèle
        /// complet. Nommage <c>{Numero}-{Exercice}-{T1..T4|A}</c> (legacy l.174).
        /// </summary>
        public static (string BaseFileName, string XmlPath, string ZipPath) CalculerChemins(
            EnTeteDeclarationDelaiPaiementXml enTete, string dossierSortie)
        {
            if (enTete == null) throw new ArgumentNullException(nameof(enTete));
            if (string.IsNullOrWhiteSpace(dossierSortie)) throw new ArgumentException("Le dossier de sortie est obligatoire.", nameof(dossierSortie));

            var baseFileName = $"{enTete.Numero}-{enTete.Exercice}-{enTete.PeriodeFichier}";
            return (
                baseFileName,
                Path.Combine(dossierSortie, $"{baseFileName}.xml"),
                Path.Combine(dossierSortie, $"{baseFileName}.zip"));
        }

        private static string ConstruireXml(DeclarationDelaiPaiementXmlModele modele)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\r\n");
            sb.Append("<DeclarationDelaiPaiement>\r\n");
            sb.Append($"<identifiantFiscal>{EscapeXml(modele.EnTete.IdentifiantFiscalSociete)}</identifiantFiscal>\r\n");
            sb.Append($"<annee>{modele.EnTete.Exercice}</annee>\r\n");
            sb.Append($"<periode>{modele.EnTete.PeriodeXml}</periode>\r\n");
            sb.Append($"<activite>{modele.EnTete.ActiviteMarrocCode}</activite>\r\n");

            // Bloc conditionnel legacy (générateur l.66-71) : UNIQUEMENT si EnProcedure (code 2).
            if (modele.EnTete.ActiviteMarrocCode == 2)
            {
                sb.Append($"<dateJugementOuvrProc>{FormatDate(modele.EnTete.DateJugement)}</dateJugementOuvrProc>\r\n");
            }

            sb.Append($"<chiffreAffaire>{FormatDecimal(modele.EnTete.ChiffreAffaire)}</chiffreAffaire>\r\n");
            sb.Append("<listeFacturesHorsDelai>\r\n");

            foreach (var facture in modele.Factures)
            {
                sb.Append("<FactureHorsDelai>\r\n");
                sb.Append($"<identifiantFiscal>{EscapeXml(facture.IdentifiantFiscalFournisseur)}</identifiantFiscal>\r\n");
                sb.Append($"<numRC>{EscapeXml(facture.NumRc)}</numRC>\r\n");
                sb.Append($"<adresseSiegeSocial>{EscapeXml(facture.AdresseSiegeSocial)}</adresseSiegeSocial>\r\n");
                sb.Append($"<numFacture>{EscapeXml(facture.NumFacture)}</numFacture>\r\n");
                sb.Append($"<dateEmission>{FormatDate(facture.DateEmission)}</dateEmission>\r\n");
                sb.Append("<natureMarchandise></natureMarchandise>\r\n");
                sb.Append($"<dateLivraisonMarchandise>{FormatDate(facture.DateLivraisonMarchandise)}</dateLivraisonMarchandise>\r\n");
                sb.Append($"<dateConvenuePaiementFacture>{FormatDate(facture.DateConvenuePaiementFacture)}</dateConvenuePaiementFacture>\r\n");
                sb.Append($"<montantFactureTtc>{FormatDecimal(facture.MontantFactureTtc)}</montantFactureTtc>\r\n");
                sb.Append($"<montantNonEncorePaye>{FormatDecimal(facture.MontantNonEncorePaye)}</montantNonEncorePaye>\r\n");
                sb.Append($"<montantPayeHorsDelai>{FormatDecimal(facture.MontantPayeHorsDelai)}</montantPayeHorsDelai>\r\n");

                // 3 balises optionnelles : UNIQUEMENT si payée/pointée dans la période (legacy l.153-160).
                if (facture.PayeeHorsDelaiPeriode)
                {
                    sb.Append($"<datePaiementHorsDelai>{FormatDate(facture.DatePaiementHorsDelai)}</datePaiementHorsDelai>\r\n");
                    sb.Append($"<modePaiement>{(facture.ModePaiement.HasValue ? facture.ModePaiement.Value.ToString(CultureInfo.InvariantCulture) : string.Empty)}</modePaiement>\r\n");
                    sb.Append($"<referencePaiement>{EscapeXml(facture.ReferencePaiement)}</referencePaiement>\r\n");
                }

                sb.Append("</FactureHorsDelai>\r\n");
            }

            sb.Append("</listeFacturesHorsDelai>\r\n");
            sb.Append("</DeclarationDelaiPaiement>");

            return sb.ToString();
        }

        private static string FormatDate(DateTime? date) => date.HasValue ? date.Value.ToString("yyyy-MM-dd") : string.Empty;

        // Culture invariante ("." décimal), pleine précision préservée — même convention que
        // DeclarationXmlExporter (module TVA), aucune infrastructure de décimales par devise société
        // dans GRF web (cf. NOTES VERIFY).
        private static string FormatDecimal(decimal value) => value.ToString("0.##########", CultureInfo.InvariantCulture);

        private static string EscapeXml(string? unescaped)
        {
            if (string.IsNullOrEmpty(unescaped)) return string.Empty;
            return System.Security.SecurityElement.Escape(unescaped) ?? string.Empty;
        }
    }
}
