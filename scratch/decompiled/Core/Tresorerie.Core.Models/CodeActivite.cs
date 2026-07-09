using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class CodeActivite
{
	public int No { get; set; }

	public string Code { get; set; }

	public string Intitule { get; set; }

	public CodeActiviteDomaine Domaine { get; set; }

	public decimal Taux { get; set; }

	public decimal Prorata { get; set; }

	public string Famille { get; set; }
}
