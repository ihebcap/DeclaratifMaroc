using System.ComponentModel;

namespace Tresorerie.Core.Enum;

public enum TypeVentilationAnalytique : short
{
	[Description("Sans")]
	Aucun,
	[Description("Affaire")]
	Affaire,
	[Description("Plan analytique")]
	PlanAnalytique
}
