using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ICaisseBanqueRepository
{
	CaisseBanque GetDefault(Caisse caisse);

	void Delete(Caisse caisse);
}
