using System.ComponentModel;

namespace Tresorerie.Core.Enum;

public enum NatureCaution
{
	[Description("Chèque")]
	Cheque,
	Traite,
	Autre,
	Bancaire
}
