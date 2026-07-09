using System;
using System.Collections.Generic;
using System.Linq;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;
using Tresorerie.Core.Services;
using Tresorerie.Erp.ICore;
using Tresorerie.UICommun.Helper;
using Tresorerie.UICommun.Helper.Views;
using Tresorerie.UIDeclarationTva.DeclarationRS.views;
using Tresorerie.UIDeclarationTva.DeclarationTej.views;

namespace Tresorerie.UIDeclarationTva.DeclarationRS.Controllers;

public class ListeRetenueFournisseurController
{
	private readonly IGroupeService _groupe;

	private readonly CaisseViewHelper _caisseHelper;

	private readonly ModeViewHelper _modeHelper;

	private readonly DeviseViewHelper _deviseHelper;

	private readonly EcheanceFournisseurHelper _echeanceFournisseurHelper;

	private readonly IErpComptaService _erpComptaService;

	private readonly BanqueViewHelper _banqueHelper;

	private readonly FournisseurErpHelper _fournisseurErpHelper;

	public ListeRetenueFournisseurController(IGroupeService groupe, CaisseViewHelper caisseHelper, ModeViewHelper modeHelper, DeviseViewHelper deviseHelper, EcheanceFournisseurHelper echeanceFournisseurHelper, IErpComptaService erpComptaService, BanqueViewHelper banqueHelper, FournisseurErpHelper fournisseurErpHelper)
	{
		_groupe = groupe ?? throw new ArgumentNullException("groupe");
		_caisseHelper = caisseHelper ?? throw new ArgumentNullException("caisseHelper");
		_modeHelper = modeHelper ?? throw new ArgumentNullException("modeHelper");
		_deviseHelper = deviseHelper ?? throw new ArgumentNullException("deviseHelper");
		_echeanceFournisseurHelper = echeanceFournisseurHelper ?? throw new ArgumentNullException("echeanceFournisseurHelper");
		_erpComptaService = erpComptaService ?? throw new ArgumentNullException("erpComptaService");
		_banqueHelper = banqueHelper ?? throw new ArgumentNullException("banqueHelper");
		_fournisseurErpHelper = fournisseurErpHelper ?? throw new ArgumentNullException("fournisseurErpHelper");
	}

	public List<CaisseView> GetAllCaisse()
	{
		return _caisseHelper.GetAllCaissesDepense().ToList();
	}

	public List<ModeView> GetAllMode()
	{
		return _modeHelper.GetAll().ToList();
	}

	public List<OperationRetenuSource> GetAllOperationRetenue()
	{
		return _groupe.OperationRetenuSourceManager.GetAll();
	}

	public List<DesignationDocument> GetAllDesignationDocument()
	{
		DesignationDocumentManager designationDocumentManager = _groupe.DesignationDocumentManager;
		Societe societe = _groupe.SocieteManager.Societe;
		IEnumerable<SocieteDesignationDocument> societeDesignationDocument = societe.GetSocieteDesignationDocument();
		return (from d in designationDocumentManager.GetAll()
			where societeDesignationDocument.Any((SocieteDesignationDocument sd) => sd.DesignationDocumentNo == d.No)
			select d).ToList();
	}

	public List<LigneDeclarationTejView> GetAllRetenueToDeclarationTej(DeclarationRetenuSourceView view)
	{
		return (from x in _groupe.SocieteManager.CaisseManager.GetAllRetenusToDeclarationRAS(new DateTime(2024, 6, 1).Date, view.DateFin.Date.AddDays(1.0).AddSeconds(-1.0))
			where !x.IsDeclarer
			select x).Select(ToView).ToList();
	}

	public List<LigneDeclarationRetenueMarocView> GetAllRetenueToDeclaration(DeclarationRetenuSourceView view)
	{
		if (view == null)
		{
			throw new ArgumentNullException("view");
		}
		List<LigneDeclarationRetenueMarocView> list = new List<LigneDeclarationRetenueMarocView>();
		SocieteManager societeManager = _groupe.SocieteManager;
		CaisseManager caisseManager = societeManager.CaisseManager;
		Societe societe = societeManager.Societe;
		List<RetenuALaSource> list2 = (from x in caisseManager.GetAllRetenusToDeclarationRAS(((societe.LegislationType == Legislation.Maroc) ? new DateTime(2024, 7, 1) : new DateTime(2024, 6, 1)).Date, view.DateFin.Date.AddDays(1.0).AddSeconds(-1.0))
			where x.DossierNo.HasValue && (societe.LegislationType != Legislation.Maroc || x.RetenuePourEcheanceNo.HasValue)
			select x).ToList();
		List<IErpComptaEcritureComptable> source = new List<IErpComptaEcritureComptable>();
		List<EcritureComptable> source2 = new List<EcritureComptable>();
		List<IErpComptaJournal> source3 = new List<IErpComptaJournal>();
		IErpExercice erpExercice = _erpComptaService.GetAllExercice().SingleOrDefault((IErpExercice x) => !x.IsCloture && x.Annee == view.DateDebut.Year);
		if (societe.LegislationType == Legislation.Maroc && societe.DeclarationRetenueUseRapprochementErp)
		{
			source = _erpComptaService.GetEcrituresRapproche(erpExercice.Debut.Date, view.DateFin.Date.AddDays(1.0).AddSeconds(-1.0)).ToList();
			source2 = caisseManager.GetEcrituresByExercice(view.Exercice, MouvementDomaine.ReglementFournisseur, societe).ToList();
			source3 = _erpComptaService.GetAllJournals().ToList();
		}
		foreach (RetenuALaSource item in list2)
		{
			Echeance echeance = societeManager.EcheanceGet(item.RetenuePourEcheanceNo.Value);
			if (echeance == null)
			{
				throw new ApplicationException("Impossible de charger l'échéance de la retenue " + item.Numero + ".");
			}
			DossierReglement dossierReglement = societeManager.GetDossierReglement(item.DossierNo.Value);
			if (dossierReglement == null)
			{
				throw new ApplicationException("Impossible de charger le dossier de la retenue " + item.Numero + ".");
			}
			if (dossierReglement.StatutDossier != StatutDossierReglement.Valide)
			{
				continue;
			}
			List<LigneDossierReglementComm> list3 = (from x in societeManager.LigneDossierReglementCommGetLigneReglement(dossierReglement.No)
				where !x.IsRetenue
				select x).ToList();
			decimal num = list3.Sum((LigneDossierReglementComm x) => x.MontantAPaye);
			IErpTiersIce infoFournisseur = _fournisseurErpHelper.GetInfoFournisseur(dossierReglement.TiersNo);
			foreach (LigneDossierReglementComm item2 in list3)
			{
				ReglementFournisseur reglement = caisseManager.ReglementFournisseurGet(item2.EntityNo);
				if (reglement == null)
				{
					throw new ApplicationException("Impossible de charger le règlement.");
				}
				string numeroPieceRapprochement = "";
				DateTime? dateRapprochementCompta = null;
				if (societe.LegislationType == Legislation.Maroc && societe.DeclarationRetenueUseRapprochementErp)
				{
					if (reglement.IsComptabilise != EtatComptabilite.Comptabilise)
					{
						continue;
					}
					if (reglement.Type == ReglementType.Cheque || reglement.Type == ReglementType.Traite || reglement.Type == ReglementType.Virement)
					{
						if (!reglement.BanqueNo.HasValue)
						{
							continue;
						}
						IErpCompteBanq banque = _banqueHelper.Get(reglement.BanqueNo.Value);
						if (banque == null)
						{
							continue;
						}
						IErpComptaJournal erpComptaJournal = source3.FirstOrDefault((IErpComptaJournal x) => x.Code == banque.Journal);
						if (erpComptaJournal == null)
						{
							throw new ApplicationException("Impossible de charger le journal de la banque " + banque.Abrege + ".");
						}
						if (erpComptaJournal.Rapprochement != ErpComptaTypeRapprochement.Tresorerie)
						{
							continue;
						}
						string journal = banque.Journal;
						string compte = erpComptaJournal.CompteGeneral;
						EcritureComptable ecritureReglementBanque = source2.FirstOrDefault((EcritureComptable x) => x.MouvementNo == reglement.No && x.CompteGeneral == compte && x.CodeJournal == journal);
						if (ecritureReglementBanque == null)
						{
							continue;
						}
						IErpComptaEcritureComptable erpComptaEcritureComptable = source.FirstOrDefault((IErpComptaEcritureComptable x) => x.No == ecritureReglementBanque.ErpNo);
						if (erpComptaEcritureComptable == null)
						{
							continue;
						}
						numeroPieceRapprochement = erpComptaEcritureComptable.PieceTresorerie;
						dateRapprochementCompta = erpComptaEcritureComptable.DateRapprochement;
					}
				}
				if (societeManager.LigneDeclarationRetenuSourceGet(item.No, reglement.No) == null)
				{
					decimal num2 = Math.Round(num / item2.MontantAPaye, 6);
					decimal declarationMontant = Math.Round(item.Montant / num2, societe.GetDefaultDeviseSociete().NombreDecimales);
					list.Add(new LigneDeclarationRetenueMarocView
					{
						CaisseNo = dossierReglement.CaisseNo,
						DeclarationMontant = declarationMontant,
						EcheanceDateDocument = echeance.DocumentDate,
						EcheanceDatePrevue = echeance.Date,
						EcheanceDesignationDocumentNo = echeance.DesignationDocumentNo,
						EcheanceNo = echeance.No,
						EcheanceNumero = echeance.DocumentNumero,
						EcheanceMontant = echeance.Montant,
						ReglementDate = reglement.Date,
						ReglementEcheance = reglement.DateEcheance,
						ReglementModeNo = reglement.ModeReglementNo,
						ReglementNo = reglement.No,
						ReglementNumero = reglement.Numero,
						ReglementMontant = reglement.Montant,
						RetenueDate = item.Date,
						DossierReglementNo = dossierReglement.No,
						DossierReglementNumero = dossierReglement.Numero,
						DossierReglementDate = dossierReglement.Date,
						RetenueModeNo = item.ModeNo,
						RetenueNo = item.No,
						RetenueNumero = item.Numero,
						RetenueTaux = item.Taux,
						RetenueBase = item.Base,
						RetenueMontant = item.Montant,
						TiersCode = dossierReglement.TiersCode,
						TiersIntitule = dossierReglement.TiersIntitule,
						TiersNo = dossierReglement.TiersNo,
						ReglementIsComptabilise = reglement.IsComptabilise,
						DateRapprochementCompta = dateRapprochementCompta,
						NumeroPieceRapprochement = numeroPieceRapprochement,
						TiersIdentifiant = (infoFournisseur?.TiersIdentifiant ?? string.Empty)
					});
				}
			}
		}
		return list;
	}

	public SocieteDevise GetDefaultDeviseSociete()
	{
		return _groupe.SocieteManager.Societe.GetDefaultDeviseSociete();
	}

	public string GetDefaultFormatDevise()
	{
		Societe societe = _groupe.SocieteManager.Societe;
		return (_deviseHelper.GetSocieteDevise(societe) ?? throw new ApplicationException("Impossible de charger la devise société.")).Format;
	}

	public LigneDeclarationRetenueSourceView InitView()
	{
		SocieteManager societeManager = _groupe.SocieteManager;
		Societe societe = societeManager.Societe;
		string numeroPieceCourante = societeManager.GetNumeroPieceCourante(EntityNumerotation.Retenue);
		SocieteDevise defaultDeviseSociete = societe.GetDefaultDeviseSociete();
		if (defaultDeviseSociete == null)
		{
			throw new ApplicationException("Impossible de charger la devise société.");
		}
		Caisse defaultCaisse = societeManager.GetDefaultCaisse(ProfilType.Grf);
		return new LigneDeclarationRetenueSourceView
		{
			Numero = numeroPieceCourante,
			IsComptabilise = false,
			Date = DateTime.Now.Date,
			NombreTimbre = 0,
			MontantTimbre = 0m,
			Libelle = string.Empty,
			DeviseNo = defaultDeviseSociete.No,
			Cours = defaultDeviseSociete.Cours,
			CaisseNo = (defaultCaisse?.No ?? 0)
		};
	}

	private LigneDeclarationTejView ToView(RetenuALaSource ligne)
	{
		if (ligne == null)
		{
			throw new ArgumentNullException("ligne");
		}
		IErpFournisseur erpFournisseur = _fournisseurErpHelper.Get(ligne.FournisseurNo);
		IErpTiersIce infoFournisseur = _fournisseurErpHelper.GetInfoFournisseur(ligne.FournisseurNo);
		NatureFournisseur natureFournisseur = _fournisseurErpHelper.GetNatureFournisseur(ligne.FournisseurCode);
		return new LigneDeclarationTejView
		{
			No = ligne.No,
			CaisseNo = ligne.CaisseNo,
			Date = ligne.Date,
			DossierNo = ligne.DossierNo,
			DossierNumero = ligne.DossierNumero,
			FournisseurIntitule = ligne.FournisseurIntitule,
			FournisseurNo = ligne.FournisseurNo,
			FournisseurNumero = ligne.FournisseurCode,
			IsComptabilise = ligne.IsComptabilise,
			ExerciceFacturation = ligne.AnneeFacturation,
			IsConvention = ligne.IsConvention,
			IsPrisCharge = ligne.IsPrisCharge,
			ModeReglementNo = ligne.ModeNo,
			MontantHorsTaxe = ligne.MontantHorsTaxe,
			MontantRetenue = ligne.Montant,
			MontantTtc = ligne.Base,
			Numero = ligne.Numero,
			OperationNo = ligne.OperationRetenueNo,
			Reference = ligne.Libelle,
			TauxRetenue = ligne.Taux,
			TauxTva = ligne.TauxTva,
			IsRecuperer = (ligne.Recuperer == RemisFournisseur.Recuperer),
			DateRecuperation = ((ligne.Recuperer == RemisFournisseur.Recuperer) ? new DateTime?(ligne.DateRecuperation) : ((DateTime?)null)),
			FournisseurActivite = (infoFournisseur?.TiersActivite ?? string.Empty),
			FournisseurAdresse = (erpFournisseur?.Adresse ?? string.Empty),
			FournisseurDateNaissance = infoFournisseur?.TiersDateNaissance,
			FournisseurEmail = (infoFournisseur?.TiersEmail ?? string.Empty),
			FournisseurIdentifiant = (infoFournisseur?.TiersIdentifiant ?? string.Empty),
			FournisseurTelephone = (erpFournisseur?.Telephone ?? string.Empty),
			NatureFournisseur = natureFournisseur
		};
	}

	public IDictionary<int, string> GetRepoFormatDevise()
	{
		Dictionary<int, string> dictionary = new Dictionary<int, string>();
		foreach (DeviseView item in _deviseHelper.GetAll().ToList())
		{
			dictionary.Add(item.No, $"n{item.NombreDecimales}");
		}
		return dictionary;
	}

	public List<DeviseView> GetAllDevise()
	{
		return _deviseHelper.GetAll().ToList();
	}

	public Legislation GetCurrentLegislation()
	{
		return _groupe.SocieteManager.Societe.LegislationType;
	}

	public EcheanceView GetEcheance(int echeanceNo)
	{
		return _echeanceFournisseurHelper.Get(echeanceNo);
	}
}
