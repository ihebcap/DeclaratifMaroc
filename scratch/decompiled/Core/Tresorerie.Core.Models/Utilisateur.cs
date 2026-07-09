using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class Utilisateur
{
	public string Email { get; set; }

	public bool IsActif { get; set; }

	public bool IsAdmin { get; set; }

	public string Login { get; set; }

	public int No { get; private set; }

	public string Nom { get; set; }

	public string Password { get; set; }

	public string Prenom { get; set; }

	public int? MenuGrcId { get; set; }

	public int? MenuGrfId { get; set; }

	public TypeModeAffichage TypeModeAffichageGrc { get; set; }

	public TypeModeAffichage TypeModeAffichageGrf { get; set; }

	public byte[] Hash { get; set; }

	public byte[] Salt { get; set; }

	public UserDefaultDashboard DefaultDashboard { get; set; }

	public Utilisateur()
	{
		Nom = string.Empty;
		Prenom = string.Empty;
		Login = string.Empty;
		Password = string.Empty;
		Email = string.Empty;
	}

	public Utilisateur(int no, string login, string password)
		: this()
	{
		No = no;
		Login = login;
		Password = password;
	}

	public override bool Equals(object obj)
	{
		if (!(obj is Utilisateur))
		{
			return false;
		}
		return (obj as Utilisateur).GetHashCode() == GetHashCode();
	}

	public override int GetHashCode()
	{
		return No.GetHashCode();
	}
}
