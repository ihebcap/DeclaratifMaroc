namespace Tresorerie.Core.Models;

public interface IErpCompteBanq
{
	string Abrege { get; }

	string Adresse { get; }

	string BanqueAbrege { get; }

	string BanqueIntitule { get; }

	string Cle { get; }

	string CodeJournalBanque { get; }

	string CodeJournalEncaissement { get; }

	string CodeJournalEscompte { get; }

	string CodePostal { get; }

	string Commentaire { get; }

	string Complement { get; }

	string Compte { get; }

	string CompteNumero { get; }

	int DeviseNo { get; }

	string Guichet { get; }

	int Id { get; set; }

	string Journal { get; }

	string NomAgence { get; }

	string Pays { get; }

	string Ville { get; }

	string Telephone { get; }

	string Telecopie { get; }

	string Contact { get; }

	string Iban { get; }

	string Bic { get; }
}
