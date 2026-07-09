using System.ComponentModel;

namespace Tresorerie.Core.Enum;

public enum ErpTypeActionCodeRisque
{
	[Description("A livrer")]
	ALivrer,
	[Description("A surveiller")]
	ASurveiller,
	[Description("A bloquer")]
	ABloquer
}
