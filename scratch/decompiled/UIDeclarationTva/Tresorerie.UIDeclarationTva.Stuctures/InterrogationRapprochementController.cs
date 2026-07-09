using System;
using System.Collections.Generic;
using System.Linq;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;
using Tresorerie.Core.Services;
using Tresorerie.Erp.ICore;
using Tresorerie.Infrastructure;
using Tresorerie.UICommun.Helper;
using Tresorerie.UICommun.LicenceGratuite;

namespace Tresorerie.UIDeclarationTva.Stuctures;

public class InterrogationRapprochementController
{
	private readonly ILicenceApplicationVersion _licenceApplicationVersion;

	private readonly IErpComptaService _erpComptaService;

	private readonly IGroupeService _groupeService;

	private readonly DeviseViewHelper _deviseViewHelper;

	public InterrogationRapprochementController(ILicenceApplicationVersion licenceApplicationVersion, IErpComptaService erpComptaService, IGroupeService groupeService, DeviseViewHelper deviseViewHelper)
	{
		_licenceApplicationVersion = licenceApplicationVersion ?? throw new ArgumentNullException("licenceApplicationVersion");
		_erpComptaService = erpComptaService ?? throw new ArgumentNullException("erpComptaService");
		_groupeService = groupeService ?? throw new ArgumentNullException("groupeService");
		_deviseViewHelper = deviseViewHelper ?? throw new ArgumentNullException("deviseViewHelper");
	}

	public string GetDefaultDeviseFormat()
	{
		Societe societe = _groupeService.SocieteManager.Societe;
		return _deviseViewHelper.GetSocieteDevise(societe).Format;
	}

	public int GetNombreDecimalDefaultDevise()
	{
		Societe societe = _groupeService.SocieteManager.Societe;
		return _deviseViewHelper.GetSocieteDevise(societe).NombreDecimales;
	}

	public List<InterrogationRapprochementView> GetEcritureRapproche(DateTime dateDebut, DateTime dateFin)
	{
		SocieteManager societeManager = _groupeService.SocieteManager;
		CaisseManager caisseManager = societeManager.CaisseManager;
		PrevisionnelManager previsionnelManager = _groupeService.PrevisionnelManager;
		Societe societe = societeManager.Societe;
		List<InterrogationRapprochementView> list = _erpComptaService.GetEcrituresRapproche(dateDebut, dateFin).Select(ToView).ToList();
		List<EcritureComptable> list2 = new List<EcritureComptable>();
		list2.AddRange(caisseManager.GetEcrituresByExercice(dateDebut.Year, MouvementDomaine.ReglementClient, societe));
		list2.AddRange(caisseManager.GetEcrituresByExercice(dateDebut.Year, MouvementDomaine.EnteteBordereau, societe));
		list2.AddRange(caisseManager.GetEcrituresByExercice(dateDebut.Year, MouvementDomaine.ReglementFournisseur, societe));
		list2.AddRange(caisseManager.GetEcrituresByExercice(dateDebut.Year, MouvementDomaine.OperationBancaire, societe));
		list2.AddRange(caisseManager.GetEcrituresByExercice(dateDebut.Year, MouvementDomaine.VirementInterne, societe));
		list2.AddRange(caisseManager.GetEcrituresByExercice(dateDebut.Year, MouvementDomaine.VirementTiers, societe));
		list2.AddRange(caisseManager.GetEcrituresByExercice(dateDebut.Year, MouvementDomaine.AlimentationCaisse, societe));
		foreach (InterrogationRapprochementView ec in list)
		{
			EcritureComptable ecritureComptable = list2.FirstOrDefault((EcritureComptable x) => x.ErpNo == ec.No);
			ec.IsTresoEntity = ecritureComptable != null;
			if (ecritureComptable == null || !ecritureComptable.MouvementNo.HasValue)
			{
				continue;
			}
			switch (ecritureComptable.Domaine)
			{
			case MouvementDomaine.ReglementClient:
			{
				ReglementClient reglementClient = caisseManager.ReglementGet(ecritureComptable.MouvementNo.Value);
				if (reglementClient != null && !reglementClient.GetAffectations().All((Affectation x) => !x.DeclarationTvaEncaissementNo.HasValue))
				{
					Affectation affectation = reglementClient.GetAffectations().Last((Affectation x) => x.DeclarationTvaEncaissementNo.HasValue);
					DeclarationTvaEncaissement declarationTvaEncaissement2 = societeManager.DeclarationTvaEncaissementGet(affectation.DeclarationTvaEncaissementNo.Value);
					if (declarationTvaEncaissement2 != null)
					{
						ec.IsDeclare = true;
						ec.DeclarationNo = declarationTvaEncaissement2.No;
						ec.DeclarationNumero = declarationTvaEncaissement2.Numero;
					}
				}
				break;
			}
			case MouvementDomaine.ReglementFournisseur:
			{
				ReglementFournisseur reglementFournisseur = caisseManager.ReglementFournisseurGet(ecritureComptable.MouvementNo.Value);
				if (reglementFournisseur != null && !reglementFournisseur.GetAffectations().All((Affectation x) => !x.DeclarationTvaEncaissementNo.HasValue))
				{
					Affectation affectation2 = reglementFournisseur.GetAffectations().Last((Affectation x) => x.DeclarationTvaEncaissementNo.HasValue);
					DeclarationTvaEncaissement declarationTvaEncaissement3 = societeManager.DeclarationTvaEncaissementGet(affectation2.DeclarationTvaEncaissementNo.Value);
					if (declarationTvaEncaissement3 != null)
					{
						ec.IsDeclare = true;
						ec.DeclarationNo = declarationTvaEncaissement3.No;
						ec.DeclarationNumero = declarationTvaEncaissement3.Numero;
					}
				}
				break;
			}
			case MouvementDomaine.OperationBancaire:
			{
				OperationBancaire operationBancaire = previsionnelManager.GetOperationBancaire(ecritureComptable.MouvementNo.Value);
				if (operationBancaire != null && operationBancaire.DeclarationTvaEncaissementNo.HasValue)
				{
					DeclarationTvaEncaissement declarationTvaEncaissement = societeManager.DeclarationTvaEncaissementGet(operationBancaire.DeclarationTvaEncaissementNo.Value);
					if (declarationTvaEncaissement != null)
					{
						ec.IsDeclare = true;
						ec.DeclarationNo = declarationTvaEncaissement.No;
						ec.DeclarationNumero = declarationTvaEncaissement.Numero;
					}
				}
				break;
			}
			default:
				throw new ApplicationException("Domaine invalide. EC");
			case MouvementDomaine.EnteteBordereau:
			case MouvementDomaine.AlimentationCaisse:
			case MouvementDomaine.VirementInterne:
			case MouvementDomaine.VirementTiers:
				break;
			}
		}
		return list.ToList();
	}

	private InterrogationRapprochementView ToView(IErpComptaEcritureComptable ec)
	{
		return new InterrogationRapprochementView
		{
			Annee = ec.Annee,
			CodeJournal = ec.CodeJournal,
			CompteGeneral = ec.CompteGeneral,
			ContrePartieCompteG = ec.ContrePartieCompteG,
			ContrePartieTiers = ec.ContrePartieTiers,
			Cours = ec.Cours,
			Date = ec.Date,
			DateCreation = ec.DateCreation,
			DeviseErpNo = ec.DeviseErpNo,
			DossierNo = ec.DossierNo,
			Echeance = ec.Echeance,
			IsLettre = ec.IsLettre,
			IsPointe = ec.IsPointe,
			Jour = ec.Jour,
			Lettrage = ec.Lettrage,
			Libelle = ec.Libelle,
			LigneNo = ec.LigneNo,
			MontantDeviseSociete = ec.MontantDeviseSociete,
			MontantDevise = ec.MontantDevise,
			No = ec.No,
			NumeroDocument = ec.NumeroDocument,
			NumeroPiece = ec.NumeroPiece,
			Periode = ec.Periode,
			PieceTresorerie = ec.PieceTresorerie,
			DateRapprochement = ec.DateRapprochement,
			IsRapproche = ec.IsRapproche,
			Pointage = ec.Pointage,
			Reference = ec.Reference,
			Sens = ec.Sens,
			TiersNumero = ec.TiersNumero,
			MontantCreditDevise = ec.MontantCreditDevise,
			MontantDebitDevise = ec.MontantDebitDevise,
			MontantCreditDeviseSociete = ec.MontantCreditDeviseSociete,
			MontantDebitDeviseSociete = ec.MontantDebitDeviseSociete
		};
	}

	public List<IErpExercice> GetAllExercice()
	{
		return (from x in _erpComptaService.GetAllExercice()
			where !x.IsCloture
			select x).ToList();
	}

	public void AuthorisationExport()
	{
	}

	public bool IsLicenceGratuit()
	{
		try
		{
			_licenceApplicationVersion.ThrowIfGratuit();
			return true;
		}
		catch (Exception ex)
		{
			new FrmMessageBoxLicenceGratuite(ex.Message).ShowDialog();
			return false;
		}
	}
}
