using System.ComponentModel;

namespace Tresorerie.Core.Enum;

public enum ReglementType : short
{
	[Description("Espèce")]
	Espece,
	[Description("Chèque")]
	Cheque,
	[Description("Traite")]
	Traite,
	[Description("Virement")]
	Virement,
	[Description("Autre")]
	Autre
}
