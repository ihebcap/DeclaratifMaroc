using System.ComponentModel;

namespace Tresorerie.Core.Enum;

public enum LettrageType
{
	[Description("Non lettré")]
	NonLettre,
	[Description("Pré-lettré")]
	PreLettre,
	[Description("Lettré")]
	Lettre
}
