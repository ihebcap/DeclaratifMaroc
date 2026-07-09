using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class LigneDossierEcheanceTvaNonRecuperable
{
	public int No { get; set; }

	public int SocieteNo { get; set; }

	public int RetenueNo { get; set; }

	public int EcheanceNo { get; set; }

	public string ErpDocumentNumero { get; set; }

	public ErpDocumentType ErpDocumentType { get; set; }

	public decimal BaseTva { get; set; }

	public string CodeTaxe { get; set; }

	public decimal TauxTaxe { get; set; }

	public decimal MontantTva { get; set; }
}
