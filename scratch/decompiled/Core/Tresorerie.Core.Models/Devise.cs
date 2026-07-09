using System.Linq;
using Tresorerie.Ressources;

namespace Tresorerie.Core.Models;

public class Devise
{
	public string Code { get; set; }

	public decimal Cours { get; set; }

	public string Format { get; set; }

	public string Intitule { get; set; }

	public decimal Libor { get; set; }

	public string Monnaie { get; set; }

	public int No { get; private set; }

	public int NombreDecimales => GetNombreDecimales(Format);

	public string Sigle { get; set; }

	public string SousMonnaie { get; set; }

	public Devise()
	{
		Code = string.Empty;
		Intitule = string.Empty;
		Format = string.Empty;
		Monnaie = string.Empty;
		SousMonnaie = string.Empty;
		Sigle = string.Empty;
		Cours = 1m;
	}

	public Devise(int no, string code, string intitule)
		: this()
	{
		No = no;
		Code = code;
		Intitule = intitule;
	}

	public override string ToString()
	{
		return string.Format(TresorerieCoreMessages.InfoDevise, No, Intitule);
	}

	private static int GetNombreDecimales(string format)
	{
		char c = (format.Contains('.') ? '.' : ',');
		if (!format.Contains(c))
		{
			return 0;
		}
		return format.Split(c)[1]?.Count((char x) => x.Equals('0')) ?? 0;
	}
}
