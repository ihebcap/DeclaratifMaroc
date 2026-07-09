using System.ComponentModel;

namespace Tresorerie.Core.Enum;

public enum TypeMouvementBancaire
{
	[Description("Chèque")]
	Cheque,
	Traite,
	Virement,
	Versement,
	Retrait,
	Interne,
	[Description("Remboursement Client")]
	RemboursementClient,
	VirementTiers,
	[Description("Crédit")]
	LigneCredit,
	[Description("Dépense")]
	Depense
}
