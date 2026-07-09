using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class ChequeEntity
{
	public int ChequeNo { get; set; }

	public int EntityNo { get; set; }

	public string EntityNumero { get; set; }

	public int SocieteNo { get; set; }

	public ChequeEntityDomaine Domaine { get; set; }
}
