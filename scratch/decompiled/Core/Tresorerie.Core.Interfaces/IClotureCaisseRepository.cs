using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IClotureCaisseRepository
{
	IList<ClotureCaisse> GetAll(int societeNo, int utilisateurNo);

	IList<ClotureCaisse> GetAll(int societeNo);

	IList<ClotureCaisse> GetAll(int[] caissesNo);

	ClotureCaisse Get(int no);

	int Add(ClotureCaisse cloture);

	void Delete(ClotureCaisse cloture);

	void UpdateNbEspece(ClotureCaisse clotureCaisse);
}
