using System.ComponentModel;

namespace Tresorerie.Core.Enum;

public enum StatutCredit
{
	[Description("Simulation")]
	Simulation,
	[Description("En cours")]
	EnCours,
	[Description("Confirmé")]
	Confirmer,
	[Description("Accordé")]
	Accorder
}
