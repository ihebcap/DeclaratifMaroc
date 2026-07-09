namespace Tresorerie.Core.Models;

public interface IErpAffaire
{
	string Numero { get; set; }

	string Intitule { get; set; }

	int PanAnalytiqueNo { get; set; }
}
