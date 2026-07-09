using System;
using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IMouvementEscompteRepository
{
	MouvementEscompte Get(int no);

	IEnumerable<MouvementEscompte> GetAllByBanqueAndDate(int banqueNo, DateTime date, int nbreJourCouverture, int societeNo);
}
