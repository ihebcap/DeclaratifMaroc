using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class LigneEngagementClient
{
	public int ClientNo { get; set; }

	public decimal Cours { get; set; }

	public DateTime Date { get; set; }

	public int DeviseNo { get; set; }

	public DateTime Echeance { get; set; }

	public bool IsPointe { get; set; }

	public EtatPreavis Preavis { get; set; }

	public Remis IsRemis { get; set; }

	public DateTime DateRemis { get; set; }

	public decimal Montant { get; set; }

	public decimal MontantCheque { get; set; }

	public decimal MontantDevise { get; set; }

	public decimal MontantFacture { get; set; }

	public decimal MontantImpaye { get; set; }

	public decimal MontantRemboursement { get; set; }

	public decimal MontantTraite { get; set; }

	public int No { get; set; }

	public string Numero { get; set; }

	public bool Remplace { get; set; }

	public int SocieteNo { get; set; }

	public TypeEngagement Type { get; set; }
}
