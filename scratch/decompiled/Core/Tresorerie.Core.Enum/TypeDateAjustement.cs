using System.ComponentModel;

namespace Tresorerie.Core.Enum;

public enum TypeDateAjustement
{
	[Description("Date opération")]
	DateOperation,
	[Description("Date règlement")]
	DateReglement,
	[Description("Date échéance")]
	DateEcheance
}
