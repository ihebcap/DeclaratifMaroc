using System.ComponentModel;

namespace Tresorerie.Core.Enum;

public enum ChampStructureVirement
{
	[Description("Numéro")]
	Numero,
	[Description("Date")]
	Date,
	[Description("Montant")]
	Montant,
	[Description("Code devise")]
	Devise,
	[Description("Cours")]
	Cours,
	[Description("Tiers code")]
	TiersCode,
	[Description("Tiers intitule")]
	TiersIntitule,
	[Description("Tiers identifiant")]
	TiersIdentifiant,
	[Description("Tiers banque")]
	TiersBanque,
	[Description("Tiers Rib")]
	TiersRib,
	[Description("Tiers adresse")]
	TiersAdresse,
	[Description("Numéro du pièce ")]
	PieceNumero,
	[Description("Libelle")]
	Libelle,
	[Description("Tiré")]
	Tire,
	[Description("Societé identifiant")]
	SocieteIdentifiant,
	[Description("Societé adresse")]
	SocieteAdresse,
	[Description("Banque")]
	Banque,
	[Description("Rib")]
	Rib,
	[Description("Raison sociale")]
	RaisonSociale,
	[Description("Réference")]
	Reference,
	[Description("Position")]
	Position,
	[Description("Nombre enregistrement")]
	NbEnregistrement,
	[Description("Somme Montant")]
	SommeMontant,
	[Description("Montant devise")]
	MontantDevise,
	[Description("Somme Montant devise")]
	SommeMontantDevisee,
	[Description("Espace vide")]
	EspaceVide,
	[Description("Numéro fichier")]
	NumeroFichier,
	[Description("Indicateur RIB/IBAN")]
	Indicateur_RIB,
	[Description("<Vide>")]
	Vide
}
