using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class DesignationDocument
{
	public int No { get; set; }

	public string Code { get; set; }

	public string Intitule { get; set; }

	public DesignationDocumentNatureOperation NatureOperation { get; set; }

	public DesignationDocument()
	{
		NatureOperation = DesignationDocumentNatureOperation.AchatService;
	}
}
