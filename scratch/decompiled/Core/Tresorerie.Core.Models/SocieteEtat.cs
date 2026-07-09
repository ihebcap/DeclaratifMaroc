using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class SocieteEtat
{
	public int BanqueNo { get; private set; }

	public string Chemin { get; set; }

	public int Id { get; private set; }

	public string Intitule { get; set; }

	public bool IsVisible { get; set; }

	public int SocieteNo { get; private set; }

	public int Source { get; private set; }

	public TypeRapport Type { get; private set; }

	public int? TypeBordNo { get; private set; }

	public SocieteEtat(int id, int societeNo, int source, int banqueNo, int? typeBordNo, TypeRapport type)
	{
		Id = id;
		Type = type;
		TypeBordNo = typeBordNo;
		BanqueNo = banqueNo;
		Source = source;
		SocieteNo = societeNo;
		Chemin = string.Empty;
	}

	public SocieteEtat(int societeNo, int source, int banqueNo, int? typeBordNo, TypeRapport type)
	{
		Type = type;
		TypeBordNo = typeBordNo;
		BanqueNo = banqueNo;
		Source = source;
		SocieteNo = societeNo;
		Chemin = string.Empty;
	}
}
