using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class ReglementCoffre
{
	public string Numero { get; set; }

	public DateTime Date { get; set; }

	public string Piece { get; set; }

	public int ClientNo { get; set; }

	public string ClientNumero { get; set; }

	public string ClientIntitule { get; set; }

	public string Tire { get; set; }

	public string BanqueClient { get; set; }

	public decimal Montant { get; set; }

	public decimal MontantMouvement { get; set; }

	public int ModeNo { get; set; }

	public DateTime PieceEcheance { get; set; }

	public int CaisseNo { get; set; }

	public string CaisseCode { get; set; }

	public string CaisseIntitule { get; set; }

	public ReglementType TypeNo { get; set; }

	public string ModeCode { get; set; }

	public string ModeIntitule { get; set; }

	public int SocieteNo { get; set; }

	public int DeviseNo { get; set; }

	public string DeviseCode { get; set; }

	public string DeviseIntitule { get; set; }
}
