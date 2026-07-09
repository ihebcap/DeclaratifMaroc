using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class MouvementCaisseEspece
{
	public int? BanqueNo { get; set; }

	public int CaisseNo { get; set; }

	public int CaisseNoOut { get; set; }

	public DateTime Date { get; set; }

	public DateTime DateCreation { get; set; }

	public string Destination { get; set; }

	public MouvementDomaine Domaine { get; set; }

	public int? ModeNo { get; set; }

	public decimal Montant { get; set; }

	public int No { get; set; }

	public string Numero { get; set; }

	public string PieceNumero { get; set; }

	public SensMouvement Sens { get; set; }

	public int SocieteNo { get; set; }

	public string Source { get; set; }

	public int? TiersNo { get; set; }

	public string CaisseCode { get; set; }

	public string CaisseOutCode { get; set; }

	public string Nature { get; set; }

	public int Statut { get; set; }

	public int DeviseNo { get; set; }

	public string AffaireNumero { get; set; }
}
