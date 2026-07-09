using System;
using DevExpress.XtraEditors.DXErrorProvider;
using Tresorerie.Core.Enum;

namespace Tresorerie.UIDeclarationTva.Stuctures;

public class DeclarationTvaView : IDXDataErrorInfo
{
	public int SocieteNo { get; set; }

	public int MouvementNo { get; set; }

	public string MouvementNumero { get; set; }

	public DateTime MouvementDate { get; set; }

	public DateTime MouvementEcheance { get; set; }

	public decimal MouvementMontantDeviseSociete { get; set; }

	public int MouvementModeNo { get; set; }

	public MouvementDomaine MouvementDomaine { get; set; }

	public ReglementType MouvementTypeMode { get; set; }

	public string MouvementEnteteBordereauNumero { get; set; }

	public string MouvementBanqueAbregee { get; set; }

	public int DocumentNo { get; set; }

	public string DocumentNumero { get; set; }

	public DateTime? DocumentDate { get; set; }

	public DateTime? DocumentEcheance { get; set; }

	public decimal DocumentMontantDeviseSociete { get; set; }

	public int AffectationNo { get; set; }

	public decimal AffectationMontant { get; set; }

	public DateTime? AffectationDate { get; set; }

	public DateTime? DateRapprochementComptable { get; set; }

	public string PieceTresorerieComptable { get; set; }

	public decimal AssietteDeclaration { get; set; }

	public decimal TauxTva { get; set; }

	public decimal MontantDeclaration { get; set; }

	public bool IsDeclare { get; set; }

	public DateTime? DateDeclaration { get; set; }

	public LigneDeclarationTvaEncaissementDomaine DomaineDeclarationTva { get; set; }

	public string TiersCode { get; set; }

	public string TiersIntitule { get; set; }

	public string TiersIdentifiant { get; set; }

	public string TiersIce { get; set; }

	public string ErpTaxeCode { get; set; }

	public string CodeActivite { get; set; }

	public decimal Prorata { get; set; }

	public string DesignationDocument { get; set; }

	public Legislation Legislation { get; set; }

	public bool HasError
	{
		get
		{
			if (!string.IsNullOrEmpty(TiersIdentifiant) && !string.IsNullOrEmpty(TiersIce) && !string.IsNullOrEmpty(DesignationDocument))
			{
				MouvementDomaine mouvementDomaine = MouvementDomaine;
				if ((mouvementDomaine != MouvementDomaine.ReglementClient && mouvementDomaine != MouvementDomaine.ReglementFournisseur) || (!string.IsNullOrEmpty(TiersCode) && !string.IsNullOrEmpty(DocumentNumero)))
				{
					if (Legislation == Legislation.Maroc)
					{
						if (TiersIdentifiant.Length == 8 && !TiersIdentifiant.Contains(" ") && TiersIce.Length == 15)
						{
							return TiersIce.Contains(" ");
						}
						return true;
					}
					return false;
				}
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
			MouvementDomaine mouvementDomaine = MouvementDomaine;
			if (mouvementDomaine != MouvementDomaine.ReglementClient && mouvementDomaine == MouvementDomaine.ReglementFournisseur)
			{
				info.ErrorText = "Le tiers est obligatoire.";
				info.ErrorType = ErrorType.Critical;
				return;
			}
		}
		if (propertyName == "TiersIdentifiant" && string.IsNullOrEmpty(TiersIdentifiant))
		{
			info.ErrorText = "L'identifiant est obligatoire.";
			info.ErrorType = ErrorType.Critical;
		}
		else if (propertyName == "TiersIdentifiant" && Legislation == Legislation.Maroc && TiersIdentifiant.Length != 8)
		{
			info.ErrorText = "Longueur invalide. 8 caractères requis.";
			info.ErrorType = ErrorType.Critical;
		}
		else if (propertyName == "TiersIdentifiant" && Legislation == Legislation.Maroc && TiersIdentifiant.Contains(" "))
		{
			info.ErrorText = "Les espaces ne sont pas autorisés.";
			info.ErrorType = ErrorType.Critical;
		}
		else if (propertyName == "TiersIce" && string.IsNullOrEmpty(TiersIce))
		{
			info.ErrorText = "L'Ice du tiers est obligatoire.";
			info.ErrorType = ErrorType.Critical;
		}
		else if (propertyName == "TiersIce" && Legislation == Legislation.Maroc && TiersIce.Length != 15)
		{
			info.ErrorText = "Longueur invalide. 15 caractères requis.";
			info.ErrorType = ErrorType.Critical;
		}
		else if (propertyName == "TiersIce" && Legislation == Legislation.Maroc && TiersIce.Contains(" "))
		{
			info.ErrorText = "Les espaces ne sont pas autorisés.";
			info.ErrorType = ErrorType.Critical;
		}
		else if (propertyName == "DesignationDocument" && string.IsNullOrEmpty(DesignationDocument))
		{
			info.ErrorText = "La désignation du document est obligatoire.";
			info.ErrorType = ErrorType.Critical;
		}
		else if (propertyName == "DocumentNumero" && string.IsNullOrEmpty(DocumentNumero))
		{
			MouvementDomaine mouvementDomaine = MouvementDomaine;
			if (mouvementDomaine != MouvementDomaine.ReglementClient && mouvementDomaine == MouvementDomaine.ReglementFournisseur)
			{
				info.ErrorText = "Le numéro du document est obligatoire.";
				info.ErrorType = ErrorType.Critical;
			}
		}
	}
}
