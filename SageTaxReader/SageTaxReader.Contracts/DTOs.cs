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
    }
}
