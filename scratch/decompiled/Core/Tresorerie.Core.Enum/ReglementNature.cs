using System.ComponentModel;

namespace Tresorerie.Core.Enum;

public enum ReglementNature : short
{
	[Description("Règlement")]
	Reglement,
	[Description("Avance")]
	Avance
}
