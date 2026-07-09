using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public interface IErpReglementTiers
{
	int No { get; }

	string TierNum { get; }

	ErpReglementRepartitionType ReparationType { get; }

	decimal Value { get; }

	int NbJour { get; }

	ReglementConditionType ConditionType { get; }

	int ModeNo { get; }

	int Jour1 { get; }

	int Jour2 { get; }

	int Jour3 { get; }

	int Jour4 { get; }

	int Jour5 { get; }

	int Jour6 { get; }
}
