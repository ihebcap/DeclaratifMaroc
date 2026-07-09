namespace Tresorerie.Core.Models;

public class FourchetteCommission
{
	public int No { get; set; }

	public int NombreJourMin { get; set; }

	public int NombreJourMax { get; set; }

	public decimal TauxCheque { get; set; }

	public decimal TauxTraite { get; set; }

	public decimal TauxVirement { get; set; }

	public decimal TauxEspece { get; set; }

	public decimal TauxAutre { get; set; }

	public int SocieteNo { get; set; }
}
