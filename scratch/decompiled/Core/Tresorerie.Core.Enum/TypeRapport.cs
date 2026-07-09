using System.ComponentModel;

namespace Tresorerie.Core.Enum;

public enum TypeRapport
{
	Standard = 1,
	Bordereaux,
	[Description("Personnalisé")]
	Personnaliser
}
