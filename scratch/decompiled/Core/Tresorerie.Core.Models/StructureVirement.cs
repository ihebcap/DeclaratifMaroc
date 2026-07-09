using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class StructureVirement
{
	public int No { get; set; }

	public int SocieteNo { get; set; }

	public int BanqueNo { get; set; }

	public EmplacementStructureVirement Emplacement { get; set; }

	public ChampStructureVirement Champ { get; set; }

	public int Position { get; set; }

	public TypeChamp Type { get; set; }

	public int Taille { get; set; }

	public string BaliseDebut { get; set; }

	public string BaliseFin { get; set; }

	public TypeFichierBordoreau TypeFichier { get; set; }
}
