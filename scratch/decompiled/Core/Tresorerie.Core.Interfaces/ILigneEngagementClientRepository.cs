using System;
using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ILigneEngagementClientRepository
{
	IEnumerable<LigneEngagementClient> GetLigneEngagementClientComparedToEcheance(int societeNo, int clientNo, DateTime date);

	IEnumerable<LigneEngagementClient> GetLigneEngagementClientComparedToRapprochement(int societeNo, int clientNo, DateTime date);

	IEnumerable<LigneEngagementClient> GetLigneEngagementClientsComparedToEcheance(int societeNo, DateTime date);

	IEnumerable<LigneEngagementClient> GetLigneEngagementClientsComparedToRapprochement(int societeNo, DateTime date);
}
