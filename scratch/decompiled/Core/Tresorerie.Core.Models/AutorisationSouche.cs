using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class AutorisationSouche
{
	public int No { get; set; }

	public int SocieteNo { get; set; }

	public int SoucheNo { get; set; }

	public ErpDomaine Domaine { get; set; }

	public int UtilisateurNo { get; set; }
}
