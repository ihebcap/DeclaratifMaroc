using System.ComponentModel;

namespace Tresorerie.Core.Enum;

public enum TypeIntegrationPrevisionnelleFournisseur
{
	[Description("Aucun")]
	Aucun,
	[Description("FA")]
	Facture,
	[Description("FA+BL")]
	FactureBonLivraison,
	[Description("FA+BL+BC")]
	FactureBonLivraisonBonCommande,
	[Description("FA+BL+BC+PC")]
	FactureBonLivraisonBonCommandePreparation,
	[Description("FA+BL+BC+PC+DA")]
	FactureBonLivraisonBonCommandePreparationDevis
}
