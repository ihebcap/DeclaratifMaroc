using System;

namespace Tresorerie.Core.Models;

public class LigneExtraitBancaire
{
	public int No { get; set; }

	public int ExtraitBancaireNo { get; set; }

	public DateTime DateOperation { get; set; }

	public DateTime? DateValeur { get; set; }

	public string Libelle { get; set; }

	public string NumeroPiece { get; set; }

	public string Reference { get; set; }

	public bool IsRapproche { get; set; }

	public string RapprochementLettre { get; set; }

	public DateTime? RapprochementDate { get; set; }

	public string OperationInterneBanque { get; set; }

	public string OperationInterBancaire { get; set; }

	public decimal Montant { get; set; }

	public decimal MontantCredit
	{
		get
		{
			if (!(Montant > 0m))
			{
				return 0m;
			}
			return Montant;
		}
	}

	public decimal MontantDebit
	{
		get
		{
			if (!(Montant < 0m))
			{
				return 0m;
			}
			return Math.Abs(Montant);
		}
	}
}
