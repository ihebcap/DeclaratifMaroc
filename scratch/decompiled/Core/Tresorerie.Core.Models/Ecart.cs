using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class Ecart : ICanBeComptabilise
{
	public int ClientNo { get; set; }

	public string Commentaire { get; set; }

	public decimal CoursDevise { get; set; }

	public DateTime Date { get; set; }

	public int DeviseNo { get; set; }

	public DateTime DocumentDate { get; set; }

	public string DocumentNumero { get; set; }

	public ErpDocumentType DocumentType { get; set; }

	public ErpDomaine Domaine { get; set; }

	public int? EcheanceEcartNo { get; set; }

	public int ErpNo { get; set; }

	public Etat Etat
	{
		get
		{
			if (!(Solde == 0m))
			{
				return Etat.NonPaye;
			}
			return Etat.TotalementPaye;
		}
	}

	public EtatComptabilite IsComptabilise { get; set; }

	public int ModeReglementNo { get; set; }

	public decimal Montant { get; set; }

	public int No { get; set; }

	public int PayeurNo { get; set; }

	public int SocieteNo { get; set; }

	public decimal Solde { get; set; }

	public int SoucheNo { get; set; }

	public EcheanceType Type { get; set; }

	public int UtilisateurNo { get; set; }
}
