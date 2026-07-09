using System.ComponentModel;

namespace Tresorerie.Core.Enum;

public enum TypeMontantServi
{
	[Description("Montant brut est null")]
	MontantBrutNull,
	[Description("Honoraires")]
	Honoraires,
	[Description("Commissions")]
	Commissions,
	[Description("Courtages")]
	Courtages,
	[Description("Loyers")]
	Loyers,
	[Description("Rémunérations au titre des activités non commerciales")]
	Remuneration,
	[Description("Autres revenus")]
	AutresRevenus,
	[Description("Les montants servis aux non résidents établis en Tunisie et qui ne procèdent pas au dépôt de la déclaration d’existence avant d’entamer leur activité.")]
	ServisResident,
	[Description("Rémunérations payées en contre partie de la performance dans la prestation.")]
	RenumerationPaye
}
