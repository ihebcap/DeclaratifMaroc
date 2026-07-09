using System.ComponentModel.DataAnnotations;

namespace Tresorerie.Core.Models;

public class BanqueTiers
{
	[Key]
	public int No { get; set; }

	public string TiersNum { get; set; } = string.Empty;

	public string Banque { get; set; } = string.Empty;

	[Display(Description = "Agence")]
	public string NomAgence { get; set; } = string.Empty;

	[Display(Description = "Rib")]
	public string RIB { get; set; } = string.Empty;

	public string Adresse { get; set; } = string.Empty;

	public string Pays { get; set; } = string.Empty;

	public int PaysNo { get; set; }

	public string Ville { get; set; } = string.Empty;

	public string CodePostal { get; set; } = string.Empty;

	public string Telephone { get; set; } = string.Empty;

	public string Telecopie { get; set; } = string.Empty;

	public string Contact { get; set; } = string.Empty;

	public bool IsPrincipal { get; set; }

	public int SocieteNo { get; set; }

	public string IBAN { get; set; } = string.Empty;

	public string BIC { get; set; } = string.Empty;

	public override int GetHashCode()
	{
		return new { No }.GetHashCode();
	}

	public override bool Equals(object obj)
	{
		if (obj == null)
		{
			return false;
		}
		if (!(obj is BanqueTiers banqueTiers))
		{
			return false;
		}
		return banqueTiers.GetHashCode() == GetHashCode();
	}
}
