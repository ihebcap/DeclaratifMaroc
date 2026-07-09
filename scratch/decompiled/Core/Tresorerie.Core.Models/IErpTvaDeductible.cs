namespace Tresorerie.Core.Models;

public interface IErpTvaDeductible
{
	string Code { get; set; }

	string CompteGeneral { get; set; }

	string Intitule { get; set; }

	int No { get; set; }

	decimal Taux { get; set; }
}
