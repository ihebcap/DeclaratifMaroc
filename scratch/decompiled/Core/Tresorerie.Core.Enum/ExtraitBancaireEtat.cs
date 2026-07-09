using System.ComponentModel;

namespace Tresorerie.Core.Enum;

public enum ExtraitBancaireEtat : short
{
	[Description("Non rapproché")]
	NonRapproche,
	[Description("Partiellement rapproché")]
	PartielRapproche,
	[Description("Totalement rapproché")]
	TotalRapproche
}
