using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IAlimentationCaisseRepository
{
	int Create(AlimentationCaisse alC);

	void Delete(AlimentationCaisse alimentation);

	bool ExistAlimentationCaisseEnAttente(int societeNo);

	AlimentationCaisse Get(int no);

	AlimentationCaisse Get(int societeNo, string numero);

	IEnumerable<AlimentationCaisse> GetAll(int societeNo);

	IEnumerable<AlimentationCaisse> GetAll(ErpComptaPeriode periode, int caisseNo, DateTime minDate, DateTime maxDate, EtatComptabilite etat, int p);

	void Update(AlimentationCaisse alimentation);

	void UpdateStatutAlimentation(AlimentationCaisse alimentation);

	Task<IEnumerable<AlimentationCaisse>> GetAllAComptaAsync(DateTime dateMin, DateTime dateMax, EtatComptabilite etat, int[] caissesNo, int[] modesNo, int societeNo, CancellationToken cancellationToken);
}
