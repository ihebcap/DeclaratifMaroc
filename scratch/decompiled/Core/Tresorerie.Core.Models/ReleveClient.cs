using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class ReleveClient
{
	public int No { get; set; }

	public string Numero { get; set; }

	public int TiersNo { get; set; }

	public DateTime Date { get; set; }

	public DateTime Echeance { get; set; }

	public decimal Montant { get; set; }

	public decimal Solde { get; set; }

	public decimal MontantDevise { get; set; }

	public decimal Cours { get; set; }

	public int DeviseNo { get; set; }

	public bool Remplace { get; set; }

	public string Piece { get; set; }

	public EtatMouvement Etat { get; set; }

	public decimal Debit { get; set; }

	public decimal Credit { get; set; }

	public decimal DebitDevise { get; set; }

	public decimal CreditDevise { get; set; }

	public TypeEntiteReleve TypeEntitee { get; set; }

	public string Libelle { get; set; }

	public int ModeNo { get; set; }

	public string ModeCode { get; set; }

	public ReglementType ModeTypeNo { get; set; }

	public string Reference { get; set; }

	public bool Annule { get; set; }

	public decimal SoldeProgressive { get; set; }

	public int TRI { get; set; }

	public int ROW_NO { get; set; }

	public string Tag { get; set; }

	public NatureEntite NatureEntite { get; set; }
}
