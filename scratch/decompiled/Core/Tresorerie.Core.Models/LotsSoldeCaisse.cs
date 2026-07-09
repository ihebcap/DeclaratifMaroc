using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class LotsSoldeCaisse
{
	public string BanqueClient { get; set; }

	public int? BanqueNo { get; set; }

	public int CaisseNo { get; set; }

	public int ClientNo { get; set; }

	public DateTime Date { get; set; }

	public MouvementDomaine Domaine { get; set; }

	public int ModeNo { get; set; }

	public decimal Montant { get; set; }

	public decimal MontantRestant { get; set; }

	public int MouvementNo { get; set; }

	public string Numero { get; set; }

	public string Piece { get; set; }

	public string Tire { get; set; }

	public int DeviseNo { get; set; }
}
