using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ISolvabiliteClientRepository
{
	SolvabiliteClient GetSolvabiliteClient(int clientNo, int societeNo);

	decimal GetTotalCredit(int clientNo, int societeNo);

	decimal GetTotalDebit(int clientNo, int societeNo);
}
