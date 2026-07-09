using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IModeReglementRepository
{
	int? Create(ModeReglement modeReglement);

	void Delete(ModeReglement modeReglement);

	ModeReglement Get(int no);

	ModeReglement Get(string code);

	IEnumerable<ModeReglement> GetAll();

	bool IsModeUsed(int modeNo, int societeNo);

	bool IsModeUsedByRegleRetenu(int modeNo);

	bool IsModeUsedByTypeOperationRetenue(int modeNo);

	void Update(ModeReglement modeReglement);
}
