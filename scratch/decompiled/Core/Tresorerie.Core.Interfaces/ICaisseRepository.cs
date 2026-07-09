using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ICaisseRepository
{
	int? Create(Caisse caisse);

	void Delete(Caisse caisse);

	Caisse Get(int caisseNo);

	IEnumerable<Caisse> GetAll(int societeNo);

	bool IsCaisseUsed(int caisseNo);

	void Update(Caisse caisse);
}
