using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class LigneVirementTiers
{
	public string BanqueClient { get; set; }

	public int BanqueNo { get; set; }

	public int CaisseNo { get; set; }

	public DateTime Date { get; set; }

	public Etat Etat { get; set; }

	public EtatComptabilite IsEtatComptabilise { get; set; }

	public string Libelle { get; set; }

	public int ModeNo { get; set; }

	public decimal Montant { get; set; }

	public int No { get; set; }

	public string Numero { get; set; }

	public string Reference { get; set; }

	public string Rib { get; set; }

	public int SocieteNo { get; set; }

	public decimal Solde { get; set; }

	public int SoucheNo { get; set; }

	public int TiersNo { get; set; }

	public int VirementNo { get; set; }

	public string VirementNumero { get; set; }
}
