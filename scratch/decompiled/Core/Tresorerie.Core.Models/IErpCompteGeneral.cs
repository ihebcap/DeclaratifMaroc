namespace Tresorerie.Core.Models;

public interface IErpCompteGeneral
{
	string Intitule { get; }

	int No { get; }

	string Numero { get; }

	string Classement { get; }

	bool IsAnalytique { get; }

	ErpTypePlanComptable Type { get; }

	ErpNaturePlanComptable Nature { get; }
}
