using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public interface IErpTiersIce
{
	TiersType TiersType { get; }

	string TiersCode { get; }

	string TiersIce { get; }

	string TiersIdentifiant { get; }

	string NatureValue { get; }

	DateTime? TiersDateNaissance { get; }

	string TiersActivite { get; }

	string TiersEmail { get; }

	string TiersCodeActiviteMarroc { get; }

	string TiersNumRegistreCommerceMarroc { get; }
}
