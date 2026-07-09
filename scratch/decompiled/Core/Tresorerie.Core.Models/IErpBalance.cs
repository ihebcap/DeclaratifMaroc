namespace Tresorerie.Core.Models;

public interface IErpBalance
{
	string CompteNumero { get; }

	string CompteIntitule { get; }

	decimal Debit { get; }

	decimal Credit { get; }

	decimal Solde { get; }
}
