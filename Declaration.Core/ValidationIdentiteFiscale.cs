using System;
using Declaration.Core.Model;

namespace Declaration.Core
{
    public static class ValidationIdentiteFiscale
    {
        public static bool EstIfValide(string? identifiantFiscal)
        {
            return !string.IsNullOrWhiteSpace(identifiantFiscal) && identifiantFiscal.Length == 8 && !identifiantFiscal.Contains(" ");
        }

        public static bool EstIceValide(string? ice)
        {
            return !string.IsNullOrWhiteSpace(ice) && ice.Length == 15 && !ice.Contains(" ");
        }

        public static EtatConformite Evaluer(string? identifiantFiscal, string? ice)
        {
            bool ifVide = string.IsNullOrWhiteSpace(identifiantFiscal);
            bool ifValide = EstIfValide(identifiantFiscal);
            
            if (!ifValide)
            {
                return ifVide ? EtatConformite.IfManquant : EtatConformite.FormatInvalide;
            }

            bool iceVide = string.IsNullOrWhiteSpace(ice);
            bool iceValide = EstIceValide(ice);
            
            if (!iceValide)
            {
                return iceVide ? EtatConformite.IceManquant : EtatConformite.FormatInvalide;
            }

            return EtatConformite.Conforme;
        }

        // Validation bloquante à l'export XML DGI.
        // CDC §4.8/§5.2 : ne PAS bloquer sur une longueur fixe (8 pour l'IF, 15 pour l'ICE) sans
        // confirmation du format exact attendu par le fisc (le fichier réellement accepté contient
        // des identifiants fournisseurs de 7 chiffres). On bloque uniquement sur une valeur vide
        // (après nettoyage) ou contenant un espace/tabulation résiduel. Point ouvert §5.2 : format
        // de longueur définitif à confirmer par le PO/fiscaliste.
        public static void ValiderPourExport(string? identifiantFiscal, string? ice, int ord)
        {
            if (string.IsNullOrWhiteSpace(identifiantFiscal) || ContientEspace(identifiantFiscal))
                throw new ApplicationException($"Ligne {ord}: L'identifiant fiscal du tiers est invalide (vide ou contenant un espace).");

            if (string.IsNullOrWhiteSpace(ice) || ContientEspace(ice))
                throw new ApplicationException($"Ligne {ord}: L'ICE du tiers est invalide (vide ou contenant un espace).");
        }

        private static bool ContientEspace(string? valeur)
        {
            if (valeur == null) return false;
            foreach (var c in valeur)
            {
                if (char.IsWhiteSpace(c)) return true;
            }
            return false;
        }
    }
}
