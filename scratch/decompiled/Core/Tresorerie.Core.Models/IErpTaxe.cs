using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public interface IErpTaxe
{
	int No { get; }

	string Code { get; }

	string Intitule { get; }

	ErpTypeTauxTaxe TypeTaux { get; }

	decimal Taux { get; }

	bool NonPercue { get; }

	ErpSensTaxe Sens { get; }

	ErpTypeTaxe TypeTaxe { get; }

	string CompteGeneral { get; }

	short Provenance { get; }

	decimal Assujettissement { get; }
}
