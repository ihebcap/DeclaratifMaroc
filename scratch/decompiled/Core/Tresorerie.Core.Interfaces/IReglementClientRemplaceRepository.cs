using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IReglementClientRemplaceRepository
{
	IEnumerable<ReglementClient> GetAllByCaisseNonRemplace(int caisseNo, Etat? etat, Remis? remis, params int[] modesNo);

	IEnumerable<ReglementClient> GetAllRemplace(int societeNo, EtatComptabilite etat, int exercice, int caisseNo, int modeNo, DateTime? dateMin, DateTime? dateMax);

	IEnumerable<ReglementClient> GetAllRemplacer(int caisseNo, int tiersNo, params int[] modesNo);

	Task<IEnumerable<ReglementClient>> GetAllRemplaceAComptaAsync(DateTime dateMin, DateTime dateMax, EtatComptabilite etat, int[] caissesNo, int[] modesNo, int societeNo, CancellationToken cancellationToken);
}
