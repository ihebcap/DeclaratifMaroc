using System;

namespace Tresorerie.Core.Models;

public class LigneCredit
{
	public int No { get; set; }

	public string Numero { get; set; } = string.Empty;

	public int CreditNo { get; set; }

	public decimal MontantAssurance { get; set; }

	public DateTime DateEcheance { get; set; }

	public decimal TauxInteret { get; set; }

	public decimal MontantMensualite { get; set; }

	public decimal MontantCapitalRembourse { get; set; }

	public decimal MontantCapitalRestantDu { get; set; }

	public decimal MontantInteret { get; set; }

	public decimal MontantFraisDossier { get; set; }

	public bool IsPointer { get; set; }

	public DateTime? DatePointage { get; set; }

	public string NumExtrait { get; set; }

	public bool IsEcheanceReporter { get; set; }

	public string CreditNumero { get; set; }

	public int BanqueNo { get; set; }

	public string AffaireNumero { get; set; }

	public override bool Equals(object obj)
	{
		if (obj == null)
		{
			return false;
		}
		if (!(obj is LigneCredit ligneCredit))
		{
			return false;
		}
		return ligneCredit.GetHashCode() == GetHashCode();
	}

	public override int GetHashCode()
	{
		return (17 * 23 + No) * 23 + Numero.GetHashCode();
	}
}
