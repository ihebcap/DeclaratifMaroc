using System;

namespace Tresorerie.Core.Models;

public class SolvabiliteFournisseur
{
	public int FournisseurNo { get; private set; }

	public bool IsDebiteur => TotalDebit >= TotalCredit;

	public int SocieteNo { get; private set; }

	public decimal Solde => Math.Abs(TotalCredit - TotalDebit);

	public decimal TotalCredit { get; private set; }

	public decimal TotalDebit { get; private set; }

	public SolvabiliteFournisseur(int fournisseurNo, int societeNo, decimal totalCredit, decimal totalDebit)
	{
		FournisseurNo = fournisseurNo;
		SocieteNo = societeNo;
		TotalCredit = totalCredit;
		TotalDebit = totalDebit;
	}
}
