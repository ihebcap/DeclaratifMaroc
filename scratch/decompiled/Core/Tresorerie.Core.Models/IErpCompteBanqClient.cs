namespace Tresorerie.Core.Models;

public interface IErpCompteBanqClient
{
	string Adresse { get; set; }

	string Banque { get; set; }

	int BanqueNo { get; }

	string Cle { get; set; }

	string CodePostal { get; set; }

	string Commentaire { get; set; }

	string Complement { get; set; }

	string Compte { get; set; }

	int DeviseNo { get; set; }

	string Guichet { get; set; }

	string Intitule { get; set; }

	int No { get; }

	string NomAgence { get; set; }

	string Numero { get; set; }

	string Pays { get; set; }

	string TiersNumero { get; set; }

	string Ville { get; set; }
}
