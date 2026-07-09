using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class RecapTiersDetailAffectation
{
	public int SocieteNo { get; set; }

	public int EcheanceNo { get; set; }

	public string DocumentNumero { get; set; }

	public DateTime DocumentDate { get; set; }

	public DateTime EcheancePrevue { get; set; }

	public decimal EcheanceMontant { get; set; }

	public int ReglementNo { get; set; }

	public string ReglementNumero { get; set; }

	public DateTime ReglementDate { get; set; }

	public DateTime ReglementEcheance { get; set; }

	public EcheanceType EcheanceType { get; set; }

	public int ModeReglementNo { get; set; }

	public decimal ReglementMontant { get; set; }

	public int AffectationNo { get; set; }

	public decimal AffectationMontant { get; set; }

	public decimal AffectationMontantDevise { get; set; }

	public int TiersNo { get; set; }

	public int CollaborateurNo { get; set; }
}
