using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ICaisseAuthorisation
{
	CaisseAuthorisation Get(int no);

	CaisseAuthorisation Get(int caisseNo, int caisseAutNo);

	IEnumerable<CaisseAuthorisation> GetAll(int caisseNo);

	int? Create(CaisseAuthorisation caisse);

	void Delete(CaisseAuthorisation caisse);
}
