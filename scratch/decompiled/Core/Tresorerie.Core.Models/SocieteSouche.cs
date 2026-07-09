using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class SocieteSouche
{
	public int Id { get; set; }

	public int SocieteNo { get; set; }

	public int SoucheNo { get; set; }

	public ErpDomaine Domaine { get; set; }
}
