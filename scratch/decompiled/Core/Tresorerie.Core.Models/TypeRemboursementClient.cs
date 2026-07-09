using System.ComponentModel;

namespace Tresorerie.Core.Models;

public enum TypeRemboursementClient
{
	[Description("Virrement tiers")]
	VirementTiers = 102,
	[Description("Remboursement client espèce")]
	RemboursementEspece = 103,
	[Description("Remboursement client chèque")]
	RemboursementCheque = 104,
	[Description("Remboursement client R/S")]
	RemboursementRS = 106
}
