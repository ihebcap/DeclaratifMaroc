using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IChequierRepository
{
	int Create(Chequier chequier);

	void Delete(Chequier chequier);

	Chequier Get(int no);

	IEnumerable<Chequier> GetAll(int societeNo);

	IEnumerable<Chequier> GetAllBanqueActif(int societeNo);

	IEnumerable<Chequier> GetAllByBanque(int banqueNo, int societeNo);

	IEnumerable<Chequier> GetAllByBanqueActif(int banqueNo, int societeNo);

	void Update(Chequier chequier);
}
