using System.ComponentModel;

namespace Tresorerie.Core.Enum;

public enum NatureActionEtape
{
	[Description("Automatique")]
	Automatique,
	[Description("Échéance")]
	Echeance,
	[Description("Rapprochement")]
	Rapprochement
}
