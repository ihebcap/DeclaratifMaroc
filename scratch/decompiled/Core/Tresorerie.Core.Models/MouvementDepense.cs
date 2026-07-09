using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class MouvementDepense : ICanBeComptabilise
{
	public int CaisseNo { get; set; }

	public string Collaborateur { get; set; }

	public DateTime Date { get; set; }

	public int DeviseNo { get; set; }

	public decimal Cours { get; set; }

	public int ErpTaxeNo { get; set; }

	public EtatComptabilite IsComptabilise { get; set; }

	public string Libelle { get; set; }

	public int ModeNo { get; set; }

	public int ModificateurNo { get; set; }

	public decimal Montant { get; set; }

	public decimal MontantDeviseSociete { get; set; }

	public decimal MontantTimbre { get; set; }

	public decimal MontantTTC => Montant + MontantTva + MontantTimbre;

	public decimal MontantTva { get; set; }

	public int No { get; set; }

	public int NombreTimbre { get; set; }

	public string Numero { get; set; }

	public string PieceNumero { get; set; }

	public int SocieteNo { get; set; }

	public decimal TauxTva { get; set; }

	public int TypeDepenseNo { get; set; }

	public int UtilisateurNo { get; set; }

	public bool WithTva { get; set; }

	public string AffaireNumero { get; set; }

	public int? DeclarationTvaEncaissementNo { get; set; }

	public string RaisonSociale { get; set; }

	public string Ice { get; set; }

	public string Identifiant { get; set; }

	public int? BanqueNo { get; set; }

	public bool IsPointe { get; set; }

	public DateTime DatePointage { get; set; }

	public string ExtraitNum { get; set; }

	public void ChangeEtatComptabilise(EtatComptabilite etat)
	{
		IsComptabilise = etat;
	}
}
