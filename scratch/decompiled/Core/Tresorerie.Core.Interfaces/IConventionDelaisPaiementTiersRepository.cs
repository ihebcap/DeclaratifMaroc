using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IConventionDelaisPaiementTiersRepository
{
	ConventionDelaisPaiementTiers Get(int no);

	ConventionDelaisPaiementTiers Get(int societeNo, int tiersNo, string numero, DomaineConvention domaine);

	IEnumerable<ConventionDelaisPaiementTiers> GetAll(int societeNo, DomaineConvention domaine);

	IEnumerable<ConventionDelaisPaiementTiers> GetAll(int societeNo, int tiersNo, DomaineConvention domaine);

	int Create(ConventionDelaisPaiementTiers convention);

	void Update(ConventionDelaisPaiementTiers convention);

	void Delete(ConventionDelaisPaiementTiers convention);
}
