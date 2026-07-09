using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class ImpayeFournisseur : ICanBeComptabilise
{
	public string BanqueFournisseur { get; set; }

	public int? BanqueNo { get; set; }

	public string Beneficiaire { get; set; }

	public int CaisseNo { get; set; }

	public int EcheanceNo { get; set; }

	public DateTime Date { get; set; }

	public DateTime DateImpaye { get; set; }

	public DateTime DateRecuperation { get; set; }

	public int DossierNo { get; set; }

	public string DossierNumero { get; set; }

	public DateTime Echeance { get; set; }

	public string FournisseurCode { get; set; }

	public string FournisseurIntitule { get; set; }

	public int FournisseurNo { get; set; }

	public bool IsComptaImpaye { get; private set; }

	public EtatComptabilite IsComptaReglement { get; set; }

	public bool IsRecuperer { get; set; }

	public string Libelle { get; set; }

	public int ModeNo { get; set; }

	public decimal Montant { get; set; }

	public int No { get; set; }

	public string Numero { get; set; }

	public string Piece { get; set; }

	public int SocieteNo { get; set; }

	public ReglementType TypeMode { get; set; }

	public int RepresentantNo { get; set; }

	public int SoucheNo { get; set; }

	public string Commentaire { get; set; }

	public int DeviseNo { get; set; }

	public decimal Cours { get; set; }

	public decimal MontantDeviseSociete { get; set; }

	public decimal SoldeImpaye { get; set; }

	public bool IsReserve { get; set; }

	public void ChangeEtatComptabilise(bool etat)
	{
		IsComptaImpaye = etat;
	}
}
