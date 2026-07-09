using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class Transfert : ICanBeComptabilise, ITresorerieMouvement, ITresorerieEntity
{
	public int CaisseDestinataireNo { get; set; }

	public int CaisseNo { get; set; }

	public int CreateurNo { get; set; }

	public DateTime Date { get; set; }

	public DateTime DateCreation { get; set; }

	public DateTime DateModification { get; set; }

	public int DeviseNo { get; set; }

	public MouvementDomaine Domaine => MouvementDomaine.Transfert;

	public EtatComptabilite IsComptabilise { get; set; }

	public int ModeNo { get; set; }

	public int ModificateurNo { get; set; }

	public decimal Montant { get; set; }

	public int No { get; set; }

	public string Numero { get; set; }

	public string ClotureNumero { get; set; }

	public int SocieteNo { get; set; }

	public StatutTransfert Statut { get; set; }

	public ReglementType Type { get; set; }
}
