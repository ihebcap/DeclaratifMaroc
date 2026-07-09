using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class VentilationAnalytique
{
	public int No { get; set; }

	public int EntityNo { get; set; }

	public int PlanAnalytiqueNo { get; set; }

	public string SectionNumero { get; set; }

	public decimal Montant { get; set; }

	public int SocieteNo { get; set; }

	public string CompteNum { get; set; } = string.Empty;

	public AnalytiqueDomaine Domaine { get; set; }
}
