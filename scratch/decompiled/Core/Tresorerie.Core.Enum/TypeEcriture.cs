using System.ComponentModel;

namespace Tresorerie.Core.Enum;

public enum TypeEcriture
{
	[Description("Écritures non lettrées")]
	NonLettre,
	[Description("Écritures lettrées")]
	Lettre,
	[Description("Toutes les écritures")]
	Tout
}
