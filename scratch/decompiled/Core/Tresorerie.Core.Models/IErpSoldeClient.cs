namespace Tresorerie.Core.Models;

public interface IErpSoldeClient
{
	int No { get; }

	string Numero { get; }

	decimal SoldeCompta { get; }

	decimal TotalDocument { get; }
}
