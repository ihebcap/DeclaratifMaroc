namespace Tresorerie.Core.Models;

public interface IErpPlanAnalytique
{
	int No { get; set; }

	string Intitule { get; set; }

	bool IsSaisieObligatoire { get; set; }

	string SectionAttente { get; set; }
}
