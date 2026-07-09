using System.ComponentModel;

namespace Tresorerie.Core.Enum;

public enum ReglementConditionType : short
{
	[Description("Jour(s) net(s)")]
	JourNet,
	[Description("Fin mois civil")]
	FinMoisCivil,
	[Description("Fin mois")]
	FinMois
}
