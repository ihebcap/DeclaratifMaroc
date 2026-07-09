using System.ComponentModel;

namespace Tresorerie.Core.Enum;

public enum Annexe4
{
	[Description("Montant brut servi au titre des honoraires, commissions...")]
	A414,
	[Description("Montant des honoraires servis aux personnes non résidentes qui réalisent des travaux de construction ou des opérations de montage ou des services de contrôles connexes ou d'autres services pour une période ne dépassant pas 6 mois.")]
	A416,
	[Description("Montant de la plus-value immobili\u008fre.")]
	A418,
	[Description("Montant plus-value de cession des actions, des parts sociales ou des parts de fonds prévues par la législation")]
	A420,
	[Description("Montant valeur mobiliere")]
	A4221,
	[Description("Montant jetons presence")]
	A4222,
	[Description("Montant actions part sociale")]
	A4223,
	[Description("Montant brut des honoraires, commissions, courtages, loyers et rémunérations des activités non commerciales provenant des opérations d’exportation.")]
	A424,
	[Description("Montant des rémunérations ou revenus servis à des personnes résidentes ou établies dans des paradis fiscaux.")]
	A425
}
