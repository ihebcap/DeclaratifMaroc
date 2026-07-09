using System.ComponentModel;

namespace Tresorerie.Core.Enum;

public enum TypePrevisionnel
{
	[Description("Solde initial")]
	SoldeInitial,
	[Description("Réalisé")]
	Realise,
	[Description("Encours")]
	EnCours,
	[Description("Prévisionnel")]
	Prevision,
	[Description("Solde bancaire")]
	SoldeBancaire
}
