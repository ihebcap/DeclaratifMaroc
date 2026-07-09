using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IParametreMailRepository
{
	ParametreMailing Get(int societeNo);

	void Update(ParametreMailing parametrageMail);
}
