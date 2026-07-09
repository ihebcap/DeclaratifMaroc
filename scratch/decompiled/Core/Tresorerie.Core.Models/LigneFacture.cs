namespace Tresorerie.Core.Models;

public class LigneFacture
{
	public int No { get; set; }

	public int FactureNo { get; set; }

	public string Article { get; set; }

	public string Designation { get; set; }

	public decimal Quantite { get; set; }

	public decimal PrixUnitaire { get; set; }

	public decimal PrixUnitaireDevise { get; set; }

	public decimal Remise { get; set; }

	public decimal MontantHT { get; set; }

	public decimal MontantTTC { get; set; }

	public LigneFactureTaxe Taxe1 { get; set; }

	public LigneFactureTaxe Taxe2 { get; set; }

	public LigneFactureTaxe Taxe3 { get; set; }
}
