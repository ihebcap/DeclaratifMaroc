using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public interface IErpLigneTaxeParamComptable
{
	string Code { get; }

	ErpTypeTaxe Type { get; }

	ErpTypeTauxTaxe TypeTaux { get; }

	decimal Taux { get; }

	string ArticleCompteGeneral { get; }

	decimal Montant { get; }

	string CompteGeneral { get; }

	short Provenance { get; }

	decimal Assujettissement { get; }
}
