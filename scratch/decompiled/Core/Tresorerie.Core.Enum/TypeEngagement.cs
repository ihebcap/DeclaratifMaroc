using System.ComponentModel;

namespace Tresorerie.Core.Enum;

public enum TypeEngagement
{
	Echeance = 0,
	Impaye = 1,
	[Description("Chèque")]
	Cheque = 2,
	Traite = 3,
	BonLivraisonErp = 4,
	FactureErp = 5,
	BonCommandeErp = 6,
	RemboursementClient = 8,
	Autre = 9
}
