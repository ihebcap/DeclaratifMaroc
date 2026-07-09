using System.ComponentModel;

namespace Tresorerie.Core.Enum;

public enum TypeEngagementClient : short
{
	Facture = 1,
	[Description("Bon livraison")]
	BonLivraison,
	[Description("Impayé")]
	Impaye,
	Remboursement,
	[Description("Chèque")]
	Cheque,
	Traite
}
