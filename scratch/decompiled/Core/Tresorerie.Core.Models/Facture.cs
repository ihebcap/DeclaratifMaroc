using System;
using System.Collections.Generic;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class Facture : ICanBeComptabilise
{
	public int No { get; set; }

	public string DocumentNumero { get; set; }

	public DateTime DocumentDate { get; set; }

	public string Reference { get; set; }

	public string Commentaire { get; set; }

	public DateTime Echeance { get; set; }

	public string TiersCode { get; set; }

	public int CollaborateurNo { get; set; }

	public int DeviseNo { get; set; }

	public decimal CoursDevise { get; set; }

	public int SoucheNo { get; set; }

	public int ModeReglementNo { get; set; }

	public decimal MontantHTDevise { get; set; }

	public decimal MontantTTCDevise { get; set; }

	public decimal MontantTTCDeviseSociete { get; set; }

	public decimal Solde { get; set; }

	public decimal SoldeDeviseSociete { get; set; }

	public decimal Timbre { get; set; }

	public bool IsTimbre { get; set; }

	public bool IsComptabilise { get; set; }

	public bool IsReserveDossierFrs { get; set; }

	public IList<LigneFacture> Lignes { get; set; } = new List<LigneFacture>();

	public ErpDomaine Domaine { get; set; }

	public ErpDocumentType DocumentType { get; set; }

	public List<EcheanceFacture> Echeances { get; set; } = new List<EcheanceFacture>();

	public bool IsPartiellementPayee
	{
		get
		{
			if (MontantTTCDevise != 0m)
			{
				if (!(Solde == 0m))
				{
					return Solde < MontantTTCDevise;
				}
				return true;
			}
			return false;
		}
	}

	public bool IsAvoir { get; set; }

	public int SocieteNo { get; set; }

	public int Statut { get; set; }

	public string AffaireNumero { get; set; }

	public string ErpDesignationDocument { get; set; }
}
