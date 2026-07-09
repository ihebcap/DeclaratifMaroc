using System;

namespace Tresorerie.Core.Models;

public class EngagementFournisseur
{
	public int FournisseurNo { get; set; }

	public decimal AssuranceCredit { get; set; }

	public decimal EncoursAutoriser { get; set; }

	public int SocieteNo { get; set; }

	public decimal TotalCaution { get; set; }

	public decimal TotalCheque { get; set; }

	public decimal TotalEngagement => Math.Max(TotalCheque + TotalTraite + TotalFacture + TotalBonLivraison + TotalImpaye + TotalRemboursement - TotalRegNonAffecte, 0m);

	public decimal RisqueReel => Math.Max(TotalEngagement - AssuranceCredit - TotalCaution, 0m);

	public decimal TotalDepassement => TotalEngagement - EncoursAutoriser;

	public decimal TotalFacture { get; set; }

	public decimal TotalImpaye { get; set; }

	public decimal TotalRegNonAffecte { get; set; }

	public decimal TotalRemboursement { get; set; }

	public decimal TotalTraite { get; set; }

	public decimal TotalBonLivraison { get; set; }
}
