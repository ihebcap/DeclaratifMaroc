namespace Tresorerie.Core.Models;

public interface IErpChiffreAffaireClient
{
	decimal ChiffreAffaire { get; }

	int No { get; }

	string Numero { get; }
}
