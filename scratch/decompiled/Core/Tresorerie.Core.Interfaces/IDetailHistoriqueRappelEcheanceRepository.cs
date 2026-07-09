using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IDetailHistoriqueRappelEcheanceRepository
{
	IEnumerable<DetailHistoriqueRappelEcheance> GetAll(int societeNo, int echeanceNo);
}
