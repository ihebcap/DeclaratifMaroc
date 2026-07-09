namespace Tresorerie.Core.Models;

public class VerifySoldeCaisse
{
	public string CaisseCode { get; set; }

	public int CaisseNo { get; set; }

	public bool IsValide { get; set; }

	public decimal Solde { get; set; }

	public decimal TotalEntree { get; set; }

	public decimal TotalSortie { get; set; }
}
