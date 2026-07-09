using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class BalanceAgee
{
	public int No { get; set; }

	public int ErpNo { get; set; }

	public int ModeReglementNo { get; set; }

	public string Commentaire { get; set; }

	public decimal Montant { get; set; }

	public decimal Solde { get; set; }

	public int DeviseNo { get; set; }

	public decimal DeviseCours { get; set; }

	public decimal MontantDevise { get; set; }

	public int CollaborateurNo { get; set; }

	public int SoucheNo { get; set; }

	public string CodeAffaire { get; set; }

	public int TiersNo { get; set; }

	public DateTime DateDocument { get; set; }

	public DateTime DateEcheance { get; set; }

	public EcheanceType EcheanceType { get; set; }

	public ErpDomaine Domaine { get; set; }

	public int SocieteNo { get; set; }

	public decimal SoldeDevise { get; set; }

	public string DocumentNumero { get; set; }

	public static object ModeReglementType_ { get; set; }

	public string PayeurNumero { get; set; }

	public string Reference { get; set; }
}
