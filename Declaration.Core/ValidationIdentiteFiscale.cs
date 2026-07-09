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

        public static void ValiderPourExport(string? identifiantFiscal, string? ice, int ord)
        {
            if (!EstIfValide(identifiantFiscal))
                throw new ApplicationException($"Ligne {ord}: L'identifiant fiscal du tiers est invalide (doit faire 8 caractères sans espaces).");
                
            if (!EstIceValide(ice))
                throw new ApplicationException($"Ligne {ord}: L'ICE du tiers est invalide (doit faire 15 caractères sans espaces).");
        }
    }
}
