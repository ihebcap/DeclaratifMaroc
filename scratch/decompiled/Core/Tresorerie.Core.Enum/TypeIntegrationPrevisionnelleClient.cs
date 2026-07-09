using System.ComponentModel;

namespace Tresorerie.Core.Enum;

public enum TypeIntegrationPrevisionnelleClient
{
	[Description("Aucun")]
	Aucun,
	[Description("FA")]
	Facture,
	[Description("FA+BL")]
	FactureBonLivraison,
	[Description("FA+BL+PL")]
	FactureBonLivraisonPreparation,
	[Description("FA+BL+PL+BC")]
	FactureBonLivraisonPreparationBonCommande,
	[Description("FA+BL+PL+BC+DV")]
	FactureBonLivraisonPreparationBonCommandeDevis
}
