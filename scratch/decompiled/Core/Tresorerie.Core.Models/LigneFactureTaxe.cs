using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class LigneFactureTaxe
{
	public string Code { get; set; }

	public ErpTypeTaxe Type { get; set; }

	public ErpTypeTauxTaxe TypeTaux { get; set; }

	public decimal Taux { get; set; }
}
