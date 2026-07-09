namespace Tresorerie.Core.Models;

public class SocieteDesignationDocument
{
	public int No { get; set; }

	public int SocieteNo { get; set; }

	public int DesignationDocumentNo { get; set; }

	public string ErpIntitule { get; set; }

	public string ErpNatureMarchandise { get; set; }

	public string ErpDateLivraisonMarchandise { get; set; }
}
