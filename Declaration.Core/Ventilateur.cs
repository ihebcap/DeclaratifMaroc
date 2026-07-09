using System;
using System.Collections.Generic;
using System.Linq;
using SageTaxReader.Contracts;

namespace Declaration.Core
{
    public class LigneDeclaration
    {
        public decimal Taux { get; set; }
        public decimal Assiette { get; set; }
        public decimal Tva { get; set; }
        public decimal Ttc { get; set; }
        public decimal Prorata { get; set; }
        public string CodeTaxe { get; set; } = "";
    }

    public class VentilationResult
    {
        public IReadOnlyList<LigneDeclaration> Lignes { get; set; } = Array.Empty<LigneDeclaration>();
        public decimal TotalAssiette { get; set; }
        public decimal TotalTva { get; set; }
        public decimal TotalTtc { get; set; }
    }

    public static class Ventilateur
    {
        public static VentilationResult Ventiler(DocumentTaxesInfo facture, decimal montantAffecte, int n)
        {
            decimal totalTtcFacture = (decimal)facture.TotalTtc;
            if (totalTtcFacture == 0)
                throw new DivideByZeroException("TotalTtc de la facture est 0.");

            decimal proration = montantAffecte / totalTtcFacture;
            decimal prorataAffiche = proration * 100m;

            var lignes = new List<LigneDeclaration>();

            foreach (var taxe in facture.LignesTaxe)
            {
                if (taxe.Type != null && !taxe.Type.StartsWith("TaxeTypeTVA") && taxe.Type != "0" && taxe.Type != "")
                    continue;

                decimal ht_i = (decimal)taxe.BaseHT;
                decimal tva_i = (decimal)taxe.MontantTva;

                decimal assiette = Math.Round(ht_i * proration, n, MidpointRounding.AwayFromZero);
                decimal tva = Math.Round(tva_i * proration, n, MidpointRounding.AwayFromZero);
                decimal ttc_ligne = assiette + tva;

                lignes.Add(new LigneDeclaration
                {
                    CodeTaxe = taxe.Code,
                    Taux = (decimal)taxe.Taux,
                    Assiette = assiette,
                    Tva = tva,
                    Ttc = ttc_ligne,
                    Prorata = prorataAffiche
                });
            }

            return new VentilationResult
            {
                Lignes = lignes,
                TotalAssiette = lignes.Sum(l => l.Assiette),
                TotalTva = lignes.Sum(l => l.Tva),
                TotalTtc = lignes.Sum(l => l.Ttc)
            };
        }
    }
}
