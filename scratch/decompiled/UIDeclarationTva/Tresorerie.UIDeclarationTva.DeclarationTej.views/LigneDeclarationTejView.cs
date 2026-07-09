using System;
using DevExpress.XtraEditors.DXErrorProvider;
using Tresorerie.Core.Enum;
using Tresorerie.Infrastructure.Helpers;

namespace Tresorerie.UIDeclarationTva.DeclarationTej.views;

public class LigneDeclarationTejView : IDXDataErrorInfo
{
	public int No { get; set; }

	public string Numero { get; set; }

	public DateTime Date { get; set; }

	public int FournisseurNo { get; set; }

	public string FournisseurNumero { get; set; }

	public string FournisseurIntitule { get; set; }

	public bool IsPrisCharge { get; set; }

	public bool IsConvention { get; set; }

	public int ExerciceFacturation { get; set; }

	public int CaisseNo { get; set; }

	public int? OperationNo { get; set; }

	public decimal MontantHorsTaxe { get; set; }

	public decimal TauxTva { get; set; }

	public decimal MontantTva
	{
		get
		{
			if (!(TauxTva == 0m))
			{
				return MontantHorsTaxe * TauxTva / 100m;
			}
			return 0m;
		}
	}

	public decimal MontantTtc { get; set; }

	public int ModeReglementNo { get; set; }

	public decimal TauxRetenue { get; set; }

	public decimal MontantRetenue { get; set; }

	public decimal MontantNetPaye => MontantTtc - MontantRetenue;

	public string Reference { get; set; }

	public int? DossierNo { get; set; }

	public string DossierNumero { get; set; }

	public bool IsComptabilise { get; set; }

	public bool IsRecuperer { get; set; }

	public DateTime? DateRecuperation { get; set; }

	public string FournisseurIdentifiant { get; set; }

	public DateTime? FournisseurDateNaissance { get; set; }

	public string FournisseurActivite { get; set; }

	public string FournisseurEmail { get; set; }

	public string FournisseurTelephone { get; set; }

	public string FournisseurAdresse { get; set; }

	public NatureFournisseur NatureFournisseur { get; set; }

	public bool HasError
	{
		get
		{
			if (!string.IsNullOrEmpty(FournisseurIdentifiant) && !FournisseurDateNaissance.IsNotLogique() && !string.IsNullOrEmpty(FournisseurNumero) && !string.IsNullOrEmpty(FournisseurActivite) && !string.IsNullOrEmpty(FournisseurEmail) && !string.IsNullOrEmpty(FournisseurTelephone))
			{
				return string.IsNullOrEmpty(FournisseurAdresse);
			}
			return true;
		}
	}

	public void GetError(ErrorInfo info)
	{
	}

	public void GetPropertyError(string propertyName, ErrorInfo info)
	{
		if (propertyName == "FournisseurNumero" && string.IsNullOrEmpty(FournisseurNumero))
		{
			info.ErrorText = "Le fournisseur est obligatoire.";
			info.ErrorType = ErrorType.Critical;
		}
		else if (propertyName == "FournisseurIdentifiant" && string.IsNullOrEmpty(FournisseurIdentifiant))
		{
			info.ErrorText = "L'identifiant est obligatoire.";
			info.ErrorType = ErrorType.Critical;
		}
		else if (propertyName == "FournisseurDateNaissance" && FournisseurDateNaissance.IsNotLogique() && NatureFournisseur == NatureFournisseur.PersonnePhysique)
		{
			info.ErrorText = "La date de naissance est obligatoire.";
			info.ErrorType = ErrorType.Critical;
		}
		else if (propertyName == "FournisseurActivite" && string.IsNullOrEmpty(FournisseurActivite))
		{
			info.ErrorText = "L'activité est obligatoire.";
			info.ErrorType = ErrorType.Critical;
		}
		else if (propertyName == "FournisseurEmail" && string.IsNullOrEmpty(FournisseurEmail))
		{
			info.ErrorText = "L'email est obligatoire.";
			info.ErrorType = ErrorType.Critical;
		}
		else if (propertyName == "FournisseurTelephone" && string.IsNullOrEmpty(FournisseurTelephone))
		{
			info.ErrorText = "Le numéro de téléphone est obligatoire.";
			info.ErrorType = ErrorType.Critical;
		}
		else if (propertyName == "FournisseurAdresse" && string.IsNullOrEmpty(FournisseurAdresse))
		{
			info.ErrorText = "L'adresse est obligatoire.";
			info.ErrorType = ErrorType.Critical;
		}
	}
}
