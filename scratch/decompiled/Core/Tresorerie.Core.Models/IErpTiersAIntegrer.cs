using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public interface IErpTiersAIntegrer
{
	string Adresse { get; }

	decimal AssuranceCredit { get; }

	string CodePostal { get; }

	string ComplementAdresse { get; }

	string CompteGeneral { get; }

	int DeviseErpNo { get; }

	string Email { get; }

	decimal EncoursAutoriser { get; }

	string Identifiant { get; }

	string Intitule { get; }

	string Numero { get; }

	string Pays { get; }

	string Region { get; }

	string Site { get; }

	string Telecopie { get; }

	string Telephone { get; }

	string Ville { get; }

	TiersType Type { get; }
}
