using System;
using System.Collections.Generic;

namespace Tresorerie.Core.Models;

public interface IEngagementClientDetailsRepository
{
	IEnumerable<EngagementClientDetails> GetEngagementComparedToEcheance(int societeNo, DateTime date);

	IEnumerable<EngagementClientDetails> GetEngagementComparedToRapprochement(int societeNo, DateTime date);
}
