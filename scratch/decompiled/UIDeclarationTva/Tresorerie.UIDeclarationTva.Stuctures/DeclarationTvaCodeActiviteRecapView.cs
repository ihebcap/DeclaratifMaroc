using Tresorerie.Core.Enum;

namespace Tresorerie.UIDeclarationTva.Stuctures;

public class DeclarationTvaCodeActiviteRecapView
{
	public int SocieteNo { get; set; }

	public LigneDeclarationTvaEncaissementDomaine Domaine { get; set; }

	public string CodeActivite { get; set; }

	public string ErpTaxeCode { get; set; }

	public decimal Taux { get; set; }

	public decimal TotalAssiette { get; set; }

	public decimal TotalTva { get; set; }
}
