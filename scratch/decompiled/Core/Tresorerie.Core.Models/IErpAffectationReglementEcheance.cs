using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public interface IErpAffectationReglementEcheance
{
	int No { get; }

	int ReglementNo { get; }

	int EcheanceNo { get; }

	decimal Montant { get; }

	ErpDomaine Domaine { get; }

	ErpDocumentType DocumentType { get; }

	string DocumentNumero { get; }
}
