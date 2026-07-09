using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IImpayeRepository
{
	Impaye Get(int reglementNo);

	IEnumerable<Impaye> GetAll(int societeNo);

	Task<IEnumerable<Impaye>> GetAllAsync(int tiersNo, int societeNo);

	IEnumerable<Impaye> GetAll(int societeNo, params int[] souchesNo);

	Task<IEnumerable<Impaye>> GetAllAsync(int tiersNo, int societeNo, params int[] souchesNo);

	void Update(Impaye impaye);

	Task<IEnumerable<Impaye>> GetAllAComptaAsync(DateTime dateMin, DateTime dateMax, EtatComptabilite etat, int[] souchesNo, int societeNo, CancellationToken cancellationToken);
}
