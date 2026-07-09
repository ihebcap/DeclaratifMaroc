using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IRemplacementRepository
{
	int? Create(Remplacement remplacement);

	void Delete(Remplacement remplacement);

	IEnumerable<Remplacement> GetLesRemplacent(int idRemplacer);

	IEnumerable<Remplacement> GetRemplacer(int idRemplacement);

	void Update(Remplacement remplacement);
}
