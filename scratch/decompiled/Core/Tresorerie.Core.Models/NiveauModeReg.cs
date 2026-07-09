using System.ComponentModel;

namespace Tresorerie.Core.Models;

public enum NiveauModeReg : short
{
	[Description("Traite")]
	TRAITE,
	[Description("Chèque")]
	CHEQUE,
	[Description("Espèce")]
	ESPECE
}
