using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class Caution
{
	public string BanqueClient { get; set; }

	public int CaisseNo { get; set; }

	public string Commentaire { get; set; }

	public DateTime Date { get; set; }

	public DateTime DateCreation { get; set; }

	public DateTime DateEcheance { get; set; }

	public DateTime DateStatut { get; set; }

	public DomaineCaution Domaine { get; set; }

	public decimal Montant { get; set; }

	public int No { get; set; }

	public string Numero { get; set; }

	public string NumeroPiece { get; set; }

	public int? ReglementNo { get; set; }

	public int SocieteNo { get; set; }

	public StatutCaution Statut { get; set; }

	public string TierCode { get; set; }

	public string TierIntitule { get; set; }

	public int TierNo { get; set; }

	public string Tire { get; set; }

	public int TypeCautionNo { get; set; }

	public int UtilisateurNo { get; set; }

	public int? BanqueSocieteNo { get; set; }

	public int? ChequeNo { get; set; }

	public bool IsChequeBarree { get; set; }

	public bool IsChequeCertifie { get; set; }

	public decimal? MontantPlafond { get; set; }

	public DateTime? DateValidite { get; set; }
}
