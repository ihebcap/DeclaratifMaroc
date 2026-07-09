using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Transactions;
using Serilog;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;
using Tresorerie.Core.Services;
using Tresorerie.Erp.ICore;
using Tresorerie.Infrastructure;
using Tresorerie.UICommun.Helper;
using Tresorerie.UICommun.Helper.Views;
using Tresorerie.UICommun.LicenceGratuite;
using Tresorerie.UIDeclarationTva.Infrastructures;

namespace Tresorerie.UIDeclarationTva.DeclarationDelaisPaiement;

public class ListDeclarationDelaisPaiementController
{
	private readonly ILicenceApplicationVersion _licenceApplicationVersion;

	private readonly IErpComptaService _erpComptaService;

	private readonly IGroupeService _groupeService;

	private readonly DeviseViewHelper _deviseViewHelper;

	private readonly DeclarationDelaisPaiementFileGenerator _declarationFileGenerator;

	private readonly ModeViewHelper _modeHelper;

	private readonly ILock _locker;

	public ListDeclarationDelaisPaiementController(ILicenceApplicationVersion licenceApplicationVersion, IErpComptaService erpComptaService, IGroupeService groupeService, DeviseViewHelper deviseViewHelper, ILock locker, DeclarationDelaisPaiementFileGenerator declarationFileGenerator, ModeViewHelper modeHelper)
	{
		_licenceApplicationVersion = licenceApplicationVersion ?? throw new ArgumentNullException("licenceApplicationVersion");
		_erpComptaService = erpComptaService ?? throw new ArgumentNullException("erpComptaService");
		_groupeService = groupeService ?? throw new ArgumentNullException("groupeService");
		_deviseViewHelper = deviseViewHelper ?? throw new ArgumentNullException("deviseViewHelper");
		_declarationFileGenerator = declarationFileGenerator ?? throw new ArgumentNullException("declarationFileGenerator");
		_locker = locker ?? throw new ArgumentNullException("locker");
		_modeHelper = modeHelper ?? throw new ArgumentNullException("modeHelper");
	}

	public List<ModeView> GetAllMode()
	{
		return _modeHelper.GetAll().ToList();
	}

	public string GetDefaultDeviseFormat()
	{
		Societe societe = _groupeService.SocieteManager.Societe;
		return _deviseViewHelper.GetSocieteDevise(societe).Format;
	}

	public void DeleteLigneDeclaration(LigneDeclarationDelaisPaiementView view)
	{
		if (view == null)
		{
			throw new ArgumentNullException("view");
		}
		_groupeService.SocieteManager.DeclarationDelaisPaiementDeleteLigne(view.No);
	}

	public DeclarationDelaisPaiementView InitView()
	{
		SocieteManager societeManager = _groupeService.SocieteManager;
		Societe societe = societeManager.Societe;
		string numeroPieceCourante = societeManager.GetNumeroPieceCourante(EntityNumerotation.DeclarationDelaisPaiement);
		return new DeclarationDelaisPaiementView
		{
			Numero = numeroPieceCourante,
			Date = DateTime.Now,
			DateDebut = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1),
			DateFin = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month)),
			Exercice = DateTime.Now.Year,
			SocieteNo = societe.No,
			Statut = StatutDeclaration.EnCours,
			TypeDeclaration = societe.DeclarationDelaisPaiementType,
			Libelle = string.Empty,
			TrimestrePeriode = DeclarationTvaEncaissementTrimestrePeriode.T1
		};
	}

	public List<IErpExercice> GetAllExercice()
	{
		return (from x in _erpComptaService.GetAllExercice()
			where !x.IsCloture
			select x).ToList();
	}

	public int GetNombreDecimalDefaultDevise()
	{
		Societe societe = _groupeService.SocieteManager.Societe;
		return _deviseViewHelper.GetSocieteDevise(societe).NombreDecimales;
	}

	public List<DeclarationDelaisPaiementView> GetAllDeclaration()
	{
		return _groupeService.SocieteManager.DeclarationDelaisPaiementGetAll().Select(ToView).ToList();
	}

	private DeclarationDelaisPaiementView ToView(DeclarationDelaisPaiementTiers declaration)
	{
		if (declaration == null)
		{
			throw new ArgumentNullException("declaration");
		}
		return new DeclarationDelaisPaiementView
		{
			No = declaration.No,
			Date = declaration.Date,
			DateDebut = declaration.DateDebut,
			DateFin = declaration.DateFin,
			Exercice = declaration.Exercice,
			IsDepose = declaration.IsDepose,
			SocieteNo = declaration.SocieteNo,
			Statut = declaration.Statut,
			TypeDeclaration = declaration.TypeDeclaration,
			Libelle = declaration.Libelle,
			Numero = declaration.Numero,
			IsFichierGenerer = declaration.IsFichierGenerer,
			TrimestrePeriode = declaration.TrimestrePeriode
		};
	}

	public DeclarationDelaisPaiementView Get(int no)
	{
		if (no <= 0)
		{
			throw new ArgumentException("no");
		}
		DeclarationDelaisPaiementTiers declaration = _groupeService.SocieteManager.DeclarationDelaisPaiementGet(no);
		return ToView(declaration);
	}

	public void UpdateDeclaration(DeclarationDelaisPaiementView view)
	{
		if (view == null)
		{
			throw new ArgumentNullException("view");
		}
		_groupeService.SocieteManager.DeclarationDelaisPaiementUpdate(view.No, view.Libelle);
	}

	public void HasRestrictionConsultation()
	{
	}

	public void HasRestrictionAjout()
	{
	}

	public void ClotureDeclaration(DeclarationDelaisPaiementView view)
	{
		if (view == null)
		{
			throw new ArgumentNullException("view");
		}
		_groupeService.SocieteManager.DeclarationDelaisPaiementCloture(view.No);
	}

	public void AnnulerClotureDeclaration(DeclarationDelaisPaiementView view)
	{
		if (view == null)
		{
			throw new ArgumentNullException("view");
		}
		_groupeService.SocieteManager.DeclarationDelaisPaiementAnnulerCloture(view.No);
	}

	public void DeposeDeclaration(DeclarationDelaisPaiementView view)
	{
		if (view == null)
		{
			throw new ArgumentNullException("view");
		}
		_groupeService.SocieteManager.DeclarationDelaisPaiementDepose(view.No);
	}

	public void AnnulerGenerationFichierDeclaration(DeclarationDelaisPaiementView view)
	{
		try
		{
			if (view == null)
			{
				throw new ArgumentNullException("view");
			}
			_groupeService.SocieteManager.DeclarationDelaisPaiementFichierAnnulerGeneration(view.No);
		}
		catch (Exception ex)
		{
			throw ex;
		}
	}

	public void GenererFichierDeclaration(DeclarationDelaisPaiementView view, string path, IProgress<int> progress, CancellationToken cancellationToken)
	{
		try
		{
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
			Log.Verbose("Déclaration délais de paiement : [Début] Génération du fichier xml");
			_declarationFileGenerator.Generate(view.No, path, defaultDeviseSociete.NombreDecimales, progress, cancellationToken);
			Log.Verbose("Déclaration délais de paiement : [Fin] Génération du fichier xml");
			societeManager.DeclarationDelaisPaiementFichierGenerer(view.No);
		}
		catch (Exception ex)
		{
			throw ex;
		}
	}

	public int CreateDeclaration(DeclarationDelaisPaiementView view)
	{
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
		int num = societeManager.DeclarationDelaisPaiementCreate(view.Numero, view.Exercice, view.Date, view.TypeDeclaration, view.Libelle, view.TrimestrePeriode);
		if (!_locker.Lock(TypeEntity.DeclarationDelaisPaiement, num, societe.No, out var lockNo))
		{
			throw new InvalidOperationException("Impossible de verouiller la déclaration.");
		}
		view.LockNo = lockNo;
		view.No = num;
		transactionScope.Complete();
		return num;
	}

	public void DeleteDeclaration(DeclarationDelaisPaiementView view)
	{
		if (view == null)
		{
			throw new ArgumentNullException("view");
		}
		_groupeService.SocieteManager.DeclarationDelaisPaiementDelete(view.No);
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

	public bool IsLocked(DeclarationDelaisPaiementView view)
	{
		if (view == null)
		{
			throw new ArgumentNullException("view");
		}
		Societe societe = _groupeService.SocieteManager.Societe;
		return _locker.IsLocked(TypeEntity.DeclarationDelaisPaiement, view.No, societe.No);
	}

	public List<LigneDeclarationDelaisPaiementView> GetLignesDeclaration(DeclarationDelaisPaiementView view)
	{
		if (view == null)
		{
			throw new ArgumentNullException("view");
		}
		return _groupeService.SocieteManager.DeclarationDelaisPaiementLigneGetAll(view.No).Select(ToView).ToList();
	}

	private LigneDeclarationDelaisPaiementView ToView(LigneDeclarationDelaisPaiement ligne)
	{
		if (ligne == null)
		{
			throw new ArgumentNullException("ligne");
		}
		return new LigneDeclarationDelaisPaiementView
		{
			No = ligne.No,
			DeclarationNo = ligne.DeclarationNo,
			AffectationNo = ligne.AffectationNo,
			Depassement = ligne.Depassement,
			EcheanceNo = ligne.EcheanceNo,
			AffectationMontant = ligne.AffectationMontant,
			ReglementCours = ligne.ReglementCours,
			ReglementSolde = ligne.ReglementSolde,
			ReglementMontant = ligne.ReglementMontant,
			ReglementEcheance = ligne.ReglementEcheance,
			ReglementDate = ligne.ReglementDate,
			ReglementNumero = ligne.ReglementNumero,
			ReglementNo = ligne.ReglementNo,
			ReglementInfoLibre1 = ligne.ReglementInfoLibre1,
			ReglementInfoLibre2 = ligne.ReglementInfoLibre2,
			ReglementInfoLibre3 = ligne.ReglementInfoLibre3,
			ReglementInfoLibre4 = ligne.ReglementInfoLibre4,
			ReglementModeNo = ligne.ReglementModeNo,
			ReglementPieceNumero = ligne.ReglementPieceNumero,
			ReglementDatePoint = (ligne.ReglementIsPointe ? ligne.ReglementDatePoint : ((DateTime?)null)),
			ReglementIsPointe = ligne.ReglementIsPointe,
			DocumentCours = ligne.DocumentCours,
			DocumentDate = ligne.DocumentDate,
			DocumentDeviseNo = ligne.DocumentDeviseNo,
			DocumentEcheanceLegale = ligne.DocumentEcheanceLegale,
			DocumentEcheancePrevue = ligne.DocumentEcheancePrevue,
			DocumentMontant = ligne.DocumentMontant,
			DocumentNumero = ligne.DocumentNumero,
			DocumentSolde = ligne.DocumentSolde,
			DocumentInfoLibre1 = ligne.DocumentInfoLibre1,
			DocumentInfoLibre2 = ligne.DocumentInfoLibre2,
			DocumentInfoLibre3 = ligne.DocumentInfoLibre3,
			DocumentInfoLibre4 = ligne.DocumentInfoLibre4,
			TiersCode = ligne.TiersCode,
			TiersIntitule = ligne.TiersIntitule,
			TiersNo = ligne.TiersNo
		};
	}
}
