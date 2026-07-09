using System.ComponentModel;

namespace Tresorerie.Core.Enum;

public enum EntityNumerotation : short
{
	[Description("Règlement client")]
	ReglementClient = 1,
	[Description("Règlement fournisseur")]
	ReglementFournisseur,
	[Description("Entête bordereau")]
	Bordereau,
	[Description("Transfert")]
	Transfert,
	[Description("Dossier fournisseur")]
	DossierFournisseur,
	[Description("Retenue")]
	Retenue,
	[Description("Alimentation caisse")]
	AlimentationCaisse,
	[Description("Dépense")]
	Depense,
	[Description("Virement interne")]
	VirementInterne,
	[Description("Virement tiers")]
	VirementTiers,
	[Description("Ligne virement tiers client")]
	LigneVirementTiers,
	[Description("Remboursement client")]
	RemboursementClient,
	[Description("Remboursement divers")]
	RemboursementDivers,
	[Description("Rappel")]
	Rappel,
	[Description("Caution")]
	Caution,
	[Description("Facture fournisseur")]
	FactureFournisseur,
	[Description("Clôture caisse")]
	ClotureCaisse,
	[Description("Dossier règlement impayé")]
	DossierImpaye,
	[Description("Dossier opération bancaire")]
	DossierOperationBancaire,
	[Description("Déclaration TVA/Encaissement")]
	DeclarationTvaEncaissement,
	[Description("Déclaration retenue à la source")]
	DeclarationRetenuSource,
	[Description("Ordre de virement")]
	BordereauVirement,
	[Description("Déclaration délais de paiement")]
	DeclarationDelaisPaiement,
	[Description("Dossier client")]
	DossierClient,
	[Description("Dossier règlement impayé Fourisseur")]
	DossierImpayeFournisseur,
	[Description("Crédit")]
	Credit
}
