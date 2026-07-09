using System.ComponentModel;

namespace Tresorerie.Core.Enum;

public enum EtatComptabilite : short
{
	[Description("N")]
	NonComptabilise,
	[Description("O")]
	Comptabilise,
	[Description("TFC")]
	TraiteFournisseurComptabilise
}
