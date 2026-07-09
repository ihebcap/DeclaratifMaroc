using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ICaisseModeReglementRepository
{
	void Create(CaisseModeReglement mode);

	void Delete(CaisseModeReglement mode);

	IEnumerable<CaisseModeReglement> GetAll(int caisseNo);

	bool IsModeUsed(int modeNo, int caisseNo);

	void Update(CaisseModeReglement mode);
}
