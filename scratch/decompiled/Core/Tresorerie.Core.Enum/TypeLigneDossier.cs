using System.ComponentModel;

namespace Tresorerie.Core.Enum;

public enum TypeLigneDossier
{
	[Description("EC")]
	Ecriture,
	[Description("RS")]
	Retenue,
	[Description("R")]
	Reglement,
	[Description("É")]
	Echeance
}
