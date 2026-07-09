using System.ComponentModel;

namespace Tresorerie.Core.Enum;

public enum TypeCalculEngagement
{
	[Description("Par rapport à l'échéance (+ nombre de jours couverture banque)")]
	Echeance,
	[Description("Par rapport au rapprochement")]
	Rapprochement
}
