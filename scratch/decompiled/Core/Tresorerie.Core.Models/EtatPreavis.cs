using System.ComponentModel;

namespace Tresorerie.Core.Models;

public enum EtatPreavis
{
	[Description("Non préavis")]
	NonPreavis,
	[Description("Chèque préavisé")]
	Preavis,
	[Description("Impayé confirmée")]
	RegulariseImpaye,
	[Description("Chèque payé")]
	RegularisePaye
}
