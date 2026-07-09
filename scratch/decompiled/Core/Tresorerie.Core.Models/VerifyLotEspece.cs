namespace Tresorerie.Core.Models;

public class VerifyLotEspece
{
	public string CaisseCode { get; set; }

	public int CaisseNo { get; set; }

	public bool Epuise { get; set; }

	public bool IsValide { get; set; }

	public decimal MontantEntree { get; set; }

	public decimal MontantRestant { get; set; }

	public int MvtNo { get; set; }

	public int No { get; set; }

	public decimal TotalSortie { get; set; }
}
