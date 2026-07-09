namespace Tresorerie.Core.Models;

public interface IErpDevise
{
	decimal Cours { get; }

	string Format { get; }

	string Intitule { get; }

	string Monnaie { get; }

	int No { get; }

	string Sigle { get; }

	string SousMonnaie { get; }

	string CodeISO { get; }

	bool NonCertain { get; }

	int NombreDecimales { get; }
}
