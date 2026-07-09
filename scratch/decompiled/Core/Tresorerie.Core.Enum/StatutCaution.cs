using System.ComponentModel;

namespace Tresorerie.Core.Enum;

public enum StatutCaution
{
	[Description("Actif")]
	None,
	[Description("Récupérée")]
	Recupere,
	[Description("Annulée")]
	Annule,
	[Description("Réglée")]
	Reglement
}
