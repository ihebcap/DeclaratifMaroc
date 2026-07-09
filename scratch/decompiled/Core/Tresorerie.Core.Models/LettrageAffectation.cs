namespace Tresorerie.Core.Models;

public class LettrageAffectation
{
	public decimal AffectationMontant { get; set; }

	public int AffectationNo { get; set; }

	public decimal EcheanceMontant { get; set; }

	public int EcheanceNo { get; set; }

	public bool IsPointer { get; set; }

	public int PointerNo { get; set; }

	public decimal ReglementMontant { get; set; }

	public int ReglementNo { get; set; }

	public override string ToString()
	{
		return string.Format("{4} E {0} : {1} | R : {2} : {3} ", EcheanceNo, EcheanceMontant, ReglementNo, ReglementMontant, PointerNo);
	}
}
