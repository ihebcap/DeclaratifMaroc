using System;

namespace Tresorerie.Core.Models;

public interface IErpDetailAffectationReglementEcheance
{
	int No { get; }

	int ReglementNo { get; }

	int EcheanceNo { get; }

	decimal Montant { get; }

	int ReglementId { get; }

	decimal ReglementMontant { get; }

	decimal EcheanceMontant { get; }

	string ReglementClient { get; }

	string EcheanceNumero { get; }

	string ReglementLibelle { get; }

	DateTime ReglementDate { get; }

	string ReglementNumeroPiece { get; }
}
