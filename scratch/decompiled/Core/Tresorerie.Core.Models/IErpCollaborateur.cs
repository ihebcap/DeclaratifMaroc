namespace Tresorerie.Core.Models;

public interface IErpCollaborateur
{
	string Adresse { get; }

	string Fonction { get; }

	int Id { get; }

	bool IsAcheteur { get; }

	bool IsCaissier { get; }

	bool IsChargeRecouvr { get; }

	bool IsFinancier { get; }

	bool IsReceptionnaire { get; }

	bool IsVendeur { get; }

	string Nom { get; }

	string Prenom { get; }

	string Service { get; }

	string Ville { get; }

	string Email { get; }
}
