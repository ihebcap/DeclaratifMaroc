using System;

namespace Tresorerie.UIDeclarationTva.DeclarationDelaisPaiement;

public class LigneControleDelaisPaiementView
{
	public int EcheanceNo { get; set; }

	public string EcheanceNumero { get; set; }

	public DateTime EcheanceDateDocument { get; set; }

	public DateTime EcheanceDatePrevue { get; set; }

	public DateTime EcheanceLegale { get; set; }

	public decimal EcheanceMontant { get; set; }

	public decimal EcheanceSolde { get; set; }

	public decimal EcheanceMontantDeviseSociete { get; set; }

	public decimal EcheanceSoldeDeviseSociete { get; set; }

	public int EcheanceDeviseNo { get; set; }

	public string EcheanceDeviseCode { get; set; }

	public decimal EcheanceCours { get; set; }

	public string DocumentInfoLibre1 { get; set; }

	public string DocumentInfoLibre2 { get; set; }

	public string DocumentInfoLibre3 { get; set; }

	public string DocumentInfoLibre4 { get; set; }

	public int? ReglementNo { get; set; }

	public string ReglementNumero { get; set; }

	public DateTime? ReglementDate { get; set; }

	public DateTime? ReglementEcheance { get; set; }

	public decimal? ReglementMontant { get; set; }

	public decimal? ReglementSolde { get; set; }

	public decimal? ReglementMontantDeviseSociete { get; set; }

	public decimal? ReglementSoldeDeviseSociete { get; set; }

	public decimal? ReglementCours { get; set; }

	public bool IsReglementComptabilise { get; set; }

	public bool IsReglementPoint { get; set; }

	public DateTime? ReglementDatePointage { get; set; }

	public string ReglementInfoLibre1 { get; set; }

	public string ReglementInfoLibre2 { get; set; }

	public string ReglementInfoLibre3 { get; set; }

	public string ReglementInfoLibre4 { get; set; }

	public int? ReglementModeNo { get; set; }

	public string ReglementPieceNumero { get; set; }

	public int TiersNo { get; set; }

	public string TiersNumero { get; set; }

	public string TiersIntitule { get; set; }

	public int? AffectationNo { get; set; }

	public decimal? AffectationMontant { get; set; }

	public int Depassement { get; set; }
}
