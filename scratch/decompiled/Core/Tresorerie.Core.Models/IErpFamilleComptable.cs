using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public interface IErpFamilleComptable
{
	int No { get; }

	int CategorieComptable { get; }

	string CodeFamille { get; }

	ErpDomaine Domaine { get; }

	string CompteGeneral { get; }

	string CompteAnalytique { get; }

	string CodeTaxe1 { get; }

	string CodeTaxe2 { get; }

	string CodeTaxe3 { get; }

	DateTime DateTaxe1 { get; }

	DateTime DateTaxe2 { get; }

	DateTime DateTaxe3 { get; }

	string AncienCodeTaxe1 { get; }

	string AncienCodeTaxe2 { get; }

	string AncienCodeTaxe3 { get; }
}
