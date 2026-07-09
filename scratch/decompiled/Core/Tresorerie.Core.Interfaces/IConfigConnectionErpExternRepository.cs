using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IConfigConnectionErpExternRepository
{
	ConfigConnectionErpExtern Get(int societeNo);

	void Update(ConfigConnectionErpExtern config);
}
