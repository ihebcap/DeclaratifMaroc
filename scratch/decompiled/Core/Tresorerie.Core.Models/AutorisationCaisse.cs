using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class AutorisationCaisse
{
	public int CaisseNo { get; set; }

	public bool IsDefaultCaisse { get; set; }

	public int No { get; set; }

	public ProfilType Type { get; set; }

	public int UtilisateurNo { get; set; }
}
