using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public interface IErpSectionAnalytique
{
	string Numero { get; set; }

	string Intitule { get; set; }

	int PanAnalytiqueNo { get; set; }

	ErpComptaTypeSectionAnalytique TypeNo { get; set; }
}
