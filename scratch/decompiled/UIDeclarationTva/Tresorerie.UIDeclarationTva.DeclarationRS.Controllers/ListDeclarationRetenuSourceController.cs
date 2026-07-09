using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Transactions;
using Serilog;
using Tresorerie.Authorization.Core;
using Tresorerie.Authorization.Core.Actions.Grf;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Interfaces;
using Tresorerie.Core.Models;
using Tresorerie.Core.Services;
using Tresorerie.Erp.ICore;
using Tresorerie.Infrastructure;
using Tresorerie.UICommun.Components;
using Tresorerie.UICommun.Helper;
using Tresorerie.UICommun.Helper.Views;
using Tresorerie.UICommun.LicenceGratuite;
using Tresorerie.UIDeclarationTva.DeclarationRS.views;
using Tresorerie.UIDeclarationTva.DeclarationTej.views;
using Tresorerie.UIDeclarationTva.Infrastructures;

namespace Tresorerie.UIDeclarationTva.DeclarationRS.Controllers;

public class ListDeclarationRetenuSourceController
{
	private readonly ILicenceApplicationVersion _licenceApplicationVersion;

	private readonly IErpComptaService _erpComptaService;

	private readonly IErpCommService _erpCommService;

	private readonly IGroupeService _groupeService;

	private readonly DeviseViewHelper _deviseViewHelper;

	private readonly BanqueViewHelper _banqueHelper;

	private readonly DeclarationRetenuSourceFileGenerator _declarationFileGenerator;

	private readonly ILock _locker;

	private readonly CaisseViewHelper _caisseHelper;

	private readonly ModeViewHelper _modeHelper;

	private readonly IDeclarationRetenuSourceRepository _declarationRetenuSourceRepository;

	private readonly IRetenuALaSourceRepository _retenuALaSourceRepository;

	private readonly IDossierReglementRepository _dossierReglementRepository;

	private readonly EcheanceHelper _echeanceHelper;

	private readonly FournisseurErpHelper _fournisseurErpHelper;

	private readonly NotifyService _notifyService;

	private readonly IAuthorizationService _authorizationService;

	public ListDeclarationRetenuSourceController(ILicenceApplicationVersion licenceApplicationVersion, IErpComptaService erpComptaService, IErpCommService erpCommService, IGroupeService groupeService, DeviseViewHelper deviseViewHelper, ILock locker, BanqueViewHelper banqueHelper, DeclarationRetenuSourceFileGenerator declarationFileGenerator, CaisseViewHelper caisseHelper, ModeViewHelper modeHelper, IDeclarationRetenuSourceRepository declarationRetenuSourceRepository, IRetenuALaSourceRepository retenuALaSourceRepository, FournisseurErpHelper fournisseurErpHelper, NotifyService notifyService, IAuthorizationService authorizationService)
	{
		_licenceApplicationVersion = licenceApplicationVersion ?? throw new ArgumentNullException("licenceApplicationVersion");
		_erpComptaService = erpComptaService ?? throw new ArgumentNullException("erpComptaService");
		_erpCommService = erpCommService ?? throw new ArgumentNullException("erpCommService");
		_groupeService = groupeService ?? throw new ArgumentNullException("groupeService");
		_deviseViewHelper = deviseViewHelper ?? throw new ArgumentNullException("deviseViewHelper");
		_banqueHelper = banqueHelper ?? throw new ArgumentNullException("banqueHelper");
		_declarationFileGenerator = declarationFileGenerator ?? throw new ArgumentNullException("declarationFileGenerator");
		_locker = locker ?? throw new ArgumentNullException("locker");
		_caisseHelper = caisseHelper;
		_modeHelper = modeHelper;
		_declarationRetenuSourceRepository = declarationRetenuSourceRepository;
		_retenuALaSourceRepository = retenuALaSourceRepository;
		_fournisseurErpHelper = fournisseurErpHelper ?? throw new ArgumentNullException("fournisseurErpHelper");
		_notifyService = notifyService;
		_authorizationService = authorizationService;
	}

	public IList<IErpTaxe> GetAllErpTaxe()
	{
		return _erpCommService.GetAllTaxes().ToList();
	}

	public string GetDefaultDeviseFormat()
	{
		Societe societe = _groupeService.SocieteManager.Societe;
		return _deviseViewHelper.GetSocieteDevise(societe).Format;
	}

	public DeclarationRetenuSourceView InitView()
	{
		SocieteManager societeManager = _groupeService.SocieteManager;
		Societe societe = societeManager.Societe;
		string numeroPieceCourante = societeManager.GetNumeroPieceCourante(EntityNumerotation.DeclarationRetenuSource);
		IErpExercice erpExercice = GetAllExercice()?.OrderBy((IErpExercice x) => x.Annee)?.LastOrDefault();
		if (erpExercice == null)
		{
			return new DeclarationRetenuSourceView
			{
				Numero = numeroPieceCourante,
				Date = DateTime.Now,
				DateDebut = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1),
				DateFin = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month)),
				SocieteNo = societe.No,
				Statut = StatutDeclaration.EnCours,
				Libelle = string.Empty,
				MoisPeriode = (DeclarationMoisPeriode)DateTime.Now.Month,
				DateComptabilisation = DateTime.Now
			};
		}
		int annee = erpExercice.Annee;
		int month = DateTime.Now.Month;
		return new DeclarationRetenuSourceView
		{
			Exercice = annee,
			Numero = numeroPieceCourante,
			Date = DateTime.Now,
			DateDebut = new DateTime(annee, month, 1),
			DateFin = new DateTime(annee, month, DateTime.DaysInMonth(annee, month)),
			SocieteNo = societe.No,
			Statut = StatutDeclaration.EnCours,
			Libelle = string.Empty,
			MoisPeriode = (DeclarationMoisPeriode)DateTime.Now.Month,
			DateComptabilisation = DateTime.Now
		};
	}

	public void HasRestrictionConsult()
	{
		if (_licenceApplicationVersion.Application == ApplicationRunning.TresoClient)
		{
			_authorizationService.HasRestriction(new DeclarationRasGrcConsulter(), ProfilType.Grc);
		}
		else
		{
			_authorizationService.HasRestriction(new DeclarationRasGrfConsulter(), ProfilType.Grf);
		}
	}

	public void HasRestrictionAjout()
	{
		if (_licenceApplicationVersion.Application == ApplicationRunning.TresoClient)
		{
			_authorizationService.HasRestriction(new DeclarationRasGrcAjout(), ProfilType.Grc);
		}
		else
		{
			_authorizationService.HasRestriction(new DeclarationRasGrfAjout(), ProfilType.Grf);
		}
	}

	public DossierReglement GetDossierView(int dossierNo)
	{
		if (dossierNo <= 0)
		{
			throw new ArgumentNullException("dossierNo");
		}
		return _groupeService.SocieteManager.GetDossierReglement(dossierNo);
	}

	public List<IErpExercice> GetAllExercice()
	{
		return (from x in _erpComptaService.GetAllExercice()
			where !x.IsCloture
			select x).ToList();
	}

	public List<CaisseView> GetAllCaisse()
	{
		return _caisseHelper.GetAllCaissesDepense().ToList();
	}

	public List<ModeView> GetAllMode()
	{
		return _modeHelper.GetAll().ToList();
	}

	public List<DeviseView> GetAllDevise()
	{
		return _deviseViewHelper.GetAll().ToList();
	}

	public SocieteDevise GetDefaultDeviseSociete()
	{
		return _groupeService.SocieteManager.Societe.GetDefaultDeviseSociete();
	}

	public int GetNombreDecimalDefaultDevise()
	{
		Societe societe = _groupeService.SocieteManager.Societe;
		return _deviseViewHelper.GetSocieteDevise(societe).NombreDecimales;
	}

	public List<DeclarationRetenuSourceRecapView> GetRecapDeclaration(DeclarationRetenuSourceView view)
	{
		if (view == null)
		{
			throw new ArgumentNullException("view");
		}
		SocieteManager societeManager = _groupeService.SocieteManager;
		Societe societe = societeManager.Societe;
		List<LigneDeclarationRetenueMarocView> list = societeManager.DeclarationRetenuSourceLigneGetAll(view.No).Select(ToView).ToList();
		List<LigneDeclarationRetenueMarocView> collection = list;
		return (from x in collection
			group x by new { x.RetenueTaux, x.RetenueModeNo } into x
			select new DeclarationRetenuSourceRecapView
			{
				SocieteNo = societe.No,
				Taux = x.Key.RetenueTaux,
				Total = collection.Where((LigneDeclarationRetenueMarocView t) => t.RetenueModeNo == x.Key.RetenueModeNo).Sum((LigneDeclarationRetenueMarocView t) => t.DeclarationMontant),
				ModeReglementNo = x.Key.RetenueModeNo
			}).ToList();
	}

	public List<DeclarationRetenuSourceView> GetAllDeclaration()
	{
		return _groupeService.SocieteManager.DeclarationRetenuSourceGetAll().Select(ToView).ToList();
	}

	private DeclarationRetenuSourceView ToView(DeclarationRetenuSource declaration)
	{
		if (declaration == null)
		{
			throw new ArgumentNullException("declaration");
		}
		return new DeclarationRetenuSourceView
		{
			No = declaration.No,
			Date = declaration.Date,
			DateDebut = declaration.DateDebut,
			DateFin = declaration.DateFin,
			Exercice = declaration.Exercice,
			IsDepose = declaration.IsDepose,
			SocieteNo = declaration.SocieteNo,
			Statut = declaration.Statut,
			Libelle = declaration.Libelle,
			Numero = declaration.Numero,
			IsFichierGenerer = declaration.IsFichierGenerer,
			MoisPeriode = declaration.MoisPeriode,
			IsComptabilise = declaration.IsComptabilise,
			DateComptabilisation = declaration.DateComptabilisation,
			Nature = declaration.Nature
		};
	}

	public DeclarationRetenuSourceView Get(int no)
	{
		if (no <= 0)
		{
			throw new ArgumentException("no");
		}
		DeclarationRetenuSource declaration = _groupeService.SocieteManager.DeclarationRetenuSourceGet(no);
		return ToView(declaration);
	}

	public void UpdateDeclaration(DeclarationRetenuSourceView view)
	{
		if (_licenceApplicationVersion.Application == ApplicationRunning.TresoClient)
		{
			_authorizationService.HasRestriction(new DeclarationRasGrcModifier(), ProfilType.Grc);
		}
		else
		{
			_authorizationService.HasRestriction(new DeclarationRasGrfModifier(), ProfilType.Grf);
		}
		if (view == null)
		{
			throw new ArgumentNullException("view");
		}
		_groupeService.SocieteManager.DeclarationRetenuSourceUpdate(view.No, view.Libelle);
	}

	public void ClotureDeclaration(DeclarationRetenuSourceView view)
	{
		if (_licenceApplicationVersion.Application == ApplicationRunning.TresoClient)
		{
			_authorizationService.HasRestriction(new DeclarationRasGrcCloturer(), ProfilType.Grc);
		}
		else
		{
			_authorizationService.HasRestriction(new DeclarationRasGrfCloturer(), ProfilType.Grf);
		}
		if (view == null)
		{
			throw new ArgumentNullException("view");
		}
		_groupeService.SocieteManager.DeclarationRetenuSourceCloture(view.No);
	}

	public void AnnulerClotureDeclaration(DeclarationRetenuSourceView view)
	{
		if (_licenceApplicationVersion.Application == ApplicationRunning.TresoClient)
		{
			_authorizationService.HasRestriction(new DeclarationRasGrcDecloture(), ProfilType.Grc);
		}
		else
		{
			_authorizationService.HasRestriction(new DeclarationRasGrfDecloture(), ProfilType.Grf);
		}
		if (view == null)
		{
			throw new ArgumentNullException("view");
		}
		_groupeService.SocieteManager.DeclarationRetenuSourceAnnulerCloture(view.No);
	}

	public void DeposeDeclaration(DeclarationRetenuSourceView view)
	{
		if (view == null)
		{
			throw new ArgumentNullException("view");
		}
		_groupeService.SocieteManager.DeclarationRetenuSourceDepose(view.No);
	}

	public void GenererFichierDeclaration(DeclarationRetenuSourceView view, string path, IProgress<int> progress, CancellationToken cancellationToken)
	{
		try
		{
			if (_licenceApplicationVersion.Application == ApplicationRunning.TresoClient)
			{
				_authorizationService.HasRestriction(new DeclarationRasGrcGenerer(), ProfilType.Grc);
			}
			else
			{
				_authorizationService.HasRestriction(new DeclarationRasGrfGenerer(), ProfilType.Grf);
			}
			if (view == null)
			{
				throw new ArgumentNullException("view");
			}
			if (string.IsNullOrEmpty(path))
			{
				throw new ArgumentNullException("path");
			}
			if (!Directory.Exists(path))
			{
				throw new ApplicationException("Chemain invalide.");
			}
			SocieteManager societeManager = _groupeService.SocieteManager;
			SocieteDevise defaultDeviseSociete = societeManager.Societe.GetDefaultDeviseSociete();
			if (defaultDeviseSociete == null)
			{
				throw new ApplicationException("Impossible de déterminer la devise société.");
			}
			Log.Verbose("Déclaration RAS : [Début] Génération du fichier xml");
			_declarationFileGenerator.Generate(view.No, path, defaultDeviseSociete.NombreDecimales, progress, cancellationToken);
			Log.Verbose("Déclaration RAS : [Fin] Génération du fichier xml");
			societeManager.DeclarationRetenuSourceFichierGenerer(view.No);
		}
		catch (Exception ex)
		{
			Log.Error($"Generer fichier déclaration: {ex.Message} - {ex}");
			throw ex;
		}
	}

	public void AnnulerGenerationFichierDeclaration(DeclarationRetenuSourceView view)
	{
		try
		{
			if (_licenceApplicationVersion.Application == ApplicationRunning.TresoClient)
			{
				_authorizationService.HasRestriction(new DeclarationRasGrcAnnulerGeneration(), ProfilType.Grc);
			}
			else
			{
				_authorizationService.HasRestriction(new DeclarationRasGrfAnnulerGeneration(), ProfilType.Grf);
			}
			if (view == null)
			{
				throw new ArgumentNullException("view");
			}
			_groupeService.SocieteManager.DeclarationRetenuSourceFichierAnnulerGeneration(view.No);
		}
		catch (Exception ex)
		{
			throw ex;
		}
	}

	public int CreateDeclaration(DeclarationRetenuSourceView view)
	{
		if (_licenceApplicationVersion.Application == ApplicationRunning.TresoClient)
		{
			_authorizationService.HasRestriction(new DeclarationRasGrcAjout(), ProfilType.Grc);
		}
		else
		{
			_authorizationService.HasRestriction(new DeclarationRasGrfAjout(), ProfilType.Grf);
		}
		if (view == null)
		{
			throw new ArgumentNullException("view");
		}
		SocieteManager societeManager = _groupeService.SocieteManager;
		Societe societe = societeManager.Societe;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		int num = societeManager.DeclarationRetenuSourceCreate(view.Numero, view.Exercice, view.Date, view.Libelle, view.MoisPeriode, view.Nature, view.DateDebut, view.DateFin);
		if (!_locker.Lock(TypeEntity.DeclarationRetenuSource, num, societe.No, out var lockNo))
		{
			throw new InvalidOperationException("Impossible de verouiller la déclaration.");
		}
		view.LockNo = lockNo;
		view.No = num;
		transactionScope.Complete();
		return num;
	}

	public void DeleteDeclaration(DeclarationRetenuSourceView view)
	{
		if (_licenceApplicationVersion.Application == ApplicationRunning.TresoClient)
		{
			_authorizationService.HasRestriction(new DeclarationRasGrcSupprimer(), ProfilType.Grc);
		}
		else
		{
			_authorizationService.HasRestriction(new DeclarationRasGrfSupprimer(), ProfilType.Grf);
		}
		if (view == null)
		{
			throw new ArgumentNullException("view");
		}
		_groupeService.SocieteManager.DeclarationRetenuSourceDelete(view.No);
	}

	public void DeleteLigneDeclaration(LigneDeclarationRetenueMarocView view)
	{
		if (view == null)
		{
			throw new ArgumentNullException("view");
		}
		_groupeService.SocieteManager.DeclarationRetenuSourceDeleteLigne(view.No);
	}

	public void DeleteLigneDeclarationTej(LigneDeclarationTejView view)
	{
		if (view == null)
		{
			throw new ArgumentNullException("view");
		}
		_groupeService.SocieteManager.DeclarationRetenuSourceTejDeleteLigne(view.No);
	}

	public List<OperationRetenuSource> GetAllOperationRetenue()
	{
		return _groupeService.OperationRetenuSourceManager.GetAll();
	}

	public void AddLignesDeclaration(DeclarationRetenuSourceView view, IEnumerable<LigneDeclarationRetenueMarocView> lignes)
	{
		if (view == null)
		{
			throw new ArgumentNullException("view");
		}
		if (lignes == null)
		{
			throw new ArgumentNullException("lignes");
		}
		SocieteManager societeManager = _groupeService.SocieteManager;
		foreach (LigneDeclarationRetenueMarocView ligne in lignes)
		{
			IErpFournisseur erpFournisseur = _fournisseurErpHelper.Get(ligne.TiersNo);
			if (erpFournisseur == null)
			{
				throw new ApplicationException("Impossible de charger le fournisseur.");
			}
			IErpTiersIce infoFournisseur = _fournisseurErpHelper.GetInfoFournisseur(erpFournisseur.No);
			if (infoFournisseur == null || string.IsNullOrEmpty(infoFournisseur.TiersIdentifiant))
			{
				throw new ApplicationException("L'identifiant du fournisseur [" + erpFournisseur.Numero + "] est invalide.");
			}
			societeManager.DeclarationRetenuSourceLigneAjouter(view.No, ligne.RetenueNo, ligne.ReglementNo, ligne.DeclarationMontant);
		}
	}

	public void AddLignesDeclarationTej(DeclarationRetenuSourceView view, IEnumerable<LigneDeclarationTejView> lignes)
	{
		if (view == null)
		{
			throw new ArgumentNullException("view");
		}
		if (lignes == null)
		{
			throw new ArgumentNullException("lignes");
		}
		SocieteManager societeManager = _groupeService.SocieteManager;
		foreach (LigneDeclarationTejView ligne in lignes)
		{
			IErpFournisseur erpFournisseur = _fournisseurErpHelper.Get(ligne.FournisseurNo);
			if (erpFournisseur == null)
			{
				throw new ApplicationException("Impossible de charger le fournisseur.");
			}
			IErpTiersIce infoFournisseur = _fournisseurErpHelper.GetInfoFournisseur(erpFournisseur.No);
			if (infoFournisseur == null || string.IsNullOrEmpty(infoFournisseur.TiersIdentifiant))
			{
				throw new ApplicationException("L'identifiant du fournisseur [" + erpFournisseur.Numero + "] est invalide.");
			}
		}
		societeManager.DeclarationRetenuSourceTejLigneAjouter(view.No, lignes.Select((LigneDeclarationTejView x) => x.No).ToList());
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

	public bool IsLocked(DeclarationRetenuSourceView view)
	{
		if (view == null)
		{
			throw new ArgumentNullException("view");
		}
		Societe societe = _groupeService.SocieteManager.Societe;
		return _locker.IsLocked(TypeEntity.DeclarationRetenuSource, view.No, societe.No);
	}

	public List<LigneDeclarationRetenueMarocView> GetLignesDeclaration(DeclarationRetenuSourceView view)
	{
		if (view == null)
		{
			throw new ArgumentNullException("view");
		}
		return _groupeService.SocieteManager.DeclarationRetenuSourceLigneGetAll(view.No).Select(ToView).ToList();
	}

	public List<LigneDeclarationTejView> GetLignesDeclarationTej(DeclarationRetenuSourceView view)
	{
		if (view == null)
		{
			throw new ArgumentNullException("view");
		}
		return _groupeService.SocieteManager.RetenueSourceTejGetAllByDeclaration(view.No).Select(ToView).ToList();
	}

	private LigneDeclarationRetenueMarocView ToView(LigneDeclarationRetenuSource lg)
	{
		if (lg == null)
		{
			throw new ArgumentNullException("lg");
		}
		SocieteManager societeManager = _groupeService.SocieteManager;
		CaisseManager caisseManager = societeManager.CaisseManager;
		RetenuALaSource retenuALaSource = caisseManager.RetenueGet(lg.RetenueNo);
		if (retenuALaSource == null)
		{
			throw new ApplicationException("Impossible de charger la retenue.");
		}
		ReglementFournisseur reglementFournisseur = caisseManager.ReglementFournisseurGet(lg.MouvementNo);
		if (reglementFournisseur == null)
		{
			throw new ApplicationException("Impossible de charger le règlement.");
		}
		Echeance echeance = societeManager.EcheanceGet(retenuALaSource.RetenuePourEcheanceNo.Value);
		if (echeance == null)
		{
			throw new ApplicationException("Impossible de charger l'échéance de la retenue " + retenuALaSource.Numero + ".");
		}
		DossierReglement dossierReglement = societeManager.GetDossierReglement(retenuALaSource.DossierNo.Value);
		if (dossierReglement == null)
		{
			throw new ApplicationException("Impossible de charger le dossier de la retenue " + retenuALaSource.Numero + ".");
		}
		IErpTiersIce infoFournisseur = _fournisseurErpHelper.GetInfoFournisseur(dossierReglement.TiersNo);
		return new LigneDeclarationRetenueMarocView
		{
			DeclarationNo = lg.DeclarationNo,
			No = lg.No,
			CaisseNo = dossierReglement.CaisseNo,
			DeclarationMontant = lg.Montant,
			EcheanceDateDocument = echeance.DocumentDate,
			EcheanceDatePrevue = echeance.Date,
			EcheanceDesignationDocumentNo = echeance.DesignationDocumentNo,
			EcheanceNo = echeance.No,
			EcheanceNumero = echeance.DocumentNumero,
			EcheanceMontant = echeance.Montant,
			ReglementDate = reglementFournisseur.Date,
			ReglementEcheance = reglementFournisseur.DateEcheance,
			ReglementModeNo = reglementFournisseur.ModeReglementNo,
			ReglementNo = reglementFournisseur.No,
			ReglementNumero = reglementFournisseur.Numero,
			ReglementMontant = reglementFournisseur.Montant,
			RetenueDate = retenuALaSource.Date,
			DossierReglementNo = dossierReglement.No,
			DossierReglementNumero = dossierReglement.Numero,
			DossierReglementDate = dossierReglement.Date,
			RetenueModeNo = retenuALaSource.ModeNo,
			RetenueNo = retenuALaSource.No,
			RetenueNumero = retenuALaSource.Numero,
			RetenueTaux = retenuALaSource.Taux,
			RetenueBase = retenuALaSource.Base,
			RetenueMontant = retenuALaSource.Montant,
			TiersCode = dossierReglement.TiersCode,
			TiersIntitule = dossierReglement.TiersIntitule,
			TiersNo = dossierReglement.TiersNo,
			ReglementIsComptabilise = reglementFournisseur.IsComptabilise,
			TiersIdentifiant = (infoFournisseur?.TiersIdentifiant ?? string.Empty)
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

	public List<DesignationDocument> GetAllDesignationDocument()
	{
		DesignationDocumentManager designationDocumentManager = _groupeService.DesignationDocumentManager;
		Societe societe = _groupeService.SocieteManager.Societe;
		IEnumerable<SocieteDesignationDocument> societeDesignationDocument = societe.GetSocieteDesignationDocument();
		return (from d in designationDocumentManager.GetAll()
			where societeDesignationDocument.Any((SocieteDesignationDocument sd) => sd.DesignationDocumentNo == d.No)
			select d).ToList();
	}

	public void GenerateCSVFile(string path)
	{
		CsvGenerator.GeneratModel<LigneDeclarationRetenuSourceImport, LigneDeclarationRetenuSourceImportMap>(path);
	}

	public Legislation GetCurrentLegislation()
	{
		return _groupeService.SocieteManager.Societe.LegislationType;
	}
}
