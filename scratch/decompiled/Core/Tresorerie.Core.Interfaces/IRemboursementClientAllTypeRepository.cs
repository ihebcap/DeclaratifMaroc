using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IRemboursementClientAllTypeRepository
{
	RemboursementClientAllType Get(int no);

	IEnumerable<RemboursementClientAllType> GetAll(int societeNo);

	IEnumerable<RemboursementClientAllType> GetAll(int societeNo, DateTime dateDebut, DateTime dateFin);

	void UpdateEtatComptabilisation(int mvtNo, EtatComptabilite etatComptabilite);

	Task<IEnumerable<RemboursementClientAllType>> GetAllAComptaAsync(DateTime dateMin, DateTime dateMax, EtatComptabilite comptabilite, int[] caissesNo, EcheanceType[] types, int societeNo, CancellationToken cancellationToken);
}
