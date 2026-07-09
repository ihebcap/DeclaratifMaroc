using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class Cheque
{
	public int ChequierNo { get; set; }

	public DateTime Date { get; set; }

	public DateTime Echeance { get; set; }

	public bool IsBarre { get; set; }

	public decimal Montant { get; set; }

	public string MotifAnnulation { get; set; }

	public int No { get; set; }

	public string Numero { get; set; }

	public ChequeStatut Statut { get; set; }

	public int? TiersNo { get; set; }

	public string Tire { get; set; }

	public DateTime? DateValiditer { get; set; }

	public decimal? MontantPlafond { get; set; }
}
