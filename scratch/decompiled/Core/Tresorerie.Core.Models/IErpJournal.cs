namespace Tresorerie.Core.Models;

public interface IErpJournal
{
	string CompteGeneralNumero { get; }

	string Intitule { get; }

	int No { get; }

	string Numero { get; }

	bool IsAnalytique { get; }

	ErpTypeJournal Type { get; }
}
