using System.ComponentModel;

namespace Tresorerie.Core.Enum;

public enum DomainePrevisionnelle : short
{
	[Description("")]
	Null = 100,
	[Description("Solde initial")]
	SoldeInitial = 99,
	[Description("Solde bancaire")]
	SoldeBancaire = 98,
	[Description("Règlement client")]
	ReglementClient = 0,
	[Description("Règlement frs")]
	ReglementFourniseur = 1,
	[Description("Versement")]
	VersementEspece = 2,
	[Description("Ali. caisse")]
	Alimentation = 3,
	[Description("Echéance client")]
	EcheanceClient = 4,
	[Description("Echéance frs")]
	EcritureFournisseur = 5,
	[Description("Op. bancaire")]
	OperationBancaire = 6,
	[Description("Prévision")]
	Prevision = 7,
	[Description("Document erp")]
	ErpClient = 8,
	[Description("Document erp")]
	ErpFournisseur = 9,
	[Description("Echéance frs")]
	EcheanceFournisseur = 10,
	[Description("Virement interne")]
	VirementInterne = 11,
	[Description("Virement tiers")]
	VirementTiers = 12,
	[Description("Crédit")]
	LigneCredit = 13,
	[Description("Dépense")]
	Depense = 14
}
