using System;
using DevExpress.XtraEditors.DXErrorProvider;
using Tresorerie.Core.Enum;

namespace Tresorerie.UIDeclarationTva.Stuctures;

public class LigneDeclarationTvaEncaissementView : IDXDataErrorInfo
{
	public int No { get; set; }

	public int DeclarationNo { get; set; }

	public LigneDeclarationTvaEncaissementEntityType Type { get; set; }

	public int EntityNo { get; set; }

	public bool IsReport { get; set; }

	public string MouvementNumero { get; set; }

	public string DocumentNumero { get; set; }

	public decimal Assiette { get; set; }

	public decimal Taux { get; set; }

	public decimal Montant { get; set; }

	public LigneDeclarationTvaEncaissementDomaine Domaine { get; set; }

	public string TiersCode { get; set; }

	public string TiersIntitule { get; set; }

	public string TiersIdentifiant { get; set; }

	public string TiersIce { get; set; }

	public DateTime DateMouvement { get; set; }

	public DateTime DateDocument { get; set; }

	public LigneDeclarationTvaEncaissementIntegrationType Integration { get; set; }

	public ReglementType TypePayement { get; set; }

	public string ErpTaxeCode { get; set; }

	public string CodeActivite { get; set; }

	public decimal Prorata { get; set; }

	public string DesignationDocument { get; set; }

	public bool HasError
	{
		get
		{
			if (!string.IsNullOrEmpty(TiersIdentifiant) && !string.IsNullOrEmpty(TiersCode) && !string.IsNullOrEmpty(TiersIce) && !string.IsNullOrEmpty(DocumentNumero))
			{
				return string.IsNullOrEmpty(DesignationDocument);
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
		else if (propertyName == "DesignationDocument" && string.IsNullOrEmpty(DesignationDocument))
		{
			info.ErrorText = "La désignation du document est obligatoire.";
			info.ErrorType = ErrorType.Critical;
		}
		else if (propertyName == "TiersIce" && string.IsNullOrEmpty(TiersIce))
		{
			info.ErrorText = "L'Ice du tiers est obligatoire.";
			info.ErrorType = ErrorType.Critical;
		}
		else if (propertyName == "DocumentNumero" && string.IsNullOrEmpty(DocumentNumero))
		{
			info.ErrorText = "Le numéro du document est obligatoire.";
			info.ErrorType = ErrorType.Critical;
		}
	}
}
