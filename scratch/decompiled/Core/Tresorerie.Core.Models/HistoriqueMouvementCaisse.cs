using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class HistoriqueMouvementCaisse
{
	public int? BanqueNo { get; set; }

	public string CaisseCode { get; set; }

	public int CaisseNo { get; set; }

	public int? CaisseNoOut { get; set; }

	public DateTime Date { get; set; }

	public MouvementDomaine Domaine { get; set; }

	public int EnteteBordereauNo { get; set; }

	public bool IsAnnule { get; set; }

	public bool IsLotEpuise { get; set; }

	public decimal LotMontant { get; set; }

	public decimal LotMontantRestant { get; set; }

	public int LotNo { get; set; }

	public string ModeCode { get; set; }

	public int ModeReglementNo { get; set; }

	public int MouvementInNo { get; set; }

	public int MouvementOutNo { get; set; }

	public int No { get; set; }

	public string NumeroIn { get; set; }

	public string NumeroOut { get; set; }

	public string Piece { get; set; }

	public SensMouvement Sens { get; set; }

	public int DeviseId { get; set; }
}
