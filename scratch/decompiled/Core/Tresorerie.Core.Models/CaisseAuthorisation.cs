namespace Tresorerie.Core.Models;

public class CaisseAuthorisation
{
	public int No { get; set; }

	public int SocieteNo { get; set; }

	public int CaisseNo { get; set; }

	public int CaisseAuthNo { get; set; }

	public string CaisseAuthCode { get; set; }

	public string CaisseAuthIntitule { get; set; }

	public bool EnSommeil { get; set; }
}
