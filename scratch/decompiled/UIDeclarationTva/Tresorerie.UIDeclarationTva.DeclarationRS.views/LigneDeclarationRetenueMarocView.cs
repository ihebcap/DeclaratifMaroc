using System;
using DevExpress.XtraEditors.DXErrorProvider;
using Tresorerie.Core.Enum;

namespace Tresorerie.UIDeclarationTva.DeclarationRS.views;

public class LigneDeclarationRetenueMarocView : IDXDataErrorInfo
{
	public int No { get; set; }

	public int DeclarationNo { get; set; }

	public int CaisseNo { get; set; }

	public int TiersNo { get; set; }

	public string TiersCode { get; set; }

	public string TiersIntitule { get; set; }

	public string TiersIdentifiant { get; set; }

	public int RetenueNo { get; set; }

	public string RetenueNumero { get; set; }

	public DateTime RetenueDate { get; set; }

	public int RetenueModeNo { get; set; }

	public decimal RetenueTaux { get; set; }

	public decimal RetenueMontant { get; set; }

	public decimal RetenueBase { get; set; }

	public int DossierReglementNo { get; set; }

	public string DossierReglementNumero { get; set; }

	public DateTime DossierReglementDate { get; set; }

	public int EcheanceNo { get; set; }

	public string EcheanceNumero { get; set; }

	public DateTime EcheanceDateDocument { get; set; }

	public DateTime EcheanceDatePrevue { get; set; }

	public decimal EcheanceMontant { get; set; }

	public int? EcheanceDesignationDocumentNo { get; set; }

	public int ReglementNo { get; set; }

	public string ReglementNumero { get; set; }

	public DateTime ReglementDate { get; set; }

	public DateTime ReglementEcheance { get; set; }

	public int ReglementModeNo { get; set; }

	public EtatComptabilite ReglementIsComptabilise { get; set; }

	public decimal ReglementMontant { get; set; }

	public decimal DeclarationMontant { get; set; }

	public string NumeroPieceRapprochement { get; set; }

	public DateTime? DateRapprochementCompta { get; set; }

	public bool HasError
	{
		get
		{
			if (!string.IsNullOrEmpty(TiersIdentifiant) && !string.IsNullOrEmpty(TiersCode) && !string.IsNullOrEmpty(RetenueNumero))
			{
				return !EcheanceDesignationDocumentNo.HasValue;
			}
			return true;
		}
	}

	public void GetError(ErrorInfo info)
	{
	}

	public void GetPropertyError(string propertyName, ErrorInfo info)
	{
		if (propertyName == "TiersCode" && string.IsNullOrEmpty(TiersCode))
		{
			info.ErrorText = "Le tiers est obligatoire.";
			info.ErrorType = ErrorType.Critical;
		}
		else if (propertyName == "TiersIdentifiant" && string.IsNullOrEmpty(TiersIdentifiant))
		{
			info.ErrorText = "L'identifiant est obligatoire.";
			info.ErrorType = ErrorType.Critical;
		}
		else if (propertyName == "RetenueNumero" && string.IsNullOrEmpty(RetenueNumero))
		{
			info.ErrorText = "Le numéro de la retenue est obligatoire.";
			info.ErrorType = ErrorType.Critical;
		}
		else if (propertyName == "EcheanceDesignationDocumentNo" && !EcheanceDesignationDocumentNo.HasValue)
		{
			info.ErrorText = "La désignation du document est obligatoire.";
			info.ErrorType = ErrorType.Critical;
		}
	}
}
