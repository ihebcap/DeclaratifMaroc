using System;
using System.Collections.Generic;

namespace SageTaxReader.Contracts
{
    public class TaxeDetail
    {
        public double Taux { get; set; }
        public double BaseHT { get; set; }
        public double MontantTva { get; set; }
        public double TTC { get; set; }
        public string Code { get; set; } = "";
        public string Intitule { get; set; } = "";
        public string Type { get; set; } = "";
    }

    public class DocumentTaxesInfo
    {
        public string NumeroPiece { get; set; } = "";
        public string Designation { get; set; } = "";
        public int TypeDocument { get; set; }
        public string Sens { get; set; } = "";
        public double TotalHT { get; set; }
        public double TotalTva { get; set; }
        public double TotalParafiscale { get; set; }
        public double TotalTtc { get; set; }
        public double Escompte { get; set; }
        public double Frais { get; set; }
        public double Acompte { get; set; }
        public double TotalHTNet { get; set; }
        public double EcartArrondi { get; set; }
        public List<TaxeDetail> LignesTaxe { get; set; } = new List<TaxeDetail>();

        // Isolation d'erreur (TASK-023) : une pièce KO n'est jamais silencieusement perdue.
        // Elle ressort dans le lot avec EnErreur=true et un motif exploitable par l'orchestrateur.
        public bool EnErreur { get; set; }
        public string MotifErreur { get; set; } = "";

        // TASK-076 : true uniquement quand TotalHTNet/TotalTva/TotalParafiscale/TotalTtc ont été
        // réellement calculés par ExtraireTaxes (lecture Sage aboutie, incohérente ou non).
        // Reste false quand l'entrée provient d'un échec de lecture pur (EntreeEnErreur — session
        // Sage KO, pièce introuvable, etc.) : dans ce cas aucun montant brut n'a été lu, et il ne
        // faut jamais en exposer un (0 par défaut serait une valeur inventée).
        public bool MontantsBrutsDisponibles { get; set; }
    }

    /// <summary>
    /// TASK-072 : détection d'une pièce dont Σ(HT net + TVA + Parafiscale) ne reconcilie pas
    /// avec le TTC document — au-delà d'un simple écart d'arrondi/escompte (celui-ci reste
    /// toléré au niveau de la déclaration entière, cf. ConstructeurDeclaration.ResiduInexplique).
    /// Cause racine observée : en-tête Sage cassé (ex. pièce FC2501717, HT=1 720 251,20 alors
    /// que TTC=20 700,00 — écart ×99). Seuil combiné (absolu ET relatif) : un écart de quelques
    /// centimes/dizaines de centimes (rounding/escompte légitime) ne doit jamais être exclu ;
    /// un écart d'ordre de grandeur doit toujours l'être.
    /// </summary>
    public static class IncoherenceHtTvaTtc
    {
        public static bool EstIncoherent(double totalHTNet, double totalTva, double totalParafiscale, double totalTtc, out double ecart)
        {
            ecart = totalTtc - (totalHTNet + totalTva + totalParafiscale);
            double ecartAbs = Math.Abs(ecart);
            double seuilRelatif = Math.Abs(totalTtc) * 0.01; // 1% du TTC document
            return ecartAbs > 5.0 && ecartAbs > seuilRelatif;
        }
    }
}
