using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class EcheanceFacture
{
	public int No { get; set; }

	public ErpDomaine Domaine { get; set; }

	public ErpDocumentType DocumentType { get; set; }

	public DateTime Date { get; set; }

	public decimal Montant { get; set; }

	public int DeviseNo { get; set; }

	public string DeviseCode { get; set; }

	public decimal MontantDevise { get; set; }

	public int ModeNo { get; set; }

	public EcheanceType Type { get; set; }

	public int DocumentNo { get; set; }

	public string DocumentNum { get; set; }
}
