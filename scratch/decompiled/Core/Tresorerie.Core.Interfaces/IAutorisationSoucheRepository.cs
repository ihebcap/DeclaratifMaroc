using System.Collections.Generic;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IAutorisationSoucheRepository
{
	void Create(ErpDomaine domaine, int utilisateurNo, int soucheNo, int societeNo);

	void Delete(int autorisationNo);

	AutorisationSouche Get(ErpDomaine domaine, int utilisateurNo, int soucheNo, int societeNo);

	IEnumerable<AutorisationSouche> GetAll(ErpDomaine domaine, int utilisateurNo, int societeNo);
}
