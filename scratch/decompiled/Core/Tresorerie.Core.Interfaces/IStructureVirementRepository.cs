using System.Collections.Generic;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IStructureVirementRepository
{
	int Create(StructureVirement structureVirement);

	void Delete(int no);

	StructureVirement Get(int no);

	IEnumerable<StructureVirement> GetAll(int banqueNo, TypeFichierBordoreau typeFichier, int societeNo);

	void Update(StructureVirement structureVirement);

	void DeleteAllByBanque(int banqueNo, TypeFichierBordoreau typeFichier);
}
