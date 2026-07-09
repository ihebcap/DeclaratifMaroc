using System.ComponentModel;

namespace Tresorerie.Core.Enum;

public enum ErpBaseEncours
{
	[Description("Solde comptable")]
	Solde,
	[Description("Solde + FA")]
	SoldeFa,
	[Description("Solde + FA + BL")]
	SoldeFaBl,
	[Description("Solde + FA + BL + PL")]
	SoldeFaBlPl,
	[Description("Solde + FA + BL + PL + BC")]
	SoldeFaBlPlBc
}
