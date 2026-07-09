using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class DetailHistoriqueRappelEcheance
{
	public string ClientIntitule { get; set; }

	public int ClientNo { get; set; }

	public string ClientNumero { get; set; }

	public DateTime DateCloture { get; set; }

	public DateTime DateRappel { get; set; }

	public DomaineRappel DomaineRappel { get; set; }

	public DateTime EcheanceDate { get; set; }

	public decimal EcheanceMontant { get; set; }

	public int EcheanceNo { get; set; }

	public string EcheanceNumero { get; set; }

	public decimal EcheanceSolde { get; set; }

	public bool IsRappelCloture { get; set; }

	public DateTime LigneDateCreation { get; set; }

	public int LigneRappelNo { get; set; }

	public decimal LigneSoldeCloture { get; set; }

	public decimal LigneSoldeRappel { get; set; }

	public DateTime RappelDateCreation { get; set; }

	public int RappelNo { get; set; }

	public string RappelNumero { get; set; }
}
