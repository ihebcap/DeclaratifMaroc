using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class CommissionImpaye : ICanBeComptabilise
{
	public int No { get; set; }

	public int TiersNo { get; set; }

	public string TiersCode { get; set; }

	public string TiersIntitule { get; set; }

	public string Commentaire { get; set; }

	public DateTime DocumentDate { get; set; }

	public string DocumentNumero { get; set; }

	public ErpDomaine Domaine { get; set; }

	public Etat Etat { get; set; }

	public bool IsComptabilise { get; set; }

	public decimal Montant { get; set; }

	public decimal Solde { get; set; }

	public int SoucheNo { get; set; }

	public int SocieteNo { get; set; }

	public int UtilisateurNo { get; set; }

	public int ModeNo { get; set; }
}
