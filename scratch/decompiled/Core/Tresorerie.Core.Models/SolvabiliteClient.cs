using System;

namespace Tresorerie.Core.Models;

public class SolvabiliteClient
{
	public int ClientNo { get; private set; }

	public bool IsDebiteur => TotalDebit >= TotalCredit;

	public int SocieteNo { get; private set; }

	public decimal Solde => Math.Abs(TotalCredit - TotalDebit);

	public decimal TotalCredit { get; private set; }

	public decimal TotalDebit { get; private set; }

	public SolvabiliteClient(int clientNo, int societeNo, decimal totalDebit, decimal totalCredit)
	{
		ClientNo = clientNo;
		SocieteNo = societeNo;
		TotalCredit = totalCredit;
		TotalDebit = totalDebit;
	}
}
