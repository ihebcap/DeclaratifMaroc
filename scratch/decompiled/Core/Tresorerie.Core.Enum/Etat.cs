using System.ComponentModel;

namespace Tresorerie.Core.Enum;

public enum Etat : short
{
	[Description("Non payé")]
	NonPaye,
	[Description("Totalement payé")]
	TotalementPaye
}
