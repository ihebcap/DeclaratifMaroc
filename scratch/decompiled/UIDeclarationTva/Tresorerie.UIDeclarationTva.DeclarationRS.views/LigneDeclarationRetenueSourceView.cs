using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.UIDeclarationTva.DeclarationRS.views;

public class LigneDeclarationRetenueSourceView
{
	public int No { get; set; }

	public string Numero { get; set; }

	public DateTime Date { get; set; }

	public int ModeReglementNo { get; set; }

	public int NombreTimbre { get; set; }

	public decimal MontantTimbre { get; set; }

	public decimal Taux { get; set; }

	public decimal BaseRetenue { get; set; }

	public decimal Montant { get; set; }

	public decimal MontantDeviseSociete { get; set; }

	public string Libelle { get; set; }

	public int CaisseNo { get; set; }

	public int FournisseurNo { get; set; }

	public string FournisseurNumero { get; set; }

	public string FournisseurIntitule { get; set; }

	public TiersType TiersType { get; set; }

	public string Beneficiaire { get; set; }

	public int? DossierNo { get; set; }

	public string DossierNumero { get; set; }

	public bool IsRecuperer { get; set; }

	public bool IsReserveDossierFrs { get; set; }

	public string AffaireNumero { get; set; }

	public int DeviseNo { get; set; }

	public decimal Cours { get; set; }

	public bool IsComptabilise { get; set; }

	public int? RetenuePourEcheanceNo { get; set; }

	public string EcheanceNumero { get; set; }

	public override int GetHashCode()
	{
		return new { No, Numero }.GetHashCode();
	}

	public override bool Equals(object obj)
	{
		if (obj == null)
		{
			return false;
		}
		if (!(obj is LigneDeclarationRetenueSourceView ligneDeclarationRetenueSourceView))
		{
			return false;
		}
		return ligneDeclarationRetenueSourceView.GetHashCode() == GetHashCode();
	}
}
