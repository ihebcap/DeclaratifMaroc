using System.ComponentModel.DataAnnotations;

namespace Tresorerie.Core.Enum;

public enum EcheanceType : short
{
	[Display(Name = "FC", Description = "Facture Erp")]
	Erp = 0,
	[Display(Name = "IMP", Description = "Impayé")]
	Impaye = 1,
	[Display(Name = "RN", Description = "Règlement Négatif")]
	ReglementNegatif = 3,
	[Display(Name = "S", Description = "Solde")]
	Solde = 4,
	[Display(Name = "G", Description = "Gain")]
	Gain = 90,
	[Display(Name = "P", Description = "Perte")]
	Perte = 91,
	[Display(Name = "GE", Description = "Gain de change")]
	GainEchange = 100,
	[Display(Name = "PE", Description = "Perte de change")]
	PerteEchange = 101,
	[Display(Name = "VM", Description = "Virement tiers")]
	VirementTiers = 102,
	[Display(Name = "RB", Description = "Remboursement client espèce")]
	RemboursementClientEspece = 103,
	[Display(Name = "RBS", Description = "Remboursement client R/S")]
	RemboursementClientRS = 106,
	[Display(Name = "RC", Description = "Remboursement client")]
	RemboursementDivers = 104,
	[Display(Name = "FT", Description = "Facture Trésorerie")]
	FactureFrsTresorerie = 105,
	[Display(Name = "RBF", Description = "Remboursement fournisseur")]
	RemboursementFournisseur = 107,
	[Display(Name = "IMPF", Description = "Impayé fournisseur")]
	ImpayeFournisseur = 108,
	[Display(Name = "CI", Description = "Commission impayé")]
	CommissionImpaye = 109,
	[Display(Name = "II", Description = "Intérêt impayé")]
	InteretImpaye = 110,
	[Display(Name = "FGR", Description = "Facture GR")]
	FactureGR = 111,
	[Display(Name = "DT", Description = "Droit de timbre")]
	DroitTimbreClient = 112
}
