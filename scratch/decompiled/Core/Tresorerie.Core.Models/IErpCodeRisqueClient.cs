using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public interface IErpCodeRisqueClient
{
	ErpTypeActionCodeRisque CodeRisque { get; }

	string Intitule { get; }

	decimal MontantMax { get; }

	decimal MontantMin { get; }

	int No { get; }
}
