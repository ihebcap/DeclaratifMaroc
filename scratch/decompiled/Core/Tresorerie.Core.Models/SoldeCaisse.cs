namespace Tresorerie.Core.Models;

public class SoldeCaisse
{
	public string CaisseCode { get; set; }

	public int CaisseNo { get; set; }

	public string ModeCode { get; set; }

	public int ModeNo { get; set; }

	public decimal Solde { get; set; }

	public int DeviseNo { get; set; }
}
