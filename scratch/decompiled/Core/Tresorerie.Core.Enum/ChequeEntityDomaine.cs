using System.ComponentModel;

namespace Tresorerie.Core.Enum;

public enum ChequeEntityDomaine : short
{
	[Description("Règlement")]
	ReglementFournisseur = 0,
	[Description("Alimentation")]
	AlimentationCaisse = 1,
	[Description("Remboursement")]
	RemboursementDivers = 2,
	[Description("Virement interne")]
	VirementInterne = 3,
	[Description("Caution")]
	Caution = 4,
	[Description("Autre")]
	Autre = 99
}
