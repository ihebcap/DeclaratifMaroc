using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ISolvabiliteFournisseurRepository
{
	SolvabiliteFournisseur GetSolvabiliteFournisseur(int fournisseurNo, int societeNo);

	decimal GetTotalCredit(int fournisseurNo, int societeNo);

	decimal GetTotalDebit(int fournisseurNo, int societeNo);
}
