using System;

namespace Tresorerie.Core.Models;

public class GarantieCredit
{
	public int No { get; set; }

	public int GarantieNo { get; set; }

	public int CreditNo { get; set; }

	public int Rang { get; set; }

	public int UtilisateurNo { get; set; }

	public DateTime DateCreation { get; set; }
}
