using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class RemboursementFournisseur : ICanBeComptabilise
{
	public int CaisseNo { get; set; }

	public int FournisseurNo { get; set; }

	public string FournisseurNumero { get; set; }

	public string FournisseurIntitule { get; set; }

	public EtatComptabilite Comptabilise { get; set; }

	public DateTime Date { get; set; }

	public DateTime DateCreation { get; set; }

	public DateTime DateModification { get; set; }

	public decimal Cours { get; set; }

	public int DeviseNo { get; set; }

	public DateTime Echeance { get; set; }

	public Etat Etat { get; set; }

	public string Libelle { get; set; }

	public int ModeReglementNo { get; set; }

	public int ModificateurNo { get; set; }

	public decimal Montant { get; set; }

	public decimal MontantDeviseSociete { get; set; }

	public int No { get; set; }

	public string Numero { get; set; }

	public string Reference { get; set; }

	public int ReglementNo { get; set; }

	public string ReglementNumero { get; set; }

	public int SocieteNo { get; set; }

	public decimal Solde { get; set; }

	public int UtilisateurNo { get; set; }
}
