using System;
using System.Collections.Generic;

namespace Declaration.Controle;

public class LigneDeclarationGrfn
{
    public int DTL_Id { get; set; }
    public decimal DTL_Assiette { get; set; }
    public decimal DTL_Taux { get; set; }
    public decimal DTL_Montant { get; set; }
    public string DTL_TiersCode { get; set; } = string.Empty;
    public string DTL_DocNumero { get; set; } = string.Empty;
    public DateTime DTL_DocDate { get; set; }
    public string DTL_MvNumero { get; set; } = string.Empty;
    public DateTime DTL_MvDate { get; set; }
    public string DTL_TypePayement { get; set; } = string.Empty;
    public int DTL_EntityType { get; set; }
    public string DTL_Domaine { get; set; } = string.Empty;
    public decimal DTL_Prorata { get; set; }
}

public class LigneEcart
{
    public string NumeroFacture { get; set; } = string.Empty;
    public decimal Taux { get; set; }
    public string NumeroRapprochement { get; set; } = string.Empty; // MvNumero
    
    public LigneDeclarationGrfn? LigneGrfn { get; set; }
    public Declaration.Core.Model.LigneDeclarationEnrichie? LigneRecalculee { get; set; }
    
    public decimal EcartAssiette => (LigneGrfn?.DTL_Assiette ?? 0m) - (LigneRecalculee?.HT ?? 0m);
    public decimal EcartTVA => (LigneGrfn?.DTL_Montant ?? 0m) - (LigneRecalculee?.Tva ?? 0m);

    public TypeEcart Type { get; set; }
    public string CauseHypothetique { get; set; } = string.Empty;
}

public enum TypeEcart
{
    Concordant,
    EcartMontant,
    ManquantGRFN,
    ManquantRecalcul
}

public class RapportEcarts
{
    public List<LigneEcart> Lignes { get; set; } = new();
    
    public int NbConcordants => Lignes.Count(l => l.Type == TypeEcart.Concordant);
    public int NbEcartsMontant => Lignes.Count(l => l.Type == TypeEcart.EcartMontant);
    public int NbManquantsGRFN => Lignes.Count(l => l.Type == TypeEcart.ManquantGRFN); // Présent dans le recalcul, absent de GRFN
    public int NbManquantsRecalcul => Lignes.Count(l => l.Type == TypeEcart.ManquantRecalcul); // Présent dans GRFN, absent du recalcul
    
    public decimal TotalEcartTVA => Lignes.Sum(l => Math.Abs(l.EcartTVA));
}
