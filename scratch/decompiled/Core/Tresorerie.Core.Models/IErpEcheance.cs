using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public interface IErpEcheance
{
	string CodeAffaire { get; }

	int CollaborateurNo { get; }

	DateTime Date { get; }

	string DeviseCode { get; }

	decimal DeviseCours { get; }

	int DeviseNo { get; }

	DateTime DocumentDate { get; }

	int DocumentNo { get; }

	ErpDocumentType DocumentType { get; }

	ErpDomaine Domaine { get; }

	bool Flag { get; }

	int ModeNo { get; }

	decimal Montant { get; }

	decimal MontantDevise { get; }

	int No { get; }

	string Piece { get; }

	string Reference { get; }

	decimal Solde { get; }

	int SoucheNo { get; }

	string TiersNumero { get; }

	string TiersPayeurNumero { get; }

	EcheanceType Type { get; }
}
