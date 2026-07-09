using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ISocieteCodeActiviteTiersRepository
{
	List<SocieteCodeActiviteTiers> GetAll(int societeNo);

	SocieteCodeActiviteTiers Get(int no);

	void Create(SocieteCodeActiviteTiers societeCodeActiviteTiers);

	void Delete(SocieteCodeActiviteTiers societeCodeActiviteTiers);

	bool IsErpIntituleMapped(int societeNo, string erpIntitule);

	bool Exist(int societeNo, int codeActiviteTiersNo, string erpIntitule);
}
