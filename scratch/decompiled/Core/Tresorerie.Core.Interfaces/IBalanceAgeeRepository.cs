using System;
using System.Collections.Generic;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IBalanceAgeeRepository
{
	IEnumerable<BalanceAgee> Get(int societeNo, ErpDomaine domaine, DateTime? dateDebut);

	IEnumerable<BalanceAgee> Get(int societeNo, ErpDomaine domaine, DateTime? dateDebut, int[] souchesNo);

	IEnumerable<BalanceAgee> Get(int societeNo, ErpDomaine domaine, int tiersNo);
}
