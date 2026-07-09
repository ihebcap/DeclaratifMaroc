namespace Tresorerie.Core.Models;

public class ConfigConnectionErpExtern
{
	public int SocieteNo { get; set; }

	public string ServerName { get; set; }

	public string DatabaseName { get; set; }

	public string UserName { get; set; }

	public string Password { get; set; }

	public TypeAuthentification Type { get; set; }

	public string TableNameTiersVente { get; set; }

	public string TableNameDocumentVente { get; set; }

	public string TableNameTiersAchat { get; set; }

	public string TableNameDocumentAchat { get; set; }

	public string TableNameReglementAchat { get; set; }

	public string TableNameReglementVente { get; set; }
}
