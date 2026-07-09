using System.ComponentModel;

namespace Tresorerie.UIDeclarationTva.Stuctures;

public enum SelectedEntityFiltre
{
	Tous = 1,
	[Description("Décaissement")]
	Decaissement,
	[Description("Mouvement bancaire")]
	MouvementBancire,
	[Description("Encaissement")]
	Encaissement
}
