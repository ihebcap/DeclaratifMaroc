using System;

namespace Tresorerie.Core.Models;

public class MouvementEscompte
{
	public int BanqueNo { get; set; }

	public int BordereauNature { get; set; }

	public int BordereauNo { get; set; }

	public string BordereauNumero { get; set; }

	public int CaisseInNo { get; set; }

	public int CaisseOutNo { get; set; }

	public int ChequeNo { get; set; }

	public int ClientNo { get; set; }

	public string CodeMode { get; set; }

	public decimal CoursDevise { get; set; }

	public DateTime Date { get; set; }

	public DateTime DateImpaye { get; set; }

	public DateTime DatePointage { get; set; }

	public DateTime DateRemis { get; set; }

	public int DeviseNo { get; set; }

	public DateTime Echeance { get; set; }

	public int ErpNo { get; set; }

	public int IsComptabilise { get; set; }

	public int IsImpaye { get; set; }

	public int IsPointe { get; set; }

	public int IsRemis { get; set; }

	public int IsRemplace { get; set; }

	public string Libelle { get; set; }

	public int ModeNo { get; set; }

	public decimal Montant { get; set; }

	public decimal MontantDevise { get; set; }

	public int No { get; set; }

	public string Numero { get; set; }

	public string Piece { get; set; }

	public string PieceBanque { get; set; }

	public int SocieteNo { get; set; }

	public decimal Solde { get; set; }

	public decimal SoldeRemplace { get; set; }

	public string TireIntitule { get; set; }

	public int UtilisateurNo { get; set; }
}
