namespace Tresorerie.Core.Models;

public class EcritureAnalytique
{
	public int EcritureNo { get; set; }

	public int LigneNo { get; set; }

	public decimal Montant { get; set; }

	public string CompteAnalytique { get; set; }

	public int PlanAnalytiqueNo { get; set; }
}
