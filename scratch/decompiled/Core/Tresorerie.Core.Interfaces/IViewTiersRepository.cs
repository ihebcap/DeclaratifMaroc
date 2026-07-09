using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IViewTiersRepository
{
	Tiers Get(int societeNo, int no);
}
