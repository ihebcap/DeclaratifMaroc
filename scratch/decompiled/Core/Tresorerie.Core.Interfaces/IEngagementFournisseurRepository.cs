using System;
using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IEngagementFournisseurRepository
{
	IEnumerable<EngagementFournisseur> GetEngagementAllFournisseurComparedToEcheance(int societeNo, DateTime date);

	EngagementFournisseur GetEngagementFournisseurComparedToEcheance(int fournisseurNo, int societeNo, DateTime date);
}
