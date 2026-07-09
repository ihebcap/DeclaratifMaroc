using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class ParameterLibelleCompta
{
	public string Pattern { get; set; }

	public TypePropriety Type { get; set; }

	public object Value { get; set; }
}
