using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IVirementInterneRepository
{
	void Comptabiliser(int virementNo);

	int Create(VirementInterne virement);

	void Update(VirementInterne virement);

	void Decomptabiliser(int virementNo);

	void Delete(int virementNo);

	IEnumerable<VirementInterne> GetAll(int societeNo);

	VirementInterne GetVirementInterne(int virementNo);

	VirementInterne GetVirementInterne(string numero, int societeNo);

	Task<IEnumerable<VirementInterne>> GetAllAComptaAsync(DateTime dateMin, DateTime dateMax, EtatComptabilite etat, int societeNo, CancellationToken cancellationToken);
}
