using System.ComponentModel;

namespace Tresorerie.Core.Enum;

public enum SensPrevisionnelle : short
{
	[Description("Encaissement")]
	Encaissement,
	[Description("Décaissement")]
	Decaissement,
	[Description("Solde initial")]
	SoldeInitial,
	[Description("Solde bancaire")]
	SoldeBancaire
}
