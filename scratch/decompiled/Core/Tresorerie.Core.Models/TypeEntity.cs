using System.ComponentModel;

namespace Tresorerie.Core.Models;

public enum TypeEntity
{
	None,
	[Description("Règlement")]
	Reglement,
	[Description("Echéance")]
	Echeance,
	Transfert,
	Bordereau,
	Historique,
	Administration,
	Parametrage,
	ReglementFournisseur,
	Chequier,
	EcheanceEcart,
	Alimentation,
	[Description("Dépense")]
	Depense,
	Dossier,
	VirementInterne,
	VirementTiers,
	RemboursementClient,
	ImpayeFournisseur,
	Rappel,
	Caution,
	Cheques,
	RemboursementFournisseur,
	DossierImpaye,
	DeclarationTvaEncaissement,
	AttestationRetenueFournisseur,
	CarnetTraite,
	Traites,
	DeclarationRetenuSource,
	Tiers,
	EcritureTresorerie,
	ConventionDelaisPaiementTiers,
	BordereauVirement,
	DeclarationDelaisPaiement,
	Note,
	DossierClient,
	DossierImpayeFournisseur,
	Credit,
	Affectation,
	BanqueTiers
}
