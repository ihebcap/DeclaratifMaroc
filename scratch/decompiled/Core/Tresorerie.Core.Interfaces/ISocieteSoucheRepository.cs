using System.Collections.Generic;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ISocieteSoucheRepository
{
	void Create(SocieteSouche societeSouche);

	void Delete(SocieteSouche societeSouche);

	SocieteSouche Get(int societeNo, int soucheNo, ErpDomaine domaine);

	IEnumerable<SocieteSouche> GetAll(int societeNo);
}
