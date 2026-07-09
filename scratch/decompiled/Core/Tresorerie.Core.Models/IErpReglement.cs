using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public interface IErpReglement
{
	string Numero { get; set; }

	DateTime Date { get; set; }

	string Libelle { get; set; }

	string ErpModeReglementDesignation { get; set; }

	int ModificateurNo { get; set; }

	decimal Montant { get; set; }

	decimal MontantDevise { get; set; }

	int ErpDeviseNo { get; set; }

	decimal Cours { get; set; }

	TiersType TiersType { get; set; }

	DateTime DateCreation { get; set; }

	DateTime DateModification { get; set; }

	string TiersPayeur { get; set; }

	DateTime ImpayeDate { get; set; }

	string Journal { get; set; }

	string CompteGeneral { get; set; }

	string PieceNumero { get; set; }

	int CaisseNo { get; set; }

	DateTime DateEcheance { get; set; }

	string ErpDeviseIntitule { get; set; }

	int ErpModeNo { get; set; }

	int? BanqueId { get; set; }

	int ErpNo { get; set; }

	int Id { get; set; }

	bool IsComptabilise { get; set; }

	string Reference { get; set; }
}
