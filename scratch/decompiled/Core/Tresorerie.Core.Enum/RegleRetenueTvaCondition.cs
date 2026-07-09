using System.ComponentModel;

namespace Tresorerie.Core.Enum;

public enum RegleRetenueTvaCondition
{
	[Description("=")]
	Egale,
	[Description(">")]
	Superieur,
	[Description("<")]
	Inferieur,
	[Description(">=")]
	SuperieurEgale,
	[Description("<=")]
	InferieurEgale
}
