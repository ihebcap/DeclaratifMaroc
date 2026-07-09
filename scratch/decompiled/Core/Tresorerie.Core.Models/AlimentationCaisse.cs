using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class AlimentationCaisse : ICanBeComptabilise
{
	public int BanqueNo { get; set; }

	public int CaisseNo { get; set; }

	public int? ChequeNo { get; set; }

	public DateTime Date { get; set; }

	public DateTime DateRapprochement { get; set; }

	public int DeviseNo { get; set; }

	public EtatComptabilite IsComptabilise { get; set; }

	public bool IsRapproche { get; set; }

	public string Libelle { get; set; } = string.Empty;

	public int ModeNo { get; set; }

	public int ModificateurNo { get; set; }

	public decimal Montant { get; set; }

	public decimal Cours { get; set; }

	public decimal MontantDeviseSociete { get; set; }

	public int No { get; set; }

	public string Numero { get; set; }

	public string PieceNumero { get; set; }

	public int SocieteNo { get; set; }

	public string Beneficiaire { get; set; }

	public StatutAlimentationCaisse StatutAlimentation { get; set; }

	public string ExtraitNum { get; set; } = string.Empty;

	public int UtilisateurNo { get; set; }

	internal void ChangeEtatComptabilise(EtatComptabilite etat)
	{
		IsComptabilise = etat;
	}
}
