using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public interface IErpBonLivraison
{
	string ClientNumero { get; }

	decimal Cours { get; }

	DateTime Date { get; }

	int DeviseNo { get; }

	decimal Montant { get; }

	int No { get; }

	string Numero { get; }

	string PayeurNumero { get; }

	ErpDocumentType Type { get; }
}
