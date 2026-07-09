using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public interface IErpComptaJournal
{
	bool AffecterStatut { get; }

	string Code { get; }

	string CompteGeneral { get; }

	ErpComptaContrePartie ContrePartie { get; }

	string Intitule { get; }

	bool IsSommeil { get; }

	int No { get; }

	ErpComptaTypeNumerotation Numerotation { get; }

	ErpComptaTypeRapprochement Rapprochement { get; }

	ErpComptaTypeJournal Type { get; }

	bool IsSaisieAnalytique { get; }
}
