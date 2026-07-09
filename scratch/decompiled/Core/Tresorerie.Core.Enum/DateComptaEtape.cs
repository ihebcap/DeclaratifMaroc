using System.ComponentModel;

namespace Tresorerie.Core.Enum;

public enum DateComptaEtape
{
	[Description("Opération")]
	Operation,
	[Description("Échéance")]
	Echeance,
	[Description("Date valeur")]
	Valeur,
	[Description("Échéance + Nb. jours couverture BQ")]
	EcheanceCouverture
}
