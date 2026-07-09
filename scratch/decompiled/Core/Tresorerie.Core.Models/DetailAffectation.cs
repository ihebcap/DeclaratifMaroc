using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class DetailAffectation
{
	public ErpDocumentType DocumentType { get; set; }

	public string TiersPayeurNo { get; set; }

	public string PayeurIntitule { get; set; }

	public int SocieteNo { get; set; }

	public int No { get; set; }

	public int TiersNo { get; set; }

	public int ErpNo { get; set; }

	public string TiersIntitule { get; set; }

	public string TiersNumero { get; set; }

	public string EcheanceType { get; set; }

	public string ReglementNumero { get; set; }

	public string EcheancePiece { get; set; }

	public DateTime DateEcheanceReglement { get; set; }

	public DateTime DateReglement { get; set; }

	public int RepresentantNo { get; set; }

	public EcheanceType EcheanceTypeCode { get; set; }

	public string ReglementBanque { get; set; }

	public DateTime AffectationDate { get; set; }

	public decimal AffectationMontant { get; set; }

	public decimal AffectationMontantDevise { get; set; }

	public decimal ReglementMontant { get; set; }

	public string EcheanceMode { get; set; }

	public string ReglementPiece { get; set; }

	public int ReglementCaisseNo { get; set; }

	public DateTime DateEcheance { get; set; }

	public DateTime DateDocument { get; set; }

	public string DeviseCode { get; set; }

	public int DeviseNo { get; set; }

	public ErpDomaine Domaine { get; set; }

	public decimal DelaisMoyenPaiementAffectation { get; set; }

	public int ReglementNo { get; set; }

	public int? ReglementErpNo { get; set; }

	public int? EcheanceErpNo { get; set; }

	public int ReglementModeNo { get; set; }

	public int EcheanceNo { get; set; }

	public bool IsSynchronised { get; set; }

	public bool ReglementIsRetenu { get; set; }

	public decimal ReglementSolde { get; set; }

	public string ReglementTire { get; set; }

	public bool ReglementIsPointer { get; set; }

	public string ReglementBordereau { get; set; }

	public string ReglementCommentaire { get; set; }

	public bool Equals(DetailAffectation item)
	{
		if (item == null)
		{
			return false;
		}
		return GetHashCode() == item.GetHashCode();
	}

	public override int GetHashCode()
	{
		return new
		{
			A = No,
			B = ReglementNumero,
			C = EcheancePiece,
			D = ErpNo
		}.GetHashCode();
	}

	public override bool Equals(object obj)
	{
		if (obj == null)
		{
			return false;
		}
		if (obj is DetailAffectation item)
		{
			return Equals(item);
		}
		return false;
	}

	public override string ToString()
	{
		return $"[Détail affectation] {No}, {ReglementNumero}, {EcheancePiece}";
	}
}
