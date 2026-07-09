using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class TypeOperationBancaire
{
	public string Code { get; set; }

	public string CompteGeneral { get; set; }

	public string Intitule { get; set; }

	public int No { get; set; }

	public SensPrevisionnelle Sens { get; set; }

	public int SocieteNo { get; set; }

	public int ErpTaxeNo { get; set; }

	public string CodeInterBanque { get; set; }
}
