using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class RemboursementClient : ICanBeComptabilise
{
	public int CaisseNo { get; set; }

	public int ClientNo { get; set; }

	public EtatComptabilite Comptabilise { get; set; }

	public decimal Cours { get; set; }

	public DateTime Date { get; set; }

	public DateTime DateCreation { get; set; }

	public DateTime DateModification { get; set; }

	public int DeviseNo { get; set; }

	public MouvementDomaine Domaine => MouvementDomaine.RemboursementClient;

	public bool IsComptabilise => Comptabilise == EtatComptabilite.Comptabilise;

	public string Libelle { get; set; }

	public int ModeReglementNo { get; set; }

	public int ModificateurNo { get; set; }

	public decimal Montant { get; set; }

	public decimal MontantDeviseSociete { get; set; }

	public int No { get; set; }

	public string Numero { get; set; }

	public int SocieteNo { get; set; }

	public int UtilisateurNo { get; set; }
}
