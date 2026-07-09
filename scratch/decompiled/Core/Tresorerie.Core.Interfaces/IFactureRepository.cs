using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IFactureRepository
{
	Task<Facture> GetAsync(int factureNo);

	Facture Get(int factureNo);

	Task<IEnumerable<Facture>> GetAllAComptaAsync(DateTime dateMin, DateTime dateMax, bool isComptabilise, int societeNo, CancellationToken cancellationToken);
}
