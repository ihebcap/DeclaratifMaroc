using System.ComponentModel;

namespace Tresorerie.Core.Enum;

public enum TiersType : short
{
	[Description("Client")]
	Client,
	[Description("Fournisseur")]
	Fournisseur,
	[Description("Salarié")]
	Salarie,
	[Description("Autre")]
	Autre
}
