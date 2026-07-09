using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ISocieteDeviseRepository
{
	void Create(SocieteDevise devise);

	void Delete(SocieteDevise devise);

	SocieteDevise Get(int societeNo, int deviseNo);

	IEnumerable<SocieteDevise> GetAll(int societeNo);

	SocieteDevise HasErpMapping(int deviseErpNo, int societeNo);

	void Update(SocieteDevise devise);
}
