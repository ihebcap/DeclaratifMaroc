using System.ComponentModel;

namespace Tresorerie.Core.Enum;

public enum TypeMailling
{
	[Description("SMTP")]
	SMTP,
	[Description("SendGrid")]
	SendGrid
}
