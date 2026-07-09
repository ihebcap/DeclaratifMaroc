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
using Tresorerie.Core.Models;
using Tresorerie.Core.Services;
using Tresorerie.Erp.ICore;
using Tresorerie.Infrastructure;
using Tresorerie.UICommun.Components;
using Tresorerie.UICommun.Helper;
using Tresorerie.UICommun.LicenceGratuite;
using Tresorerie.UIDeclarationTva.ImportService;
using Tresorerie.UIDeclarationTva.Infrastructures;

namespace Tresorerie.UIDeclarationTva.Stuctures;

public class ListDeclarationTvaController
{
	private readonly ILicenceApplicationVersion _licenceApplicationVersion;

	private readonly IErpComptaService _erpComptaService;

	private readonly IErpCommService _erpCommService;

	private readonly IGroupeService _groupeService;

	private readonly DeviseViewHelper _deviseViewHelper;

	private readonly BanqueViewHelper _banqueHelper;

	private readonly DeclarationTvaEncaissementFileGenerator _declarationFileGenerator;

	private readonly LigneDeclarationTvaEncaissementImportService _importService;

	private readonly IAuthorizationService _authorizationService;

	private readonly ILock _locker;

	public ListDeclarationTvaController(ILicenceApplicationVersion licenceApplicationVersion, IErpComptaService erpComptaService, IErpCommService erpCommService, IGroupeService groupeService, DeviseViewHelper deviseViewHelper, ILock locker, BanqueViewHelper banqueHelper, DeclarationTvaEncaissementFileGenerator declarationFileGenerator, LigneDeclarationTvaEncaissementImportService importService, IAuthorizationService authorizationService)
	{
		_licenceApplicationVersion = licenceApplicationVersion ?? throw new ArgumentNullException("licenceApplicationVersion");
		_erpComptaService = erpComptaService ?? throw new ArgumentNullException("erpComptaService");
		_erpCommService = erpCommService ?? throw new ArgumentNullException("erpCommService");
		_groupeService = groupeService ?? throw new ArgumentNullException("groupeService");
		_deviseViewHelper = deviseViewHelper ?? throw new ArgumentNullException("deviseViewHelper");
		_banqueHelper = banqueHelper ?? throw new ArgumentNullException("banqueHelper");
		_declarationFileGenerator = declarationFileGenerator ?? throw new ArgumentNullException("declarationFileGenerator");
		_importService = importService ?? throw new ArgumentNullException("importService");
		_locker = locker ?? throw new ArgumentNullException("locker");
		_authorizationService = authorizationService ?? throw new ArgumentNullException("authorizationService");
	}

	public void ImporterLigneDeclaration(DeclarationTvaEncaissementView declaration, string source, IProgress<int> progress, CancellationToken cancellationToken)
	{
		if (_licenceApplicationVersion.Application == ApplicationRunning.TresoClient)
		{
			_authorizationService.HasRestriction(new DeclarationTvaMarrocGrcImporter(), ProfilType.Grc);
		}
		else
		{
			_authorizationService.HasRestriction(new DeclarationTvaMarrocGrfImporter(), ProfilType.Grf);
		}
		_importService.ImporterLigneDeclaration(declaration, source, progress, cancellationToken);
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

	public DeclarationTvaEncaissementView InitView()
	{
		SocieteManager societeManager = _groupeService.SocieteManager;
		Societe societe = societeManager.Societe;
		string numeroPieceCourante = societeManager.GetNumeroPieceCourante(EntityNumerotation.DeclarationTvaEncaissement);
		return new DeclarationTvaEncaissementView
		{
			Numero = numeroPieceCourante,
			Date = DateTime.Now,
			DateDebut = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1),
			DateFin = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month)),
			Exercice = DateTime.Now.Year,
			SocieteNo = societe.No,
			Statut = StatutDeclaration.EnCours,
			TypeDeclaration = societe.TypeDeclarationTva,
			Libelle = string.Empty,
			MoisPeriode = (DeclarationMoisPeriode)DateTime.Now.Month,
			DateComptabilisation = DateTime.Now
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

	public List<DeclarationTvaRecapView> GetRecapDeclaration(DeclarationTvaEncaissementView view)
	{
		if (view == null)
		{
			throw new ArgumentNullException("view");
		}
		SocieteManager societeManager = _groupeService.SocieteManager;
		Societe societe = societeManager.Societe;
		IEnumerable<LigneDeclarationTvaEncaissement> source = societeManager.DeclarationTvaEncaissementLigneGetAll(view.No);
		IEnumerable<LigneDeclarationTvaEncaissement> collection = source.Where((LigneDeclarationTvaEncaissement x) => !x.IsReport);
		return (from x in collection
			group x by new { x.Domaine, x.ErpTaxeCode, x.Taux } into x
			select new DeclarationTvaRecapView
			{
				SocieteNo = societe.No,
				ErpTaxeCode = x.Key.ErpTaxeCode,
				Taux = x.Key.Taux,
				Domaine = x.Key.Domaine,
				TotalAssiette = collection.Where((LigneDeclarationTvaEncaissement t) => t.Domaine == x.Key.Domaine && t.ErpTaxeCode == x.Key.ErpTaxeCode && t.Taux == x.Key.Taux).Sum((LigneDeclarationTvaEncaissement t) => t.Assiette),
				TotalTva = collection.Where((LigneDeclarationTvaEncaissement t) => t.Domaine == x.Key.Domaine && t.ErpTaxeCode == x.Key.ErpTaxeCode && t.Taux == x.Key.Taux).Sum((LigneDeclarationTvaEncaissement t) => t.Montant)
			}).ToList();
	}

	public List<DeclarationTvaCodeActiviteRecapView> GetRecapDeclarationParCodeActivite(DeclarationTvaEncaissementView view)
	{
		if (view == null)
		{
			throw new ArgumentNullException("view");
		}
		SocieteManager societeManager = _groupeService.SocieteManager;
		Societe societe = societeManager.Societe;
		IEnumerable<LigneDeclarationTvaEncaissement> source = societeManager.DeclarationTvaEncaissementLigneGetAll(view.No);
		IEnumerable<LigneDeclarationTvaEncaissement> collection = source.Where((LigneDeclarationTvaEncaissement x) => !x.IsReport);
		return (from x in collection
			group x by new { x.CodeActivite, x.Domaine, x.ErpTaxeCode, x.Taux } into x
			select new DeclarationTvaCodeActiviteRecapView
			{
				SocieteNo = societe.No,
				ErpTaxeCode = x.Key.ErpTaxeCode,
				Taux = x.Key.Taux,
				Domaine = x.Key.Domaine,
				CodeActivite = x.Key.CodeActivite,
				TotalAssiette = collection.Where((LigneDeclarationTvaEncaissement t) => t.CodeActivite == x.Key.CodeActivite && t.Domaine == x.Key.Domaine && t.ErpTaxeCode == x.Key.ErpTaxeCode && t.Taux == x.Key.Taux).Sum((LigneDeclarationTvaEncaissement t) => t.Assiette),
				TotalTva = collection.Where((LigneDeclarationTvaEncaissement t) => t.CodeActivite == x.Key.CodeActivite && t.Domaine == x.Key.Domaine && t.ErpTaxeCode == x.Key.ErpTaxeCode && t.Taux == x.Key.Taux).Sum((LigneDeclarationTvaEncaissement t) => t.Montant)
			}).ToList();
	}

	public List<DeclarationTvaEncaissementView> GetAllDeclaration()
	{
		return _groupeService.SocieteManager.DeclarationTvaEncaissementGetAll().Select(ToView).ToList();
	}

	private DeclarationTvaEncaissementView ToView(DeclarationTvaEncaissement declaration)
	{
		if (declaration == null)
		{
			throw new ArgumentNullException("declaration");
		}
		return new DeclarationTvaEncaissementView
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
			TrimestrePeriode = declaration.TrimestrePeriode,
			MoisPeriode = declaration.MoisPeriode,
			IsComptabilise = declaration.IsComptabilise,
			DateComptabilisation = declaration.DateComptabilisation
		};
	}

	public DeclarationTvaEncaissementView Get(int no)
	{
		if (no <= 0)
		{
			throw new ArgumentException("no");
		}
		DeclarationTvaEncaissement declaration = _groupeService.SocieteManager.DeclarationTvaEncaissementGet(no);
		return ToView(declaration);
	}

	public void UpdateDeclaration(DeclarationTvaEncaissementView view)
	{
		if (_licenceApplicationVersion.Application == ApplicationRunning.TresoClient)
		{
			_authorizationService.HasRestriction(new DeclarationTvaMarrocGrcModifier(), ProfilType.Grc);
		}
		else
		{
			_authorizationService.HasRestriction(new DeclarationTvaMarrocGrfModifier(), ProfilType.Grf);
		}
		if (view == null)
		{
			throw new ArgumentNullException("view");
		}
		_groupeService.SocieteManager.DeclarationTvaEncaissementUpdate(view.No, view.Libelle);
	}

	public void HasRestrictionConsultation()
	{
		if (_licenceApplicationVersion.Application == ApplicationRunning.TresoClient)
		{
			_authorizationService.HasRestriction(new DeclarationTvaMarrocGrcConsulter(), ProfilType.Grc);
		}
		else
		{
			_authorizationService.HasRestriction(new DeclarationTvaMarrocGrfConsulter(), ProfilType.Grf);
		}
	}

	public void HasRestrictionAjout()
	{
		if (_licenceApplicationVersion.Application == ApplicationRunning.TresoClient)
		{
			_authorizationService.HasRestriction(new DeclarationTvaMarrocGrcAjout(), ProfilType.Grc);
		}
		else
		{
			_authorizationService.HasRestriction(new DeclarationTvaMarrocGrfAjout(), ProfilType.Grf);
		}
	}

	public void ClotureDeclaration(DeclarationTvaEncaissementView view)
	{
		if (_licenceApplicationVersion.Application == ApplicationRunning.TresoClient)
		{
			_authorizationService.HasRestriction(new DeclarationTvaMarrocGrcCloturer(), ProfilType.Grc);
		}
		else
		{
			_authorizationService.HasRestriction(new DeclarationTvaMarrocGrfCloturer(), ProfilType.Grf);
		}
		if (view == null)
		{
			throw new ArgumentNullException("view");
		}
		_groupeService.SocieteManager.DeclarationTvaEncaissementCloture(view.No);
	}

	public void AnnulerClotureDeclaration(DeclarationTvaEncaissementView view)
	{
		if (_licenceApplicationVersion.Application == ApplicationRunning.TresoClient)
		{
			_authorizationService.HasRestriction(new DeclarationTvaMarrocGrcDecloture(), ProfilType.Grc);
		}
		else
		{
			_authorizationService.HasRestriction(new DeclarationTvaMarrocGrfDecloture(), ProfilType.Grf);
		}
		if (view == null)
		{
			throw new ArgumentNullException("view");
		}
		_groupeService.SocieteManager.DeclarationTvaEncaissementAnnulerCloture(view.No);
	}

	public void DeposeDeclaration(DeclarationTvaEncaissementView view)
	{
		if (view == null)
		{
			throw new ArgumentNullException("view");
		}
		_groupeService.SocieteManager.DeclarationTvaEncaissementDepose(view.No);
	}

	public void AnnulerGenerationFichierDeclaration(DeclarationTvaEncaissementView view)
	{
		try
		{
			if (_licenceApplicationVersion.Application == ApplicationRunning.TresoClient)
			{
				_authorizationService.HasRestriction(new DeclarationTvaMarrocGrcAnnulerGeneration(), ProfilType.Grc);
			}
			else
			{
				_authorizationService.HasRestriction(new DeclarationTvaMarrocGrfAnnulerGeneration(), ProfilType.Grf);
			}
			if (view == null)
			{
				throw new ArgumentNullException("view");
			}
			_groupeService.SocieteManager.DeclarationTvaEncaissementFichierAnnulerGeneration(view.No);
		}
		catch (Exception ex)
		{
			throw ex;
		}
	}

	public void GenererFichierDeclaration(DeclarationTvaEncaissementView view, string path, IProgress<int> progress, CancellationToken cancellationToken)
	{
		try
		{
			if (_licenceApplicationVersion.Application == ApplicationRunning.TresoClient)
			{
				_authorizationService.HasRestriction(new DeclarationTvaMarrocGrcGenerer(), ProfilType.Grc);
			}
			else
			{
				_authorizationService.HasRestriction(new DeclarationTvaMarrocGrfGenerer(), ProfilType.Grf);
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
			Log.Verbose("Déclaration TVA/ENC : [Début] Génération du fichier xml");
			_declarationFileGenerator.Generate(view.No, path, defaultDeviseSociete.NombreDecimales, progress, cancellationToken);
			Log.Verbose("Déclaration TVA/ENC : [Fin] Génération du fichier xml");
			societeManager.DeclarationTvaEncaissementFichierGenerer(view.No);
		}
		catch (Exception ex)
		{
			throw ex;
		}
	}

	public int CreateDeclaration(DeclarationTvaEncaissementView view)
	{
		if (_licenceApplicationVersion.Application == ApplicationRunning.TresoClient)
		{
			_authorizationService.HasRestriction(new DeclarationTvaMarrocGrcAjout(), ProfilType.Grc);
		}
		else
		{
			_authorizationService.HasRestriction(new DeclarationTvaMarrocGrfAjout(), ProfilType.Grf);
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
		int num = societeManager.DeclarationTvaEncaissementCreate(view.Numero, view.Exercice, view.Date, view.TypeDeclaration, view.Libelle, view.MoisPeriode, view.TrimestrePeriode);
		if (!_locker.Lock(TypeEntity.DeclarationTvaEncaissement, num, societe.No, out var lockNo))
		{
			throw new InvalidOperationException("Impossible de verouiller la déclaration.");
		}
		view.LockNo = lockNo;
		view.No = num;
		transactionScope.Complete();
		return num;
	}

	public void DeleteDeclaration(DeclarationTvaEncaissementView view)
	{
		if (_licenceApplicationVersion.Application == ApplicationRunning.TresoClient)
		{
			_authorizationService.HasRestriction(new DeclarationTvaMarrocGrcSupprimer(), ProfilType.Grc);
		}
		else
		{
			_authorizationService.HasRestriction(new DeclarationTvaMarrocGrfSupprimer(), ProfilType.Grf);
		}
		if (view == null)
		{
			throw new ArgumentNullException("view");
		}
		_groupeService.SocieteManager.DeclarationTvaEncaissementDelete(view.No);
	}

	public void DeleteLigneDeclaration(LigneDeclarationTvaEncaissementView view)
	{
		if (view == null)
		{
			throw new ArgumentNullException("view");
		}
		_groupeService.SocieteManager.DeclarationTvaEncaissementDeleteLigne(view.No);
	}

	public void ReporterLigneDeclaration(LigneDeclarationTvaEncaissementView view)
	{
		if (view == null)
		{
			throw new ArgumentNullException("view");
		}
		_groupeService.SocieteManager.DeclarationTvaEncaissementReporterLigne(view.No);
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

	public bool IsLocked(DeclarationTvaEncaissementView view)
	{
		if (view == null)
		{
			throw new ArgumentNullException("view");
		}
		Societe societe = _groupeService.SocieteManager.Societe;
		return _locker.IsLocked(TypeEntity.DeclarationTvaEncaissement, view.No, societe.No);
	}

	public List<LigneDeclarationTvaEncaissementView> GetLignesDeclaration(DeclarationTvaEncaissementView view)
	{
		if (view == null)
		{
			throw new ArgumentNullException("view");
		}
		return _groupeService.SocieteManager.DeclarationTvaEncaissementLigneGetAll(view.No).Select(ToView).ToList();
	}

	private LigneDeclarationTvaEncaissementView ToView(LigneDeclarationTvaEncaissement ligne)
	{
		if (ligne == null)
		{
			throw new ArgumentNullException("ligne");
		}
		return new LigneDeclarationTvaEncaissementView
		{
			No = ligne.No,
			DeclarationNo = ligne.DeclarationNo,
			EntityNo = ligne.EntityNo,
			Type = ligne.EntityType,
			IsReport = ligne.IsReport,
			MouvementNumero = ligne.MouvementNumero,
			DocumentNumero = ligne.DocumentNumero,
			Assiette = ligne.Assiette,
			Taux = ligne.Taux,
			Montant = ligne.Montant,
			Domaine = ligne.Domaine,
			TiersCode = ligne.TiersCode,
			TiersIntitule = ligne.TiersIntitule,
			TiersIdentifiant = ligne.TiersIdentifiant,
			TiersIce = ligne.TiersIce,
			DateMouvement = ligne.DateMouvement,
			Integration = ligne.TypeIntegration,
			TypePayement = ligne.TypePayement,
			ErpTaxeCode = ligne.ErpTaxeCode,
			CodeActivite = ligne.CodeActivite,
			Prorata = ligne.Prorata,
			DateDocument = ligne.DateDocument,
			DesignationDocument = ligne.DesignationDocument
		};
	}

	public void GenerateCSVFile(string path)
	{
		CsvGenerator.GeneratModel<LigneDeclarationTvaEncaissementImport, LigneDeclarationTvaEncaissementImportMap>(path);
	}
}
