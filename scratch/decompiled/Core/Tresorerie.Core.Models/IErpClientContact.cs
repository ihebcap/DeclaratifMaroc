namespace Tresorerie.Core.Models;

public interface IErpClientContact
{
	string Civilite { get; set; }

	string EMail { get; set; }

	string Fonction { get; set; }

	string Intitule { get; set; }

	int No { get; set; }

	string Nom { get; set; }

	string Num { get; set; }

	string Prenom { get; set; }

	string Service { get; set; }

	string Telecopie { get; set; }

	string Telephone { get; set; }

	string TelPortable { get; set; }

	int ServiceNo { get; set; }
}
