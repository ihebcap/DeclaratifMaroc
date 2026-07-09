using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ISocieteModeReglementRepository
{
	void Create(SocieteModeReglement mode);

	void Delete(SocieteModeReglement mode);

	SocieteModeReglement Get(int societeNo, int modeNo);

	IEnumerable<SocieteModeReglement> GetAll(int societeNo);

	SocieteModeReglement HasErpMapping(int modeErpNo, int societeNo);

	void Update(SocieteModeReglement mode);
}
