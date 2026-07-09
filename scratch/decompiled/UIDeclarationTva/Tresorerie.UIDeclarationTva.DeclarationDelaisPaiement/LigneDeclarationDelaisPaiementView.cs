using System;

namespace Tresorerie.UIDeclarationTva.DeclarationDelaisPaiement;

public class LigneDeclarationDelaisPaiementView
{
	public int No { get; set; }

	public int DeclarationNo { get; set; }

	public int EcheanceNo { get; set; }

	public int? AffectationNo { get; set; }

	public decimal Depassement { get; set; }

	public int TiersNo { get; set; }

	public string TiersCode { get; set; }

	public string TiersIntitule { get; set; }

	public string DocumentNumero { get; set; }

	public DateTime DocumentDate { get; set; }

	public DateTime DocumentEcheancePrevue { get; set; }

	public DateTime DocumentEcheanceLegale { get; set; }

	public decimal DocumentMontant { get; set; }

	public string DocumentInfoLibre1 { get; set; }

	public string DocumentInfoLibre2 { get; set; }

	public string DocumentInfoLibre3 { get; set; }

	public string DocumentInfoLibre4 { get; set; }

	public decimal DocumentSolde { get; set; }

	public int DocumentDeviseNo { get; set; }

	public decimal DocumentCours { get; set; }

	public int? ReglementNo { get; set; }

	public string ReglementNumero { get; set; }

	public DateTime? ReglementDate { get; set; }

	public DateTime? ReglementEcheance { get; set; }

	public decimal? ReglementMontant { get; set; }

	public bool ReglementIsPointe { get; set; }

	public DateTime? ReglementDatePoint { get; set; }

	public decimal? ReglementSolde { get; set; }

	public decimal? ReglementCours { get; set; }

	public decimal? AffectationMontant { get; set; }

	public string ReglementInfoLibre1 { get; set; }

	public string ReglementInfoLibre2 { get; set; }

	public string ReglementInfoLibre3 { get; set; }

	public string ReglementInfoLibre4 { get; set; }

	public int? ReglementModeNo { get; set; }

	public string ReglementPieceNumero { get; set; }
}
