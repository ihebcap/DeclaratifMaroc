namespace Tresorerie.Core.Models;

public class InformationLibre
{
	public int SocieteNo { get; set; }

	public string IntituleInfoLibre1 { get; set; }

	public string IntituleInfoLibre2 { get; set; }

	public string IntituleInfoLibre3 { get; set; }

	public string IntituleInfoLibre4 { get; set; }

	public bool IsObligatoireInfoLibre1 { get; set; }

	public bool IsObligatoireInfoLibre2 { get; set; }

	public bool IsObligatoireInfoLibre3 { get; set; }

	public bool IsObligatoireInfoLibre4 { get; set; }

	public bool IsPersonnaliserInfoLibre1 => !string.IsNullOrEmpty(IntituleInfoLibre1);

	public bool IsPersonnaliserInfoLibre2 => !string.IsNullOrEmpty(IntituleInfoLibre2);

	public bool IsPersonnaliserInfoLibre3 => !string.IsNullOrEmpty(IntituleInfoLibre3);

	public bool IsPersonnaliserInfoLibre4 => !string.IsNullOrEmpty(IntituleInfoLibre4);
}
