namespace Tresorerie.Core.Models;

public class VerifySoldeReglementToReplace
{
	public bool IsValide { get; set; }

	public decimal Montant { get; set; }

	public int No { get; set; }

	public string Numero { get; set; }

	public decimal SoldeToReplace { get; set; }

	public decimal TotalMontantRemplacement { get; set; }
}
