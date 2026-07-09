using System.ComponentModel;

namespace Tresorerie.Core.Enum;

public enum TypeComptabilisationReglementFrs : short
{
	[Description("Date opération")]
	DateOperation,
	[Description("Date système")]
	DateSysteme,
	[Description("Date récupération")]
	DateRecuperation
}
