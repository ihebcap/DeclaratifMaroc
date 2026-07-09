using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ITiersServiceContactRepository
{
	List<TiersServiceContact> GetAll(int SocieteNo);

	TiersServiceContact GetByErpNo(int societeNo, int no);

	int Create(TiersServiceContact tiersServiceContact);

	void UpdateNotify(int no, bool isNotify);
}
