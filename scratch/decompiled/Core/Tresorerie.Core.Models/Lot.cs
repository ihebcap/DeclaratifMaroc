namespace Tresorerie.Core.Models;

public class Lot
{
	public bool IsEpuise => MontantRestant.Equals(0m);

	public decimal Montant { get; set; }

	public decimal MontantRestant { get; internal set; }

	public int No { get; private set; }

	public Lot(int no, decimal montant, decimal montantRest)
	{
		No = no;
		Montant = montant;
		MontantRestant = montantRest;
	}
}
