using System;

namespace Tresorerie.Core.Models;

public class ClotureCaisse
{
	public int No { get; set; }

	public string Numero { get; set; }

	public DateTime Date { get; set; }

	public int CaisseOrigineNo { get; set; }

	public int CaisseDestinationNo { get; set; }

	public DateTime DateDebut { get; set; }

	public DateTime DateFin { get; set; }

	public decimal Montant { get; set; }

	public int UserNo { get; set; }

	public int SocieteNo { get; set; }

	public decimal TotalEspece { get; set; }

	public decimal TotalCheque { get; set; }

	public decimal TotalAutre { get; set; }

	public decimal TotalTraite { get; set; }

	public decimal TotalVirement { get; set; }

	public int NbVirement { get; set; }

	public int NbCheque { get; set; }

	public int NbTraite { get; set; }

	public int NbAutre { get; set; }

	public int NbEspece { get; set; }
}
