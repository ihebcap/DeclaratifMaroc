using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class TypeCaution
{
	public string Code { get; set; }

	public string Intitule { get; set; }

	public NatureCaution Nature { get; set; }

	public bool CanChangeToReglement { get; set; }

	public int No { get; set; }
}
