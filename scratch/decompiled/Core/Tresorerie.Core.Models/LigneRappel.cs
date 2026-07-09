using System;

namespace Tresorerie.Core.Models;

public class LigneRappel
{
	public string ClientIntitule { get; set; }

	public int ClientNo { get; set; }

	public string ClientNumero { get; set; }

	public DateTime DateCreation { get; set; }

	public DateTime EcheanceDate { get; set; }

	public int EcheanceNo { get; set; }

	public string EcheanceNumero { get; set; }

	public decimal Montant { get; set; }

	public int No { get; set; }

	public int RappelNo { get; set; }

	public string RappelNumero { get; set; }

	public decimal Solde { get; set; }

	public decimal SoldeCloture { get; set; }

	public decimal SoldeRappel { get; set; }
}
