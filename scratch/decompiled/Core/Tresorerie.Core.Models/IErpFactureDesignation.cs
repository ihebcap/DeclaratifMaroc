using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public interface IErpFactureDesignation
{
	ErpDocumentType DocumentType { get; }

	string DocumentNumero { get; }

	string DocumentDesignation { get; }

	string DocumentNatureMarchandise { get; }

	string DocumentDateLivraisonMarchandise { get; }
}
