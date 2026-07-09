using System.ComponentModel;

namespace Tresorerie.Core.Enum;

public enum StatutTransfert
{
	[Description("Aucun")]
	None,
	[Description("Saisi")]
	Saisie,
	[Description("Envoyé")]
	Envoye,
	[Description("Validé")]
	Valide
}
