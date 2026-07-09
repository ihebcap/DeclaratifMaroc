using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public interface IErpFamilleArticle
{
	int No { get; }

	string CodeFamille { get; }

	string Intitule { get; }

	int UniteVente { get; }

	bool HorsStat { get; }

	TypeFamille Type { get; }

	string FamilleCentralisatrice { get; }
}
