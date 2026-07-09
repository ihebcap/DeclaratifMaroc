using System.ComponentModel;

namespace Tresorerie.Core.Enum;

public enum LettreRecouvrementFieldValue
{
	[Description("Numéro document")]
	NumeroDocument = 1,
	[Description("Date")]
	DateDocument,
	[Description("Client")]
	ClientCode,
	[Description("Intitulé client")]
	ClientIntitule,
	[Description("Payeur")]
	PayeurCode,
	[Description("Intitulé payeur")]
	PayeurIntitule,
	[Description("Echéance")]
	DateEcheance,
	[Description("Montant Devise")]
	Montant,
	[Description("Sode Devise")]
	Solde,
	[Description("Montant")]
	MontantDeviseSociete,
	[Description("Solde")]
	SoldeDeviseSociete,
	[Description("Devise")]
	Devise,
	[Description("Cours")]
	Cours,
	[Description("Commentaire")]
	Commentaire,
	[Description("Info. Libre 1")]
	InfoLibre1,
	[Description("Info. Libre 2")]
	InfoLibre2,
	[Description("Info. Libre 3")]
	InfoLibre3,
	[Description("Info. Libre 4")]
	InfoLibre4,
	[Description("Référence")]
	Reference
}
