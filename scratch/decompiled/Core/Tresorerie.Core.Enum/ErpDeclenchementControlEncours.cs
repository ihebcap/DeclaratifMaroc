using System.ComponentModel;

namespace Tresorerie.Core.Enum;

public enum ErpDeclenchementControlEncours
{
	[Description("Aucun")]
	Aucun,
	[Description("Création de l'entête document")]
	CreationEnteteDocument,
	[Description("Validation du ligne")]
	ValidationLigne,
	[Description("Fermeture du document")]
	FermetureDocument
}
