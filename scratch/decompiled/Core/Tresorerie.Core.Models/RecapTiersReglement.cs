using System;

namespace Tresorerie.Core.Models;

public class RecapTiersReglement
{
	public decimal AffectationMontant { get; set; }

	public string TiersIntitule { get; set; }

	public int TiersNo { get; set; }

	public string TiersNumero { get; set; }

	public DateTime EcheancePrevue { get; set; }

	public DateTime DocumentDate { get; set; }

	public decimal EcheanceMontant { get; set; }

	public string EcheanceNumero { get; set; }

	public decimal EcheanceSolde { get; set; }

	public int NbReglement { get; set; }

	public int NbReglementPartiellementSolde { get; set; }

	public int NbReglementTotalementSolde { get; set; }

	public DateTime ReglementDate { get; set; }

	public DateTime ReglementEcheance { get; set; }

	public decimal ReglementMontant { get; set; }

	public string ReglementNumero { get; set; }

	public decimal ReglementSolde { get; set; }

	public int SocieteNo { get; set; }

	public decimal TotalReglementInclueDmp { get; set; }

	public decimal TotalMontantReglement { get; set; }

	public decimal TotalSoldeReglement { get; set; }
}
