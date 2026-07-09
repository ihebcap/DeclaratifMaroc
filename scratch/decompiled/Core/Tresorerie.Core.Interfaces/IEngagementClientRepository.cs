using System;
using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IEngagementClientRepository
{
	IEnumerable<EngagementClient> GetEngagementAllClientsComparedToEcheance(int societeNo, DateTime date);

	IEnumerable<EngagementClient> GetEngagementAllClientsComparedToRapprochement(int societeNo, DateTime date);

	EngagementClient GetEngagementClientComparedToEcheance(int clientNo, int societeNo, DateTime date);

	EngagementClient GetEngagementClientComparedToRapprochement(int clientNo, int societeNo, DateTime date);
}
