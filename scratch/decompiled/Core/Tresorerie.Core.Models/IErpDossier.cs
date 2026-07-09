using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public interface IErpDossier
{
	string FormatPrix { get; set; }

	string FormatQuantite { get; set; }

	int ConversionDevise { get; set; }

	int DeviseNo { get; set; }

	int DeviseNoCommercial { get; }

	ErpBaseEncours ErpBaseEncours { get; set; }

	ErpDeclenchementControlEncours ErpDeclenchementControlEncours { get; set; }

	string JournalSituationNumero { get; set; }

	string RaisonSocial { get; }

	string Identifiant { get; }

	string Activite { get; }

	decimal Capital { get; }

	string Telephone { get; }

	string Telecopie { get; }

	string Adresse { get; }

	string AdresseComplement { get; }

	string CodePostal { get; }

	string Ville { get; }

	string Site { get; }

	string Email { get; }
}
