using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class DossierImpaye : ICanBeComptabilise, ICanBeLettre
{
	public int No { get; set; }

	public string Numero { get; set; }

	public DateTime Date { get; set; }

	public int CaisseNo { get; set; }

	public int TiersNo { get; set; }

	public string TiersNumero { get; set; }

	public string TiersIntitule { get; set; }

	public TiersType TiersType { get; set; }

	public LettrageType Lettrage { get; set; }

	public string Lettre { get; set; }

	public int? CommissionNo { get; set; }

	public decimal MontantCommission { get; set; }

	public int? InteretNo { get; set; }

	public decimal MontantInteret { get; set; }

	public decimal MontantImpaye { get; set; }

	public decimal Montant => MontantCommission + MontantInteret + MontantImpaye;

	public string Commentaire { get; set; }

	public int SocieteNo { get; set; }

	public decimal TauxInteret { get; set; }

	public bool IsComptabilise { get; set; }

	public int UtilisateurNo { get; set; }

	public DateTime DateCreation { get; set; }

	public DateTime DateModification { get; set; }

	public int ModificateurNo { get; set; }

	public int NombreJour { get; set; }

	public ErpDomaine Domaine { get; set; }

	public StatutDossierImpaye Statut { get; set; }

	public string IsLettre => Lettrage.GetDisplayDescription();

	public string StatutCaption => Statut.GetDisplayDescription();

	public override int GetHashCode()
	{
		return No.GetHashCode();
	}

	public override bool Equals(object obj)
	{
		if (obj == null)
		{
			return false;
		}
		if (!(obj is DossierImpaye dossierImpaye))
		{
			return false;
		}
		return dossierImpaye.GetHashCode() == GetHashCode();
	}
}
