using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class VirementInterne : ICanBeComptabilise
{
	public int CaisseNo { get; set; }

	public int? ChequeNo { get; set; }

	public int CompteInNo { get; set; }

	public int CompteOutNo { get; set; }

	public DateTime Date { get; set; }

	public DateTime DateCreation { get; set; }

	public DateTime DatePointage { get; set; }

	public decimal DeviseCours { get; set; }

	public int DeviseInNo { get; set; }

	public int DeviseOutNo { get; set; }

	public bool IsCheque { get; set; }

	public EtatComptabilite IsComptabilise { get; set; }

	public bool IsPointer { get; set; }

	public string Libelle { get; set; }

	public int ModeReglementNo { get; set; }

	public decimal Montant { get; set; }

	public decimal MontantDeviseSociete { get; set; }

	public int No { get; set; }

	public string Numero { get; set; }

	public string PieceNumero { get; set; }

	public int SocieteNo { get; set; }

	public int UtilisateurNo { get; set; }

	public string ExtraitNum { get; set; } = string.Empty;

	public void ChangeEtatComptabilise(EtatComptabilite etatComptabilite)
	{
		IsComptabilise = etatComptabilite;
	}
}
