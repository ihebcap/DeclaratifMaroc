using System.ComponentModel;

namespace Tresorerie.Core.Enum;

public enum NatureComptaImpaye
{
	[Description("Tiers")]
	Tiers,
	[Description("Compte général")]
	CompteGeneral,
	[Description("Mode règlement")]
	ModeReglement
}
