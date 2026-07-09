namespace Tresorerie.Core.Models;

public class VerifySoldeEcheance
{
	public bool Etat { get; set; }

	public bool IsValide { get; set; }

	public decimal Montant { get; set; }

	public int No { get; set; }

	public string Numero { get; set; }

	public decimal Solde { get; set; }

	public decimal TotalMontantAffectation { get; set; }
}
