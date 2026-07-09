using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Interfaces;
using Tresorerie.Core.Models;
using Tresorerie.Infrastructure;
using Tresorerie.Infrastructure.Helpers;
using Tresorerie.Ressources;

namespace Tresorerie.Core.Services;

public class SocieteManager
{
	private readonly IAlimentationCaisseRepository _alimentationCaisseRepository;

	private readonly IAutorisationCaisseRepository _autorisationCaisseRepository;

	private readonly IAutorisationSoucheRepository _autorisationSoucheRepository;

	private readonly IBanqueTiersRepository _banqueTiersRepository;

	private readonly CaisseManager _caisseManager;

	private readonly WorkerManager _workerManager;

	private readonly DossierImpayeManager _dossierImpayeManager;

	private readonly ICautionRepository _cautionRepository;

	private readonly IChequeRepository _chequeRepository;

	private readonly IChequeEntityRepository _chequeEntityRepository;

	private readonly IChequierRepository _chequierRepository;

	private readonly IDetailHistoriqueRappelEcheanceRepository _detailHistoriqueRappelEcheanceRepository;

	private readonly IDossierReglementRepository _dossierReglementRepository;

	private readonly IEcartEchangeRepository _ecartEchangeRepository;

	private readonly IEcartRepository _ecartRepository;

	private readonly IEcheanceRepository _echeanceRepository;

	private readonly IEcritureComptaRepository _ecritureCompta;

	private readonly EcritureComptaFrsManager _ecritureComptaFrsManager;

	private readonly IEngagementClientRepository _engagementClientRepository;

	private readonly IEngagementFournisseurRepository _engagementFournisseurRepository;

	private readonly IEngagementClientDetailsRepository _engagementClientDetailsRepository;

	private readonly IGridLayoutFilterRepository _gridLayoutFilterRepository;

	private readonly IGridLayoutRepository _gridLayoutRepository;

	private readonly HistoriqueMvtManager _historiqueMvtManager;

	private readonly ILigneDossierReglementRepository _ligneDossierReglementRepository;

	private readonly ILigneEngagementClientRepository _ligneEngagementClientRepository;

	private readonly ILigneRappelRepository _ligneRappelRepository;

	private readonly IMouvementEscompteRepository _mouvementEscompteRepositry;

	private readonly IMouvmentBancaireRepository _mvtBancaireRepository;

	private readonly INoteRepository _noteRepository;

	private readonly IVentilationAnalytiqueRepository _ventilationAnalytiqueRepository;

	private readonly NotifyService _notifyService;

	private readonly IRappelRepository _rappelRepository;

	private readonly IRecapTiersRepository _recapTiersRepository;

	private readonly ISocieteDeviseRepository _societeDeviseRepository;

	private readonly ISocieteImpressionRepository _societeImpressionRepository;

	private readonly ISocieteModeReglementRepository _societeModeRepository;

	private readonly ISocieteRepository _societeRepository;

	private readonly ISocieteSoucheRepository _societeSoucheRepository;

	private readonly ISocieteUtilisateurRepository _societeUtilisateurRepository;

	private readonly ISocTypeBordereauRepository _socTypeBordereauRepository;

	private readonly ISolvabiliteClientRepository _solvabiliteClientRepository;

	private readonly ISolvabiliteFournisseurRepository _solvabiliteFournisseurRepository;

	private readonly TransfertManager _transfertManager;

	private readonly ITypeCautionRepository _typeCautionRepository;

	private readonly IUtilisateurGridRepository _utilisateurGridRepository;

	private readonly VerifySoldeManager _verifySoldeManager;

	private readonly ILigneDossierReglementCommRepository _ligneDossierReglementCommRepository;

	private readonly IReglementClientRepository _reglementClientRepository;

	private readonly IReglementFournisseurRepository _reglementFournisseurRepository;

	private readonly IReleveClientRepository _releveClientRepository;

	private readonly IFactureRepository _factureRepository;

	private readonly IBalanceAgeeRepository _balanceAgeeRepository;

	private readonly IInformationsBanqueRepository _informationsBanqueRepository;

	private readonly IParametreMailRepository _parametreMailRepository;

	private readonly ILicenceApplicationVersion _licenceApplicationVersion;

	private readonly IFourchetteCommissionRepository _fourchetteCommissionRepository;

	private readonly IInformationLibreRepository _informationLibreRepository;

	private readonly IDeclarationTvaEncaissementRepository _declarationTvaEncaissementRepository;

	private readonly IDeclarationRetenuSourceRepository _declarationRetenuSourceRepository;

	private readonly ILigneDeclarationTvaEncaissementRepository _ligneDeclarationTvaEncaissementRepository;

	private readonly ILettreRecouvrementFieldsRepository _lettreRecouvrementFieldsRepository;

	private readonly ISocieteCodeActiviteTaxeRepository _societeCodeActiviteTaxeRepository;

	private readonly IAttestationRetenueTiersRepository _attestationRetenueTiersRepository;

	private readonly ISocieteDesignationDocumentRepository _societeDesignationDocumentRepository;

	private readonly ILigneDossierEcheanceTvaNonRecuperableRepository _ligneDossierEcheanceTvaNonRecuperableRepository;

	private readonly ICarnetTraiteRepository _carnetTraiteRepository;

	private readonly ITraiteRepository _traiteRepository;

	private readonly IRetenuALaSourceRepository _retenuALaSourceRepository;

	private readonly ILigneDeclarationRetenuSourceRepository _ligneDeclarationRetenuSourceRepository;

	private readonly ITiersServiceContactRepository _tiersServiceContactRepository;

	private readonly IConfigConnectionErpExternRepository _configConnectionErpExternRepository;

	private readonly IJoursReposRepository _joursReposRepository;

	private readonly IConventionDelaisPaiementTiersRepository _conventionDelaisPaiementTiersRepository;

	private readonly ISocieteCodeActiviteTiersRepository _societeCodeActiviteTiersRepository;

	private readonly IDeclarationDelaisPaiementRepository _declarationDelaisPaiementRepository;

	private readonly ILigneDeclarationDelaisPaiementRepository _ligneDeclarationDelaisPaiementRepository;

	private readonly ITypeCreditRepository _typeCreditRepository;

	private readonly IGarentieRepository _garentieRepository;

	private readonly IInformationLibreGrfRepository _informationLibreGrfRepository;

	private readonly IWorkflowHistoryRepository _workflowHistoryRepository;

	public CaisseManager CaisseManager
	{
		get
		{
			if (_caisseManager.SocieteManager == null)
			{
				_caisseManager.SocieteManager = this;
			}
			return _caisseManager;
		}
	}

	public WorkerManager WorkerManager
	{
		get
		{
			if (_workerManager.SocieteManager == null)
			{
				_workerManager.SocieteManager = this;
			}
			return _workerManager;
		}
	}

	public DossierImpayeManager DossierImpayeManager
	{
		get
		{
			if (_dossierImpayeManager.SocieteManager == null)
			{
				_dossierImpayeManager.SocieteManager = this;
			}
			return _dossierImpayeManager;
		}
	}

	public EcritureComptaFrsManager EcritureComptaFrsManager
	{
		get
		{
			if (_ecritureComptaFrsManager.SocieteManager == null)
			{
				_ecritureComptaFrsManager.SocieteManager = this;
			}
			return _ecritureComptaFrsManager;
		}
	}

	public IErpExercice Exercice { get; private set; }

	public GroupeService Groupe { get; set; }

	public HistoriqueMvtManager HistoriqueMvtManager
	{
		get
		{
			if (_historiqueMvtManager.SocieteManager == null)
			{
				_historiqueMvtManager.SocieteManager = this;
			}
			return _historiqueMvtManager;
		}
	}

	public Societe Societe { get; private set; }

	public TransfertManager TransfertManager
	{
		get
		{
			if (_transfertManager.SocieteManager == null)
			{
				_transfertManager.SocieteManager = this;
			}
			return _transfertManager;
		}
	}

	public Utilisateur Utilisateur { get; private set; }

	public VerifySoldeManager VerifySoldeManager
	{
		get
		{
			if (_verifySoldeManager.SocieteManager == null)
			{
				_verifySoldeManager.SocieteManager = this;
			}
			return _verifySoldeManager;
		}
	}

	public Task<IEnumerable<AttestationRetenueTiers>> AttestationRetenueTiersGetAllAsync()
	{
		return _attestationRetenueTiersRepository.GetAllAsync(Societe.No);
	}

	public Task<IEnumerable<AttestationRetenueTiers>> AttestationRetenueTiersGetAllAsync(int tiersNo)
	{
		return _attestationRetenueTiersRepository.GetAllAsync(Societe.No, tiersNo);
	}

	public Task<AttestationRetenueTiers> AttestationRetenueTiersGetAsync(int no)
	{
		return _attestationRetenueTiersRepository.GetAsync(no);
	}

	public async Task<int> AttestationRetenueTiersCreateAsync(int tiersNo, string tiersCode, DateTime date, string numero, DateTime dateDebut, DateTime dateFin, string codeVerification, int annee, bool isEnRegle, string fileName = "", byte[] file = null)
	{
		if (tiersNo <= 0)
		{
			throw new ArgumentNullException("tiersNo");
		}
		if (string.IsNullOrEmpty(tiersCode))
		{
			throw new ArgumentNullException("tiersCode");
		}
		if (string.IsNullOrEmpty(numero))
		{
			throw new ArgumentNullException("numero");
		}
		if (!string.IsNullOrEmpty(fileName) && file == null)
		{
			throw new ArgumentNullException("file");
		}
		if (string.IsNullOrEmpty(codeVerification))
		{
			throw new ApplicationException("codeVerification");
		}
		if (await _attestationRetenueTiersRepository.GetAsync(Societe.No, tiersNo, numero) != null)
		{
			throw new ApplicationException("L'attestation [" + numero + "] existe déja.");
		}
		if (annee < 2000 || annee > 2100)
		{
			throw new ApplicationException("Annee invalide.");
		}
		if (dateFin.Date < dateDebut.Date)
		{
			throw new ApplicationException("La date fin doit être supérieure ou égale à la date début.");
		}
		AttestationRetenueTiers attestationRetenueTiers = new AttestationRetenueTiers
		{
			Numero = numero,
			SocieteNo = Societe.No,
			DateDebut = dateDebut.Date,
			DateFin = dateFin.Date,
			TiersNo = tiersNo,
			Date = date,
			FileName = fileName,
			FilePdf = file,
			TiersCode = tiersCode,
			CodeVerification = codeVerification,
			IsEnRegle = isEnRegle,
			Annee = annee
		};
		int num = await _attestationRetenueTiersRepository.CreateAsync(attestationRetenueTiers);
		_notifyService.Notify(TypeEntity.AttestationRetenueFournisseur, num, TypeAction.Ajout, Societe.No);
		return num;
	}

	public async Task AttestationRetenueTiersDeleteAsync(int no)
	{
		AttestationRetenueTiers attestation = await _attestationRetenueTiersRepository.GetAsync(no);
		if (attestation == null)
		{
			throw new ApplicationException("Impossible de charger l'attestation.");
		}
		if (await _attestationRetenueTiersRepository.IsAttestationUsedAsync(no))
		{
			throw new ApplicationException("L'attestation [" + attestation.Numero + "] est attachée à un ou plusieurs dossiers de règlement fournisseur.");
		}
		await _attestationRetenueTiersRepository.DeleteAsync(attestation);
		_notifyService.Notify(TypeEntity.AttestationRetenueFournisseur, attestation.No, TypeAction.Suppression, Societe.No);
	}

	public List<SocieteDesignationDocument> SocieteDesignationDocumentGetAll(Societe societe = null)
	{
		societe = societe ?? Societe;
		return _societeDesignationDocumentRepository.GetAll(societe.No);
	}

	public List<SocieteCodeActiviteTiers> SocieteCodeActiviteTiersGetAll(Societe societe = null)
	{
		societe = societe ?? Societe;
		return _societeCodeActiviteTiersRepository.GetAll(societe.No);
	}

	public void SocieteDesignationDocumentCreate(int designationDocumentNo, string erpIntitule)
	{
		DesignationDocument designationDocument = Groupe.DesignationDocumentManager.Get(designationDocumentNo);
		if (designationDocument == null)
		{
			throw new ApplicationException("Impossible de charger la désignation.");
		}
		if (string.IsNullOrEmpty(erpIntitule))
		{
			throw new ApplicationException("Erp intitule invalide.");
		}
		if (_societeDesignationDocumentRepository.IsErpIntituleMapped(Societe.No, erpIntitule))
		{
			throw new ApplicationException("La désignation [" + erpIntitule + "] existe déja.");
		}
		if (_societeDesignationDocumentRepository.Exist(Societe.No, designationDocumentNo, erpIntitule))
		{
			throw new ApplicationException("La désignation [" + designationDocument.Code + "] existe déja.");
		}
		SocieteDesignationDocument societeDesignationDocument = new SocieteDesignationDocument
		{
			DesignationDocumentNo = designationDocumentNo,
			ErpIntitule = erpIntitule,
			SocieteNo = Societe.No
		};
		_societeDesignationDocumentRepository.Create(societeDesignationDocument);
		Societe.RefreshSocDesignationDocument();
	}

	public void SocieteCodeActiviteTiersCreate(int codeActiviteTiersNo, string erpIntitule)
	{
		CodeActivite codeActivite = Groupe.CodeActiviteManager.Get(codeActiviteTiersNo);
		if (codeActivite == null)
		{
			throw new ApplicationException("Impossible de charger le code activité.");
		}
		if (string.IsNullOrEmpty(erpIntitule))
		{
			throw new ApplicationException("Erp intitule invalide.");
		}
		if (_societeCodeActiviteTiersRepository.IsErpIntituleMapped(Societe.No, erpIntitule))
		{
			throw new ApplicationException("Le code activité [" + erpIntitule + "] existe déja.");
		}
		if (_societeCodeActiviteTiersRepository.Exist(Societe.No, codeActiviteTiersNo, erpIntitule))
		{
			throw new ApplicationException("Le code activité [" + codeActivite.Code + "] existe déja.");
		}
		SocieteCodeActiviteTiers societeCodeActiviteTiers = new SocieteCodeActiviteTiers
		{
			CodeActiviteNo = codeActiviteTiersNo,
			ErpIntitule = erpIntitule,
			SocieteNo = Societe.No
		};
		_societeCodeActiviteTiersRepository.Create(societeCodeActiviteTiers);
		Societe.RefreshSocCodeActiviteTiers();
	}

	public void SocieteDesignationDocumentDelete(int no)
	{
		SocieteDesignationDocument societeDesignationDocument = _societeDesignationDocumentRepository.Get(no);
		if (societeDesignationDocument == null)
		{
			throw new ApplicationException("Impossible de charger le mappage désignation document.");
		}
		_societeDesignationDocumentRepository.Delete(societeDesignationDocument);
		Societe.RefreshSocDesignationDocument();
	}

	public void SocieteCodeActiviteTiersDelete(int no)
	{
		SocieteCodeActiviteTiers societeCodeActiviteTiers = _societeCodeActiviteTiersRepository.Get(no);
		if (societeCodeActiviteTiers == null)
		{
			throw new ApplicationException("Impossible de charger le mappage code activité.");
		}
		_societeCodeActiviteTiersRepository.Delete(societeCodeActiviteTiers);
		Societe.RefreshSocCodeActiviteTiers();
	}

	public bool DesignationDocumentErpIntituleIsMapped(string erpIntitule, Societe societe = null)
	{
		return _societeDesignationDocumentRepository.IsErpIntituleMapped((societe ?? Societe).No, erpIntitule);
	}

	public bool CodeActiviteTiersErpIntituleIsMapped(string erpIntitule, Societe societe = null)
	{
		return _societeCodeActiviteTiersRepository.IsErpIntituleMapped((societe ?? Societe).No, erpIntitule);
	}

	public List<LigneDossierEcheanceTvaNonRecuperable> LigneDossierEcheanceTvaGetAll(int echeanceNo)
	{
		return _ligneDossierEcheanceTvaNonRecuperableRepository.GetAllByEcheance(echeanceNo);
	}

	public void LigneDossierEcheanceTvaCreate(int echeanceNo, int retenueNo, string codeTaxe, decimal montantBase, decimal tauxTaxe, decimal montantTva)
	{
		Echeance echeance = _echeanceRepository.Get(echeanceNo);
		if (echeance == null)
		{
			throw new ArgumentNullException("Impossible de charger l'échéance.");
		}
		if ((echeance.Type != EcheanceType.Erp && (echeance.Type != EcheanceType.Solde || echeance.IsSpe != 0)) || echeance.Domaine != ErpDomaine.Achat)
		{
			throw new ApplicationException("Type d'échéance invalide.");
		}
		RetenuALaSource retenuALaSource = CaisseManager.RetenueGet(retenueNo);
		if (retenuALaSource == null)
		{
			throw new ArgumentNullException("Impossible de charger la retenue.");
		}
		if (!retenuALaSource.RetenuePourEcheanceNo.HasValue || retenuALaSource.RetenuePourEcheanceNo.Value != echeanceNo)
		{
			throw new ApplicationException("La retenue [" + retenuALaSource.Numero + "] n'appartient pas à l'échéance [" + echeance.DocumentNumero + "].");
		}
		if (string.IsNullOrEmpty(codeTaxe))
		{
			throw new ArgumentNullException("Code taxe invalide.");
		}
		if (tauxTaxe <= 0m || tauxTaxe > 100m)
		{
			throw new ApplicationException("Le taux de taxe est invalide.");
		}
		if (montantTva <= 0m || montantTva > echeance.Montant)
		{
			throw new ApplicationException("Le montant de la TVA est invalide.");
		}
		if (montantBase <= 0m || montantBase > echeance.Montant)
		{
			throw new ApplicationException("La base de calcule est invalide.");
		}
		if (_ligneDossierEcheanceTvaNonRecuperableRepository.Get(echeanceNo, retenueNo, codeTaxe) != null)
		{
			throw new ApplicationException("Ligne TVA [" + codeTaxe + "] existe déja.");
		}
		LigneDossierEcheanceTvaNonRecuperable ligne = new LigneDossierEcheanceTvaNonRecuperable
		{
			CodeTaxe = codeTaxe,
			BaseTva = montantBase,
			EcheanceNo = echeanceNo,
			ErpDocumentNumero = echeance.DocumentNumero,
			ErpDocumentType = echeance.DocumentType,
			MontantTva = montantTva,
			RetenueNo = retenueNo,
			SocieteNo = Societe.No,
			TauxTaxe = tauxTaxe
		};
		_ligneDossierEcheanceTvaNonRecuperableRepository.Create(ligne);
	}

	public LigneDeclarationRetenuSource LigneDeclarationRetenuSourceGet(int no)
	{
		return _ligneDeclarationRetenuSourceRepository.Get(no);
	}

	public LigneDeclarationRetenuSource LigneDeclarationRetenuSourceGet(int retenueNo, int mouvementNo)
	{
		return _ligneDeclarationRetenuSourceRepository.Get(retenueNo, mouvementNo);
	}

	public decimal SocieteGetDelaisMoyenPaiement(ErpDomaine domaine, DateTime dateDu, DateTime dateAu, Societe societe = null)
	{
		societe = societe ?? Societe;
		return _societeRepository.GetDelaisMoyenPaiement(societe.No, domaine, dateDu, dateAu);
	}

	public Dictionary<DateTime, decimal> SocieteGetDetailsDelaisMoyenPaiement(ErpDomaine domaine, DateTime dateDu, DateTime dateAu, Societe societe = null)
	{
		societe = societe ?? Societe;
		return _societeRepository.GetDetailsDelaisMoyenPaiement(societe.No, domaine, dateDu, dateAu);
	}

	public TypeCredit TypeCreditGet(int no)
	{
		return _typeCreditRepository.Get(no);
	}

	public Garantie GarentieGet(int no)
	{
		return _garentieRepository.Get(no);
	}

	public TypeCredit TypeCreditGet(string code, Societe societe = null)
	{
		societe = societe ?? Societe;
		return _typeCreditRepository.Get(societe.No, code);
	}

	public Garantie GarentieGet(string code, Societe societe = null)
	{
		societe = societe ?? Societe;
		return _garentieRepository.Get(societe.No, code);
	}

	public List<TypeCredit> TypeCreditGetAll(Societe societe = null)
	{
		societe = societe ?? Societe;
		return _typeCreditRepository.GetAll(societe.No);
	}

	public List<Garantie> GarentieGetAll(Societe societe = null)
	{
		societe = societe ?? Societe;
		return _garentieRepository.GetAll(societe.No);
	}

	public JoursRepos JoursReposGet(int no)
	{
		return _joursReposRepository.Get(no);
	}

	public JoursRepos JoursReposGet(DateTime date, Societe societe = null)
	{
		societe = societe ?? Societe;
		return _joursReposRepository.Get(societe.No, date);
	}

	public List<JoursRepos> JoursReposGetAll(Societe societe = null)
	{
		societe = societe ?? Societe;
		return _joursReposRepository.GetAll(societe.No);
	}

	public int TypeCreditCreate(TypeCredit typeCredit)
	{
		if (string.IsNullOrEmpty(typeCredit.Code))
		{
			throw new ArgumentNullException("Code");
		}
		if (string.IsNullOrEmpty(typeCredit.Intitule))
		{
			throw new ArgumentNullException("Intitule");
		}
		if (TypeCreditGet(typeCredit.Code) != null)
		{
			throw new ApplicationException("Le type de crédit [" + typeCredit.Code + "] existe déjà.");
		}
		typeCredit.SocieteNo = Societe.No;
		typeCredit.DateCreation = DateTime.Today;
		typeCredit.UtilisateurNo = Utilisateur.No;
		return _typeCreditRepository.Create(typeCredit);
	}

	public int GarentieCreate(Garantie garentie)
	{
		if (string.IsNullOrEmpty(garentie.Code))
		{
			throw new ArgumentNullException("Code");
		}
		if (string.IsNullOrEmpty(garentie.Intitule))
		{
			throw new ArgumentNullException("Intitule");
		}
		if (GarentieGet(garentie.Code) != null)
		{
			throw new ApplicationException("La garantie [" + garentie.Code + "] existe déjà.");
		}
		garentie.SocieteNo = Societe.No;
		garentie.DateCreation = DateTime.Today;
		garentie.UtilisateurNo = Utilisateur.No;
		return _garentieRepository.Create(garentie);
	}

	public void TypeCreditUpdate(TypeCredit type)
	{
		if (string.IsNullOrEmpty(type.Intitule))
		{
			throw new ArgumentNullException("Intitule");
		}
		TypeCredit typeCredit = TypeCreditGet(type.No) ?? throw new ApplicationException("Impossible de charger le type de crédit.");
		typeCredit.Intitule = type.Intitule;
		typeCredit.MethodeCalcul = type.MethodeCalcul;
		typeCredit.ConventionCalcul = type.ConventionCalcul;
		_typeCreditRepository.Update(typeCredit);
	}

	public void GarentieUpdate(int no, string intitule)
	{
		if (string.IsNullOrEmpty(intitule))
		{
			throw new ArgumentNullException("intitule");
		}
		Garantie garantie = GarentieGet(no) ?? throw new ApplicationException("Impossible de charger la garantie.");
		garantie.Intitule = intitule;
		_garentieRepository.Update(garantie);
	}

	public int JoursReposCreate(string intitule, DateTime date)
	{
		if (string.IsNullOrEmpty(intitule))
		{
			throw new ArgumentNullException("intitule");
		}
		if (date.Date.Year < 1900)
		{
			throw new ApplicationException("Date invalide.");
		}
		if (_joursReposRepository.Get(Societe.No, date.Date) != null)
		{
			throw new ApplicationException("Le jour de repos [" + date.Date.ToShortDateString() + "] existe déjà.");
		}
		return _joursReposRepository.Create(new JoursRepos
		{
			Intitule = intitule,
			Date = date.Date,
			SocieteNo = Societe.No
		});
	}

	public void JoursReposUpdate(int no, string intitule, DateTime date)
	{
		if (string.IsNullOrEmpty(intitule))
		{
			throw new ArgumentNullException("intitule");
		}
		if (date.Date.Year < 1900)
		{
			throw new ApplicationException("Date invalide.");
		}
		JoursRepos joursRepos = _joursReposRepository.Get(no);
		if (joursRepos == null)
		{
			throw new ApplicationException("Impossible de charger le jour de repos.");
		}
		JoursRepos joursRepos2 = _joursReposRepository.Get(Societe.No, date.Date);
		if (joursRepos2 != null && joursRepos2.No != no)
		{
			throw new ApplicationException("Le jour de repos [" + date.Date.ToShortDateString() + "] existe déjà.");
		}
		joursRepos.Intitule = intitule;
		joursRepos.Date = date.Date;
		_joursReposRepository.Update(joursRepos);
	}

	public void TypeCreditDelete(int no)
	{
		TypeCredit typeCredit = _typeCreditRepository.Get(no);
		if (typeCredit == null)
		{
			throw new ApplicationException("Impossible de charger le type de crédit.");
		}
		if (Groupe.CreditManager.IsCreditUtiliseType(no))
		{
			throw new ApplicationException("Le type de crédit est utilisé.");
		}
		_typeCreditRepository.Delete(typeCredit);
	}

	public void GarentieDelete(int no)
	{
		Garantie garantie = _garentieRepository.Get(no);
		if (garantie == null)
		{
			throw new ApplicationException("Impossible de charger le garentie.");
		}
		if (Groupe.CreditManager.IsCreditUtiliseGarentie(no))
		{
			throw new ApplicationException("Le garentie est utilisé.");
		}
		_garentieRepository.Delete(garantie);
	}

	public void JoursReposDelete(int no)
	{
		JoursRepos joursRepos = _joursReposRepository.Get(no);
		if (joursRepos == null)
		{
			throw new ApplicationException("Impossible de charger le jour de repos.");
		}
		_joursReposRepository.Delete(joursRepos);
	}

	public IEnumerable<ConventionDelaisPaiementTiers> ConventionDelaisPaiementTiersGetAll(DomaineConvention domaine)
	{
		return _conventionDelaisPaiementTiersRepository.GetAll(Societe.No, domaine);
	}

	public IEnumerable<ConventionDelaisPaiementTiers> ConventionDelaisPaiementTiersGetAll(int tiersNo, DomaineConvention domaine)
	{
		return _conventionDelaisPaiementTiersRepository.GetAll(Societe.No, tiersNo, domaine);
	}

	public ConventionDelaisPaiementTiers ConventionDelaisPaiementTiersGet(int no)
	{
		return _conventionDelaisPaiementTiersRepository.Get(no);
	}

	public int ConventionDelaisPaiementTiersCreate(int tiersNo, string tiersCode, DateTime date, string numero, DateTime? dateDebut, DateTime? dateFin, int delaisPaiement, DomaineConvention domaine, TypeConvention typeConvention, int? factureNo, string fileName = "", byte[] file = null)
	{
		if (tiersNo <= 0)
		{
			throw new ArgumentNullException("tiersNo");
		}
		if (string.IsNullOrEmpty(tiersCode))
		{
			throw new ArgumentNullException("tiersCode");
		}
		if (string.IsNullOrEmpty(numero))
		{
			throw new ArgumentNullException("numero");
		}
		if (!factureNo.HasValue && typeConvention == TypeConvention.Facture)
		{
			throw new ArgumentNullException("numero");
		}
		if (!string.IsNullOrEmpty(fileName) && file == null)
		{
			throw new ArgumentNullException("file");
		}
		if (delaisPaiement <= 0)
		{
			throw new ApplicationException("delaisPaiement");
		}
		if (delaisPaiement > 180)
		{
			throw new ApplicationException("Le délai de paiement ne doit pas dépasser 180 jours.");
		}
		if (_conventionDelaisPaiementTiersRepository.Get(Societe.No, tiersNo, numero, domaine) != null)
		{
			throw new ApplicationException("La convention [" + numero + "] existe déja.");
		}
		IEnumerable<ConventionDelaisPaiementTiers> all = _conventionDelaisPaiementTiersRepository.GetAll(Societe.No, tiersNo, domaine);
		if (typeConvention == TypeConvention.Convention)
		{
			if (!dateDebut.HasValue)
			{
				throw new ApplicationException("La date début invalide.");
			}
			if (!dateFin.HasValue)
			{
				throw new ApplicationException("La date fin invalide.");
			}
			if (dateFin.Value.Date < dateDebut.Value.Date)
			{
				throw new ApplicationException("La date fin doit être supérieure ou égale à la date début.");
			}
			if (all.Any((ConventionDelaisPaiementTiers x) => x.DateDebut.HasValue && x.DateFin.HasValue && x.DateDebut.Value.Date <= dateDebut.Value.Date && x.DateFin.Value.Date >= dateDebut.Value.Date))
			{
				throw new ApplicationException("Il existe une convention pour la même période.");
			}
		}
		else
		{
			if (!factureNo.HasValue)
			{
				throw new ApplicationException("Le numéro de facture est obligatoire.");
			}
			ErpDomaine domaine2 = ((domaine != DomaineConvention.Client) ? ErpDomaine.Achat : ErpDomaine.Vente);
			if (EcheanceGetAll(domaine2, tiersNo, Etat.NonPaye)?.FirstOrDefault((Echeance x) => x.No == factureNo.Value) == null)
			{
				throw new ApplicationException("Impossible de charger la facture");
			}
			if (all.Any((ConventionDelaisPaiementTiers x) => x.FactureNo.HasValue && x.FactureNo == factureNo))
			{
				throw new ApplicationException("Il existe une convention pour la même facture.");
			}
		}
		ConventionDelaisPaiementTiers convention = new ConventionDelaisPaiementTiers
		{
			Numero = numero,
			SocieteNo = Societe.No,
			DateDebut = dateDebut?.Date,
			DateFin = dateFin?.Date,
			TiersNo = tiersNo,
			Date = date,
			FileName = fileName,
			FilePdf = file,
			TiersCode = tiersCode,
			Domaine = domaine,
			NombreJoursDelaisPaiement = delaisPaiement,
			FactureNo = factureNo,
			Type = typeConvention
		};
		int num = _conventionDelaisPaiementTiersRepository.Create(convention);
		_notifyService.Notify(TypeEntity.ConventionDelaisPaiementTiers, num, TypeAction.Ajout, Societe.No);
		return num;
	}

	public void ConventionDelaisPaiementTiersDelete(int no)
	{
		ConventionDelaisPaiementTiers conventionDelaisPaiementTiers = _conventionDelaisPaiementTiersRepository.Get(no);
		if (conventionDelaisPaiementTiers == null)
		{
			throw new ApplicationException("Impossible de charger la convention.");
		}
		_conventionDelaisPaiementTiersRepository.Delete(conventionDelaisPaiementTiers);
		_notifyService.Notify(TypeEntity.ConventionDelaisPaiementTiers, conventionDelaisPaiementTiers.No, TypeAction.Suppression, Societe.No);
	}

	public void ConventionDelaisPaiementTiersTerminer(int no, DateTime dateFin)
	{
		ConventionDelaisPaiementTiers conventionDelaisPaiementTiers = _conventionDelaisPaiementTiersRepository.Get(no);
		if (conventionDelaisPaiementTiers == null)
		{
			throw new ApplicationException("Impossible de charger la convention.");
		}
		if (conventionDelaisPaiementTiers.Type != TypeConvention.Convention)
		{
			throw new ApplicationException("Type convention invalide.");
		}
		if (!conventionDelaisPaiementTiers.DateDebut.HasValue)
		{
			throw new ApplicationException("La date début invalide.");
		}
		if (!conventionDelaisPaiementTiers.DateFin.HasValue)
		{
			throw new ApplicationException("La date fin invalide.");
		}
		if (dateFin.Date < conventionDelaisPaiementTiers.DateDebut.Value.Date)
		{
			throw new ApplicationException("La date fin doit être supérieure ou égale à la date début.");
		}
		if (dateFin.Date > conventionDelaisPaiementTiers.DateFin.Value.Date)
		{
			throw new ApplicationException("La date fin doit être inférieure ou égale à la date fin.");
		}
		conventionDelaisPaiementTiers.DateFin = dateFin.Date;
		_conventionDelaisPaiementTiersRepository.Update(conventionDelaisPaiementTiers);
		_notifyService.Notify(TypeEntity.ConventionDelaisPaiementTiers, conventionDelaisPaiementTiers.No, TypeAction.Modification, Societe.No);
	}

	public IEnumerable<DeclarationDelaisPaiementTiers> DeclarationDelaisPaiementGetAll()
	{
		return _declarationDelaisPaiementRepository.GetAll(Societe.No);
	}

	public DeclarationDelaisPaiementTiers DeclarationDelaisPaiementGet(int no)
	{
		return _declarationDelaisPaiementRepository.Get(no);
	}

	public int DeclarationDelaisPaiementCreate(string numero, int exercice, DateTime date, TypeDeclarationDelaisPaiement type, string libelle, DeclarationTvaEncaissementTrimestrePeriode trimestrePeriode)
	{
		if (string.IsNullOrEmpty(numero))
		{
			throw new ArgumentNullException("numero");
		}
		if (_declarationDelaisPaiementRepository.Get(Societe.No, numero) != null)
		{
			throw new ApplicationException("La déclaration [" + numero + "] existe déja.");
		}
		DateTime dateDebut = default(DateTime);
		DateTime dateFin = default(DateTime);
		switch (type)
		{
		case TypeDeclarationDelaisPaiement.Annuelle:
			dateDebut = new DateTime(exercice, 1, 1);
			dateFin = new DateTime(exercice, 12, 31);
			break;
		case TypeDeclarationDelaisPaiement.Trimestrielle:
			switch (trimestrePeriode)
			{
			case DeclarationTvaEncaissementTrimestrePeriode.T1:
				dateDebut = new DateTime(exercice, 1, 1);
				dateFin = new DateTime(exercice, 3, 31);
				break;
			case DeclarationTvaEncaissementTrimestrePeriode.T2:
				dateDebut = new DateTime(exercice, 4, 1);
				dateFin = new DateTime(exercice, 6, 30);
				break;
			case DeclarationTvaEncaissementTrimestrePeriode.T3:
				dateDebut = new DateTime(exercice, 7, 1);
				dateFin = new DateTime(exercice, 9, 30);
				break;
			case DeclarationTvaEncaissementTrimestrePeriode.T4:
				dateDebut = new DateTime(exercice, 10, 1);
				dateFin = new DateTime(exercice, 12, 31);
				break;
			default:
				throw new ApplicationException("Trimestre invalide.");
			}
			break;
		default:
			throw new ApplicationException("Type déclaration invalide.");
		}
		if (_declarationDelaisPaiementRepository.Get(Societe.No, exercice, dateDebut, dateFin) != null)
		{
			throw new ApplicationException("Déclaration existe déja pour la même période.");
		}
		if (_declarationDelaisPaiementRepository.GetAll(Societe.No, exercice).Any((DeclarationDelaisPaiementTiers x) => x.DateDebut <= dateDebut && x.DateFin >= dateFin))
		{
			throw new ApplicationException("Période de déclaration existe déja.");
		}
		DeclarationDelaisPaiementTiers declaration = new DeclarationDelaisPaiementTiers
		{
			Numero = numero,
			SocieteNo = Societe.No,
			Statut = StatutDeclaration.EnCours,
			TypeDeclaration = type,
			Date = date,
			Exercice = exercice,
			CreateurNo = Utilisateur.No,
			DateCreation = DateTime.Now,
			DateDebut = dateDebut.Date,
			DateFin = dateFin.Date.AddDays(1.0).AddSeconds(-1.0),
			ModificateurNo = Utilisateur.No,
			DateModification = DateTime.Now,
			IsDepose = false,
			Libelle = libelle,
			IsFichierGenerer = false,
			TrimestrePeriode = trimestrePeriode
		};
		int num = _declarationDelaisPaiementRepository.Create(declaration);
		_notifyService.Notify(TypeEntity.DeclarationDelaisPaiement, num, TypeAction.Ajout, Societe.No);
		return num;
	}

	public void DeclarationDelaisPaiementUpdate(int no, string libelle)
	{
		DeclarationDelaisPaiementTiers declarationDelaisPaiementTiers = _declarationDelaisPaiementRepository.Get(no);
		if (declarationDelaisPaiementTiers == null)
		{
			throw new ApplicationException("Impossible de charger la déclaration.");
		}
		if (declarationDelaisPaiementTiers.IsDepose)
		{
			throw new ApplicationException("La déclaration est déposée.");
		}
		if (declarationDelaisPaiementTiers.Statut != StatutDeclaration.EnCours)
		{
			throw new ApplicationException("La déclaration est clôturée.");
		}
		declarationDelaisPaiementTiers.Libelle = libelle;
		declarationDelaisPaiementTiers.ModificateurNo = Utilisateur.No;
		declarationDelaisPaiementTiers.DateModification = DateTime.Now;
		_declarationDelaisPaiementRepository.Update(declarationDelaisPaiementTiers);
		_notifyService.Notify(TypeEntity.DeclarationDelaisPaiement, no, TypeAction.Modification, Societe.No);
	}

	public void DeclarationDelaisPaiementCloture(int no)
	{
		DeclarationDelaisPaiementTiers declarationDelaisPaiementTiers = _declarationDelaisPaiementRepository.Get(no);
		if (declarationDelaisPaiementTiers == null)
		{
			throw new ApplicationException("Impossible de charger la déclaration.");
		}
		if (declarationDelaisPaiementTiers.IsDepose)
		{
			throw new ApplicationException("La déclaration est déposée.");
		}
		if (declarationDelaisPaiementTiers.Statut != StatutDeclaration.EnCours)
		{
			throw new ApplicationException("La déclaration est clôturée.");
		}
		if (!_declarationDelaisPaiementRepository.HasLignes(no))
		{
			throw new ApplicationException("La déclaration ne contient aucune ligne.");
		}
		declarationDelaisPaiementTiers.Statut = StatutDeclaration.Cloture;
		declarationDelaisPaiementTiers.ModificateurNo = Utilisateur.No;
		declarationDelaisPaiementTiers.DateModification = DateTime.Now;
		_declarationDelaisPaiementRepository.Update(declarationDelaisPaiementTiers);
		_notifyService.Notify(TypeEntity.DeclarationDelaisPaiement, no, TypeAction.Modification, Societe.No);
	}

	public void DeclarationDelaisPaiementAnnulerCloture(int no)
	{
		DeclarationDelaisPaiementTiers declarationDelaisPaiementTiers = _declarationDelaisPaiementRepository.Get(no);
		if (declarationDelaisPaiementTiers == null)
		{
			throw new ApplicationException("Impossible de charger la déclaration.");
		}
		if (declarationDelaisPaiementTiers.IsDepose)
		{
			throw new ApplicationException("La déclaration est déposée.");
		}
		if (declarationDelaisPaiementTiers.IsFichierGenerer)
		{
			throw new ApplicationException("Le fichier de la déclaration est généré.");
		}
		if (declarationDelaisPaiementTiers.Statut != StatutDeclaration.Cloture)
		{
			throw new ApplicationException("La déclaration n'est pas clôturée.");
		}
		declarationDelaisPaiementTiers.Statut = StatutDeclaration.EnCours;
		declarationDelaisPaiementTiers.ModificateurNo = Utilisateur.No;
		declarationDelaisPaiementTiers.DateModification = DateTime.Now;
		_declarationDelaisPaiementRepository.Update(declarationDelaisPaiementTiers);
		_notifyService.Notify(TypeEntity.DeclarationDelaisPaiement, no, TypeAction.Modification, Societe.No);
	}

	public void DeclarationDelaisPaiementDepose(int no)
	{
		DeclarationDelaisPaiementTiers declarationDelaisPaiementTiers = _declarationDelaisPaiementRepository.Get(no);
		if (declarationDelaisPaiementTiers == null)
		{
			throw new ApplicationException("Impossible de charger la déclaration.");
		}
		if (!declarationDelaisPaiementTiers.IsFichierGenerer)
		{
			throw new ApplicationException("Le fichier du déclaration n'est pas généré.");
		}
		if (declarationDelaisPaiementTiers.IsDepose)
		{
			throw new ApplicationException("La déclaration est déposée.");
		}
		if (declarationDelaisPaiementTiers.Statut != StatutDeclaration.Cloture)
		{
			throw new ApplicationException("La déclaration n'est pas clôturée.");
		}
		if (!_declarationDelaisPaiementRepository.HasLignes(no))
		{
			throw new ApplicationException("La déclaration ne contient aucune ligne.");
		}
		declarationDelaisPaiementTiers.IsDepose = true;
		declarationDelaisPaiementTiers.ModificateurNo = Utilisateur.No;
		declarationDelaisPaiementTiers.DateModification = DateTime.Now;
		_declarationDelaisPaiementRepository.Update(declarationDelaisPaiementTiers);
		_notifyService.Notify(TypeEntity.DeclarationDelaisPaiement, no, TypeAction.Modification, Societe.No);
	}

	public void DeclarationDelaisPaiementFichierGenerer(int no)
	{
		DeclarationDelaisPaiementTiers declarationDelaisPaiementTiers = _declarationDelaisPaiementRepository.Get(no);
		if (declarationDelaisPaiementTiers == null)
		{
			throw new ApplicationException("Impossible de charger la déclaration.");
		}
		if (declarationDelaisPaiementTiers.IsFichierGenerer)
		{
			throw new ApplicationException("Le fichier du déclaration est déjà généré.");
		}
		if (declarationDelaisPaiementTiers.IsDepose)
		{
			throw new ApplicationException("La déclaration est déposée.");
		}
		if (declarationDelaisPaiementTiers.Statut != StatutDeclaration.Cloture)
		{
			throw new ApplicationException("La déclaration n'est pas clôturée.");
		}
		if (!_declarationDelaisPaiementRepository.HasLignes(no))
		{
			throw new ApplicationException("La déclaration ne contient aucune ligne.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		declarationDelaisPaiementTiers.IsFichierGenerer = true;
		declarationDelaisPaiementTiers.ModificateurNo = Utilisateur.No;
		declarationDelaisPaiementTiers.DateModification = DateTime.Now;
		_declarationDelaisPaiementRepository.Update(declarationDelaisPaiementTiers);
		_notifyService.Notify(TypeEntity.DeclarationDelaisPaiement, no, TypeAction.Modification, Societe.No);
		transactionScope.Complete();
	}

	public void DeclarationDelaisPaiementFichierAnnulerGeneration(int no)
	{
		DeclarationDelaisPaiementTiers declarationDelaisPaiementTiers = _declarationDelaisPaiementRepository.Get(no);
		if (declarationDelaisPaiementTiers == null)
		{
			throw new ApplicationException("Impossible de charger la déclaration.");
		}
		if (!declarationDelaisPaiementTiers.IsFichierGenerer)
		{
			throw new ApplicationException("Le fichier du déclaration n'est pas généré.");
		}
		if (declarationDelaisPaiementTiers.IsDepose)
		{
			throw new ApplicationException("La déclaration est déposée.");
		}
		if (declarationDelaisPaiementTiers.Statut != StatutDeclaration.Cloture)
		{
			throw new ApplicationException("La déclaration n'est pas clôturée.");
		}
		if (!_declarationDelaisPaiementRepository.HasLignes(no))
		{
			throw new ApplicationException("La déclaration ne contient aucune ligne.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		declarationDelaisPaiementTiers.IsFichierGenerer = false;
		declarationDelaisPaiementTiers.ModificateurNo = Utilisateur.No;
		declarationDelaisPaiementTiers.DateModification = DateTime.Now;
		_declarationDelaisPaiementRepository.Update(declarationDelaisPaiementTiers);
		_notifyService.Notify(TypeEntity.DeclarationDelaisPaiement, no, TypeAction.Modification, Societe.No);
		transactionScope.Complete();
	}

	public void DeclarationDelaisPaiementDelete(int no)
	{
		DeclarationDelaisPaiementTiers declarationDelaisPaiementTiers = _declarationDelaisPaiementRepository.Get(no);
		if (declarationDelaisPaiementTiers == null)
		{
			throw new ApplicationException("Impossible de charger la déclaration.");
		}
		if (declarationDelaisPaiementTiers.IsDepose)
		{
			throw new ApplicationException("La déclaration est déposée.");
		}
		if (declarationDelaisPaiementTiers.Statut != StatutDeclaration.EnCours)
		{
			throw new ApplicationException("La déclaration est clôturée.");
		}
		if (_declarationDelaisPaiementRepository.HasLignes(no))
		{
			throw new ApplicationException("La déclaration contient des lignes.");
		}
		_declarationDelaisPaiementRepository.Delete(declarationDelaisPaiementTiers);
		_notifyService.Notify(TypeEntity.DeclarationDelaisPaiement, no, TypeAction.Suppression, Societe.No);
	}

	public void DeclarationDelaisPaiementLigneAjouter(int declarationNo, int echeanceNo, int? affectationNo, decimal depassement, DateTime echeanceLegale)
	{
		if (declarationNo <= 0)
		{
			throw new ArgumentNullException("declarationNo");
		}
		if (echeanceNo <= 0)
		{
			throw new ArgumentNullException("echeanceNo");
		}
		if (depassement <= 0m)
		{
			throw new ArgumentNullException("depassement");
		}
		DeclarationDelaisPaiementTiers obj = _declarationDelaisPaiementRepository.Get(declarationNo) ?? throw new ApplicationException("Impossible de charger la déclaration.");
		if (obj.IsDepose)
		{
			throw new ApplicationException("La déclaration est déposée.");
		}
		if (obj.Statut != StatutDeclaration.EnCours)
		{
			throw new ApplicationException("La déclaration est clôturée.");
		}
		if (_echeanceRepository.Get(echeanceNo) == null)
		{
			throw new ApplicationException("Impossible de charger l'échéance.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		_ligneDeclarationDelaisPaiementRepository.Create(new LigneDeclarationDelaisPaiement
		{
			DeclarationNo = declarationNo,
			EcheanceNo = echeanceNo,
			AffectationNo = affectationNo,
			Depassement = depassement,
			DocumentEcheanceLegale = echeanceLegale
		});
		_notifyService.Notify(TypeEntity.DeclarationDelaisPaiement, declarationNo, TypeAction.Modification, Societe.No);
		transactionScope.Complete();
	}

	public void DeclarationDelaisPaiementDeleteLigne(int ligneNo)
	{
		LigneDeclarationDelaisPaiement ligneDeclarationDelaisPaiement = _ligneDeclarationDelaisPaiementRepository.Get(ligneNo);
		if (ligneDeclarationDelaisPaiement == null)
		{
			throw new ApplicationException("Impossible de charger la ligne.");
		}
		DeclarationDelaisPaiementTiers declarationDelaisPaiementTiers = _declarationDelaisPaiementRepository.Get(ligneDeclarationDelaisPaiement.DeclarationNo);
		if (declarationDelaisPaiementTiers == null)
		{
			throw new ApplicationException("Impossible de charger la déclaration.");
		}
		if (declarationDelaisPaiementTiers.IsDepose)
		{
			throw new ApplicationException("La déclaration est déposée.");
		}
		if (declarationDelaisPaiementTiers.Statut != StatutDeclaration.EnCours)
		{
			throw new ApplicationException("La déclaration est clôturée.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		_ligneDeclarationDelaisPaiementRepository.Delete(ligneDeclarationDelaisPaiement);
		_notifyService.Notify(TypeEntity.DeclarationDelaisPaiement, declarationDelaisPaiementTiers.No, TypeAction.Modification, Societe.No);
		transactionScope.Complete();
	}

	public IEnumerable<LigneDeclarationDelaisPaiement> DeclarationDelaisPaiementLigneGetAll(int declarationNo)
	{
		return _ligneDeclarationDelaisPaiementRepository.GetAll(declarationNo);
	}

	public List<WorkflowHistory> WorkflowHistoryGetAll(TypeEntity typeEntity, int entityNo)
	{
		return _workflowHistoryRepository.GetAll(typeEntity, entityNo);
	}

	public void WorkflowHistoryCreate(TypeEntity typeEntity, int entityNo, string commentaire, WorkflowValidationStatut statut)
	{
		if (!Societe.UseWorkFlowValidationEcheanceFournisseur)
		{
			throw new ApplicationException("Opération invalide ! Veuillez activer l’option du processus de validation.");
		}
		if (typeEntity == TypeEntity.Echeance)
		{
			Echeance echeance = EcheanceGet(entityNo);
			if (echeance == null)
			{
				throw new ApplicationException("Impossible de charger l'échéance.");
			}
			if (echeance.WorkflowStatut == (EcheanceWorkflowValidationStatut)statut)
			{
				throw new ApplicationException("Opération invalide! Il existe un process de validation en cours pour l'échéance.");
			}
			if (echeance.IsReserveDossierFrs)
			{
				throw new ApplicationException("Opération invalide! L'échéance [" + echeance.DocumentNumero + "] est réservée dans un dossier de règlement fournisseur.");
			}
			if (echeance.Solde != echeance.Montant)
			{
				throw new ApplicationException("Opération invalide! l'échéance [" + echeance.DocumentNumero + "] est partiellement soldée.");
			}
			if (statut == WorkflowValidationStatut.Rejete && string.IsNullOrEmpty(commentaire))
			{
				throw new ApplicationException("Commentaire obligatoire.");
			}
			WorkflowHistory workflow = new WorkflowHistory
			{
				Commentaire = commentaire,
				EntityNo = entityNo,
				EntityType = typeEntity,
				Statut = statut,
				UserNo = Utilisateur.No,
				SocieteNo = Societe.No,
				DateCreation = DateTime.Now
			};
			if (_workflowHistoryRepository.Create(workflow) <= 0)
			{
				throw new ApplicationException("Opération invalide![Workflow]");
			}
			if (typeEntity == TypeEntity.Echeance)
			{
				Echeance echeance2 = EcheanceGet(entityNo);
				if (echeance2 == null)
				{
					throw new ApplicationException("Impossible de charger l'échéance.");
				}
				echeance2.WorkflowStatut = (EcheanceWorkflowValidationStatut)((statut != WorkflowValidationStatut.Rejete) ? statut : WorkflowValidationStatut.Aucun);
				_echeanceRepository.Update(echeance2);
				_notifyService.Notify(typeEntity, entityNo, TypeAction.Modification, Societe.No);
				return;
			}
			throw new NotImplementedException("typeEntity");
		}
		throw new NotImplementedException("typeEntity");
	}

	public SocieteManager(ISocieteRepository societeRepository, ISocieteDeviseRepository societeDeviseRepository, ISocieteModeReglementRepository societeModeRepository, IEcheanceRepository echeanceRepository, ISocieteImpressionRepository societeImpressionRepository, ISocTypeBordereauRepository socTypeBordereauRepository, IMouvmentBancaireRepository mvtBancaireRepository, CaisseManager caisseManager, HistoriqueMvtManager historiqueMvtManager, TransfertManager transfertManager, DossierImpayeManager dossierImpayeManager, NotifyService notifyService, ISocieteSoucheRepository societeSoucheRepository, ISolvabiliteClientRepository solvabiliteClientRepository, ISolvabiliteFournisseurRepository solvabiliteFournisseurRepository, IEngagementClientRepository engagementClientRepository, IEngagementFournisseurRepository engagementFournisseurRepository, IEngagementClientDetailsRepository engagementClientDetailsRepository, ILigneEngagementClientRepository ligneEngagementClientRepository, IDossierReglementRepository dossierReglementRepository, ILigneDossierReglementRepository ligneDossierReglementRepository, EcritureComptaFrsManager ecritureComptaFrsManager, IChequierRepository chequierRepository, IChequeRepository chequeRepository, IChequeEntityRepository chequeEntityRepository, IBanqueTiersRepository banqueTiersRepository, ISocieteUtilisateurRepository societeUtilisateurRepository, IAutorisationCaisseRepository autorisationCaisseRepository, IAutorisationSoucheRepository autorisationSoucheRepository, INoteRepository noteRepository, IGridLayoutRepository gridLayoutRepository, IGridLayoutFilterRepository gridLayoutFilterRepository, IUtilisateurGridRepository utilisateurGridRepository, IEcritureComptaRepository ecritureCompta, VerifySoldeManager verifySoldeManager, IMouvementEscompteRepository mouvementEscompteRepositry, IEcartRepository ecartRepository, IEcartEchangeRepository ecartEchangeRepository, IAlimentationCaisseRepository alimentationCaisseRepository, IRecapTiersRepository recapTiersRepository, IRappelRepository rappelRepository, ILigneRappelRepository ligneRappelRepository, IDetailHistoriqueRappelEcheanceRepository detailHistoriqueRappelEcheanceRepository, ICautionRepository cautionRepository, ITypeCautionRepository typeCautionRepository, ILigneDossierReglementCommRepository ligneDossierReglementCommRepository, IReglementClientRepository reglementClientRepository, IReglementFournisseurRepository reglementFournisseurRepository, IReleveClientRepository releveClientRepository, IFactureRepository factureRepository, IVentilationAnalytiqueRepository ventilationAnalytiqueRepository, IBalanceAgeeRepository balanceAgeeRepository, IParametreMailRepository parametreMailRepository, IInformationsBanqueRepository informationsBanqueRepository, ILicenceApplicationVersion licenceApplicationVersion, IFourchetteCommissionRepository fourchetteCommissionRepository, IInformationLibreRepository informationLibreRepository, IDeclarationTvaEncaissementRepository declarationTvaEncaissementRepository, ILigneDeclarationTvaEncaissementRepository ligneDeclarationTvaEncaissementRepository, ILettreRecouvrementFieldsRepository lettreRecouvrementFieldsRepository, ISocieteCodeActiviteTaxeRepository societeCodeActiviteTaxeRepository, IAttestationRetenueTiersRepository attestationRetenueTiersRepository, ISocieteDesignationDocumentRepository societeDesignationDocumentRepository, ILigneDossierEcheanceTvaNonRecuperableRepository ligneDossierEcheanceTvaNonRecuperableRepository, ICarnetTraiteRepository carnetTraiteRepository, ITraiteRepository traiteRepository, IDeclarationRetenuSourceRepository declarationRetenuSourceRepository, IRetenuALaSourceRepository retenuALaSourceRepository, ILigneDeclarationRetenuSourceRepository ligneDeclarationRetenuSourceRepository, ITiersServiceContactRepository tiersServiceContactRepository, IConfigConnectionErpExternRepository configConnectionErpExternRepository, IJoursReposRepository joursReposRepository, IConventionDelaisPaiementTiersRepository conventionDelaisPaiementTiersRepository, ISocieteCodeActiviteTiersRepository societeCodeActiviteTiersRepository, IDeclarationDelaisPaiementRepository declarationDelaisPaiementRepository, ILigneDeclarationDelaisPaiementRepository ligneDeclarationDelaisPaiementRepository, ITypeCreditRepository typeCreditRepository, IGarentieRepository garentieRepository, IInformationLibreGrfRepository informationLibreGrfRepository, IWorkflowHistoryRepository workflowHistoryRepository, WorkerManager workerManager)
	{
		_societeRepository = societeRepository ?? throw new ArgumentNullException("societeRepository");
		_societeDeviseRepository = societeDeviseRepository ?? throw new ArgumentNullException("societeDeviseRepository");
		_societeModeRepository = societeModeRepository ?? throw new ArgumentNullException("societeModeRepository");
		_caisseManager = caisseManager ?? throw new ArgumentNullException("caisseManager");
		_echeanceRepository = echeanceRepository ?? throw new ArgumentNullException("echeanceRepository");
		_societeImpressionRepository = societeImpressionRepository ?? throw new ArgumentNullException("societeImpressionRepository");
		_socTypeBordereauRepository = socTypeBordereauRepository ?? throw new ArgumentNullException("socTypeBordereauRepository");
		_historiqueMvtManager = historiqueMvtManager ?? throw new ArgumentNullException("historiqueMvtManager");
		_transfertManager = transfertManager ?? throw new ArgumentNullException("transfertManager");
		_dossierImpayeManager = dossierImpayeManager ?? throw new ArgumentNullException("dossierImpayeManager");
		_notifyService = notifyService ?? throw new ArgumentNullException("notifyService");
		_societeSoucheRepository = societeSoucheRepository ?? throw new ArgumentNullException("societeSoucheRepository");
		_solvabiliteClientRepository = solvabiliteClientRepository ?? throw new ArgumentNullException("solvabiliteClientRepository");
		_solvabiliteFournisseurRepository = solvabiliteFournisseurRepository ?? throw new ArgumentNullException("solvabiliteFournisseurRepository");
		_engagementClientRepository = engagementClientRepository ?? throw new ArgumentNullException("engagementClientRepository");
		_engagementFournisseurRepository = engagementFournisseurRepository ?? throw new ArgumentNullException("engagementFournisseurRepository");
		_engagementClientDetailsRepository = engagementClientDetailsRepository ?? throw new ArgumentNullException("engagementClientDetailsRepository");
		_ligneEngagementClientRepository = ligneEngagementClientRepository ?? throw new ArgumentNullException("ligneEngagementClientRepository");
		_dossierReglementRepository = dossierReglementRepository ?? throw new ArgumentNullException("dossierReglementRepository");
		_ligneDossierReglementRepository = ligneDossierReglementRepository ?? throw new ArgumentNullException("ligneDossierReglementRepository");
		_mvtBancaireRepository = mvtBancaireRepository ?? throw new ArgumentNullException("mvtBancaireRepository");
		_ecritureComptaFrsManager = ecritureComptaFrsManager ?? throw new ArgumentNullException("ecritureComptaFrsManager");
		_chequierRepository = chequierRepository ?? throw new ArgumentNullException("chequierRepository");
		_chequeRepository = chequeRepository ?? throw new ArgumentNullException("chequeRepository");
		_chequeEntityRepository = chequeEntityRepository ?? throw new ArgumentNullException("chequeEntityRepository");
		_banqueTiersRepository = banqueTiersRepository ?? throw new ArgumentNullException("banqueTiersRepository");
		_societeUtilisateurRepository = societeUtilisateurRepository ?? throw new ArgumentNullException("societeUtilisateurRepository");
		_autorisationCaisseRepository = autorisationCaisseRepository ?? throw new ArgumentNullException("autorisationCaisseRepository");
		_autorisationSoucheRepository = autorisationSoucheRepository ?? throw new ArgumentNullException("autorisationSoucheRepository");
		_noteRepository = noteRepository ?? throw new ArgumentNullException("noteRepository");
		_gridLayoutRepository = gridLayoutRepository ?? throw new ArgumentNullException("gridLayoutRepository");
		_gridLayoutFilterRepository = gridLayoutFilterRepository ?? throw new ArgumentNullException("gridLayoutFilterRepository");
		_utilisateurGridRepository = utilisateurGridRepository ?? throw new ArgumentNullException("utilisateurGridRepository");
		_ecritureCompta = ecritureCompta ?? throw new ArgumentNullException("ecritureCompta");
		_verifySoldeManager = verifySoldeManager ?? throw new ArgumentNullException("verifySoldeManager");
		_mouvementEscompteRepositry = mouvementEscompteRepositry ?? throw new ArgumentNullException("mouvementEscompteRepositry");
		_ecartRepository = ecartRepository ?? throw new ArgumentNullException("ecartRepository");
		_ecartEchangeRepository = ecartEchangeRepository ?? throw new ArgumentNullException("ecartEchangeRepository");
		_alimentationCaisseRepository = alimentationCaisseRepository ?? throw new ArgumentNullException("alimentationCaisseRepository");
		_recapTiersRepository = recapTiersRepository ?? throw new ArgumentNullException("recapTiersRepository");
		_rappelRepository = rappelRepository ?? throw new ArgumentNullException("rappelRepository");
		_ligneRappelRepository = ligneRappelRepository ?? throw new ArgumentNullException("ligneRappelRepository");
		_detailHistoriqueRappelEcheanceRepository = detailHistoriqueRappelEcheanceRepository ?? throw new ArgumentNullException("detailHistoriqueRappelEcheanceRepository");
		_cautionRepository = cautionRepository ?? throw new ArgumentNullException("cautionRepository");
		_typeCautionRepository = typeCautionRepository ?? throw new ArgumentNullException("typeCautionRepository");
		_ligneDossierReglementCommRepository = ligneDossierReglementCommRepository ?? throw new ArgumentNullException("ligneDossierReglementCommRepository");
		_reglementClientRepository = reglementClientRepository ?? throw new ArgumentNullException("reglementClientRepository");
		_reglementFournisseurRepository = reglementFournisseurRepository ?? throw new ArgumentNullException("reglementFournisseurRepository");
		_releveClientRepository = releveClientRepository ?? throw new ArgumentNullException("releveClientRepository");
		_factureRepository = factureRepository ?? throw new ArgumentNullException("factureRepository");
		_ventilationAnalytiqueRepository = ventilationAnalytiqueRepository ?? throw new ArgumentNullException("ventilationAnalytiqueRepository");
		_balanceAgeeRepository = balanceAgeeRepository ?? throw new ArgumentNullException("balanceAgeeRepository");
		_parametreMailRepository = parametreMailRepository ?? throw new ArgumentNullException("parametreMailRepository");
		_informationsBanqueRepository = informationsBanqueRepository ?? throw new ArgumentNullException("informationsBanqueRepository");
		_licenceApplicationVersion = licenceApplicationVersion ?? throw new ArgumentNullException("licenceApplicationVersion");
		_fourchetteCommissionRepository = fourchetteCommissionRepository ?? throw new ArgumentNullException("fourchetteCommissionRepository");
		_informationLibreRepository = informationLibreRepository ?? throw new ArgumentNullException("informationLibreRepository");
		_declarationTvaEncaissementRepository = declarationTvaEncaissementRepository ?? throw new ArgumentNullException("declarationTvaEncaissementRepository");
		_ligneDeclarationTvaEncaissementRepository = ligneDeclarationTvaEncaissementRepository ?? throw new ArgumentNullException("ligneDeclarationTvaEncaissementRepository");
		_lettreRecouvrementFieldsRepository = lettreRecouvrementFieldsRepository ?? throw new ArgumentNullException("lettreRecouvrementFieldsRepository");
		_societeCodeActiviteTaxeRepository = societeCodeActiviteTaxeRepository ?? throw new ArgumentNullException("societeCodeActiviteTaxeRepository");
		_attestationRetenueTiersRepository = attestationRetenueTiersRepository ?? throw new ArgumentNullException("attestationRetenueTiersRepository");
		_societeDesignationDocumentRepository = societeDesignationDocumentRepository ?? throw new ArgumentNullException("societeDesignationDocumentRepository");
		_ligneDossierEcheanceTvaNonRecuperableRepository = ligneDossierEcheanceTvaNonRecuperableRepository ?? throw new ArgumentNullException("ligneDossierEcheanceTvaNonRecuperableRepository");
		_carnetTraiteRepository = carnetTraiteRepository ?? throw new ArgumentNullException("carnetTraiteRepository");
		_traiteRepository = traiteRepository ?? throw new ArgumentNullException("traiteRepository");
		_declarationRetenuSourceRepository = declarationRetenuSourceRepository;
		_retenuALaSourceRepository = retenuALaSourceRepository;
		_ligneDeclarationRetenuSourceRepository = ligneDeclarationRetenuSourceRepository ?? throw new ArgumentNullException("ligneDeclarationRetenuSourceRepository");
		_tiersServiceContactRepository = tiersServiceContactRepository ?? throw new ArgumentNullException("tiersServiceContactRepository");
		_configConnectionErpExternRepository = configConnectionErpExternRepository ?? throw new ArgumentNullException("configConnectionErpExternRepository");
		_joursReposRepository = joursReposRepository ?? throw new ArgumentNullException("joursReposRepository");
		_conventionDelaisPaiementTiersRepository = conventionDelaisPaiementTiersRepository ?? throw new ArgumentNullException("conventionDelaisPaiementTiersRepository");
		_societeCodeActiviteTiersRepository = societeCodeActiviteTiersRepository ?? throw new ArgumentNullException("societeCodeActiviteTiersRepository");
		_declarationDelaisPaiementRepository = declarationDelaisPaiementRepository ?? throw new ArgumentNullException("declarationDelaisPaiementRepository");
		_ligneDeclarationDelaisPaiementRepository = ligneDeclarationDelaisPaiementRepository ?? throw new ArgumentNullException("ligneDeclarationDelaisPaiementRepository");
		_typeCreditRepository = typeCreditRepository ?? throw new ArgumentNullException("typeCreditRepository");
		_garentieRepository = garentieRepository ?? throw new ArgumentNullException("garentieRepository");
		_informationLibreGrfRepository = informationLibreGrfRepository ?? throw new ArgumentNullException("informationLibreGrfRepository");
		_workflowHistoryRepository = workflowHistoryRepository ?? throw new ArgumentNullException("workflowHistoryRepository");
		_workerManager = workerManager ?? throw new ArgumentNullException("workerManager");
	}

	public Task<IEnumerable<Ecart>> GetAllEcartAComptaAsync(ErpDomaine domaine, DateTime dateDe, DateTime dateA, EtatComptabilite comptabilite, EcheanceType[] types, CancellationToken cancellationToken)
	{
		return _ecartRepository.GetAllAComptaAsync(domaine, dateDe.Date, dateA.Date.AddDays(1.0), comptabilite, types, Societe.No, cancellationToken);
	}

	public Task<IEnumerable<EcartEchange>> GetAllEcartEchangeAComptaAsync(DateTime dateDe, DateTime dateA, EtatComptabilite comptabilite, EcheanceType[] types, CancellationToken cancellationToken)
	{
		return _ecartEchangeRepository.GetAllAComptaAsync(dateDe.Date, dateA.Date.AddDays(1.0), comptabilite, types, Societe.No, cancellationToken);
	}

	public void AnnulerReservationCheque(int chequeNo)
	{
		Cheque cheque = _chequeRepository.Get(chequeNo);
		if (cheque == null)
		{
			throw new ArgumentNullException("chequeNo");
		}
		if (cheque.Statut != ChequeStatut.Reserve)
		{
			throw new ArgumentException("Le Chèque n'est pas réservé!");
		}
		cheque.Statut = ChequeStatut.NonUtilise;
		cheque.Echeance = cheque.Date;
		cheque.IsBarre = false;
		cheque.TiersNo = null;
		cheque.Tire = string.Empty;
		cheque.Montant = 0m;
		cheque.MotifAnnulation = string.Empty;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_chequeRepository.Update(cheque);
		transactionScope.Complete();
	}

	public void AnnulerReservationTraite(int traiteNo)
	{
		Traite traite = _traiteRepository.Get(traiteNo);
		if (traite == null)
		{
			throw new ArgumentNullException("traiteNo");
		}
		if (traite.Statut != ChequeStatut.Reserve)
		{
			throw new ArgumentException("La traite n'est pas réservé!");
		}
		traite.Statut = ChequeStatut.NonUtilise;
		traite.Echeance = traite.Date;
		traite.TiersNo = null;
		traite.Tire = string.Empty;
		traite.Montant = 0m;
		traite.MotifAnnulation = string.Empty;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_traiteRepository.Update(traite);
		_notifyService.Notify(TypeEntity.Traites, traiteNo, TypeAction.Modification, Societe.No);
		_notifyService.Notify(TypeEntity.CarnetTraite, traite.CarnetTraiteNo, TypeAction.Modification, Societe.No);
		transactionScope.Complete();
	}

	public void AutorisationCaisseCreate(int utilisateurNo, int caisseNo, ProfilType type, Societe societe = null)
	{
		societe = societe ?? Societe;
		if (utilisateurNo <= 0)
		{
			throw new ArgumentNullException("utilisateurNo");
		}
		if (caisseNo <= 0)
		{
			throw new ArgumentNullException("caisseNo");
		}
		Utilisateur utilisateur = societe.GetUtilisateur(utilisateurNo);
		if (utilisateur == null)
		{
			throw new ApplicationException(rcRessources.UtilisateurInvalide);
		}
		Caisse caisse = societe.GetCaisse(caisseNo);
		if (caisse == null)
		{
			throw new ApplicationException(rcRessources.CaisseInvalide);
		}
		if (!Utilisateur.IsAdmin)
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAddAutorisationCaisse, caisse.Code, utilisateur.Login));
		}
		if (_autorisationCaisseRepository.Get(utilisateurNo, caisseNo, type) != null)
		{
			throw new ApplicationException("Autorisation caisse existe déjà!");
		}
		if (_autorisationCaisseRepository.GetAll(utilisateur.No, caisse.SocieteNo, type).Any((AutorisationCaisse a) => a.CaisseNo == caisseNo))
		{
			throw new ApplicationException("Autorisation caisse existe déjà!");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_autorisationCaisseRepository.Create(utilisateurNo, caisseNo, type);
		transactionScope.Complete();
	}

	public void AutorisationCaisseDelete(int utilisateurNo, int caisseNo, ProfilType type)
	{
		if (utilisateurNo <= 0)
		{
			throw new ArgumentNullException("utilisateurNo");
		}
		if (caisseNo <= 0)
		{
			throw new ArgumentNullException("caisseNo");
		}
		Caisse caisse = Societe.GetCaisse(caisseNo);
		if (caisse == null)
		{
			throw new ApplicationException(rcRessources.CaisseInvalide);
		}
		Utilisateur utilisateur = Societe.GetUtilisateur(utilisateurNo);
		if (utilisateur == null)
		{
			throw new ApplicationException(rcRessources.UtilisateurInvalide);
		}
		if (!Utilisateur.IsAdmin)
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorDeleteAutorisationCaisse, caisse.Code, utilisateur.Login));
		}
		AutorisationCaisse autorisationCaisse = _autorisationCaisseRepository.Get(utilisateurNo, caisseNo, type);
		if (autorisationCaisse == null)
		{
			throw new ApplicationException("Autorisation caisse invalide!");
		}
		if (_autorisationCaisseRepository.GetAll(utilisateur.No, caisse.SocieteNo, type).SingleOrDefault((AutorisationCaisse a) => a.CaisseNo == caisseNo) == null)
		{
			throw new ApplicationException("Autorisation caisse invalide!");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_autorisationCaisseRepository.Delete(autorisationCaisse.No);
		transactionScope.Complete();
	}

	public void AutorisationSoucheCreate(ErpDomaine domaine, int utilisateurNo, int soucheNo, Societe societe = null)
	{
		societe = societe ?? Societe;
		if (utilisateurNo <= 0)
		{
			throw new ArgumentNullException("utilisateurNo");
		}
		if (soucheNo < 0)
		{
			throw new ArgumentNullException("soucheNo");
		}
		Utilisateur utilisateur = societe.GetUtilisateur(utilisateurNo);
		if (utilisateur == null)
		{
			throw new ApplicationException(rcRessources.UtilisateurInvalide);
		}
		if (!Utilisateur.IsAdmin)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorAddAutorisationSouche);
		}
		if (_autorisationSoucheRepository.Get(domaine, utilisateurNo, soucheNo, societe.No) != null)
		{
			throw new ApplicationException("Autorisation souche existe déjà!");
		}
		if (GetAllAutorizedSouches(domaine, utilisateur.No, societe).Any((AutorisationSouche a) => a.SoucheNo == soucheNo))
		{
			throw new ApplicationException("Autorisation souche existe déjà!");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_autorisationSoucheRepository.Create(domaine, utilisateurNo, soucheNo, societe.No);
		transactionScope.Complete();
	}

	public void AutorisationSoucheDelete(ErpDomaine domaine, int utilisateurNo, int soucheNo)
	{
		if (utilisateurNo <= 0)
		{
			throw new ArgumentNullException("utilisateurNo");
		}
		if (soucheNo < 0)
		{
			throw new ArgumentNullException("soucheNo");
		}
		if (!Utilisateur.IsAdmin)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorDeleteAutorisationSouche);
		}
		Utilisateur utilisateur = Societe.GetUtilisateur(utilisateurNo);
		if (utilisateur == null)
		{
			throw new ApplicationException(rcRessources.UtilisateurInvalide);
		}
		AutorisationSouche autorisationSouche = _autorisationSoucheRepository.Get(domaine, utilisateurNo, soucheNo, Societe.No);
		if (autorisationSouche == null)
		{
			throw new ApplicationException("Souche invalide!");
		}
		if (GetAllAutorizedSouches(domaine, utilisateur.No).SingleOrDefault((AutorisationSouche a) => a.SoucheNo == soucheNo) == null)
		{
			throw new ApplicationException("Autorisation souche invalide!");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_autorisationSoucheRepository.Delete(autorisationSouche.No);
		transactionScope.Complete();
	}

	public int? CaisseCreate(string code, string intitule, string journal, string compteGeneral, int depense, int recette, bool isDefault, Societe societe = null)
	{
		if (string.IsNullOrEmpty(code))
		{
			throw new ArgumentNullException("code");
		}
		societe = societe ?? Societe;
		if (societe.GetCaisse(code) != null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorCaisseExist);
		}
		if (!Utilisateur.IsAdmin)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorCreateCaisse);
		}
		Caisse newCaisse = societe.GetNewCaisse(code);
		newCaisse.Intitule = intitule;
		newCaisse.Journal = journal;
		newCaisse.CompteGeneral = compteGeneral;
		if (depense == 0 && recette == 0)
		{
			throw new ApplicationException("Le type de la caisse est obligaroire!");
		}
		newCaisse.Depense = depense;
		newCaisse.Recette = recette;
		newCaisse.IsDefault = isDefault;
		newCaisse.SocieteNo = societe.No;
		int? result = CaisseManager.Create(newCaisse, societe);
		societe.RefreshCaisse();
		_notifyService.Notify(TypeEntity.Parametrage, 0, TypeAction.Modification, societe.No);
		return result;
	}

	public void CaisseDelete(int caisseNo, Societe societe = null)
	{
		if (caisseNo <= 0)
		{
			throw new ArgumentNullException("caisseNo");
		}
		societe = societe ?? Societe;
		Caisse caisse = societe.GetCaisse(caisseNo);
		if (caisse == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (!Utilisateur.IsAdmin)
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorDeleteCaisse, caisse.Code));
		}
		if (CaisseManager.HasMouvment(caisseNo))
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorCaisseMouvementee);
		}
		CaisseManager.Delete(caisse);
		societe.RefreshCaisse();
		_notifyService.Notify(TypeEntity.Parametrage, 0, TypeAction.Modification, societe.No);
	}

	public void CaisseModeCreate(int caisseNo, int modeNo, string journalEncaissement, string compteGeneralEncaissement, string journalDecaissement, string compteGeneralDecaissement, string compteGeneralImpayeEncaissement, string compteGeneralImpayeDecaissement, string compteGeneralAvanceEncaissement, string compteGeneralAvanceDecaissement, Societe societe = null, bool isOldModeReglement = false)
	{
		societe = societe ?? Societe;
		Caisse caisse = societe.GetCaisse(caisseNo);
		if (caisse == null)
		{
			throw new InvalidProgramException(TresorerieCoreMessages.ErrorCaisseInvalid);
		}
		if (!Utilisateur.IsAdmin)
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorUpdateCaisse, caisse.Code));
		}
		ModeReglement modeReglement = Groupe.ModeReglementManager.Get(modeNo);
		if (modeReglement == null)
		{
			throw new InvalidProgramException(TresorerieCoreMessages.ErrorModeInvalide);
		}
		if (modeReglement.EnSommeil && !isOldModeReglement)
		{
			throw new InvalidOperationException("Opération invalide! Le mode est en sommeil!");
		}
		if (caisse.HasModeReglement(modeNo))
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorModeReglementAffectation);
		}
		if (modeReglement.Type == ReglementType.Autre && modeReglement.IsModeAvoir)
		{
			journalEncaissement = string.Empty;
			compteGeneralEncaissement = string.Empty;
			journalDecaissement = string.Empty;
			compteGeneralDecaissement = string.Empty;
			compteGeneralImpayeEncaissement = string.Empty;
			compteGeneralImpayeDecaissement = string.Empty;
			compteGeneralAvanceEncaissement = string.Empty;
			compteGeneralAvanceDecaissement = string.Empty;
		}
		CaisseManager.CaisseModeCreate(caisseNo, modeNo, journalEncaissement, compteGeneralEncaissement, journalDecaissement, compteGeneralDecaissement, compteGeneralImpayeEncaissement, compteGeneralImpayeDecaissement, compteGeneralAvanceEncaissement, compteGeneralAvanceDecaissement);
		societe.RefreshCaisse();
		_notifyService.Notify(TypeEntity.Parametrage, 0, TypeAction.Modification, societe.No);
	}

	public void CaisseModeDelete(int no, int caisseNo, Societe societe = null)
	{
		societe = societe ?? Societe;
		Caisse caisse = societe.GetCaisse(caisseNo);
		if (caisse == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorOperationInvalide);
		}
		if (!Utilisateur.IsAdmin)
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorUpdateCaisse, caisse.Code));
		}
		CaisseModeReglement mode = caisse.GetMode(no);
		if (mode == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorOperationInvalide);
		}
		if (CaisseManager.IsModeUsed(no, caisseNo))
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorCaisseMouvementee);
		}
		CaisseManager.CaisseModeDelete(mode);
		societe.RefreshCaisse();
		_notifyService.Notify(TypeEntity.Parametrage, 0, TypeAction.Modification, societe.No);
	}

	public bool IsEcheanceUtiliseDossierFrs(int echeanceNo, Societe societe = null)
	{
		if (echeanceNo <= 0)
		{
			throw new ArgumentNullException("echeanceNo");
		}
		societe = societe ?? Societe;
		Echeance echeance = _echeanceRepository.Get(echeanceNo);
		if (echeance == null)
		{
			throw new ApplicationException("Impossible de charger l'échéance.");
		}
		if (echeance.Domaine != ErpDomaine.Achat)
		{
			return false;
		}
		return _echeanceRepository.IsUsedInDossierFrs(echeance.No, societe.No);
	}

	public void CaisseModeUpdate(int no, int caisseNo, string journalEncaissement, string compteGeneralEncaissement, string journalDecaissement, string compteGeneralDecaissement, string compteGeneralImpayeEncaissement, string compteGeneralImpayeDecaissement, string compteGeneralAvanceEncaissement, string compteGeneralAvanceDecaissement, Societe societe = null)
	{
		societe = societe ?? Societe;
		Caisse caisse = societe.GetCaisse(caisseNo);
		if (caisse == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorOperationInvalide);
		}
		if (!Utilisateur.IsAdmin)
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorUpdateCaisse, caisse.Code));
		}
		CaisseModeReglement mode = caisse.GetMode(no);
		if (mode == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorOperationInvalide);
		}
		mode.JournalEncaissement = ((mode.Type == ReglementType.Autre && mode.IsModeAvoir) ? string.Empty : journalEncaissement);
		mode.CompteGeneralEncaissement = ((mode.Type == ReglementType.Autre && mode.IsModeAvoir) ? string.Empty : compteGeneralEncaissement);
		mode.JournalDecaissement = ((mode.Type == ReglementType.Autre && mode.IsModeAvoir) ? string.Empty : journalDecaissement);
		mode.CompteGeneralDecaissement = ((mode.Type == ReglementType.Autre && mode.IsModeAvoir) ? string.Empty : compteGeneralDecaissement);
		mode.CompteGeneralImpayeEncaissement = ((mode.Type == ReglementType.Autre && mode.IsModeAvoir) ? string.Empty : compteGeneralImpayeEncaissement);
		mode.CompteGeneralImpayeDecaissement = ((mode.Type == ReglementType.Autre && mode.IsModeAvoir) ? string.Empty : compteGeneralImpayeDecaissement);
		mode.CompteGeneralAvanceEncaissement = ((mode.Type == ReglementType.Autre && mode.IsModeAvoir) ? string.Empty : compteGeneralAvanceEncaissement);
		mode.CompteGeneralAvanceDecaissement = ((mode.Type == ReglementType.Autre && mode.IsModeAvoir) ? string.Empty : compteGeneralAvanceDecaissement);
		CaisseManager.CaisseModeUpdate(mode);
		Societe.RefreshCaisse();
		_notifyService.Notify(TypeEntity.Parametrage, 0, TypeAction.Modification, societe.No);
	}

	public void CaisseUpdate(int no, string intitule, string journal, string compteGeneral, int depense, int recette, bool enSommeil, bool isDefault)
	{
		Caisse caisse = Societe.GetCaisse(no);
		if (caisse == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (!Utilisateur.IsAdmin)
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorUpdateCaisse, caisse.Code));
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		if (enSommeil)
		{
			if (!caisse.EnSommeil)
			{
				CaisseManager.MettreEnSommeilCaisse(no);
			}
			caisse.EnSommeil = true;
		}
		else
		{
			caisse.EnSommeil = false;
		}
		caisse.Intitule = intitule;
		caisse.Journal = journal;
		caisse.CompteGeneral = compteGeneral;
		caisse.Depense = depense;
		caisse.Recette = recette;
		caisse.IsDefault = isDefault;
		if (!Societe.Contain(caisse))
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		CaisseManager.Update(caisse);
		Societe.RefreshCaisse();
		_notifyService.Notify(TypeEntity.Parametrage, 0, TypeAction.Modification, caisse.SocieteNo);
		transactionScope.Complete();
	}

	public bool CanAddEcheanceInRappel(int echeanceNo)
	{
		return !_ligneRappelRepository.EcheanceInclueRappelNonCloture(echeanceNo);
	}

	public void ChequeCreate(string numero, int chequierNo)
	{
		if (string.IsNullOrEmpty(numero))
		{
			throw new ArgumentNullException("numero");
		}
		if (chequierNo <= 0)
		{
			throw new ArgumentNullException("chequierNo");
		}
		Chequier chequier = _chequierRepository.Get(chequierNo);
		if (chequier == null)
		{
			throw new ApplicationException("Chéquier n'existe pas");
		}
		if (chequier.GetCheque(numero) != null)
		{
			throw new InvalidOperationException("Chèque existe déjà dans le chéquier!");
		}
		Cheque cheque = new Cheque
		{
			Numero = numero,
			ChequierNo = chequierNo,
			Statut = ChequeStatut.NonUtilise,
			TiersNo = null,
			Date = DateTime.Now,
			Echeance = DateTime.Now,
			Montant = 0m,
			IsBarre = false,
			Tire = string.Empty,
			MotifAnnulation = string.Empty,
			DateValiditer = chequier.DateValiditer,
			MontantPlafond = chequier.MontantPlafond
		};
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_chequeRepository.Create(cheque);
		transactionScope.Complete();
	}

	public int? ChequeCreate(string numero, int chequierNo, ChequeStatut statut, int tiersNo, DateTime date, DateTime echeance, decimal montant, bool isBarre, string tire)
	{
		if (string.IsNullOrEmpty(numero))
		{
			throw new ArgumentNullException("numero");
		}
		if (chequierNo <= 0)
		{
			throw new ArgumentNullException("chequierNo");
		}
		Chequier chequier = _chequierRepository.Get(chequierNo);
		if (chequier == null)
		{
			throw new ApplicationException("Chéquier n'existe pas");
		}
		if (chequier.GetCheque(numero) != null)
		{
			throw new InvalidOperationException("Chèque existe déjà dans le chéquier!");
		}
		Cheque cheque = new Cheque
		{
			Numero = numero,
			ChequierNo = chequierNo,
			Statut = statut,
			TiersNo = tiersNo,
			Date = date,
			Echeance = echeance,
			Montant = montant,
			IsBarre = isBarre,
			Tire = tire,
			MotifAnnulation = string.Empty,
			DateValiditer = chequier.DateValiditer,
			MontantPlafond = chequier.MontantPlafond
		};
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		int? result = _chequeRepository.Create(cheque);
		transactionScope.Complete();
		return result;
	}

	public bool EcheanceHasWorkflow(int societeNo)
	{
		return _echeanceRepository.HasWorkflowValidation(societeNo);
	}

	public void TraiteCreate(string numero, int carnetTraiteNo)
	{
		if (string.IsNullOrEmpty(numero))
		{
			throw new ArgumentNullException("numero");
		}
		if (carnetTraiteNo <= 0)
		{
			throw new ArgumentNullException("carnetTraiteNo");
		}
		if ((_carnetTraiteRepository.Get(carnetTraiteNo) ?? throw new ApplicationException("Carnet de traite n'existe pas")).GetTraite(numero) != null)
		{
			throw new InvalidOperationException("Traite existe déjà dans le carnet de traite!");
		}
		Traite traite = new Traite
		{
			Numero = numero,
			CarnetTraiteNo = carnetTraiteNo,
			Statut = ChequeStatut.NonUtilise,
			TiersNo = null,
			Date = DateTime.Now,
			Echeance = DateTime.Now,
			Montant = 0m,
			Tire = string.Empty,
			MotifAnnulation = string.Empty
		};
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_traiteRepository.Create(traite);
		transactionScope.Complete();
	}

	public void ChequeDelete(int chequeNo)
	{
		if (chequeNo <= 0)
		{
			throw new ArgumentNullException("chequeNo");
		}
		Cheque cheque = _chequeRepository.Get(chequeNo);
		if (cheque == null)
		{
			throw new ApplicationException("Chèque invalide!");
		}
		Chequier chequier = _chequierRepository.Get(cheque.ChequierNo);
		if (chequier == null)
		{
			throw new ApplicationException("Chéquier invalide!");
		}
		if (cheque.Statut != ChequeStatut.NonUtilise)
		{
			throw new ApplicationException("Le chèque est déjà utilisé!");
		}
		chequier.NombreCheque--;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_chequeRepository.Delete(cheque);
		_chequierRepository.Update(chequier);
		transactionScope.Complete();
	}

	public void TraiteDelete(int traiteNo)
	{
		if (traiteNo <= 0)
		{
			throw new ArgumentNullException("traiteNo");
		}
		Traite traite = _traiteRepository.Get(traiteNo);
		if (traite == null)
		{
			throw new ApplicationException("Traite invalide!");
		}
		CarnetTraite carnetTraite = _carnetTraiteRepository.Get(traite.CarnetTraiteNo);
		if (carnetTraite == null)
		{
			throw new ApplicationException("Carnet de traite invalide!");
		}
		if (traite.Statut != ChequeStatut.NonUtilise)
		{
			throw new ApplicationException("La traite est déjà utilisée!");
		}
		carnetTraite.NombreTraite--;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_traiteRepository.Delete(traite);
		_carnetTraiteRepository.Update(carnetTraite);
		_notifyService.Notify(TypeEntity.Traites, traiteNo, TypeAction.Suppression, Societe.No);
		_notifyService.Notify(TypeEntity.CarnetTraite, carnetTraite.No, TypeAction.Modification, Societe.No);
		transactionScope.Complete();
	}

	public IEnumerable<ChequeEntity> ChequeEntityGetAll(int chequeNo, Societe societe = null)
	{
		societe = societe ?? Societe;
		return _chequeEntityRepository.GetAll(societe.No, chequeNo);
	}

	public Cheque ChequeGet(int no)
	{
		if (no <= 0)
		{
			throw new ArgumentNullException("no");
		}
		return _chequeRepository.Get(no);
	}

	public Traite TraiteGet(int no)
	{
		if (no <= 0)
		{
			throw new ArgumentNullException("no");
		}
		return _traiteRepository.Get(no);
	}

	public Cheque ChequeGet(string numero, int chequierNo, Societe societe = null)
	{
		societe = societe ?? Societe;
		if (string.IsNullOrEmpty(numero))
		{
			throw new ArgumentNullException("numero");
		}
		return _chequeRepository.Get(numero, chequierNo, societe.No);
	}

	public IEnumerable<Cheque> ChequeGetAll(int chequierNo, Societe societe = null, params ChequeStatut[] statuts)
	{
		societe = societe ?? Societe;
		if (chequierNo <= 0)
		{
			throw new ArgumentNullException("chequierNo");
		}
		if (statuts.Any())
		{
			return _chequeRepository.GetAll(chequierNo, societe.No, statuts);
		}
		return _chequeRepository.GetAll(chequierNo);
	}

	public IEnumerable<Traite> TraiteGetAll(int carnetTraiteNo, Societe societe = null, params ChequeStatut[] statuts)
	{
		societe = societe ?? Societe;
		if (carnetTraiteNo <= 0)
		{
			throw new ArgumentNullException("carnetTraiteNo");
		}
		if (statuts.Any())
		{
			return _traiteRepository.GetAll(carnetTraiteNo, societe.No, statuts);
		}
		return _traiteRepository.GetAll(carnetTraiteNo);
	}

	public Cheque ChequeGetMigrate(string numero, Societe societe = null)
	{
		societe = societe ?? Societe;
		if (string.IsNullOrEmpty(numero))
		{
			throw new ArgumentNullException("numero");
		}
		return _chequeRepository.Get(numero, societe.No);
	}

	public void ChequeUpdatePlafond(int chequeNo, decimal montant)
	{
		if (chequeNo <= 0)
		{
			throw new ArgumentNullException("chequeNo");
		}
		if ((_chequeRepository.Get(chequeNo) ?? throw new ApplicationException("Chèque invalide!")).Statut != ChequeStatut.NonUtilise)
		{
			throw new ApplicationException("Impossible de modifier le chèque.");
		}
		if (Societe.LegislationType == Legislation.Maroc)
		{
			return;
		}
		if (montant <= 0m)
		{
			throw new ApplicationException("Le montant du plafond doit être supérieur à zéro.");
		}
		if (montant > 30000m)
		{
			throw new ApplicationException("Le montant du plafond ne doit pas dépasser 30 000 dinars.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_chequeRepository.UpdatePlafond(chequeNo, montant);
		_notifyService.Notify(TypeEntity.Cheques, chequeNo, TypeAction.Modification, Societe.No);
		transactionScope.Complete();
	}

	public void ChequeUpdate(int chequeNo, ChequeStatut statut, DateTime echeance, DateTime date, bool isBarre, int tiersNo, decimal montant, string tire)
	{
		if (chequeNo <= 0)
		{
			throw new ArgumentNullException("chequeNo");
		}
		if (tiersNo <= 0)
		{
			throw new ArgumentNullException("tiersNo");
		}
		if (montant < 0m)
		{
			throw new ArgumentException("Montant invalide!");
		}
		if (string.IsNullOrEmpty(tire))
		{
			throw new ArgumentException("Tiré invalide!");
		}
		Cheque cheque = _chequeRepository.Get(chequeNo);
		if (cheque == null)
		{
			throw new ApplicationException("Chèque invalide!");
		}
		if (_chequierRepository.Get(cheque.ChequierNo) == null)
		{
			throw new ApplicationException("Chéquier invalide!");
		}
		if (cheque.Statut == ChequeStatut.Annuler)
		{
			throw new ApplicationException("Le Chèque a été annulé!");
		}
		cheque.Statut = statut;
		cheque.Date = date;
		cheque.Echeance = echeance;
		cheque.IsBarre = isBarre;
		cheque.TiersNo = tiersNo;
		cheque.Montant = montant;
		cheque.Tire = tire;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_chequeRepository.Update(cheque);
		_notifyService.Notify(TypeEntity.Cheques, chequeNo, TypeAction.Modification, Societe.No);
		transactionScope.Complete();
	}

	public void TraiteUpdate(int traiteNo, ChequeStatut statut, DateTime echeance, DateTime date, int tiersNo, decimal montant, string tire)
	{
		if (traiteNo <= 0)
		{
			throw new ArgumentNullException("traiteNo");
		}
		if (tiersNo <= 0)
		{
			throw new ArgumentNullException("tiersNo");
		}
		if (montant < 0m)
		{
			throw new ArgumentException("Montant invalide!");
		}
		if (string.IsNullOrEmpty(tire))
		{
			throw new ArgumentException("Tiré invalide!");
		}
		Traite traite = _traiteRepository.Get(traiteNo);
		if (traite == null)
		{
			throw new ApplicationException("Traite invalide!");
		}
		CarnetTraite carnetTraite = _carnetTraiteRepository.Get(traite.CarnetTraiteNo);
		if (carnetTraite == null)
		{
			throw new ApplicationException("Carnet de traite invalide!");
		}
		if (traite.Statut == ChequeStatut.Annuler)
		{
			throw new ApplicationException("La tratie a été annulé!");
		}
		traite.Statut = statut;
		traite.Date = date;
		traite.Echeance = echeance;
		traite.TiersNo = tiersNo;
		traite.Montant = montant;
		traite.Tire = tire;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_traiteRepository.Update(traite);
		_notifyService.Notify(TypeEntity.Traites, traiteNo, TypeAction.Modification, Societe.No);
		_notifyService.Notify(TypeEntity.CarnetTraite, carnetTraite.No, TypeAction.Modification, Societe.No);
		transactionScope.Complete();
	}

	public int ChequierCreate(int banqueNo, int nombreCheque, string firstCheque, string lastCheque, DateTime date, DateTime? dateValidite = null, decimal? montantPlafond = null, Societe societe = null, bool isInitialisation = false)
	{
		societe = societe ?? Societe;
		if (banqueNo <= 0)
		{
			throw new ArgumentNullException("banqueNo");
		}
		if (string.IsNullOrEmpty(firstCheque))
		{
			throw new ArgumentNullException("firstCheque");
		}
		if (string.IsNullOrEmpty(lastCheque))
		{
			throw new ArgumentNullException("lastCheque");
		}
		if (nombreCheque <= 0 || (nombreCheque > 100 && !isInitialisation))
		{
			throw new ArgumentException("Nombre de chèque est invalide!");
		}
		InformationsBanque byErpNo = _informationsBanqueRepository.GetByErpNo(societe.No, banqueNo);
		if (byErpNo != null && byErpNo.EnSommeil)
		{
			throw new ArgumentException("La banque est en sommeil!");
		}
		if (_chequierRepository.GetAllByBanque(banqueNo, Societe.No).Any((Chequier x) => x.FirstCheque == firstCheque))
		{
			throw new ArgumentException("Chéquier existe déja.");
		}
		if (Societe.LegislationType != Legislation.Maroc)
		{
			if (!dateValidite.HasValue)
			{
				throw new ApplicationException("Date validité invalide.");
			}
			if (!montantPlafond.HasValue)
			{
				throw new ApplicationException("Plafond chèquier invalide.");
			}
			if (dateValidite.Value.Date < DateTime.Now.Date)
			{
				throw new ApplicationException("La date de validation est invalide.");
			}
			decimal? num = montantPlafond;
			if ((num.GetValueOrDefault() <= default(decimal)) & num.HasValue)
			{
				throw new ApplicationException("Le montant du plafond est invalide.");
			}
			num = montantPlafond;
			decimal num2 = 30000;
			if ((num.GetValueOrDefault() > num2) & num.HasValue)
			{
				throw new ApplicationException("Le montant du plafond ne doit pas dépasser 30 000 dinars.");
			}
		}
		Chequier chequier = new Chequier
		{
			Date = date,
			BanqueNo = banqueNo,
			FirstCheque = firstCheque,
			LastCheque = lastCheque,
			NombreCheque = nombreCheque,
			SocieteNo = societe.No,
			DateValiditer = dateValidite,
			MontantPlafond = montantPlafond
		};
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		int num3 = _chequierRepository.Create(chequier);
		if (num3 <= 0)
		{
			throw new InvalidOperationException("Insertion du chéquier invalide!");
		}
		_notifyService.Notify(TypeEntity.Chequier, num3, TypeAction.Ajout, chequier.SocieteNo);
		transactionScope.Complete();
		return num3;
	}

	public int CarnetTraiteCreate(int banqueNo, int nombreTraite, string firstTraite, string lastTraite, DateTime date, Societe societe = null)
	{
		societe = societe ?? Societe;
		if (banqueNo <= 0)
		{
			throw new ArgumentNullException("banqueNo");
		}
		if (string.IsNullOrEmpty(firstTraite))
		{
			throw new ArgumentNullException("firstTraite");
		}
		if (string.IsNullOrEmpty(lastTraite))
		{
			throw new ArgumentNullException("lastTraite");
		}
		if (nombreTraite <= 0 || nombreTraite > 100)
		{
			throw new ArgumentException("Nombre de traite est invalide!");
		}
		InformationsBanque byErpNo = _informationsBanqueRepository.GetByErpNo(societe.No, banqueNo);
		if (byErpNo != null && byErpNo.EnSommeil)
		{
			throw new ArgumentException("La banque est en sommeil!");
		}
		if (_carnetTraiteRepository.GetAllByBanque(banqueNo, Societe.No).Any((CarnetTraite x) => x.FirstTraite == firstTraite))
		{
			throw new ArgumentException("Carnet de traite existe déja.");
		}
		CarnetTraite carnetTraite = new CarnetTraite
		{
			Date = date,
			BanqueNo = banqueNo,
			FirstTraite = firstTraite,
			LastTraite = lastTraite,
			NombreTraite = nombreTraite,
			SocieteNo = societe.No
		};
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		int num = _carnetTraiteRepository.Create(carnetTraite);
		if (num <= 0)
		{
			throw new InvalidOperationException("Insertion du carnet de traite invalide!");
		}
		_notifyService.Notify(TypeEntity.CarnetTraite, num, TypeAction.Ajout, carnetTraite.SocieteNo);
		transactionScope.Complete();
		return num;
	}

	public void ChequierDelete(int chequierNo)
	{
		if (chequierNo <= 0)
		{
			throw new ArgumentNullException("chequierNo");
		}
		Chequier chequier = _chequierRepository.Get(chequierNo);
		if (chequier == null)
		{
			throw new ArgumentException("Chéquier invalide!");
		}
		List<Cheque> list = chequier.GetCheques().ToList();
		if (list.Any((Cheque x) => x.Statut != ChequeStatut.NonUtilise))
		{
			throw new InvalidOperationException("Un ou plusieur chèque du chéquier sont utilisés!");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		foreach (Cheque item in list)
		{
			ChequeDelete(item.No);
		}
		_chequierRepository.Delete(chequier);
		_notifyService.Notify(TypeEntity.Chequier, chequierNo, TypeAction.Suppression, chequier.SocieteNo);
		transactionScope.Complete();
	}

	public void CarnetTaiteDelete(int no)
	{
		if (no <= 0)
		{
			throw new ArgumentNullException("no");
		}
		CarnetTraite carnetTraite = _carnetTraiteRepository.Get(no);
		if (carnetTraite == null)
		{
			throw new ArgumentException("Carnet de traite invalide!");
		}
		List<Traite> list = carnetTraite.GetTraites().ToList();
		if (list.Any((Traite x) => x.Statut != ChequeStatut.NonUtilise))
		{
			throw new InvalidOperationException("Le carnet de traite contient une ou plusieurs traites utilisées !");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		foreach (Traite item in list)
		{
			TraiteDelete(item.No);
		}
		_carnetTraiteRepository.Delete(carnetTraite);
		_notifyService.Notify(TypeEntity.CarnetTraite, no, TypeAction.Suppression, carnetTraite.SocieteNo);
		transactionScope.Complete();
	}

	public Chequier ChequierGet(int no)
	{
		if (no <= 0)
		{
			throw new ArgumentNullException("no");
		}
		return _chequierRepository.Get(no);
	}

	public CarnetTraite CarnetTraiteGet(int no)
	{
		if (no <= 0)
		{
			throw new ArgumentNullException("no");
		}
		return _carnetTraiteRepository.Get(no);
	}

	public IEnumerable<Chequier> ChequierGetAll(int banqueNo, Societe societe = null)
	{
		if (banqueNo <= 0)
		{
			throw new ArgumentNullException("banqueNo");
		}
		societe = societe ?? Societe;
		return _chequierRepository.GetAllByBanque(banqueNo, societe.No);
	}

	public IEnumerable<CarnetTraite> CarnetTraiteGetAll(int banqueNo, Societe societe = null)
	{
		if (banqueNo <= 0)
		{
			throw new ArgumentNullException("banqueNo");
		}
		societe = societe ?? Societe;
		return _carnetTraiteRepository.GetAllByBanque(banqueNo, societe.No);
	}

	public IEnumerable<Chequier> ChequierGetAllBanqueActif(int banqueNo, Societe societe = null)
	{
		if (banqueNo <= 0)
		{
			throw new ArgumentNullException("banqueNo");
		}
		societe = societe ?? Societe;
		return _chequierRepository.GetAllByBanqueActif(banqueNo, societe.No);
	}

	public IEnumerable<CarnetTraite> CarnetTraiteGetAllBanqueActif(int banqueNo, Societe societe = null)
	{
		if (banqueNo <= 0)
		{
			throw new ArgumentNullException("banqueNo");
		}
		societe = societe ?? Societe;
		return _carnetTraiteRepository.GetAllByBanqueActif(banqueNo, societe.No);
	}

	public IEnumerable<Chequier> ChequierGetAll(Societe societe = null)
	{
		societe = societe ?? Societe;
		return _chequierRepository.GetAll(societe.No);
	}

	public IEnumerable<CarnetTraite> CarnetTraiteGetAll(Societe societe = null)
	{
		societe = societe ?? Societe;
		return _carnetTraiteRepository.GetAll(societe.No);
	}

	public IEnumerable<Chequier> ChequierGetAllBanqueActif(Societe societe = null)
	{
		societe = societe ?? Societe;
		return _chequierRepository.GetAllBanqueActif(societe.No);
	}

	public IEnumerable<CarnetTraite> CarnetTraiteGetAllBanqueActif(Societe societe = null)
	{
		societe = societe ?? Societe;
		return _carnetTraiteRepository.GetAllBanqueActif(societe.No);
	}

	public void CloturerRappel(int rappelNo)
	{
		if (rappelNo <= 0)
		{
			throw new ArgumentException("rappelNo");
		}
		Rappel rappel = _rappelRepository.Get(rappelNo);
		if (rappel == null)
		{
			throw new ApplicationException("Impossible de charger le rappel.");
		}
		IEnumerable<LigneRappel> all = _ligneRappelRepository.GetAll(rappelNo);
		if (!all.Any() && rappel.Domaine == DomaineRappel.Recouvrement)
		{
			throw new ApplicationException("Le rappel ne contient aucune ligne.");
		}
		if (rappel.IsCloture)
		{
			throw new ApplicationException("Le rappel est clôturé.");
		}
		rappel.IsCloture = true;
		rappel.DateCloture = DateTime.Now;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		foreach (LigneRappel item in all)
		{
			Echeance echeance = _echeanceRepository.Get(item.EcheanceNo);
			if (echeance == null)
			{
				throw new ApplicationException("Impossible de charger l'échéance.");
			}
			item.SoldeCloture = echeance.SoldeDeviseSociete;
			_ligneRappelRepository.Update(item);
			_notifyService.Notify(TypeEntity.Echeance, echeance.No, TypeAction.Modification, echeance.SocieteNo);
		}
		_rappelRepository.Update(rappel);
		_notifyService.Notify(TypeEntity.Rappel, rappel.No, TypeAction.Modification, rappel.SocieteNo);
		transactionScope.Complete();
	}

	public void Create(Societe societe)
	{
		if (societe == null)
		{
			throw new ArgumentNullException("societe");
		}
		if (Get(societe.RaisonSociale) != null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorSocieteExiste);
		}
		if (!Utilisateur.IsAdmin)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorCreateSociete);
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		if (!_societeRepository.Create(societe).HasValue)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorOperationInvalide);
		}
		transactionScope.Complete();
	}

	public TypeCaution GetTypeCaution(int no)
	{
		return _typeCautionRepository.Get(no);
	}

	public int CreateCaution(Caution caution)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		if (caution == null)
		{
			throw new ArgumentNullException("caution");
		}
		if (caution.Date == DateTime.MinValue)
		{
			throw new ApplicationException("Date de la caution obligatoire!");
		}
		if (caution.Montant == 0m)
		{
			throw new ApplicationException("Montant de la caution obligatoire!");
		}
		if (caution.TierNo == 0)
		{
			throw new ApplicationException("Numéro de client est invalide!");
		}
		if (string.IsNullOrEmpty(caution.Commentaire))
		{
			throw new ApplicationException("Commentaire obligatoire!");
		}
		if (caution.CaisseNo == 0)
		{
			throw new ApplicationException("Numéro de caisse est invalide!");
		}
		if (_caisseManager.Get(caution.CaisseNo) == null)
		{
			throw new ApplicationException($"Impossible de charger la caisse n°[{caution.CaisseNo}]");
		}
		if (caution.TypeCautionNo == 0)
		{
			throw new ApplicationException("Numéro Type de la caution est invalide!");
		}
		TypeCaution typeCaution = _typeCautionRepository.Get(caution.TypeCautionNo);
		if (typeCaution == null)
		{
			throw new ApplicationException($"Impossible de charger le type de la caution n°[{caution.TypeCautionNo}]");
		}
		if (typeCaution.Nature == NatureCaution.Cheque || typeCaution.Nature == NatureCaution.Traite || typeCaution.Nature == NatureCaution.Bancaire)
		{
			if (string.IsNullOrEmpty(caution.NumeroPiece))
			{
				throw new ApplicationException("Numéro de pièce obligatoire!");
			}
			if (caution.DateEcheance == DateTime.MinValue)
			{
				throw new ApplicationException("Date échéance obligatoire!");
			}
			if (string.IsNullOrEmpty(caution.BanqueClient) && caution.Domaine == DomaineCaution.Client)
			{
				throw new ApplicationException("Banque tiers obligatoire!");
			}
			if (caution.Domaine == DomaineCaution.Fournisseur || typeCaution.Nature == NatureCaution.Bancaire)
			{
				if (!caution.BanqueSocieteNo.HasValue)
				{
					throw new ApplicationException(rcRessources.BanqueObligatoireText);
				}
				if ((Groupe.InformationBanqueManager.GetByBanqueId(caution.BanqueSocieteNo.GetValueOrDefault()) ?? throw new ApplicationException($"Impossible de charger la banque [{caution.BanqueSocieteNo.GetValueOrDefault()}]")).EnSommeil)
				{
					throw new ApplicationException("Impossible de créer la caution! La banque est en sommeil!");
				}
			}
			if (string.IsNullOrEmpty(caution.Tire))
			{
				throw new ApplicationException("Tiré obligatoire!");
			}
		}
		if (typeCaution.Nature == NatureCaution.Cheque && caution.Domaine == DomaineCaution.Fournisseur)
		{
			if (!caution.ChequeNo.HasValue)
			{
				throw new ApplicationException("Le numéro de chèque est obligatoire !");
			}
			Cheque cheque = ChequeGet(caution.ChequeNo.Value);
			if (cheque == null)
			{
				throw new ArgumentException("Le Chèque est invalide!");
			}
			if (cheque.Statut != ChequeStatut.NonUtilise && cheque.Statut != ChequeStatut.Reserve)
			{
				throw new ArgumentException("Le statut du Chèque est invalide!");
			}
			if (cheque.Statut == ChequeStatut.Reserve && cheque.TiersNo != caution.TierNo)
			{
				throw new ApplicationException("Le chèque est réservé pour le compte d'un autre fournisseur.");
			}
		}
		Legislation legislationType = Societe.LegislationType;
		if (typeCaution.Nature == NatureCaution.Cheque && legislationType == Legislation.Tunisie)
		{
			if (!caution.MontantPlafond.HasValue)
			{
				throw new ApplicationException("Le montant du plafond obligatoire.");
			}
			decimal montant = caution.Montant;
			decimal? montantPlafond = caution.MontantPlafond;
			if (((montant > montantPlafond.GetValueOrDefault()) & montantPlafond.HasValue) && !caution.IsChequeCertifie)
			{
				throw new ApplicationException("Le montant ne doit pas dépasser le plafond.");
			}
			if (!caution.DateValidite.HasValue)
			{
				throw new ApplicationException("La date de validité obligatoire.");
			}
			if (caution.DateEcheance.Date > caution.DateValidite.Value.Date)
			{
				throw new ApplicationException("La date d'échéance ne doit pas dépasser la date de validité.");
			}
		}
		if (caution.TierNo != 0 && string.IsNullOrEmpty(caution.TierCode))
		{
			throw new ApplicationException("Code tiers obligatoire!");
		}
		if (caution.TierNo != 0 && string.IsNullOrEmpty(caution.TierIntitule))
		{
			throw new ApplicationException("Intitulé tiers obligatoire!");
		}
		caution.Numero = GetNumeroPieceCourante(EntityNumerotation.Caution);
		if (string.IsNullOrEmpty(caution.Numero) || caution.Numero == 0.ToString())
		{
			throw new ApplicationException("Veuillez paramétrer la numérotation des cautions!");
		}
		caution.SocieteNo = Societe.No;
		caution.UtilisateurNo = Utilisateur.No;
		caution.DateCreation = DateTime.Now;
		caution.Statut = StatutCaution.None;
		caution.DateStatut = DateTime.Now;
		int num = _cautionRepository.Create(caution);
		_notifyService.Notify(TypeEntity.Caution, num, TypeAction.Ajout, Societe.No);
		return num;
	}

	public int CreateDossierReglement(DomaineDossier domaine, string numero, DateTime date, int tiersNo, string tiersCode, string tiersIntitule, TiersType tiersType, string beneficiaire, string libelle, int caisseNo, decimal montant, decimal montantEcart, string identifiantBeneficiaire, NatureFournisseur natureBeneficiaire, int deviseNo, decimal cours, StatutDossierReglement statut, bool isDossierCommercial = false, Societe societe = null, int? attestationRetenueNo = null)
	{
		societe = societe ?? Societe;
		if (string.IsNullOrEmpty(numero))
		{
			throw new ApplicationException("Numéro dossier invalide!");
		}
		if (_dossierReglementRepository.GetDossier(domaine, numero, societe.No) != null)
		{
			throw new ApplicationException("Dossier existe déjà!");
		}
		if (!isDossierCommercial && (date.Date < Exercice.Debut.Date || date.Date > Exercice.Fin.Date))
		{
			throw new ApplicationException("La date comptabilisation du dossier doit être compris entre la date début et la date fin de l'exercice courant.");
		}
		if (isDossierCommercial && tiersType != TiersType.Fournisseur && domaine == DomaineDossier.Fournisseur)
		{
			throw new ApplicationException($"La création d'un dossier fournisseur ne supprote pas le type tiers [{tiersType}].");
		}
		if (isDossierCommercial && tiersType != TiersType.Client && domaine == DomaineDossier.Client)
		{
			throw new ApplicationException($"La création d'un dossier client ne supprote pas le type tiers [{tiersType}].");
		}
		if (tiersNo <= 0)
		{
			throw new ApplicationException("tiers invalide!");
		}
		if (string.IsNullOrEmpty(beneficiaire))
		{
			throw new ApplicationException("Beneficiaire invalide!");
		}
		if (string.IsNullOrEmpty(libelle))
		{
			throw new ApplicationException("Libelle dossier invalide!");
		}
		if (montant < 0m)
		{
			throw new ArgumentException("Le montant du dossier est invalide!");
		}
		if (_caisseManager.Get(caisseNo) == null)
		{
			throw new ArgumentException("Caisse invalide!");
		}
		if (attestationRetenueNo.HasValue && _attestationRetenueTiersRepository.GetAsync(attestationRetenueNo.Value).ConfigureAwait(continueOnCapturedContext: false).GetAwaiter()
			.GetResult() == null)
		{
			throw new ApplicationException("Impossible de charger l'attestation retenue.");
		}
		DeclarationRetenuSource declarationRetenuSource = _declarationRetenuSourceRepository.Get(Societe.No, date.Year, (DeclarationMoisPeriode)date.Month);
		if (declarationRetenuSource != null && declarationRetenuSource.Statut == StatutDeclaration.Cloture)
		{
			throw new ApplicationException("Le dossier est inclus dans une période de déclaration RAS clôturée.");
		}
		DossierReglement dossierReglement = new DossierReglement
		{
			Domaine = domaine,
			Numero = numero,
			Beneficiaire = beneficiaire,
			Date = date,
			TiersNo = tiersNo,
			Libelle = libelle,
			SocieteNo = societe.No,
			TiersType = tiersType,
			Lettrage = LettrageType.NonLettre,
			CaisseNo = caisseNo,
			Montant = montant,
			MontantEcart = montantEcart,
			UtilisateurNo = Utilisateur.No,
			IdentifiantBeneficiaire = identifiantBeneficiaire,
			NatureBeneficiaire = natureBeneficiaire,
			DeviseNo = deviseNo,
			Cours = cours,
			TiersCode = tiersCode,
			TiersIntitule = tiersIntitule,
			StatutDossier = statut,
			IsDossierCommercial = isDossierCommercial,
			AttestationRetenuNo = attestationRetenueNo,
			PhaseDossier = PhaseDossierReglement.ValidationFacture
		};
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		int num = _dossierReglementRepository.Create(dossierReglement);
		if (_licenceApplicationVersion.Application == ApplicationRunning.TresoClient)
		{
			_notifyService.Notify(TypeEntity.DossierClient, num, TypeAction.Ajout, dossierReglement.SocieteNo);
		}
		else
		{
			_notifyService.Notify(TypeEntity.Dossier, num, TypeAction.Ajout, dossierReglement.SocieteNo);
		}
		transactionScope.Complete();
		return num;
	}

	public void CreateGridFilter(string name, byte[] layout, Guid guid, Utilisateur utilisateur = null)
	{
		utilisateur = utilisateur ?? Utilisateur;
		if (string.IsNullOrEmpty(name))
		{
			throw new ArgumentException("Le nom du filtre est invalide!");
		}
		if (layout == null)
		{
			throw new ArgumentNullException("layout");
		}
		if (guid == Guid.Empty)
		{
			throw new ArgumentException("Le guid du grid est invalide!");
		}
		if (_gridLayoutFilterRepository.Get(name, guid) != null)
		{
			throw new ApplicationException("Le filtre exist déjà!");
		}
		GridLayoutFilter gridLayoutFilter = new GridLayoutFilter
		{
			Guid = guid,
			Name = name,
			UtilisateurNo = utilisateur.No,
			Layout = layout
		};
		_gridLayoutFilterRepository.Create(gridLayoutFilter);
	}

	public void CreateGridLayout(string name, Guid guid, byte[] layout, Utilisateur utilisateur = null)
	{
		utilisateur = utilisateur ?? Utilisateur;
		if (string.IsNullOrEmpty(name))
		{
			throw new ArgumentNullException("name");
		}
		if (guid == Guid.Empty)
		{
			throw new ArgumentNullException("guid");
		}
		if (layout == null)
		{
			throw new ArgumentNullException("layout");
		}
		if (_gridLayoutRepository.Get(name, guid) != null)
		{
			throw new ApplicationException("Layout existe déjà!");
		}
		GridLayout gridLayout = new GridLayout
		{
			Guid = guid,
			UtilisateurNo = utilisateur.No,
			Name = name,
			Layout = layout
		};
		_gridLayoutRepository.Create(gridLayout);
	}

	public int CreateLigneRappel(int rappelNo, int echeanceNo)
	{
		Rappel rappel = _rappelRepository.Get(rappelNo);
		if (rappel == null)
		{
			throw new ApplicationException("Impossible de charger le rappel.");
		}
		if (rappel.IsCloture)
		{
			throw new ApplicationException("Le rappel est clôturé.");
		}
		Echeance echeance = _echeanceRepository.Get(echeanceNo);
		if (echeance == null)
		{
			throw new ApplicationException("Impossible de charger l'échéance.");
		}
		if (_ligneRappelRepository.Get(rappelNo, echeanceNo) != null)
		{
			throw new ApplicationException("L'échéance déjà inclue dans ce rappel.");
		}
		if (_ligneRappelRepository.EcheanceInclueRappelNonCloture(echeanceNo) && rappel.Domaine != DomaineRappel.Email)
		{
			throw new ApplicationException("L'échéance [" + echeance.DocumentNumero + "] est inclue dans un rappel non encore clôturé.");
		}
		LigneRappel ligne = new LigneRappel
		{
			RappelNo = rappelNo,
			EcheanceNo = echeanceNo,
			SoldeRappel = echeance.SoldeDeviseSociete,
			DateCreation = DateTime.Now,
			SoldeCloture = 0m
		};
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		int num = _ligneRappelRepository.Create(ligne);
		if (num <= 0)
		{
			throw new ApplicationException("Impossible de créer la ligne rappel.");
		}
		int no = 0;
		if (Societe.GenererNoteRappel)
		{
			Note note = new Note
			{
				EntiteNo = echeance.No,
				EntiteType = TypeEntity.Echeance,
				SocieteNo = Societe.No,
				Text = rappel.Note,
				UtilisateurNo = Utilisateur.No,
				Date = DateTime.Now.Date
			};
			no = _noteRepository.Create(note);
		}
		_notifyService.Notify(TypeEntity.Echeance, echeance.No, TypeAction.Modification, echeance.SocieteNo);
		_notifyService.Notify(TypeEntity.Note, no, TypeAction.Ajout, Societe.No);
		transactionScope.Complete();
		return num;
	}

	public int CreateRappel(string numero, DomaineRappel domaine, int clientNo, string clientNumero, string clientIntitule, DateTime date, string note, bool isRelance = false, int? parentNo = null)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		if (string.IsNullOrEmpty(numero))
		{
			throw new ArgumentNullException("numero");
		}
		if (clientNo <= 0)
		{
			throw new ApplicationException("Client invalide.[no]");
		}
		if (string.IsNullOrEmpty(clientNumero))
		{
			throw new ApplicationException("Client invalide.[numero]");
		}
		if (string.IsNullOrEmpty(clientIntitule))
		{
			throw new ApplicationException("Client invalide.[intitule]");
		}
		if (date.Date < DateTime.Now.Date)
		{
			throw new ApplicationException("Date du rappel est invalide.");
		}
		if (string.IsNullOrEmpty(note))
		{
			throw new ApplicationException("La note du rappel est invalide.");
		}
		if (_rappelRepository.Get(Societe.No, numero) != null)
		{
			throw new ApplicationException("Le numéro du rappel existe déjà.");
		}
		string parentNumero = string.Empty;
		if (isRelance)
		{
			if (!parentNo.HasValue)
			{
				throw new ApplicationException("Le rappel parent est invalide.");
			}
			Rappel rappel = _rappelRepository.Get(parentNo.Value);
			if (rappel == null)
			{
				throw new ApplicationException("Impossible de charger le rappel parent.");
			}
			if (!rappel.IsCloture)
			{
				throw new ApplicationException("Le rappel [" + rappel.Numero + "] n'est pas clôturé.");
			}
			parentNumero = rappel.Numero;
		}
		Rappel rappel2 = new Rappel
		{
			Numero = numero,
			ClientNo = clientNo,
			Date = date,
			IsRelance = isRelance,
			ParentNo = parentNo,
			Domaine = domaine,
			Note = note,
			SocieteNo = Societe.No,
			UtilisateurNo = Utilisateur.No,
			DateCreation = DateTime.Now,
			IsCloture = false,
			DateCloture = DateTime.Now,
			ParentNumero = parentNumero,
			ClientNumero = clientNumero,
			ClientIntitule = clientIntitule
		};
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		int num = _rappelRepository.Create(rappel2);
		if (num <= 0)
		{
			throw new ApplicationException("Impossible de crée le rappel.");
		}
		_notifyService.Notify(TypeEntity.Rappel, num, TypeAction.Ajout, rappel2.SocieteNo);
		transactionScope.Complete();
		return num;
	}

	public void CreateUtilisateurGrid(int layoutNo, Guid guid, Utilisateur utilisateur = null)
	{
		utilisateur = utilisateur ?? Utilisateur;
		if (layoutNo <= 0)
		{
			throw new ArgumentNullException("layoutNo");
		}
		GridLayout gridLayout = _gridLayoutRepository.Get(layoutNo);
		if (gridLayout == null)
		{
			throw new ArgumentNullException("layout");
		}
		if (guid == Guid.Empty)
		{
			throw new ArgumentException("guid");
		}
		UtilisateurGrid utilisateurGrid = new UtilisateurGrid
		{
			Guid = guid,
			UtilisateurNo = utilisateur.No,
			GridLayoutNo = gridLayout.No
		};
		_utilisateurGridRepository.Create(utilisateurGrid);
	}

	public void Delete(int societeNo)
	{
		Societe societe = Get(societeNo);
		if (societe == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorSocieteNo);
		}
		if (!Utilisateur.IsAdmin)
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorDeleteSociete, societe.RaisonSociale));
		}
		if (societe.No == 1)
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorSocieteSuppression, societe.RaisonSociale));
		}
		if (societe.GetCaisses().Any())
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorSocieteCaisse);
		}
		if (societe.No == Societe.No)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorSocieteCourante);
		}
		if (_notifyService.GetAll().Any((Notification x) => x.SocieteNo == societeNo))
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorSocieteEnExecution);
		}
		_societeRepository.Delete(societe);
	}

	public void DeleteCaution(Caution caution)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		if (caution == null)
		{
			throw new ArgumentNullException("caution");
		}
		if (caution.No == 0)
		{
			throw new ApplicationException("Numéro caution est invalide!");
		}
		Caution caution2 = _cautionRepository.Get(caution.No);
		if (caution2 == null)
		{
			throw new ApplicationException($"Impossible de charger le numéro de la caution n°[{caution.No}]");
		}
		int valueOrDefault = caution2.ReglementNo.GetValueOrDefault();
		if (caution2.Statut == StatutCaution.Reglement && valueOrDefault > 0)
		{
			throw new ApplicationException("La caution n° [" + caution2.Numero + "] est relié à un règlement");
		}
		if (caution2.Statut == StatutCaution.Recupere)
		{
			throw new ApplicationException("La caution n°[" + caution.Numero + "] est récupérée!");
		}
		if (caution2.Statut != StatutCaution.None)
		{
			throw new ApplicationException("Le statut du caution n°[" + caution.Numero + "][" + caution2.Statut.GetDisplayDescription() + "] ne permet pas la suppression !");
		}
		_cautionRepository.Delete(caution2);
		_notifyService.Notify(TypeEntity.Caution, caution2.No, TypeAction.Suppression, Societe.No);
	}

	public void DeleteDossierReglementCom(int dossierNo)
	{
		if (dossierNo <= 0)
		{
			throw new ArgumentNullException("dossierNo");
		}
		if ((GetDossierReglement(dossierNo) ?? throw new ApplicationException("Impossible de charger le dossier de règlement.")).StatutDossier != StatutDossierReglement.Encours)
		{
			throw new ApplicationException("Le statut du dossier est invalide.");
		}
		IEnumerable<LigneDossierReglementComm> enumerable = LigneDossierReglementCommGetAll(dossierNo);
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		bool flag = _licenceApplicationVersion.Application == ApplicationRunning.TresoClient;
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		foreach (LigneDossierReglementComm item in enumerable)
		{
			switch (item.TypeLigneDossier)
			{
			case TypeLigneDossier.Echeance:
				EcheanceAnnulerReservation(item.EntityNo);
				break;
			case TypeLigneDossier.Reglement:
				if (_licenceApplicationVersion.Application == ApplicationRunning.TresoClient)
				{
					CaisseManager.ReglementClientAnnulerReservation(item.EntityNo);
				}
				else
				{
					CaisseManager.ReglementFournisseurAnnulerReservation(item.EntityNo);
				}
				break;
			}
			if (_ligneDossierReglementCommRepository.Get(item.No) == null)
			{
				throw new ApplicationException("Impossible de charger la ligne.");
			}
		}
		if (flag)
		{
			DeleteDossierReglementClient(dossierNo);
		}
		else
		{
			DeleteDossierReglementFournisseur(dossierNo);
		}
		transactionScope.Complete();
	}

	public bool CanDecomptabiliserLigneDossierReglementFournisseurComm(int entityNo, TypeLigneDossier typeLigneDossier, Societe societe = null)
	{
		societe = societe ?? Societe;
		return _ligneDossierReglementCommRepository.CanDecompatabiliser(entityNo, typeLigneDossier, societe.No);
	}

	public bool CanDecomptabiliserLigneDossierReglementClientComm(int entityNo, TypeLigneDossier typeLigneDossier, Societe societe = null)
	{
		societe = societe ?? Societe;
		return _ligneDossierReglementCommRepository.CanDecompatabiliser(entityNo, typeLigneDossier, societe.No);
	}

	public void DeleteDossierReglementFournisseur(int dossierNo)
	{
		if (dossierNo <= 0)
		{
			throw new ArgumentNullException("dossierNo");
		}
		DossierReglement dossier = _dossierReglementRepository.GetDossier(dossierNo);
		if (dossier == null)
		{
			throw new ApplicationException("dossier");
		}
		if (dossier.IsCloture)
		{
			throw new InvalidOperationException("Le dossier [" + dossier.Numero + "] est cloturé!");
		}
		if (dossier.Lettrage != LettrageType.NonLettre)
		{
			throw new InvalidOperationException("Le dossier [" + dossier.Numero + "] a subit une opération de lettrage!");
		}
		if (dossier.IsComptabilise)
		{
			throw new InvalidOperationException("Le dossier [" + dossier.Numero + "] est comptabilisé!");
		}
		List<LigneDossierReglement> list = _ligneDossierReglementRepository.GetAll(dossierNo).ToList();
		List<LigneDossierReglement> source = list.Where((LigneDossierReglement x) => x.Type == TypeLigneDossier.Reglement).ToList();
		List<LigneDossierReglement> source2 = list.Where((LigneDossierReglement x) => x.Type == TypeLigneDossier.Retenue).ToList();
		if (source.Where(delegate(LigneDossierReglement x)
		{
			ReglementFournisseur reglementFournisseur = CaisseManager.ReglementFournisseurGet(x.No);
			if (reglementFournisseur == null)
			{
				throw new ApplicationException("Impossible de charger le règlement.");
			}
			return reglementFournisseur.Type == ReglementType.Cheque || reglementFournisseur.Type == ReglementType.Traite;
		}).Any((LigneDossierReglement x) => x.IsRecuperer))
		{
			throw new ApplicationException("Un ou plusieurs règlements sont récupérés.");
		}
		if (source2.Any((LigneDossierReglement x) => x.IsRecuperer))
		{
			throw new ApplicationException("Une ou plusieurs retenues sont récupérées.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		foreach (LigneDossierReglement item in list)
		{
			switch (item.Type)
			{
			case TypeLigneDossier.Ecriture:
				_ecritureCompta.DeleteLigneEcritureFrs(item.No);
				break;
			case TypeLigneDossier.Retenue:
			case TypeLigneDossier.Reglement:
				CaisseManager.ReglementFournisseurDelete(item.No, dossier.IsDossierCommercial);
				_notifyService.Notify(TypeEntity.ReglementFournisseur, item.No, TypeAction.Suppression, dossier.SocieteNo);
				break;
			}
		}
		_dossierReglementRepository.Delete(dossierNo);
		_notifyService.Notify(TypeEntity.Dossier, dossierNo, TypeAction.Suppression, dossier.SocieteNo);
		transactionScope.Complete();
	}

	public void DeleteDossierReglementClient(int dossierNo)
	{
		if (dossierNo <= 0)
		{
			throw new ArgumentNullException("dossierNo");
		}
		DossierReglement dossier = _dossierReglementRepository.GetDossier(dossierNo);
		if (dossier == null)
		{
			throw new ApplicationException("dossier");
		}
		if (dossier.IsCloture)
		{
			throw new InvalidOperationException("Le dossier [" + dossier.Numero + "] est cloturé!");
		}
		if (dossier.Lettrage != LettrageType.NonLettre)
		{
			throw new InvalidOperationException("Le dossier [" + dossier.Numero + "] a subit une opération de lettrage!");
		}
		if (dossier.IsComptabilise)
		{
			throw new InvalidOperationException("Le dossier [" + dossier.Numero + "] est comptabilisé!");
		}
		List<LigneDossierReglement> list = _ligneDossierReglementRepository.GetAll(dossierNo).ToList();
		List<LigneDossierReglement> source = list.Where((LigneDossierReglement x) => x.Type == TypeLigneDossier.Reglement).ToList();
		List<LigneDossierReglement> source2 = list.Where((LigneDossierReglement x) => x.Type == TypeLigneDossier.Retenue).ToList();
		if (source.Where(delegate(LigneDossierReglement x)
		{
			ReglementClient reglementClient = CaisseManager.ReglementGet(x.No);
			if (reglementClient == null)
			{
				throw new ApplicationException("Impossible de charger le règlement.");
			}
			return reglementClient.Type == ReglementType.Cheque || reglementClient.Type == ReglementType.Traite;
		}).Any((LigneDossierReglement x) => x.IsRecuperer))
		{
			throw new ApplicationException("Un ou plusieurs règlements sont récupérés.");
		}
		if (source2.Any((LigneDossierReglement x) => x.IsRecuperer))
		{
			throw new ApplicationException("Une ou plusieurs retenues sont récupérées.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		foreach (LigneDossierReglement item in list)
		{
			TypeLigneDossier type = item.Type;
			if (type != TypeLigneDossier.Ecriture && (uint)(type - 1) <= 1u)
			{
				CaisseManager.ReglementClientDelete(item.No);
				_notifyService.Notify(TypeEntity.Reglement, item.No, TypeAction.Suppression, dossier.SocieteNo);
			}
		}
		_dossierReglementRepository.Delete(dossierNo);
		_notifyService.Notify(TypeEntity.DossierClient, dossierNo, TypeAction.Suppression, dossier.SocieteNo);
		transactionScope.Complete();
	}

	public void ComptabiliserFactureGr(int echeanceNo, IList<EcritureComptable> collections)
	{
		Echeance echeance = EcheanceGet(echeanceNo);
		if (echeance == null)
		{
			throw new ApplicationException("Impossible de charger l'échéance !");
		}
		foreach (EcritureComptable collection in collections)
		{
			_ecritureCompta.UpdateEcriture(collection);
		}
		foreach (Echeance item in EcheanceGetAllByDocument(echeance.Domaine, echeance.DocumentNumero).ToList())
		{
			item.IsComptabilise = true;
			item.UtilisateurNo = Utilisateur.No;
			_echeanceRepository.Update(item);
			_notifyService.Notify(TypeEntity.Echeance, item.No, TypeAction.Modification, Societe.No);
		}
	}

	public void DecomptabiliserFactureGr(int echeanceNo, IList<EcritureComptable> collections)
	{
		Echeance echeance = EcheanceGet(echeanceNo);
		if (echeance == null)
		{
			throw new ApplicationException("Impossible de charger l'échéance !");
		}
		if (echeance.IsFactureGrFromComptaErp)
		{
			throw new ApplicationException("Cette échéance a été importée depuis l'ERP.");
		}
		foreach (EcritureComptable collection in collections)
		{
			collection.ErpNo = 0;
			_ecritureCompta.UpdateEcriture(collection);
		}
		foreach (Echeance item in EcheanceGetAllByDocument(echeance.Domaine, echeance.DocumentNumero).ToList())
		{
			item.IsComptabilise = false;
			item.UtilisateurNo = Utilisateur.No;
			_echeanceRepository.Update(item);
			_notifyService.Notify(TypeEntity.Echeance, item.No, TypeAction.Modification, Societe.No);
		}
	}

	public void DeleteGridFilter(string name, Guid guid)
	{
		if (string.IsNullOrEmpty(name))
		{
			throw new ArgumentException("Le nom du filtre est invalide!");
		}
		if (guid == Guid.Empty)
		{
			throw new ArgumentException("Le guid du grid est invalide!");
		}
		if (_gridLayoutFilterRepository.Get(name, guid) == null)
		{
			throw new ApplicationException("Le filtre est invalide!");
		}
		_gridLayoutFilterRepository.Delete(name, guid);
	}

	public void DeleteGridLayout(int no)
	{
		if (no <= 0)
		{
			throw new ArgumentNullException("no");
		}
		GridLayout gridLayout = _gridLayoutRepository.Get(no);
		if (gridLayout == null)
		{
			throw new ApplicationException("Layout invalide!");
		}
		_gridLayoutRepository.Delete(gridLayout);
	}

	public void DeleteLigneRappel(int ligneRappelNo)
	{
		LigneRappel ligneRappel = _ligneRappelRepository.Get(ligneRappelNo);
		if (ligneRappel == null)
		{
			throw new ApplicationException("Impossible de charger la ligne rappel.");
		}
		if ((_rappelRepository.Get(ligneRappel.RappelNo) ?? throw new ApplicationException("Impossible de charger le rappel.")).IsCloture)
		{
			throw new ApplicationException("Le rappel est clôturé.");
		}
		Echeance echeance = _echeanceRepository.Get(ligneRappel.EcheanceNo);
		if (echeance == null)
		{
			throw new ApplicationException("Impossible de charger l'échéance.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_ligneRappelRepository.Delete(ligneRappel);
		_notifyService.Notify(TypeEntity.Echeance, echeance.No, TypeAction.Modification, echeance.SocieteNo);
		transactionScope.Complete();
	}

	public void DeleteRappel(int rappelNo)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		if (rappelNo <= 0)
		{
			throw new ArgumentException("rappelNo");
		}
		Rappel rappel = _rappelRepository.Get(rappelNo);
		if (rappel == null)
		{
			throw new ApplicationException("Impossible de charger le rappel.");
		}
		if (rappel.IsCloture)
		{
			throw new ApplicationException("Le rappel est clôturé.");
		}
		if (_rappelRepository.GetByParentId(Societe.No, rappelNo).Any())
		{
			throw new ApplicationException("Le rappel est un parent d'un autre rappel.");
		}
		IEnumerable<LigneRappel> all = _ligneRappelRepository.GetAll(rappelNo);
		if (!all.Any() && rappel.Domaine == DomaineRappel.Recouvrement)
		{
			throw new ApplicationException("Le rappel de contient aucune ligne.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_rappelRepository.Delete(rappel);
		_notifyService.Notify(TypeEntity.Rappel, rappel.No, TypeAction.Suppression, rappel.SocieteNo);
		foreach (LigneRappel item in all)
		{
			_notifyService.Notify(TypeEntity.Echeance, item.EcheanceNo, TypeAction.Modification, rappel.SocieteNo);
		}
		transactionScope.Complete();
	}

	public bool DeviseCanBeModified(int deviseNo, Societe societe = null)
	{
		if (deviseNo <= 0)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorDeviseNo);
		}
		societe = societe ?? Societe;
		SocieteDevise devise = societe.GetDevise(deviseNo);
		if (devise == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorDeviseNo);
		}
		return !EcheanceExiste(devise);
	}

	public SocieteDevise GetSocieteDevise(int deviseNo, Societe societe = null)
	{
		if (deviseNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorDeviseNo);
		}
		societe = societe ?? Societe;
		return _societeDeviseRepository.Get(societe.No, deviseNo);
	}

	public void DeviseCreate(int deviseNo, int erpNo, Societe societe = null)
	{
		if (deviseNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorDeviseNo);
		}
		if (erpNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorErpNo);
		}
		if (!Utilisateur.IsAdmin)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorCreateDevise);
		}
		societe = societe ?? Societe;
		if (societe.HasDevise(deviseNo))
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorDeviseNo);
		}
		if (DeviseErpHasMapping(erpNo, societe))
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorDeviseCorrespondance);
		}
		Devise devise = Groupe.DeviseManager.Get(deviseNo);
		if (devise == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorDeviseNo);
		}
		SocieteDevise devise2 = societe.InitNewDeviseSociete(devise, erpNo);
		_societeDeviseRepository.Create(devise2);
		societe.RefreshDevise();
		_notifyService.Notify(TypeEntity.Parametrage, 0, TypeAction.Modification, societe.No);
	}

	public void DeviseDelete(int deviseNo, Societe societe = null)
	{
		if (deviseNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorDeviseNo);
		}
		societe = societe ?? Societe;
		SocieteDevise devise = societe.GetDevise(deviseNo);
		if (devise == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorDeviseNo);
		}
		if (!Utilisateur.IsAdmin)
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorDeleteDevise, devise.Code));
		}
		if (!DeviseCanBeModified(deviseNo, societe))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorDeviseUtilisee, devise.Code));
		}
		_societeDeviseRepository.Delete(devise);
		societe.RefreshDevise();
		_notifyService.Notify(TypeEntity.Parametrage, 0, TypeAction.Modification, societe.No);
	}

	public bool DeviseErpHasMapping(int erpNo, Societe societe = null)
	{
		societe = societe ?? Societe;
		return _societeDeviseRepository.HasErpMapping(erpNo, societe.No) != null;
	}

	public SocieteDevise GetDeviseErpMapping(int erpNo, Societe societe = null)
	{
		societe = societe ?? Societe;
		return _societeDeviseRepository.HasErpMapping(erpNo, societe.No);
	}

	public void DeviseUpdate(int deviseNo, int erpNo, Societe societe = null)
	{
		if (deviseNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorDeviseNo);
		}
		if (erpNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorErpNo);
		}
		societe = societe ?? Societe;
		SocieteDevise devise = societe.GetDevise(deviseNo);
		if (devise == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorDeviseNo);
		}
		if (!Utilisateur.IsAdmin)
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorUpdateDevise, devise.Code));
		}
		if (!DeviseCanBeModified(deviseNo, societe))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorDeviseUtilisee, devise.Code));
		}
		if (DeviseErpHasMapping(erpNo, societe))
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorDeviseCorrespondance);
		}
		devise.ErpNo = erpNo;
		_societeDeviseRepository.Update(devise);
		societe.RefreshDevise();
		_notifyService.Notify(TypeEntity.Parametrage, 0, TypeAction.Modification, societe.No);
	}

	public void Disconnect()
	{
		Societe = null;
		Utilisateur = null;
	}

	public void EcartAjustementGainCreate(int reglementNo, int deviseSocieteNo)
	{
		if (reglementNo < 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorReglement);
		}
		ReglementClient reglementClient = CaisseManager.ReglementGet(reglementNo);
		if (reglementClient == null)
		{
			throw new ApplicationException("Impossible de charger le règlement!");
		}
		if (reglementClient.DeviseNo != deviseSocieteNo)
		{
			throw new ApplicationException("Opération invalide! le règlement est en devise!");
		}
		if (reglementClient.Solde == 0m)
		{
			return;
		}
		if (reglementClient.Solde > Societe.MaxGain)
		{
			throw new ApplicationException("Le reste à payer du règlement est supérieur au max. gain autorisé!");
		}
		DateTime dateTime = DateTime.Now;
		switch (Societe.DateAjustement)
		{
		case TypeDateAjustement.DateEcheance:
			dateTime = reglementClient.Date;
			break;
		case TypeDateAjustement.DateOperation:
			dateTime = DateTime.Now;
			break;
		case TypeDateAjustement.DateReglement:
			dateTime = reglementClient.Date;
			break;
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		CaisseManager.ReglementAjuster(reglementClient);
		int num = EcheanceCreate(0, reglementClient.Numero, ErpDomaine.Vente, ErpDocumentType.None, dateTime.Date, reglementClient.ClientNo, reglementClient.ClientCode, reglementClient.ClientIntitule, reglementClient.ClientNo, reglementClient.ClientCode, reglementClient.ClientIntitule, reglementClient.DeviseNo, reglementClient.DeviseCours, reglementClient.Solde, reglementClient.Date, reglementClient.ModeReglementNo, 0, 0, EcheanceType.Gain, $"Gain sur le reglement {reglementClient.Numero}", deviseSocieteNo, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, reglementClient.No);
		if (num <= 0)
		{
			throw new ApplicationException("L'écart de gain est invalide!");
		}
		CaisseManager.Imputer(reglementNo, num, reglementClient.Solde, deviseSocieteNo, isDossierImpaye: false);
		transactionScope.Complete();
	}

	public void EcartAjustementPertGrfCreate(int reglementNo, int deviseSocieteNo)
	{
		if (reglementNo < 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorReglement);
		}
		ReglementFournisseur reglementFournisseur = CaisseManager.ReglementFournisseurGet(reglementNo);
		if (reglementFournisseur == null)
		{
			throw new ApplicationException("Impossible de charger le règlement!");
		}
		if (reglementFournisseur.DeviseNo != deviseSocieteNo)
		{
			throw new ApplicationException("Opération invalide! le règlement est en devise!");
		}
		if (reglementFournisseur.Solde == 0m)
		{
			return;
		}
		if (reglementFournisseur.Solde > Societe.MaxPerteFrs)
		{
			throw new ApplicationException("Le reste à payer du règlement est supérieur au max. perte autorisé!");
		}
		DateTime dateTime = DateTime.Now;
		switch (Societe.DateAjustementFrs)
		{
		case TypeDateAjustement.DateEcheance:
			dateTime = reglementFournisseur.Date;
			break;
		case TypeDateAjustement.DateOperation:
			dateTime = DateTime.Now;
			break;
		case TypeDateAjustement.DateReglement:
			dateTime = reglementFournisseur.Date;
			break;
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		CaisseManager.ReglementFournisseurAjuster(reglementFournisseur);
		int num = EcheanceCreate(0, reglementFournisseur.Numero, ErpDomaine.Achat, ErpDocumentType.None, dateTime.Date, reglementFournisseur.FournisseurNo, reglementFournisseur.FournisseurCode, reglementFournisseur.FournisseurIntitule, reglementFournisseur.FournisseurNo, reglementFournisseur.FournisseurCode, reglementFournisseur.FournisseurIntitule, reglementFournisseur.DeviseNo, reglementFournisseur.DeviseCours, reglementFournisseur.Solde, reglementFournisseur.Date, reglementFournisseur.ModeReglementNo, 0, 0, EcheanceType.Perte, $"Perte sur le reglement {reglementFournisseur.Numero}", deviseSocieteNo, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, reglementFournisseur.No);
		if (num <= 0)
		{
			throw new ApplicationException("L'écart de perte est invalide!");
		}
		CaisseManager.ReglementFournisseurImputer(reglementNo, num, reglementFournisseur.Solde, deviseSocieteNo, isDossierImpaye: false);
		transactionScope.Complete();
	}

	public void EcartAjustementPerteCreate(int echeanceNo, int deviseSocieteNo)
	{
		if (echeanceNo < 0)
		{
			throw new ArgumentException("Echéance No est invalide!");
		}
		Echeance echeance = EcheanceGet(echeanceNo);
		if (echeance == null)
		{
			throw new ApplicationException("Impossible de charger l'échéance!");
		}
		if (echeance.DeviseNo != deviseSocieteNo)
		{
			throw new ApplicationException("Opération invalide! L'échéance [" + echeance.DocumentNumero + "] est en devise!");
		}
		if (echeance.Solde == 0m)
		{
			return;
		}
		if (echeance.Solde > Societe.MaxPerte)
		{
			throw new ApplicationException("Le reste à payer de l'échéance [" + echeance.DocumentNumero + "] est supérieur au max. perte autorisé!");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		Affectation affectation = (from x in echeance.GetAffectations()
			orderby x.No
			select x).FirstOrDefault((Affectation x) => x.Montant > 0m);
		if (affectation == null)
		{
			throw new ApplicationException("Opération invalide! L'échéance [" + echeance.DocumentNumero + "] ne possède aucune affectation!");
		}
		ReglementClient reglementClient = CaisseManager.ReglementGet(affectation.ReglementNo);
		if (reglementClient == null)
		{
			throw new ApplicationException("Impossible de charger le règlement de l'échéance [" + echeance.DocumentNumero + "].");
		}
		DateTime documentDate = DateTime.Now;
		switch (Societe.DateAjustement)
		{
		case TypeDateAjustement.DateEcheance:
			documentDate = echeance.DocumentDate;
			break;
		case TypeDateAjustement.DateOperation:
			documentDate = DateTime.Now;
			break;
		case TypeDateAjustement.DateReglement:
			documentDate = reglementClient.Date;
			break;
		}
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		int num = EcheanceCreate(0, echeance.DocumentNumero, ErpDomaine.Vente, ErpDocumentType.None, documentDate, echeance.ClientNo, echeance.ClientCode, echeance.ClientIntitule, echeance.PayeurNo, echeance.PayeurCode, echeance.PayeurIntitule, echeance.DeviseNo, echeance.CoursDevise, echeance.Solde * -1m, echeance.Date, echeance.ModeReglementNo, echeance.SoucheNo, echeance.CollaborateurNo, EcheanceType.Perte, "Perte sur la facture " + echeance.DocumentNumero, deviseSocieteNo, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, echeance.No);
		CaisseManager.Imputer(affectation.ReglementNo, num, echeance.Solde * -1m, deviseSocieteNo, isDossierImpaye: false);
		CaisseManager.AffectationUpdate(affectation.ReglementNo, affectation.No, affectation.Montant + echeance.Solde, deviseSocieteNo);
		_echeanceRepository.Ajuster(echeance.No);
		_notifyService.Notify(TypeEntity.Echeance, num, TypeAction.Ajout, echeance.SocieteNo);
		transactionScope.Complete();
	}

	public void EcartAjustementGaintGrfCreate(int echeanceNo, int deviseSocieteNo)
	{
		if (echeanceNo < 0)
		{
			throw new ArgumentException("Echéance No est invalide!");
		}
		Echeance echeance = EcheanceGet(echeanceNo);
		if (echeance == null)
		{
			throw new ApplicationException("Impossible de charger l'échéance!");
		}
		if (echeance.DeviseNo != deviseSocieteNo)
		{
			throw new ApplicationException("Opération invalide! L'échéance [" + echeance.DocumentNumero + "] est en devise!");
		}
		if (echeance.Solde == 0m)
		{
			return;
		}
		if (echeance.Solde > Societe.MaxGainFrs)
		{
			throw new ApplicationException("Le reste à payer de l'échéance [" + echeance.DocumentNumero + "] est supérieur au max. gain autorisé!");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		Affectation affectation = (from x in echeance.GetAffectations()
			orderby x.No
			select x).FirstOrDefault((Affectation x) => x.Montant > 0m);
		if (affectation == null)
		{
			throw new ApplicationException("Opération invalide! L'échéance [" + echeance.DocumentNumero + "] ne possède aucune affectation!");
		}
		ReglementFournisseur reglementFournisseur = CaisseManager.ReglementFournisseurGet(affectation.ReglementNo);
		if (reglementFournisseur == null)
		{
			throw new ApplicationException("Impossible de charger le règlement de l'échéance [" + echeance.DocumentNumero + "].");
		}
		DateTime documentDate = DateTime.Now;
		switch (Societe.DateAjustementFrs)
		{
		case TypeDateAjustement.DateEcheance:
			documentDate = echeance.DocumentDate;
			break;
		case TypeDateAjustement.DateOperation:
			documentDate = DateTime.Now;
			break;
		case TypeDateAjustement.DateReglement:
			documentDate = reglementFournisseur.Date;
			break;
		}
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		int num = EcheanceCreate(0, echeance.DocumentNumero, ErpDomaine.Achat, ErpDocumentType.None, documentDate, echeance.ClientNo, echeance.ClientCode, echeance.ClientIntitule, echeance.PayeurNo, echeance.PayeurCode, echeance.PayeurIntitule, echeance.DeviseNo, echeance.CoursDevise, echeance.Solde * -1m, echeance.Date, echeance.ModeReglementNo, echeance.SoucheNo, echeance.CollaborateurNo, EcheanceType.Gain, "Gain sur la facture " + echeance.DocumentNumero, deviseSocieteNo, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, echeance.No);
		CaisseManager.ReglementFournisseurImputer(affectation.ReglementNo, num, echeance.Solde * -1m, deviseSocieteNo, isDossierImpaye: false);
		CaisseManager.AffectationFournisseurUpdate(affectation.ReglementNo, affectation.No, affectation.Montant + echeance.Solde, deviseSocieteNo);
		_echeanceRepository.Ajuster(echeance.No);
		_notifyService.Notify(TypeEntity.Echeance, num, TypeAction.Ajout, echeance.SocieteNo);
		transactionScope.Complete();
	}

	public EcartEchange EcartEchangeGet(int ecartEchangeNo)
	{
		return _ecartEchangeRepository.Get(ecartEchangeNo);
	}

	public IEnumerable<EcartEchange> EcartEchangeGetAll(Societe societe = null)
	{
		societe = societe ?? Societe;
		return _ecartEchangeRepository.GetAll(societe.No, EcheanceType.PerteEchange, EcheanceType.GainEchange);
	}

	public IEnumerable<EcartEchange> EcartEchangeGetAll(DateTime dateDebut, DateTime dateFin, EtatComptabilite etat, Societe societe = null)
	{
		societe = societe ?? Societe;
		return _ecartEchangeRepository.GetAll(dateDebut, dateFin, societe.No, etat, EcheanceType.PerteEchange, EcheanceType.GainEchange);
	}

	public Ecart EcartGet(int no, Societe societe = null)
	{
		if (no <= 0)
		{
			throw new ArgumentException("Ecart No");
		}
		societe = societe ?? Societe;
		return _ecartRepository.GetByNo(no, societe.No);
	}

	public void EcheanceReporterEcheancePrevue(int echeanceNo, DateTime newEcheance)
	{
		Echeance echeance = _echeanceRepository.Get(echeanceNo);
		if (echeance == null)
		{
			throw new ApplicationException("Impossible de charger l'échéance.");
		}
		if (echeance.Domaine != ErpDomaine.Vente)
		{
			throw new ApplicationException("Opération invalide!L'échéance n'est pas de type vente");
		}
		if (echeance.Type != EcheanceType.Erp && (echeance.Type != EcheanceType.Solde || echeance.IsSpe != 0))
		{
			throw new ApplicationException($"Opération invalide!Impossible de reporter une échéance de type {echeance.Type}.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		echeance.EcheanceReporte = (Societe.IsReportEcheanceAppliedToEcheanceOrigine ? echeance.EcheanceReporte : newEcheance);
		echeance.Date = (Societe.IsReportEcheanceAppliedToEcheanceOrigine ? newEcheance : echeance.Date);
		_echeanceRepository.Update(echeance);
		_notifyService.Notify(TypeEntity.Echeance, echeance.No, TypeAction.Modification, echeance.SocieteNo);
		transactionScope.Complete();
	}

	public void EcheanceFournisseurReporterEcheancePrevue(int echeanceNo, DateTime newEcheance)
	{
		Echeance echeance = _echeanceRepository.Get(echeanceNo);
		if (echeance == null)
		{
			throw new ApplicationException("Impossible de charger l'échéance.");
		}
		if (echeance.Domaine != ErpDomaine.Achat)
		{
			throw new ApplicationException("Opération invalide!L'échéance n'est pas de type achat");
		}
		if (echeance.Type != EcheanceType.Erp && (echeance.Type != EcheanceType.Solde || echeance.IsSpe != 0))
		{
			throw new ApplicationException($"Opération invalide!Impossible de reporter une échéance de type {echeance.Type}.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		echeance.EcheanceReporte = (Societe.IsReportEcheanceAppliedToEcheanceOrigine ? echeance.EcheanceReporte : newEcheance);
		echeance.Date = (Societe.IsReportEcheanceAppliedToEcheanceOrigine ? newEcheance : echeance.Date);
		_echeanceRepository.Update(echeance);
		_notifyService.Notify(TypeEntity.Echeance, echeance.No, TypeAction.Modification, echeance.SocieteNo);
		transactionScope.Complete();
	}

	public void EcheanceAnnulerReporterEcheancePrevue(int echeanceNo)
	{
		Echeance echeance = _echeanceRepository.Get(echeanceNo);
		if (echeance == null)
		{
			throw new ApplicationException("Impossible de charger l'échéance.");
		}
		if (echeance.Domaine != ErpDomaine.Vente)
		{
			throw new ApplicationException("Opération invalide!L'échéance n'est pas de type vente");
		}
		if (echeance.Type != EcheanceType.Erp && (echeance.Type != EcheanceType.Solde || echeance.IsSpe != 0))
		{
			throw new ApplicationException($"Opération invalide!Impossible de reporter une échéance de type {echeance.Type}.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		DateTime date = echeance.Date;
		DateTime echeanceReporte = echeance.EcheanceReporte;
		echeance.Date = (Societe.IsReportEcheanceAppliedToEcheanceOrigine ? echeanceReporte : date);
		echeance.EcheanceReporte = (Societe.IsReportEcheanceAppliedToEcheanceOrigine ? echeanceReporte : date);
		_echeanceRepository.Update(echeance);
		_notifyService.Notify(TypeEntity.Echeance, echeance.No, TypeAction.Modification, echeance.SocieteNo);
		transactionScope.Complete();
	}

	public void EcheanceFournisseurAnnulerReporterEcheancePrevue(int echeanceNo)
	{
		Echeance echeance = _echeanceRepository.Get(echeanceNo);
		if (echeance == null)
		{
			throw new ApplicationException("Impossible de charger l'échéance.");
		}
		if (echeance.Domaine != ErpDomaine.Achat)
		{
			throw new ApplicationException("Opération invalide!L'échéance n'est pas de type achat");
		}
		if (echeance.Type != EcheanceType.Erp && (echeance.Type != EcheanceType.Solde || echeance.IsSpe != 0))
		{
			throw new ApplicationException($"Opération invalide!Impossible de reporter une échéance de type {echeance.Type}.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		DateTime date = echeance.Date;
		DateTime echeanceReporte = echeance.EcheanceReporte;
		echeance.Date = (Societe.IsReportEcheanceAppliedToEcheanceOrigine ? echeanceReporte : date);
		echeance.EcheanceReporte = (Societe.IsReportEcheanceAppliedToEcheanceOrigine ? echeanceReporte : date);
		_echeanceRepository.Update(echeance);
		_notifyService.Notify(TypeEntity.Echeance, echeance.No, TypeAction.Modification, echeance.SocieteNo);
		transactionScope.Complete();
	}

	public int EcheanceCreate(int erpNo, string piece, ErpDomaine domaine, ErpDocumentType documentType, DateTime documentDate, int clientNo, string clientCode, string clientIntitule, int payeurNo, string payeurCode, string payeurIntitule, int deviseNo, decimal deviseCours, decimal montant, DateTime date, int modeNo, int soucheNo, int collaborateurNo, EcheanceType type, string commentaire, int deviseSocieteNo, string codeAffaire, string reference, string info1, string info2, string info3, string info4, int? ecartNo = null, Societe societe = null, int isSpe = 0, bool isTimbre = false, decimal timbre = 0m, decimal montantDeviseOm = 0m, int? designationDocumentNo = null, bool isComptabilise = false, bool useNotification = true)
	{
		if (erpNo < 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorErpNo);
		}
		if (deviseNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorDeviseNo);
		}
		if (clientNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorClientNo);
		}
		if (date < DateTime.MinValue)
		{
			throw new ArgumentNullException("date");
		}
		if (string.IsNullOrEmpty(piece))
		{
			throw new ArgumentNullException("piece");
		}
		if (deviseSocieteNo < 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorDeviseNo);
		}
		societe = societe ?? Societe;
		if (societe == null)
		{
			throw new ArgumentNullException("societe");
		}
		if ((societe.GetMode(modeNo) ?? throw new ArgumentNullException("Impossible de charger le mode réglement")).EnSommeil)
		{
			throw new ApplicationException("Mode réglement est en sommeil!");
		}
		SocieteDevise devise = societe.GetDevise(deviseSocieteNo);
		if (devise == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorDeviseNo);
		}
		deviseCours = ((deviseNo == deviseSocieteNo) ? 1m : deviseCours);
		decimal num = default(decimal);
		num = ((societe.UseObjetMetier && !(montantDeviseOm == 0m)) ? montantDeviseOm : Math.Round(montant * deviseCours, devise.NombreDecimales, MidpointRounding.AwayFromZero));
		if (!UserHasAutorisationSouche(domaine, soucheNo))
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorAutorisationSouche);
		}
		if ((type == EcheanceType.Perte || type == EcheanceType.Gain) && !ecartNo.HasValue)
		{
			throw new ApplicationException("Ecart no invalide!");
		}
		if (type == EcheanceType.Erp && _echeanceRepository.GetByErpNo(domaine, societe.No, piece, erpNo) != null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorEcheanceExist);
		}
		if (!societe.HasMode(modeNo))
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorModeNo);
		}
		if (societe.LegislationType == Legislation.Maroc && designationDocumentNo.HasValue && Groupe.DesignationDocumentManager.Get(designationDocumentNo.Value) == null)
		{
			throw new ApplicationException("Impossible de charger la désignation document.");
		}
		Echeance echeance = new Echeance(erpNo, piece, domaine, documentType, documentDate, montant, montant, date, type, modeNo, societe.No, clientNo, clientCode, clientIntitule, payeurNo, payeurCode, payeurIntitule, deviseNo, soucheNo, collaborateurNo, deviseCours, commentaire, num, num)
		{
			UtilisateurNo = Utilisateur.No,
			Ajuste = false,
			EcartNo = ecartNo,
			AffaireNumero = codeAffaire,
			Reference = reference,
			Info1 = info1,
			Info2 = info2,
			Info3 = info3,
			Info4 = info4,
			IsSpe = isSpe,
			IsTimbre = isTimbre,
			Timbre = timbre,
			EcheanceReporte = date,
			DesignationDocumentNo = ((societe.LegislationType == Legislation.Maroc) ? designationDocumentNo : ((int?)null)),
			IsComptabilise = isComptabilise
		};
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		int? num2 = _echeanceRepository.Create(echeance);
		if (!num2.HasValue)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorEcheanceEchouee);
		}
		if (useNotification)
		{
			_notifyService.Notify(TypeEntity.Echeance, num2.Value, TypeAction.Ajout, echeance.SocieteNo);
		}
		transactionScope.Complete();
		return num2.Value;
	}

	public Task<IEnumerable<Facture>> GetAllFactureFournisseurAComptaAsync(DateTime dateDe, DateTime dateA, bool isComptabilise, CancellationToken cancellationToken)
	{
		return _factureRepository.GetAllAComptaAsync(dateDe.Date, dateA.Date.AddDays(1.0), isComptabilise, Societe.No, cancellationToken);
	}

	public Task<Facture> FactureFournisseurGetAsync(int factureNo)
	{
		return _factureRepository.GetAsync(factureNo);
	}

	public Facture FactureFournisseurGet(int factureNo)
	{
		return _factureRepository.Get(factureNo);
	}

	public void EcheanceDelete(int no, bool traitementSpe = false)
	{
		Echeance echeance = _echeanceRepository.Get(no);
		if (echeance == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorEcheanceNo);
		}
		if (!UserHasAutorisationSouche(echeance.Domaine, echeance.SoucheNo))
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorAutorisationSouche);
		}
		if (echeance.GetAffectations().Any())
		{
			throw new ApplicationException("L'échéance a une ou plusieurs affectations!");
		}
		if ((echeance.Type == EcheanceType.GainEchange || echeance.Type == EcheanceType.PerteEchange) && echeance.IsComptaEcart)
		{
			throw new ApplicationException("L'échéance d'écart est comptabilisé!");
		}
		if (echeance.Type == EcheanceType.RemboursementClientEspece || echeance.Type == EcheanceType.RemboursementClientRS)
		{
			throw new ApplicationException("Opération invalide! L'échéance est liée à un remboursement client!");
		}
		if (echeance.Type == EcheanceType.VirementTiers)
		{
			throw new ApplicationException("Opération invalide! L'échéance est liée à un virement en masse!");
		}
		if (echeance.Type == EcheanceType.CommissionImpaye)
		{
			throw new ApplicationException("Opération invalide! L'échéance [" + echeance.DocumentNumero + "] est liée à un dossier règlement impayé!");
		}
		if (echeance.Type == EcheanceType.InteretImpaye)
		{
			throw new ApplicationException("Opération invalide! L'échéance [" + echeance.DocumentNumero + "] est liée à un dossier règlement impayé!");
		}
		if (echeance.Type == EcheanceType.RemboursementDivers)
		{
			throw new ApplicationException("Opération invalide! L'échéance est liée à un remboursement Chèque!");
		}
		if (echeance.Type == EcheanceType.RemboursementFournisseur)
		{
			throw new ApplicationException("Opération invalide! L'échéance est liée à un remboursement fournisseur!");
		}
		if (echeance.ReglementAvoirNo != 0)
		{
			throw new ApplicationException("Cette échéance est associée à un règlement d'avoir.");
		}
		if (echeance.RemoursementEcheanceAvoirNo.HasValue)
		{
			throw new ApplicationException("Le document [" + echeance.DocumentNumero + "] possède un remboursement R/S.");
		}
		if (echeance.IsReserveDossierFrs)
		{
			throw new ApplicationException("L'échéance [" + echeance.DocumentNumero + "] est réservée pour un dossier de règlement fournisseur.");
		}
		if (IsEcheanceUtiliseDossierFrs(echeance.No))
		{
			throw new ApplicationException("L'échéance [" + echeance.DocumentNumero + "] est utilisée dans un dossier de règlement fournisseur");
		}
		if (!traitementSpe && echeance.IsSpe != 0)
		{
			throw new ApplicationException("Cette échéance ne peut pas être supprimée!");
		}
		if (echeance.Type == EcheanceType.FactureGR && echeance.IsComptabilise && !echeance.IsFactureGrFromComptaErp)
		{
			throw new ApplicationException("L'échéance [" + echeance.DocumentNumero + "] est comptabilisée.");
		}
		if (echeance.CreditNo.HasValue)
		{
			throw new ApplicationException("L'échéance [" + echeance.DocumentNumero + "] est réservée pour un crédit.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		foreach (Note item in _noteRepository.GetAllNotesByEntite(echeance.No, TypeEntity.Echeance))
		{
			_noteRepository.Delete(item);
		}
		if (echeance.Type == EcheanceType.FactureFrsTresorerie)
		{
			_ventilationAnalytiqueRepository.DeleteByEntity(echeance.SocieteNo, no, AnalytiqueDomaine.FactureFournisseurTresorerie);
		}
		if (echeance.Type == EcheanceType.FactureGR)
		{
			_ventilationAnalytiqueRepository.DeleteByEntity(echeance.SocieteNo, no, AnalytiqueDomaine.EcritureTresorerie);
			_ecritureCompta.DeleteLigne(echeance.No, MouvementDomaine.EcritureTresorerie);
		}
		foreach (WorkflowHistory item2 in _workflowHistoryRepository.GetAll(TypeEntity.Echeance, echeance.No))
		{
			_workflowHistoryRepository.Delete(item2);
		}
		_echeanceRepository.Delete(echeance);
		_notifyService.Notify(TypeEntity.Echeance, no, TypeAction.Suppression, echeance.SocieteNo);
		transactionScope.Complete();
	}

	public bool EcheanceExiste(SocieteModeReglement mode)
	{
		if (mode == null)
		{
			throw new ArgumentNullException("mode");
		}
		return _echeanceRepository.HasEcheance(mode);
	}

	public bool EcheanceExiste(SocieteDevise devise)
	{
		if (devise == null)
		{
			throw new ArgumentNullException("devise");
		}
		return _echeanceRepository.HasEcheance(devise);
	}

	public void EcheanceChangerTiersPayeur(int echeanceNo, int payeurNo, string payeurNumero, string payeurIntitule)
	{
		if (payeurNo <= 0)
		{
			throw new ArgumentNullException("payeurNo");
		}
		if (string.IsNullOrEmpty(payeurNumero) || string.IsNullOrWhiteSpace(payeurNumero))
		{
			throw new ApplicationException("Code payeur invalide.");
		}
		if (string.IsNullOrEmpty(payeurIntitule) || string.IsNullOrWhiteSpace(payeurIntitule))
		{
			throw new ApplicationException("Intitulé payeur invalide.");
		}
		Echeance echeance = _echeanceRepository.Get(echeanceNo);
		if (echeance == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorEcheanceNo);
		}
		if (echeance.Type != EcheanceType.Erp && echeance.Type != EcheanceType.Solde)
		{
			throw new ApplicationException("Type d'échéance invalide.");
		}
		IEnumerable<Echeance> allByDocument = _echeanceRepository.GetAllByDocument(echeance.Domaine, Societe.No, echeance.DocumentNumero);
		if (!allByDocument.Any())
		{
			throw new ApplicationException("Impossible de déterminer les écheances du document.");
		}
		if (allByDocument.Any((Echeance x) => x.Solde != x.Montant))
		{
			throw new ApplicationException("Il existe une échéance payée.");
		}
		if (allByDocument.Any((Echeance x) => x.PayeurNo == payeurNo))
		{
			throw new ApplicationException("Opération invalide! C'est le même tiers payeur.");
		}
		foreach (Echeance item in allByDocument)
		{
			item.PayeurNo = payeurNo;
			item.PayeurCode = payeurNumero;
			item.PayeurIntitule = payeurIntitule;
			_echeanceRepository.Update(item);
			_notifyService.Notify(TypeEntity.Echeance, item.No, TypeAction.Modification, echeance.SocieteNo);
		}
	}

	public void EcheanceChangerDesignationDocument(int echeanceNo, int designationDocumentNo, bool fromDossier = false)
	{
		if (designationDocumentNo <= 0)
		{
			throw new ArgumentNullException("designationDocumentNo");
		}
		if (Societe.LegislationType != Legislation.Maroc)
		{
			throw new ApplicationException("Opération invalide! [Législation]");
		}
		Echeance echeance = _echeanceRepository.Get(echeanceNo);
		if (echeance == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorEcheanceNo);
		}
		if (echeance.Type != EcheanceType.Erp && echeance.Type != EcheanceType.Solde && echeance.Type != EcheanceType.FactureGR)
		{
			throw new ApplicationException("Type d'échéance invalide.");
		}
		IEnumerable<Echeance> allByDocument = _echeanceRepository.GetAllByDocument(echeance.Domaine, Societe.No, echeance.DocumentNumero);
		if (!allByDocument.Any())
		{
			throw new ApplicationException("Impossible de déterminer les écheances du document.");
		}
		if (allByDocument.Any((Echeance x) => x.Solde != x.Montant))
		{
			throw new ApplicationException("Il existe une échéance payée.");
		}
		if (Groupe.DesignationDocumentManager.Get(designationDocumentNo) == null)
		{
			throw new ApplicationException("Impossible de charger la désignation document.");
		}
		if (allByDocument.Any((Echeance x) => x.DesignationDocumentNo == designationDocumentNo))
		{
			return;
		}
		if (!fromDossier && allByDocument.Any((Echeance x) => x.IsReserveDossierFrs))
		{
			throw new ApplicationException("Il existe une échéance réservée dans un dossier de règlement.");
		}
		foreach (Echeance item in allByDocument)
		{
			item.DesignationDocumentNo = designationDocumentNo;
			_echeanceRepository.Update(item);
			_notifyService.Notify(TypeEntity.Echeance, item.No, TypeAction.Modification, echeance.SocieteNo);
		}
	}

	public Echeance EcheanceGet(int societeNo, string echeanceNumero, EcheanceType echeanceType)
	{
		return _echeanceRepository.Get(societeNo, echeanceNumero, echeanceType);
	}

	public Echeance EcheanceGet(string echeanceNumero, EcheanceType echeanceType)
	{
		return _echeanceRepository.Get(Societe.No, echeanceNumero, echeanceType);
	}

	public Echeance EcheanceGet(int societeNo, ErpDomaine domaine, string documentNumero, int erpNo)
	{
		return _echeanceRepository.GetByErpNo(domaine, societeNo, documentNumero, erpNo);
	}

	public Echeance EcheanceGet(int no)
	{
		return _echeanceRepository.Get(no);
	}

	public Task<IEnumerable<Echeance>> EcheanceGetAllAsync(ErpDomaine domaine, EcheanceType[] echeancesType, int clientNo, Societe societe = null)
	{
		societe = societe ?? Societe;
		if (!Utilisateur.IsAdmin)
		{
			return _echeanceRepository.GetAllAsync(domaine, societe.No, echeancesType, clientNo, (from a in GetAllAutorizedSouches(domaine)
				select a.SoucheNo).ToArray());
		}
		return _echeanceRepository.GetAllAsync(domaine, societe.No, echeancesType, clientNo);
	}

	public Task<IEnumerable<Echeance>> EcheanceGetAllAsync(ErpDomaine domaine, EcheanceType[] echeancesType, Societe societe = null)
	{
		societe = societe ?? Societe;
		if (!Utilisateur.IsAdmin)
		{
			return _echeanceRepository.GetAllAsync(domaine, societe.No, echeancesType, (from a in GetAllAutorizedSouches(domaine)
				select a.SoucheNo).ToArray());
		}
		return _echeanceRepository.GetAllAsync(domaine, societe.No, echeancesType);
	}

	public IEnumerable<Echeance> EcheanceGetAllHasAffaire(ErpDomaine domaine, DateTime? dateDebut = null, DateTime? dateFin = null, bool hasNotErp = false, Societe societe = null)
	{
		societe = societe ?? Societe;
		return _echeanceRepository.GetAllHasAffaire(domaine, societe.No, dateDebut, dateFin, hasNotErp);
	}

	public IEnumerable<Echeance> EcheanceGetAll(ErpDomaine domaine, EcheanceType[] echeanceTypes, DateTime? dateDebut = null, DateTime? dateFin = null, bool isImporter = false, Societe societe = null)
	{
		societe = societe ?? Societe;
		if (!Utilisateur.IsAdmin)
		{
			return _echeanceRepository.GetAll(domaine, societe.No, echeanceTypes, dateDebut, dateFin, isImporter, (from a in GetAllAutorizedSouches(domaine)
				select a.SoucheNo).ToArray());
		}
		return _echeanceRepository.GetAll(domaine, societe.No, echeanceTypes, dateDebut, dateFin, isImporter);
	}

	public IEnumerable<Echeance> EcheanceGetAll(ErpDomaine domaine, DateTime? dateDebut = null, DateTime? dateFin = null, bool onlySpe = false, Societe societe = null)
	{
		societe = societe ?? Societe;
		if (!Utilisateur.IsAdmin)
		{
			return _echeanceRepository.GetAll(domaine, societe.No, nonPaye: false, onlySpe, dateDebut, dateFin, (from a in GetAllAutorizedSouches(domaine)
				select a.SoucheNo).ToArray());
		}
		return _echeanceRepository.GetAll(domaine, societe.No, nonPaye: false, onlySpe, dateDebut, dateFin);
	}

	public IEnumerable<Echeance> EcheanceGetAll(ErpDomaine domaine, Etat etat, Societe societe = null)
	{
		societe = societe ?? Societe;
		if (Utilisateur.IsAdmin)
		{
			return _echeanceRepository.GetAll(domaine, societe.No, nonPaye: true);
		}
		int[] array = (from x in GetAutorisationSouche(domaine)
			select x.SoucheNo).ToArray();
		if (array == null || !array.Any())
		{
			return new List<Echeance>();
		}
		IEcheanceRepository echeanceRepository = _echeanceRepository;
		int no = societe.No;
		int[] souchesNo = array;
		return echeanceRepository.GetAll(domaine, no, nonPaye: true, onlySpe: false, null, null, souchesNo);
	}

	public IEnumerable<Echeance> EcheanceGetAll(ErpDomaine domaine, int clientNo, Etat? etat = null, Societe societe = null)
	{
		if (clientNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorClientNo);
		}
		societe = societe ?? Societe;
		IEnumerable<Echeance> echeances = _echeanceRepository.GetAllByTiers(domaine, societe.No, clientNo, etat);
		if (Utilisateur.IsAdmin)
		{
			return echeances;
		}
		List<AutorisationSouche> autorisation = (from a in GetAutorisationSouche(domaine)
			where echeances.Any((Echeance e) => e.SoucheNo == a.SoucheNo)
			select a).ToList();
		if (!autorisation.Any())
		{
			return new List<Echeance>();
		}
		return echeances.Where((Echeance e) => autorisation.Any((AutorisationSouche a) => a.SoucheNo == e.SoucheNo));
	}

	public IEnumerable<Echeance> EcheanceGetAll(ErpDomaine domaine, int clientNo, DateTime dateDu, DateTime DateAu, Etat? etat = null, Societe societe = null)
	{
		if (clientNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorClientNo);
		}
		societe = societe ?? Societe;
		IEnumerable<Echeance> echeances = _echeanceRepository.GetAllByTiers(domaine, societe.No, clientNo, dateDu, DateAu, etat);
		if (Utilisateur.IsAdmin)
		{
			return echeances;
		}
		List<AutorisationSouche> autorisation = (from a in GetAutorisationSouche(domaine)
			where echeances.Any((Echeance e) => e.SoucheNo == a.SoucheNo)
			select a).ToList();
		if (!autorisation.Any())
		{
			return new List<Echeance>();
		}
		return echeances.Where((Echeance e) => autorisation.Any((AutorisationSouche a) => a.SoucheNo == e.SoucheNo));
	}

	public IEnumerable<Echeance> EcheanceGetAll(ErpDomaine domaine, int clientNo, DateTime dateDu, DateTime DateAu, int designationDocumentNo, Etat? etat = null, Societe societe = null)
	{
		if (clientNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorClientNo);
		}
		societe = societe ?? Societe;
		IEnumerable<Echeance> echeances = _echeanceRepository.GetAllByTiers(domaine, societe.No, clientNo, dateDu, DateAu, designationDocumentNo, etat);
		if (Utilisateur.IsAdmin)
		{
			return echeances;
		}
		List<AutorisationSouche> autorisation = (from a in GetAutorisationSouche(domaine)
			where echeances.Any((Echeance e) => e.SoucheNo == a.SoucheNo)
			select a).ToList();
		if (!autorisation.Any())
		{
			return new List<Echeance>();
		}
		return echeances.Where((Echeance e) => autorisation.Any((AutorisationSouche a) => a.SoucheNo == e.SoucheNo));
	}

	public int EcheanceGetCountImpaye(ErpDomaine domaine, string clientCode, Societe societe = null)
	{
		if (string.IsNullOrEmpty(clientCode))
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorClientNo);
		}
		societe = societe ?? Societe;
		return _echeanceRepository.GetNombreImpayeByTiers(domaine, societe.No, clientCode);
	}

	public int EcheanceGetCountDepassementDelaisPayement(ErpDomaine domaine, string clientCode, int delaiPayement = 0, Societe societe = null)
	{
		if (string.IsNullOrEmpty(clientCode))
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorClientNo);
		}
		societe = societe ?? Societe;
		return _echeanceRepository.GetAllDepassementDetaisPayement(domaine, societe.No, clientCode, delaiPayement);
	}

	public IEnumerable<Echeance> EcheanceGetAllByDocument(ErpDomaine domaine, string piece, ErpDocumentType type, int clientNo, int soucheNo, Societe societe = null)
	{
		societe = societe ?? Societe;
		return _echeanceRepository.GetAllByDocument(domaine, societe.No, piece, type, clientNo, soucheNo);
	}

	public IEnumerable<Echeance> EcheanceGetAllByDocument(ErpDomaine domaine, string piece, Societe societe = null)
	{
		societe = societe ?? Societe;
		return _echeanceRepository.GetAllByDocument(domaine, societe.No, piece);
	}

	public void EcheanceImpayeFournisseurDelete(int no)
	{
		if (no <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorEcheanceNo);
		}
		Echeance echeance = _echeanceRepository.Get(no);
		if (echeance == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorEcheanceNo);
		}
		if (!UserHasAutorisationSouche(echeance.Domaine, echeance.SoucheNo))
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorAutorisationSouche);
		}
		if (echeance.Type != EcheanceType.ImpayeFournisseur)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorOperationInvalide);
		}
		CaisseManager.ReglementFournisseurAnnulerImpaye(echeance.ReglementImpayeNo);
	}

	public void EcheanceImpayeDelete(int no)
	{
		if (no <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorEcheanceNo);
		}
		Echeance echeance = _echeanceRepository.Get(no);
		if (echeance == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorEcheanceNo);
		}
		if (!UserHasAutorisationSouche(echeance.Domaine, echeance.SoucheNo))
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorAutorisationSouche);
		}
		if (echeance.Type != EcheanceType.Impaye)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorOperationInvalide);
		}
		ReglementClient reglementClient = CaisseManager.ReglementGet(echeance.ReglementImpayeNo);
		if (reglementClient == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorOperationInvalide);
		}
		if (reglementClient.IsComptabiliseImpaye)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ImpayeErrorSuppression);
		}
		if (echeance.GetAffectations().Any())
		{
			throw new InvalidOperationException(string.Format(TresorerieCoreMessages.ErrorEcheancePayee, echeance.DocumentNumero));
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		int reglementImpayeNo = echeance.ReglementImpayeNo;
		CaisseManager.ReglementDeremettre(reglementImpayeNo);
		_echeanceRepository.Delete(echeance);
		_notifyService.Notify(TypeEntity.Echeance, no, TypeAction.Suppression, echeance.SocieteNo);
		_notifyService.Notify(TypeEntity.Reglement, reglementImpayeNo, TypeAction.Modification, echeance.SocieteNo);
		_notifyService.Notify(TypeEntity.Bordereau, reglementClient.EnteteBordereauNo.GetValueOrDefault(), TypeAction.Modification, echeance.SocieteNo);
		transactionScope.Complete();
	}

	public void EcheanceUpdate(Echeance echeance)
	{
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		if (echeance == null)
		{
			throw new ApplicationException("L'échéance est invalide!");
		}
		_echeanceRepository.Update(echeance);
		_notifyService.Notify(TypeEntity.Echeance, echeance.No, TypeAction.Modification, echeance.SocieteNo);
		transactionScope.Complete();
	}

	public override bool Equals(object obj)
	{
		if (!(obj is SocieteManager societeManager))
		{
			return false;
		}
		return societeManager.GetHashCode() == GetHashCode();
	}

	public int EtapeComptaCreate(int order, int typeBordereauNo, NatureJournalEtape natureJournal, string journal, NatureDebitEtape natureDebit, string compteGeneral, NatureActionEtape action, DateComptaEtape optionDate, Societe societe = null)
	{
		if (typeBordereauNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorBordereauTypeInvalide);
		}
		if (!Utilisateur.IsAdmin)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorCreateEtapeCompta);
		}
		societe = societe ?? Societe;
		if (Groupe.BordereauxTypeManager.Get(typeBordereauNo) == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorOperationInvalide);
		}
		journal = journal ?? string.Empty;
		compteGeneral = compteGeneral ?? string.Empty;
		EtapeCompta etape = new EtapeCompta
		{
			CompteGeneral = compteGeneral,
			Journal = journal,
			NatureAction = action,
			NatureDebit = natureDebit,
			NatureJournal = natureJournal,
			Order = order,
			SocieteNo = societe.No,
			TypeBordereauNo = typeBordereauNo,
			OptionDate = optionDate
		};
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		int? num = _socTypeBordereauRepository.Create(etape);
		societe.RefreshSocTypeBordereaux();
		_notifyService.Notify(TypeEntity.Parametrage, 0, TypeAction.Modification, societe.No);
		transactionScope.Complete();
		if (!num.HasValue)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorInsertion);
		}
		return num.Value;
	}

	public void EtapeComptaDelete(int etapeNo)
	{
		EtapeCompta etape = EtapeComptaGet(etapeNo);
		if (etape == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorEtapeInvalide);
		}
		if (!Utilisateur.IsAdmin)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorDeleteEtapeCompta);
		}
		SocieteTypeBordereau societeTypeBordereau = GetSocTypeBordereaux().SingleOrDefault((SocieteTypeBordereau t) => t.No == etape.TypeBordereauNo && t.SocieteNo == etape.SocieteNo);
		if (societeTypeBordereau == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorTypeBordereauInvalide);
		}
		EtapeCompta etapeCompta = EtapeComptaGet(societeTypeBordereau.No, societeTypeBordereau.SocieteNo, societeTypeBordereau.Etapes.Count);
		if (etapeCompta.No != etape.No)
		{
			throw new InvalidOperationException(string.Format(TresorerieCoreMessages.ErrorEtape, etapeCompta.Order));
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using (TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions))
		{
			_socTypeBordereauRepository.Delete(etape);
			transactionScope.Complete();
		}
		Societe.RefreshSocTypeBordereaux();
		_notifyService.Notify(TypeEntity.Parametrage, 0, TypeAction.Modification, Societe.No);
	}

	public EtapeCompta EtapeComptaGet(int typeBordereauNo, int societeNo, int order)
	{
		return _socTypeBordereauRepository.Get(typeBordereauNo, societeNo, order);
	}

	public EtapeCompta EtapeComptaGet(int etapeNo)
	{
		if (etapeNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorEtapeNo);
		}
		return _socTypeBordereauRepository.Get(etapeNo);
	}

	public void EtapeComptaUpdate(int etapeNo, NatureJournalEtape natureJournal, string journal, NatureDebitEtape natureDebit, string compteGeneral, NatureActionEtape action, DateComptaEtape optionDate)
	{
		if (etapeNo <= 0)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorEtapeNo);
		}
		EtapeCompta etapeCompta = _socTypeBordereauRepository.Get(etapeNo);
		if (etapeCompta == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorEtapeInvalide);
		}
		if (!Utilisateur.IsAdmin)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorUpdateEtapeCompta);
		}
		etapeCompta.NatureJournal = natureJournal;
		etapeCompta.Journal = journal;
		etapeCompta.NatureDebit = natureDebit;
		etapeCompta.CompteGeneral = compteGeneral;
		etapeCompta.NatureAction = action;
		etapeCompta.OptionDate = optionDate;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using (TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions))
		{
			_socTypeBordereauRepository.Update(etapeCompta);
			transactionScope.Complete();
		}
		Societe.RefreshSocTypeBordereaux();
		_notifyService.Notify(TypeEntity.Parametrage, 0, TypeAction.Modification, Societe.No);
	}

	public void EtatCreateBordereau(string intitule, string chemin, int? typeBordereauNo, int banqueNo, Societe societe = null)
	{
		societe = societe ?? Societe;
		if (string.IsNullOrEmpty(intitule.Trim()))
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorEtatIntitule);
		}
		if (banqueNo == 0)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorBanqueInvalide);
		}
		if (!typeBordereauNo.HasValue)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorTypeBordereauInvalide);
		}
		if (typeBordereauNo == 0)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorTypeBordereauInvalide);
		}
		if (_societeImpressionRepository.GetExistEtatBord(typeBordereauNo.Value, banqueNo, TypeRapport.Bordereaux, societe.No) != null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorEtatExist);
		}
		SocieteEtat societeImpression = new SocieteEtat(societe.No, 3, banqueNo, typeBordereauNo, TypeRapport.Bordereaux)
		{
			Intitule = intitule,
			Chemin = chemin,
			IsVisible = false
		};
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_societeImpressionRepository.Create(societeImpression);
		transactionScope.Complete();
	}

	public void EtatDelete(int no)
	{
		if (no < 0)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorSocieteNo);
		}
		SocieteEtat societeEtat = EtatGet(no);
		if (societeEtat == null)
		{
			throw new ArgumentException("Etat invalide!");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_societeImpressionRepository.Delete(societeEtat);
		transactionScope.Complete();
	}

	public SocieteEtat EtatGet(int no)
	{
		return _societeImpressionRepository.Get(no);
	}

	public SocieteEtat EtatGetStandard(int no, string name, Societe societe = null)
	{
		societe = societe ?? Societe;
		return _societeImpressionRepository.EtatGetStandard(no, name, societe.No);
	}

	public SocieteEtat EtatGetStandardByIntitule(string intitule, Societe societe = null)
	{
		societe = societe ?? Societe;
		return _societeImpressionRepository.EtatGetStandard(intitule, societe.No);
	}

	public void EtatUpdate(int no, string intitule, string chemin, bool isVisible)
	{
		if (no < 0)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorEtatNo);
		}
		if (string.IsNullOrEmpty(intitule.Trim()))
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorEtatIntitule);
		}
		SocieteEtat societeEtat = EtatGet(no);
		if (societeEtat == null)
		{
			throw new ApplicationException("Etat invalide!");
		}
		societeEtat.Intitule = intitule;
		societeEtat.Chemin = chemin;
		societeEtat.IsVisible = isVisible;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_societeImpressionRepository.Update(societeEtat);
		transactionScope.Complete();
	}

	public bool ExistAlimentationCaisseEnAttente(int societeNo)
	{
		return _alimentationCaisseRepository.ExistAlimentationCaisseEnAttente(societeNo);
	}

	public int FactureFournisseurCreate(string piece, int fournisseurNo, string fournisseurCode, string fournisseurIntitule, string affaireNumero, decimal montant, int modeNo, int soucheNo, int collaborateurNo, int deviseNo, decimal cours, int deviseSocieteNo, string reference, string commentaire, DateTime documentDate, DateTime echeanceDate, bool isTimbre, decimal timbre, Societe societe = null, int isSpe = 0)
	{
		societe = societe ?? Societe;
		switch (societe.VentilationAnalytique)
		{
		case TypeVentilationAnalytique.Affaire:
			if (string.IsNullOrEmpty(affaireNumero))
			{
				throw new ApplicationException("Le code affaire est obligatoire !");
			}
			break;
		}
		return EcheanceCreate(0, piece, ErpDomaine.Achat, ErpDocumentType.None, documentDate, fournisseurNo, fournisseurCode, fournisseurIntitule, fournisseurNo, fournisseurCode, fournisseurIntitule, deviseNo, cours, montant, echeanceDate, modeNo, soucheNo, collaborateurNo, EcheanceType.FactureFrsTresorerie, commentaire, deviseSocieteNo, affaireNumero, reference, string.Empty, string.Empty, string.Empty, string.Empty, null, societe, isSpe, isTimbre, timbre);
	}

	public Societe Get(int no)
	{
		return _societeRepository.Get(no);
	}

	public Societe Get(string raisonSociale)
	{
		if (string.IsNullOrEmpty(raisonSociale))
		{
			throw new ArgumentNullException("raisonSociale");
		}
		return _societeRepository.Get(raisonSociale);
	}

	public IEnumerable<Societe> GetAll()
	{
		return _societeRepository.GetAll();
	}

	public Caisse GetDefaultCaisse(ProfilType type, int? userNo = null, Societe societe = null)
	{
		societe = societe ?? Societe;
		userNo = userNo ?? Utilisateur.No;
		List<Caisse> source = (from x in societe.GetCaisses()
			where (x.Recette == ((type == ProfilType.Grc) ? 1 : 0) || x.Depense == ((type == ProfilType.Grf) ? 1 : 0)) && !x.EnSommeil
			select x).ToList();
		Caisse result = source.FirstOrDefault((Caisse x) => x.IsDefault);
		AutorisationCaisse autorisationCaisse = GetAllAutorizedCaisses(type, userNo, societe).SingleOrDefault((AutorisationCaisse x) => x.IsDefaultCaisse);
		if (autorisationCaisse != null)
		{
			Caisse caisse = source.SingleOrDefault((Caisse x) => x.No == autorisationCaisse.CaisseNo);
			if (caisse != null)
			{
				return caisse;
			}
		}
		return result;
	}

	public IEnumerable<AutorisationCaisse> GetAllAutorizedCaisses(ProfilType type, int? userNo = null, Societe societe = null)
	{
		societe = societe ?? Societe;
		userNo = userNo ?? Utilisateur.No;
		return _autorisationCaisseRepository.GetAll(userNo.Value, societe.No, type);
	}

	public IEnumerable<AutorisationSouche> GetAllAutorizedSouches(ErpDomaine domaine, int? userNo = null, Societe societe = null)
	{
		societe = societe ?? Societe;
		userNo = userNo ?? Utilisateur.No;
		return _autorisationSoucheRepository.GetAll(domaine, userNo.Value, societe.No);
	}

	public IEnumerable<Societe> GetAllByVisibilite(bool visibilite)
	{
		return _societeRepository.GetAllByVisibilite(visibilite);
	}

	public List<Caution> GetAllCautions(int societeNo, DomaineCaution domaine)
	{
		return _cautionRepository.GetAll(societeNo, domaine).ToList();
	}

	public List<Caution> GetAllCautionsTiers(int tierNo, DomaineCaution domaine)
	{
		return _cautionRepository.GetAllByTier(tierNo, domaine).ToList();
	}

	public List<Caution> GetAllCautionsTiersNonTransformer(int tierNo, DomaineCaution domaine)
	{
		return _cautionRepository.GetAllByTierNonTransformer(tierNo, domaine).ToList();
	}

	public Task<IEnumerable<DossierReglement>> GetAllDossierReglementFournisseurAComptaAsync(DateTime dateMin, DateTime dateMax, bool isComptabilise, int[] caissesNo, bool isDossierCommercial, CancellationToken cancellationToken, Societe societe = null)
	{
		societe = societe ?? Societe;
		StatutDossierReglement statut = (isDossierCommercial ? StatutDossierReglement.Valide : StatutDossierReglement.Encours);
		return _dossierReglementRepository.GetAllAComptaAsync(DomaineDossier.Fournisseur, dateMin.Date, dateMax.Date.AddDays(1.0), isComptabilise, isDossierCommercial, statut, caissesNo, societe.No, cancellationToken);
	}

	public IEnumerable<DossierReglement> GetAllDossierReglementFournisseurToLettrer(DateTime dateMin, DateTime dateMax)
	{
		if (Utilisateur.IsAdmin)
		{
			return _dossierReglementRepository.GetAllDossierFournisseurToLettrer(dateMin, dateMax, Societe.No);
		}
		int[] caisses = (from a in _autorisationCaisseRepository.GetAll(Utilisateur.No, Societe.No, ProfilType.Grf)
			select a.CaisseNo).ToArray();
		return (from x in _dossierReglementRepository.GetAllDossierFournisseurToLettrer(dateMin, dateMax, Societe.No)
			where caisses.Any((int y) => y == x.CaisseNo)
			orderby x.Date descending
			select x).ToList();
	}

	public IEnumerable<DossierReglement> GetAllDossierReglement(DomaineDossier domaine, bool isDossierCommercial, Societe societe = null)
	{
		societe = societe ?? Societe;
		if (Utilisateur.IsAdmin)
		{
			return _dossierReglementRepository.GetAll(domaine, societe.No, isDossierCommercial);
		}
		int[] caisses = (from a in _autorisationCaisseRepository.GetAll(Utilisateur.No, societe.No, (domaine == DomaineDossier.Fournisseur) ? ProfilType.Grf : ProfilType.Grc)
			select a.CaisseNo).ToArray();
		return (from x in _dossierReglementRepository.GetAll(domaine, societe.No, isDossierCommercial)
			where caisses.Any((int y) => y == x.CaisseNo)
			orderby x.Date descending
			select x).ToList();
	}

	public IEnumerable<DossierReglement> GetAllDossierReglement(DomaineDossier domaine, TypeLigneDossier typeLigne, int entityNo, Societe societe = null)
	{
		societe = societe ?? Societe;
		if (Utilisateur.IsAdmin)
		{
			return _dossierReglementRepository.GetAll(domaine, typeLigne, entityNo);
		}
		int[] caisses = (from a in _autorisationCaisseRepository.GetAll(Utilisateur.No, societe.No, ProfilType.Grf)
			select a.CaisseNo).ToArray();
		return (from x in _dossierReglementRepository.GetAll(domaine, typeLigne, entityNo)
			where caisses.Any((int y) => y == x.CaisseNo)
			orderby x.Date descending
			select x).ToList();
	}

	public IEnumerable<DossierReglement> GetAllDossierReglementByFournisseur(int fournisseurNo, bool isDossierCommercial, Societe societe = null)
	{
		societe = societe ?? Societe;
		if (Utilisateur.IsAdmin)
		{
			return _dossierReglementRepository.GetAll(DomaineDossier.Fournisseur, societe.No, fournisseurNo, isDossierCommercial);
		}
		int[] caisses = (from a in _autorisationCaisseRepository.GetAll(Utilisateur.No, societe.No, ProfilType.Grf)
			select a.CaisseNo).ToArray();
		return (from x in _dossierReglementRepository.GetAll(DomaineDossier.Fournisseur, societe.No, fournisseurNo, isDossierCommercial)
			where caisses.Any((int y) => y == x.CaisseNo)
			orderby x.Date descending
			select x).ToList();
	}

	public IEnumerable<DossierReglement> GetAllDossierReglementByClient(int clientNo, bool isDossierCommercial, Societe societe = null)
	{
		societe = societe ?? Societe;
		if (Utilisateur.IsAdmin)
		{
			return _dossierReglementRepository.GetAll(DomaineDossier.Client, societe.No, clientNo, isDossierCommercial);
		}
		int[] caisses = (from a in _autorisationCaisseRepository.GetAll(Utilisateur.No, societe.No, ProfilType.Grc)
			select a.CaisseNo).ToArray();
		return (from x in _dossierReglementRepository.GetAll(DomaineDossier.Client, societe.No, clientNo, isDossierCommercial)
			where caisses.Any((int y) => y == x.CaisseNo)
			orderby x.Date descending
			select x).ToList();
	}

	public IEnumerable<SocieteEtat> GetAllEtat(Societe societe = null)
	{
		societe = societe ?? Societe;
		return _societeImpressionRepository.GetAll(societe.No);
	}

	public IEnumerable<SocieteEtat> GetAllEtatBySource(int sourceNo, Societe societe = null)
	{
		societe = societe ?? Societe;
		return (from x in GetAllEtat(societe)
			where x.Source == sourceNo
			select x).ToList();
	}

	public IEnumerable<Ecart> GetAllGainPerte(ErpDomaine domaine, Societe societe = null)
	{
		societe = societe ?? Societe;
		if (!Utilisateur.IsAdmin)
		{
			return _ecartRepository.GetAllGainPerte(societe.No, domaine, (from a in GetAllAutorizedSouches(domaine)
				select a.SoucheNo).ToArray());
		}
		return _ecartRepository.GetAllGainPerte(societe.No, domaine);
	}

	public IEnumerable<Ecart> GetAllGainPerte(DateTime dateDebut, DateTime dateFin, EtatComptabilite etat, Societe societe = null)
	{
		societe = societe ?? Societe;
		return _ecartRepository.GetAllGainPerte(dateDebut, dateFin, etat, societe.No);
	}

	public IEnumerable<GridLayoutFilter> GetAllGridLayoutFilter(Guid guid)
	{
		if (guid == Guid.Empty)
		{
			throw new ArgumentException("Le guid du grid est invalide!");
		}
		return _gridLayoutFilterRepository.GetAll(guid);
	}

	public IEnumerable<GridLayout> GetAllGridLayouts(Guid guid)
	{
		if (guid == Guid.Empty)
		{
			throw new ArgumentException("Le guid du grid est invalide!");
		}
		return _gridLayoutRepository.GetAll(guid);
	}

	public IEnumerable<LigneRappel> GetAllLigneRappel(int rappelNo)
	{
		return _ligneRappelRepository.GetAll(rappelNo);
	}

	public IEnumerable<LigneRappel> GetAllLigneRappel(Societe societe = null)
	{
		societe = societe ?? Societe;
		return _ligneRappelRepository.GetAllLigneRappel(societe.No);
	}

	public List<MouvementBancaire> GetAllMouvementBancairesReglementClientByBordreauNumero(string bordereauNumero, Societe societe = null)
	{
		societe = societe ?? Societe;
		return _mvtBancaireRepository.GetAllReglementClientByBordreauNumero(societe.No, bordereauNumero);
	}

	public IEnumerable<MouvementBancaire> GetAllMouvementBancaires(int banqueNo, DateTime dateDe, DateTime dateA, bool isPointer, Societe societe = null)
	{
		societe = societe ?? Societe;
		TypeBordereauxManager bordereauxTypeManager = Groupe.BordereauxTypeManager;
		InformationBanqueManager informationBanqueManager = Groupe.InformationBanqueManager;
		if (banqueNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorBanqueInvalide);
		}
		SocieteDevise defaultDeviseSociete = societe.GetDefaultDeviseSociete();
		if (defaultDeviseSociete == null)
		{
			throw new ApplicationException("Impossible de charger la devise société.");
		}
		List<TypeBordereau> typeBordereauChequeTraite = (from t in bordereauxTypeManager.GetAll()
			where t.GetModeReglements().Any((ModeReglement m) => m.Type == ReglementType.Cheque || m.Type == ReglementType.Traite)
			select t).ToList();
		List<InformationsBanqueBordereau> infoBanqueTypeChequeTraite = informationBanqueManager.GetByBanqueId(banqueNo)?.Bordereaux.ToList();
		return _mvtBancaireRepository.GetAll(banqueNo, societe.No, dateDe, dateA, isPointer, defaultDeviseSociete.No, typeBordereauChequeTraite, infoBanqueTypeChequeTraite);
	}

	public List<MouvementEscompte> GetAllMouvementEscompteByBanqueAndDate(int banqueNo, DateTime date, int nbreJourCouverture, Societe societe = null)
	{
		societe = societe ?? Societe;
		if (banqueNo <= 0)
		{
			throw new ArgumentException("banqueNo");
		}
		if (nbreJourCouverture < 0)
		{
			throw new ArgumentException("Le nombre de jour de couverture est invalide!");
		}
		if (date == DateTime.MinValue)
		{
			throw new ArgumentException("La date d'échéance est invalide!");
		}
		return _mouvementEscompteRepositry.GetAllByBanqueAndDate(banqueNo, date, nbreJourCouverture, societe.No).ToList();
	}

	public IEnumerable<Rappel> GetAllRappel(Societe societe = null)
	{
		societe = societe ?? Societe;
		return _rappelRepository.GetAll(societe.No);
	}

	public IEnumerable<Rappel> GetAllRappelByClient(int clientNo, Societe societe = null)
	{
		societe = societe ?? Societe;
		return _rappelRepository.GetAllByClient(societe.No, clientNo);
	}

	public IEnumerable<Rappel> GetAllRappelEcheance(int echeanceNo, Societe societe = null)
	{
		societe = societe ?? Societe;
		return _rappelRepository.GetAllRappelEchence(societe.No, echeanceNo);
	}

	public IEnumerable<Rappel> GetAllRappelParents(int clientNo, DomaineRappel domaine, Societe societe = null)
	{
		societe = societe ?? Societe;
		return _rappelRepository.GetAllParents(societe.No, clientNo, domaine);
	}

	public IEnumerable<Rappel> GetAllRappelRecouvrementClotureAndBeforeDate(DateTime dateFin, bool isCloture, Societe societe = null)
	{
		societe = societe ?? Societe;
		return _rappelRepository.GetAllRecouvrementClotureBeforeDate(societe.No, dateFin, isCloture);
	}

	public IEnumerable<Echeance> GetAllSuiviRecouvrement(ErpDomaine domaine, DateTime? dateDebut = null, Societe societe = null)
	{
		societe = societe ?? Societe;
		if (!Utilisateur.IsAdmin)
		{
			return _echeanceRepository.GetAll(domaine, societe.No, nonPaye: true, onlySpe: false, dateDebut, null, (from a in GetAllAutorizedSouches(domaine)
				select a.SoucheNo).ToArray());
		}
		return _echeanceRepository.GetAll(domaine, societe.No, nonPaye: true, onlySpe: false, dateDebut);
	}

	public IEnumerable<Echeance> GetAllSuiviRecouvrementNonReserver(ErpDomaine domaine, DateTime? dateDebut = null, Societe societe = null)
	{
		societe = societe ?? Societe;
		if (!Utilisateur.IsAdmin)
		{
			return _echeanceRepository.GetAllNonReserver(domaine, societe.No, nonPaye: true, onlySpe: false, dateDebut, null, (from a in GetAllAutorizedSouches(domaine)
				select a.SoucheNo).ToArray());
		}
		return _echeanceRepository.GetAllNonReserver(domaine, societe.No, nonPaye: true, onlySpe: false, dateDebut);
	}

	public IEnumerable<Echeance> GetAllSuiviRecouvrementEcheanceFacture(ErpDomaine domaine, DateTime echeanceDebut, Societe societe = null)
	{
		societe = societe ?? Societe;
		if (!Utilisateur.IsAdmin)
		{
			return _echeanceRepository.GetAll(domaine, societe.No, nonPaye: true, echeanceDebut, onlySpe: false, (from a in GetAllAutorizedSouches(domaine)
				select a.SoucheNo).ToArray());
		}
		return _echeanceRepository.GetAll(domaine, societe.No, nonPaye: true, echeanceDebut);
	}

	public IEnumerable<TypeBordereau> GetAllTypeBordereaux()
	{
		TypeBordereauxManager bordereauxTypeManager = Groupe.BordereauxTypeManager;
		List<CaisseModeReglement> modes = Societe.GetCaisses().SelectMany((Caisse x) => x.GetAllModes()).Distinct()
			.ToList();
		return from t in bordereauxTypeManager.GetAll()
			where t.GetModeReglements().Any((ModeReglement m) => modes.Any((CaisseModeReglement x) => x.No == m.No))
			select t;
	}

	public IEnumerable<int> GetAutorizedSoucheNoCollectionVente()
	{
		if (Utilisateur.IsAdmin && !Societe.HasSoucheRestrictionClt)
		{
			throw new ApplicationException("Operation invalide! [GetAutorizedSoucheNoCollection");
		}
		ErpDomaine domaine = ErpDomaine.Vente;
		if (Utilisateur.IsAdmin)
		{
			return from s in Societe.GetSouches(domaine)
				select s.SoucheNo;
		}
		IEnumerable<int> enumerable = from a in _autorisationSoucheRepository.GetAll(domaine, Utilisateur.No, Societe.No)
			select a.SoucheNo;
		if (!Societe.HasSoucheRestrictionClt)
		{
			return enumerable;
		}
		return (from s in Societe.GetSouches(domaine)
			select s.SoucheNo).Intersect(enumerable);
	}

	public IEnumerable<int> GetAutorizedSoucheNoCollectionAchat()
	{
		if (Utilisateur.IsAdmin && !Societe.HasSoucheRestrictionFrs)
		{
			throw new ApplicationException("Operation invalide! [GetAutorizedSoucheNoCollection");
		}
		ErpDomaine domaine = ErpDomaine.Achat;
		if (Utilisateur.IsAdmin)
		{
			return from s in Societe.GetSouches(domaine)
				select s.SoucheNo;
		}
		IEnumerable<int> enumerable = from a in _autorisationSoucheRepository.GetAll(domaine, Utilisateur.No, Societe.No)
			select a.SoucheNo;
		if (!Societe.HasSoucheRestrictionFrs)
		{
			return enumerable;
		}
		return (from s in Societe.GetSouches(domaine)
			select s.SoucheNo).Intersect(enumerable);
	}

	public BanqueTiers GetBanqueTiers(string banque, string tiersNum, Societe societe = null)
	{
		if (string.IsNullOrEmpty(tiersNum))
		{
			throw new ArgumentNullException("tiersNum");
		}
		if (string.IsNullOrEmpty(banque))
		{
			throw new ArgumentNullException("banque");
		}
		societe = societe ?? Societe;
		return _banqueTiersRepository.Get(banque, tiersNum, societe.No);
	}

	public IEnumerable<BanqueTiers> GetBanquesTiers(string clientNum, Societe societe = null)
	{
		societe = societe ?? Societe;
		if (string.IsNullOrEmpty(clientNum))
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorClientNo);
		}
		return _banqueTiersRepository.GetAll(clientNum, societe.No);
	}

	public Caution GetCaution(int cautionNo)
	{
		if (cautionNo == 0)
		{
			throw new ArgumentNullException("cautionNo");
		}
		return _cautionRepository.Get(cautionNo);
	}

	public IEnumerable<DetailHistoriqueRappelEcheance> GetDetailHistoriqueRappelEcheance(int echeanceNo, Societe societe = null)
	{
		societe = societe ?? Societe;
		return _detailHistoriqueRappelEcheanceRepository.GetAll(societe.No, echeanceNo);
	}

	public DossierReglement GetDossierReglement(int dossierNo)
	{
		return _dossierReglementRepository.GetDossier(dossierNo);
	}

	public DossierReglement GetDossierReglement(DomaineDossier domaine, string dossierNumero)
	{
		if (string.IsNullOrEmpty(dossierNumero))
		{
			throw new ArgumentNullException("dossierNumero");
		}
		return _dossierReglementRepository.GetDossier(domaine, dossierNumero, Societe.No);
	}

	public int GetEcheanceEcartNo(int echeanceNo)
	{
		return _echeanceRepository.GetEcheanceEcart(echeanceNo);
	}

	public IEnumerable<EngagementClient> GetEngagementAllClients(DateTime dateEchu, Societe societe = null)
	{
		societe = societe ?? Societe;
		if (societe.TypeCalculEngagement != TypeCalculEngagement.Echeance)
		{
			return _engagementClientRepository.GetEngagementAllClientsComparedToRapprochement(societe.No, dateEchu);
		}
		return _engagementClientRepository.GetEngagementAllClientsComparedToEcheance(societe.No, dateEchu);
	}

	public IEnumerable<EngagementFournisseur> GetEngagementAllFournisseur(DateTime dateEchu, Societe societe = null)
	{
		societe = societe ?? Societe;
		return _engagementFournisseurRepository.GetEngagementAllFournisseurComparedToEcheance(societe.No, dateEchu);
	}

	public EngagementFournisseur GetEngagementFournisseur(int fournisseurNo, DateTime dateEchu, Societe societe = null)
	{
		societe = societe ?? Societe;
		return _engagementFournisseurRepository.GetEngagementFournisseurComparedToEcheance(fournisseurNo, societe.No, dateEchu);
	}

	public IList<EngagementClientDetails> GetEngagementAllClientDetails(DateTime dateEchu, Societe societe = null)
	{
		societe = societe ?? Societe;
		return ((societe.TypeCalculEngagement == TypeCalculEngagement.Echeance) ? _engagementClientDetailsRepository.GetEngagementComparedToEcheance(societe.No, dateEchu) : _engagementClientDetailsRepository.GetEngagementComparedToRapprochement(societe.No, dateEchu)).ToList();
	}

	public EngagementClient GetEngagementClient(int clientNo, DateTime dateEchu, Societe societe = null)
	{
		if (clientNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorClientNo);
		}
		societe = societe ?? Societe;
		if (societe.TypeCalculEngagement != TypeCalculEngagement.Echeance)
		{
			return _engagementClientRepository.GetEngagementClientComparedToRapprochement(clientNo, societe.No, dateEchu);
		}
		return _engagementClientRepository.GetEngagementClientComparedToEcheance(clientNo, societe.No, dateEchu);
	}

	public GridLayoutFilter GetGridFilter(int no)
	{
		if (no <= 0)
		{
			throw new ArgumentException("Le filtre no est invalide!");
		}
		return _gridLayoutFilterRepository.Get(no);
	}

	public GridLayoutFilter GetGridFilter(string name, Guid guid)
	{
		if (string.IsNullOrEmpty(name))
		{
			throw new ArgumentException("Le nom du filtre est invalide!");
		}
		if (guid == Guid.Empty)
		{
			throw new ArgumentException("Le guid du grid est invalide!");
		}
		return _gridLayoutFilterRepository.Get(name, guid);
	}

	public GridLayout GetGridLayout(int no)
	{
		return _gridLayoutRepository.Get(no);
	}

	public GridLayout GetGridLayout(string name, Guid guid)
	{
		if (string.IsNullOrEmpty(name))
		{
			throw new ArgumentException("Le nom du schéma est invalide!");
		}
		if (guid == Guid.Empty)
		{
			throw new ArgumentException("Le guid du grid est invalide!");
		}
		return _gridLayoutRepository.Get(name, guid);
	}

	public override int GetHashCode()
	{
		return 17 * 23 + Societe.No;
	}

	public IEnumerable<LigneDossierReglement> GetLignesDossier(int dossierNo)
	{
		return _ligneDossierReglementRepository.GetAll(dossierNo).ToList();
	}

	public IEnumerable<LigneEngagementClient> GetLignesEngagementClient(int clientNo, DateTime date, Societe societe = null)
	{
		if (clientNo <= 0)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorClientNo);
		}
		societe = societe ?? Societe;
		if (societe.TypeCalculEngagement == TypeCalculEngagement.Echeance)
		{
			return _ligneEngagementClientRepository.GetLigneEngagementClientComparedToEcheance(societe.No, clientNo, date);
		}
		return _ligneEngagementClientRepository.GetLigneEngagementClientComparedToRapprochement(societe.No, clientNo, date);
	}

	public MouvementEscompte GetMouvementEscompte(int no)
	{
		if (no <= 0)
		{
			throw new ArgumentException("le mouvement est introuvable!");
		}
		return _mouvementEscompteRepositry.Get(no);
	}

	public string GetNumeroPieceCourante(EntityNumerotation numerotation, int societeNo)
	{
		Societe societe = Get(societeNo);
		if (societe == null)
		{
			throw new ApplicationException("Impossible de charger la societe.");
		}
		return GetNumeroPieceCourante(numerotation, societe);
	}

	public string GetNumeroPieceCourante(EntityNumerotation numerotation, Societe societe = null)
	{
		societe = societe ?? Societe;
		return _societeRepository.GetNumeroPieceCourante(societe.No, numerotation);
	}

	public string GetNumeroPieceCourante(string prefix, bool isAnnee, bool isMois, int count, EntityNumerotation numerotation, int? societeNo = null)
	{
		return _societeRepository.GetNumeroPieceCourante(prefix, isAnnee, isMois, count, numerotation, societeNo);
	}

	public Rappel GetRappel(int no)
	{
		return _rappelRepository.Get(no);
	}

	public Rappel GetRappel(string numero, Societe societe = null)
	{
		societe = societe ?? Societe;
		return _rappelRepository.Get(societe.No, numero);
	}

	public IEnumerable<SocieteTypeBordereau> GetSocTypeBordereaux(Societe societe = null)
	{
		societe = societe ?? Societe;
		return _socTypeBordereauRepository.GetAll(societe.No);
	}

	public SolvabiliteClient GetSolvabiliteClient(int clientNo, Societe societe = null)
	{
		if (clientNo <= 0)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorClientNo);
		}
		societe = societe ?? Societe;
		return _solvabiliteClientRepository.GetSolvabiliteClient(clientNo, societe.No);
	}

	public decimal GetTotalCreditClient(int clientNo, Societe societe = null)
	{
		if (clientNo <= 0)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorClientNo);
		}
		societe = societe ?? Societe;
		return _solvabiliteClientRepository.GetTotalCredit(clientNo, societe.No);
	}

	public decimal GetTotalDebitClient(int clientNo, Societe societe = null)
	{
		if (clientNo <= 0)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorClientNo);
		}
		societe = societe ?? Societe;
		return _solvabiliteClientRepository.GetTotalDebit(clientNo, societe.No);
	}

	public SolvabiliteFournisseur GetSolvabiliteFournisseur(int fournisseurNo, Societe societe = null)
	{
		if (fournisseurNo <= 0)
		{
			throw new ArgumentNullException("fournisseurNo");
		}
		societe = societe ?? Societe;
		return _solvabiliteFournisseurRepository.GetSolvabiliteFournisseur(fournisseurNo, societe.No);
	}

	public decimal GetTotalCreditFournisseur(int fournisseurNo, Societe societe = null)
	{
		if (fournisseurNo <= 0)
		{
			throw new ArgumentNullException("fournisseurNo");
		}
		societe = societe ?? Societe;
		return _solvabiliteFournisseurRepository.GetTotalCredit(fournisseurNo, societe.No);
	}

	public decimal GetTotalDebitFournisseur(int fournisseurNo, Societe societe = null)
	{
		if (fournisseurNo <= 0)
		{
			throw new ArgumentNullException("fournisseurNo");
		}
		societe = societe ?? Societe;
		return _solvabiliteFournisseurRepository.GetTotalDebit(fournisseurNo, societe.No);
	}

	public Dictionary<int, decimal> GetTotalReglementSansImpayeAllTiers(MouvementDomaine domaine, DateTime dateMin, DateTime dateMax, Societe societe = null)
	{
		societe = societe ?? Societe;
		return _recapTiersRepository.GetTotalReglementSansImpayeAllTiers(societe.No, domaine, dateMin, dateMax);
	}

	public UtilisateurGrid GetUtilisateurGrid(int no)
	{
		if (no <= 0)
		{
			throw new ArgumentNullException("no");
		}
		return _utilisateurGridRepository.Get(no);
	}

	public UtilisateurGrid GetUtilisateurGrid(Guid gridGuid, Utilisateur utilisateur = null)
	{
		utilisateur = utilisateur ?? Utilisateur;
		if (gridGuid == Guid.Empty)
		{
			throw new ArgumentException("Le guid du grid est invalide!");
		}
		return _utilisateurGridRepository.Get(gridGuid, utilisateur.No);
	}

	public bool HasErpConnection(Societe societe = null)
	{
		societe = societe ?? Societe;
		ErpConnection erpConnection = societe.ErpConnection;
		if (erpConnection == null)
		{
			return false;
		}
		if (!erpConnection.IsValid())
		{
			return false;
		}
		string connection = erpConnection.GetConnection();
		if (string.IsNullOrEmpty(connection))
		{
			return false;
		}
		try
		{
			using SqlConnection sqlConnection = new SqlConnection(connection);
			sqlConnection.Open();
		}
		catch
		{
			return false;
		}
		return true;
	}

	public bool HasEtapeCompta(int sociteNo, int typeBordereauNo, int order)
	{
		return _socTypeBordereauRepository.HasEtapeCompta(sociteNo, typeBordereauNo, order);
	}

	public bool HasMouvementBancaireNonRapproche(int banqueNo, DateTime dateA, Societe societe = null)
	{
		societe = societe ?? Societe;
		if (banqueNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorBanqueInvalide);
		}
		SocieteDevise defaultDeviseSociete = societe.GetDefaultDeviseSociete();
		if (defaultDeviseSociete == null)
		{
			throw new ApplicationException("Impossible de charger la devise société.");
		}
		return _mvtBancaireRepository.HasMouvementBancaireNonRapproche(banqueNo, societe.No, dateA, defaultDeviseSociete.No);
	}

	public Societe InitNewSociete(string raisonSociale)
	{
		if (string.IsNullOrEmpty(raisonSociale))
		{
			throw new ArgumentNullException("raisonSociale");
		}
		return new Societe
		{
			RaisonSociale = raisonSociale
		};
	}

	public void LettreDossierReglement(int dossierNo, string lettre)
	{
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_dossierReglementRepository.Lettre(dossierNo, lettre);
		_notifyService.Notify(TypeEntity.Dossier, dossierNo, TypeAction.Modification, Societe.No);
		transactionScope.Complete();
	}

	public void DeLettrerDossierReglement(int dossierNo)
	{
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_dossierReglementRepository.DeLettrer(dossierNo);
		_notifyService.Notify(TypeEntity.Dossier, dossierNo, TypeAction.Modification, Societe.No);
		transactionScope.Complete();
	}

	public bool ModeCanBeModified(int modeNo, Societe societe = null)
	{
		if (modeNo <= 0)
		{
			throw new ArgumentNullException("modeNo");
		}
		societe = societe ?? Societe;
		SocieteModeReglement mode = societe.GetMode(modeNo);
		if (mode == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorModeInvalide);
		}
		bool flag = societe.GetCaisses().Any((Caisse x) => x.GetAllModes().Any((CaisseModeReglement m) => m.No == modeNo));
		if (!EcheanceExiste(mode))
		{
			return !flag;
		}
		return false;
	}

	public int ModeCorrespondanceErp(int modeNo, Societe societe = null)
	{
		if (modeNo <= 0)
		{
			throw new ArgumentNullException("modeNo");
		}
		societe = societe ?? Societe;
		return (societe.GetMode(modeNo) ?? throw new ApplicationException(TresorerieCoreMessages.ErrorModeInvalide)).ErpNo;
	}

	public void ModeCreate(int modeNo, int erpNo, bool isActive, Societe societe = null)
	{
		if (modeNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorModeNo);
		}
		if (erpNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorErpNo);
		}
		if (!Utilisateur.IsAdmin)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorCreateMode);
		}
		societe = societe ?? Societe;
		if (societe.HasMode(modeNo))
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorModeNo);
		}
		ModeReglement modeReglement = Groupe.ModeReglementManager.Get(modeNo);
		if (modeReglement == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorModeInvalide);
		}
		if (modeReglement.EnSommeil)
		{
			throw new ApplicationException("Opération invalide! Le mode est en sommeil!");
		}
		if (modeReglement.Type != ReglementType.Autre && ModeErpHasMapping(erpNo, societe))
		{
			throw new ApplicationException(rcRessources.CorrespondanceExiste);
		}
		SocieteModeReglement mode = societe.InitNewMode(modeReglement, erpNo, isActive);
		_societeModeRepository.Create(mode);
		societe.RefreshMode();
		_notifyService.Notify(TypeEntity.Parametrage, 0, TypeAction.Modification, societe.No);
	}

	public void ModeDelete(int modeNo, Societe societe = null)
	{
		if (modeNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorModeNo);
		}
		societe = societe ?? Societe;
		SocieteModeReglement mode = societe.GetMode(modeNo);
		if (mode == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorModeInvalide);
		}
		if (!Utilisateur.IsAdmin)
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorDeleteMode, mode.Code));
		}
		_societeModeRepository.Delete(mode);
		_notifyService.Notify(TypeEntity.Parametrage, 0, TypeAction.Modification, societe.No);
	}

	public bool ModeErpHasMapping(int erpNo, Societe societe = null)
	{
		societe = societe ?? Societe;
		return _societeModeRepository.HasErpMapping(erpNo, societe.No) != null;
	}

	public SocieteModeReglement GetMappingModeErp(int erpNo, Societe societe = null)
	{
		societe = societe ?? Societe;
		return _societeModeRepository.HasErpMapping(erpNo, societe.No);
	}

	public void ModeUpdate(int modeNo, int erpNo, bool isActive, Societe societe = null)
	{
		if (modeNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorModeNo);
		}
		if (erpNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorErpNo);
		}
		societe = societe ?? Societe;
		SocieteModeReglement mode = societe.GetMode(modeNo);
		if (mode == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorModeInvalide);
		}
		if (!Utilisateur.IsAdmin)
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorUpdateMode, mode.Code));
		}
		if (mode.ErpNo != erpNo && mode.Type != ReglementType.Autre && ModeErpHasMapping(erpNo, societe))
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorModeCorrespondant);
		}
		mode.ErpNo = erpNo;
		mode.IsActive = isActive;
		_societeModeRepository.Update(mode);
		societe.RefreshMode();
		_notifyService.Notify(TypeEntity.Parametrage, 0, TypeAction.Modification, societe.No);
	}

	public void MouvementBancaireRapproched(int mouvementNo, MouvementDomaine domaine, DateTime dateRapprochement, TypeMouvementBancaire mvtType, string extraitNum)
	{
		if (mouvementNo <= 0)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorMvtNo);
		}
		if (string.IsNullOrEmpty(extraitNum) || string.IsNullOrWhiteSpace(extraitNum))
		{
			throw new ApplicationException("La pièce trésorerie est obligatoire.");
		}
		bool flag = false;
		switch (mvtType)
		{
		case TypeMouvementBancaire.Cheque:
		case TypeMouvementBancaire.Traite:
		case TypeMouvementBancaire.Virement:
			if (domaine == MouvementDomaine.ReglementClient)
			{
				ReglementClient reglementClient = CaisseManager.ReglementGet(mouvementNo);
				if (reglementClient == null)
				{
					throw new ApplicationException(TresorerieCoreMessages.ErrorReglementInvalide);
				}
				if (reglementClient.IsPointe)
				{
					throw new ApplicationException($"Le règlement [{reglementClient.Numero}] est déjà rapproché.");
				}
				if (reglementClient.IsImpaye != ImpayeEtat.NonImpaye)
				{
					throw new ApplicationException("Le règlement " + reglementClient.Numero + " est impayé.");
				}
				if (reglementClient.Preavis == EtatPreavis.RegulariseImpaye)
				{
					throw new ApplicationException("Le règlement " + reglementClient.Numero + " est préavisé, régularisé impayé.");
				}
				flag = reglementClient.Preavis == EtatPreavis.Preavis;
			}
			if (domaine == MouvementDomaine.ReglementFournisseur)
			{
				ReglementFournisseur reglementFournisseur = CaisseManager.ReglementFournisseurGet(mouvementNo);
				if (reglementFournisseur == null)
				{
					throw new ApplicationException(TresorerieCoreMessages.ErrorReglementInvalide);
				}
				if (reglementFournisseur.IsPointe)
				{
					throw new ApplicationException($"Le règlement [{reglementFournisseur.Numero}] est déjà rapproché.");
				}
				if (Societe.RecupererPieceFrs && (mvtType == TypeMouvementBancaire.Cheque || mvtType == TypeMouvementBancaire.Traite) && reglementFournisseur.Recuperer != RemisFournisseur.Recuperer)
				{
					throw new ApplicationException("Le règlement [" + reglementFournisseur.Numero + "] doit être récupéré.");
				}
				if (Societe.VerifRapprochementDecaisse && mvtType == TypeMouvementBancaire.Traite && reglementFournisseur.IsComptabilise != EtatComptabilite.TraiteFournisseurComptabilise)
				{
					throw new ApplicationException("Impossible de rapprocher un règlement traite fournisseur non comptabilisé n°[" + reglementFournisseur.Numero + "]");
				}
			}
			break;
		case TypeMouvementBancaire.Depense:
		{
			MouvementDepense mouvementDepense = Groupe.DepenseManager.GetMouvementDepense(mouvementNo);
			if (mouvementDepense == null)
			{
				throw new ApplicationException("Impossible de charger la dépense.");
			}
			if (mouvementDepense.IsPointe)
			{
				throw new ApplicationException($"La dépense [{mouvementDepense.Numero}] est déjà rapprochée.");
			}
			break;
		}
		case TypeMouvementBancaire.Versement:
		{
			Bordereau bordereau = CaisseManager.BordereauGet(mouvementNo);
			if (bordereau == null)
			{
				throw new ApplicationException(TresorerieCoreMessages.ErrorBordereauNo);
			}
			if (!bordereau.IsRemis)
			{
				throw new InvalidOperationException(TresorerieCoreMessages.ErrorBordereauNonRemis);
			}
			if (bordereau.IsPointe)
			{
				throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorBordereauRapproche, bordereau.Numero));
			}
			break;
		}
		case TypeMouvementBancaire.Retrait:
		{
			AlimentationCaisse alimentationCaisse = Groupe.AlimentationCaisseManager.Get(mouvementNo);
			if (alimentationCaisse == null)
			{
				throw new ArgumentException("Alimentation de caisse invalide!");
			}
			if (alimentationCaisse.IsRapproche)
			{
				throw new ArgumentException("L'alimentation de caisse [" + alimentationCaisse.Numero + "] est déjà rapproché!");
			}
			break;
		}
		case TypeMouvementBancaire.Interne:
		{
			VirementInterne virementInterne = CaisseManager.VirementInterneGet(mouvementNo);
			if (virementInterne == null)
			{
				throw new ArgumentException("Le virement interne invalide!");
			}
			if (virementInterne.IsPointer)
			{
				throw new ArgumentException("Le virement interne [" + virementInterne.Numero + "] est déjà rapproché!");
			}
			break;
		}
		case TypeMouvementBancaire.RemboursementClient:
		{
			RemboursementClientAllType remboursementClientAllType = CaisseManager.RemboursementClientAllTypeGet(mouvementNo);
			if (remboursementClientAllType == null)
			{
				throw new ArgumentException("Le remboursement client invalide!");
			}
			if (remboursementClientAllType.IsRemboursementChequePointe)
			{
				throw new ArgumentException("Le remboursement client [" + remboursementClientAllType.Numero + "] est déjà rapproché!");
			}
			break;
		}
		case TypeMouvementBancaire.VirementTiers:
		{
			VirementTiers virementTiers = CaisseManager.VirementTiersGet(mouvementNo);
			if (virementTiers == null)
			{
				throw new ArgumentException("Le virement tiers invalide!");
			}
			if (virementTiers.IsPointer)
			{
				throw new ArgumentException("Le virement tiers [" + virementTiers.Numero + "] est déjà rapproché!");
			}
			break;
		}
		default:
			throw new NotImplementedException(TresorerieCoreMessages.ErrorMvtNonGenere);
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		switch (mvtType)
		{
		case TypeMouvementBancaire.Cheque:
		case TypeMouvementBancaire.Traite:
		case TypeMouvementBancaire.Virement:
			if (flag)
			{
				CaisseManager.ReglementClientPreavisRegulariserPayer(mouvementNo);
			}
			_mvtBancaireRepository.PointerMouvement(mouvementNo, isPointe: true, dateRapprochement, extraitNum);
			_notifyService.Notify((domaine == MouvementDomaine.ReglementClient) ? TypeEntity.Reglement : TypeEntity.ReglementFournisseur, mouvementNo, TypeAction.Modification, Societe.No);
			break;
		case TypeMouvementBancaire.Versement:
			_mvtBancaireRepository.PointerMouvement(mouvementNo, isPointe: true, dateRapprochement, extraitNum);
			_notifyService.Notify(TypeEntity.Bordereau, mouvementNo, TypeAction.Modification, Societe.No);
			break;
		case TypeMouvementBancaire.Retrait:
			_mvtBancaireRepository.PointerMouvement(mouvementNo, isPointe: true, dateRapprochement, extraitNum);
			_notifyService.Notify(TypeEntity.Alimentation, mouvementNo, TypeAction.Modification, Societe.No);
			break;
		case TypeMouvementBancaire.Interne:
			_mvtBancaireRepository.PointerVirementInterne(mouvementNo, isPointe: true, dateRapprochement, extraitNum);
			_notifyService.Notify(TypeEntity.VirementInterne, mouvementNo, TypeAction.Modification, Societe.No);
			break;
		case TypeMouvementBancaire.RemboursementClient:
			_mvtBancaireRepository.PointerRemboursement(mouvementNo, isPointe: true, dateRapprochement, extraitNum);
			_notifyService.Notify(TypeEntity.RemboursementClient, mouvementNo, TypeAction.Modification, Societe.No);
			break;
		case TypeMouvementBancaire.VirementTiers:
			_mvtBancaireRepository.PointerVirementInterne(mouvementNo, isPointe: true, dateRapprochement, extraitNum);
			_notifyService.Notify(TypeEntity.VirementTiers, mouvementNo, TypeAction.Modification, Societe.No);
			break;
		case TypeMouvementBancaire.Depense:
			_mvtBancaireRepository.PointerMouvement(mouvementNo, isPointe: true, dateRapprochement, extraitNum);
			_notifyService.Notify(TypeEntity.Depense, mouvementNo, TypeAction.Modification, Societe.No);
			break;
		default:
			throw new NotImplementedException(TresorerieCoreMessages.ErrorMvtNonGenere);
		}
		transactionScope.Complete();
	}

	public void MouvementBancaireUnRapproched(int mouvementNo, MouvementDomaine domaine, TypeMouvementBancaire mvtType)
	{
		if (mouvementNo <= 0)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorMvtNo);
		}
		bool flag = false;
		switch (mvtType)
		{
		case TypeMouvementBancaire.Cheque:
		case TypeMouvementBancaire.Traite:
		case TypeMouvementBancaire.Virement:
			if (domaine == MouvementDomaine.ReglementClient)
			{
				ReglementClient reglementClient = CaisseManager.ReglementGet(mouvementNo);
				if (reglementClient == null)
				{
					throw new ApplicationException(TresorerieCoreMessages.ErrorReglementInvalide);
				}
				if (reglementClient.IsImpaye == ImpayeEtat.Impaye && !reglementClient.IsPointe)
				{
					throw new ApplicationException("Le règlement " + reglementClient.Numero + " est impayé.");
				}
				if (!reglementClient.IsPointe)
				{
					throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorReglementDerapproche, reglementClient.Numero));
				}
				flag = reglementClient.Preavis == EtatPreavis.RegularisePaye;
			}
			if (domaine == MouvementDomaine.ReglementFournisseur)
			{
				ReglementFournisseur reglementFournisseur = CaisseManager.ReglementFournisseurGet(mouvementNo);
				if (reglementFournisseur == null)
				{
					throw new ApplicationException(TresorerieCoreMessages.ErrorReglementInvalide);
				}
				if (!reglementFournisseur.IsPointe)
				{
					throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorReglementDerapproche, reglementFournisseur.Numero));
				}
				if (mvtType == TypeMouvementBancaire.Traite && reglementFournisseur.IsComptabilise == EtatComptabilite.TraiteFournisseurComptabilise)
				{
					throw new ApplicationException("Impossible d'annuler le rapprochement d'un règlement traite fournisseur comptabilisé, n°[" + reglementFournisseur.Numero + "]");
				}
			}
			break;
		case TypeMouvementBancaire.Depense:
		{
			MouvementDepense mouvementDepense = Groupe.DepenseManager.GetMouvementDepense(mouvementNo);
			if (mouvementDepense == null)
			{
				throw new ApplicationException("Impossible de charger la dépense.");
			}
			if (!mouvementDepense.IsPointe)
			{
				throw new ApplicationException("La dépense [" + mouvementDepense.Numero + "] n'est pas rapprochée.");
			}
			break;
		}
		case TypeMouvementBancaire.Versement:
		{
			Bordereau bordereau = CaisseManager.BordereauGet(mouvementNo);
			if (bordereau == null)
			{
				throw new ApplicationException(TresorerieCoreMessages.ErrorBordereauNo);
			}
			if (!bordereau.IsRemis)
			{
				throw new InvalidOperationException(TresorerieCoreMessages.ErrorBordereauNonRemis);
			}
			if (!bordereau.IsPointe)
			{
				throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorBordereauDerapproche, bordereau.Numero));
			}
			break;
		}
		case TypeMouvementBancaire.Retrait:
			if (!(Groupe.AlimentationCaisseManager.Get(mouvementNo) ?? throw new ArgumentException("Alimentation de caisse invalide!")).IsRapproche)
			{
				throw new ArgumentException("L'alimentation de caisse n'est pas rapprochée!");
			}
			break;
		case TypeMouvementBancaire.Interne:
			if (!(CaisseManager.VirementInterneGet(mouvementNo) ?? throw new ArgumentException("Virement interne de caisse invalide!")).IsPointer)
			{
				throw new ArgumentException("Le virement interne n'est pas rapproché!");
			}
			break;
		case TypeMouvementBancaire.RemboursementClient:
		{
			RemboursementClientAllType remboursementClientAllType = CaisseManager.RemboursementClientAllTypeGet(mouvementNo);
			if (remboursementClientAllType == null)
			{
				throw new ArgumentException("Le remboursement client invalide!");
			}
			if (!remboursementClientAllType.IsRemboursementChequePointe)
			{
				throw new ArgumentException($"Le remboursement client [{remboursementClientAllType.Numero}] n'est pas rapproché!");
			}
			break;
		}
		case TypeMouvementBancaire.VirementTiers:
		{
			VirementTiers virementTiers = CaisseManager.VirementTiersGet(mouvementNo);
			if (virementTiers == null)
			{
				throw new ArgumentException("Le virement tiers invalide!");
			}
			if (!virementTiers.IsPointer)
			{
				throw new ArgumentException("Le virement tiers [" + virementTiers.Numero + "] n'est pas rapproché!");
			}
			break;
		}
		default:
			throw new NotImplementedException(TresorerieCoreMessages.ErrorMvtNonGenere);
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		switch (mvtType)
		{
		case TypeMouvementBancaire.Cheque:
		case TypeMouvementBancaire.Traite:
		case TypeMouvementBancaire.Virement:
			_mvtBancaireRepository.PointerMouvement(mouvementNo, isPointe: false, DateTime.Now, "");
			if (flag)
			{
				ReglementClient reglementClient2 = CaisseManager.ReglementGet(mouvementNo);
				if (reglementClient2 == null)
				{
					throw new ApplicationException("Impossible de charger le règlement.");
				}
				reglementClient2.Preavis = EtatPreavis.Preavis;
				reglementClient2.ModificateurNo = Utilisateur.No;
				_reglementClientRepository.Update(reglementClient2);
			}
			_notifyService.Notify((domaine == MouvementDomaine.ReglementClient) ? TypeEntity.Reglement : TypeEntity.ReglementFournisseur, mouvementNo, TypeAction.Modification, Societe.No);
			break;
		case TypeMouvementBancaire.Versement:
			_mvtBancaireRepository.PointerMouvement(mouvementNo, isPointe: false, DateTime.Now, "");
			_notifyService.Notify(TypeEntity.Bordereau, mouvementNo, TypeAction.Modification, Societe.No);
			break;
		case TypeMouvementBancaire.Retrait:
			_mvtBancaireRepository.PointerMouvement(mouvementNo, isPointe: false, DateTime.Now, "");
			_notifyService.Notify(TypeEntity.Alimentation, mouvementNo, TypeAction.Modification, Societe.No);
			break;
		case TypeMouvementBancaire.Interne:
			_mvtBancaireRepository.PointerVirementInterne(mouvementNo, isPointe: false, DateTime.Now, "");
			_notifyService.Notify(TypeEntity.VirementInterne, mouvementNo, TypeAction.Modification, Societe.No);
			break;
		case TypeMouvementBancaire.RemboursementClient:
			_mvtBancaireRepository.PointerRemboursement(mouvementNo, isPointe: false, DateTime.Now, "");
			_notifyService.Notify(TypeEntity.RemboursementClient, mouvementNo, TypeAction.Modification, Societe.No);
			break;
		case TypeMouvementBancaire.VirementTiers:
			_mvtBancaireRepository.PointerVirementInterne(mouvementNo, isPointe: false, DateTime.Now, "");
			_notifyService.Notify(TypeEntity.VirementTiers, mouvementNo, TypeAction.Modification, Societe.No);
			break;
		case TypeMouvementBancaire.Depense:
			_mvtBancaireRepository.PointerMouvement(mouvementNo, isPointe: false, DateTime.Now, "");
			_notifyService.Notify(TypeEntity.Depense, mouvementNo, TypeAction.Modification, Societe.No);
			break;
		default:
			throw new NotImplementedException(TresorerieCoreMessages.ErrorMvtNonGenere);
		}
		transactionScope.Complete();
	}

	public List<RecapTiersDetailAffectation> RecapTiersFactureGetAllAffectationByDateDocument(ErpDomaine domaine, DateTime dateMin, DateTime dateMax, Societe societe = null)
	{
		societe = societe ?? Societe;
		return _recapTiersRepository.GetAllRecapAffectationByDateDocument(societe.No, domaine, dateMin, dateMax).ToList();
	}

	public List<RecapTiersDetailAffectation> RecapTiersFactureGetAllAffectationByDateReglement(ErpDomaine domaine, DateTime dateMin, DateTime dateMax, Societe societe = null)
	{
		societe = societe ?? Societe;
		return _recapTiersRepository.GetAllRecapAffectationByDateReglement(societe.No, domaine, dateMin, dateMax).ToList();
	}

	public List<RecapTiersDocument> RecapTiersFactureGetAll(ErpDomaine domaine, DateTime dateMin, DateTime dateMax, Societe societe = null)
	{
		societe = societe ?? Societe;
		return _recapTiersRepository.GetAllRecapFacture(societe.No, domaine, dateMin, dateMax).ToList();
	}

	public List<RecapTiersReglement> RecapTiersReglementGetAll(MouvementDomaine domaine, DateTime dateMin, DateTime dateMax, Societe societe = null)
	{
		societe = societe ?? Societe;
		return _recapTiersRepository.GetAllRecapReglement(societe.No, domaine, dateMin, dateMax).ToList();
	}

	public List<RecapTiersDocument> RecapTiersImpayeGetAll(ErpDomaine domaine, DateTime dateMin, DateTime dateMax, Societe societe = null)
	{
		societe = societe ?? Societe;
		return _recapTiersRepository.GetAllRecapImpaye(societe.No, domaine, dateMin, dateMax).ToList();
	}

	public int ReglementFournisseurCoffreCreate(string numero, DateTime date, decimal montant, int deviseNo, int fournisseurNo, string fournisseurCode, string fournisseurIntitule, int modeNo, int caisseNo, string pieceNumero, string libelle, string tire, decimal cours, DateTime echeancePiece, int? banqueNo, string banqueFournisseur, TiersType typeTiers, decimal? montantPlafond, bool isCertifie, DateTime? dateValidite, bool isComptabilite = true, bool isImporterFromErp = false, bool isImporterComptabiliser = false, int? chequeNo = null)
	{
		if (montant <= 0m)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorMontantInavlide, "montant");
		}
		int num = CaisseManager.ReglementFournisseurCreate(numero, date, montant, deviseNo, fournisseurNo, fournisseurCode, fournisseurIntitule, typeTiers, modeNo, caisseNo, pieceNumero, libelle, echeancePiece, banqueNo, banqueFournisseur, isBarre: false, tire, chequeNo, cours, montant * cours, string.Empty, null, isCertifie, dateValidite, montantPlafond, string.Empty, string.Empty, string.Empty, string.Empty, ReglementNature.Reglement, isImporterFromErp, isImporterComptabiliser);
		ReglementFournisseur reglementFournisseur = CaisseManager.ReglementFournisseurGet(num);
		if (reglementFournisseur == null)
		{
			throw new ApplicationException($"Impossible de charger le règlement [{num}].");
		}
		if (isComptabilite)
		{
			reglementFournisseur.ChangeEtatComptabilise(EtatComptabilite.Comptabilise);
			_reglementFournisseurRepository.Update(reglementFournisseur);
		}
		return num;
	}

	public int ReglementClientCoffreCreate(string numero, DateTime date, decimal montant, int deviseNo, int clientNo, string clientCode, string clientIntitule, int modeNo, int caisseNo, string pieceNumero, string libelle, string tire, DateTime echeancePiece, int? banqueNo, string banqueClient, int deviseSocieteNo, decimal? montantPlafond, bool isCertifie, DateTime? dateValidite, string reference, decimal cours, bool isComptabilise = true, bool isImporterFromErp = false, bool isImporterComptabiliser = false, bool useNotification = true)
	{
		if (montant <= 0m)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorMontantInavlide, "montant");
		}
		int num = CaisseManager.ReglementCreate(numero, date, montant, deviseNo, clientNo, clientCode, clientIntitule, modeNo, caisseNo, pieceNumero, libelle, tire, echeancePiece, banqueNo, banqueClient, (cours == 0m) ? 1m : cours, deviseSocieteNo, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, reference, 0, isCertifie, dateValidite, montantPlafond, 0m, 0m, ReglementNature.Reglement, soumisDroitTimbre: false, 0m, isImporterFromErp, isImporterComptabiliser, useNotification);
		ReglementClient reglementClient = CaisseManager.ReglementGet(num);
		if (reglementClient == null)
		{
			throw new ApplicationException($"Impossible de charger le règlement [{num}].");
		}
		if (isComptabilise)
		{
			reglementClient.ChangeEtatComptabilise(EtatComptabilite.Comptabilise);
			_reglementClientRepository.Update(reglementClient);
		}
		return num;
	}

	public void ReglementEscompteDeRegler(int mouvementNo)
	{
		if (mouvementNo <= 0)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorMvtNo);
		}
		ReglementClient reglementClient = CaisseManager.ReglementGet(mouvementNo);
		if (reglementClient == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglementInvalide);
		}
		if (!reglementClient.IsEscompteRegle)
		{
			throw new InvalidOperationException("Le règlement " + reglementClient.Numero + " est déjà non réglé!");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_mvtBancaireRepository.EscompteRegler(mouvementNo, isPointe: false, DateTime.Now);
		_notifyService.Notify(TypeEntity.Reglement, mouvementNo, TypeAction.Modification, Societe.No);
		transactionScope.Complete();
	}

	public void ReglementEscompteRegler(int mouvementNo, DateTime dateRegler)
	{
		if (mouvementNo <= 0)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorMvtNo);
		}
		ReglementClient reglementClient = CaisseManager.ReglementGet(mouvementNo);
		if (reglementClient == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglementInvalide);
		}
		if (reglementClient.IsRemis != Remis.RemisBanque)
		{
			throw new InvalidOperationException($"Le règlement {reglementClient.Numero} est déjà non remis à la banque!");
		}
		if (reglementClient.BordereauNature != 0 && reglementClient.BordereauNature != 2)
		{
			throw new InvalidOperationException($"Le règlement {reglementClient.Numero} n'est pas de type escompte!");
		}
		if (reglementClient.IsImpaye == ImpayeEtat.Impaye)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementImpaye);
		}
		if (reglementClient.Preavis != EtatPreavis.NonPreavis && reglementClient.Preavis != EtatPreavis.RegularisePaye)
		{
			throw new ApplicationException("Le règlement " + reglementClient.Numero + " est préavisé.");
		}
		if (reglementClient.IsEscompteRegle)
		{
			throw new ApplicationException($"Le règlement [{reglementClient.Numero}] est déjà règlé!");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_mvtBancaireRepository.EscompteRegler(mouvementNo, isPointe: true, dateRegler);
		_notifyService.Notify(TypeEntity.Reglement, mouvementNo, TypeAction.Modification, Societe.No);
		transactionScope.Complete();
	}

	public void SetCurrent(Societe societe, Utilisateur utilisateur)
	{
		if (societe == null)
		{
			throw new ArgumentNullException("societe");
		}
		if (utilisateur == null)
		{
			throw new ArgumentNullException("utilisateur");
		}
		if (!societe.HasUtilisateur(utilisateur.Login))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorSocieteUtilisateur, societe.RaisonSociale));
		}
		Societe = societe;
		Utilisateur = utilisateur;
	}

	public void SetCurrentExercice(IErpExercice exercice)
	{
		Exercice = exercice;
	}

	public void SocieteUtilisateurCreate(int utilisateurNo, Societe societe = null)
	{
		societe = societe ?? Societe;
		Utilisateur byNo = Groupe.UtilisateurManager.GetByNo(utilisateurNo);
		if (byNo == null)
		{
			throw new ArgumentNullException("utilisateurNo");
		}
		if (!Utilisateur.IsAdmin)
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAddUserSociete, byNo.Login, societe.RaisonSociale));
		}
		_societeUtilisateurRepository.Create(societe.No, utilisateurNo);
	}

	public void SocieteUtilisateurDelete(int utilisateurNo, ProfilType type, Societe societe = null)
	{
		societe = societe ?? Societe;
		Utilisateur utilisateur = societe.GetUtilisateurs().SingleOrDefault((Utilisateur x) => x.No == utilisateurNo);
		if (utilisateur == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorSocUtilisateurInvalide);
		}
		if (!Utilisateur.IsAdmin)
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorDeleteUserSociete, societe.RaisonSociale, utilisateur.Login));
		}
		if (_autorisationCaisseRepository.GetAll(utilisateur.No, societe.No, type).Any((AutorisationCaisse a) => societe.GetCaisses().Any((Caisse c) => c.No == a.CaisseNo)))
		{
			throw new ApplicationException($"L'utilisateur [{utilisateur.Login}] est autorisé à une ou plusieur caisses!");
		}
		_societeUtilisateurRepository.Delete(societe.No, utilisateurNo);
	}

	public void SocieteVerifEtat(Dictionary<int, string> etats, Societe societe = null)
	{
		societe = societe ?? Societe;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		foreach (KeyValuePair<int, string> etat in etats)
		{
			SocieteEtat societeEtat = EtatGetStandard(etat.Key, etat.Value);
			if (societeEtat == null)
			{
				societeEtat = new SocieteEtat(societe.No, etat.Key, 0, null, TypeRapport.Standard)
				{
					Intitule = etat.Value,
					IsVisible = true
				};
				_societeImpressionRepository.Create(societeEtat);
			}
		}
		transactionScope.Complete();
	}

	public void SocieteVerifierEtat(int source, string name, Societe societe = null)
	{
		societe = societe ?? Societe;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		SocieteEtat societeEtat = EtatGetStandard(source, name);
		if (societeEtat == null)
		{
			societeEtat = new SocieteEtat(societe.No, source, 0, null, TypeRapport.Standard)
			{
				Intitule = name,
				IsVisible = true
			};
			_societeImpressionRepository.Create(societeEtat);
			transactionScope.Complete();
		}
	}

	public int SoldeTiersCreate(string piece, string reference, int tiersNo, string tiersCode, string tiersIntitule, decimal montant, int deviseNo, decimal deviseCours, int modeNo, int soucheNo, int collaborateurNo, string libelle, DateTime documentDate, DateTime echeanceDate, int deviseSocieteNo, ErpDomaine domaine, Societe societe = null, int isSpe = 0, int? designationDocumentNo = null)
	{
		societe = societe ?? Societe;
		return EcheanceCreate(0, piece, domaine, ErpDocumentType.None, documentDate, tiersNo, tiersCode, tiersIntitule, tiersNo, tiersCode, tiersIntitule, deviseNo, deviseCours, montant, echeanceDate, modeNo, soucheNo, collaborateurNo, EcheanceType.Solde, libelle, deviseSocieteNo, string.Empty, reference, string.Empty, string.Empty, string.Empty, string.Empty, null, societe, isSpe, isTimbre: false, 0m, 0m, designationDocumentNo);
	}

	public int SoldeTiersCreate(string piece, string reference, int tiersNo, string tiersCode, string tiersIntitule, decimal montant, int deviseNo, decimal deviseCours, int modeNo, int soucheNo, string libelle, DateTime documentDate, DateTime echeanceDate, int deviseSocieteNo, ErpDomaine domaine, Societe societe = null, int isSpe = 0, int? designationDocumentNo = null)
	{
		if (domaine == ErpDomaine.Vente)
		{
			_licenceApplicationVersion.ThrowIfGratuit();
		}
		int collaborateurNo = 0;
		return SoldeTiersCreate(piece, reference, tiersNo, tiersCode, tiersIntitule, montant, deviseNo, deviseCours, modeNo, soucheNo, collaborateurNo, libelle, documentDate, echeanceDate, deviseSocieteNo, domaine, societe, isSpe, designationDocumentNo);
	}

	public void SoucheCreateVente(IErpSoucheVente souche, Societe societe = null)
	{
		if (souche == null)
		{
			throw new ArgumentNullException("souche");
		}
		societe = societe ?? Societe;
		ErpDomaine domaine = ErpDomaine.Vente;
		if (_societeSoucheRepository.Get(societe.No, souche.Id, domaine) != null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorSouche);
		}
		SocieteSouche societeSouche = new SocieteSouche
		{
			SoucheNo = souche.Id,
			SocieteNo = societe.No,
			Domaine = domaine
		};
		_societeSoucheRepository.Create(societeSouche);
		societe.Refresh();
		_notifyService.Notify(TypeEntity.Parametrage, 0, TypeAction.Modification, societe.No);
	}

	public void SoucheCreateAchat(IErpSoucheAchat souche, Societe societe = null)
	{
		if (souche == null)
		{
			throw new ArgumentNullException("souche");
		}
		societe = societe ?? Societe;
		ErpDomaine domaine = ErpDomaine.Achat;
		if (_societeSoucheRepository.Get(societe.No, souche.Id, domaine) != null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorSouche);
		}
		SocieteSouche societeSouche = new SocieteSouche
		{
			SoucheNo = souche.Id,
			SocieteNo = societe.No,
			Domaine = domaine
		};
		_societeSoucheRepository.Create(societeSouche);
		societe.Refresh();
		_notifyService.Notify(TypeEntity.Parametrage, 0, TypeAction.Modification, societe.No);
	}

	public void SoucheDelete(SocieteSouche societeSouche)
	{
		if (societeSouche == null)
		{
			throw new ArgumentNullException("societeSouche");
		}
		_societeSoucheRepository.Delete(societeSouche);
		Societe.Refresh();
		_notifyService.Notify(TypeEntity.Parametrage, 0, TypeAction.Modification, Societe.No);
	}

	public override string ToString()
	{
		return string.Format(TresorerieCoreMessages.ErrorSocieteNo);
	}

	public void Update(Societe societe)
	{
		if (societe == null)
		{
			throw new ArgumentNullException("societe");
		}
		if (!Utilisateur.IsAdmin)
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorUpdateSociete, societe.RaisonSociale));
		}
		_societeRepository.Update(societe);
		_notifyService.Notify(TypeEntity.Parametrage, 0, TypeAction.Modification, societe.No);
	}

	public void UpdateCaisseDefault(int utilisateurNo, ProfilType type, int? caisseNo = null)
	{
		foreach (Caisse caiss in Societe.GetCaisses())
		{
			_autorisationCaisseRepository.Update(utilisateurNo, caiss.No, isDefault: false, type);
		}
		if (caisseNo.HasValue)
		{
			_autorisationCaisseRepository.Update(utilisateurNo, caisseNo.Value, isDefault: true, type);
		}
	}

	public void UpdateCaution(Caution caution)
	{
		if (caution == null)
		{
			throw new ArgumentNullException("caution");
		}
		if (caution.No == 0)
		{
			throw new ApplicationException("Numéro caution est invalide!");
		}
		if (string.IsNullOrEmpty(caution.Commentaire))
		{
			throw new ApplicationException("Commentaire obligatoire!");
		}
		Caution caution2 = _cautionRepository.Get(caution.No);
		if (caution2 == null)
		{
			throw new ApplicationException($"Impossible de charger le numéro de la caution n°[{caution.No}]");
		}
		if (caution2.ReglementNo > 0 && caution.Statut == StatutCaution.Annule)
		{
			throw new ApplicationException("Impossible d'annuler une caution transformée en règlement!");
		}
		if (caution2.Statut == StatutCaution.Recupere && caution.Statut == StatutCaution.Annule)
		{
			throw new ApplicationException("Impossible d'annuler une caution récupérée!");
		}
		if (caution2.ReglementNo > 0 && caution.Statut == StatutCaution.Recupere)
		{
			throw new ApplicationException("Impossible de récupérer une caution transformée en règlement!");
		}
		if (caution2.Statut == StatutCaution.Annule && caution.Statut == StatutCaution.Recupere)
		{
			throw new ApplicationException("Impossible de récupérer une caution annulée");
		}
		if (caution.Statut == StatutCaution.Reglement)
		{
			int valueOrDefault = caution.ReglementNo.GetValueOrDefault();
			if (valueOrDefault == 0)
			{
				throw new ApplicationException("Numéro règlement est invalide!");
			}
			switch (caution2.Domaine)
			{
			case DomaineCaution.Client:
				if (_caisseManager.ReglementGet(valueOrDefault) == null)
				{
					throw new ApplicationException($"Le règlement n°[{valueOrDefault}] est introuvable!");
				}
				break;
			case DomaineCaution.Fournisseur:
				if (_caisseManager.ReglementFournisseurGet(valueOrDefault) == null)
				{
					throw new ApplicationException($"Le règlement n°[{valueOrDefault}] est introuvable!");
				}
				break;
			default:
				throw new ApplicationException("Le domaine [" + caution2.Domaine.GetDisplayDescription() + "] n'est pas configuré.");
			}
			caution2.ReglementNo = caution.ReglementNo;
		}
		caution2.Commentaire = caution.Commentaire;
		caution2.Statut = caution.Statut;
		caution2.DateStatut = DateTime.Now;
		_cautionRepository.Update(caution2);
		_notifyService.Notify(TypeEntity.Caution, caution2.No, TypeAction.Modification, Societe.No);
	}

	public void UpdateDossierReglement(int dossierNo, string beneficiaire, string identifiant, NatureFournisseur natureBeneficiaire, string libelle, decimal montant, decimal cours, int deviseSocieteNo, int? attestationRetenueNo = null)
	{
		DossierReglement dossier = _dossierReglementRepository.GetDossier(dossierNo);
		if (dossier == null)
		{
			throw new ArgumentNullException("dossier");
		}
		if (string.IsNullOrEmpty(beneficiaire))
		{
			throw new InvalidOperationException("Bénéficiaire invalide!");
		}
		if (string.IsNullOrEmpty(libelle))
		{
			throw new InvalidOperationException("Libellé invalide!");
		}
		if (montant < 0m)
		{
			throw new ArgumentException("Le montant est invalide!");
		}
		bool flag = dossier.Cours != cours;
		if (dossier.IsComptabilise && flag)
		{
			throw new InvalidOperationException("Le dossier est comptabilisé!");
		}
		if (Societe.LegislationType != Legislation.Maroc && attestationRetenueNo.HasValue)
		{
			throw new ApplicationException($"Législation {Societe.LegislationType}, impossible d'affecté une attestation.");
		}
		if (attestationRetenueNo.HasValue && AttestationRetenueTiersGetAsync(attestationRetenueNo.Value).ConfigureAwait(continueOnCapturedContext: false).GetAwaiter().GetResult() == null)
		{
			throw new ApplicationException("Impossible de charger l'attestation.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_dossierReglementRepository.Update(dossierNo, beneficiaire, identifiant, natureBeneficiaire, libelle, montant, cours, attestationRetenueNo);
		if (flag)
		{
			List<LigneDossierReglement> list = _ligneDossierReglementRepository.GetAll(dossierNo).ToList();
			List<LigneDossierReglement> source = list.Where((LigneDossierReglement x) => x.Type == TypeLigneDossier.Reglement).ToList();
			List<LigneDossierReglement> source2 = list.Where((LigneDossierReglement x) => x.Type == TypeLigneDossier.Retenue).ToList();
			if (source.Where(delegate(LigneDossierReglement x)
			{
				ReglementFournisseur reglementFournisseur = CaisseManager.ReglementFournisseurGet(x.No);
				if (reglementFournisseur == null)
				{
					throw new ApplicationException("Impossible de charger le règlement.");
				}
				return reglementFournisseur.Type == ReglementType.Cheque || reglementFournisseur.Type == ReglementType.Traite;
			}).Any((LigneDossierReglement x) => x.IsRecuperer))
			{
				throw new ApplicationException("Un ou plusieurs règlements sont récupérés.");
			}
			if (source2.Any((LigneDossierReglement x) => x.IsRecuperer))
			{
				throw new ApplicationException("Une ou plusieurs retenues sont récupérées.");
			}
			foreach (LigneDossierReglement item in list)
			{
				if (item.Type == TypeLigneDossier.Reglement)
				{
					CaisseManager.ReglementFournisseurUpdateCours(item.No, deviseSocieteNo, cours, dossier.IsDossierCommercial);
					_notifyService.Notify(TypeEntity.ReglementFournisseur, item.No, TypeAction.Modification, dossier.SocieteNo);
				}
			}
		}
		_notifyService.Notify(TypeEntity.Dossier, dossierNo, TypeAction.Modification, dossier.SocieteNo);
		transactionScope.Complete();
	}

	public Task<IEnumerable<ReleveClient>> GetReleveClientAsync(string clientCode, Societe societe = null)
	{
		societe = societe ?? Societe;
		DateTime dateLimitBL = (societe.ReleveClientDtLimiteBonLiv ? DateHelper.GetMinSqlDateTime() : societe.DateDebutMajClt);
		return _releveClientRepository.GetAllAsync(societe.No, clientCode, societe.ReleveClientBonLiv, dateLimitBL);
	}

	public void UpdateGridLayout(int layoutNo, string name, Guid guid, byte[] layout, Utilisateur utilisateur = null)
	{
		utilisateur = utilisateur ?? Utilisateur;
		if (string.IsNullOrEmpty(name))
		{
			throw new ArgumentNullException("name");
		}
		if (guid == Guid.Empty)
		{
			throw new ArgumentNullException("guid");
		}
		if (layout == null)
		{
			throw new ArgumentNullException("layout");
		}
		GridLayout gridLayout = new GridLayout
		{
			Guid = guid,
			UtilisateurNo = utilisateur.No,
			Name = name,
			Layout = layout,
			No = layoutNo
		};
		_gridLayoutRepository.Update(gridLayout);
	}

	public void UpdateMontantFactureFournisseur(int factureNo, decimal montantDevise, int deviseSocieteNo, Etat etat)
	{
		if (factureNo <= 0)
		{
			throw new ArgumentNullException("factureNo");
		}
		Echeance echeance = EcheanceGet(factureNo);
		if (echeance == null)
		{
			throw new InvalidOperationException("Impossible de charger la facture.");
		}
		if (echeance.Type != EcheanceType.FactureFrsTresorerie)
		{
			throw new InvalidOperationException("Facture fournisseur invalide");
		}
		if (echeance.Montant == montantDevise)
		{
			return;
		}
		if (echeance.IsComptabilise)
		{
			throw new InvalidOperationException("La facture [" + echeance.DocumentNumero + "] est comptabilisée.");
		}
		if (!UserHasAutorisationSouche(echeance.Domaine, echeance.SoucheNo))
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorAutorisationSouche);
		}
		if (echeance.GetAffectations().Any())
		{
			throw new InvalidOperationException("La facture [" + echeance.DocumentNumero + "] est partiellement payée.");
		}
		SocieteDevise devise = Societe.GetDevise(deviseSocieteNo);
		if (devise == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorDeviseNo);
		}
		decimal montantDeviseSociete = Math.Round(montantDevise * echeance.CoursDevise, devise.NombreDecimales, MidpointRounding.AwayFromZero);
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_echeanceRepository.UpdateMontantFactureFournisseur(factureNo, montantDevise, montantDeviseSociete, etat);
		_notifyService.Notify(TypeEntity.Echeance, echeance.No, TypeAction.Modification, echeance.SocieteNo);
		transactionScope.Complete();
	}

	public void UpdateRappel(int rappelNo, DateTime date, string note, bool isCloture, DateTime dateCloture, bool isRelance = false, int? parentNo = null)
	{
		if (rappelNo <= 0)
		{
			throw new ArgumentException("rappelNo");
		}
		Rappel rappel = _rappelRepository.Get(rappelNo);
		if (rappel == null)
		{
			throw new ApplicationException("Impossible de charger le rappel.");
		}
		if (date.Date <= DateTime.Now.Date)
		{
			throw new ApplicationException("Date du rappel est invalide.");
		}
		if (string.IsNullOrEmpty(note))
		{
			throw new ApplicationException("La note du rappel est invalide.");
		}
		string parentNumero = string.Empty;
		if (isRelance)
		{
			if (!parentNo.HasValue)
			{
				throw new ApplicationException("Le rappel parent est invalide.");
			}
			Rappel rappel2 = _rappelRepository.Get(parentNo.Value);
			if (rappel2 == null)
			{
				throw new ApplicationException("Impossible de charger le rappel parent.");
			}
			if (!rappel2.IsCloture)
			{
				throw new ApplicationException("Le rappel [" + rappel2.Numero + "] n'est pas clôturé.");
			}
			if (rappel2.IsRelance)
			{
				throw new ApplicationException("Le rappel [" + rappel2.Numero + "] déjà de type relance.");
			}
			parentNumero = rappel2.Numero;
		}
		rappel.Date = date;
		rappel.Note = note;
		rappel.IsCloture = isCloture;
		rappel.DateCloture = dateCloture;
		rappel.IsRelance = isRelance;
		rappel.ParentNo = parentNo;
		rappel.ParentNumero = parentNumero;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_rappelRepository.Update(rappel);
		_notifyService.Notify(TypeEntity.Rappel, rappel.No, TypeAction.Modification, rappel.SocieteNo);
		transactionScope.Complete();
	}

	public void UpdateUtilisateurGrid(int layoutNo, int utilisateurGridNo)
	{
		if (layoutNo <= 0)
		{
			throw new ArgumentNullException("layoutNo");
		}
		GridLayout gridLayout = _gridLayoutRepository.Get(layoutNo);
		if (gridLayout == null)
		{
			throw new ArgumentNullException("layout");
		}
		UtilisateurGrid utilisateurGrid = _utilisateurGridRepository.Get(utilisateurGridNo);
		if (utilisateurGrid == null)
		{
			throw new ApplicationException("Utilisateur n'a pas de shéma par defaut!");
		}
		utilisateurGrid.GridLayoutNo = gridLayout.No;
		_utilisateurGridRepository.Update(utilisateurGrid);
	}

	public bool UserHasAutorisationCaisse(int caisseNo, ProfilType type, Utilisateur utilisateur = null, Societe societe = null)
	{
		societe = societe ?? Societe;
		utilisateur = utilisateur ?? Utilisateur;
		if (!utilisateur.IsAdmin)
		{
			return _autorisationCaisseRepository.GetAll(utilisateur.No, societe.No, type).Any((AutorisationCaisse a) => a.CaisseNo == caisseNo);
		}
		return true;
	}

	public bool UserHasAutorisationCaisse(int caisseNo, Utilisateur utilisateur = null, Societe societe = null)
	{
		societe = societe ?? Societe;
		utilisateur = utilisateur ?? Utilisateur;
		if (!utilisateur.IsAdmin)
		{
			return _autorisationCaisseRepository.GetAll(utilisateur.No, societe.No, Groupe.ProfilType).Any((AutorisationCaisse a) => a.CaisseNo == caisseNo);
		}
		return true;
	}

	public bool UserHasAutorisationSouche(ErpDomaine domaine, int soucheNo)
	{
		if (!Utilisateur.IsAdmin)
		{
			return GetAllAutorizedSouches(domaine).Any((AutorisationSouche a) => a.SoucheNo == soucheNo);
		}
		return true;
	}

	public bool UserHasMouvement(int utilisateurNo, Societe societe = null)
	{
		societe = societe ?? Societe;
		return _societeUtilisateurRepository.HasMouvement(utilisateurNo, societe.No);
	}

	private IEnumerable<AutorisationSouche> GetAutorisationSouche(ErpDomaine domaine)
	{
		return domaine switch
		{
			ErpDomaine.Vente => GetAutorisationSoucheVente(), 
			ErpDomaine.Achat => GetAutorisationSoucheAchat(), 
			_ => throw new InvalidOperationException("Domaine invalide!"), 
		};
	}

	private IEnumerable<AutorisationSouche> GetAutorisationSoucheAchat()
	{
		ErpDomaine domaine = ErpDomaine.Achat;
		if (Utilisateur.IsAdmin)
		{
			return GetAllAutorizedSouches(domaine);
		}
		if (!Societe.HasSoucheRestrictionFrs)
		{
			return GetAllAutorizedSouches(domaine);
		}
		IEnumerable<SocieteSouche> societeSouche = Societe.GetSouches(domaine);
		return from a in GetAllAutorizedSouches(domaine)
			where societeSouche.Any((SocieteSouche s) => s.SoucheNo == a.SoucheNo)
			select a;
	}

	private IEnumerable<AutorisationSouche> GetAutorisationSoucheVente()
	{
		ErpDomaine domaine = ErpDomaine.Vente;
		if (Utilisateur.IsAdmin)
		{
			return GetAllAutorizedSouches(domaine);
		}
		if (!Societe.HasSoucheRestrictionClt)
		{
			return GetAllAutorizedSouches(domaine);
		}
		IEnumerable<SocieteSouche> societeSouche = Societe.GetSouches(domaine);
		return from a in GetAllAutorizedSouches(domaine)
			where societeSouche.Any((SocieteSouche s) => s.SoucheNo == a.SoucheNo)
			select a;
	}

	public void LigneDossierReglementCommUpdateOrdre(int ligneNo, int ordre)
	{
		if (ligneNo <= 0)
		{
			throw new ArgumentNullException("ligneNo");
		}
		LigneDossierReglementComm ligneDossierReglementComm = _ligneDossierReglementCommRepository.Get(ligneNo);
		if (ligneDossierReglementComm == null)
		{
			throw new ApplicationException("Impossible de charger la ligne.");
		}
		if (ligneDossierReglementComm.EntitySolde < 0m)
		{
			throw new ApplicationException("Impossible de changer l'ordre d'un document d'avoir.");
		}
		if (ligneDossierReglementComm.IsRetenue)
		{
			throw new ApplicationException("Impossible de changer l'ordre d'un retenue.");
		}
		if ((_dossierReglementRepository.GetDossier(ligneDossierReglementComm.DossierNo) ?? throw new ApplicationException("Impossible de charger le dossier de règlement.")).StatutDossier != StatutDossierReglement.Encours)
		{
			throw new ApplicationException("Le statut du dossier est invalide.");
		}
		_ligneDossierReglementCommRepository.UpdateOrdre(ligneNo, ordre);
		if (_licenceApplicationVersion.Application == ApplicationRunning.TresoClient)
		{
			_notifyService.Notify(TypeEntity.DossierClient, ligneDossierReglementComm.DossierNo, TypeAction.Modification, Societe.No);
		}
	}

	public LigneDossierReglementComm LigneDossierReglementCommGet(int ligneNo)
	{
		return _ligneDossierReglementCommRepository.Get(ligneNo);
	}

	public LigneDossierReglementComm LigneDossierReglementCommGet(TypeLigneDossier type, int dossierNo, int entityNo)
	{
		return _ligneDossierReglementCommRepository.Get(type, dossierNo, entityNo);
	}

	public IEnumerable<LigneDossierReglementComm> LigneDossierReglementCommGetAll(int dossierNo)
	{
		return _ligneDossierReglementCommRepository.GetAll(dossierNo);
	}

	public IEnumerable<LigneDossierReglementComm> LigneDossierReglementCommGetAll(Societe societe = null)
	{
		societe = societe ?? Societe;
		return _ligneDossierReglementCommRepository.GetAllLigneDossiers(societe.No);
	}

	public IEnumerable<LigneDossierReglementComm> LigneDossierReglementCommGetLigneReglement(int dossierNo)
	{
		return from x in _ligneDossierReglementCommRepository.GetLigneReglement(dossierNo)
			orderby x.Ordre
			select x;
	}

	public IEnumerable<LigneDossierReglementComm> LigneDossierReglementCommGetLigneEcheance(int dossierNo)
	{
		return from x in _ligneDossierReglementCommRepository.GetLigneEcheance(dossierNo)
			orderby x.Ordre
			select x;
	}

	public void LigneDossierReglementCommCreate(int dossierNo, int entityNo, TypeLigneDossier type, decimal montantAPaye, bool isRetenue, bool isEcartChange = false)
	{
		if (dossierNo <= 0)
		{
			throw new ArgumentNullException("dossierNo");
		}
		if (entityNo <= 0)
		{
			throw new ArgumentNullException("entityNo");
		}
		if (type != TypeLigneDossier.Reglement && type != TypeLigneDossier.Echeance)
		{
			throw new ArgumentNullException("type");
		}
		if (isEcartChange && montantAPaye == 0m)
		{
			throw new ArgumentNullException("montantAPaye");
		}
		if (isEcartChange && type != TypeLigneDossier.Echeance)
		{
			throw new ArgumentNullException("isEcartChange");
		}
		if (isRetenue && type != TypeLigneDossier.Reglement)
		{
			throw new ApplicationException("Type invalide.");
		}
		DossierReglement dossier = _dossierReglementRepository.GetDossier(dossierNo);
		if (dossier == null)
		{
			throw new ApplicationException("Impossible de charger le dossier de règlement.");
		}
		if (dossier.StatutDossier != StatutDossierReglement.Encours)
		{
			throw new ApplicationException("Le dossier de règlement n'est pas encours de saisie.");
		}
		decimal num = default(decimal);
		decimal montantFacturationMois = default(decimal);
		switch (type)
		{
		case TypeLigneDossier.Reglement:
			if (!isRetenue)
			{
				ReglementFournisseur obj = CaisseManager.ReglementFournisseurGet(entityNo) ?? throw new ApplicationException("Impossible de charger le règlement.");
				if (obj.IsReserveDossierFrs)
				{
					throw new ApplicationException("Le règlement est réservé.");
				}
				if (obj.Solde == 0m)
				{
					throw new ApplicationException("Le règlement est déjà soldé.");
				}
				if (obj.CaisseNo != dossier.CaisseNo)
				{
					throw new ApplicationException("Le règlement et le dossier de règlement doivent partagé la même caisse.");
				}
				if (obj.DeviseNo != dossier.DeviseNo)
				{
					throw new ApplicationException("Le règlement et le dossier de règlement doivent partager la même devise.");
				}
				if (Societe.UseOnlyOneReglementInDossier && _ligneDossierReglementCommRepository.GetLigneReglement(dossierNo).Any((LigneDossierReglementComm x) => !x.IsRetenue))
				{
					throw new ApplicationException("Opération invalide! Un seul règlement est autorisé par dossier.");
				}
				num = obj.Solde;
			}
			else
			{
				RetenuALaSource obj2 = CaisseManager.RetenueGet(entityNo) ?? throw new ApplicationException("Impossible de charger la retenue.");
				if (obj2.IsReserveDossierFrs)
				{
					throw new ApplicationException("La retenue est réservé.");
				}
				if (obj2.CaisseNo != dossier.CaisseNo)
				{
					throw new ApplicationException("La retenue et le dossier de règlement doivent partagé la même caisse.");
				}
				if (obj2.DeviseNo != dossier.DeviseNo)
				{
					throw new ApplicationException("La retenue et le dossier de règlement doivent partager la même devise.");
				}
				num = obj2.Montant;
			}
			break;
		case TypeLigneDossier.Echeance:
		{
			Echeance echeance = _echeanceRepository.Get(entityNo);
			if (echeance == null)
			{
				throw new ApplicationException("Impossible de charger l'échéance.");
			}
			if (echeance.Domaine != ErpDomaine.Achat && _licenceApplicationVersion.Application != ApplicationRunning.TresoClient)
			{
				throw new ApplicationException("L'échéance n'est pas de type Achat.");
			}
			if (echeance.Domaine != ErpDomaine.Vente && _licenceApplicationVersion.Application == ApplicationRunning.TresoClient)
			{
				throw new ApplicationException("L'échéance n'est pas de type Vente.");
			}
			if (echeance.IsReserveDossierFrs)
			{
				throw new ApplicationException("L'échéance est réservée dans un autre dossier de règlement");
			}
			if (echeance.Etat != Etat.NonPaye && echeance.Type != EcheanceType.GainEchange && echeance.Type != EcheanceType.PerteEchange)
			{
				throw new ApplicationException("L'échéance est totalement payée.");
			}
			if (echeance.DeviseNo != dossier.DeviseNo)
			{
				throw new ApplicationException("L'échéance et le dossier de règlement doivent partager la même devise.");
			}
			if (_licenceApplicationVersion.Application != ApplicationRunning.TresoClient && Societe.UseWorkFlowValidationEcheanceFournisseur && echeance.WorkflowStatut != EcheanceWorkflowValidationStatut.Valide)
			{
				throw new ApplicationException("L'échéance doit être validée avant de pouvoir être incluse dans un bon à payer fournisseur.");
			}
			num = echeance.Solde;
			isRetenue = false;
			DateTime dateDu = new DateTime(echeance.DocumentDate.Year, echeance.DocumentDate.Month, 1);
			DateTime dateAu = echeance.DocumentDate.Date.AddDays(1.0).AddSeconds(-1.0);
			int designationDocumentNo = (echeance.DesignationDocumentNo.HasValue ? echeance.DesignationDocumentNo.Value : 0);
			montantFacturationMois = _echeanceRepository.GetAllByTiers(echeance.Domaine, echeance.SocieteNo, echeance.ClientNo, dateDu, dateAu, designationDocumentNo).ToList().Sum((Echeance x) => x.MontantDeviseSociete);
			break;
		}
		}
		if (_ligneDossierReglementCommRepository.Get(type, dossierNo, entityNo) != null)
		{
			throw new ApplicationException("Ligne existe déjà.");
		}
		if (!isEcartChange && montantAPaye > num)
		{
			throw new ApplicationException("Le montant à imputer est invalide.");
		}
		int ordre = 0;
		if (!isRetenue && num > 0m)
		{
			ordre = _ligneDossierReglementCommRepository.GetLastOrdre(dossierNo, type) + 1;
		}
		LigneDossierReglementComm ligneDossierReglementComm = new LigneDossierReglementComm
		{
			DossierNo = dossierNo,
			DateCreation = DateTime.Now.Date,
			EntityNo = entityNo,
			EntitySolde = num,
			MontantAPaye = montantAPaye,
			TypeLigneDossier = type,
			IsRetenue = isRetenue,
			SocieteNo = Societe.No,
			CreateurNo = Utilisateur.No,
			Ordre = ordre,
			MontantFacturationMois = montantFacturationMois
		};
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_ligneDossierReglementCommRepository.Create(ligneDossierReglementComm);
		switch (type)
		{
		case TypeLigneDossier.Reglement:
			_notifyService.Notify(TypeEntity.Reglement, entityNo, TypeAction.Modification, Societe.No);
			break;
		case TypeLigneDossier.Echeance:
			_notifyService.Notify(TypeEntity.Echeance, entityNo, TypeAction.Modification, Societe.No);
			break;
		}
		decimal montantDossier = _dossierReglementRepository.GetMontantDossier(ligneDossierReglementComm.DossierNo);
		_dossierReglementRepository.UpdateMontant(ligneDossierReglementComm.DossierNo, montantDossier);
		if (_licenceApplicationVersion.Application == ApplicationRunning.TresoClient)
		{
			_notifyService.Notify(TypeEntity.DossierClient, ligneDossierReglementComm.DossierNo, TypeAction.Modification, dossier.SocieteNo);
		}
		else
		{
			_notifyService.Notify(TypeEntity.Dossier, ligneDossierReglementComm.DossierNo, TypeAction.Modification, dossier.SocieteNo);
		}
		transactionScope.Complete();
	}

	public void LigneDossierReglementCommRetirer(int ligneNo)
	{
		LigneDossierReglementComm ligneDossierReglementComm = _ligneDossierReglementCommRepository.Get(ligneNo);
		if (ligneDossierReglementComm == null)
		{
			throw new ApplicationException("Impossible de charger la ligne.");
		}
		DeleteLigneDossierComm(ligneDossierReglementComm);
		DossierReglement dossier = _dossierReglementRepository.GetDossier(ligneDossierReglementComm.DossierNo);
		if (dossier == null)
		{
			throw new ApplicationException("Impossible de charger le dossier de règlement.");
		}
		if (_licenceApplicationVersion.Application == ApplicationRunning.TresoClient)
		{
			_notifyService.Notify(TypeEntity.DossierClient, ligneDossierReglementComm.DossierNo, TypeAction.Modification, dossier.SocieteNo);
		}
		else
		{
			_notifyService.Notify(TypeEntity.Dossier, ligneDossierReglementComm.DossierNo, TypeAction.Modification, dossier.SocieteNo);
		}
	}

	private void DeleteLigneDossierComm(LigneDossierReglementComm ligne)
	{
		if (ligne == null)
		{
			throw new ArgumentNullException("ligne");
		}
		DossierReglement dossier = _dossierReglementRepository.GetDossier(ligne.DossierNo);
		if (dossier == null)
		{
			throw new ApplicationException("Impossible de charger le dossier de règlement.");
		}
		bool flag = false;
		if (ligne.TypeLigneDossier == TypeLigneDossier.Echeance && dossier.StatutDossier == StatutDossierReglement.Valide)
		{
			Echeance echeance = _echeanceRepository.Get(ligne.EntityNo);
			if (echeance == null)
			{
				throw new ApplicationException("Impossible de charger l'échéance.");
			}
			if (echeance.Type != EcheanceType.PerteEchange && echeance.Type != EcheanceType.GainEchange)
			{
				throw new ApplicationException("L'échéance n'est pas de type ecart de change.");
			}
			flag = true;
		}
		if (dossier.StatutDossier != StatutDossierReglement.Encours && !flag)
		{
			throw new ApplicationException("Le statut du dossier est invalide.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_ligneDossierReglementCommRepository.Delete(ligne);
		decimal montantDossier = _dossierReglementRepository.GetMontantDossier(ligne.DossierNo);
		_dossierReglementRepository.UpdateMontant(ligne.DossierNo, montantDossier);
		transactionScope.Complete();
	}

	public void LigneDossierReglementCommUpdateMontantAPaye(int ligneNo, decimal montantAPaye)
	{
		if (ligneNo <= 0)
		{
			throw new ArgumentNullException("ligneNo");
		}
		LigneDossierReglementComm ligneDossierReglementComm = _ligneDossierReglementCommRepository.Get(ligneNo);
		if (ligneDossierReglementComm == null)
		{
			throw new ApplicationException("Impossible de charger la ligne.");
		}
		DossierReglement dossier = _dossierReglementRepository.GetDossier(ligneDossierReglementComm.DossierNo);
		if (dossier == null)
		{
			throw new ApplicationException("Impossible de charger le dossier de règlement.");
		}
		if (dossier.StatutDossier != StatutDossierReglement.Encours)
		{
			throw new ApplicationException("Le statut du dossier est invalide.");
		}
		if (ligneDossierReglementComm.TypeLigneDossier != TypeLigneDossier.Echeance && ligneDossierReglementComm.TypeLigneDossier != TypeLigneDossier.Reglement)
		{
			throw new ApplicationException("Type ligne invalide.");
		}
		decimal value = default(decimal);
		decimal num = default(decimal);
		switch (ligneDossierReglementComm.TypeLigneDossier)
		{
		case TypeLigneDossier.Echeance:
		{
			Echeance echeance = _echeanceRepository.Get(ligneDossierReglementComm.EntityNo);
			if (echeance == null)
			{
				throw new ApplicationException("Impossible de charger l'échéance.");
			}
			if (echeance.Etat != Etat.NonPaye || echeance.Solde == 0m)
			{
				throw new ApplicationException("L'échéance est totalement payée.");
			}
			value = echeance.Solde;
			num = echeance.MontantDeviseSociete;
			break;
		}
		case TypeLigneDossier.Reglement:
			if (ligneDossierReglementComm.IsRetenue)
			{
				if ((CaisseManager.RetenueGet(ligneDossierReglementComm.EntityNo) ?? throw new ArgumentException("La retenue est invalide!")).Montant != montantAPaye)
				{
					throw new ApplicationException("Impossible de modifier le montant à payer d'un retenue à la source.");
				}
				return;
			}
			if (!ligneDossierReglementComm.IsRetenue)
			{
				ReglementFournisseur obj = CaisseManager.ReglementFournisseurGet(ligneDossierReglementComm.EntityNo) ?? throw new ApplicationException("Impossible de charger le règlement.");
				if (obj.Solde <= 0m)
				{
					throw new ApplicationException("Le règlement est totalement soldé.");
				}
				value = obj.Solde;
				num = obj.MontantDeviseSociete;
			}
			break;
		}
		if ((montantAPaye < 0m && num >= 0m) || (montantAPaye > 0m && num <= 0m) || Math.Abs(montantAPaye) > Math.Abs(value))
		{
			throw new ApplicationException("Le montant à payer est invalide.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_ligneDossierReglementCommRepository.UpdateMontantAPayer(ligneDossierReglementComm.No, montantAPaye);
		decimal montantDossier = _dossierReglementRepository.GetMontantDossier(ligneDossierReglementComm.DossierNo);
		_dossierReglementRepository.UpdateMontant(ligneDossierReglementComm.DossierNo, montantDossier);
		if (_licenceApplicationVersion.Application == ApplicationRunning.TresoClient)
		{
			_notifyService.Notify(TypeEntity.DossierClient, ligneDossierReglementComm.DossierNo, TypeAction.Modification, dossier.SocieteNo);
		}
		else
		{
			_notifyService.Notify(TypeEntity.Dossier, ligneDossierReglementComm.DossierNo, TypeAction.Modification, dossier.SocieteNo);
		}
		transactionScope.Complete();
	}

	public void LigneDossierReglementCommUpdateCours(int ligneNo, decimal cours)
	{
		if (ligneNo <= 0)
		{
			throw new ArgumentNullException("ligneNo");
		}
		LigneDossierReglementComm ligneDossierReglementComm = _ligneDossierReglementCommRepository.Get(ligneNo);
		if (ligneDossierReglementComm == null)
		{
			throw new ApplicationException("Impossible de charger la ligne.");
		}
		DossierReglement dossier = _dossierReglementRepository.GetDossier(ligneDossierReglementComm.DossierNo);
		if (dossier == null)
		{
			throw new ApplicationException("Impossible de charger le dossier de règlement.");
		}
		if (dossier.StatutDossier != StatutDossierReglement.Encours)
		{
			throw new ApplicationException("Le statut du dossier est invalide.");
		}
		if (ligneDossierReglementComm.TypeLigneDossier != TypeLigneDossier.Reglement)
		{
			throw new ApplicationException("Type ligne invalide.");
		}
		if (cours < 0m)
		{
			throw new ApplicationException("Le cours est invalide.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		if (ligneDossierReglementComm.IsRetenue)
		{
			RetenuALaSource retenuALaSource = CaisseManager.RetenueGet(ligneDossierReglementComm.EntityNo);
			if (retenuALaSource == null)
			{
				throw new ApplicationException("Impossible de charger la retenue.");
			}
			if (retenuALaSource.Cours == cours)
			{
				return;
			}
			if (retenuALaSource.IsComptabilise)
			{
				throw new ApplicationException("La retenue [" + retenuALaSource.Numero + "] est comptabilisée.");
			}
			if (retenuALaSource.Montant != retenuALaSource.Solde)
			{
				throw new ApplicationException("La retenue est soldée.");
			}
			using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
			SocieteDevise deviseErp = Societe.GetDeviseErp(Societe.DeviseErpNo);
			if (deviseErp == null)
			{
				throw new ApplicationException("Impossible de charger la devise société.");
			}
			CaisseManager.RetenueFournisseurUpdateCours(retenuALaSource.No, deviseErp.No, cours, dossier.IsDossierCommercial);
			_notifyService.Notify(TypeEntity.Dossier, ligneDossierReglementComm.DossierNo, TypeAction.Modification, dossier.SocieteNo);
			_notifyService.Notify(TypeEntity.ReglementFournisseur, retenuALaSource.No, TypeAction.Modification, dossier.SocieteNo);
			transactionScope.Complete();
			return;
		}
		ReglementFournisseur reglementFournisseur = CaisseManager.ReglementFournisseurGet(ligneDossierReglementComm.EntityNo);
		if (reglementFournisseur == null)
		{
			throw new ApplicationException("Impossible de charger le règlement.");
		}
		if (reglementFournisseur.DeviseCours == cours)
		{
			return;
		}
		if (reglementFournisseur.IsComptabilise != EtatComptabilite.NonComptabilise)
		{
			throw new ApplicationException("Le règlement [" + reglementFournisseur.Numero + "] est comptabilisé.");
		}
		if (reglementFournisseur.IsValider)
		{
			throw new ApplicationException("Le règlement est validé.");
		}
		if (reglementFournisseur.Montant != reglementFournisseur.Solde)
		{
			throw new ApplicationException("Le règlement est partiellement soldé.");
		}
		using TransactionScope transactionScope2 = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		SocieteDevise deviseErp2 = Societe.GetDeviseErp(Societe.DeviseErpNo);
		if (deviseErp2 == null)
		{
			throw new ApplicationException("Impossible de charger la devise société.");
		}
		CaisseManager.ReglementFournisseurUpdateCours(reglementFournisseur.No, deviseErp2.No, cours, dossier.IsDossierCommercial);
		_notifyService.Notify(TypeEntity.Dossier, ligneDossierReglementComm.DossierNo, TypeAction.Modification, dossier.SocieteNo);
		_notifyService.Notify(TypeEntity.ReglementFournisseur, reglementFournisseur.No, TypeAction.Modification, dossier.SocieteNo);
		transactionScope2.Complete();
	}

	public void EcheanceReserver(int echeanceNo, ErpDomaine domaine = ErpDomaine.Achat)
	{
		Echeance echeance = _echeanceRepository.Get(echeanceNo);
		if (echeance == null)
		{
			throw new ApplicationException("Impossible de charger l'échéance.");
		}
		string text = ((domaine == ErpDomaine.Achat) ? "Achat" : "Vente");
		if (echeance.Domaine != domaine)
		{
			throw new ApplicationException("Impossible de réserver une échéance n'est pas de type " + text);
		}
		if (echeance.IsReserveDossierFrs)
		{
			throw new ApplicationException("L'échéance [" + echeance.DocumentNumero + "] est déjà réservée.");
		}
		if (echeance.Etat != Etat.NonPaye)
		{
			throw new ApplicationException("Opération invalide. L'échéance [" + echeance.DocumentNumero + "] est totalement payée.[Réservation]");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_echeanceRepository.UpdateReservation(echeance.No, reserve: true);
		_notifyService.Notify(TypeEntity.Echeance, echeance.No, TypeAction.Modification, echeance.SocieteNo);
		if (echeance.Type == EcheanceType.ImpayeFournisseur)
		{
			_notifyService.Notify(TypeEntity.ImpayeFournisseur, echeance.ReglementImpayeNo, TypeAction.Modification, echeance.SocieteNo);
		}
		transactionScope.Complete();
	}

	public void EcheanceAnnulerReservation(int echeanceNo)
	{
		Echeance echeance = _echeanceRepository.Get(echeanceNo);
		if (echeance == null)
		{
			throw new ApplicationException("Impossible de charger l'échéance.");
		}
		if (echeance.Domaine != ErpDomaine.Achat && _licenceApplicationVersion.Application != ApplicationRunning.TresoClient)
		{
			throw new ApplicationException("Impossible de réservé une écheance n'est pas de type Achat");
		}
		if (echeance.Domaine != ErpDomaine.Vente && _licenceApplicationVersion.Application == ApplicationRunning.TresoClient)
		{
			throw new ApplicationException("Impossible de réservé une écheance n'est pas de type Achat");
		}
		if (!echeance.IsReserveDossierFrs)
		{
			throw new ApplicationException("L'échéance [" + echeance.DocumentNumero + "] est déjà non réservée.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_echeanceRepository.UpdateReservation(echeance.No, reserve: false);
		_notifyService.Notify(TypeEntity.Echeance, echeance.No, TypeAction.Modification, echeance.SocieteNo);
		if (echeance.Type == EcheanceType.ImpayeFournisseur)
		{
			_notifyService.Notify(TypeEntity.ImpayeFournisseur, echeance.ReglementImpayeNo, TypeAction.Modification, echeance.SocieteNo);
		}
		transactionScope.Complete();
	}

	public void DossierReglementClientValider(int dossierNo)
	{
		if (dossierNo <= 0)
		{
			throw new ArgumentException("dossierNo");
		}
		DossierReglement dossier = _dossierReglementRepository.GetDossier(dossierNo);
		if (dossier == null)
		{
			throw new ApplicationException("Impossible de charger le dossier.");
		}
		if (dossier.StatutDossier != StatutDossierReglement.Encours)
		{
			throw new ApplicationException("Le statut du dossier est invalide.");
		}
		IEnumerable<LigneDossierReglementComm> all = _ligneDossierReglementCommRepository.GetAll(dossierNo);
		if (!all.Any())
		{
			throw new ApplicationException("Le dossier de contient aucune ligne.");
		}
		IEnumerable<LigneDossierReglementComm> enumerable = all.Where((LigneDossierReglementComm x) => x.TypeLigneDossier == TypeLigneDossier.Reglement);
		LigneDossierReglementComm ligneDossierReglementComm = enumerable.FirstOrDefault();
		if (ligneDossierReglementComm == null)
		{
			throw new ApplicationException("Le dossier de contient aucune ligne règlement.");
		}
		if (!CaisseManager.AffectationGetAllByReglement(ligneDossierReglementComm.EntityNo).Any())
		{
			throw new ApplicationException("Le dossier n'a subit aucune affectation.");
		}
		SocieteDevise deviseErp = Societe.GetDeviseErp(Societe.DeviseErpNo);
		decimal d = default(decimal);
		if (dossier.DeviseNo != deviseErp.No)
		{
			foreach (LigneDossierReglementComm item in all.Where((LigneDossierReglementComm x) => x.TypeLigneDossier == TypeLigneDossier.Echeance))
			{
				Echeance echeance = _echeanceRepository.Get(item.EntityNo);
				if (echeance == null)
				{
					throw new ApplicationException("Impossible de charger l'échéance.");
				}
				d += item.MontantAPaye * echeance.CoursDevise;
			}
			foreach (LigneDossierReglementComm item2 in enumerable)
			{
				ReglementClient reglementClient = CaisseManager.ReglementGet(item2.EntityNo);
				if (reglementClient == null)
				{
					throw new ApplicationException($"Impossible de charger le règlement [{item2.EntityNo}].");
				}
				d -= item2.MontantAPaye * reglementClient.DeviseCours;
			}
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_dossierReglementRepository.UpdateStatut(dossierNo, StatutDossierReglement.Valide);
		_dossierReglementRepository.UpdatePhase(dossierNo, PhaseDossierReglement.Cloture);
		_dossierReglementRepository.UpdateMontantEcart(dossierNo, Math.Round(d, deviseErp.NombreDecimales));
		_notifyService.Notify(TypeEntity.DossierClient, dossierNo, TypeAction.Modification, dossier.SocieteNo);
		foreach (LigneDossierReglementComm item3 in enumerable)
		{
			bool isValid = _reglementClientRepository.IsReglementValid(item3.EntityNo, item3.SocieteNo);
			_reglementClientRepository.UpdateReglementValid(item3.EntityNo, isValid);
			_notifyService.Notify(TypeEntity.Reglement, item3.EntityNo, TypeAction.Modification, dossier.SocieteNo);
		}
		transactionScope.Complete();
	}

	public void DossierReglementFournisseurValider(int dossierNo)
	{
		if (dossierNo <= 0)
		{
			throw new ArgumentException("dossierNo");
		}
		DossierReglement dossier = _dossierReglementRepository.GetDossier(dossierNo);
		if (dossier == null)
		{
			throw new ApplicationException("Impossible de charger le dossier.");
		}
		if (dossier.StatutDossier != StatutDossierReglement.Encours)
		{
			throw new ApplicationException("Le statut du dossier est invalide.");
		}
		IEnumerable<LigneDossierReglementComm> all = _ligneDossierReglementCommRepository.GetAll(dossierNo);
		if (!all.Any())
		{
			throw new ApplicationException("Le dossier de contient aucune ligne.");
		}
		IEnumerable<LigneDossierReglementComm> enumerable = all.Where((LigneDossierReglementComm x) => x.TypeLigneDossier == TypeLigneDossier.Reglement);
		LigneDossierReglementComm ligneDossierReglementComm = enumerable.FirstOrDefault();
		if (ligneDossierReglementComm == null)
		{
			throw new ApplicationException("Le dossier de contient aucune ligne règlement.");
		}
		if (!CaisseManager.AffectationGetAllByReglement(ligneDossierReglementComm.EntityNo).Any())
		{
			throw new ApplicationException("Le dossier n'a subit aucune affectation.");
		}
		SocieteDevise deviseErp = Societe.GetDeviseErp(Societe.DeviseErpNo);
		decimal d = default(decimal);
		if (dossier.DeviseNo != deviseErp.No)
		{
			foreach (LigneDossierReglementComm item in all.Where((LigneDossierReglementComm x) => x.TypeLigneDossier == TypeLigneDossier.Echeance))
			{
				Echeance echeance = _echeanceRepository.Get(item.EntityNo);
				if (echeance == null)
				{
					throw new ApplicationException("Impossible de charger l'échéance.");
				}
				d += item.MontantAPaye * echeance.CoursDevise;
			}
			foreach (LigneDossierReglementComm item2 in enumerable)
			{
				ReglementFournisseur reglementFournisseur = CaisseManager.ReglementFournisseurGet(item2.EntityNo);
				if (reglementFournisseur == null)
				{
					throw new ApplicationException($"Impossible de charger le règlement [{item2.EntityNo}].");
				}
				d -= item2.MontantAPaye * reglementFournisseur.DeviseCours;
			}
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_dossierReglementRepository.UpdateStatut(dossierNo, StatutDossierReglement.Valide);
		_dossierReglementRepository.UpdatePhase(dossierNo, PhaseDossierReglement.Cloture);
		_dossierReglementRepository.UpdateMontantEcart(dossierNo, Math.Round(d, deviseErp.NombreDecimales));
		_notifyService.Notify(TypeEntity.Dossier, dossierNo, TypeAction.Modification, dossier.SocieteNo);
		foreach (LigneDossierReglementComm item3 in enumerable)
		{
			bool isValid = _reglementFournisseurRepository.IsReglementValid(item3.EntityNo, item3.SocieteNo);
			_reglementFournisseurRepository.UpdateReglementValid(item3.EntityNo, isValid);
			_notifyService.Notify(TypeEntity.ReglementFournisseur, item3.EntityNo, TypeAction.Modification, dossier.SocieteNo);
		}
		transactionScope.Complete();
	}

	public void DossierReglementFournisseurChangePhase(int dossierNo, PhaseDossierReglement newEtape)
	{
		if (dossierNo <= 0)
		{
			throw new ArgumentNullException("dossierNo");
		}
		DossierReglement dossier = _dossierReglementRepository.GetDossier(dossierNo);
		if (dossier == null)
		{
			throw new ApplicationException($"Impossible de charger le dossier de règlement fournisseur [{dossierNo}].");
		}
		if (newEtape != PhaseDossierReglement.SaisiePaiement && newEtape != PhaseDossierReglement.ValidationFacture)
		{
			throw new ApplicationException("Pahse invalide.");
		}
		IEnumerable<LigneDossierReglementComm> all = _ligneDossierReglementCommRepository.GetAll(dossierNo);
		IEnumerable<LigneDossierReglementComm> source = all.Where((LigneDossierReglementComm x) => x.TypeLigneDossier == TypeLigneDossier.Reglement);
		if (!all.Where((LigneDossierReglementComm x) => x.TypeLigneDossier == TypeLigneDossier.Echeance).Any() && newEtape == PhaseDossierReglement.SaisiePaiement)
		{
			throw new ApplicationException("Le dossier ne contient aucune ligne échéance.");
		}
		if (dossier.PhaseDossier == PhaseDossierReglement.SaisiePaiement && newEtape == PhaseDossierReglement.ValidationFacture)
		{
			if (all.Any((LigneDossierReglementComm x) => x.IsRetenue))
			{
				throw new ApplicationException("Opération invalide!\nLe dossier contient des lignes retenue à la source.");
			}
			if (source.Any())
			{
				throw new ApplicationException("Opération invalide!\nLe dossier contient des lignes règlements.");
			}
		}
		if (dossier.StatutDossier == StatutDossierReglement.Valide)
		{
			throw new ApplicationException("Le dossier de règlement est validé.");
		}
		if (dossier.IsComptabilise)
		{
			throw new ApplicationException("Le dossier de règlement est comptabilisé.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_dossierReglementRepository.UpdatePhase(dossierNo, newEtape);
		_notifyService.Notify(TypeEntity.Dossier, dossierNo, TypeAction.Modification, dossier.SocieteNo);
		transactionScope.Complete();
	}

	public void DossierReglementFournisseurAnnulerValidation(int dossierNo, int deviseSocieteNo)
	{
		if (dossierNo <= 0)
		{
			throw new ArgumentNullException("dossierNo");
		}
		if (deviseSocieteNo <= 0)
		{
			throw new ArgumentNullException("deviseSocieteNo");
		}
		DossierReglement dossier = _dossierReglementRepository.GetDossier(dossierNo);
		if (dossier == null)
		{
			throw new ApplicationException($"Impossible de charger le dossier de règlement fournisseur [{dossierNo}].");
		}
		if (dossier.StatutDossier != StatutDossierReglement.Valide)
		{
			throw new ApplicationException("Le dossier de règlement n'est pas validé.");
		}
		if (dossier.IsComptabilise)
		{
			throw new ApplicationException("Le dossier de règlement est comptabilisé.");
		}
		DeclarationRetenuSource declarationRetenuSource = _declarationRetenuSourceRepository.Get(Societe.No, dossier.Date.Year, (DeclarationMoisPeriode)dossier.Date.Month);
		if (declarationRetenuSource != null && declarationRetenuSource.Statut == StatutDeclaration.Cloture)
		{
			throw new ApplicationException("Le dossier [" + dossier.Numero + "] est inclus dans une période de déclaration RAS clôturée.");
		}
		IEnumerable<LigneDossierReglementComm> all = _ligneDossierReglementCommRepository.GetAll(dossierNo);
		IEnumerable<LigneDossierReglementComm> enumerable = all.Where((LigneDossierReglementComm x) => x.TypeLigneDossier == TypeLigneDossier.Reglement);
		IEnumerable<LigneDossierReglementComm> ligneEcheance = all.Where((LigneDossierReglementComm x) => x.TypeLigneDossier == TypeLigneDossier.Echeance);
		foreach (LigneDossierReglementComm item in enumerable)
		{
			ReglementFournisseur reglementFournisseur = CaisseManager.ReglementFournisseurGet(item.EntityNo);
			if (reglementFournisseur == null)
			{
				throw new ApplicationException("Impossible de charger le règlement.");
			}
			if (reglementFournisseur.IsReserveDossierFrs)
			{
				throw new ApplicationException("Le règlement [" + reglementFournisseur.Numero + "] est réservé dans un autre dossier de règlement fournisseur.");
			}
		}
		foreach (LigneDossierReglementComm item2 in ligneEcheance)
		{
			Echeance echeance = _echeanceRepository.Get(item2.EntityNo);
			if (echeance == null)
			{
				throw new ApplicationException("Impossible de charger l'échéance.");
			}
			if (echeance.IsReserveDossierFrs)
			{
				throw new ApplicationException("L'échéance [" + echeance.DocumentNumero + "] est réservée dans un autre dossier de règlement fournisseur.");
			}
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		foreach (LigneDossierReglementComm item3 in enumerable)
		{
			IEnumerable<Affectation> enumerable2 = from x in CaisseManager.AffectationGetAllByReglement(item3.EntityNo)
				orderby x.MontantDeviseSociete descending
				where !x.EcartEcheanceNo.HasValue && ligneEcheance.Any((LigneDossierReglementComm e) => e.EntityNo == x.EcheanceNo)
				select x;
			if (!enumerable2.Any())
			{
				throw new ApplicationException($"Aucune affectation trouvée pour le règlement n°[{item3.EntityNo}].");
			}
			foreach (Affectation item4 in enumerable2)
			{
				CaisseManager.AffectationFournisseurDelete(item4.ReglementNo, item4.No, deviseSocieteNo, isDossierImpaye: false, dossierNo);
			}
		}
		_dossierReglementRepository.UpdateStatut(dossierNo, StatutDossierReglement.Encours);
		_dossierReglementRepository.UpdatePhase(dossierNo, PhaseDossierReglement.SaisiePaiement);
		_dossierReglementRepository.UpdateMontantEcart(dossierNo, 0m);
		_notifyService.Notify(TypeEntity.Dossier, dossierNo, TypeAction.Modification, dossier.SocieteNo);
		foreach (LigneDossierReglementComm item5 in enumerable)
		{
			CaisseManager.ReglementFournisseurReserver(item5.EntityNo);
			ReglementFournisseur reglementFournisseur2 = CaisseManager.ReglementFournisseurGet(item5.EntityNo);
			if (reglementFournisseur2 == null)
			{
				throw new ApplicationException($"Impossible de charger le règlement n°[{item5.EntityNo}].");
			}
			if (reglementFournisseur2.Solde == 0m)
			{
				throw new ApplicationException("Solde règlement [" + reglementFournisseur2.Numero + "] est égal à zéro !");
			}
			_ligneDossierReglementCommRepository.UpdateMontantSolde(item5.No, reglementFournisseur2.Solde);
			decimal num = Math.Min(item5.MontantAPaye, reglementFournisseur2.Solde);
			if (item5.MontantAPaye != num)
			{
				LigneDossierReglementCommUpdateMontantAPaye(item5.No, num);
			}
			bool isValid = _reglementFournisseurRepository.IsReglementValid(item5.EntityNo, item5.SocieteNo);
			_reglementFournisseurRepository.UpdateReglementValid(item5.EntityNo, isValid);
			_notifyService.Notify(TypeEntity.ReglementFournisseur, item5.EntityNo, TypeAction.Modification, dossier.SocieteNo);
		}
		foreach (LigneDossierReglementComm item6 in _ligneDossierReglementCommRepository.GetLigneEcheance(dossierNo))
		{
			EcheanceReserver(item6.EntityNo);
			Echeance echeance2 = _echeanceRepository.Get(item6.EntityNo);
			if (echeance2 == null)
			{
				throw new ApplicationException($"Impossible de charger l'échéance n°[{item6.EntityNo}].");
			}
			if (echeance2.Solde == 0m)
			{
				throw new ApplicationException("Solde échéance [" + echeance2.DocumentNumero + "] est égal à zéro !");
			}
			_ligneDossierReglementCommRepository.UpdateMontantSolde(item6.No, echeance2.Solde);
			decimal num2 = (decimal)((echeance2.Montant >= 0m) ? 1 : (-1)) * Math.Min(Math.Abs(item6.MontantAPaye), Math.Abs(echeance2.Solde));
			if (item6.MontantAPaye != num2)
			{
				LigneDossierReglementCommUpdateMontantAPaye(item6.No, num2);
			}
		}
		transactionScope.Complete();
	}

	public void DossierReglementClientAnnulerValidation(int dossierNo, int deviseSocieteNo)
	{
		if (dossierNo <= 0)
		{
			throw new ArgumentNullException("dossierNo");
		}
		if (deviseSocieteNo <= 0)
		{
			throw new ArgumentNullException("deviseSocieteNo");
		}
		DossierReglement dossier = _dossierReglementRepository.GetDossier(dossierNo);
		if (dossier == null)
		{
			throw new ApplicationException($"Impossible de charger le dossier de règlement client [{dossierNo}].");
		}
		if (dossier.StatutDossier != StatutDossierReglement.Valide)
		{
			throw new ApplicationException("Le dossier de règlement n'est pas validé.");
		}
		if (dossier.IsComptabilise)
		{
			throw new ApplicationException("Le dossier de règlement est comptabilisé.");
		}
		DeclarationRetenuSource declarationRetenuSource = _declarationRetenuSourceRepository.Get(Societe.No, dossier.Date.Year, (DeclarationMoisPeriode)dossier.Date.Month);
		if (declarationRetenuSource != null && declarationRetenuSource.Statut == StatutDeclaration.Cloture)
		{
			throw new ApplicationException("Le dossier [" + dossier.Numero + "] est inclus dans une période de déclaration RAS clôturée.");
		}
		IEnumerable<LigneDossierReglementComm> all = _ligneDossierReglementCommRepository.GetAll(dossierNo);
		IEnumerable<LigneDossierReglementComm> enumerable = all.Where((LigneDossierReglementComm x) => x.TypeLigneDossier == TypeLigneDossier.Reglement);
		IEnumerable<LigneDossierReglementComm> ligneEcheance = all.Where((LigneDossierReglementComm x) => x.TypeLigneDossier == TypeLigneDossier.Echeance);
		foreach (LigneDossierReglementComm item in enumerable)
		{
			ReglementClient reglementClient = CaisseManager.ReglementGet(item.EntityNo);
			if (reglementClient == null)
			{
				throw new ApplicationException("Impossible de charger le règlement.");
			}
			if (reglementClient.IsReserveDossierClt)
			{
				throw new ApplicationException("Le règlement [" + reglementClient.Numero + "] est réservé dans un autre dossier de règlement.");
			}
		}
		foreach (LigneDossierReglementComm item2 in ligneEcheance)
		{
			Echeance echeance = _echeanceRepository.Get(item2.EntityNo);
			if (echeance == null)
			{
				throw new ApplicationException("Impossible de charger l'échéance.");
			}
			if (echeance.IsReserveDossierFrs)
			{
				throw new ApplicationException("L'échéance [" + echeance.DocumentNumero + "] est réservée dans un autre dossier de règlement .");
			}
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		foreach (LigneDossierReglementComm item3 in enumerable)
		{
			IEnumerable<Affectation> enumerable2 = from x in CaisseManager.AffectationGetAllByReglement(item3.EntityNo)
				orderby x.MontantDeviseSociete descending
				where !x.EcartEcheanceNo.HasValue && ligneEcheance.Any((LigneDossierReglementComm e) => e.EntityNo == x.EcheanceNo)
				select x;
			if (!enumerable2.Any())
			{
				throw new ApplicationException($"Aucune affectation trouvée pour le règlement n°[{item3.EntityNo}].");
			}
			foreach (Affectation item4 in enumerable2)
			{
				CaisseManager.AffectationDelete(item4.ReglementNo, item4.No, deviseSocieteNo, isDossierImpaye: false, dossierNo);
			}
		}
		_dossierReglementRepository.UpdateStatut(dossierNo, StatutDossierReglement.Encours);
		_dossierReglementRepository.UpdatePhase(dossierNo, PhaseDossierReglement.SaisiePaiement);
		_dossierReglementRepository.UpdateMontantEcart(dossierNo, 0m);
		_notifyService.Notify(TypeEntity.DossierClient, dossierNo, TypeAction.Modification, dossier.SocieteNo);
		foreach (LigneDossierReglementComm item5 in enumerable)
		{
			CaisseManager.ReglementClientReserver(item5.EntityNo);
			ReglementClient reglementClient2 = CaisseManager.ReglementGet(item5.EntityNo);
			if (reglementClient2 == null)
			{
				throw new ApplicationException($"Impossible de charger le règlement n°[{item5.EntityNo}].");
			}
			if (reglementClient2.Solde == 0m)
			{
				throw new ApplicationException("Solde règlement [" + reglementClient2.Numero + "] est égal à zéro !");
			}
			_ligneDossierReglementCommRepository.UpdateMontantSolde(item5.No, reglementClient2.Solde);
			decimal num = Math.Min(item5.MontantAPaye, reglementClient2.Solde);
			if (item5.MontantAPaye != num)
			{
				LigneDossierReglementCommUpdateMontantAPaye(item5.No, num);
			}
			bool isValid = _reglementClientRepository.IsReglementValid(item5.EntityNo, item5.SocieteNo);
			_reglementClientRepository.UpdateReglementValid(item5.EntityNo, isValid);
			_notifyService.Notify(TypeEntity.Reglement, item5.EntityNo, TypeAction.Modification, dossier.SocieteNo);
		}
		foreach (LigneDossierReglementComm item6 in _ligneDossierReglementCommRepository.GetLigneEcheance(dossierNo))
		{
			EcheanceReserver(item6.EntityNo, ErpDomaine.Vente);
			Echeance echeance2 = _echeanceRepository.Get(item6.EntityNo);
			if (echeance2 == null)
			{
				throw new ApplicationException($"Impossible de charger l'échéance n°[{item6.EntityNo}].");
			}
			if (echeance2.Solde == 0m)
			{
				throw new ApplicationException("Solde échéance [" + echeance2.DocumentNumero + "] est égal à zéro !");
			}
			_ligneDossierReglementCommRepository.UpdateMontantSolde(item6.No, echeance2.Solde);
			decimal num2 = (decimal)((echeance2.Montant >= 0m) ? 1 : (-1)) * Math.Min(Math.Abs(item6.MontantAPaye), Math.Abs(echeance2.Solde));
			if (item6.MontantAPaye != num2)
			{
				LigneDossierReglementCommUpdateMontantAPaye(item6.No, num2);
			}
		}
		transactionScope.Complete();
	}

	public List<BalanceAgee> BalanceAgeeGetAll(ErpDomaine domaine, DateTime? dateDebut)
	{
		if (!Utilisateur.IsAdmin)
		{
			return _balanceAgeeRepository.Get(Societe.No, domaine, dateDebut, (from a in GetAllAutorizedSouches(domaine)
				select a.SoucheNo).ToArray()).ToList();
		}
		return _balanceAgeeRepository.Get(Societe.No, domaine, dateDebut).ToList();
	}

	public ParametreMailing ParametreMailGet()
	{
		return _parametreMailRepository.Get(Societe.No);
	}

	public void UpdateParametreMailGet(ParametreMailing parametreMailing)
	{
		_parametreMailRepository.Update(parametreMailing);
	}

	public InformationLibre InfoLibreReglementGet()
	{
		return _informationLibreRepository.GetReglement(Societe.No);
	}

	public InformationLibre InfoLibreBordereauGet()
	{
		return _informationLibreRepository.GetBordereau(Societe.No);
	}

	public InformationLibre InfoLibreGrfGet()
	{
		return _informationLibreGrfRepository.Get(Societe.No);
	}

	public void UpdateInformationLibreReglement(InformationLibre informationLibre)
	{
		_informationLibreRepository.UpdateReglement(informationLibre);
	}

	public void UpdateInformationLibreBordereau(InformationLibre informationLibre)
	{
		_informationLibreRepository.UpdateBordereau(informationLibre);
	}

	public void UpdateInformationLibreGrf(InformationLibre informationLibre)
	{
		_informationLibreGrfRepository.Update(informationLibre);
	}

	public ConfigConnectionErpExtern GetConfigConnectionErpExtern()
	{
		return _configConnectionErpExternRepository.Get(Societe.No);
	}

	public void UpdateConfigErpExtern(ConfigConnectionErpExtern configConnectionErpExtern)
	{
		_configConnectionErpExternRepository.Update(configConnectionErpExtern);
	}

	public IEnumerable<MouvementBancaire> GetAllMouvementBancaires()
	{
		return _mvtBancaireRepository.GetAllMouvementBancaire(Societe.No);
	}

	public FourchetteCommission FourchetteCommissionGet(int no)
	{
		return _fourchetteCommissionRepository.Get(no);
	}

	public List<FourchetteCommission> FourchetteCommissionGetAll()
	{
		return _fourchetteCommissionRepository.GetAll(Societe.No);
	}

	public int FourchetteCommissionCreate(FourchetteCommission fourchetteCommission)
	{
		if (fourchetteCommission == null)
		{
			throw new ArgumentNullException("fourchetteCommission");
		}
		if (fourchetteCommission.NombreJourMin >= fourchetteCommission.NombreJourMax)
		{
			throw new ApplicationException("Le nombre de jours max. doit être supérieur au nombre de jours min.");
		}
		if (fourchetteCommission.TauxCheque < 0m || fourchetteCommission.TauxCheque > 100m)
		{
			throw new ApplicationException("Taux chèque invalid.");
		}
		if (fourchetteCommission.TauxTraite < 0m || fourchetteCommission.TauxTraite > 100m)
		{
			throw new ApplicationException("Taux traite invalid.");
		}
		if (fourchetteCommission.TauxEspece < 0m || fourchetteCommission.TauxEspece > 100m)
		{
			throw new ApplicationException("Taux espèce invalid.");
		}
		if (fourchetteCommission.TauxVirement < 0m || fourchetteCommission.TauxVirement > 100m)
		{
			throw new ApplicationException("Taux virement invalid.");
		}
		if (fourchetteCommission.TauxAutre < 0m || fourchetteCommission.TauxAutre > 100m)
		{
			throw new ApplicationException("Taux autre invalid.");
		}
		List<FourchetteCommission> all = _fourchetteCommissionRepository.GetAll(Societe.No);
		if (all.Any((FourchetteCommission x) => x.NombreJourMin <= fourchetteCommission.NombreJourMin && x.NombreJourMax >= fourchetteCommission.NombreJourMin))
		{
			throw new ApplicationException("Périodicité exite déjà.");
		}
		if (all.Any((FourchetteCommission x) => x.NombreJourMin <= fourchetteCommission.NombreJourMax && x.NombreJourMax >= fourchetteCommission.NombreJourMax))
		{
			throw new ApplicationException("Périodicité exite déjà.");
		}
		return _fourchetteCommissionRepository.Create(fourchetteCommission);
	}

	public void FourchetteCommissionUpdate(FourchetteCommission fourchetteCommission)
	{
		if (fourchetteCommission == null)
		{
			throw new ArgumentNullException("fourchetteCommission");
		}
		if (_fourchetteCommissionRepository.Get(fourchetteCommission.No) == null)
		{
			throw new ApplicationException("Impossible de charger la périodicité.");
		}
		if (fourchetteCommission.TauxCheque < 0m || fourchetteCommission.TauxCheque > 100m)
		{
			throw new ApplicationException("Taux chèque invalid.");
		}
		if (fourchetteCommission.TauxTraite < 0m || fourchetteCommission.TauxTraite > 100m)
		{
			throw new ApplicationException("Taux traite invalid.");
		}
		if (fourchetteCommission.TauxEspece < 0m || fourchetteCommission.TauxEspece > 100m)
		{
			throw new ApplicationException("Taux espèce invalid.");
		}
		if (fourchetteCommission.TauxVirement < 0m || fourchetteCommission.TauxVirement > 100m)
		{
			throw new ApplicationException("Taux virement invalid.");
		}
		if (fourchetteCommission.TauxAutre < 0m || fourchetteCommission.TauxAutre > 100m)
		{
			throw new ApplicationException("Taux autre invalid.");
		}
		_fourchetteCommissionRepository.Update(fourchetteCommission);
	}

	public void FourchetteCommissionDelete(FourchetteCommission fourchetteCommission)
	{
		if (fourchetteCommission == null)
		{
			throw new ArgumentNullException("fourchetteCommission");
		}
		if (_fourchetteCommissionRepository.Get(fourchetteCommission.No) == null)
		{
			throw new ApplicationException("Impossible de charger la périodicité.");
		}
		_fourchetteCommissionRepository.Delete(fourchetteCommission);
	}

	public IEnumerable<DeclarationTvaEncaissement> DeclarationTvaEncaissementGetAll()
	{
		return _declarationTvaEncaissementRepository.GetAll(Societe.No);
	}

	public IEnumerable<DeclarationRetenuSource> DeclarationRetenuSourceGetAll()
	{
		return _declarationRetenuSourceRepository.GetAll(Societe.No);
	}

	public bool DeclarationRetenuSourceHasLignes(int declarationNo)
	{
		if (Societe.LegislationType != Legislation.Maroc)
		{
			return _retenuALaSourceRepository.DeclarationTejHasLignes(declarationNo);
		}
		return _declarationRetenuSourceRepository.HasLignes(declarationNo);
	}

	public DeclarationTvaEncaissement DeclarationTvaEncaissementGet(int no)
	{
		return _declarationTvaEncaissementRepository.Get(no);
	}

	public DeclarationRetenuSource DeclarationRetenuSourceGet(int no)
	{
		return _declarationRetenuSourceRepository.Get(no);
	}

	public int DeclarationTvaEncaissementCreate(string numero, int exercice, DateTime date, TypeDeclarationTva type, string libelle, DeclarationMoisPeriode moisPeriode, DeclarationTvaEncaissementTrimestrePeriode trimestrePeriode)
	{
		if (string.IsNullOrEmpty(numero))
		{
			throw new ArgumentNullException("numero");
		}
		if (_declarationTvaEncaissementRepository.Get(Societe.No, numero) != null)
		{
			throw new ApplicationException("La déclaration [" + numero + "] existe déja.");
		}
		DateTime dateDebut = default(DateTime);
		DateTime dateFin = default(DateTime);
		switch (type)
		{
		case TypeDeclarationTva.Mensuelle:
			dateDebut = new DateTime(exercice, (int)moisPeriode, 1);
			dateFin = new DateTime(exercice, (int)moisPeriode, DateTime.DaysInMonth(exercice, (int)moisPeriode));
			break;
		case TypeDeclarationTva.Trimestrielle:
			switch (trimestrePeriode)
			{
			case DeclarationTvaEncaissementTrimestrePeriode.T1:
				dateDebut = new DateTime(exercice, 1, 1);
				dateFin = new DateTime(exercice, 3, 31);
				break;
			case DeclarationTvaEncaissementTrimestrePeriode.T2:
				dateDebut = new DateTime(exercice, 4, 1);
				dateFin = new DateTime(exercice, 6, 30);
				break;
			case DeclarationTvaEncaissementTrimestrePeriode.T3:
				dateDebut = new DateTime(exercice, 7, 1);
				dateFin = new DateTime(exercice, 9, 30);
				break;
			case DeclarationTvaEncaissementTrimestrePeriode.T4:
				dateDebut = new DateTime(exercice, 10, 1);
				dateFin = new DateTime(exercice, 12, 31);
				break;
			default:
				throw new ApplicationException("Trimestre invalide.");
			}
			break;
		default:
			throw new ApplicationException("Type déclaration invalide.");
		}
		if (_declarationTvaEncaissementRepository.Get(Societe.No, exercice, dateDebut, dateFin) != null)
		{
			throw new ApplicationException("Déclaration existe déja pour la même période.");
		}
		if (_declarationTvaEncaissementRepository.GetAll(Societe.No, exercice).Any((DeclarationTvaEncaissement x) => x.DateDebut <= dateDebut && x.DateFin >= dateFin))
		{
			throw new ApplicationException("Période de déclaration existe déja.");
		}
		DeclarationTvaEncaissement declaration = new DeclarationTvaEncaissement
		{
			Numero = numero,
			SocieteNo = Societe.No,
			Statut = StatutDeclaration.EnCours,
			TypeDeclaration = type,
			Date = date,
			Exercice = exercice,
			CreateurNo = Utilisateur.No,
			DateCreation = DateTime.Now,
			DateDebut = dateDebut.Date,
			DateFin = dateFin.Date.AddDays(1.0).AddSeconds(-1.0),
			ModificateurNo = Utilisateur.No,
			DateModification = DateTime.Now,
			IsDepose = false,
			Libelle = libelle,
			IsFichierGenerer = false,
			MoisPeriode = moisPeriode,
			TrimestrePeriode = trimestrePeriode,
			IsComptabilise = false,
			DateComptabilisation = date
		};
		int num = _declarationTvaEncaissementRepository.Create(declaration);
		_notifyService.Notify(TypeEntity.DeclarationTvaEncaissement, num, TypeAction.Ajout, Societe.No);
		return num;
	}

	public int DeclarationRetenuSourceCreate(string numero, int exercice, DateTime date, string libelle, DeclarationMoisPeriode moisPeriode, NatureDeclaration nature, DateTime dateDebut, DateTime dateFin)
	{
		if (string.IsNullOrEmpty(numero))
		{
			throw new ArgumentNullException("numero");
		}
		if (_declarationRetenuSourceRepository.Get(Societe.No, numero) != null)
		{
			throw new ApplicationException("La déclaration [" + numero + "] existe déja.");
		}
		if (dateDebut < new DateTime(2024, 6, 1) && Societe.LegislationType != Legislation.Maroc)
		{
			throw new InvalidOperationException("Opération invalide période non pris en charge.");
		}
		if (dateFin < new DateTime(2024, 7, 1) && Societe.LegislationType == Legislation.Maroc)
		{
			throw new InvalidOperationException("Opération invalide période non pris en charge.");
		}
		if (_declarationRetenuSourceRepository.GetAll(Societe.No, exercice).Any((DeclarationRetenuSource x) => dateDebut.IsBetween(x.DateDebut, x.DateFin) || dateFin.IsBetween(x.DateDebut, x.DateFin)))
		{
			throw new ApplicationException("Un chevauchement de périodes de déclaration existe déjà.");
		}
		DeclarationRetenuSource declaration = new DeclarationRetenuSource
		{
			Numero = numero,
			SocieteNo = Societe.No,
			Statut = StatutDeclaration.EnCours,
			Date = date,
			Exercice = exercice,
			CreateurNo = Utilisateur.No,
			DateCreation = DateTime.Now,
			DateDebut = dateDebut.Date,
			DateFin = dateFin.Date.AddDays(1.0).AddSeconds(-1.0),
			ModificateurNo = Utilisateur.No,
			DateModification = DateTime.Now,
			IsDepose = false,
			Libelle = libelle,
			IsFichierGenerer = false,
			MoisPeriode = moisPeriode,
			IsComptabilise = false,
			DateComptabilisation = date,
			Nature = nature
		};
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		int num = _declarationRetenuSourceRepository.Create(declaration);
		_notifyService.Notify(TypeEntity.DeclarationRetenuSource, num, TypeAction.Ajout, Societe.No);
		transactionScope.Complete();
		return num;
	}

	public void DeclarationTvaEncaissementUpdate(int no, string libelle)
	{
		DeclarationTvaEncaissement declarationTvaEncaissement = _declarationTvaEncaissementRepository.Get(no);
		if (declarationTvaEncaissement == null)
		{
			throw new ApplicationException("Impossible de charger la déclaration.");
		}
		if (declarationTvaEncaissement.IsDepose)
		{
			throw new ApplicationException("La déclaration est déposée.");
		}
		if (declarationTvaEncaissement.Statut != StatutDeclaration.EnCours)
		{
			throw new ApplicationException("La déclaration est clôturée.");
		}
		declarationTvaEncaissement.Libelle = libelle;
		declarationTvaEncaissement.ModificateurNo = Utilisateur.No;
		declarationTvaEncaissement.DateModification = DateTime.Now;
		_declarationTvaEncaissementRepository.Update(declarationTvaEncaissement);
		_notifyService.Notify(TypeEntity.DeclarationTvaEncaissement, no, TypeAction.Modification, Societe.No);
	}

	public void DeclarationRetenuSourceUpdate(int no, string libelle)
	{
		DeclarationRetenuSource declarationRetenuSource = _declarationRetenuSourceRepository.Get(no);
		if (declarationRetenuSource == null)
		{
			throw new ApplicationException("Impossible de charger la déclaration.");
		}
		if (declarationRetenuSource.IsDepose)
		{
			throw new ApplicationException("La déclaration est déposée.");
		}
		if (declarationRetenuSource.Statut != StatutDeclaration.EnCours)
		{
			throw new ApplicationException("La déclaration est clôturée.");
		}
		declarationRetenuSource.Libelle = libelle;
		declarationRetenuSource.ModificateurNo = Utilisateur.No;
		declarationRetenuSource.DateModification = DateTime.Now;
		_declarationRetenuSourceRepository.Update(declarationRetenuSource);
		_notifyService.Notify(TypeEntity.DeclarationRetenuSource, no, TypeAction.Modification, Societe.No);
	}

	public void DeclarationTvaEncaissementCloture(int no)
	{
		DeclarationTvaEncaissement declarationTvaEncaissement = _declarationTvaEncaissementRepository.Get(no);
		if (declarationTvaEncaissement == null)
		{
			throw new ApplicationException("Impossible de charger la déclaration.");
		}
		if (declarationTvaEncaissement.IsDepose)
		{
			throw new ApplicationException("La déclaration est déposée.");
		}
		if (declarationTvaEncaissement.Statut != StatutDeclaration.EnCours)
		{
			throw new ApplicationException("La déclaration est clôturée.");
		}
		if (!_declarationTvaEncaissementRepository.HasLignes(no))
		{
			throw new ApplicationException("La déclaration ne contient aucune ligne.");
		}
		declarationTvaEncaissement.Statut = StatutDeclaration.Cloture;
		declarationTvaEncaissement.ModificateurNo = Utilisateur.No;
		declarationTvaEncaissement.DateModification = DateTime.Now;
		_declarationTvaEncaissementRepository.Update(declarationTvaEncaissement);
		_notifyService.Notify(TypeEntity.DeclarationTvaEncaissement, no, TypeAction.Modification, Societe.No);
	}

	public void DeclarationRetenuSourceCloture(int no)
	{
		DeclarationRetenuSource declarationRetenuSource = _declarationRetenuSourceRepository.Get(no);
		if (declarationRetenuSource == null)
		{
			throw new ApplicationException("Impossible de charger la déclaration.");
		}
		if (declarationRetenuSource.IsDepose)
		{
			throw new ApplicationException("La déclaration est déposée.");
		}
		if (declarationRetenuSource.Statut != StatutDeclaration.EnCours)
		{
			throw new ApplicationException("La déclaration est clôturée.");
		}
		if (Societe.LegislationType == Legislation.Maroc)
		{
			if (!_declarationRetenuSourceRepository.HasLignes(no))
			{
				throw new ApplicationException("La déclaration ne contient aucune ligne.");
			}
		}
		else if (!_retenuALaSourceRepository.DeclarationTejHasLignes(no))
		{
			throw new ApplicationException("La déclaration ne contient aucune ligne.");
		}
		if (_dossierReglementRepository.HasDossierNonCloture(DomaineDossier.Fournisseur, declarationRetenuSource.SocieteNo, declarationRetenuSource.DateDebut.Date, declarationRetenuSource.DateFin.Date.AddDays(1.0).AddSeconds(-1.0)))
		{
			throw new ApplicationException("Opération invalide !\nIl existe un ou plusieurs dossiers de règlements non validés dans la période de déclaration.");
		}
		declarationRetenuSource.Statut = StatutDeclaration.Cloture;
		declarationRetenuSource.ModificateurNo = Utilisateur.No;
		declarationRetenuSource.DateModification = DateTime.Now;
		_declarationRetenuSourceRepository.Update(declarationRetenuSource);
		_notifyService.Notify(TypeEntity.DeclarationRetenuSource, no, TypeAction.Modification, Societe.No);
	}

	public void DeclarationTvaEncaissementAnnulerCloture(int no)
	{
		DeclarationTvaEncaissement declarationTvaEncaissement = _declarationTvaEncaissementRepository.Get(no);
		if (declarationTvaEncaissement == null)
		{
			throw new ApplicationException("Impossible de charger la déclaration.");
		}
		if (declarationTvaEncaissement.IsDepose)
		{
			throw new ApplicationException("La déclaration est déposée.");
		}
		if (declarationTvaEncaissement.IsFichierGenerer)
		{
			throw new ApplicationException("Le fichier de la déclaration est généré.");
		}
		if (declarationTvaEncaissement.Statut != StatutDeclaration.Cloture)
		{
			throw new ApplicationException("La déclaration n'est pas clôturée.");
		}
		declarationTvaEncaissement.Statut = StatutDeclaration.EnCours;
		declarationTvaEncaissement.ModificateurNo = Utilisateur.No;
		declarationTvaEncaissement.DateModification = DateTime.Now;
		_declarationTvaEncaissementRepository.Update(declarationTvaEncaissement);
		_notifyService.Notify(TypeEntity.DeclarationTvaEncaissement, no, TypeAction.Modification, Societe.No);
	}

	public void DeclarationRetenuSourceAnnulerCloture(int no)
	{
		DeclarationRetenuSource declarationRetenuSource = _declarationRetenuSourceRepository.Get(no);
		if (declarationRetenuSource == null)
		{
			throw new ApplicationException("Impossible de charger la déclaration.");
		}
		if (declarationRetenuSource.IsDepose)
		{
			throw new ApplicationException("La déclaration est déposée.");
		}
		if (declarationRetenuSource.IsFichierGenerer)
		{
			throw new ApplicationException("Le fichier de la déclaration est généré.");
		}
		if (declarationRetenuSource.Statut != StatutDeclaration.Cloture)
		{
			throw new ApplicationException("La déclaration n'est pas clôturée.");
		}
		if (_declarationRetenuSourceRepository.HasDeclarationPosterieur(declarationRetenuSource.SocieteNo, no))
		{
			throw new ApplicationException($"Opération invalide ! \n Il existe une déclaration dont la période est supérieure à [{declarationRetenuSource.MoisPeriode}/{declarationRetenuSource.Exercice}].");
		}
		declarationRetenuSource.Statut = StatutDeclaration.EnCours;
		declarationRetenuSource.ModificateurNo = Utilisateur.No;
		declarationRetenuSource.DateModification = DateTime.Now;
		_declarationRetenuSourceRepository.Update(declarationRetenuSource);
		_notifyService.Notify(TypeEntity.DeclarationRetenuSource, no, TypeAction.Modification, Societe.No);
	}

	public void DeclarationTvaEncaissementDepose(int no)
	{
		DeclarationTvaEncaissement declarationTvaEncaissement = _declarationTvaEncaissementRepository.Get(no);
		if (declarationTvaEncaissement == null)
		{
			throw new ApplicationException("Impossible de charger la déclaration.");
		}
		if (!declarationTvaEncaissement.IsFichierGenerer)
		{
			throw new ApplicationException("Le fichier du déclaration n'est pas généré.");
		}
		if (declarationTvaEncaissement.IsDepose)
		{
			throw new ApplicationException("La déclaration est déposée.");
		}
		if (declarationTvaEncaissement.Statut != StatutDeclaration.Cloture)
		{
			throw new ApplicationException("La déclaration n'est pas clôturée.");
		}
		if (!_declarationTvaEncaissementRepository.HasLignes(no))
		{
			throw new ApplicationException("La déclaration ne contient aucune ligne.");
		}
		declarationTvaEncaissement.IsDepose = true;
		declarationTvaEncaissement.ModificateurNo = Utilisateur.No;
		declarationTvaEncaissement.DateModification = DateTime.Now;
		_declarationTvaEncaissementRepository.Update(declarationTvaEncaissement);
		_notifyService.Notify(TypeEntity.DeclarationTvaEncaissement, no, TypeAction.Modification, Societe.No);
	}

	public void DeclarationRetenuSourceDepose(int no)
	{
		DeclarationRetenuSource declarationRetenuSource = _declarationRetenuSourceRepository.Get(no);
		if (declarationRetenuSource == null)
		{
			throw new ApplicationException("Impossible de charger la déclaration.");
		}
		if (!declarationRetenuSource.IsFichierGenerer)
		{
			throw new ApplicationException("Le fichier du déclaration n'est pas généré.");
		}
		if (declarationRetenuSource.IsDepose)
		{
			throw new ApplicationException("La déclaration est déposée.");
		}
		if (declarationRetenuSource.Statut != StatutDeclaration.Cloture)
		{
			throw new ApplicationException("La déclaration n'est pas clôturée.");
		}
		if (Societe.LegislationType == Legislation.Maroc)
		{
			if (!_declarationRetenuSourceRepository.HasLignes(no))
			{
				throw new ApplicationException("La déclaration ne contient aucune ligne.");
			}
		}
		else if (!_retenuALaSourceRepository.DeclarationTejHasLignes(no))
		{
			throw new ApplicationException("La déclaration ne contient aucune ligne.");
		}
		declarationRetenuSource.IsDepose = true;
		declarationRetenuSource.ModificateurNo = Utilisateur.No;
		declarationRetenuSource.DateModification = DateTime.Now;
		_declarationRetenuSourceRepository.Update(declarationRetenuSource);
		_notifyService.Notify(TypeEntity.DeclarationRetenuSource, no, TypeAction.Modification, Societe.No);
	}

	public void DeclarationTvaEncaissementFichierGenerer(int no)
	{
		DeclarationTvaEncaissement declarationTvaEncaissement = _declarationTvaEncaissementRepository.Get(no);
		if (declarationTvaEncaissement == null)
		{
			throw new ApplicationException("Impossible de charger la déclaration.");
		}
		if (declarationTvaEncaissement.IsFichierGenerer)
		{
			throw new ApplicationException("Le fichier du déclaration est déjà généré.");
		}
		if (declarationTvaEncaissement.IsDepose)
		{
			throw new ApplicationException("La déclaration est déposée.");
		}
		if (declarationTvaEncaissement.Statut != StatutDeclaration.Cloture)
		{
			throw new ApplicationException("La déclaration n'est pas clôturée.");
		}
		if (!_declarationTvaEncaissementRepository.HasLignes(no))
		{
			throw new ApplicationException("La déclaration ne contient aucune ligne.");
		}
		List<LigneDeclarationTvaEncaissement> list = (from x in _ligneDeclarationTvaEncaissementRepository.GetAll(no)
			where x.IsReport
			select x).ToList();
		foreach (LigneDeclarationTvaEncaissement item in list)
		{
			switch (item.EntityType)
			{
			case LigneDeclarationTvaEncaissementEntityType.Affectation:
			{
				Affectation affectation = CaisseManager.AffectationGet(item.EntityNo);
				if (affectation == null)
				{
					throw new ApplicationException("Impossible de charger l'affectation.");
				}
				if (!affectation.DeclarationTvaEncaissementNo.HasValue || affectation.DeclarationTvaEncaissementNo.Value != declarationTvaEncaissement.No)
				{
					throw new ApplicationException("L'affectation ne partage pas la même déclaration que la ligne.");
				}
				break;
			}
			case LigneDeclarationTvaEncaissementEntityType.Depense:
			{
				MouvementDepense mouvementDepense = Groupe.DepenseManager.GetMouvementDepense(item.EntityNo);
				if (mouvementDepense == null)
				{
					throw new ApplicationException("Impossible de charger la dépense");
				}
				if (!mouvementDepense.DeclarationTvaEncaissementNo.HasValue || mouvementDepense.DeclarationTvaEncaissementNo.Value != declarationTvaEncaissement.No)
				{
					throw new ApplicationException("La depense ne partage pas la même déclaration que la ligne.");
				}
				break;
			}
			case LigneDeclarationTvaEncaissementEntityType.OperationBancaire:
			{
				OperationBancaire operationBancaire = Groupe.PrevisionnelManager.GetOperationBancaire(item.EntityNo);
				if (operationBancaire == null)
				{
					throw new ApplicationException("Impossible de charger l'opération bancaire.");
				}
				if (!operationBancaire.DeclarationTvaEncaissementNo.HasValue || operationBancaire.DeclarationTvaEncaissementNo.Value != declarationTvaEncaissement.No)
				{
					throw new ApplicationException("L'opération bancaire ne partage pas la même déclaration que la ligne.");
				}
				break;
			}
			}
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		foreach (LigneDeclarationTvaEncaissement item2 in list)
		{
			DepenseManager depenseManager = Groupe.DepenseManager;
			switch (item2.EntityType)
			{
			case LigneDeclarationTvaEncaissementEntityType.Affectation:
				if (!(CaisseManager.AffectationGet(item2.EntityNo) ?? throw new ApplicationException("Impossible de charger l'affectation.")).DeclarationTvaEncaissementNo.HasValue)
				{
					throw new ApplicationException("Impossible de charger la déclaration de l'affectation.");
				}
				CaisseManager.AffectationAnnulerDeclarationTva(item2.EntityNo);
				break;
			case LigneDeclarationTvaEncaissementEntityType.Depense:
				depenseManager.DepenseAnnulerDeclarationTva(item2.EntityNo);
				break;
			case LigneDeclarationTvaEncaissementEntityType.OperationBancaire:
				Groupe.PrevisionnelManager.OperationBancaireAnnulerDeclarationTva(item2.EntityNo);
				break;
			}
		}
		declarationTvaEncaissement.IsFichierGenerer = true;
		declarationTvaEncaissement.ModificateurNo = Utilisateur.No;
		declarationTvaEncaissement.DateModification = DateTime.Now;
		_declarationTvaEncaissementRepository.Update(declarationTvaEncaissement);
		_notifyService.Notify(TypeEntity.DeclarationTvaEncaissement, no, TypeAction.Modification, Societe.No);
		transactionScope.Complete();
	}

	public void DeclarationTvaEncaissementFichierAnnulerGeneration(int no)
	{
		DeclarationTvaEncaissement declarationTvaEncaissement = _declarationTvaEncaissementRepository.Get(no);
		if (declarationTvaEncaissement == null)
		{
			throw new ApplicationException("Impossible de charger la déclaration.");
		}
		if (!declarationTvaEncaissement.IsFichierGenerer)
		{
			throw new ApplicationException("Le fichier du déclaration n'est pas généré.");
		}
		if (declarationTvaEncaissement.IsDepose)
		{
			throw new ApplicationException("La déclaration est déposée.");
		}
		if (declarationTvaEncaissement.Statut != StatutDeclaration.Cloture)
		{
			throw new ApplicationException("La déclaration n'est pas clôturée.");
		}
		if (!_declarationTvaEncaissementRepository.HasLignes(no))
		{
			throw new ApplicationException("La déclaration ne contient aucune ligne.");
		}
		if ((from x in _ligneDeclarationTvaEncaissementRepository.GetAll(no)
			where x.IsReport
			select x).ToList().Any())
		{
			throw new ApplicationException("Opération invalide! Il existe des lignes reportées.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		declarationTvaEncaissement.IsFichierGenerer = false;
		declarationTvaEncaissement.ModificateurNo = Utilisateur.No;
		declarationTvaEncaissement.DateModification = DateTime.Now;
		_declarationTvaEncaissementRepository.Update(declarationTvaEncaissement);
		_notifyService.Notify(TypeEntity.DeclarationTvaEncaissement, no, TypeAction.Modification, Societe.No);
		transactionScope.Complete();
	}

	public void DeclarationRetenuSourceFichierGenerer(int no)
	{
		DeclarationRetenuSource declarationRetenuSource = _declarationRetenuSourceRepository.Get(no);
		if (declarationRetenuSource == null)
		{
			throw new ApplicationException("Impossible de charger la déclaration.");
		}
		if (declarationRetenuSource.IsFichierGenerer)
		{
			throw new ApplicationException("Le fichier du déclaration est déjà généré.");
		}
		if (declarationRetenuSource.IsDepose)
		{
			throw new ApplicationException("La déclaration est déposée.");
		}
		if (declarationRetenuSource.Statut != StatutDeclaration.Cloture)
		{
			throw new ApplicationException("La déclaration n'est pas clôturée.");
		}
		if (Societe.LegislationType == Legislation.Maroc)
		{
			if (!_declarationRetenuSourceRepository.HasLignes(no))
			{
				throw new ApplicationException("La déclaration ne contient aucune ligne.");
			}
		}
		else if (!_retenuALaSourceRepository.DeclarationTejHasLignes(no))
		{
			throw new ApplicationException("La déclaration ne contient aucune ligne.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		declarationRetenuSource.IsFichierGenerer = true;
		declarationRetenuSource.ModificateurNo = Utilisateur.No;
		declarationRetenuSource.DateModification = DateTime.Now;
		_declarationRetenuSourceRepository.Update(declarationRetenuSource);
		_notifyService.Notify(TypeEntity.DeclarationRetenuSource, no, TypeAction.Modification, Societe.No);
		transactionScope.Complete();
	}

	public void DeclarationRetenuSourceFichierAnnulerGeneration(int no)
	{
		DeclarationRetenuSource declarationRetenuSource = _declarationRetenuSourceRepository.Get(no);
		if (declarationRetenuSource == null)
		{
			throw new ApplicationException("Impossible de charger la déclaration.");
		}
		if (!declarationRetenuSource.IsFichierGenerer)
		{
			throw new ApplicationException("Le fichier du déclaration n'est pas généré.");
		}
		if (declarationRetenuSource.IsDepose)
		{
			throw new ApplicationException("La déclaration est déposée.");
		}
		if (declarationRetenuSource.Statut != StatutDeclaration.Cloture)
		{
			throw new ApplicationException("La déclaration n'est pas clôturée.");
		}
		if (Societe.LegislationType == Legislation.Maroc)
		{
			if (!_declarationRetenuSourceRepository.HasLignes(no))
			{
				throw new ApplicationException("La déclaration ne contient aucune ligne.");
			}
		}
		else if (!_retenuALaSourceRepository.DeclarationTejHasLignes(no))
		{
			throw new ApplicationException("La déclaration ne contient aucune ligne.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		declarationRetenuSource.IsFichierGenerer = false;
		declarationRetenuSource.ModificateurNo = Utilisateur.No;
		declarationRetenuSource.DateModification = DateTime.Now;
		_declarationRetenuSourceRepository.Update(declarationRetenuSource);
		_notifyService.Notify(TypeEntity.DeclarationRetenuSource, no, TypeAction.Modification, Societe.No);
		transactionScope.Complete();
	}

	public void DeclarationTvaEncaissementDelete(int no)
	{
		DeclarationTvaEncaissement declarationTvaEncaissement = _declarationTvaEncaissementRepository.Get(no);
		if (declarationTvaEncaissement == null)
		{
			throw new ApplicationException("Impossible de charger la déclaration.");
		}
		if (declarationTvaEncaissement.IsDepose)
		{
			throw new ApplicationException("La déclaration est déposée.");
		}
		if (declarationTvaEncaissement.Statut != StatutDeclaration.EnCours)
		{
			throw new ApplicationException("La déclaration est clôturée.");
		}
		if (_declarationTvaEncaissementRepository.HasLignes(no))
		{
			throw new ApplicationException("La déclaration contient des lignes.");
		}
		_declarationTvaEncaissementRepository.Delete(declarationTvaEncaissement);
		_notifyService.Notify(TypeEntity.DeclarationTvaEncaissement, no, TypeAction.Suppression, Societe.No);
	}

	public void DeclarationRetenuSourceDelete(int no)
	{
		DeclarationRetenuSource declarationRetenuSource = _declarationRetenuSourceRepository.Get(no);
		if (declarationRetenuSource == null)
		{
			throw new ApplicationException("Impossible de charger la déclaration.");
		}
		if (declarationRetenuSource.IsDepose)
		{
			throw new ApplicationException("La déclaration est déposée.");
		}
		if (declarationRetenuSource.Statut != StatutDeclaration.EnCours)
		{
			throw new ApplicationException("La déclaration est clôturée.");
		}
		if (Societe.LegislationType == Legislation.Maroc)
		{
			if (_declarationRetenuSourceRepository.HasLignes(no))
			{
				throw new ApplicationException("La déclaration contient des lignes.");
			}
		}
		else if (_retenuALaSourceRepository.DeclarationTejHasLignes(no))
		{
			throw new ApplicationException("La déclaration contient des lignes.");
		}
		_declarationRetenuSourceRepository.Delete(declarationRetenuSource);
		_notifyService.Notify(TypeEntity.DeclarationRetenuSource, no, TypeAction.Suppression, Societe.No);
	}

	public int DeclarationTvaEncaissementLigneAjouter(int declarationNo, int? entityNo, LigneDeclarationTvaEncaissementEntityType entityType, LigneDeclarationTvaEncaissementIntegrationType integration, LigneDeclarationTvaEncaissementDomaine domaine, decimal assiette, decimal taux, decimal montant, string tiersCode, string tiersIntitule, string tiersIdentifiant, string tiersIce, string numeroMouvement, string numeroDocument, ReglementType typePayement, DateTime dateMouvement, DateTime dateDocument, string erpCodeTaxe, string codeActivite, decimal prorata, string designationDocument)
	{
		if (declarationNo <= 0)
		{
			throw new ArgumentNullException("declarationNo");
		}
		if (integration == LigneDeclarationTvaEncaissementIntegrationType.Integration && (!entityNo.HasValue || entityNo <= 0))
		{
			throw new ArgumentNullException("entityNo");
		}
		if (assiette == 0m)
		{
			throw new ArgumentNullException("assiette");
		}
		if (taux < 0m || taux > 100m)
		{
			throw new ArgumentNullException("taux");
		}
		if (string.IsNullOrEmpty(tiersCode))
		{
			throw new ApplicationException("Code tiers invalide.");
		}
		if (string.IsNullOrEmpty(tiersIntitule))
		{
			throw new ApplicationException("Intitulé tiers invalide.");
		}
		if (string.IsNullOrEmpty(tiersIdentifiant))
		{
			throw new ApplicationException("Identifiant tiers invalide.[" + tiersCode + "]");
		}
		if (string.IsNullOrEmpty(tiersIce))
		{
			throw new ApplicationException("ICE tiers invalide.[" + tiersCode + "]");
		}
		if (string.IsNullOrEmpty(numeroMouvement))
		{
			throw new ApplicationException("Numéro mouvement invalide.");
		}
		if (string.IsNullOrEmpty(erpCodeTaxe))
		{
			throw new ApplicationException("Code taxe erp invalide.");
		}
		if (string.IsNullOrEmpty(numeroDocument))
		{
			throw new ApplicationException("Numéro document invalide.");
		}
		if (Societe.LegislationType == Legislation.Maroc)
		{
			if (tiersIdentifiant.Length != 8)
			{
				throw new ApplicationException("Longueur identifiant invalide. 8 caractères requis.");
			}
			if (tiersIce.Length != 15)
			{
				throw new ApplicationException("Longueur Ice invalide. 15 caractères requis.");
			}
			if (tiersIdentifiant.Contains(" "))
			{
				throw new ApplicationException("Identifiant, les espaces ne sont pas autorisés.");
			}
			if (tiersIce.Contains(" "))
			{
				throw new ApplicationException("Ice, les espaces ne sont pas autorisés.");
			}
		}
		if (prorata < 0m || prorata > 100m)
		{
			throw new ApplicationException("Prorata invalide.");
		}
		DepenseManager depenseManager = Groupe.DepenseManager;
		DeclarationTvaEncaissement obj = _declarationTvaEncaissementRepository.Get(declarationNo) ?? throw new ApplicationException("Impossible de charger la déclaration.");
		if (obj.IsDepose)
		{
			throw new ApplicationException("La déclaration est déposée.");
		}
		if (obj.Statut != StatutDeclaration.EnCours)
		{
			throw new ApplicationException("La déclaration est clôturée.");
		}
		switch (integration)
		{
		case LigneDeclarationTvaEncaissementIntegrationType.Integration:
			switch (entityType)
			{
			case LigneDeclarationTvaEncaissementEntityType.Affectation:
			{
				Affectation affectation = CaisseManager.AffectationGet(entityNo.Value);
				if (affectation == null)
				{
					throw new ApplicationException("Impossible de charger l'affectation.");
				}
				switch (affectation.GetEcheance().Domaine)
				{
				case ErpDomaine.Vente:
				{
					ReglementClient reglementClient = CaisseManager.ReglementGet(affectation.ReglementNo);
					if (reglementClient == null)
					{
						throw new ApplicationException("Impossible de charger le règlement");
					}
					if ((reglementClient.Type == ReglementType.Cheque || reglementClient.Type == ReglementType.Traite || reglementClient.Type == ReglementType.Virement) && !reglementClient.IsPointe)
					{
						throw new ApplicationException("Le règlement [" + reglementClient.Numero + "] n'est pas pointé.");
					}
					if (reglementClient.IsComptabilise != EtatComptabilite.Comptabilise)
					{
						throw new ApplicationException("Le règlement [" + reglementClient.Numero + "] n'est pas comptabilisé.");
					}
					break;
				}
				case ErpDomaine.Achat:
				{
					ReglementFournisseur reglementFournisseur = CaisseManager.ReglementFournisseurGet(affectation.ReglementNo);
					if (reglementFournisseur == null)
					{
						throw new ApplicationException("Impossible de charger le règlement");
					}
					if ((reglementFournisseur.Type == ReglementType.Cheque || reglementFournisseur.Type == ReglementType.Traite || reglementFournisseur.Type == ReglementType.Virement) && !reglementFournisseur.IsPointe)
					{
						throw new ApplicationException("Le règlement [" + reglementFournisseur.Numero + "] n'est pas pointé.");
					}
					if (reglementFournisseur.IsComptabilise != EtatComptabilite.Comptabilise && reglementFournisseur.IsComptabilise != EtatComptabilite.TraiteFournisseurComptabilise)
					{
						throw new ApplicationException("Le règlement [" + reglementFournisseur.Numero + "] n'est pas comptabilisé.");
					}
					break;
				}
				}
				break;
			}
			case LigneDeclarationTvaEncaissementEntityType.Depense:
			{
				MouvementDepense mouvementDepense = Groupe.DepenseManager.GetMouvementDepense(entityNo.Value);
				if (mouvementDepense == null)
				{
					throw new ApplicationException("Impossible de charger la dépense");
				}
				if (mouvementDepense.IsComptabilise != EtatComptabilite.Comptabilise)
				{
					throw new ApplicationException("La dépense [" + mouvementDepense.Numero + "] n'est pas comptabilisée.");
				}
				if (!mouvementDepense.WithTva)
				{
					throw new ApplicationException("La dépense [" + mouvementDepense.Numero + "] ne contient pas une TVA.");
				}
				break;
			}
			case LigneDeclarationTvaEncaissementEntityType.OperationBancaire:
				if ((Groupe.PrevisionnelManager.GetOperationBancaire(entityNo.Value) ?? throw new ApplicationException("Impossible de charger l'opération bancaire.")).Sens == SensPrevisionnelle.Encaissement)
				{
					domaine = LigneDeclarationTvaEncaissementDomaine.Encaissement;
				}
				break;
			}
			break;
		default:
			throw new NotImplementedException("Type intégration invalide.");
		case LigneDeclarationTvaEncaissementIntegrationType.SaisieManuelle:
		case LigneDeclarationTvaEncaissementIntegrationType.Importation:
			break;
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		LigneDeclarationTvaEncaissement ligne = new LigneDeclarationTvaEncaissement
		{
			DeclarationNo = declarationNo,
			EntityNo = (entityNo.HasValue ? entityNo.Value : 0),
			EntityType = entityType,
			MouvementNumero = numeroMouvement,
			DocumentNumero = numeroDocument,
			Assiette = assiette,
			Taux = taux,
			Montant = montant,
			Domaine = domaine,
			DateDocument = dateDocument,
			DateMouvement = dateMouvement,
			TiersCode = tiersCode,
			TiersIntitule = tiersIntitule,
			TiersIdentifiant = tiersIdentifiant,
			TypePayement = typePayement,
			TypeIntegration = integration,
			TiersIce = tiersIce,
			ErpTaxeCode = erpCodeTaxe,
			CodeActivite = codeActivite,
			Prorata = prorata,
			DesignationDocument = designationDocument
		};
		int result = _ligneDeclarationTvaEncaissementRepository.Create(ligne);
		if (integration == LigneDeclarationTvaEncaissementIntegrationType.Integration)
		{
			switch (entityType)
			{
			case LigneDeclarationTvaEncaissementEntityType.Affectation:
			{
				Affectation affectation2 = CaisseManager.AffectationGet(entityNo.Value);
				if (affectation2 == null)
				{
					throw new ApplicationException("Impossible de charger l'affectation.");
				}
				if (!affectation2.DeclarationTvaEncaissementNo.HasValue || affectation2.DeclarationTvaEncaissementNo.Value != declarationNo)
				{
					CaisseManager.AffectationSetDeclarationTva(entityNo.Value, declarationNo);
				}
				break;
			}
			case LigneDeclarationTvaEncaissementEntityType.Depense:
				depenseManager.DepenseSetDeclarationTva(entityNo.Value, declarationNo);
				break;
			case LigneDeclarationTvaEncaissementEntityType.OperationBancaire:
				Groupe.PrevisionnelManager.OperationBancaireSetDeclarationTva(entityNo.Value, declarationNo);
				break;
			}
		}
		_notifyService.Notify(TypeEntity.DeclarationTvaEncaissement, declarationNo, TypeAction.Modification, Societe.No);
		transactionScope.Complete();
		return result;
	}

	public void DeclarationRetenuSourceTejLigneAjouter(int declarationNo, List<int> retenuSourceNos)
	{
		if (declarationNo <= 0)
		{
			throw new ArgumentNullException("declarationNo");
		}
		if (retenuSourceNos == null)
		{
			throw new ArgumentNullException("retenuSourceNos");
		}
		DeclarationRetenuSource declarationRetenuSource = _declarationRetenuSourceRepository.Get(declarationNo);
		if (declarationRetenuSource == null)
		{
			throw new ApplicationException("Impossible de charger la déclaration.");
		}
		if (!retenuSourceNos.Any())
		{
			throw new ApplicationException("Impossible de charger la liste des retenues à la source");
		}
		if (declarationRetenuSource.IsDepose)
		{
			throw new ApplicationException("La déclaration est déposée.");
		}
		if (declarationRetenuSource.Statut != StatutDeclaration.EnCours)
		{
			throw new ApplicationException("La déclaration est clôturée.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		foreach (int retenuSourceNo in retenuSourceNos)
		{
			RetenuALaSource retenuALaSource = _retenuALaSourceRepository.Get(retenuSourceNo);
			if (retenuALaSource == null)
			{
				throw new ApplicationException($"Impossible de charger la retenu de la source n° {retenuSourceNo}");
			}
			if (retenuALaSource.IsDeclarer)
			{
				throw new ApplicationException("la retenue à la source n° " + retenuALaSource.Numero + " est deja déclarer");
			}
			_retenuALaSourceRepository.SetAsDeclarerTej(retenuSourceNo, declarationNo, declarationRetenuSource.Numero, Utilisateur.No);
			_notifyService.Notify(TypeEntity.ReglementFournisseur, retenuSourceNo, TypeAction.Modification, Societe.No);
		}
		_notifyService.Notify(TypeEntity.DeclarationRetenuSource, declarationNo, TypeAction.Modification, Societe.No);
		transactionScope.Complete();
	}

	public void DeclarationRetenuSourceLigneAjouter(int declarationNo, int retenueNo, int mouvementNo, decimal montant)
	{
		if (declarationNo <= 0)
		{
			throw new ArgumentNullException("declarationNo");
		}
		if (retenueNo <= 0)
		{
			throw new ArgumentNullException("retenueNo");
		}
		if (mouvementNo <= 0)
		{
			throw new ArgumentNullException("mouvementNo");
		}
		if (montant <= 0m)
		{
			throw new ArgumentNullException("montant");
		}
		DeclarationRetenuSource obj = _declarationRetenuSourceRepository.Get(declarationNo) ?? throw new ApplicationException("Impossible de charger la déclaration.");
		if (obj.IsDepose)
		{
			throw new ApplicationException("La déclaration est déposée.");
		}
		if (obj.Statut != StatutDeclaration.EnCours)
		{
			throw new ApplicationException("La déclaration est clôturée.");
		}
		if (_retenuALaSourceRepository.Get(retenueNo) == null)
		{
			throw new ApplicationException("Impossible de charger la retenue.");
		}
		if (_reglementFournisseurRepository.Get(mouvementNo) == null)
		{
			throw new ApplicationException("Impossible de charger le règlement.");
		}
		if (_ligneDeclarationRetenuSourceRepository.Get(retenueNo, mouvementNo) != null)
		{
			throw new ApplicationException("Ligne existe déja.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		_ligneDeclarationRetenuSourceRepository.Create(new LigneDeclarationRetenuSource
		{
			DeclarationNo = declarationNo,
			Montant = montant,
			MouvementNo = mouvementNo,
			RetenueNo = retenueNo
		});
		_notifyService.Notify(TypeEntity.DeclarationRetenuSource, declarationNo, TypeAction.Modification, Societe.No);
		transactionScope.Complete();
	}

	public IEnumerable<LigneDeclarationTvaEncaissement> DeclarationTvaEncaissementLigneGetAll(int declarationNo)
	{
		return _ligneDeclarationTvaEncaissementRepository.GetAll(declarationNo);
	}

	public IEnumerable<LigneDeclarationRetenuSource> DeclarationRetenuSourceLigneGetAll(int declarationNo)
	{
		return _ligneDeclarationRetenuSourceRepository.GetAll(declarationNo);
	}

	public IEnumerable<RetenuALaSource> RetenuSourceGetAllByPeriode(int societeNo, DateTime dateDebut, DateTime dateFin)
	{
		return _retenuALaSourceRepository.GetByPeriode(societeNo, dateDebut, dateFin);
	}

	public IEnumerable<RetenuALaSource> RetenueSourceTejGetAllByDeclaration(int declarationNo)
	{
		return _retenuALaSourceRepository.GetByDeclarationTej(declarationNo);
	}

	public void DeclarationTvaEncaissementDeleteLigne(int no)
	{
		LigneDeclarationTvaEncaissement ligne = _ligneDeclarationTvaEncaissementRepository.Get(no);
		if (ligne == null)
		{
			throw new ApplicationException("Impossible de charger la ligne.");
		}
		DeclarationTvaEncaissement declarationTvaEncaissement = _declarationTvaEncaissementRepository.Get(ligne.DeclarationNo);
		if (declarationTvaEncaissement == null)
		{
			throw new ApplicationException("Impossible de charger la déclaration.");
		}
		if (declarationTvaEncaissement.IsDepose)
		{
			throw new ApplicationException("La déclaration est déposée.");
		}
		if (declarationTvaEncaissement.Statut != StatutDeclaration.EnCours)
		{
			throw new ApplicationException("La déclaration est clôturée.");
		}
		switch (ligne.EntityType)
		{
		case LigneDeclarationTvaEncaissementEntityType.Affectation:
		{
			Affectation affectation = CaisseManager.AffectationGet(ligne.EntityNo);
			if (affectation == null)
			{
				throw new ApplicationException("Impossible de charger l'affectation.");
			}
			if (!affectation.DeclarationTvaEncaissementNo.HasValue || affectation.DeclarationTvaEncaissementNo.Value != declarationTvaEncaissement.No)
			{
				throw new ApplicationException("L'affectation ne partage pas la même déclaration que la ligne.");
			}
			break;
		}
		case LigneDeclarationTvaEncaissementEntityType.Depense:
		{
			MouvementDepense mouvementDepense = Groupe.DepenseManager.GetMouvementDepense(ligne.EntityNo);
			if (mouvementDepense == null)
			{
				throw new ApplicationException("Impossible de charger la dépense");
			}
			if (!mouvementDepense.DeclarationTvaEncaissementNo.HasValue || mouvementDepense.DeclarationTvaEncaissementNo.Value != declarationTvaEncaissement.No)
			{
				throw new ApplicationException("La depense ne partage pas la même déclaration que la ligne.");
			}
			break;
		}
		case LigneDeclarationTvaEncaissementEntityType.OperationBancaire:
		{
			OperationBancaire operationBancaire = Groupe.PrevisionnelManager.GetOperationBancaire(ligne.EntityNo);
			if (operationBancaire == null)
			{
				throw new ApplicationException("Impossible de charger l'opération bancaire.");
			}
			if (!operationBancaire.DeclarationTvaEncaissementNo.HasValue || operationBancaire.DeclarationTvaEncaissementNo.Value != declarationTvaEncaissement.No)
			{
				throw new ApplicationException("L'opération bancaire ne partage pas la même déclaration que la ligne.");
			}
			break;
		}
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		DepenseManager depenseManager = Groupe.DepenseManager;
		switch (ligne.EntityType)
		{
		case LigneDeclarationTvaEncaissementEntityType.Affectation:
			if (!(CaisseManager.AffectationGet(ligne.EntityNo) ?? throw new ApplicationException("Impossible de charger l'affectation.")).DeclarationTvaEncaissementNo.HasValue)
			{
				throw new ApplicationException("Impossible de charger la déclaration de l'affectation.");
			}
			if (_ligneDeclarationTvaEncaissementRepository.GetAll(ligne.EntityType, ligne.EntityNo).All((LigneDeclarationTvaEncaissement x) => x.No == ligne.No))
			{
				CaisseManager.AffectationAnnulerDeclarationTva(ligne.EntityNo);
			}
			break;
		case LigneDeclarationTvaEncaissementEntityType.Depense:
			depenseManager.DepenseAnnulerDeclarationTva(ligne.EntityNo);
			break;
		case LigneDeclarationTvaEncaissementEntityType.OperationBancaire:
			Groupe.PrevisionnelManager.OperationBancaireAnnulerDeclarationTva(ligne.EntityNo);
			break;
		}
		_ligneDeclarationTvaEncaissementRepository.Delete(ligne);
		_notifyService.Notify(TypeEntity.DeclarationTvaEncaissement, declarationTvaEncaissement.No, TypeAction.Modification, Societe.No);
		transactionScope.Complete();
	}

	public void DeclarationRetenuSourceTejDeleteLigne(int retenuSourceNo)
	{
		RetenuALaSource retenuALaSource = _retenuALaSourceRepository.Get(retenuSourceNo);
		if (retenuALaSource == null)
		{
			throw new ApplicationException("Impossible de charger la ligne.");
		}
		DeclarationRetenuSource declarationRetenuSource = _declarationRetenuSourceRepository.Get(retenuALaSource.DeclarationNo.Value);
		if (declarationRetenuSource == null)
		{
			throw new ApplicationException("Impossible de charger la déclaration.");
		}
		if (declarationRetenuSource.IsDepose)
		{
			throw new ApplicationException("La déclaration est déposée.");
		}
		if (declarationRetenuSource.Statut != StatutDeclaration.EnCours)
		{
			throw new ApplicationException("La déclaration est clôturée.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		retenuALaSource.DeclarationNo = null;
		retenuALaSource.DeclarationNum = "";
		retenuALaSource.ModificateurNo = Utilisateur.No;
		_retenuALaSourceRepository.DeleteDeclarationTej(retenuALaSource.No, Utilisateur.No);
		_notifyService.Notify(TypeEntity.DeclarationRetenuSource, declarationRetenuSource.No, TypeAction.Modification, Societe.No);
		_notifyService.Notify(TypeEntity.ReglementFournisseur, retenuALaSource.No, TypeAction.Modification, Societe.No);
		transactionScope.Complete();
	}

	public void DeclarationRetenuSourceDeleteLigne(int no)
	{
		LigneDeclarationRetenuSource ligneDeclarationRetenuSource = _ligneDeclarationRetenuSourceRepository.Get(no);
		if (ligneDeclarationRetenuSource == null)
		{
			throw new ApplicationException("Impossible de charger la ligne.");
		}
		DeclarationRetenuSource declarationRetenuSource = _declarationRetenuSourceRepository.Get(ligneDeclarationRetenuSource.DeclarationNo);
		if (declarationRetenuSource == null)
		{
			throw new ApplicationException("Impossible de charger la déclaration.");
		}
		if (declarationRetenuSource.IsDepose)
		{
			throw new ApplicationException("La déclaration est déposée.");
		}
		if (declarationRetenuSource.Statut != StatutDeclaration.EnCours)
		{
			throw new ApplicationException("La déclaration est clôturée.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		_ligneDeclarationRetenuSourceRepository.Delete(ligneDeclarationRetenuSource);
		_notifyService.Notify(TypeEntity.DeclarationRetenuSource, declarationRetenuSource.No, TypeAction.Modification, Societe.No);
		transactionScope.Complete();
	}

	public void DeclarationTvaEncaissementReporterLigne(int no)
	{
		LigneDeclarationTvaEncaissement ligneDeclarationTvaEncaissement = _ligneDeclarationTvaEncaissementRepository.Get(no);
		if (ligneDeclarationTvaEncaissement == null)
		{
			throw new ApplicationException("Impossible de charger la ligne.");
		}
		if (ligneDeclarationTvaEncaissement.IsReport)
		{
			throw new ApplicationException("La ligne est déjà reportée.");
		}
		DeclarationTvaEncaissement declarationTvaEncaissement = _declarationTvaEncaissementRepository.Get(ligneDeclarationTvaEncaissement.DeclarationNo);
		if (declarationTvaEncaissement == null)
		{
			throw new ApplicationException("Impossible de charger la déclaration.");
		}
		if (declarationTvaEncaissement.IsDepose)
		{
			throw new ApplicationException("La déclaration est déposée.");
		}
		if (declarationTvaEncaissement.IsFichierGenerer)
		{
			throw new ApplicationException("Le fichier de la déclaration est déjà génére.");
		}
		if (declarationTvaEncaissement.Statut != StatutDeclaration.EnCours)
		{
			throw new ApplicationException("La déclaration est clôturée.");
		}
		switch (ligneDeclarationTvaEncaissement.EntityType)
		{
		case LigneDeclarationTvaEncaissementEntityType.Affectation:
		{
			Affectation affectation = CaisseManager.AffectationGet(ligneDeclarationTvaEncaissement.EntityNo);
			if (affectation == null)
			{
				throw new ApplicationException("Impossible de charger l'affectation.");
			}
			if (!affectation.DeclarationTvaEncaissementNo.HasValue || affectation.DeclarationTvaEncaissementNo.Value != declarationTvaEncaissement.No)
			{
				throw new ApplicationException("L'affectation ne partage pas la même déclaration que la ligne.");
			}
			break;
		}
		case LigneDeclarationTvaEncaissementEntityType.Depense:
		{
			MouvementDepense mouvementDepense = Groupe.DepenseManager.GetMouvementDepense(ligneDeclarationTvaEncaissement.EntityNo);
			if (mouvementDepense == null)
			{
				throw new ApplicationException("Impossible de charger la dépense");
			}
			if (!mouvementDepense.DeclarationTvaEncaissementNo.HasValue || mouvementDepense.DeclarationTvaEncaissementNo.Value != declarationTvaEncaissement.No)
			{
				throw new ApplicationException("La depense ne partage pas la même déclaration que la ligne.");
			}
			break;
		}
		case LigneDeclarationTvaEncaissementEntityType.OperationBancaire:
		{
			OperationBancaire operationBancaire = Groupe.PrevisionnelManager.GetOperationBancaire(ligneDeclarationTvaEncaissement.EntityNo);
			if (operationBancaire == null)
			{
				throw new ApplicationException("Impossible de charger l'opération bancaire.");
			}
			if (!operationBancaire.DeclarationTvaEncaissementNo.HasValue || operationBancaire.DeclarationTvaEncaissementNo.Value != declarationTvaEncaissement.No)
			{
				throw new ApplicationException("L'opération bancaire ne partage pas la même déclaration que la ligne.");
			}
			break;
		}
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		ligneDeclarationTvaEncaissement.IsReport = true;
		_ligneDeclarationTvaEncaissementRepository.Update(ligneDeclarationTvaEncaissement);
		_notifyService.Notify(TypeEntity.DeclarationTvaEncaissement, declarationTvaEncaissement.No, TypeAction.Modification, Societe.No);
		transactionScope.Complete();
	}

	public Task<IEnumerable<LettreRecouvrementFields>> LettreRecouvrementFieldGetAllAsync(Societe societe = null)
	{
		societe = societe ?? Societe;
		return _lettreRecouvrementFieldsRepository.GetAllAsync(societe.No);
	}

	public async Task SetLettreRecouvrementField(LettreRecouvrementFieldValue fieldValue, bool isGenere, int order)
	{
		LettreRecouvrementFields lettreRecouvrementFields = await _lettreRecouvrementFieldsRepository.GetAsync(Societe.No, fieldValue);
		if (lettreRecouvrementFields != null)
		{
			lettreRecouvrementFields.IsGenere = isGenere;
			lettreRecouvrementFields.Rang = order;
			await _lettreRecouvrementFieldsRepository.UpdateAsync(lettreRecouvrementFields);
			return;
		}
		lettreRecouvrementFields = new LettreRecouvrementFields
		{
			SocieteNo = Societe.No,
			Field = fieldValue,
			IsGenere = isGenere,
			Rang = order
		};
		await _lettreRecouvrementFieldsRepository.CreateAsync(lettreRecouvrementFields);
	}

	public List<SocieteCodeActiviteTaxe> CodeActiviteTaxeGetAll(Societe societe = null)
	{
		societe = societe ?? Societe;
		return _societeCodeActiviteTaxeRepository.GetAll(societe.No);
	}

	public SocieteCodeActiviteTaxe CodeActiviteTaxeGet(string erpTaxeCode, Societe societe = null)
	{
		societe = societe ?? Societe;
		return _societeCodeActiviteTaxeRepository.Get(societe.No, erpTaxeCode);
	}

	public void CodeActiviteTaxeCreate(int codeActiviteNo, string erpTaxeCode)
	{
		if (Groupe.CodeActiviteManager.Get(codeActiviteNo) == null)
		{
			throw new ApplicationException("Impossible de charger le code activité.");
		}
		if (string.IsNullOrEmpty(erpTaxeCode))
		{
			throw new ApplicationException("Code taxe invalide.");
		}
		if (_societeCodeActiviteTaxeRepository.IsTaxeMapped(Societe.No, erpTaxeCode))
		{
			throw new ApplicationException("Le code taxe [" + erpTaxeCode + "] est déja utilisé.");
		}
		SocieteCodeActiviteTaxe societeCodeActiviteTaxe = new SocieteCodeActiviteTaxe
		{
			CodeActiviteNo = codeActiviteNo,
			ErpCodeTaxe = erpTaxeCode,
			SocieteNo = Societe.No
		};
		_societeCodeActiviteTaxeRepository.Create(societeCodeActiviteTaxe);
		Societe.RefreshSocCodeActivite();
	}

	public bool CodeActiviteTaxeIsMapped(string erpTaxe)
	{
		return _societeCodeActiviteTaxeRepository.IsTaxeMapped(Societe.No, erpTaxe);
	}

	public void CodeActiviteTaxeDelete(int no)
	{
		SocieteCodeActiviteTaxe societeCodeActiviteTaxe = _societeCodeActiviteTaxeRepository.Get(no);
		if (societeCodeActiviteTaxe == null)
		{
			throw new ApplicationException("Impossible de charger le mappage taxe-code activité.");
		}
		_societeCodeActiviteTaxeRepository.Delete(societeCodeActiviteTaxe);
		Societe.RefreshSocCodeActivite();
	}

	public IList<TiersServiceContact> GetAllTiersServiceContact()
	{
		return _tiersServiceContactRepository.GetAll(Societe.No);
	}

	public TiersServiceContact GetTiersServiceContactByErpNo(int erpNo)
	{
		return _tiersServiceContactRepository.GetByErpNo(Societe.No, erpNo);
	}

	public int CreateTiersServiceContact(TiersServiceContact tiersServiceContact)
	{
		if (tiersServiceContact == null)
		{
			throw new ArgumentNullException("tiersServiceContact");
		}
		tiersServiceContact.SocieteNo = Societe.No;
		return _tiersServiceContactRepository.Create(tiersServiceContact);
	}

	public void UpdateTiersServiceContactNotify(int no, bool isNotify)
	{
		if (no == 0)
		{
			throw new Exception("Impossible de charger le tiers service contact");
		}
		_tiersServiceContactRepository.UpdateNotify(no, isNotify);
	}

	public void AffecterCreditEcheance(int echeanceNo, int creditNo, string creditNum)
	{
		Echeance echeance = EcheanceGet(echeanceNo) ?? throw new ApplicationException($"Impossible de charger le document [{echeanceNo}]");
		echeance.CreditNo = creditNo;
		echeance.CreditNum = creditNum;
		EcheanceUpdate(echeance);
	}

	public void DeleteAffecterCreditEcheance(int echeanceNo)
	{
		Echeance echeance = EcheanceGet(echeanceNo) ?? throw new ApplicationException($"Impossible de charger le document [{echeanceNo}]");
		echeance.CreditNo = null;
		echeance.CreditNum = string.Empty;
		EcheanceUpdate(echeance);
	}

	public void AjouterPieceJointeEcheance(int echeanceNo, string fileName, byte[] filePdf)
	{
		Echeance echeance = _echeanceRepository.Get(echeanceNo) ?? throw new ApplicationException($"Impossible de charger le document [{echeanceNo}]");
		if (!string.IsNullOrEmpty(echeance.FileName))
		{
			throw new ApplicationException("Le document [" + echeance.DocumentNumero + "] contient une pièce jointe");
		}
		if (string.IsNullOrEmpty(fileName))
		{
			throw new ApplicationException("Nom de fichier invalide");
		}
		if (filePdf == null)
		{
			throw new ApplicationException("Fichier invalide");
		}
		_echeanceRepository.UpdatePieceJointe(echeanceNo, fileName, filePdf);
		_notifyService.Notify(TypeEntity.Echeance, echeanceNo, TypeAction.Modification, Societe.No);
	}

	public void SupprimerPieceJointeEcheance(int echeanceNo)
	{
		Echeance echeance = _echeanceRepository.Get(echeanceNo) ?? throw new ApplicationException($"Impossible de charger le document [{echeanceNo}].");
		if (string.IsNullOrEmpty(echeance.FileName))
		{
			throw new ApplicationException("Le document [" + echeance.DocumentNumero + "] ne contient aucune pièce jointe.");
		}
		_echeanceRepository.UpdatePieceJointe(echeanceNo, string.Empty, null);
		_notifyService.Notify(TypeEntity.Echeance, echeanceNo, TypeAction.Modification, Societe.No);
	}
}
