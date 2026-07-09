using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class LotSoldeCaisseEspece
{
	public int CaisseNo { get; set; }

	public int DeviseNo { get; set; }

	public DateTime Date { get; set; }

	public DateTime DateCreation { get; set; }

	public MouvementDomaine Domaine { get; set; }

	public decimal Montant { get; set; }

	public decimal MontantCredit { get; set; }

	public decimal MontantDebit { get; set; }

	public int MouvementNo { get; set; }

	public string MouvementNumero { get; set; }

	public SensHistorique Sens { get; set; }

	public int SocieteNo { get; set; }

	public string AffaireNumero { get; set; }
}
