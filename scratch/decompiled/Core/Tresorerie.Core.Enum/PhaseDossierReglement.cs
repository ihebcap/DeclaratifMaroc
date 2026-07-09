using System.ComponentModel;

namespace Tresorerie.Core.Enum;

public enum PhaseDossierReglement
{
	[Description("Saisie")]
	Saisie,
	[Description("Validation facture")]
	ValidationFacture,
	[Description("Saisie paiement")]
	SaisiePaiement,
	[Description("Clôturé")]
	Cloture
}
