using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Serilog;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Interfaces;
using Tresorerie.Core.Models;
using Tresorerie.Infrastructure;
using Tresorerie.Infrastructure.Helpers;
using Tresorerie.Ressources;

namespace Tresorerie.Core.Services;

public class CaisseManager
{
	private readonly IAffectationRepository _affectationRepository;

	private readonly ReglementClientRemplaceFactory _factory;

	private readonly IAutorisationCaisseRepository _autorisationCaisseRepository;

	private readonly IBordereauRepository _bordereauRepository;

	private readonly IBordereauVirementRepository _bordereauVirementRepository;

	private readonly ICaisseModeReglementRepository _caisseModeReglementRepository;

	private readonly ICaisseRepository _caisseRepository;

	private readonly ICaisseCaisseRepository _caisseCaisseRepository;

	private readonly ICaisseBanqueRepository _caisseBanqueRepository;

	private readonly IChequeRepository _chequeRepository;

	private readonly IDossierReglementRepository _dossierReglementRepository;

	private readonly IEcartEchangeRepository _ecartEchangeRepository;

	private readonly ICautionRepository _cautionRepository;

	private readonly IEcartRepository _ecartRepository;

	private readonly IEcheanceRepository _echeanceRepository;

	private readonly IEcritureComptaRepository _ecritureComptaRepository;

	private readonly IHistoriqueMvtRepository _historiqueRepository;

	private readonly IImpayeFournisseurRepository _impayeFournisseurRepository;

	private readonly IImpayeRepository _impayeRepository;

	private readonly ILettrageAffectationRepository _lettrageAffectationRepository;

	private readonly ILigneBordereauRepository _ligneBordereauRepository;

	private readonly ILigneBordereauVirementRepository _ligneBordereauVirementRepository;

	private readonly ILigneVirementTiersRepository _ligneVirementTiersRepository;

	private readonly IMouvementCaisseEspeceRepository _mouvementCaisseEspeceRepository;

	private readonly IMouvementCaisseRepository _mouvementCaisseRepository;

	private readonly IMouvementRepository _mouvementRepository;

	private readonly INoteRepository _noteRepository;

	private readonly NotifyService _notifyService;

	private readonly IReglementClientRepository _reglementClientRepository;

	private readonly IReglementClientRemplaceRepository _reglementClientRemplaceRepository;

	private readonly IReglementFournisseurRepository _reglementFournisseurRepository;

	private readonly IRemboursementClientAllTypeRepository _remboursementClientAllTypeRepository;

	private readonly IRemboursementClientRepository _remboursementClientRepository;

	private readonly IRemplacementRepository _remplacementRepository;

	private readonly IRetenuALaSourceRepository _retenuALaSourceRepository;

	private readonly IVirementInterneRepository _virementInterneRepository;

	private readonly IVirementTiersRepository _virementTiersRepository;

	private readonly IViewTiersRepository _viewTiersRepository;

	private readonly IRemboursementFournisseurRepository _remboursementFournisseurRepository;

	private readonly IDetailAffectationRepository _detailAffectationRepository;

	private readonly ICaisseAuthorisation _caisseAuthorisationRepository;

	private readonly IReglementCoffreRepository _reglementCoffreRepository;

	private readonly ILicenceApplicationVersion _licenceApplicationVersion;

	private readonly ITraiteRepository _traiteRepository;

	public SocieteManager SocieteManager { get; set; }

	public CaisseManager(ICaisseRepository caisseRepository, ICaisseCaisseRepository caisseCaisseRepository, ICaisseBanqueRepository caisseBanqueRepository, ICaisseModeReglementRepository caisseModeReglementRepository, IReglementClientRepository reglementClientRepository, IReglementClientRemplaceRepository reglementClientRemplaceRepository, IEcheanceRepository echeanceRepository, IAffectationRepository affectationRepository, IHistoriqueMvtRepository historiqueRepository, IBordereauRepository bordereauRepository, ILigneBordereauRepository ligneBordereau, IRemplacementRepository remplacementRepository, IEcritureComptaRepository ecritureComptaRepository, NotifyService notifyService, IMouvementCaisseRepository mouvementCaisseRepository, IReglementFournisseurRepository reglementFournisseurRepository, IImpayeRepository impayeRepository, INoteRepository noteRepository, IAutorisationCaisseRepository autorisationCaisseRepository, IRetenuALaSourceRepository retenuALaSourceRepository, IDossierReglementRepository dossierReglementRepository, IChequeRepository chequeRepository, IMouvementCaisseEspeceRepository mouvementCaisseEspeceRepository, IMouvementRepository mouvementRepository, IEcartRepository ecartRepository, IEcartEchangeRepository ecartEchangeRepository, IVirementInterneRepository viremenInterneRepository, IVirementTiersRepository virementTiersRepository, ILigneVirementTiersRepository ligneVirementTiersRepository, IRemboursementClientRepository remboursementClientRepository, IRemboursementClientAllTypeRepository remboursementClientAllTypeRepository, ILettrageAffectationRepository lettrageAffectationRepository, IImpayeFournisseurRepository impayeFournisseurRepository, IViewTiersRepository delaiRegTiersRepository, IRemboursementFournisseurRepository remboursementFournisseurRepository, ICautionRepository cautionRepository, ICaisseAuthorisation caisseAuthorisationRepository, IDetailAffectationRepository detailAffectationRepository, IReglementCoffreRepository reglementCoffreRepository, ILicenceApplicationVersion licenceApplicationVersion, ITraiteRepository traiteRepository, IBordereauVirementRepository bordereauVirementRepository, ILigneBordereauVirementRepository ligneBordereauVirementRepository)
	{
		_caisseRepository = caisseRepository ?? throw new ArgumentNullException("caisseRepository");
		_caisseCaisseRepository = caisseCaisseRepository ?? throw new ArgumentNullException("caisseCaisseRepository");
		_caisseBanqueRepository = caisseBanqueRepository ?? throw new ArgumentNullException("caisseBanqueRepository");
		_bordereauRepository = bordereauRepository ?? throw new ArgumentNullException("bordereauRepository");
		_ligneBordereauRepository = ligneBordereau ?? throw new ArgumentNullException("ligneBordereau");
		_remplacementRepository = remplacementRepository ?? throw new ArgumentNullException("remplacementRepository");
		_affectationRepository = affectationRepository ?? throw new ArgumentNullException("affectationRepository");
		_caisseModeReglementRepository = caisseModeReglementRepository ?? throw new ArgumentNullException("caisseModeReglementRepository");
		_reglementClientRepository = reglementClientRepository ?? throw new ArgumentNullException("reglementClientRepository");
		_reglementClientRemplaceRepository = reglementClientRemplaceRepository ?? throw new ArgumentNullException("reglementClientRemplaceRepository");
		_echeanceRepository = echeanceRepository ?? throw new ArgumentNullException("echeanceRepository");
		_historiqueRepository = historiqueRepository ?? throw new ArgumentNullException("historiqueRepository");
		_notifyService = notifyService ?? throw new ArgumentNullException("notifyService");
		_mouvementCaisseRepository = mouvementCaisseRepository ?? throw new ArgumentNullException("mouvementCaisseRepository");
		_impayeRepository = impayeRepository ?? throw new ArgumentNullException("impayeRepository");
		_ecritureComptaRepository = ecritureComptaRepository ?? throw new ArgumentNullException("ecritureComptaRepository");
		_reglementFournisseurRepository = reglementFournisseurRepository ?? throw new ArgumentNullException("reglementFournisseurRepository");
		_noteRepository = noteRepository ?? throw new ArgumentNullException("noteRepository");
		_autorisationCaisseRepository = autorisationCaisseRepository ?? throw new ArgumentNullException("autorisationCaisseRepository");
		_retenuALaSourceRepository = retenuALaSourceRepository ?? throw new ArgumentNullException("retenuALaSourceRepository");
		_dossierReglementRepository = dossierReglementRepository ?? throw new ArgumentNullException("dossierReglementRepository");
		_chequeRepository = chequeRepository ?? throw new ArgumentNullException("chequeRepository");
		_mouvementCaisseEspeceRepository = mouvementCaisseEspeceRepository ?? throw new ArgumentNullException("mouvementCaisseEspeceRepository");
		_mouvementRepository = mouvementRepository ?? throw new ArgumentNullException("mouvementRepository");
		_ecartRepository = ecartRepository ?? throw new ArgumentNullException("ecartRepository");
		_ecartEchangeRepository = ecartEchangeRepository ?? throw new ArgumentNullException("ecartEchangeRepository");
		_virementInterneRepository = viremenInterneRepository ?? throw new ArgumentNullException("viremenInterneRepository");
		_virementTiersRepository = virementTiersRepository ?? throw new ArgumentNullException("virementTiersRepository");
		_ligneVirementTiersRepository = ligneVirementTiersRepository ?? throw new ArgumentNullException("ligneVirementTiersRepository");
		_remboursementClientRepository = remboursementClientRepository ?? throw new ArgumentNullException("remboursementClientRepository");
		_remboursementClientAllTypeRepository = remboursementClientAllTypeRepository ?? throw new ArgumentNullException("remboursementClientAllTypeRepository");
		_lettrageAffectationRepository = lettrageAffectationRepository ?? throw new ArgumentNullException("lettrageAffectationRepository");
		_impayeFournisseurRepository = impayeFournisseurRepository ?? throw new ArgumentNullException("impayeFournisseurRepository");
		_viewTiersRepository = delaiRegTiersRepository ?? throw new ArgumentNullException("delaiRegTiersRepository");
		_remboursementFournisseurRepository = remboursementFournisseurRepository ?? throw new ArgumentNullException("remboursementFournisseurRepository");
		_cautionRepository = cautionRepository ?? throw new ArgumentNullException("cautionRepository");
		_factory = new ReglementClientRemplaceFactory();
		_detailAffectationRepository = detailAffectationRepository ?? throw new NullReferenceException("detailAffectationRepository");
		_caisseAuthorisationRepository = caisseAuthorisationRepository ?? throw new ArgumentNullException("caisseAuthorisationRepository");
		_reglementCoffreRepository = reglementCoffreRepository ?? throw new ArgumentNullException("reglementCoffreRepository");
		_licenceApplicationVersion = licenceApplicationVersion ?? throw new ArgumentNullException("licenceApplicationVersion");
		_traiteRepository = traiteRepository ?? throw new ArgumentNullException("traiteRepository");
		_bordereauVirementRepository = bordereauVirementRepository;
		_ligneBordereauVirementRepository = ligneBordereauVirementRepository;
	}

	public Task<IEnumerable<ReglementClient>> GetAllReglementClientAComptaAsync(DateTime dateMin, DateTime dateMax, EtatComptabilite etat, int[] caissesNo, int[] modesNo, Remis[] remis, bool? pointe, CancellationToken cancellationToken)
	{
		Societe societe = SocieteManager.Societe;
		return _reglementClientRepository.GetAllAComptaAsync(dateMin.Date, dateMax.Date.AddDays(1.0), etat, caissesNo, modesNo, remis, pointe, societe.No, cancellationToken);
	}

	public async Task<IList<ReglementClientRemplace>> GetAllReglementClientRemplaceAComptaAsync(DateTime dateMin, DateTime dateMax, EtatComptabilite etat, int[] caissesNo, int[] modesNo, CancellationToken cancellationToken)
	{
		Societe societe = SocieteManager.Societe;
		return (await _reglementClientRemplaceRepository.GetAllRemplaceAComptaAsync(dateMin.Date, dateMax.Date.AddDays(1.0), etat, caissesNo, modesNo, societe.No, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).Select(_factory.Generate).ToList();
	}

	public Task<IEnumerable<RemboursementClientAllType>> GetAllRemboursementClientAllTypeAComptaAsync(DateTime dateDe, DateTime dateA, EtatComptabilite comptabilite, EcheanceType[] types, CancellationToken cancellationToken)
	{
		Societe societe = SocieteManager.Societe;
		ProfilType profilType = SocieteManager.Groupe.ProfilType;
		IEnumerable<int> enumerable = Enumerable.Empty<int>();
		enumerable = ((!SocieteManager.Utilisateur.IsAdmin) ? (from a in _autorisationCaisseRepository.GetAll(SocieteManager.Utilisateur.No, societe.No, profilType)
			select a.CaisseNo) : (from a in societe.GetCaisses()
			select a.No));
		return _remboursementClientAllTypeRepository.GetAllAComptaAsync(dateDe.Date, dateA.Date.AddDays(1.0), comptabilite, enumerable.ToArray(), types, societe.No, cancellationToken);
	}

	public Task<IEnumerable<Impaye>> GetAllImpayeAComptaAsync(DateTime dateMin, DateTime dateMax, int[] souchesNo, EtatComptabilite etat, CancellationToken cancellationToken)
	{
		Societe societe = SocieteManager.Societe;
		return _impayeRepository.GetAllAComptaAsync(dateMin.Date, dateMax.Date.AddDays(1.0), etat, souchesNo.ToArray(), societe.No, cancellationToken);
	}

	public Task<IEnumerable<ReglementClient>> GetAllReglementClientByDateEcheanceAComptaAsync(DateTime dateMin, DateTime dateMax, EtatComptabilite etat, int[] caissesNo, int[] modesNo, Remis[] remis, bool? pointe, CancellationToken cancellationToken)
	{
		Societe societe = SocieteManager.Societe;
		return _reglementClientRepository.GetAllAComptaByDateEcheanceAsync(dateMin.Date, dateMax.Date.AddDays(1.0), etat, caissesNo, modesNo, remis, pointe, societe.No, cancellationToken);
	}

	public Task<IEnumerable<ReglementClient>> GetAllReglementClientByDateRemisAComptaAsync(DateTime dateMin, DateTime dateMax, EtatComptabilite etat, int[] caissesNo, int[] modesNo, bool? pointe, CancellationToken cancellationToken)
	{
		Societe societe = SocieteManager.Societe;
		return _reglementClientRepository.GetAllAComptaByDateRemisAsync(dateMin.Date, dateMax.Date.AddDays(1.0), etat, caissesNo, modesNo, pointe, societe.No, cancellationToken);
	}

	public ReglementClient ReglementClientGetByErpNo(int erpReglementNo, Societe societe = null)
	{
		int societeNo = societe?.No ?? SocieteManager.Societe.No;
		return _reglementClientRepository.GetByErpNo(erpReglementNo, societeNo);
	}

	public Task<IEnumerable<ReglementFournisseur>> GetAllReglementFournisseurAComptaAsync(DateTime dateMin, DateTime dateMax, EtatComptabilite etat, int[] caissesNo, int[] modesNo, CancellationToken cancellationToken)
	{
		Societe societe = SocieteManager.Societe;
		return _reglementFournisseurRepository.GetAllAComptaAsync(dateMin.Date, dateMax.Date.AddDays(1.0), etat, caissesNo, modesNo, societe.No, cancellationToken);
	}

	public async Task<IList<ReglementFournisseur>> GetAllReglementFournisseurByDateRecupAComptaAsync(DateTime dateMin, DateTime dateMax, EtatComptabilite etat, int[] caissesNo, int[] modesNo, CancellationToken cancellationToken)
	{
		Societe societe = SocieteManager.Societe;
		List<SocieteModeReglement> source = (from x in societe.GetAllModes()
			where modesNo.Any((int modeNo) => x.No == modeNo)
			select x).ToList();
		IEnumerable<SocieteModeReglement> modesRecuperable = source.Where((SocieteModeReglement x) => x.Type == ReglementType.Cheque || x.Type == ReglementType.Traite || x.IsRetenu);
		IEnumerable<int> modesNonRecuperable = modesNo.Where((int modeNo) => !modesRecuperable.Any((SocieteModeReglement x) => x.No == modeNo));
		List<ReglementFournisseur> result = new List<ReglementFournisseur>();
		if (modesRecuperable.Any())
		{
			List<ReglementFournisseur> list = result;
			list.AddRange(await _reglementFournisseurRepository.GetAllByDateRecuperationAComptaAsync(dateMin.Date, dateMax.Date.AddDays(1.0), etat, caissesNo, modesRecuperable.Select((SocieteModeReglement x) => x.No).ToArray(), societe.No, cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
		}
		if (modesNonRecuperable.Any())
		{
			List<ReglementFournisseur> list = result;
			list.AddRange(await _reglementFournisseurRepository.GetAllAComptaAsync(dateMin.Date, dateMax.Date.AddDays(1.0), etat, caissesNo, modesNonRecuperable.ToArray(), societe.No, cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
		}
		return result;
	}

	public Task<IEnumerable<ReglementFournisseur>> GetAllReglementFournisseurTraiteByEcheanceAComptaAsync(DateTime dateEcheanceMin, DateTime dateEcheanceMax, EtatComptabilite etat, int[] caissesNo, int[] modesNo, CancellationToken cancellationToken)
	{
		Societe societe = SocieteManager.Societe;
		return _reglementFournisseurRepository.GetAllDecaisseByEcheanceAComptaAsync(dateEcheanceMin.Date, dateEcheanceMax.Date.AddDays(1.0), etat, caissesNo, modesNo, societe.No, cancellationToken);
	}

	public Task<IEnumerable<ReglementFournisseur>> GetAllReglementFournisseurTraiteByRapprochementAComptaAsync(DateTime dateRapprochementMin, DateTime dateRapprochementMax, EtatComptabilite etat, int[] caissesNo, int[] modesNo, CancellationToken cancellationToken)
	{
		Societe societe = SocieteManager.Societe;
		return _reglementFournisseurRepository.GetAllDecaisseByRapprochementAComptaAsync(dateRapprochementMin.Date, dateRapprochementMax.Date.AddDays(1.0), etat, caissesNo, modesNo, societe.No, cancellationToken);
	}

	public Task<IEnumerable<ReglementFournisseur>> GetAllReglementFournisseurTraiteByRecuperationAComptaAsync(DateTime dateRecuperationMin, DateTime dateRecuperationMax, EtatComptabilite etat, int[] caissesNo, int[] modesNo, CancellationToken cancellationToken)
	{
		Societe societe = SocieteManager.Societe;
		return _reglementFournisseurRepository.GetAllDecaisseByRecuperationAComptaAsync(dateRecuperationMin.Date, dateRecuperationMax.Date.AddDays(1.0), etat, caissesNo, modesNo, societe.No, cancellationToken);
	}

	public void AffectationDelete(int reglementNo, int affectationNo, int deviseSocieteNo, bool isDossierImpaye, int dossierNo = 0)
	{
		ReglementClient reglementClient = _reglementClientRepository.Get(reglementNo);
		if (reglementClient == null)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorReglementNo);
		}
		Caisse caisse = Get(reglementClient.CaisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Impossible de supprimer l'imputation! La caisse [" + caisse.Intitule + "] est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grc))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (reglementClient.IsAnnule)
		{
			throw new ApplicationException("Le règlement [" + reglementClient.Numero + "] est annulé!");
		}
		IEnumerable<Affectation> affectations = reglementClient.GetAffectations();
		Affectation affectation = affectations.SingleOrDefault((Affectation x) => x.No == affectationNo);
		if (affectation == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorOperationInvalide);
		}
		if (affectation.DeclarationTvaEncaissementNo.HasValue)
		{
			throw new ApplicationException("L'affectation est inclut dans une déclaration TVA/Encaissement.");
		}
		if (affectation.IsSynchro)
		{
			throw new ApplicationException("L'affectation est synchronisée");
		}
		Echeance echeance = affectation.GetEcheance();
		if (echeance == null)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorEcheanceNo);
		}
		if (echeance.Type == EcheanceType.DroitTimbreClient)
		{
			throw new ApplicationException("L'affectation est associée à un règlement soumis au droit de timbre.");
		}
		if (dossierNo != 0 && (_dossierReglementRepository.GetDossier(dossierNo) ?? throw new ApplicationException("Impossible de charger le dossier de règlement.")).StatutDossier != StatutDossierReglement.Valide)
		{
			throw new ApplicationException("Le dossier n'est pas validé.");
		}
		if (!isDossierImpaye)
		{
			DossierImpayeManager dossierImpayeManager = SocieteManager.DossierImpayeManager;
			if (dossierImpayeManager.GetLigneImpayeByReglementNo(reglementNo) != null)
			{
				throw new ApplicationException("Le règlement [" + reglementClient.Numero + "] appartient à un dossier règlement impayé!");
			}
			if (echeance.Type == EcheanceType.Impaye && dossierImpayeManager.GetLignesImpayeByEcheanceNo(echeance.No).Any())
			{
				throw new ApplicationException("L'échéance [" + echeance.DocumentNumero + "] appartient à un dossier règlement impayé!");
			}
			Societe societe = SocieteManager.Societe;
			if (echeance.Type == EcheanceType.Impaye && societe.HasDossierImpRestriction)
			{
				throw new InvalidOperationException("l'imputation de l'impayé [" + echeance.DocumentNumero + "] n'est possible qu'à partir d'un dossier règlement impayé!");
			}
			if (echeance.Type == EcheanceType.CommissionImpaye || echeance.Type == EcheanceType.InteretImpaye)
			{
				throw new InvalidOperationException($"Opération invalide pour l'échéance [{echeance.DocumentNumero}][{echeance.Type}]!");
			}
		}
		if (isDossierImpaye && echeance.Type != EcheanceType.Impaye && echeance.Type != EcheanceType.CommissionImpaye && echeance.Type != EcheanceType.InteretImpaye)
		{
			throw new InvalidOperationException($"Opération invalide pour l'échéance [{echeance.DocumentNumero}][{echeance.Type}]!");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		if (echeance.Ajuste)
		{
			int echeanceEcart = _echeanceRepository.GetEcheanceEcart(echeance.No);
			Echeance echeance2 = _echeanceRepository.Get(echeanceEcart);
			if (echeance2 == null)
			{
				throw new ApplicationException("Impossible de charger l'échéance d'écart");
			}
			Ecart byNo = _ecartRepository.GetByNo(echeance2.No, echeance2.SocieteNo);
			if (byNo == null)
			{
				throw new ApplicationException("Impossible de charger l'écart [" + echeance2.DocumentNumero + "].");
			}
			if (byNo.IsComptabilise != EtatComptabilite.NonComptabilise)
			{
				throw new ApplicationException("L'échéance d'écart [" + byNo.DocumentNumero + "] est comptabilisée!");
			}
			Affectation affectation2 = echeance2.GetAffectations().SingleOrDefault();
			if (affectation2 == null)
			{
				throw new ApplicationException("Impossible de charger l'affectation d'écart!");
			}
			int reglementNo2 = affectation2.ReglementNo;
			if (reglementNo2 == affectation.ReglementNo)
			{
				decimal montant = affectation.Montant + echeance2.Montant;
				AffectationUpdateInternal(reglementNo2, affectationNo, montant, deviseSocieteNo);
				AffectationDeleteInternal(reglementNo2, affectation2.No, deviseSocieteNo);
				_echeanceRepository.Delete(echeance2);
				_echeanceRepository.Ajuster(echeance.No, isAjuste: false);
				_notifyService.Notify(TypeEntity.Echeance, echeance.No, TypeAction.Modification, echeance.SocieteNo);
				_notifyService.Notify(TypeEntity.Echeance, echeance2.No, TypeAction.Suppression, echeance.SocieteNo);
			}
			else
			{
				Affectation affectation3 = (_reglementClientRepository.Get(reglementNo2) ?? throw new ApplicationException("Impossbile de charger le règlement d'ajustement!")).GetAffectations().SingleOrDefault((Affectation x) => x.EcheanceNo == affectation.EcheanceNo);
				if (affectation3 == null)
				{
					throw new ApplicationException("Impossbile de charger l'affectation d'ajustement!");
				}
				decimal montant2 = affectation3.Montant + echeance2.Montant;
				AffectationUpdateInternal(reglementNo2, affectation3.No, montant2, deviseSocieteNo);
				AffectationDeleteInternal(reglementNo2, affectation2.No, deviseSocieteNo);
				_echeanceRepository.Delete(echeance2);
				_echeanceRepository.Ajuster(echeance.No, isAjuste: false);
				_notifyService.Notify(TypeEntity.Echeance, echeance.No, TypeAction.Modification, echeance.SocieteNo);
				_notifyService.Notify(TypeEntity.Echeance, echeance2.No, TypeAction.Suppression, echeance.SocieteNo);
			}
		}
		if (echeance.Type == EcheanceType.Perte)
		{
			if (!echeance.EcartNo.HasValue)
			{
				throw new ApplicationException("Impossible de charger échéance d'ajustement!");
			}
			Echeance echeance3 = _echeanceRepository.Get(echeance.EcartNo.Value);
			if (echeance3 == null)
			{
				throw new ApplicationException("Impossible de charger l'échéance d'ajustement!");
			}
			if (echeance3.IsComptaEcart)
			{
				throw new ApplicationException("Ecart [" + echeance3.DocumentNumero + "] est comptabilisé!");
			}
			if (echeance.GetAffectations().SingleOrDefault() == null)
			{
				throw new ApplicationException("Impossible de charger l'affectation d'écart!");
			}
			_affectationRepository.Delete(affectation);
			_echeanceRepository.Delete(echeance);
			echeance3.GetAffectations().ToList();
			Affectation affectation4 = echeance3.GetAffectations().SingleOrDefault((Affectation x) => x.ReglementNo == affectation.ReglementNo);
			if (affectation4 == null)
			{
				throw new ApplicationException("Impossible de charger l'affectation d'échéance d'ajustement!");
			}
			decimal montant3 = affectation4.Montant + affectation.Montant;
			decimal montantDevise = affectation4.MontantDeviseSociete + affectation.MontantDeviseSociete;
			_affectationRepository.Update(affectation4, montant3, montantDevise);
			echeance3.Solde -= affectation.Montant;
			echeance3.SoldeDeviseSociete -= affectation.MontantDeviseSociete;
			_echeanceRepository.Update(echeance3);
			_echeanceRepository.Ajuster(echeance3.No, isAjuste: false);
			_notifyService.Notify(TypeEntity.Reglement, reglementNo, TypeAction.Modification, reglementClient.SocieteNo);
			_notifyService.Notify(TypeEntity.Echeance, echeance.No, TypeAction.Suppression, echeance.SocieteNo);
			_notifyService.Notify(TypeEntity.Echeance, echeance3.No, TypeAction.Modification, echeance.SocieteNo);
			transactionScope.Complete();
			return;
		}
		if (reglementClient.Ajuste)
		{
			Echeance echeanceEcartGain = null;
			reglementClient = _reglementClientRepository.Get(reglementNo);
			Affectation affectation5 = reglementClient.GetAffectations().FirstOrDefault(delegate(Affectation x)
			{
				echeanceEcartGain = _echeanceRepository.Get(x.EcheanceNo);
				return echeanceEcartGain.Type == EcheanceType.Gain;
			});
			if (affectation5 == null)
			{
				throw new ApplicationException("Impossible de charger l'affectation d'ajustement de gain!");
			}
			Ecart byNo2 = _ecartRepository.GetByNo(echeanceEcartGain.No, echeance.SocieteNo);
			if (byNo2 == null)
			{
				throw new ApplicationException("Impossible de charger l'écart [" + echeanceEcartGain.DocumentNumero + "].");
			}
			if (byNo2.IsComptabilise != EtatComptabilite.NonComptabilise)
			{
				throw new ApplicationException("L'échéance d'écart [" + byNo2.DocumentNumero + "] est comptabilisée!");
			}
			AffectationDeleteInternal(reglementNo, affectation5.No, deviseSocieteNo);
			_echeanceRepository.Delete(echeanceEcartGain);
			_notifyService.Notify(TypeEntity.Echeance, echeanceEcartGain.No, TypeAction.Suppression, reglementClient.SocieteNo);
			if (affectationNo == affectation5.No)
			{
				transactionScope.Complete();
				return;
			}
		}
		AffectationDeleteInternal(reglementNo, affectationNo, deviseSocieteNo);
		transactionScope.Complete();
	}

	private int EcheanceCalculerDelaisMoyenPayement(int echeanceNo)
	{
		Echeance echeance = _echeanceRepository.Get(echeanceNo);
		if (echeance == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorEcheanceNo);
		}
		return echeance.Domaine switch
		{
			ErpDomaine.Vente => EcheanceCalculerDelaisMoyenPayementClient(echeanceNo), 
			ErpDomaine.Achat => EcheanceCalculerDelaisMoyenPayementFournisseur(echeanceNo), 
			_ => 0, 
		};
	}

	private int EcheanceCalculerDelaisMoyenPayementClient(int echeanceNo)
	{
		Echeance echeance = _echeanceRepository.Get(echeanceNo);
		if (echeance == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorEcheanceNo);
		}
		if (echeance.Type != EcheanceType.Erp && echeance.Type != EcheanceType.Solde && echeance.Type != EcheanceType.Impaye && echeance.Type != EcheanceType.FactureGR)
		{
			return 0;
		}
		decimal num = default(decimal);
		foreach (Affectation affectation in echeance.GetAffectations())
		{
			int num2 = (int)((_reglementClientRepository.Get(affectation.ReglementNo) ?? throw new ApplicationException(TresorerieCoreMessages.ErrorReglementNo)).DateEcheance.Date - echeance.DocumentDate.Date).TotalDays;
			num += affectation.Montant * (decimal)num2;
		}
		return (int)Math.Round((echeance.MontantDeviseSociete == 0m) ? 0m : (num / echeance.MontantDeviseSociete), 0, MidpointRounding.AwayFromZero);
	}

	private int EcheanceCalculerDelaisMoyenPayementFournisseur(int echeanceNo)
	{
		Echeance echeance = _echeanceRepository.Get(echeanceNo);
		if (echeance == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorEcheanceNo);
		}
		if (echeance.Type != EcheanceType.Erp && echeance.Type != EcheanceType.Solde && echeance.Type != EcheanceType.FactureGR && echeance.Type != EcheanceType.FactureFrsTresorerie && echeance.Type != EcheanceType.ImpayeFournisseur)
		{
			return 0;
		}
		decimal num = default(decimal);
		foreach (Affectation affectation in echeance.GetAffectations())
		{
			int num2 = (int)((_reglementFournisseurRepository.Get(affectation.ReglementNo) ?? throw new ApplicationException(TresorerieCoreMessages.ErrorReglementNo)).DateEcheance.Date - echeance.DocumentDate.Date).TotalDays;
			num += affectation.Montant * (decimal)num2;
		}
		return (int)Math.Round((echeance.MontantDeviseSociete == 0m) ? 0m : (num / echeance.MontantDeviseSociete), 0, MidpointRounding.AwayFromZero);
	}

	public void AffectationDeleteInternal(int reglementNo, int affectationNo, int deviseSocieteNo)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		ReglementClient reglementClient = _reglementClientRepository.Get(reglementNo);
		if (reglementClient == null)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorReglementNo);
		}
		Caisse caisse = Get(reglementClient.CaisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Impossible de supprimer l'imputation! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grc))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (reglementClient.IsAnnule)
		{
			throw new ApplicationException("Le règlement est annulé!");
		}
		Affectation affectation = reglementClient.GetAffectations().SingleOrDefault((Affectation x) => x.No == affectationNo);
		if (affectation == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorOperationInvalide);
		}
		if (affectation.IsSynchro)
		{
			throw new ApplicationException("L'affectation est synchronisée");
		}
		Echeance echeance = affectation.GetEcheance();
		if (echeance == null)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorEcheanceNo);
		}
		Affectation affectation2 = reglementClient.GetAffectations().SingleOrDefault((Affectation x) => x.EcartEcheanceNo == echeance.No);
		if (!SocieteManager.UserHasAutorisationSouche(echeance.Domaine, echeance.SoucheNo))
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorAutorisationSouche);
		}
		reglementClient.Ajuste = false;
		reglementClient.Solde += affectation.Montant;
		reglementClient.SoldeDeviseSociete += affectation.MontantDeviseSociete;
		reglementClient.DateModification = DateTime.Now;
		reglementClient.ModificateurNo = utilisateur.No;
		if (!(reglementClient.Solde < 0m))
		{
			decimal soldeDeviseSociete = reglementClient.SoldeDeviseSociete;
			decimal? obj = affectation2?.MontantDeviseSociete;
			decimal? num = (decimal?)soldeDeviseSociete + obj;
			if (!((num.GetValueOrDefault() < default(decimal)) & num.HasValue))
			{
				if (SocieteManager.Societe.GetDevise(deviseSocieteNo) == null)
				{
					throw new ApplicationException(TresorerieCoreMessages.ErrorDeviseNo);
				}
				TransactionOptions transactionOptions = new TransactionOptions
				{
					IsolationLevel = IsolationLevel.ReadCommitted,
					Timeout = TransactionManager.MaximumTimeout
				};
				using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
				DeleteEcartChange(reglementClient, echeance, deviseSocieteNo);
				_affectationRepository.Delete(affectation);
				echeance.Solde += affectation.Montant;
				echeance.SoldeDeviseSociete += affectation.MontantDeviseSociete;
				echeance.Ajuste = false;
				echeance.DelaisMoyenPayement = EcheanceCalculerDelaisMoyenPayement(echeance.No);
				_echeanceRepository.Update(echeance);
				_reglementClientRepository.Update(reglementClient);
				_notifyService.Notify(TypeEntity.Reglement, reglementNo, TypeAction.Modification, reglementClient.SocieteNo);
				_notifyService.Notify(TypeEntity.Echeance, echeance.No, TypeAction.Modification, reglementClient.SocieteNo);
				transactionScope.Complete();
				return;
			}
		}
		throw new ArgumentException(TresorerieCoreMessages.ErrorEcheanceNegative);
	}

	public void AffectationFournisseurDeleteInternal(int reglementNo, int affectationNo, int deviseSocieteNo)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		ReglementFournisseur reglementFournisseur = _reglementFournisseurRepository.Get(reglementNo);
		if (reglementFournisseur == null)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorReglementNo);
		}
		Caisse caisse = Get(reglementFournisseur.CaisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Impossible de supprimer l'imputation! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grf))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (reglementFournisseur.IsAnnule)
		{
			throw new ApplicationException("Le règlement est annulé!");
		}
		Affectation affectation = reglementFournisseur.GetAffectations().SingleOrDefault((Affectation x) => x.No == affectationNo);
		if (affectation == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorOperationInvalide);
		}
		if (affectation.IsSynchro)
		{
			throw new ApplicationException("L'affectation est synchronisée");
		}
		Echeance echeance = affectation.GetEcheance();
		if (echeance == null)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorEcheanceNo);
		}
		Affectation affectation2 = reglementFournisseur.GetAffectations().SingleOrDefault((Affectation x) => x.EcartEcheanceNo == echeance.No);
		if (!SocieteManager.UserHasAutorisationSouche(echeance.Domaine, echeance.SoucheNo))
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorAutorisationSouche);
		}
		reglementFournisseur.Ajuste = false;
		reglementFournisseur.Solde += affectation.Montant;
		reglementFournisseur.SoldeDevise += affectation.MontantDeviseSociete;
		reglementFournisseur.DateModification = DateTime.Now;
		reglementFournisseur.ModificateurNo = utilisateur.No;
		if (!(reglementFournisseur.Solde < 0m))
		{
			decimal soldeDevise = reglementFournisseur.SoldeDevise;
			decimal? obj = affectation2?.MontantDeviseSociete;
			decimal? num = (decimal?)soldeDevise + obj;
			if (!((num.GetValueOrDefault() < default(decimal)) & num.HasValue))
			{
				if (SocieteManager.Societe.GetDevise(deviseSocieteNo) == null)
				{
					throw new ApplicationException(TresorerieCoreMessages.ErrorDeviseNo);
				}
				TransactionOptions transactionOptions = new TransactionOptions
				{
					IsolationLevel = IsolationLevel.ReadCommitted,
					Timeout = TransactionManager.MaximumTimeout
				};
				using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
				DeleteEcartChangeFournisseur(reglementFournisseur, echeance, deviseSocieteNo);
				_affectationRepository.Delete(affectation);
				echeance.Solde += affectation.Montant;
				echeance.SoldeDeviseSociete += affectation.MontantDeviseSociete;
				echeance.Ajuste = false;
				echeance.DelaisMoyenPayement = EcheanceCalculerDelaisMoyenPayement(echeance.No);
				_echeanceRepository.Update(echeance);
				_reglementFournisseurRepository.Update(reglementFournisseur);
				_notifyService.Notify(TypeEntity.ReglementFournisseur, reglementNo, TypeAction.Modification, reglementFournisseur.SocieteNo);
				_notifyService.Notify(TypeEntity.Echeance, echeance.No, TypeAction.Modification, reglementFournisseur.SocieteNo);
				transactionScope.Complete();
				return;
			}
		}
		throw new ArgumentException(TresorerieCoreMessages.ErrorEcheanceNegative);
	}

	public Affectation AffectationGet(int no)
	{
		if (no < 0)
		{
			throw new ArgumentNullException("no");
		}
		return _affectationRepository.Get(no);
	}

	public List<Affectation> AffectationGetByEcartNo(int ecartNo)
	{
		if (ecartNo <= 0)
		{
			throw new ArgumentException("l'écart no est invalide!");
		}
		return _affectationRepository.GetByEcartEcheance(ecartNo).ToList();
	}

	public void AffectationUpdate(int reglementNo, int affectationNo, decimal montant, int deviseSocieteNo)
	{
		ReglementClient reglementClient = _reglementClientRepository.Get(reglementNo);
		if (reglementClient == null)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorReglementNo);
		}
		if (reglementClient.IsAnnule)
		{
			throw new ApplicationException("Le règlement [" + reglementClient.Numero + "] est annulé!");
		}
		Caisse caisse = Get(reglementClient.CaisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grc))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		IEnumerable<Affectation> affectations = reglementClient.GetAffectations();
		Affectation affectation = affectations.SingleOrDefault((Affectation x) => x.No == affectationNo);
		if (affectation == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorOperationInvalide);
		}
		if (affectation.IsSynchro)
		{
			throw new ApplicationException("L'affectation est synchronisée");
		}
		if (affectation.DeclarationTvaEncaissementNo.HasValue)
		{
			throw new ApplicationException("L'affectation est inclut dans une déclaration TVA/Encaissement.");
		}
		Echeance echeance = affectation.GetEcheance();
		if (echeance == null)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorEcheanceNo);
		}
		Societe societe = SocieteManager.Societe;
		DossierImpayeManager dossierImpayeManager = SocieteManager.DossierImpayeManager;
		if (dossierImpayeManager.GetLigneImpayeByReglementNo(reglementNo) != null)
		{
			throw new ApplicationException("Le règlement [" + reglementClient.Numero + "] appartient à un dossier règlement impayé!");
		}
		if (echeance.Type == EcheanceType.Impaye && dossierImpayeManager.GetLignesImpayeByEcheanceNo(echeance.No).Any())
		{
			throw new ApplicationException("L'échéance [" + echeance.DocumentNumero + "] appartient à un dossier règlement impayé!");
		}
		bool flag = _licenceApplicationVersion.Application == ApplicationRunning.TresoClient && societe.HasDossierImpRestriction;
		if (echeance.Type == EcheanceType.Impaye && flag)
		{
			throw new InvalidOperationException("l'imputation de l'impayé [" + echeance.DocumentNumero + "] n'est possible qu'à partir d'un dossier règlement impayé!");
		}
		if (echeance.Type == EcheanceType.CommissionImpaye || echeance.Type == EcheanceType.InteretImpaye)
		{
			throw new InvalidOperationException($"Opération invalide pour l'échéance [{echeance.DocumentNumero}][{echeance.Type}]!");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		if (echeance.Ajuste)
		{
			int echeanceEcartPerteNo = _echeanceRepository.GetEcheanceEcart(echeance.No);
			decimal montant2 = reglementClient.Montant;
			decimal num = (from x in reglementClient.GetAffectations()
				where x.No != affectationNo && x.EcheanceNo != echeanceEcartPerteNo
				select x).Sum((Affectation x) => x.Montant);
			montant2 -= num;
			if (montant > montant2)
			{
				throw new ApplicationException($"Montant invalide! [doit être inferieur ou egale a {montant2:0.000}]");
			}
			Echeance echeance2 = _echeanceRepository.Get(echeanceEcartPerteNo);
			if (echeance2 == null)
			{
				throw new ApplicationException("Impossible de charger l'échéance d'écart");
			}
			Ecart byNo = _ecartRepository.GetByNo(echeance2.No, echeance.SocieteNo);
			if (byNo == null)
			{
				throw new ApplicationException("Impossible de charger l'écart [" + echeance2.DocumentNumero + "].");
			}
			if (byNo.IsComptabilise != EtatComptabilite.NonComptabilise)
			{
				throw new ApplicationException("L'échéance d'écart [" + byNo.DocumentNumero + "] est comptabilisée!");
			}
			Affectation affectation2 = echeance2.GetAffectations().SingleOrDefault();
			if (affectation2 == null)
			{
				throw new ApplicationException("Impossible de charger l'affectation d'écart!");
			}
			int? num2 = affectation2.ReglementNo;
			if (num2 == affectation.ReglementNo)
			{
				decimal montant3 = affectation.Montant + echeance2.Montant;
				AffectationUpdateInternal(num2.Value, affectationNo, montant3, deviseSocieteNo);
				AffectationDeleteInternal(num2.Value, affectation2.No, deviseSocieteNo);
				_echeanceRepository.Delete(echeance2);
				_echeanceRepository.Ajuster(echeance.No, isAjuste: false);
			}
			else
			{
				Affectation affectation3 = (_reglementClientRepository.Get(num2.Value) ?? throw new ApplicationException("Impossbile de charger le reglement d'ajustement!")).GetAffectations().SingleOrDefault((Affectation x) => x.EcheanceNo == affectation.EcheanceNo);
				if (affectation3 == null)
				{
					throw new ApplicationException("Impossbile de charger l'affectation d'ajustement!");
				}
				decimal montant4 = affectation3.Montant + echeance2.Montant;
				AffectationUpdateInternal(num2.Value, affectation3.No, montant4, deviseSocieteNo);
				AffectationDelete(num2.Value, affectation2.No, deviseSocieteNo, isDossierImpaye: false);
				_echeanceRepository.Delete(echeance2);
				_echeanceRepository.Ajuster(echeance.No, isAjuste: false);
			}
		}
		if (reglementClient.Ajuste)
		{
			Echeance echeanceEcartGain = null;
			Affectation affectation4 = reglementClient.GetAffectations().FirstOrDefault(delegate(Affectation x)
			{
				echeanceEcartGain = _echeanceRepository.Get(x.EcheanceNo);
				return echeanceEcartGain.Type == EcheanceType.Gain;
			});
			if (affectation4 == null)
			{
				throw new ApplicationException("Impossible de charger l'affectation d'ajustement de gain!");
			}
			Ecart byNo2 = _ecartRepository.GetByNo(echeanceEcartGain.No, echeance.SocieteNo);
			if (byNo2 == null)
			{
				throw new ApplicationException("Impossible de charger l'écart [" + echeanceEcartGain.DocumentNumero + "].");
			}
			if (byNo2.IsComptabilise != EtatComptabilite.NonComptabilise)
			{
				throw new ApplicationException("L'échéance d'écart [" + byNo2.DocumentNumero + "] est comptabilisée!");
			}
			AffectationDeleteInternal(reglementNo, affectation4.No, deviseSocieteNo);
			_echeanceRepository.Delete(echeanceEcartGain);
			_notifyService.Notify(TypeEntity.Echeance, echeanceEcartGain.No, TypeAction.Suppression, reglementClient.SocieteNo);
		}
		AffectationUpdateInternal(reglementNo, affectationNo, montant, deviseSocieteNo);
		transactionScope.Complete();
	}

	public void AffectationFournisseurUpdate(int reglementNo, int affectationNo, decimal montant, int deviseSocieteNo)
	{
		ReglementFournisseur reglementFournisseur = _reglementFournisseurRepository.Get(reglementNo);
		if (reglementFournisseur == null)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorReglementNo);
		}
		if (reglementFournisseur.IsAnnule)
		{
			throw new ApplicationException("Le règlement [" + reglementFournisseur.Numero + "] est annulé!");
		}
		Caisse caisse = Get(reglementFournisseur.CaisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grf))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		IEnumerable<Affectation> affectations = reglementFournisseur.GetAffectations();
		Affectation affectation = affectations.SingleOrDefault((Affectation x) => x.No == affectationNo);
		if (affectation == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorOperationInvalide);
		}
		if (affectation.IsSynchro)
		{
			throw new ApplicationException("L'affectation est synchronisée");
		}
		if (affectation.DeclarationTvaEncaissementNo.HasValue)
		{
			throw new ApplicationException("L'affectation est inclut dans une déclaration TVA/Encaissement.");
		}
		Echeance echeance = affectation.GetEcheance();
		if (echeance == null)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorEcheanceNo);
		}
		_ = SocieteManager.Societe;
		DossierImpayeManager dossierImpayeManager = SocieteManager.DossierImpayeManager;
		if (dossierImpayeManager.GetLigneImpayeByReglementNo(reglementNo) != null)
		{
			throw new ApplicationException("Le règlement [" + reglementFournisseur.Numero + "] appartient à un dossier règlement impayé!");
		}
		if (echeance.Type == EcheanceType.ImpayeFournisseur && dossierImpayeManager.GetLignesImpayeFournisseurByEcheanceNo(echeance.No).Any())
		{
			throw new ApplicationException("L'échéance [" + echeance.DocumentNumero + "] appartient à un dossier règlement impayé!");
		}
		if (echeance.Type == EcheanceType.CommissionImpaye || echeance.Type == EcheanceType.InteretImpaye)
		{
			throw new InvalidOperationException($"Opération invalide pour l'échéance [{echeance.DocumentNumero}][{echeance.Type}]!");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		if (echeance.Ajuste)
		{
			int echeanceEcartPerteNo = _echeanceRepository.GetEcheanceEcart(echeance.No);
			decimal montant2 = reglementFournisseur.Montant;
			decimal num = (from x in reglementFournisseur.GetAffectations()
				where x.No != affectationNo && x.EcheanceNo != echeanceEcartPerteNo
				select x).Sum((Affectation x) => x.Montant);
			montant2 -= num;
			if (montant < montant2)
			{
				throw new ApplicationException($"Montant invalide! [doit être suppérieur ou egale a {montant2:0.000}]");
			}
			Echeance echeance2 = _echeanceRepository.Get(echeanceEcartPerteNo);
			if (echeance2 == null)
			{
				throw new ApplicationException("Impossible de charger l'échéance d'écart");
			}
			Ecart byNo = _ecartRepository.GetByNo(echeance2.No, echeance.SocieteNo);
			if (byNo == null)
			{
				throw new ApplicationException("Impossible de charger l'écart [" + echeance2.DocumentNumero + "].");
			}
			if (byNo.IsComptabilise != EtatComptabilite.NonComptabilise)
			{
				throw new ApplicationException("L'échéance d'écart [" + byNo.DocumentNumero + "] est comptabilisée!");
			}
			Affectation affectation2 = echeance2.GetAffectations().SingleOrDefault();
			if (affectation2 == null)
			{
				throw new ApplicationException("Impossible de charger l'affectation d'écart!");
			}
			int? num2 = affectation2.ReglementNo;
			if (num2 == affectation.ReglementNo)
			{
				decimal montant3 = affectation.Montant + echeance2.Montant;
				AffectationUpdateFournisseurInternal(num2.Value, affectationNo, montant3, deviseSocieteNo);
				AffectationDeleteInternal(num2.Value, affectation2.No, deviseSocieteNo);
				_echeanceRepository.Delete(echeance2);
				_echeanceRepository.Ajuster(echeance.No, isAjuste: false);
			}
			else
			{
				Affectation affectation3 = (_reglementFournisseurRepository.Get(num2.Value) ?? throw new ApplicationException("Impossbile de charger le reglement d'ajustement!")).GetAffectations().SingleOrDefault((Affectation x) => x.EcheanceNo == affectation.EcheanceNo);
				if (affectation3 == null)
				{
					throw new ApplicationException("Impossbile de charger l'affectation d'ajustement!");
				}
				decimal montant4 = affectation3.Montant + echeance2.Montant;
				AffectationUpdateInternal(num2.Value, affectation3.No, montant4, deviseSocieteNo);
				AffectationDelete(num2.Value, affectation2.No, deviseSocieteNo, isDossierImpaye: false);
				_echeanceRepository.Delete(echeance2);
				_echeanceRepository.Ajuster(echeance.No, isAjuste: false);
			}
		}
		if (reglementFournisseur.Ajuste)
		{
			Echeance echeanceEcartGain = null;
			Affectation affectation4 = reglementFournisseur.GetAffectations().FirstOrDefault(delegate(Affectation x)
			{
				echeanceEcartGain = _echeanceRepository.Get(x.EcheanceNo);
				return echeanceEcartGain.Type == EcheanceType.Gain;
			});
			if (affectation4 == null)
			{
				throw new ApplicationException("Impossible de charger l'affectation d'ajustement de gain!");
			}
			Ecart byNo2 = _ecartRepository.GetByNo(echeanceEcartGain.No, echeance.SocieteNo);
			if (byNo2 == null)
			{
				throw new ApplicationException("Impossible de charger l'écart [" + echeanceEcartGain.DocumentNumero + "].");
			}
			if (byNo2.IsComptabilise != EtatComptabilite.NonComptabilise)
			{
				throw new ApplicationException("L'échéance d'écart [" + byNo2.DocumentNumero + "] est comptabilisée!");
			}
			AffectationDeleteInternal(reglementNo, affectation4.No, deviseSocieteNo);
			_echeanceRepository.Delete(echeanceEcartGain);
			_notifyService.Notify(TypeEntity.Echeance, echeanceEcartGain.No, TypeAction.Suppression, reglementFournisseur.SocieteNo);
		}
		AffectationUpdateInternal(reglementNo, affectationNo, montant, deviseSocieteNo);
		transactionScope.Complete();
	}

	public void AnnulerChequeFrs(int chequeNo, string motifAnnulation, bool isUsed)
	{
		Cheque cheque = _chequeRepository.Get(chequeNo);
		if (cheque == null)
		{
			throw new ApplicationException("Chèque invalide!");
		}
		if (string.IsNullOrEmpty(motifAnnulation))
		{
			throw new ApplicationException("Il faut indiquer le motif d'annulation du Chèque!");
		}
		if (cheque.Statut != ChequeStatut.NonUtilise && !isUsed)
		{
			throw new ApplicationException("Statut chèque invalide!");
		}
		cheque.Statut = ChequeStatut.Annuler;
		cheque.MotifAnnulation = motifAnnulation;
		_chequeRepository.Update(cheque);
		Societe societe = SocieteManager.Societe;
		_notifyService.Notify(TypeEntity.Cheques, chequeNo, TypeAction.Modification, societe.No);
	}

	public void AnnulerTraiteFrs(int traiteNo, string motifAnnulation, bool isUsed)
	{
		Traite traite = _traiteRepository.Get(traiteNo);
		if (traite == null)
		{
			throw new ApplicationException("Traite invalide!");
		}
		if (string.IsNullOrEmpty(motifAnnulation))
		{
			throw new ApplicationException("Il faut indiquer le motif d'annulation de la traite!");
		}
		if (traite.Statut != ChequeStatut.NonUtilise && !isUsed)
		{
			throw new ApplicationException("Statut traite invalide!");
		}
		traite.Statut = ChequeStatut.Annuler;
		traite.MotifAnnulation = motifAnnulation;
		_traiteRepository.Update(traite);
		Societe societe = SocieteManager.Societe;
		_notifyService.Notify(TypeEntity.Traites, traiteNo, TypeAction.Modification, societe.No);
		_notifyService.Notify(TypeEntity.CarnetTraite, traite.CarnetTraiteNo, TypeAction.Modification, societe.No);
	}

	[Obsolete]
	public int BordereauCreate(string numero, DateTime date, int deviseNo, int typeBordereauNo, int caisseNo, string pieceNumero, int banqueNo, string libelle, string infoLibre1, string infoLibre2, string infoLibre3, string infoLibre4, Utilisateur utilisateur = null, Societe societe = null, decimal? montantFrais = null, decimal? montantTvaFrais = null)
	{
		return BordereauCreate(date, deviseNo, typeBordereauNo, caisseNo, pieceNumero, banqueNo, libelle, infoLibre1, infoLibre2, infoLibre3, infoLibre4, utilisateur, societe, montantFrais, montantTvaFrais);
	}

	public int BordereauCreate(DateTime date, int deviseNo, int typeBordereauNo, int caisseNo, string pieceNumero, int banqueNo, string libelle, string infoLibre1, string infoLibre2, string infoLibre3, string infoLibre4, Utilisateur utilisateur = null, Societe societe = null, decimal? montantFrais = null, decimal? montantTvaFrais = null)
	{
		if (caisseNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (banqueNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorBanqueInvalide);
		}
		if (deviseNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorDeviseNo);
		}
		if (typeBordereauNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorBordereauTypeInvalide);
		}
		if (banqueNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorBanqueInvalide);
		}
		if (montantFrais.HasValue && montantFrais.Value < 0m)
		{
			throw new ArgumentException("Le montant de frais est invalid ");
		}
		if (montantTvaFrais.HasValue && montantTvaFrais.Value < 0m)
		{
			throw new ArgumentException("Le montant de Tva est invalid ");
		}
		societe = societe ?? SocieteManager.Societe;
		if (societe == null)
		{
			throw new ApplicationException("Impossible de charger la société courante.");
		}
		if (!societe.GetSocieteDevises().Any((SocieteDevise x) => x.No == deviseNo))
		{
			throw new ApplicationException("Impossible de charger la devise du bordereau.");
		}
		utilisateur = utilisateur ?? SocieteManager.Utilisateur;
		if (utilisateur == null)
		{
			throw new ApplicationException("Impossible de charger la société courante.");
		}
		Caisse caisse = _caisseRepository.Get(caisseNo);
		if (caisse == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Impossible de créer le bordereau! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisseNo, ProfilType.Grc, utilisateur, societe))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		InformationsBanque byBanqueId = SocieteManager.Groupe.InformationBanqueManager.GetByBanqueId(banqueNo);
		if (byBanqueId != null && byBanqueId.EnSommeil)
		{
			throw new ApplicationException("Impossible de créer le bordereau! La banque est en sommeil");
		}
		string numeroPieceCourante = SocieteManager.GetNumeroPieceCourante(EntityNumerotation.Bordereau, societe.No);
		if (string.IsNullOrEmpty(numeroPieceCourante))
		{
			throw new ApplicationException("Impossible de récupérer le numéro du bordereau!");
		}
		if (_bordereauRepository.Exists(caisse.SocieteNo, numeroPieceCourante))
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorBordereauExist);
		}
		TypeBordereau typeBordereau = SocieteManager.Groupe.BordereauxTypeManager.Get(typeBordereauNo);
		if (typeBordereau == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorBordereauTypeInvalide);
		}
		if (!typeBordereau.GetModeReglements().Any((ModeReglement x) => caisse.HasModeReglement(x.No)))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorBordereauMode, caisse.Code));
		}
		Bordereau bordereau = new Bordereau(numeroPieceCourante, banqueNo, date, caisseNo, caisse.SocieteNo, typeBordereauNo, isComptabiliser: false)
		{
			DateRemis = DateTime.Now,
			Libelle = libelle,
			PieceNumero = pieceNumero,
			UserNo = utilisateur.No,
			StatutTransfert = StatutTransfert.None,
			DateCreation = DateTime.Now,
			DateModification = DateTime.Now,
			ModificateurNo = utilisateur.No,
			BordereauNature = typeBordereau.Nature,
			DeviseNo = deviseNo,
			MontantFrais = montantFrais,
			MontantTvaFrais = montantTvaFrais,
			InfoLibre1 = infoLibre1,
			InfoLibre2 = infoLibre2,
			InfoLibre3 = infoLibre3,
			InfoLibre4 = infoLibre4
		};
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		int num = _bordereauRepository.Create(bordereau);
		_notifyService.Notify(TypeEntity.Bordereau, num, TypeAction.Ajout, bordereau.SocieteNo);
		transactionScope.Complete();
		return num;
	}

	public int BordereauVirementCreate(DateTime date, int deviseNo, int caisseNo, string pieceNumero, int banqueNo, string libelle, Utilisateur utilisateur = null, Societe societe = null)
	{
		if (caisseNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (banqueNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorBanqueInvalide);
		}
		if (deviseNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorDeviseNo);
		}
		if (banqueNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorBanqueInvalide);
		}
		societe = societe ?? SocieteManager.Societe;
		if (societe == null)
		{
			throw new ApplicationException("Impossible de charger la société courante.");
		}
		if (!societe.GetSocieteDevises().Any((SocieteDevise x) => x.No == deviseNo))
		{
			throw new ApplicationException("Impossible de charger la devise du bordereau.");
		}
		utilisateur = utilisateur ?? SocieteManager.Utilisateur;
		if (utilisateur == null)
		{
			throw new ApplicationException("Impossible de charger la société courante.");
		}
		Caisse caisse = _caisseRepository.Get(caisseNo);
		if (caisse == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Impossible de créer le bordereau! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisseNo, ProfilType.Grf, utilisateur, societe))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		InformationsBanque byBanqueId = SocieteManager.Groupe.InformationBanqueManager.GetByBanqueId(banqueNo);
		if (byBanqueId != null && byBanqueId.EnSommeil)
		{
			throw new ApplicationException("Impossible de créer le bordereau! La banque est en sommeil");
		}
		string numeroPieceCourante = SocieteManager.GetNumeroPieceCourante(EntityNumerotation.BordereauVirement, societe.No);
		if (string.IsNullOrEmpty(numeroPieceCourante))
		{
			throw new ApplicationException("Impossible de récupérer le numéro du bordereau!");
		}
		if (_bordereauRepository.Exists(caisse.SocieteNo, numeroPieceCourante))
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorBordereauExist);
		}
		BordereauVirement bordereauVirement = new BordereauVirement(numeroPieceCourante, banqueNo, date, caisseNo, caisse.SocieteNo, isComptabiliser: false)
		{
			DateRemis = DateTime.Now,
			Libelle = libelle,
			PieceNumero = pieceNumero,
			UserNo = utilisateur.No,
			StatutTransfert = StatutTransfert.None,
			DateCreation = DateTime.Now,
			DateModification = DateTime.Now,
			ModificateurNo = utilisateur.No,
			DeviseNo = deviseNo
		};
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		int num = _bordereauVirementRepository.Create(bordereauVirement);
		_notifyService.Notify(TypeEntity.BordereauVirement, num, TypeAction.Ajout, bordereauVirement.SocieteNo);
		transactionScope.Complete();
		return num;
	}

	public void BordereauChangerCoursReglements(int bordereauNo, int societeDeviseNo, decimal newCours)
	{
		if (newCours <= 0m)
		{
			throw new ApplicationException("newCours");
		}
		if (bordereauNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorBordereauNo);
		}
		if (societeDeviseNo <= 0)
		{
			throw new ArgumentNullException("societeDeviseNo");
		}
		Societe societe = SocieteManager.Societe;
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		Bordereau bordereau = _bordereauRepository.Get(bordereauNo);
		if (bordereau == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorOperationInvalide);
		}
		if (bordereau.DeviseNo == societeDeviseNo)
		{
			throw new ApplicationException("Opération invalide! Le bordereau " + bordereau.Numero + " est en devise société.");
		}
		Caisse caisse = Get(bordereau.CaisseNo);
		if (caisse == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grc, utilisateur, societe))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (bordereau.IsRemis)
		{
			throw new ApplicationException(TresorerieCoreMessages.InfoBordereauRemis);
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		foreach (LigneBordereau item in bordereau.GetLigneBordereaux())
		{
			ReglementClientChangeCours(item.No, societeDeviseNo, newCours);
		}
		Bordereau bordereau2 = _bordereauRepository.Get(bordereauNo);
		if (bordereau2 == null)
		{
			throw new ArgumentNullException("Impossible de charger le bordereau.");
		}
		bordereau2.DateModification = DateTime.Now;
		bordereau2.ModificateurNo = utilisateur.No;
		bordereau2.MontantDeviseSociete = bordereau2.GetLigneBordereaux().Sum((LigneBordereau x) => x.MontantDeviseSociete);
		_bordereauRepository.Update(bordereau2);
		_notifyService.Notify(TypeEntity.Bordereau, bordereauNo, TypeAction.Modification, bordereau2.SocieteNo);
		transactionScope.Complete();
	}

	public void BordereauCreateLigne(int bordereauNo, int reglementNo, int societeDeviseNo, Utilisateur utilisateur = null, Societe societe = null)
	{
		societe = societe ?? SocieteManager.Societe;
		if (societe == null)
		{
			throw new ApplicationException("Impossible de charger la société courante.");
		}
		utilisateur = utilisateur ?? SocieteManager.Utilisateur;
		if (societe == null)
		{
			throw new ApplicationException("Impossible de charger l'utilisateur courant.");
		}
		if (bordereauNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorBordereauNo);
		}
		if (reglementNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorReglementNo);
		}
		if (societeDeviseNo <= 0)
		{
			throw new ArgumentNullException("societeDeviseNo");
		}
		Bordereau bordereau = _bordereauRepository.Get(bordereauNo);
		if (bordereau == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorOperationInvalide);
		}
		Caisse caisse = Get(bordereau.CaisseNo);
		if (caisse == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Impossible d'ajouter la ligne bordereau! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grc, utilisateur, societe))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (bordereau.IsRemis)
		{
			throw new ApplicationException(TresorerieCoreMessages.InfoBordereauRemis);
		}
		ReglementClient reglement = _reglementClientRepository.Get(reglementNo);
		if (reglement == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorOperationInvalide);
		}
		if (reglement.IsAnnule)
		{
			throw new ApplicationException("Le règlement est annulé!");
		}
		if (reglement.IsReglementAvoir)
		{
			throw new ApplicationException("Opération invalide. Règlement d'avoir.");
		}
		TypeBordereau typeBordereau = SocieteManager.Groupe.BordereauxTypeManager.Get(bordereau.TypeNo);
		ModeReglement modeReglement = typeBordereau.GetModeReglements().FirstOrDefault((ModeReglement x) => x.No == reglement.ModeReglementNo);
		if (modeReglement == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorModeReglementInvalide);
		}
		if (modeReglement.Type == ReglementType.Espece)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorModeReglementInvalide);
		}
		if (reglement.IsRemis != Remis.NonRemis)
		{
			Bordereau bordereau2 = _bordereauRepository.Get(reglement.EnteteBordereauNo.GetValueOrDefault());
			if (bordereau2 == null)
			{
				throw new InvalidOperationException(TresorerieCoreMessages.ErrorBordereauNo);
			}
			throw new ApplicationException(string.Format(TresorerieCoreMessages.InfoReglementRemis, bordereau2.Numero));
		}
		if (reglement.StatutTransfert != StatutTransfert.None)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorOperationInvalide);
		}
		if (reglement.CaisseNo != bordereau.CaisseNo)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglementCaisse);
		}
		if (reglement.IsRemplace != Remplace.NonRemplace)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorrReglementTransfert);
		}
		int num = ((bordereau.DeviseNo == 0) ? societeDeviseNo : bordereau.DeviseNo);
		if (num != reglement.DeviseNo && num != reglement.DeviseOrigineNo)
		{
			throw new InvalidOperationException("Le règlement et le bordereau ne partagent pas la même devise.");
		}
		LigneBordereau ligneBordereau = new LigneBordereau(reglement, bordereau)
		{
			BordereauNature = typeBordereau.Nature,
			DateRemis = DateTime.Now
		};
		bordereau.DateModification = DateTime.Now;
		bordereau.ModificateurNo = utilisateur.No;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_ligneBordereauRepository.Add(ligneBordereau, null);
		HistoriqueMvt historiqueMvt = reglement.GetHistoriques().Single((HistoriqueMvt x) => x.CaisseNo == bordereau.CaisseNo && x.Sens == SensMouvement.Entree && !x.Lot.IsEpuise);
		Lot lot = historiqueMvt.Lot;
		HistoriqueMvt historiqueMvt2 = new HistoriqueMvt(lot.No, bordereau.CaisseNo, SensMouvement.Sortie, MouvementDomaine.EnteteBordereau, modeReglement.No, reglementNo, bordereauNo, StatutTransfert.None, num, new Lot(ligneBordereau.No, ligneBordereau.Montant, 0m));
		_historiqueRepository.Create(historiqueMvt2);
		lot.MontantRestant = 0m;
		_historiqueRepository.Update(historiqueMvt);
		_mouvementRepository.ModifyMouvement(reglement.No, utilisateur.No);
		bordereau.Montant += ligneBordereau.Montant;
		bordereau.MontantDeviseSociete += ligneBordereau.MontantDeviseSociete;
		_bordereauRepository.Update(bordereau);
		_notifyService.Notify(TypeEntity.Bordereau, bordereauNo, TypeAction.Modification, reglement.SocieteNo);
		_notifyService.Notify(TypeEntity.Reglement, reglement.No, TypeAction.Modification, reglement.SocieteNo);
		transactionScope.Complete();
	}

	public void BordereauVirementCreateLigne(int bordereauNo, int reglementNo, int societeDeviseNo, Utilisateur utilisateur = null, Societe societe = null)
	{
		societe = societe ?? SocieteManager.Societe;
		if (societe == null)
		{
			throw new ApplicationException("Impossible de charger la société courante.");
		}
		utilisateur = utilisateur ?? SocieteManager.Utilisateur;
		if (societe == null)
		{
			throw new ApplicationException("Impossible de charger l'utilisateur courant.");
		}
		if (bordereauNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorBordereauNo);
		}
		if (reglementNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorReglementNo);
		}
		if (societeDeviseNo <= 0)
		{
			throw new ArgumentNullException("societeDeviseNo");
		}
		BordereauVirement bordereauVirement = _bordereauVirementRepository.Get(bordereauNo);
		if (bordereauVirement == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorOperationInvalide);
		}
		Caisse caisse = Get(bordereauVirement.CaisseNo);
		if (caisse == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Impossible d'ajouter la ligne bordereau! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grf, utilisateur, societe))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (bordereauVirement.IsRemis)
		{
			throw new ApplicationException(TresorerieCoreMessages.InfoBordereauRemis);
		}
		ReglementFournisseur reglementFournisseur = _reglementFournisseurRepository.Get(reglementNo);
		if (reglementFournisseur == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorOperationInvalide);
		}
		if (reglementFournisseur.IsAnnule)
		{
			throw new ApplicationException("Le règlement est annulé!");
		}
		if (reglementFournisseur.Type != ReglementType.Virement)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorModeReglementInvalide);
		}
		if (((bordereauVirement.DeviseNo == 0) ? societeDeviseNo : bordereauVirement.DeviseNo) != reglementFournisseur.DeviseNo)
		{
			throw new InvalidOperationException("Le règlement et le bordereau ne partagent pas la même devise.");
		}
		LigneBordereauVirement ligneBordereauVirement = new LigneBordereauVirement(reglementFournisseur, bordereauVirement)
		{
			DateRemis = DateTime.Now
		};
		bordereauVirement.DateModification = DateTime.Now;
		bordereauVirement.ModificateurNo = utilisateur.No;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_ligneBordereauVirementRepository.Add(ligneBordereauVirement, null);
		_mouvementRepository.ModifyMouvement(reglementFournisseur.No, utilisateur.No);
		bordereauVirement.Montant += ligneBordereauVirement.Montant;
		bordereauVirement.MontantDeviseSociete += ligneBordereauVirement.MontantDeviseSociete;
		_bordereauVirementRepository.Update(bordereauVirement);
		_notifyService.Notify(TypeEntity.BordereauVirement, bordereauNo, TypeAction.Modification, reglementFournisseur.SocieteNo);
		_notifyService.Notify(TypeEntity.ReglementFournisseur, reglementFournisseur.No, TypeAction.Modification, reglementFournisseur.SocieteNo);
		transactionScope.Complete();
	}

	public void BordereauDelete(int bordereauNo)
	{
		if (bordereauNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorBordereauNo);
		}
		Bordereau bordereau = _bordereauRepository.Get(bordereauNo);
		if (bordereau == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorBordereauNo);
		}
		Caisse caisse = Get(bordereau.CaisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Opération invalide! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grc))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (bordereau.IsRemis)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.InfoBordereauRemis);
		}
		if (bordereau.IsPointe)
		{
			throw new ApplicationException(TresorerieCoreMessages.InfoBordereauRapproche);
		}
		HistoriqueMvt historiqueMvt = _historiqueRepository.GetAllByCaisse(bordereau.CaisseNo, bordereau.DeviseNo).FirstOrDefault((HistoriqueMvt x) => x.MouvementOutNo == bordereauNo);
		if (!bordereau.GetLigneBordereaux().Any() && historiqueMvt != null)
		{
			VersementEspeceDelete(bordereauNo, bordereau.CaisseNo);
			return;
		}
		IEnumerable<LigneBordereau> ligneBordereaux = bordereau.GetLigneBordereaux();
		OperationBancaire operationBancaireByBordereau = SocieteManager.Groupe.PrevisionnelManager.GetOperationBancaireByBordereau(bordereauNo);
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		foreach (Note item in _noteRepository.GetAllNotesByEntite(bordereau.No, TypeEntity.Bordereau))
		{
			_noteRepository.Delete(item);
		}
		foreach (LigneBordereau item2 in ligneBordereaux)
		{
			BordereauDeleteLigne(bordereau.No, item2.No);
		}
		if (operationBancaireByBordereau != null)
		{
			SocieteManager.Groupe.PrevisionnelManager.OperationBancaireSupprimer(operationBancaireByBordereau.No, fromDeleteBordereau: true);
		}
		_bordereauRepository.Delete(bordereau);
		_notifyService.Notify(TypeEntity.Bordereau, bordereauNo, TypeAction.Suppression, bordereau.SocieteNo);
		transactionScope.Complete();
	}

	public void BordereauVirementDelete(int bordereauNo)
	{
		if (bordereauNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorBordereauNo);
		}
		BordereauVirement bordereauVirement = _bordereauVirementRepository.Get(bordereauNo);
		if (bordereauVirement == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorBordereauNo);
		}
		Caisse caisse = Get(bordereauVirement.CaisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Opération invalide! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grc))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (bordereauVirement.IsRemis)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.InfoBordereauRemis);
		}
		if (bordereauVirement.IsPointe)
		{
			throw new ApplicationException(TresorerieCoreMessages.InfoBordereauRapproche);
		}
		if (bordereauVirement.IsFichierGenere)
		{
			throw new InvalidOperationException("Opération invalide! Un ordre de virement a été généré.");
		}
		IEnumerable<LigneBordereauVirement> ligneBordereaux = bordereauVirement.GetLigneBordereaux();
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		foreach (Note item in _noteRepository.GetAllNotesByEntite(bordereauVirement.No, TypeEntity.Bordereau))
		{
			_noteRepository.Delete(item);
		}
		foreach (LigneBordereauVirement item2 in ligneBordereaux)
		{
			BordereauVirementDeleteLigne(bordereauVirement.No, item2.No);
		}
		_bordereauVirementRepository.Delete(bordereauVirement);
		_notifyService.Notify(TypeEntity.BordereauVirement, bordereauNo, TypeAction.Suppression, bordereauVirement.SocieteNo);
		transactionScope.Complete();
	}

	public void BordereauDeleteLigne(int bordereauNo, int ligneNo)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		if (bordereauNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorBordereauNo);
		}
		if (ligneNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorLigneBordoreau);
		}
		Bordereau bordereau = _bordereauRepository.Get(bordereauNo);
		if (bordereau == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorBordereauNo);
		}
		Caisse caisse = Get(bordereau.CaisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Opération invalide! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grc))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (bordereau.IsRemis)
		{
			throw new ApplicationException(TresorerieCoreMessages.InfoBordereauRemis);
		}
		if (bordereau.IsPointe)
		{
			throw new ApplicationException(TresorerieCoreMessages.InfoBordereauRapproche);
		}
		LigneBordereau ligne = bordereau.GetLigneBordereaux().SingleOrDefault((LigneBordereau x) => x.No == ligneNo);
		if (ligne == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorLigneBordoreau);
		}
		ReglementClient reglementClient = _reglementClientRepository.Get(ligneNo);
		if (reglementClient == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementNo);
		}
		if (reglementClient.IsRemis != Remis.RemisBordereau)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorOperationInvalide);
		}
		if (reglementClient.IsPointe)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglementRapproche);
		}
		if (reglementClient.IsAnnule)
		{
			throw new ApplicationException("Le règlement est annulé!");
		}
		bordereau.DateModification = DateTime.Now;
		bordereau.ModificateurNo = utilisateur.No;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_ligneBordereauRepository.Delete(ligneNo);
		List<HistoriqueMvt> source = _historiqueRepository.GetAllByMouvement(ligne.No).ToList();
		HistoriqueMvt historiqueMvt = source.Single((HistoriqueMvt x) => x.Sens == SensMouvement.Sortie && x.MouvementOutNo == bordereauNo && x.MouvementInNo == ligne.No);
		_historiqueRepository.Delete(historiqueMvt);
		HistoriqueMvt historiqueMvt2 = (from s in source
			where s.Sens == SensMouvement.Entree && s.CaisseNo == bordereau.CaisseNo
			orderby s.No
			select s).Last();
		historiqueMvt2.Lot.MontantRestant += ligne.Montant;
		_historiqueRepository.Update(historiqueMvt2);
		_mouvementRepository.ModifyMouvement(reglementClient.No, utilisateur.No);
		bordereau.Montant -= ligne.Montant;
		bordereau.MontantDeviseSociete -= ligne.MontantDeviseSociete;
		_bordereauRepository.Update(bordereau);
		_notifyService.Notify(TypeEntity.Reglement, reglementClient.No, TypeAction.Modification, reglementClient.SocieteNo);
		_notifyService.Notify(TypeEntity.Bordereau, bordereauNo, TypeAction.Modification, reglementClient.SocieteNo);
		transactionScope.Complete();
	}

	public void BordereauVirementDeleteLigne(int bordereauNo, int ligneNo)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		if (bordereauNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorBordereauNo);
		}
		if (ligneNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorLigneBordoreau);
		}
		BordereauVirement bordereauVirement = _bordereauVirementRepository.Get(bordereauNo);
		if (bordereauVirement == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorBordereauNo);
		}
		Caisse caisse = Get(bordereauVirement.CaisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Opération invalide! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grf))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (bordereauVirement.IsRemis)
		{
			throw new ApplicationException(TresorerieCoreMessages.InfoBordereauRemis);
		}
		if (bordereauVirement.IsPointe)
		{
			throw new ApplicationException(TresorerieCoreMessages.InfoBordereauRapproche);
		}
		LigneBordereauVirement ligneBordereauVirement = bordereauVirement.GetLigneBordereaux().SingleOrDefault((LigneBordereauVirement x) => x.No == ligneNo);
		if (ligneBordereauVirement == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorLigneBordoreau);
		}
		ReglementFournisseur reglementFournisseur = _reglementFournisseurRepository.Get(ligneNo);
		if (reglementFournisseur == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementNo);
		}
		if (reglementFournisseur.IsAnnule)
		{
			throw new ApplicationException("Le règlement est annulé!");
		}
		bordereauVirement.DateModification = DateTime.Now;
		bordereauVirement.ModificateurNo = utilisateur.No;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_ligneBordereauVirementRepository.Delete(ligneNo);
		_mouvementRepository.ModifyMouvement(reglementFournisseur.No, utilisateur.No);
		bordereauVirement.Montant -= ligneBordereauVirement.Montant;
		bordereauVirement.MontantDeviseSociete -= ligneBordereauVirement.MontantDeviseSociete;
		_bordereauVirementRepository.Update(bordereauVirement);
		_notifyService.Notify(TypeEntity.ReglementFournisseur, reglementFournisseur.No, TypeAction.Modification, reglementFournisseur.SocieteNo);
		_notifyService.Notify(TypeEntity.BordereauVirement, bordereauNo, TypeAction.Modification, reglementFournisseur.SocieteNo);
		transactionScope.Complete();
	}

	public void BordereauDeremettre(int bordereauNo)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		if (bordereauNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorBordereauNo);
		}
		Bordereau bordereau = _bordereauRepository.Get(bordereauNo);
		if (bordereau == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorBordereauNo);
		}
		Caisse caisse = Get(bordereau.CaisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Opération invalide! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grc))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (!bordereau.IsRemis)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorBordereauNonRemis);
		}
		if (bordereau.GetLigneBordereaux().Any((LigneBordereau x) => x.IsImpaye))
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorBordereauOperationInvalide);
		}
		if (bordereau.IsComptabilise)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorBordereauComptabilise);
		}
		if (bordereau.IsPointe)
		{
			throw new ApplicationException(TresorerieCoreMessages.InfoBordereauRapproche);
		}
		if (bordereau.GetLigneBordereaux().Any((LigneBordereau x) => x.GetReglement().IsPointe))
		{
			throw new ApplicationException(TresorerieCoreMessages.InfoBordereauRapproche);
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		bordereau.IsRemis = false;
		bordereau.PieceNumero = string.Empty;
		bordereau.DateModification = DateTime.Now;
		bordereau.ModificateurNo = utilisateur.No;
		_bordereauRepository.Update(bordereau);
		foreach (LigneBordereau item in bordereau.GetLigneBordereaux())
		{
			_ligneBordereauRepository.Update(item, bordereau.CompteBanqueNo, isRemis: false);
			_notifyService.Notify(TypeEntity.Reglement, item.No, TypeAction.Modification, bordereau.SocieteNo);
		}
		_notifyService.Notify(TypeEntity.Bordereau, bordereauNo, TypeAction.Modification, bordereau.SocieteNo);
		transactionScope.Complete();
	}

	public void BordereauVirementDeremettre(int bordereauNo)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		if (bordereauNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorBordereauNo);
		}
		BordereauVirement bordereauVirement = _bordereauVirementRepository.Get(bordereauNo);
		if (bordereauVirement == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorBordereauNo);
		}
		Caisse caisse = Get(bordereauVirement.CaisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Opération invalide! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grf))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (!bordereauVirement.IsRemis)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorBordereauNonRemis);
		}
		bordereauVirement.GetLigneBordereaux();
		if (bordereauVirement.IsComptabilise)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorBordereauComptabilise);
		}
		if (bordereauVirement.IsPointe)
		{
			throw new ApplicationException(TresorerieCoreMessages.InfoBordereauRapproche);
		}
		if (bordereauVirement.IsFichierGenere)
		{
			throw new InvalidOperationException("Opération invalide! Un ordre de virement a été généré.");
		}
		if (bordereauVirement.GetLigneBordereaux().Any((LigneBordereauVirement x) => x.GetReglement().IsPointe))
		{
			throw new ApplicationException(TresorerieCoreMessages.InfoBordereauRapproche);
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		bordereauVirement.IsRemis = false;
		bordereauVirement.PieceNumero = string.Empty;
		bordereauVirement.DateModification = DateTime.Now;
		bordereauVirement.ModificateurNo = utilisateur.No;
		_bordereauVirementRepository.Update(bordereauVirement);
		_notifyService.Notify(TypeEntity.BordereauVirement, bordereauNo, TypeAction.Modification, bordereauVirement.SocieteNo);
		transactionScope.Complete();
	}

	public IEnumerable<HistoriqueMvt> BordereauEspeceGetAllLigne(int bordereauNo)
	{
		if (bordereauNo < 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorBordereauNo);
		}
		return from x in _historiqueRepository.GetAllLigneBordreau(bordereauNo)
			where x.Sens == SensMouvement.Sortie
			select x;
	}

	public Bordereau BordereauGet(int caisseNo, string numero)
	{
		if (string.IsNullOrEmpty(numero))
		{
			throw new ArgumentNullException("numero");
		}
		if (caisseNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		return _bordereauRepository.Get(caisseNo, numero);
	}

	public BordereauVirement BordereauVirementGet(int caisseNo, string numero)
	{
		if (string.IsNullOrEmpty(numero))
		{
			throw new ArgumentNullException("numero");
		}
		if (caisseNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		return _bordereauVirementRepository.Get(caisseNo, numero);
	}

	public Bordereau BordereauGet(int no)
	{
		return _bordereauRepository.Get(no);
	}

	public IList<Bordereau> GetAllBordereauxToLettrer(DateTime dateMin, DateTime datMax)
	{
		Societe societe = SocieteManager.Societe;
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		return (utilisateur.IsAdmin ? _bordereauRepository.GetAllToLettrer(societe.No, dateMin, datMax) : _bordereauRepository.GetAllToLettrer(societe.No, dateMin, datMax, (from a in _autorisationCaisseRepository.GetAll(utilisateur.No, societe.No, ProfilType.Grc)
			select a.CaisseNo).ToArray())).ToList();
	}

	public BordereauVirement BordereauVirementGet(int no)
	{
		return _bordereauVirementRepository.Get(no);
	}

	public Bordereau BordereauGet(string numero, Societe societe = null)
	{
		societe = societe ?? SocieteManager.Societe;
		return _bordereauRepository.GetByNumero(societe.No, numero);
	}

	public BordereauVirement BordereauVirementGet(string numero, Societe societe = null)
	{
		societe = societe ?? SocieteManager.Societe;
		return _bordereauVirementRepository.GetByNumero(societe.No, numero);
	}

	public IEnumerable<Bordereau> BordereauGetAll(int caisseNo)
	{
		if (caisseNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		return _bordereauRepository.GetAll(caisseNo);
	}

	public IEnumerable<Bordereau> BordereauGetAll(Societe societe = null, Utilisateur utilisateur = null)
	{
		societe = societe ?? SocieteManager.Societe;
		utilisateur = utilisateur ?? SocieteManager.Utilisateur;
		if (!utilisateur.IsAdmin)
		{
			return _bordereauRepository.GetAllBySociete(societe.No, (from a in _autorisationCaisseRepository.GetAll(utilisateur.No, societe.No, ProfilType.Grc)
				select a.CaisseNo).ToArray());
		}
		return _bordereauRepository.GetAllBySociete(societe.No);
	}

	public IEnumerable<BordereauVirement> BordereauVirementGetAll(Societe societe = null, Utilisateur utilisateur = null)
	{
		societe = societe ?? SocieteManager.Societe;
		utilisateur = utilisateur ?? SocieteManager.Utilisateur;
		if (!utilisateur.IsAdmin)
		{
			return _bordereauVirementRepository.GetAllBySociete(societe.No, (from a in _autorisationCaisseRepository.GetAll(utilisateur.No, societe.No, ProfilType.Grf)
				select a.CaisseNo).ToArray());
		}
		return _bordereauVirementRepository.GetAllBySociete(societe.No);
	}

	public IEnumerable<Bordereau> BordereauGetAllByBanque(int banqueNo, Societe societe = null)
	{
		societe = societe ?? SocieteManager.Societe;
		return _bordereauRepository.GetAllByBanque(banqueNo, societe.No);
	}

	public void BordereauUpdateInfoLibre(int bordereauNo, string info1, string info2, string info3, string info4)
	{
		if (bordereauNo <= 0)
		{
			throw new ApplicationException("bordereauNo");
		}
		Bordereau bordereau = _bordereauRepository.Get(bordereauNo);
		if (bordereau == null)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorBordereauNo);
		}
		Caisse caisse = Get(bordereau.CaisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grc))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (bordereau.IsComptabilise)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorBordereauComptabilise);
		}
		InformationLibre informationLibre = SocieteManager.InfoLibreBordereauGet() ?? throw new ArgumentException("Impossible de charger le parametrage des informations libres");
		string text = (string.IsNullOrEmpty(informationLibre.IntituleInfoLibre1) ? "Info libre 1" : informationLibre.IntituleInfoLibre1);
		if (informationLibre.IsObligatoireInfoLibre1 && string.IsNullOrEmpty(info1))
		{
			throw new ApplicationException(text + " est obligatoire!");
		}
		string text2 = (string.IsNullOrEmpty(informationLibre.IntituleInfoLibre2) ? "Info libre 2" : informationLibre.IntituleInfoLibre2);
		if (informationLibre.IsObligatoireInfoLibre2 && string.IsNullOrEmpty(info2))
		{
			throw new ApplicationException(text2 + " est obligatoire!");
		}
		string text3 = (string.IsNullOrEmpty(informationLibre.IntituleInfoLibre3) ? "Info libre 3" : informationLibre.IntituleInfoLibre3);
		if (informationLibre.IsObligatoireInfoLibre3 && string.IsNullOrEmpty(info3))
		{
			throw new ApplicationException(text3 + " est obligatoire!");
		}
		string text4 = (string.IsNullOrEmpty(informationLibre.IntituleInfoLibre4) ? "Info libre 4" : informationLibre.IntituleInfoLibre4);
		if (informationLibre.IsObligatoireInfoLibre4 && string.IsNullOrEmpty(info4))
		{
			throw new ApplicationException(text4 + " est obligatoire!");
		}
		Societe societe = SocieteManager.Societe;
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		bordereau.InfoLibre1 = info1;
		bordereau.InfoLibre2 = info2;
		bordereau.InfoLibre3 = info3;
		bordereau.InfoLibre4 = info4;
		bordereau.DateModification = DateTime.Now;
		bordereau.ModificateurNo = utilisateur.No;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_bordereauRepository.Update(bordereau);
		_notifyService.Notify(TypeEntity.Bordereau, bordereauNo, TypeAction.Modification, societe.No);
		transactionScope.Complete();
	}

	public void BordereauUpdateNumero(int bordereauNo, string newNumero)
	{
		if (bordereauNo <= 0)
		{
			throw new ArgumentNullException("bordereauNo");
		}
		if (string.IsNullOrEmpty(newNumero))
		{
			throw new ArgumentNullException("newNumero");
		}
		Societe societe = SocieteManager.Societe;
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		Bordereau bordereau = _bordereauRepository.Get(bordereauNo);
		if (bordereau == null)
		{
			throw new ApplicationException("Impossible de charger le bordereau.");
		}
		if (!(bordereau.Numero == newNumero))
		{
			if (_bordereauRepository.Get(societe.No, newNumero) != null)
			{
				throw new ApplicationException("Numèro bordereau existe déjà.");
			}
			if (bordereau.IsComptabilise)
			{
				throw new ApplicationException(TresorerieCoreMessages.ErrorBordereauComptabilise);
			}
			if (bordereau.IsRemis)
			{
				throw new ApplicationException("Le bordereau est remis à la banque.");
			}
			ModeReglement modeReglement = (SocieteManager.Groupe.BordereauxTypeManager.Get(bordereau.TypeNo) ?? throw new ApplicationException("Impossible de charger le type bordereau.")).GetModeReglements().FirstOrDefault();
			if (modeReglement == null)
			{
				throw new ApplicationException("Mode règlement du type bordereau invalide.");
			}
			if ((modeReglement.Type == ReglementType.Cheque || modeReglement.Type == ReglementType.Traite) && bordereau.GetLigneBordereaux().Any())
			{
				throw new ApplicationException("Opération invalide. Le bordereau contient des lignes.");
			}
			bordereau.ChangeNumero(newNumero);
			bordereau.DateModification = DateTime.Now;
			bordereau.ModificateurNo = utilisateur.No;
			_bordereauRepository.Update(bordereau);
			_notifyService.Notify(TypeEntity.Bordereau, bordereauNo, TypeAction.Modification, societe.No);
		}
	}

	public void BordereauVirementUpdateNumero(int bordereauNo, string newNumero)
	{
		if (bordereauNo <= 0)
		{
			throw new ArgumentNullException("bordereauNo");
		}
		if (string.IsNullOrEmpty(newNumero))
		{
			throw new ArgumentNullException("newNumero");
		}
		Societe societe = SocieteManager.Societe;
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		BordereauVirement bordereauVirement = _bordereauVirementRepository.Get(bordereauNo);
		if (bordereauVirement == null)
		{
			throw new ApplicationException("Impossible de charger le bordereau.");
		}
		if (!(bordereauVirement.Numero == newNumero))
		{
			if (_bordereauVirementRepository.Get(societe.No, newNumero) != null)
			{
				throw new ApplicationException("Numèro bordereau existe déjà.");
			}
			if (bordereauVirement.IsComptabilise)
			{
				throw new ApplicationException(TresorerieCoreMessages.ErrorBordereauComptabilise);
			}
			if (bordereauVirement.IsRemis)
			{
				throw new ApplicationException("Le bordereau est remis à la banque.");
			}
			_ = SocieteManager.Groupe;
			if (bordereauVirement.GetLigneBordereaux().Any())
			{
				throw new ApplicationException("Opération invalide. Le bordereau contient des lignes.");
			}
			bordereauVirement.ChangeNumero(newNumero);
			bordereauVirement.DateModification = DateTime.Now;
			bordereauVirement.ModificateurNo = utilisateur.No;
			_bordereauVirementRepository.Update(bordereauVirement);
			_notifyService.Notify(TypeEntity.BordereauVirement, bordereauNo, TypeAction.Modification, societe.No);
		}
	}

	public void BordereauVersementEspeceCreate(int bordereauNo, int caisseNo, int modeNo, decimal montant, string numeroPiece)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		if (string.IsNullOrEmpty(numeroPiece))
		{
			throw new ArgumentNullException("Numero Piece invalide!");
		}
		Bordereau bordereau = _bordereauRepository.Get(bordereauNo);
		if (bordereau == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorBordereauNo);
		}
		if (bordereau.IsRemis)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.InfoBordereauRemis);
		}
		Caisse obj = _caisseRepository.Get(caisseNo) ?? throw new ArgumentNullException(TresorerieCoreMessages.ErrorCaisseInvalid);
		if (obj.EnSommeil)
		{
			throw new ApplicationException("Impossible de créer le versement! La caisse est en sommeil!");
		}
		if ((obj.GetMode(modeNo) ?? throw new ArgumentException(TresorerieCoreMessages.ErrorModeInvalide)).Type != ReglementType.Espece)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorModeReglementInvalide);
		}
		decimal solde = SocieteManager.HistoriqueMvtManager.GetSolde(caisseNo, modeNo, bordereau.DeviseNo);
		if (montant <= 0m || montant > solde)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorMontantInavlide);
		}
		List<HistoriqueMvt> list = _historiqueRepository.GetAllNonEpuise(caisseNo, modeNo, bordereau.DeviseNo).ToList();
		int num = 0;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		while (montant > 0m && num < list.Count)
		{
			HistoriqueMvt historiqueMvt = list[num];
			Lot lot = historiqueMvt.Lot;
			decimal montantRestant = lot.MontantRestant;
			lot.MontantRestant = ((lot.MontantRestant >= montant) ? (lot.MontantRestant - montant) : 0m);
			_historiqueRepository.Update(historiqueMvt);
			HistoriqueMvt historiqueMvt2 = new HistoriqueMvt(lot.No, caisseNo, SensMouvement.Sortie, MouvementDomaine.EnteteBordereau, modeNo, lot.No, bordereauNo, StatutTransfert.None, bordereau.DeviseNo, new Lot(lot.No, (montantRestant >= montant) ? montant : montantRestant, 0m));
			_historiqueRepository.Create(historiqueMvt2);
			montant -= montantRestant;
			num++;
		}
		bordereau.IsRemis = true;
		bordereau.PieceNumero = numeroPiece;
		bordereau.DateRemis = DateTime.Now;
		bordereau.DateModification = DateTime.Now;
		bordereau.ModificateurNo = utilisateur.No;
		_bordereauRepository.Update(bordereau);
		transactionScope.Complete();
	}

	public void BordereauTransformerImpaye(int reglementNo, DateTime dateImpaye, string commentaire, int soucheNo, int representantNo, string pieceTreso, bool useRegularisationPreavis = false, bool isTypeBordreauCumuleBanque = false)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		if (reglementNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorReglementNo);
		}
		ReglementClient reglementClient = _reglementClientRepository.Get(reglementNo);
		if (reglementClient == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementNo);
		}
		if (!useRegularisationPreavis && reglementClient.Preavis != EtatPreavis.NonPreavis)
		{
			throw new ApplicationException("Le règlement " + reglementClient.Numero + " est préavisé.");
		}
		if (useRegularisationPreavis && reglementClient.Preavis != EtatPreavis.Preavis)
		{
			throw new ApplicationException("Le règlement " + reglementClient.Numero + " n'est pas préavisé.");
		}
		RemboursementFournisseur byReglementNo = _remboursementFournisseurRepository.GetByReglementNo(reglementNo);
		if (byReglementNo != null)
		{
			throw new ApplicationException("Le règlement est lié à le remboursement fournisseur [" + byReglementNo.Numero + "]!");
		}
		if (reglementClient.TiersType != TiersType.Client)
		{
			throw new InvalidOperationException("Le tiers [" + reglementClient.ClientCode + "] du règlement [" + reglementClient.Numero + "] est [" + reglementClient.TiersType.GetDisplayDescription() + "]!");
		}
		if (reglementClient.BordereauNature == 0 && reglementClient.IsPointe && string.IsNullOrEmpty(pieceTreso))
		{
			throw new InvalidOperationException("Le numéro de la pièce-tréso inexistante");
		}
		Caisse caisse = Get(reglementClient.CaisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Opération invalide! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grc))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (!SocieteManager.UserHasAutorisationSouche(ErpDomaine.Vente, soucheNo))
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorAutorisationSouche);
		}
		if (reglementClient.IsImpaye == ImpayeEtat.Impaye)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglementImpaye);
		}
		if (reglementClient.IsAnnule)
		{
			throw new ApplicationException("Le règlement est annulé!");
		}
		ReglementType type = reglementClient.GetModeReglement().Type;
		if (type != ReglementType.Traite && type != ReglementType.Cheque)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorOperationInvalide);
		}
		if (reglementClient.IsRemis != Remis.RemisBanque)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglementNonRemis);
		}
		if (reglementClient.BordereauNature == 1 && reglementClient.IsPointe && !isTypeBordreauCumuleBanque)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglementRapprocheInfo);
		}
		if ((reglementClient.BordereauNature == 0 || reglementClient.BordereauNature == 2) && reglementClient.IsEscompteRegle)
		{
			throw new InvalidOperationException("Opération invalide! Règlement est déjà réglé.");
		}
		SocieteDevise defaultDeviseSociete = SocieteManager.Societe.GetDefaultDeviseSociete();
		if (defaultDeviseSociete == null)
		{
			throw new ApplicationException("Impossible de charger la devise société.");
		}
		decimal num = Math.Round(reglementClient.Montant * reglementClient.DeviseCours, defaultDeviseSociete.NombreDecimales, MidpointRounding.AwayFromZero);
		Echeance echeance = new Echeance(reglementNo, reglementClient.Numero, ErpDomaine.Vente, ErpDocumentType.None, dateImpaye, reglementClient.Montant, reglementClient.Montant, reglementClient.DateEcheance, EcheanceType.Impaye, reglementClient.ModeReglementNo, reglementClient.SocieteNo, reglementClient.ClientNo, reglementClient.ClientCode, reglementClient.ClientIntitule, reglementClient.ClientNo, reglementClient.ClientCode, reglementClient.ClientIntitule, reglementClient.DeviseNo, soucheNo, representantNo, reglementClient.DeviseCours, commentaire, num, num)
		{
			UtilisateurNo = utilisateur.No,
			EcheanceReporte = reglementClient.DateEcheance,
			ExtraitNum = pieceTreso
		};
		reglementClient.DateModification = DateTime.Now;
		reglementClient.ModificateurNo = utilisateur.No;
		reglementClient.IsPointe = false;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		reglementClient.ChangeToImpaye(dateImpaye);
		if (useRegularisationPreavis)
		{
			reglementClient.Preavis = EtatPreavis.RegulariseImpaye;
		}
		_reglementClientRepository.Update(reglementClient);
		_notifyService.Notify(TypeEntity.Reglement, reglementNo, TypeAction.Modification, reglementClient.SocieteNo);
		_notifyService.Notify(TypeEntity.Bordereau, reglementClient.EnteteBordereauNo.GetValueOrDefault(), TypeAction.Modification, reglementClient.SocieteNo);
		int? num2 = _echeanceRepository.Create(echeance);
		_notifyService.Notify(TypeEntity.Echeance, num2.GetValueOrDefault(), TypeAction.Ajout, reglementClient.SocieteNo);
		transactionScope.Complete();
	}

	public void BordreauMettre(int bordereauNo, string piece, DateTime dateRemis)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		if (bordereauNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorBordereauNo);
		}
		if (string.IsNullOrEmpty(piece))
		{
			throw new ArgumentNullException("piece");
		}
		Bordereau bordereau = _bordereauRepository.Get(bordereauNo);
		if (bordereau == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorBordereauNo);
		}
		Caisse caisse = Get(bordereau.CaisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Opération invalide! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grc))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (bordereau.IsRemis)
		{
			throw new ApplicationException(TresorerieCoreMessages.InfoBordereauRemis);
		}
		if (bordereau.IsPointe)
		{
			throw new ApplicationException(TresorerieCoreMessages.InfoBordereauRapproche);
		}
		List<LigneBordereau> list = bordereau.GetLigneBordereaux().ToList();
		HistoriqueMvt historiqueMvt = _historiqueRepository.GetAllByCaisse(bordereau.CaisseNo, bordereau.DeviseNo).FirstOrDefault((HistoriqueMvt x) => x.MouvementOutNo == bordereauNo);
		if (!list.Any() && historiqueMvt != null)
		{
			VersementRemis(bordereauNo, piece, dateRemis);
			return;
		}
		if (!list.Any())
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorBordereauVide);
		}
		bordereau.IsRemis = true;
		bordereau.PieceNumero = piece;
		bordereau.DateRemis = dateRemis;
		bordereau.DateModification = DateTime.Now;
		bordereau.ModificateurNo = utilisateur.No;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_bordereauRepository.Update(bordereau);
		foreach (LigneBordereau item in list)
		{
			item.DateRemis = dateRemis;
			item.PieceNum = piece;
			_ligneBordereauRepository.Update(item, bordereau.CompteBanqueNo, isRemis: true);
			_notifyService.Notify(TypeEntity.Reglement, item.No, TypeAction.Modification, bordereau.SocieteNo);
		}
		_notifyService.Notify(TypeEntity.Bordereau, bordereauNo, TypeAction.Modification, bordereau.SocieteNo);
		transactionScope.Complete();
	}

	public void BordreauVirementMettre(int bordereauNo, string piece, DateTime dateRemis)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		if (bordereauNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorBordereauNo);
		}
		if (string.IsNullOrEmpty(piece))
		{
			throw new ArgumentNullException("piece");
		}
		BordereauVirement bordereauVirement = _bordereauVirementRepository.Get(bordereauNo);
		if (bordereauVirement == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorBordereauNo);
		}
		Caisse caisse = Get(bordereauVirement.CaisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Opération invalide! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grf))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (bordereauVirement.IsRemis)
		{
			throw new ApplicationException(TresorerieCoreMessages.InfoBordereauRemis);
		}
		if (bordereauVirement.IsPointe)
		{
			throw new ApplicationException(TresorerieCoreMessages.InfoBordereauRapproche);
		}
		if (!bordereauVirement.GetLigneBordereaux().ToList().Any())
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorBordereauVide);
		}
		bordereauVirement.IsRemis = true;
		bordereauVirement.PieceNumero = piece;
		bordereauVirement.DateRemis = dateRemis;
		bordereauVirement.DateModification = DateTime.Now;
		bordereauVirement.ModificateurNo = utilisateur.No;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_bordereauVirementRepository.Update(bordereauVirement);
		_notifyService.Notify(TypeEntity.BordereauVirement, bordereauNo, TypeAction.Modification, bordereauVirement.SocieteNo);
		transactionScope.Complete();
	}

	public void BordreauUpdate(int bordereauNo, string libelle, string pieceNumero, string infoLibre1, string infoLibre2, string infoLibre3, string infoLibre4)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		Bordereau bordereau = _bordereauRepository.Get(bordereauNo);
		if (bordereau == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorBordereauNo);
		}
		Caisse caisse = Get(bordereau.CaisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Opération invalide! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grc))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		bordereau.Libelle = libelle;
		bordereau.DateModification = DateTime.Now;
		bordereau.ModificateurNo = utilisateur.No;
		bordereau.InfoLibre1 = infoLibre1;
		bordereau.InfoLibre2 = infoLibre2;
		bordereau.InfoLibre3 = infoLibre3;
		bordereau.InfoLibre4 = infoLibre4;
		if (!bordereau.IsRemis)
		{
			bordereau.PieceNumero = pieceNumero;
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_bordereauRepository.Update(bordereau);
		_notifyService.Notify(TypeEntity.Bordereau, bordereauNo, TypeAction.Modification, bordereau.SocieteNo);
		transactionScope.Complete();
	}

	public void BordreauVirementUpdate(int bordereauNo, string libelle, string pieceNumero, bool isFichierGenere)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		BordereauVirement bordereauVirement = _bordereauVirementRepository.Get(bordereauNo);
		if (bordereauVirement == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorBordereauNo);
		}
		Caisse caisse = Get(bordereauVirement.CaisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Opération invalide! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grf))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		bordereauVirement.Libelle = libelle;
		bordereauVirement.DateModification = DateTime.Now;
		bordereauVirement.ModificateurNo = utilisateur.No;
		bordereauVirement.IsFichierGenere = isFichierGenere;
		if (!bordereauVirement.IsRemis)
		{
			bordereauVirement.PieceNumero = pieceNumero;
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_bordereauVirementRepository.Update(bordereauVirement);
		_notifyService.Notify(TypeEntity.BordereauVirement, bordereauNo, TypeAction.Modification, bordereauVirement.SocieteNo);
		transactionScope.Complete();
	}

	public void CaisseModeCreate(int caisseNo, int modeNo, string journalEncaissement, string compteGeneralEncaissement, string journalDecaissement, string comprteGeneralDecaissement, string compteGeneralImpayeEncaissement, string compteGeneralImpayeDecaissement, string compteGeneralAvanceEncaissement, string compteGeneralAvanceDecaissement, Societe societe = null)
	{
		societe = societe ?? SocieteManager.Societe;
		Caisse caisse = societe.GetCaisse(caisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException("caisse");
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Impossible d'affecter le mode! La caisse est en sommeil!");
		}
		SocieteModeReglement mode = societe.GetMode(modeNo) ?? throw new ArgumentNullException("mode");
		if (caisse.HasModeReglement(modeNo))
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorModeReglementAffectation);
		}
		CaisseModeReglement mode2 = new CaisseModeReglement(mode, caisseNo, caisse.SocieteNo)
		{
			JournalEncaissement = journalEncaissement,
			CompteGeneralEncaissement = compteGeneralEncaissement,
			JournalDecaissement = journalDecaissement,
			CompteGeneralDecaissement = comprteGeneralDecaissement,
			CompteGeneralImpayeEncaissement = compteGeneralImpayeEncaissement,
			CompteGeneralImpayeDecaissement = compteGeneralImpayeDecaissement,
			CompteGeneralAvanceEncaissement = compteGeneralAvanceEncaissement,
			CompteGeneralAvanceDecaissement = compteGeneralAvanceDecaissement
		};
		_caisseModeReglementRepository.Create(mode2);
	}

	public void CaisseModeDelete(CaisseModeReglement caisseModeReglement)
	{
		if (caisseModeReglement == null)
		{
			throw new ArgumentNullException("caisseModeReglement");
		}
		Caisse caisse = SocieteManager.Societe.GetCaisse(caisseModeReglement.CaisseNo);
		if (caisse == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorOperationInvalide);
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Impossible de supprimer le mode! La caisse est en sommeil!");
		}
		CaisseModeReglement mode = caisse.GetMode(caisseModeReglement.No);
		if (mode == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorOperationInvalide);
		}
		if (IsModeUsed(mode.No, caisse.No))
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorModeMvte);
		}
		_caisseModeReglementRepository.Delete(caisseModeReglement);
	}

	public void CaisseModeUpdate(CaisseModeReglement caisseModeReglement)
	{
		if (caisseModeReglement == null)
		{
			throw new ArgumentNullException("caisseModeReglement");
		}
		Societe societe = SocieteManager.Societe;
		if ((societe.GetCaisse(caisseModeReglement.CaisseNo) ?? throw new ArgumentNullException("Caisse!")).EnSommeil)
		{
			throw new ApplicationException("Impossible de modifier le mode! La caisse est en sommeil!");
		}
		if (societe.GetMode(caisseModeReglement.No) == null)
		{
			throw new ApplicationException($"Impossible de charger le mode [{caisseModeReglement.No}]!");
		}
		_caisseModeReglementRepository.Update(caisseModeReglement);
	}

	public void ComptabiliserBordereaux(int mvtNo, List<EcritureComptable> erpEcritures)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		Bordereau bordereau = BordereauGet(mvtNo);
		if (bordereau == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorBordereauNo);
		}
		if (bordereau.IsComptabilise)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorBordereauComptabilise);
		}
		foreach (EcritureComptable erpEcriture in erpEcritures)
		{
			_ecritureComptaRepository.Create(erpEcriture);
		}
		bordereau.ChangeEtatComptabilise(etat: true);
		bordereau.DateModification = DateTime.Now;
		bordereau.ModificateurNo = utilisateur.No;
		_bordereauRepository.Update(bordereau);
		_notifyService.Notify(TypeEntity.Bordereau, bordereau.No, TypeAction.Modification, bordereau.SocieteNo);
		transactionScope.Complete();
	}

	public void ComptabiliserDossier(int dossierNo)
	{
		if (dossierNo <= 0)
		{
			throw new InvalidOperationException("Le dossier est invalide!");
		}
		Societe societe = SocieteManager.Societe;
		DossierReglement dossierReglement = SocieteManager.GetDossierReglement(dossierNo) ?? throw new ArgumentNullException($"Impossible de charger le dossier [{dossierNo}]");
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_dossierReglementRepository.Comptabiliser(dossierNo);
		if (dossierReglement.Domaine == DomaineDossier.Fournisseur)
		{
			if (societe.TypeComptabilisationReglement == TypeComptabilisationReglementFrs.DateSysteme)
			{
				_dossierReglementRepository.UpdateDate(dossierNo, DateTime.Now.Date);
			}
			_notifyService.Notify(TypeEntity.Dossier, dossierNo, TypeAction.Modification, SocieteManager.Societe.No);
			transactionScope.Complete();
		}
		else
		{
			_notifyService.Notify(TypeEntity.DossierClient, dossierNo, TypeAction.Modification, SocieteManager.Societe.No);
			transactionScope.Complete();
		}
	}

	public void ComptabiliserEcheanceClientSpe(int echeanceNo, IList<EcritureComptable> erpEcritures)
	{
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		Echeance echeance = SocieteManager.EcheanceGet(echeanceNo);
		if (echeance == null)
		{
			throw new ApplicationException("Impossible de charger l'échéance [" + echeance.DocumentNumero + "]!");
		}
		if (echeance.IsComptabilise)
		{
			throw new ApplicationException("L'échéance [" + echeance.DocumentNumero + "] est comptabilisée !");
		}
		foreach (EcritureComptable erpEcriture in erpEcritures)
		{
			_ecritureComptaRepository.Create(erpEcriture);
		}
		echeance.IsComptabilise = true;
		echeance.IsSpe = 99;
		_echeanceRepository.Update(echeance);
		_notifyService.Notify(TypeEntity.Echeance, echeanceNo, TypeAction.Modification, echeance.SocieteNo);
		transactionScope.Complete();
	}

	public void SaveEcheanceEcritureTresorerie(int echeanceNo, IList<EcritureComptable> erpEcritures)
	{
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		Echeance echeance = SocieteManager.EcheanceGet(echeanceNo);
		if (echeance == null)
		{
			throw new ApplicationException("Impossible de charger l'échéance !");
		}
		bool flag = erpEcritures.FirstOrDefault()?.LigneVirementNo.HasValue ?? false;
		if (echeance.IsComptabilise && !flag)
		{
			throw new ApplicationException("L'échéance est comptabilisée !");
		}
		foreach (EcritureComptable erpEcriture in erpEcritures)
		{
			_ecritureComptaRepository.Create(erpEcriture);
		}
		_notifyService.Notify(TypeEntity.Echeance, echeanceNo, TypeAction.Modification, echeance.SocieteNo);
		transactionScope.Complete();
	}

	public void ComptabiliserEcart(int ecartNo, List<EcritureComptable> erpEcritures)
	{
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		Ecart ecart = SocieteManager.EcartGet(ecartNo);
		if (ecart == null)
		{
			throw new InvalidOperationException("l'écart est invalide!");
		}
		if (ecart.IsComptabilise == EtatComptabilite.Comptabilise)
		{
			throw new ApplicationException("L'échéance est invalide!");
		}
		foreach (EcritureComptable erpEcriture in erpEcritures)
		{
			_ecritureComptaRepository.Create(erpEcriture);
		}
		_ecartRepository.Comptabiliser(ecartNo);
		_notifyService.Notify(TypeEntity.Echeance, ecartNo, TypeAction.Modification, ecart.SocieteNo);
		transactionScope.Complete();
	}

	public void ComptabiliserEcartEchange(int ecartNo, List<EcritureComptable> erpEcritures)
	{
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		EcartEchange ecartEchange = SocieteManager.EcartEchangeGet(ecartNo);
		if (ecartEchange == null)
		{
			throw new InvalidOperationException("l'écart est invalide!");
		}
		if (ecartEchange.IsComptabilise == EtatComptabilite.Comptabilise)
		{
			throw new ApplicationException("L'échéance est invalide!");
		}
		foreach (EcritureComptable erpEcriture in erpEcritures)
		{
			_ecritureComptaRepository.Create(erpEcriture);
		}
		_ecartEchangeRepository.Comptabiliser(ecartNo, EtatComptabilite.Comptabilise);
		_notifyService.Notify(TypeEntity.Echeance, ecartNo, TypeAction.Modification, ecartEchange.SocieteNo);
		transactionScope.Complete();
	}

	public void ComptabiliserEcartEchangeDossier(int dossierNo, IList<EcritureComptable> erpEcritures)
	{
		if ((SocieteManager.GetDossierReglement(dossierNo) ?? throw new ApplicationException($"Impossible de charger le dossier de règlement [{dossierNo}].")).IsComptabilise)
		{
			throw new ApplicationException("Le dossier de règlement est comptabilisé.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		foreach (EcritureComptable erpEcriture in erpEcritures)
		{
			_ecritureComptaRepository.Create(erpEcriture);
		}
		transactionScope.Complete();
	}

	public void ComptabiliserFactureFournisseur(int factureNo, List<EcritureComptable> erpEcritures)
	{
		if (factureNo <= 0)
		{
			throw new ArgumentNullException("factureNo");
		}
		if (erpEcritures == null)
		{
			throw new ArgumentNullException("erpEcritures");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		Echeance echeance = SocieteManager.EcheanceGet(factureNo);
		if (echeance == null)
		{
			throw new InvalidOperationException("Impossible de charger la facture.");
		}
		if (echeance.Type != EcheanceType.FactureFrsTresorerie)
		{
			throw new InvalidOperationException("Type d'échéance invalid !");
		}
		if (echeance.IsComptabilise)
		{
			throw new ApplicationException("La facture [" + echeance.DocumentNumero + "] est comptabilisée");
		}
		foreach (EcritureComptable erpEcriture in erpEcritures)
		{
			_ecritureComptaRepository.Create(erpEcriture);
		}
		echeance.IsComptabilise = true;
		_echeanceRepository.Update(echeance);
		_notifyService.Notify(TypeEntity.Echeance, factureNo, TypeAction.Modification, echeance.SocieteNo);
		transactionScope.Complete();
	}

	public void ComptabiliserImpaye(int reglementNo, List<EcritureComptable> erpEcritures)
	{
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		Impaye impaye = ImpayeGet(reglementNo);
		if (impaye == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorImpayeInvalid);
		}
		if (impaye.IsComptabilise == EtatComptabilite.Comptabilise)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorImpayeComptabilise);
		}
		foreach (EcritureComptable erpEcriture in erpEcritures)
		{
			_ecritureComptaRepository.Create(erpEcriture);
		}
		impaye.ChangeEtatComptabilise(EtatComptabilite.Comptabilise);
		_impayeRepository.Update(impaye);
		_notifyService.Notify(TypeEntity.Reglement, impaye.ReglementNo, TypeAction.Modification, impaye.SocieteNo);
		_notifyService.Notify(TypeEntity.Echeance, impaye.EcheanceNo, TypeAction.Modification, impaye.SocieteNo);
		transactionScope.Complete();
	}

	public void ComptabiliserImpayeFournisseur(int reglementNo, List<EcritureComptable> erpEcritures)
	{
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		ImpayeFournisseur impayeFournisseur = ImpayeFournisseurGet(reglementNo);
		if (impayeFournisseur == null)
		{
			throw new ApplicationException($"Impossible de charger l'impayé [{reglementNo}].");
		}
		if (impayeFournisseur.IsComptaImpaye)
		{
			throw new ApplicationException("L'impayé [" + impayeFournisseur.Numero + "] est comptabilisé.");
		}
		foreach (EcritureComptable erpEcriture in erpEcritures)
		{
			_ecritureComptaRepository.Create(erpEcriture);
		}
		impayeFournisseur.ChangeEtatComptabilise(etat: true);
		_impayeFournisseurRepository.Update(impayeFournisseur);
		_notifyService.Notify(TypeEntity.ReglementFournisseur, impayeFournisseur.No, TypeAction.Modification, impayeFournisseur.SocieteNo);
		_notifyService.Notify(TypeEntity.ImpayeFournisseur, impayeFournisseur.No, TypeAction.Modification, impayeFournisseur.SocieteNo);
		transactionScope.Complete();
	}

	public void ComptabiliserReglement(int mvtNo, List<EcritureComptable> erpEcritures)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		ReglementClient reglementClient = ReglementGet(mvtNo);
		if (reglementClient == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementNo);
		}
		if (reglementClient.IsComptabilise == EtatComptabilite.Comptabilise)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglementComptabilise);
		}
		if (reglementClient.IsAnnule)
		{
			throw new ApplicationException("Le règlement est annulé!");
		}
		foreach (EcritureComptable erpEcriture in erpEcritures)
		{
			_ecritureComptaRepository.Create(erpEcriture);
		}
		reglementClient.ChangeEtatComptabilise(EtatComptabilite.Comptabilise);
		reglementClient.DateModification = DateTime.Now;
		reglementClient.ModificateurNo = utilisateur.No;
		_reglementClientRepository.Update(reglementClient);
		_notifyService.Notify(TypeEntity.Reglement, reglementClient.No, TypeAction.Modification, reglementClient.SocieteNo);
		transactionScope.Complete();
	}

	public void ComptabiliserReglementAvance(int mvtNo, List<EcritureComptable> erpEcritures)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		ReglementClient reglementClient = ReglementGet(mvtNo);
		if (reglementClient == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementNo);
		}
		if (reglementClient.IsComptabilise == EtatComptabilite.Comptabilise)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglementComptabilise);
		}
		if (reglementClient.IsAvanceComptabilise)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglementComptabilise);
		}
		if (reglementClient.IsAnnule)
		{
			throw new ApplicationException("Le règlement est annulé!");
		}
		foreach (EcritureComptable erpEcriture in erpEcritures)
		{
			_ecritureComptaRepository.Create(erpEcriture);
		}
		reglementClient.IsAvanceComptabilise = true;
		reglementClient.DateModification = DateTime.Now;
		reglementClient.ModificateurNo = utilisateur.No;
		_reglementClientRepository.Update(reglementClient);
		_notifyService.Notify(TypeEntity.Reglement, reglementClient.No, TypeAction.Modification, reglementClient.SocieteNo);
		transactionScope.Complete();
	}

	public void ComptabiliserReglementFournisseurAvance(int mvtNo, List<EcritureComptable> erpEcritures)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		ReglementFournisseur reglementFournisseur = ReglementFournisseurGet(mvtNo);
		if (reglementFournisseur == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementNo);
		}
		if (reglementFournisseur.IsComptabilise == EtatComptabilite.Comptabilise)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglementComptabilise);
		}
		if (reglementFournisseur.IsAvanceComptabilise)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglementComptabilise);
		}
		if (reglementFournisseur.IsAnnule)
		{
			throw new ApplicationException("Le règlement est annulé!");
		}
		foreach (EcritureComptable erpEcriture in erpEcritures)
		{
			_ecritureComptaRepository.Create(erpEcriture);
		}
		reglementFournisseur.IsAvanceComptabilise = true;
		reglementFournisseur.DateModification = DateTime.Now;
		reglementFournisseur.ModificateurNo = utilisateur.No;
		_reglementFournisseurRepository.Update(reglementFournisseur);
		_notifyService.Notify(TypeEntity.ReglementFournisseur, reglementFournisseur.No, TypeAction.Modification, reglementFournisseur.SocieteNo);
		transactionScope.Complete();
	}

	public void ConfirmerAvanceReglement(int reglementNo)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		ReglementClient reglementClient = ReglementGet(reglementNo);
		if (reglementClient == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementNo);
		}
		if (reglementClient.IsComptabilise == EtatComptabilite.Comptabilise)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglementComptabilise);
		}
		if (reglementClient.ReglementNature != ReglementNature.Avance)
		{
			throw new ApplicationException("Le règlement ne constitue pas une avance.");
		}
		if (reglementClient.IsAnnule)
		{
			throw new ApplicationException("Le règlement est annulé!");
		}
		if (!reglementClient.IsAvanceComptabilise)
		{
			throw new ApplicationException("L'avance n'est pas comptabilisée.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		reglementClient.ReglementNature = ReglementNature.Reglement;
		reglementClient.DateModification = DateTime.Now;
		reglementClient.ModificateurNo = utilisateur.No;
		_reglementClientRepository.Update(reglementClient);
		_notifyService.Notify(TypeEntity.Reglement, reglementClient.No, TypeAction.Modification, reglementClient.SocieteNo);
		transactionScope.Complete();
	}

	public void ConfirmerAvanceReglementFournisseur(int reglementNo)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		ReglementFournisseur reglementFournisseur = ReglementFournisseurGet(reglementNo);
		if (reglementFournisseur == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementNo);
		}
		if (reglementFournisseur.IsComptabilise == EtatComptabilite.Comptabilise)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglementComptabilise);
		}
		if (reglementFournisseur.ReglementNature != ReglementNature.Avance)
		{
			throw new ApplicationException("Le règlement ne constitue pas une avance.");
		}
		if (reglementFournisseur.IsAnnule)
		{
			throw new ApplicationException("Le règlement est annulé!");
		}
		if (!reglementFournisseur.IsAvanceComptabilise)
		{
			throw new ApplicationException("L'avance n'est pas comptabilisée.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		reglementFournisseur.ReglementNature = ReglementNature.Reglement;
		reglementFournisseur.DateModification = DateTime.Now;
		reglementFournisseur.ModificateurNo = utilisateur.No;
		_reglementFournisseurRepository.Update(reglementFournisseur);
		_notifyService.Notify(TypeEntity.ReglementFournisseur, reglementFournisseur.No, TypeAction.Modification, reglementFournisseur.SocieteNo);
		transactionScope.Complete();
	}

	public void ComptabiliserReglementComptaToAnnuler(int mvtNo, List<EcritureComptable> erpEcritures)
	{
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		ReglementClient obj = ReglementGet(mvtNo) ?? throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementNo);
		if (!obj.IsAnnule)
		{
			throw new ApplicationException("Règlement n'est pas annulé.");
		}
		if (obj.IsComptabilise != EtatComptabilite.Comptabilise)
		{
			throw new ApplicationException("Règlement n'est pas comptabilisé!");
		}
		foreach (EcritureComptable erpEcriture in erpEcritures)
		{
			_ecritureComptaRepository.Create(erpEcriture);
		}
		transactionScope.Complete();
	}

	public void ComptabiliserReglementFournisseur(int mvtNo, IEnumerable<EcritureComptable> erpEcritures)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		Societe societe = SocieteManager.Societe;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		ReglementFournisseur reglementFournisseur = ReglementFournisseurGet(mvtNo);
		if (reglementFournisseur == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementNo);
		}
		if (reglementFournisseur.IsComptabilise == EtatComptabilite.Comptabilise)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglementComptabilise);
		}
		if (reglementFournisseur.IsAnnule)
		{
			throw new ApplicationException("Le règlement est annulé.");
		}
		foreach (EcritureComptable erpEcriture in erpEcritures)
		{
			_ecritureComptaRepository.Create(erpEcriture);
		}
		_reglementFournisseurRepository.UpdateEtatCompta(reglementFournisseur.No, EtatComptabilite.Comptabilise, utilisateur.No);
		if (societe.TypeComptabilisationReglement == TypeComptabilisationReglementFrs.DateSysteme)
		{
			_reglementFournisseurRepository.UpdateReglementDate(reglementFournisseur.No, utilisateur.No, DateTime.Now.Date);
		}
		_notifyService.Notify(TypeEntity.ReglementFournisseur, reglementFournisseur.No, TypeAction.Modification, reglementFournisseur.SocieteNo);
		transactionScope.Complete();
	}

	public void ComptabiliserReglementFournisseurTraite(int mvtNo, List<EcritureComptable> erpEcritures)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		ReglementFournisseur reglementFournisseur = ReglementFournisseurGet(mvtNo);
		if (reglementFournisseur == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementNo);
		}
		if (reglementFournisseur.DateEcheance.Date > DateTime.Now.Date)
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorReglementComptabiliseDateEcheanceTraite, reglementFournisseur.Numero));
		}
		if (reglementFournisseur.IsAnnule)
		{
			throw new ApplicationException("Le règlement est annulé.");
		}
		if (SocieteManager.Societe.RecupererPieceFrs && reglementFournisseur.Recuperer == RemisFournisseur.NonRecuperer)
		{
			throw new ApplicationException("Le règlement n°[" + reglementFournisseur.Numero + "] n'est pas encore remis au fournisseur [" + reglementFournisseur.FournisseurIntitule + "]");
		}
		foreach (EcritureComptable erpEcriture in erpEcritures)
		{
			_ecritureComptaRepository.Create(erpEcriture);
		}
		_reglementFournisseurRepository.UpdateEtatComptaTraiteFrs(reglementFournisseur.No, EtatComptabilite.TraiteFournisseurComptabilise, utilisateur.No);
		_notifyService.Notify(TypeEntity.ReglementFournisseur, reglementFournisseur.No, TypeAction.Modification, reglementFournisseur.SocieteNo);
		transactionScope.Complete();
	}

	public void ComptabiliserRemboursement(int no, List<EcritureComptable> erpEcritures)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		RemboursementClientAllType remboursementClientAllType = _remboursementClientAllTypeRepository.Get(no);
		if (remboursementClientAllType == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorRemboursementInvalid);
		}
		if (remboursementClientAllType.Comptabilise != EtatComptabilite.NonComptabilise)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorRemboursementComptabilise);
		}
		foreach (EcritureComptable erpEcriture in erpEcritures)
		{
			_ecritureComptaRepository.Create(erpEcriture);
		}
		_remboursementClientAllTypeRepository.UpdateEtatComptabilisation(no, EtatComptabilite.Comptabilise);
		_notifyService.Notify(TypeEntity.RemboursementClient, no, TypeAction.Modification, remboursementClientAllType.SocieteNo);
		transactionScope.Complete();
	}

	public void ComptabiliserRemboursementFournisseur(int remboursementNo, List<EcritureComptable> erpEcritures)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		RemboursementFournisseur remboursementFournisseur = _remboursementFournisseurRepository.Get(remboursementNo);
		if (remboursementFournisseur == null)
		{
			throw new InvalidOperationException($"Impossible de charger le remboursement fournisseur [{remboursementNo}]!");
		}
		if (remboursementFournisseur.Comptabilise != EtatComptabilite.NonComptabilise)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorRemboursementComptabilise);
		}
		foreach (EcritureComptable erpEcriture in erpEcritures)
		{
			_ecritureComptaRepository.Create(erpEcriture);
		}
		_remboursementFournisseurRepository.UpdateEtatComptabilisation(remboursementNo, EtatComptabilite.Comptabilise);
		ReglementClient reglementClient = _reglementClientRepository.Get(remboursementFournisseur.ReglementNo);
		if (reglementClient == null)
		{
			throw new InvalidOperationException("Impossible de charger le règlement du remboursement [" + remboursementFournisseur.Numero + "].");
		}
		if (reglementClient.IsComptabilise != EtatComptabilite.NonComptabilise)
		{
			throw new ApplicationException("Le règlement [" + reglementClient.Numero + "] est comptabilisé");
		}
		if (reglementClient.TiersType != TiersType.Fournisseur)
		{
			throw new ApplicationException("Le type tiers du règlement [" + reglementClient.Numero + "] est invalide!");
		}
		reglementClient.ChangeEtatComptabilise(EtatComptabilite.Comptabilise);
		reglementClient.DateModification = DateTime.Now;
		reglementClient.ModificateurNo = utilisateur.No;
		_reglementClientRepository.Update(reglementClient);
		_notifyService.Notify(TypeEntity.Reglement, reglementClient.No, TypeAction.Modification, reglementClient.SocieteNo);
		_notifyService.Notify(TypeEntity.RemboursementFournisseur, remboursementNo, TypeAction.Modification, remboursementFournisseur.SocieteNo);
		transactionScope.Complete();
	}

	public void ComptabiliserRemplacement(int mvtNo, List<EcritureComptable> erpEcritures)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		ReglementClient reglementClient = ReglementGet(mvtNo);
		if (reglementClient == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementNo);
		}
		if (reglementClient.IsComptabilise != EtatComptabilite.Comptabilise)
		{
			throw new ApplicationException("le règlement [" + reglementClient.Numero + "] n'est pas comptabilisé!");
		}
		if (reglementClient.IsRemplace != Remplace.TotalementRemplace)
		{
			throw new ApplicationException("Le règlement [" + reglementClient.Numero + "] n'est pas remplacé!");
		}
		foreach (EcritureComptable erpEcriture in erpEcritures)
		{
			_ecritureComptaRepository.Create(erpEcriture);
		}
		reglementClient.IsRemplacmentComptabilise = EtatComptabilite.Comptabilise;
		reglementClient.DateModification = DateTime.Now;
		reglementClient.ModificateurNo = utilisateur.No;
		_reglementClientRepository.Update(reglementClient);
		_notifyService.Notify(TypeEntity.Reglement, reglementClient.No, TypeAction.Modification, reglementClient.SocieteNo);
		transactionScope.Complete();
	}

	public void ComptabiliserVirementInterne(int no, List<EcritureComptable> erpEcritures)
	{
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		VirementInterne virementInterne = VirementInterneGet(no);
		if (virementInterne == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorVirementInterneMvtInvalid);
		}
		if (virementInterne.IsComptabilise == EtatComptabilite.Comptabilise)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorVirementInterneComptabilise);
		}
		foreach (EcritureComptable erpEcriture in erpEcritures)
		{
			_ecritureComptaRepository.Create(erpEcriture);
		}
		_virementInterneRepository.Comptabiliser(no);
		_notifyService.Notify(TypeEntity.VirementInterne, no, TypeAction.Modification, virementInterne.SocieteNo);
		transactionScope.Complete();
	}

	public void ComptabiliserVirementTiers(int no, List<EcritureComptable> erpEcritures)
	{
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		VirementTiers virementTiers = VirementTiersGet(no);
		if (virementTiers == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorVirementInterneMvtInvalid);
		}
		if (virementTiers.IsComptabilise == EtatComptabilite.Comptabilise)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorVirementInterneComptabilise);
		}
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		foreach (EcritureComptable erpEcriture in erpEcritures)
		{
			erpEcriture.SetDefaultStringEmpty();
			_ecritureComptaRepository.Create(erpEcriture);
		}
		if (virementTiers.TypeTiers == TiersType.Client)
		{
			foreach (LigneVirementTiers ligne in virementTiers.GetLignes())
			{
				_ligneVirementTiersRepository.VirClientUpdateEtatCompta(ligne.No, EtatComptabilite.Comptabilise);
				_notifyService.Notify(TypeEntity.RemboursementClient, ligne.No, TypeAction.Modification, virementTiers.SocieteNo);
			}
		}
		else
		{
			foreach (LigneVirementTiers ligne2 in virementTiers.GetLignes())
			{
				_reglementFournisseurRepository.UpdateEtatCompta(ligne2.No, EtatComptabilite.Comptabilise, utilisateur.No);
				_notifyService.Notify(TypeEntity.ReglementFournisseur, ligne2.No, TypeAction.Modification, virementTiers.SocieteNo);
			}
		}
		_virementTiersRepository.Comptabiliser(no);
		_notifyService.Notify(TypeEntity.VirementTiers, no, TypeAction.Modification, virementTiers.SocieteNo);
		transactionScope.Complete();
	}

	public int? Create(Caisse caisse, Societe societe = null)
	{
		if (caisse == null)
		{
			throw new ArgumentNullException("caisse");
		}
		societe = societe ?? SocieteManager.Societe;
		if (societe.GetCaisse(caisse.Code) != null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorCaisseExist);
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Impossible de créer une caisse en sommeil!");
		}
		if (caisse.Depense == 0 && caisse.Recette == 0)
		{
			throw new InvalidOperationException("Le type de la caisse est obligatoire!");
		}
		return _caisseRepository.Create(caisse);
	}

	public RemboursementFournisseur RemboursementFournisseurGet(int no)
	{
		return _remboursementFournisseurRepository.Get(no);
	}

	public RemboursementFournisseur RemboursementFournisseurGetByReglement(int reglementNo)
	{
		return _remboursementFournisseurRepository.GetByReglementNo(reglementNo);
	}

	public async Task<IEnumerable<RemboursementFournisseur>> GetAllRemboursementFournisseurAComptaAsync(DateTime dateDe, DateTime dateA, EtatComptabilite etat, CancellationToken cancellationToken)
	{
		Societe societe = SocieteManager.Societe;
		return await _remboursementFournisseurRepository.GetAllAComptaAsync(dateDe.Date, dateA.Date.AddDays(1.0), etat, societe.No, cancellationToken);
	}

	public IEnumerable<RemboursementFournisseur> RemboursementFournisseurGetAll(Societe societe = null)
	{
		societe = societe ?? SocieteManager.Societe;
		ProfilType profilType = SocieteManager.Groupe.ProfilType;
		if (SocieteManager.Utilisateur.IsAdmin)
		{
			return _remboursementFournisseurRepository.GetAll(societe.No);
		}
		int[] caisses = (from a in _autorisationCaisseRepository.GetAll(SocieteManager.Utilisateur.No, societe.No, profilType)
			select a.CaisseNo).ToArray();
		return from x in _remboursementFournisseurRepository.GetAll(societe.No)
			where caisses.Any((int y) => y == x.CaisseNo)
			orderby x.Date descending
			select x;
	}

	public int CreateRemboursementFournisseur(string numero, DateTime date, decimal montant, int deviseNo, int fournisseurNo, string fournisseurCode, string fournisseurIntitule, int modeNo, int caisseNo, string piece, string libelle, string tire, DateTime dateEcheance, int? banqueNo, string fournisseurBanque, string fournisseurRIB, decimal coursDevise, int deviseSocieteNo, string affaireNumero, bool fournisseurIsBloquer, bool isCertifier, DateTime? dateValidite, decimal? montantPlafond)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		Societe societe = SocieteManager.Societe;
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		Caisse caisse = _caisseRepository.Get(caisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException($"Caisse [{caisseNo}] invalide!");
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Impossible de créer le remboursement! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisseNo))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (coursDevise <= 0m)
		{
			throw new ApplicationException("Le cours est invalide!");
		}
		if (societe.BloquerRembourcementClient && fournisseurIsBloquer)
		{
			throw new ApplicationException("Le client " + fournisseurCode + " est bloqué.");
		}
		if (banqueNo.HasValue)
		{
			InformationsBanque byBanqueId = SocieteManager.Groupe.InformationBanqueManager.GetByBanqueId(banqueNo.Value);
			if (byBanqueId != null && byBanqueId.EnSommeil)
			{
				throw new ApplicationException("Impossible de créer le remboursement! La banque est en sommeil.");
			}
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		int num = ReglementAutreCreate0(numero, date, montant, deviseNo, fournisseurNo, fournisseurCode, fournisseurIntitule, TiersType.Fournisseur, modeNo, caisseNo, piece, libelle, tire, dateEcheance, banqueNo, fournisseurBanque, coursDevise, deviseSocieteNo, affaireNumero, fournisseurRIB, string.Empty, string.Empty, string.Empty, string.Empty, isCertifier, dateValidite, montantPlafond);
		SocieteDevise defaultDeviseSociete = societe.GetDefaultDeviseSociete();
		decimal num2 = Math.Round(montant * coursDevise, defaultDeviseSociete.NombreDecimales, MidpointRounding.AwayFromZero);
		if (!societe.VirementDefaultSouche.HasValue)
		{
			throw new InvalidOperationException("Impossible de charger la configuration de la souche par défaut!");
		}
		Echeance echeance = new Echeance(0, numero.ToUpper(), ErpDomaine.Achat, ErpDocumentType.None, date, montant, montant, date, EcheanceType.RemboursementFournisseur, modeNo, societe.No, fournisseurNo, fournisseurCode, fournisseurIntitule, fournisseurNo, fournisseurCode, fournisseurIntitule, deviseNo, societe.VirementDefaultSouche.Value, 0, coursDevise, libelle, num2, num2)
		{
			RemboursementClientNo = num,
			RemboursementClientNumero = numero,
			IsComptaRemboursementClient = false,
			CaisseNo = caisse.No,
			DatePointeRemboursementCheque = DateHelper.GetMinSqlDateTime(),
			UtilisateurNo = utilisateur.No,
			EcheanceReporte = date
		};
		int? num3 = _echeanceRepository.Create(echeance);
		if (!num3.HasValue)
		{
			throw new ApplicationException("EcheanceNo invalide!");
		}
		_notifyService.Notify(TypeEntity.Echeance, num3.Value, TypeAction.Ajout, societe.No);
		_notifyService.Notify(TypeEntity.RemboursementFournisseur, num3.Value, TypeAction.Ajout, societe.No);
		transactionScope.Complete();
		return num;
	}

	public void CreateRemboursementClientEspece(string numero, DateTime date, int deviseNo, decimal cours, int clientNo, string clientCode, string clientIntitule, int caisseNo, string libelle, Dictionary<int, decimal> lots, bool clientIsBloquer)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		Societe societe = SocieteManager.Societe;
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		Caisse caisse = _caisseRepository.Get(caisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException("Caisse invalide!");
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Impossible de crée le remboursement! La caisse est en sommeil!");
		}
		if (societe.BloquerRembourcementClient && clientIsBloquer)
		{
			throw new ApplicationException("Le client " + clientCode + " est bloqué.");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisseNo))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (cours <= 0m)
		{
			throw new ApplicationException("Le cours est invalide!");
		}
		if (lots.Any((KeyValuePair<int, decimal> x) => x.Key <= 0 || x.Value <= 0m))
		{
			throw new InvalidOperationException("Montant lot invalide!");
		}
		List<HistoriqueMvt> list = new List<HistoriqueMvt>();
		foreach (int key in lots.Keys)
		{
			IEnumerable<HistoriqueMvt> allByMouvement = _historiqueRepository.GetAllByMouvement(key);
			list.AddRange(allByMouvement);
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		decimal num = lots.Sum((KeyValuePair<int, decimal> x) => x.Value);
		SocieteDevise defaultDeviseSociete = societe.GetDefaultDeviseSociete();
		decimal num2 = Math.Round(num * cours, defaultDeviseSociete.NombreDecimales, MidpointRounding.AwayFromZero);
		RemboursementClient remboursementClient = new RemboursementClient
		{
			Numero = numero,
			CaisseNo = caisse.No,
			SocieteNo = societe.No,
			ClientNo = clientNo,
			Date = date,
			DeviseNo = deviseNo,
			Libelle = libelle,
			Montant = num,
			ModeReglementNo = list.First().ModeReglementNo,
			Comptabilise = EtatComptabilite.NonComptabilise,
			Cours = cours,
			MontantDeviseSociete = num2,
			DateModification = DateTime.Now,
			ModificateurNo = utilisateur.No,
			UtilisateurNo = utilisateur.No,
			DateCreation = DateTime.Now
		};
		int? num3 = _remboursementClientRepository.Create(remboursementClient);
		if (!num3.HasValue)
		{
			throw new InvalidOperationException("Remboursement [Creation]!");
		}
		foreach (HistoriqueMvt item in list)
		{
			if (item.Sens != SensMouvement.Sortie)
			{
				Lot lot = item.Lot;
				decimal value = lots.Single((KeyValuePair<int, decimal> x) => x.Key == lot.No).Value;
				lot.MontantRestant -= value;
				_historiqueRepository.Update(item);
				HistoriqueMvt historiqueMvt = new HistoriqueMvt(lot.No, caisseNo, SensMouvement.Sortie, MouvementDomaine.RemboursementClient, item.ModeReglementNo, lot.No, num3.Value, StatutTransfert.None, deviseNo, new Lot(lot.No, value, 0m));
				_historiqueRepository.Create(historiqueMvt);
			}
		}
		if (!societe.VirementDefaultSouche.HasValue)
		{
			throw new InvalidOperationException("Impossible de charger la configuration de la souche par défaut!");
		}
		Echeance echeance = new Echeance(0, numero.ToUpper(), ErpDomaine.Vente, ErpDocumentType.None, date, num, num, date, EcheanceType.RemboursementClientEspece, list.First().ModeReglementNo, societe.No, clientNo, clientCode, clientIntitule, clientNo, clientCode, clientIntitule, deviseNo, societe.VirementDefaultSouche.Value, 0, cours, libelle, num2, num2)
		{
			RemboursementClientNo = num3.Value,
			RemboursementClientNumero = numero,
			IsComptaRemboursementClient = false,
			CaisseNo = caisse.No,
			DatePointeRemboursementCheque = DateHelper.GetMinSqlDateTime(),
			UtilisateurNo = utilisateur.No,
			EcheanceReporte = date
		};
		int? num4 = _echeanceRepository.Create(echeance);
		if (!num4.HasValue)
		{
			throw new ApplicationException("EcheanceNo invalide!");
		}
		_notifyService.Notify(TypeEntity.Echeance, num4.Value, TypeAction.Ajout, remboursementClient.SocieteNo);
		_notifyService.Notify(TypeEntity.RemboursementClient, num4.Value, TypeAction.Ajout, remboursementClient.SocieteNo);
		transactionScope.Complete();
	}

	public void CreateRemboursementClientRS(string numero, DateTime date, decimal montant, int caisseNo, int modeNo, int deviseNo, int clientNo, string clientCode, string clientIntitule, string libelle, int echeanceAvoirNo, bool clientIsBloquer)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		if (string.IsNullOrEmpty(numero))
		{
			throw new ApplicationException("Le numéro est invalide!");
		}
		if (caisseNo <= 0)
		{
			throw new ApplicationException("La caisse No est invalide!");
		}
		if (modeNo <= 0)
		{
			throw new ApplicationException("Le mode No est invalide!");
		}
		if (montant <= 0m)
		{
			throw new ApplicationException("Le montant est invalide!");
		}
		if (string.IsNullOrEmpty(libelle))
		{
			throw new ApplicationException("Le libellé est invalide!");
		}
		if (clientNo <= 0)
		{
			throw new ApplicationException("Client invalide!");
		}
		Societe societe = SocieteManager.Societe;
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		Caisse caisse = Get(caisseNo);
		if (caisse == null)
		{
			throw new ArgumentException("La caisse de la dépense est invalide!");
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Opération invalide! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisseNo))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		CaisseModeReglement obj = caisse.GetMode(modeNo) ?? throw new ArgumentException("Le mode de règlement est invalide!");
		if (!caisse.HasModeReglement(modeNo))
		{
			throw new ArgumentException("Le mode de règlement est invalide!");
		}
		if (!obj.IsRetenu)
		{
			throw new ArgumentException("Le mode de règlement est invalide!");
		}
		Echeance echeance = _echeanceRepository.Get(echeanceAvoirNo);
		if (echeance == null)
		{
			throw new ApplicationException("Impossible de charger l'échéance d'avoir du remboursement [" + numero + "].");
		}
		if (societe.BloquerRembourcementClient && clientIsBloquer)
		{
			throw new ApplicationException("Le client " + clientCode + " est bloqué.");
		}
		if (echeance.Type != EcheanceType.Erp)
		{
			throw new ApplicationException("L'échéance [" + echeance.DocumentNumero + "] n'est pas un document ERP.");
		}
		if (echeance.RemoursementEcheanceAvoirNo.HasValue)
		{
			throw new ApplicationException("L'avoir [" + echeance.DocumentNumero + "] possède déjà un remboursement.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		RemboursementClient rembourcementClient = new RemboursementClient
		{
			Numero = numero,
			CaisseNo = caisse.No,
			SocieteNo = societe.No,
			ClientNo = clientNo,
			Date = date,
			DeviseNo = deviseNo,
			Libelle = libelle,
			Montant = montant,
			ModeReglementNo = modeNo,
			Comptabilise = EtatComptabilite.NonComptabilise,
			Cours = 1m,
			MontantDeviseSociete = montant,
			DateModification = DateTime.Now,
			ModificateurNo = utilisateur.No,
			UtilisateurNo = utilisateur.No,
			DateCreation = DateTime.Now
		};
		int? remoursementEcheanceAvoirNo = _remboursementClientRepository.Create(rembourcementClient);
		if (!remoursementEcheanceAvoirNo.HasValue)
		{
			throw new InvalidOperationException("Remboursement [Creation]!");
		}
		if (!societe.VirementDefaultSouche.HasValue)
		{
			throw new InvalidOperationException("Veuillez configurer la souche virement par défaut");
		}
		Echeance echeance2 = new Echeance(0, numero.ToUpper(), ErpDomaine.Vente, ErpDocumentType.None, date, montant, montant, date, EcheanceType.RemboursementClientRS, modeNo, societe.No, clientNo, clientCode, clientIntitule, clientNo, clientCode, clientIntitule, deviseNo, societe.VirementDefaultSouche.Value, 0, 1m, libelle, montant, montant)
		{
			RemboursementClientNo = remoursementEcheanceAvoirNo.Value,
			RemboursementClientNumero = numero,
			IsComptaRemboursementClient = false,
			CaisseNo = caisse.No,
			DatePointeRemboursementCheque = DateHelper.GetMinSqlDateTime(),
			UtilisateurNo = utilisateur.No,
			Reference = echeance.DocumentNumero,
			EcheanceReporte = date
		};
		int? num = _echeanceRepository.Create(echeance2);
		if (!num.HasValue)
		{
			throw new ApplicationException("EchéanceNo invalide!");
		}
		echeance.RemoursementEcheanceAvoirNo = remoursementEcheanceAvoirNo;
		_echeanceRepository.Update(echeance);
		_notifyService.Notify(TypeEntity.Echeance, echeance.No, TypeAction.Modification, societe.No);
		_notifyService.Notify(TypeEntity.Echeance, num.Value, TypeAction.Ajout, societe.No);
		_notifyService.Notify(TypeEntity.RemboursementClient, num.Value, TypeAction.Ajout, societe.No);
		transactionScope.Complete();
	}

	public void CreateRemboursementCheque(string numero, DateTime date, decimal montant, string pieceNumero, int caisseNo, int modeNo, int deviseNo, decimal cours, int clientNo, string clientCode, string clientIntitule, DateTime echeance, string libelle, string tire, int banqueNo, bool clientIsBloquer)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		if (string.IsNullOrEmpty(numero))
		{
			throw new ApplicationException("Le numéro est invalide!");
		}
		if (caisseNo <= 0)
		{
			throw new ApplicationException("La caisse No est invalide!");
		}
		if (modeNo <= 0)
		{
			throw new ApplicationException("Le mode No est invalide!");
		}
		if (string.IsNullOrEmpty(pieceNumero))
		{
			throw new ApplicationException("Numéro du pièce invalide!");
		}
		if (montant <= 0m)
		{
			throw new ApplicationException("Le montant est invalide!");
		}
		if (cours <= 0m)
		{
			throw new ApplicationException("Le cours est invalide!");
		}
		if (string.IsNullOrEmpty(libelle))
		{
			throw new ApplicationException("Le libellé est invalide!");
		}
		if (clientNo <= 0)
		{
			throw new ApplicationException("Client invalide!");
		}
		if (banqueNo <= 0)
		{
			throw new ApplicationException("Banque invalide!");
		}
		InformationsBanque byBanqueId = SocieteManager.Groupe.InformationBanqueManager.GetByBanqueId(banqueNo);
		if (byBanqueId != null && byBanqueId.EnSommeil)
		{
			throw new ApplicationException("La banque est en sommeil");
		}
		Societe societe = SocieteManager.Societe;
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		Caisse caisse = Get(caisseNo);
		if (caisse == null)
		{
			throw new ArgumentException("La caisse de la dépense est invalide!");
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Opération invalide! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisseNo))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		CaisseModeReglement obj = caisse.GetMode(modeNo) ?? throw new ArgumentException("Le mode de règlement est invalide!");
		if (obj.EnSommeil)
		{
			throw new ArgumentException("Opération invalide! Le mode est en sommeil!");
		}
		if (!caisse.HasModeReglement(modeNo))
		{
			throw new ArgumentException("Le mode de règlement est invalide!");
		}
		if (obj.Type != ReglementType.Cheque)
		{
			throw new ArgumentException("Le type du mode de règlement est invalide!");
		}
		Cheque cheque = SocieteManager.ChequierGetAll(banqueNo).SingleOrDefault((Chequier x) => x.GetCheques().Any((Cheque c) => c.Numero == pieceNumero))?.GetCheque(pieceNumero);
		if (cheque == null)
		{
			throw new ApplicationException("Chèque invalide!");
		}
		if (cheque.Statut != ChequeStatut.NonUtilise)
		{
			throw new ApplicationException("Chèque déjà utilisé!");
		}
		if ((SocieteManager.ChequierGet(cheque.ChequierNo) ?? throw new ApplicationException("Chéquier invalide!")).BanqueNo != banqueNo)
		{
			throw new ApplicationException("Banque chéquier invalide!");
		}
		if (societe.BloquerRembourcementClient && clientIsBloquer)
		{
			throw new ApplicationException("Le client " + clientCode + " est bloqué.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		cheque.Statut = ChequeStatut.Utilise;
		cheque.Echeance = echeance;
		cheque.TiersNo = clientNo;
		cheque.Tire = tire;
		cheque.Montant = montant;
		_chequeRepository.Update(cheque);
		if (!societe.VirementDefaultSouche.HasValue)
		{
			throw new InvalidOperationException("Veuillez configurer la souche virement par défaut");
		}
		SocieteDevise defaultDeviseSociete = societe.GetDefaultDeviseSociete();
		if (defaultDeviseSociete == null)
		{
			throw new ApplicationException("Impossible de charger la devise société.");
		}
		decimal num = Math.Round(montant * cours, defaultDeviseSociete.NombreDecimales, MidpointRounding.AwayFromZero);
		Echeance echeance2 = new Echeance(0, numero.ToUpper(), ErpDomaine.Vente, ErpDocumentType.None, date, montant, montant, echeance, EcheanceType.RemboursementDivers, modeNo, societe.No, clientNo, clientCode, clientIntitule, clientNo, clientCode, clientIntitule, deviseNo, societe.VirementDefaultSouche.Value, 0, cours, libelle, num, num)
		{
			IsComptaRemboursementClient = false,
			CaisseNo = caisse.No,
			ChequeNo = cheque.No,
			DatePointeRemboursementCheque = DateHelper.GetMinSqlDateTime(),
			UtilisateurNo = utilisateur.No,
			BanqueNo = banqueNo,
			EcheanceReporte = date
		};
		int? num2 = _echeanceRepository.Create(echeance2);
		if (!num2.HasValue)
		{
			throw new ApplicationException("EcheandeNo invalide!");
		}
		_notifyService.Notify(TypeEntity.Echeance, num2.Value, TypeAction.Ajout, societe.No);
		_notifyService.Notify(TypeEntity.RemboursementClient, num2.Value, TypeAction.Ajout, societe.No);
		transactionScope.Complete();
	}

	public void DeComptabiliserBordereaux(int mvtNo)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		Bordereau bordereau = BordereauGet(mvtNo);
		if (bordereau == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementNo);
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		if (!bordereau.IsComptabilise)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglemntNonComptabilise);
		}
		foreach (LigneBordereau item in bordereau.GetLigneBordereaux())
		{
			_ecritureComptaRepository.DeleteLigneBordereau(item.No);
		}
		_ecritureComptaRepository.DeleteLigneBordereau(bordereau.No);
		bordereau.ChangeEtatComptabilise(etat: false);
		bordereau.DateModification = DateTime.Now;
		bordereau.ModificateurNo = utilisateur.No;
		_bordereauRepository.Update(bordereau);
		_notifyService.Notify(TypeEntity.Bordereau, bordereau.No, TypeAction.Modification, bordereau.SocieteNo);
		transactionScope.Complete();
	}

	public void DecomptabiliserDossier(int dossierNo)
	{
		if (dossierNo <= 0)
		{
			throw new InvalidOperationException("Le dossier est invalide!");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		DossierReglement dossierReglement = SocieteManager.GetDossierReglement(dossierNo) ?? throw new ArgumentNullException($"Impossible de charger le dossier [{dossierNo}]");
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_dossierReglementRepository.Decomptabiliser(dossierNo);
		if (dossierReglement.Domaine == DomaineDossier.Fournisseur)
		{
			_notifyService.Notify(TypeEntity.Dossier, dossierNo, TypeAction.Modification, SocieteManager.Societe.No);
		}
		else
		{
			_notifyService.Notify(TypeEntity.DossierClient, dossierNo, TypeAction.Modification, SocieteManager.Societe.No);
		}
		transactionScope.Complete();
	}

	public void DeComptabiliserEcart(int ecartNo)
	{
		Ecart ecart = SocieteManager.EcartGet(ecartNo);
		if (ecart == null)
		{
			throw new InvalidOperationException("L'échéance est invalide!");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		if (ecart.IsComptabilise == EtatComptabilite.NonComptabilise)
		{
			throw new ApplicationException("L'échéance n'est pas comptabilisé!");
		}
		_ecritureComptaRepository.DeleteLigne(ecartNo, MouvementDomaine.Ecart);
		_ecartRepository.Decomptabiliser(ecartNo);
		_notifyService.Notify(TypeEntity.Echeance, ecartNo, TypeAction.Modification, ecart.SocieteNo);
		transactionScope.Complete();
	}

	public void DecomptabiliserEcartEchangeDossierFournisseur(int dossierNo)
	{
		DossierReglement dossierReglement = SocieteManager.GetDossierReglement(dossierNo);
		if (dossierReglement == null)
		{
			throw new ApplicationException($"Impossible de charger le dossier [{dossierNo}]!");
		}
		if (!dossierReglement.IsComptabilise)
		{
			throw new InvalidOperationException("Le dossier fournisseur [" + dossierReglement.Numero + "] n'est pas comptablisé!");
		}
		_ecritureComptaRepository.DeleteLigne(dossierNo, MouvementDomaine.EcartChangeFournisseur);
	}

	public void DecomptabiliserEcartEchangeDossierClient(int dossierNo)
	{
		DossierReglement dossierReglement = SocieteManager.GetDossierReglement(dossierNo);
		if (dossierReglement == null)
		{
			throw new ApplicationException($"Impossible de charger le dossier [{dossierNo}]!");
		}
		if (!dossierReglement.IsComptabilise)
		{
			throw new InvalidOperationException("Le dossier Client [" + dossierReglement.Numero + "] n'est pas comptablisé!");
		}
		_ecritureComptaRepository.DeleteLigne(dossierNo, MouvementDomaine.Ecart);
	}

	public void DeComptabiliserEcartEchange(int ecartNo)
	{
		EcartEchange ecartEchange = SocieteManager.EcartEchangeGet(ecartNo);
		if (ecartEchange == null)
		{
			throw new InvalidOperationException("L'échéance est invalide!");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		if (ecartEchange.IsComptabilise == EtatComptabilite.NonComptabilise)
		{
			throw new ApplicationException("L'échéance n'est pas comptabilisé!");
		}
		_ecritureComptaRepository.DeleteLigne(ecartNo, MouvementDomaine.EcartEcheance);
		_ecartEchangeRepository.Comptabiliser(ecartNo, EtatComptabilite.NonComptabilise);
		_notifyService.Notify(TypeEntity.Echeance, ecartNo, TypeAction.Modification, ecartEchange.SocieteNo);
		transactionScope.Complete();
	}

	public void DeComptabiliserFactureFournisseur(int factureNo)
	{
		if (factureNo == 0)
		{
			throw new ArgumentNullException("factureNo");
		}
		Echeance echeance = SocieteManager.EcheanceGet(factureNo);
		if (echeance == null)
		{
			throw new InvalidOperationException($"Impossible de charger la facture [{factureNo}].");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		if (!echeance.IsComptabilise)
		{
			throw new ApplicationException("Facture fournisseur [" + echeance.DocumentNumero + "] n'est pas comptabilisé.");
		}
		_ecritureComptaRepository.DeleteLigneFactureFournisseurTresorerie(echeance.No);
		echeance.IsComptabilise = false;
		_echeanceRepository.Update(echeance);
		_notifyService.Notify(TypeEntity.Echeance, factureNo, TypeAction.Modification, echeance.SocieteNo);
		transactionScope.Complete();
	}

	public void DeComptabiliserImpaye(int mvtNo)
	{
		Impaye impaye = ImpayeGet(mvtNo);
		if (impaye == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorImpayeInvalid);
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		if (impaye.IsComptabilise == EtatComptabilite.NonComptabilise)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorImpayeNonComptabilise);
		}
		_ecritureComptaRepository.DeleteLigneImpaye(impaye.ReglementNo);
		impaye.ChangeEtatComptabilise(EtatComptabilite.NonComptabilise);
		_impayeRepository.Update(impaye);
		_notifyService.Notify(TypeEntity.Reglement, impaye.ReglementNo, TypeAction.Modification, impaye.SocieteNo);
		_notifyService.Notify(TypeEntity.Echeance, impaye.EcheanceNo, TypeAction.Modification, impaye.SocieteNo);
		transactionScope.Complete();
	}

	public void DeComptabiliserImpayeFournisseur(int mvtNo)
	{
		ImpayeFournisseur impayeFournisseur = ImpayeFournisseurGet(mvtNo);
		if (impayeFournisseur == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorImpayeInvalid);
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		if (!impayeFournisseur.IsComptaImpaye)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorImpayeNonComptabilise);
		}
		_ecritureComptaRepository.DeleteLigneImpaye(impayeFournisseur.No);
		impayeFournisseur.ChangeEtatComptabilise(etat: false);
		_impayeFournisseurRepository.Update(impayeFournisseur);
		_notifyService.Notify(TypeEntity.ReglementFournisseur, impayeFournisseur.No, TypeAction.Modification, impayeFournisseur.SocieteNo);
		_notifyService.Notify(TypeEntity.ImpayeFournisseur, impayeFournisseur.No, TypeAction.Modification, impayeFournisseur.SocieteNo);
		transactionScope.Complete();
	}

	public void DeComptabiliserReglement(int mvtNo)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		ReglementClient reglementClient = ReglementGet(mvtNo);
		if (reglementClient == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementNo);
		}
		if (reglementClient.IsRemplacmentComptabilise == EtatComptabilite.Comptabilise)
		{
			throw new InvalidOperationException("Le remplacement du règlement est comptabilisé!");
		}
		if (reglementClient.IsRemis == Remis.RemisBanque)
		{
			Bordereau bordereau = BordereauGet(reglementClient.EnteteBordereauNo.Value);
			if (bordereau.IsComptabilise)
			{
				throw new ApplicationException($"Bordereaux {bordereau.Numero} est comptabilisé");
			}
		}
		if (reglementClient.IsRemplacer && reglementClient.IsRemplacmentComptabilise == EtatComptabilite.Comptabilise)
		{
			throw new ApplicationException("L'action du remplacement est comptabilisé!");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		if (reglementClient.IsComptabilise == EtatComptabilite.NonComptabilise)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglementComptabilise);
		}
		_ecritureComptaRepository.DeleteLigneReglementClient(reglementClient.No);
		reglementClient.ChangeEtatComptabilise(EtatComptabilite.NonComptabilise);
		reglementClient.DateModification = DateTime.Now;
		reglementClient.ModificateurNo = utilisateur.No;
		_reglementClientRepository.Update(reglementClient);
		_notifyService.Notify(TypeEntity.Reglement, reglementClient.No, TypeAction.Modification, reglementClient.SocieteNo);
		transactionScope.Complete();
	}

	public void DeComptabiliserReglementFournisseur(int mvtNo)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		ReglementFournisseur reglementFournisseur = ReglementFournisseurGet(mvtNo);
		if (reglementFournisseur == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementNo);
		}
		CaisseModeReglement mode = (SocieteManager.Societe.GetCaisse(reglementFournisseur.CaisseNo) ?? throw new ApplicationException("Impossible de charger la caisse.")).GetMode(reglementFournisseur.ModeReglementNo);
		if (mode == null)
		{
			throw new ApplicationException("Impossible de charger le mode de règlement.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		if (reglementFournisseur.IsComptabilise == EtatComptabilite.NonComptabilise)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglementComptabilise);
		}
		if (mode.Type == ReglementType.Traite && (reglementFournisseur.IsDecaisse || reglementFournisseur.IsComptabilise == EtatComptabilite.TraiteFournisseurComptabilise))
		{
			throw new ApplicationException("Impossible de décomptabiliser un traite fournisseur décaissé n°[" + reglementFournisseur.Numero + "]");
		}
		_ecritureComptaRepository.DeleteLigneReglementFournisseur(reglementFournisseur.No);
		_reglementFournisseurRepository.UpdateEtatCompta(reglementFournisseur.No, EtatComptabilite.NonComptabilise, utilisateur.No);
		_notifyService.Notify(TypeEntity.ReglementFournisseur, mvtNo, TypeAction.Modification, reglementFournisseur.SocieteNo);
		transactionScope.Complete();
	}

	public void DeComptabiliserReglementFournisseurTraite(int mvtNo, string codeJournal)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		ReglementClient reglementClient = ReglementGet(mvtNo);
		if (reglementClient == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementNo);
		}
		if (reglementClient.IsComptabilise != EtatComptabilite.TraiteFournisseurComptabilise)
		{
			throw new ApplicationException("La traite fournisseur n'est pas comptabilisée");
		}
		_reglementFournisseurRepository.UpdateEtatComptaTraiteFrs(reglementClient.No, EtatComptabilite.Comptabilise, utilisateur.No);
		_ecritureComptaRepository.DeleteLigneReglementTraiteFournisseur(mvtNo, codeJournal);
		_notifyService.Notify(TypeEntity.Reglement, reglementClient.No, TypeAction.Modification, reglementClient.SocieteNo);
	}

	public void DeComptabiliserRemplacement(int mvtNo)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		ReglementClient reglementClient = ReglementGet(mvtNo);
		if (reglementClient == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementNo);
		}
		if (reglementClient.IsRemplacmentComptabilise != EtatComptabilite.Comptabilise)
		{
			throw new ApplicationException($"Remplacement {reglementClient.Numero} n'est pas comptabilisé");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		if (reglementClient.IsComptabilise == EtatComptabilite.NonComptabilise)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglementComptabilise);
		}
		_ecritureComptaRepository.DeleteRemplacement(reglementClient.No);
		reglementClient.IsRemplacmentComptabilise = EtatComptabilite.NonComptabilise;
		reglementClient.DateModification = DateTime.Now;
		reglementClient.ModificateurNo = utilisateur.No;
		_reglementClientRepository.Update(reglementClient);
		_notifyService.Notify(TypeEntity.Reglement, reglementClient.No, TypeAction.Modification, reglementClient.SocieteNo);
		transactionScope.Complete();
	}

	public void DeComptabiliserVersement(int mvtNo)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		Bordereau bordereau = BordereauGet(mvtNo);
		if (bordereau == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementNo);
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		if (!bordereau.IsComptabilise)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglemntNonComptabilise);
		}
		_ecritureComptaRepository.DeleteLigneBordereau(mvtNo);
		bordereau.ChangeEtatComptabilise(etat: false);
		bordereau.DateModification = DateTime.Now;
		bordereau.ModificateurNo = utilisateur.No;
		_bordereauRepository.Update(bordereau);
		_notifyService.Notify(TypeEntity.Bordereau, bordereau.No, TypeAction.Modification, bordereau.SocieteNo);
		transactionScope.Complete();
	}

	public void Delete(Caisse caisse)
	{
		if (caisse == null)
		{
			throw new ArgumentNullException("caisse");
		}
		if (SocieteManager.Societe.GetCaisse(caisse.No) == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorCaisseInvalid);
		}
		if (!_mouvementCaisseRepository.IsCaisseVide(caisse.No))
		{
			throw new ApplicationException("La caisse a des mouvements!");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_caisseBanqueRepository.Delete(caisse);
		_caisseCaisseRepository.Delete(caisse);
		_caisseRepository.Delete(caisse);
		transactionScope.Complete();
	}

	public IList<EcritureComptable> GetEcritures(int mvtNo, MouvementDomaine domaine)
	{
		return _ecritureComptaRepository.GetAll(mvtNo, domaine);
	}

	public IList<EcritureComptable> GetEcrituresByDocumentNumero(string numDocument, MouvementDomaine domaine)
	{
		return _ecritureComptaRepository.GetAllByDocumentNumero(numDocument, domaine);
	}

	public EcritureComptable GetEcritureByErpNo(int erpNo, Societe societe = null)
	{
		societe = societe ?? SocieteManager.Societe;
		return _ecritureComptaRepository.GetByErpNo(erpNo, societe.No);
	}

	public IList<EcritureComptable> GetEcrituresByExercice(int annee, MouvementDomaine domaine, Societe societe = null)
	{
		societe = societe ?? SocieteManager.Societe;
		return _ecritureComptaRepository.Get(annee, societe.No, domaine);
	}

	public List<EcritureComptable> EcritureCurrent(int mouvementNo, MouvementDomaine domaine)
	{
		return (from x in _ecritureComptaRepository.GetEcritureEnAttente(mouvementNo, domaine)
			orderby x.Etape
			select x).ToList();
	}

	public EcritureComptable EcritureGet(int no)
	{
		return _ecritureComptaRepository.Get(no);
	}

	public List<EcritureComptable> EcritureGetByPeriode(int mouvementNo, int etape, MouvementDomaine domaine, NatureActionEtape natureAction)
	{
		return _ecritureComptaRepository.Get(mouvementNo, etape, domaine, natureAction).ToList();
	}

	public string EcritureGetPieceNumero(int etape, string reference, int exercice, ErpComptaPeriode periode)
	{
		Societe societe = SocieteManager.Societe;
		return _ecritureComptaRepository.EcritureGetPieceNumero(societe.No, etape, reference, exercice, periode);
	}

	public Caisse Get(int no)
	{
		return _caisseRepository.Get(no);
	}

	public List<DetailAffectation> GetAllDetailAffectationReglementClient(bool isSynchroniser, DateTime? dateDebut = null, DateTime? dateFin = null)
	{
		Societe societe = SocieteManager.Societe;
		return _detailAffectationRepository.GetAll(societe.No, ErpDomaine.Vente, isSynchroniser, dateDebut, dateFin, default(EcheanceType)).ToList();
	}

	public List<DetailAffectation> GetAllDetailAffectationReglementFournisseur(bool isSynchroniser, DateTime? dateDebut = null, DateTime? dateFin = null)
	{
		Societe societe = SocieteManager.Societe;
		return _detailAffectationRepository.GetAll(societe.No, ErpDomaine.Achat, isSynchroniser, dateDebut, dateFin, default(EcheanceType)).ToList();
	}

	public List<DetailAffectation> GetAllDetailAffectationReglementClientByReglement(int reglementNo, bool isSynchroniser)
	{
		Societe societe = SocieteManager.Societe;
		return _detailAffectationRepository.GetAllByReglement(societe.No, reglementNo, ErpDomaine.Vente, isSynchroniser, default(EcheanceType)).ToList();
	}

	public DetailAffectation GetDetailAffectationReglementClientToSynchronise(int affectationNo)
	{
		Societe societe = SocieteManager.Societe;
		return _detailAffectationRepository.GetToSynchroniser(affectationNo, societe.No);
	}

	public List<LettrageAffectation> GetAffectationClientToLettrage(int clientNo, DateTime dateMinReg, DateTime dateMaxReg, DateTime dateMinEch, DateTime dateMaxEch)
	{
		Societe societe = SocieteManager.Societe;
		List<LettrageAffectation> list = _lettrageAffectationRepository.GetAll(societe.No, clientNo, dateMinReg, dateMaxReg, dateMinEch, dateMaxEch).ToList();
		int num = 1;
		foreach (LettrageAffectation item in list)
		{
			LettrageAffectation elementCurrent = item;
			List<IGrouping<int, LettrageAffectation>> group = (from x in list
				where x.IsPointer && (x.EcheanceNo == elementCurrent.EcheanceNo || x.ReglementNo == elementCurrent.ReglementNo)
				group x by x.PointerNo).ToList();
			item.IsPointer = true;
			if (!group.Any())
			{
				num = (item.PointerNo = num + 1);
				continue;
			}
			if (group.Count() == 1)
			{
				item.PointerNo = group.First().Key;
				continue;
			}
			num = (item.PointerNo = num + 1);
			foreach (LettrageAffectation item2 in list.Where((LettrageAffectation x) => x.IsPointer && group.Any((IGrouping<int, LettrageAffectation> g) => g.Key == x.PointerNo)).ToList())
			{
				item2.PointerNo = num;
			}
		}
		IEnumerable<IGrouping<int, LettrageAffectation>> enumerable = from x in list
			group x by x.PointerNo;
		List<LettrageAffectation> list2 = new List<LettrageAffectation>();
		foreach (IGrouping<int, LettrageAffectation> element in enumerable)
		{
			decimal num4 = element.Sum((LettrageAffectation x) => x.AffectationMontant);
			decimal num5 = element.Select((LettrageAffectation x) => x.ReglementNo).Distinct().Sum((int i) => element.First((LettrageAffectation x) => x.ReglementNo == i).ReglementMontant);
			if (!(element.Select((LettrageAffectation x) => x.EcheanceNo).Distinct().Sum((int i) => element.First((LettrageAffectation x) => x.EcheanceNo == i).EcheanceMontant) != num4) && !(num4 != num5))
			{
				list2.AddRange(element.ToList());
			}
		}
		return list2;
	}

	public List<LettrageAffectation> GetAffectationReglementToLettrage(int reglementNo)
	{
		Societe societe = SocieteManager.Societe;
		List<LettrageAffectation> list = _lettrageAffectationRepository.GetAll(societe.No, reglementNo).ToList();
		int num = 1;
		foreach (LettrageAffectation item in list)
		{
			LettrageAffectation elementCurrent = item;
			List<IGrouping<int, LettrageAffectation>> group = (from x in list
				where x.IsPointer && (x.EcheanceNo == elementCurrent.EcheanceNo || x.ReglementNo == elementCurrent.ReglementNo)
				group x by x.PointerNo).ToList();
			item.IsPointer = true;
			if (!group.Any())
			{
				num = (item.PointerNo = num + 1);
				continue;
			}
			if (group.Count() == 1)
			{
				item.PointerNo = group.First().Key;
				continue;
			}
			num = (item.PointerNo = num + 1);
			foreach (LettrageAffectation item2 in list.Where((LettrageAffectation x) => x.IsPointer && group.Any((IGrouping<int, LettrageAffectation> g) => g.Key == x.PointerNo)).ToList())
			{
				item2.PointerNo = num;
			}
		}
		IEnumerable<IGrouping<int, LettrageAffectation>> enumerable = from x in list
			group x by x.PointerNo;
		List<LettrageAffectation> list2 = new List<LettrageAffectation>();
		foreach (IGrouping<int, LettrageAffectation> element in enumerable)
		{
			decimal num4 = element.Sum((LettrageAffectation x) => x.AffectationMontant);
			decimal num5 = element.Select((LettrageAffectation x) => x.ReglementNo).Distinct().Sum((int i) => element.First((LettrageAffectation x) => x.ReglementNo == i).ReglementMontant);
			if (!(element.Select((LettrageAffectation x) => x.EcheanceNo).Distinct().Sum((int i) => element.First((LettrageAffectation x) => x.EcheanceNo == i).EcheanceMontant) != num4) && !(num4 != num5))
			{
				list2.AddRange(element.ToList());
			}
		}
		return list2;
	}

	public IEnumerable<Caisse> GetAllAutorisedCaisse(int utilisateurNo, int societeNo)
	{
		UtilisateurManager utilisateurManager = SocieteManager.Groupe.UtilisateurManager;
		Societe societe = SocieteManager.Get(societeNo);
		if (societe == null)
		{
			throw new ApplicationException("Impossible de charger la societe.");
		}
		Utilisateur byNo = utilisateurManager.GetByNo(utilisateurNo);
		if (byNo == null)
		{
			throw new ApplicationException("Impossible de charger l'utilisateur.");
		}
		if (byNo.IsAdmin)
		{
			return societe.GetCaisses();
		}
		int[] caisses = (from a in _autorisationCaisseRepository.GetAll(byNo.No, societe.No)
			select a.CaisseNo).ToArray();
		return (from c in societe.GetCaisses()
			where caisses.Any((int ca) => ca == c.No)
			select c).ToList();
	}

	public Task<IEnumerable<Bordereau>> GetAllBordereauAComptaAsync(DateTime dateDe, DateTime dateA, EtatComptabilite etat, int[] caisseNo, int[] typeBordereauNo, int[] banqueNos, CancellationToken cancellationToken)
	{
		Societe societe = SocieteManager.Societe;
		return _bordereauRepository.GetAllAComptaAsync(dateDe.Date, dateA.Date.AddDays(1.0), etat, caisseNo, typeBordereauNo, banqueNos, societe.No, cancellationToken);
	}

	public Task<IEnumerable<Bordereau>> GetAllBordereauByDateRemisAComptaAsync(DateTime dateDe, DateTime dateA, EtatComptabilite etat, int[] caisseNo, int[] typeBordereauNo, int[] banqueNos, CancellationToken cancellationToken)
	{
		Societe societe = SocieteManager.Societe;
		return _bordereauRepository.GetAllAComptaByDateRemisAsync(dateDe.Date, dateA.Date.AddDays(1.0), etat, caisseNo, typeBordereauNo, banqueNos, societe.No, cancellationToken);
	}

	public IEnumerable<int> GetAllBordereauNoComptabilisationEnMasse(int[] banqueNos, int[] caisseNo, int[] typeBordereauNo, DateTime dateDe, DateTime dateA, bool isComptabilise, Societe societe = null)
	{
		societe = societe ?? SocieteManager.Societe;
		return _bordereauRepository.GetAllBordereauNoComptabilisationEnMasse(societe.No, banqueNos, caisseNo, typeBordereauNo, dateDe, dateA, isComptabilise);
	}

	public IEnumerable<Caisse> GetCaisses(int societeNo)
	{
		return (SocieteManager.Get(societeNo) ?? throw new ApplicationException("Impossible de charger la societe.")).GetCaisses();
	}

	public IEnumerable<Caisse> GetAllCaisses(int societeNo, int utilisateurNo)
	{
		UtilisateurManager utilisateurManager = SocieteManager.Groupe.UtilisateurManager;
		Societe societe = SocieteManager.Get(societeNo);
		if (societe == null)
		{
			throw new ApplicationException("Impossible de charger la societe.");
		}
		Utilisateur byNo = utilisateurManager.GetByNo(utilisateurNo);
		if (byNo == null)
		{
			throw new ApplicationException("Impossible de charger l'utilisateur.");
		}
		return GetAllCaissesByAutorisation(societe, byNo);
	}

	public IEnumerable<Caisse> GetAllCaissesByAutorisation(Societe societe = null, Utilisateur utilisateur = null)
	{
		societe = societe ?? SocieteManager.Societe;
		if (SocieteManager.Utilisateur.IsAdmin)
		{
			return societe.GetCaisses();
		}
		utilisateur = utilisateur ?? SocieteManager.Utilisateur;
		int[] caisses = (from a in _autorisationCaisseRepository.GetAll(utilisateur.No, societe.No)
			select a.CaisseNo).ToArray();
		return (from c in societe.GetCaisses()
			where caisses.Any((int ca) => ca == c.No)
			select c).ToList();
	}

	public IEnumerable<HistoriqueMouvementCaisse> GetAllMouvement(int[] caissesNo, int deviseNo)
	{
		foreach (int no in caissesNo)
		{
			if (Get(no) == null)
			{
				throw new ArgumentNullException("caissesNo");
			}
		}
		if (SocieteManager.Groupe.DeviseManager.Get(deviseNo) == null)
		{
			throw new InvalidOperationException(rcRessources.DeviseNotFound);
		}
		return _mouvementCaisseRepository.GetAllMouvement(caissesNo, deviseNo);
	}

	public IEnumerable<Note> GetAllNotes(TypeEntity typeEntity, Societe societe = null)
	{
		societe = societe ?? SocieteManager.Societe;
		return _noteRepository.GetAllNotesByTypeEntite(typeEntity, societe.No);
	}

	public IEnumerable<Note> GetAllNotes(Societe societe = null)
	{
		societe = societe ?? SocieteManager.Societe;
		return _noteRepository.GetAllNotes(societe.No);
	}

	public Note GetNote(int no)
	{
		return _noteRepository.Get(no);
	}

	public IEnumerable<Note> GetAllNotesByClient(int clientNo, Societe societe = null)
	{
		societe = societe ?? SocieteManager.Societe;
		return _noteRepository.GetAllNotesByClient(clientNo, societe.No);
	}

	public IEnumerable<Note> GetAllNotes(int entityNo, TypeEntity type)
	{
		return _noteRepository.GetAllNotesByEntite(entityNo, type);
	}

	public List<ReglementCoffre> ReglementCoffreGetAll(Societe societe = null, ProfilType profilType = ProfilType.Grc)
	{
		societe = societe ?? SocieteManager.Societe;
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		int[] caissesNo = (utilisateur.IsAdmin ? (from x in societe.GetCaisses()
			select x.No).ToArray() : (from a in _autorisationCaisseRepository.GetAll(utilisateur.No, societe.No, profilType)
			select a.CaisseNo).ToArray());
		return _reglementCoffreRepository.GetAll(societe.No, caissesNo);
	}

	public IEnumerable<ReglementClient> ReglementGetAll(int societeNo, int utilisateurNo, DateTime dateDebut, DateTime dateFin)
	{
		Utilisateur byNo = SocieteManager.Groupe.UtilisateurManager.GetByNo(utilisateurNo);
		if (byNo == null)
		{
			throw new ApplicationException("Impossible de charger l'utilisateur;");
		}
		Societe societe = SocieteManager.Get(societeNo);
		if (societe == null)
		{
			throw new ApplicationException("Impossible de charger la societe");
		}
		int[] caissesNo = (byNo.IsAdmin ? (from x in societe.GetCaisses()
			select x.No).ToArray() : (from a in _autorisationCaisseRepository.GetAll(byNo.No, societeNo, ProfilType.Grc)
			select a.CaisseNo).ToArray());
		return _reglementClientRepository.GetAll(societeNo, dateDebut, dateFin, caissesNo);
	}

	public IEnumerable<ReglementClient> ReglementGetAll(DateTime dateDebut, DateTime dateFin)
	{
		Societe societe = SocieteManager.Societe;
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		int[] caissesNo = (utilisateur.IsAdmin ? (from x in societe.GetCaisses()
			select x.No).ToArray() : (from a in _autorisationCaisseRepository.GetAll(utilisateur.No, societe.No, ProfilType.Grc)
			select a.CaisseNo).ToArray());
		return _reglementClientRepository.GetAll(societe.No, dateDebut, dateFin, caissesNo);
	}

	public IEnumerable<ReglementClient> ReglementGetAllNonAnnule(DateTime dateDebut, DateTime dateFin)
	{
		Societe societe = SocieteManager.Societe;
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		int[] caissesNo = (utilisateur.IsAdmin ? (from x in societe.GetCaisses()
			select x.No).ToArray() : (from a in _autorisationCaisseRepository.GetAll(utilisateur.No, societe.No, ProfilType.Grc)
			select a.CaisseNo).ToArray());
		return _reglementClientRepository.GetAll(societe.No, dateDebut, dateFin, caissesNo);
	}

	public IEnumerable<ReglementClient> ReglementGetAllByDateEcheance(DateTime echeanceDebut, DateTime echeanceFin)
	{
		Societe societe = SocieteManager.Societe;
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		int[] caissesNo = (utilisateur.IsAdmin ? (from x in societe.GetCaisses()
			select x.No).ToArray() : (from a in _autorisationCaisseRepository.GetAll(utilisateur.No, societe.No, ProfilType.Grc)
			select a.CaisseNo).ToArray());
		return _reglementClientRepository.GetAllByDateEcheance(societe.No, echeanceDebut, echeanceFin, caissesNo);
	}

	public IEnumerable<ReglementClient> ReglementClientGetAllPieceNonEchuByClient(int clientNo, Societe societe = null)
	{
		societe = societe ?? SocieteManager.Societe;
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		int[] caissesNo = (utilisateur.IsAdmin ? (from x in societe.GetCaisses()
			select x.No).ToArray() : (from a in _autorisationCaisseRepository.GetAll(utilisateur.No, societe.No, ProfilType.Grc)
			select a.CaisseNo).ToArray());
		return _reglementClientRepository.GetAllPieceNonEchuByClient(societe.No, clientNo, caissesNo);
	}

	public IList<ReglementClient> GetAllReglementACompta(int exercice, int caisseNo, int modeNo, DateTime dateMin, DateTime dateMax, DateTime echeanceMin, DateTime echeanceMax, EtatComptabilite etat, bool parRapportDateRemis, Remis[] remis)
	{
		Societe societe = SocieteManager.Societe;
		if ((societe.GetCaisse(caisseNo) ?? throw new ApplicationException(TresorerieCoreMessages.ErrorCaisseNo)).GetMode(modeNo) == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorModeReglementInvalide);
		}
		return ReglementGetAll(societe.No, etat, exercice, caisseNo, modeNo, dateMin, dateMax, echeanceMin, echeanceMax, parRapportDateRemis, remis).ToList();
	}

	public IEnumerable<ReglementClient> GetAllReglementEscompte(int banqueNo, DateTime dateDe, DateTime dateA, bool isRegler)
	{
		if (banqueNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorBanqueInvalide);
		}
		return _reglementClientRepository.GetAllReglementEscompte(SocieteManager.Societe.No, banqueNo, dateDe.Date, dateA.Date, isRegler);
	}

	public IList<ReglementClientRemplace> GetAllRemlaceACompta(int exercice, int caisseNo, int modeNo, EtatComptabilite etat, DateTime? dateMin = null, DateTime? dateMax = null)
	{
		Societe societe = SocieteManager.Societe;
		if ((societe.GetCaisse(caisseNo) ?? throw new ApplicationException(TresorerieCoreMessages.ErrorCaisseNo)).GetMode(modeNo) == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorModeReglementInvalide);
		}
		ReglementClientRemplaceFactory reglementClientRemplaceFactory = new ReglementClientRemplaceFactory();
		return _reglementClientRemplaceRepository.GetAllRemplace(societe.No, etat, exercice, caisseNo, modeNo, dateMin, dateMax).Select(reglementClientRemplaceFactory.Generate).ToList();
	}

	public IEnumerable<RetenuALaSource> GetAllRetenus()
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		if (utilisateur.IsAdmin)
		{
			return _retenuALaSourceRepository.GetAll(SocieteManager.Societe.No);
		}
		int[] caisses = (from a in _autorisationCaisseRepository.GetAll(utilisateur.No, SocieteManager.Societe.No, ProfilType.Grf)
			select a.CaisseNo).ToArray();
		return (from x in _retenuALaSourceRepository.GetAll(SocieteManager.Societe.No)
			where caisses.Any((int y) => y == x.CaisseNo)
			orderby x.Date descending
			select x).ToList();
	}

	public IEnumerable<RetenuALaSource> GetAllRetenusToDeclarationRAS(DateTime dateDu, DateTime dateAu)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		if (utilisateur.IsAdmin)
		{
			return _retenuALaSourceRepository.GetAllToDeclarationRAS(SocieteManager.Societe.No, dateDu.Date, dateAu.Date.AddDays(1.0).AddSeconds(-1.0));
		}
		int[] caisses = (from a in _autorisationCaisseRepository.GetAll(utilisateur.No, SocieteManager.Societe.No, ProfilType.Grf)
			select a.CaisseNo).ToArray();
		return (from x in _retenuALaSourceRepository.GetAllToDeclarationRAS(SocieteManager.Societe.No, dateDu.Date, dateAu.Date.AddDays(1.0).AddSeconds(-1.0))
			where caisses.Any((int y) => y == x.CaisseNo)
			orderby x.Date descending
			select x).ToList();
	}

	public IEnumerable<RetenuALaSource> GetAllRetenusByDossier(int dossierNo)
	{
		if (dossierNo <= 0)
		{
			throw new ArgumentException("Le dossier No est invalide!");
		}
		return _retenuALaSourceRepository.GetAllByDossier(dossierNo);
	}

	public IEnumerable<TypeBordereau> GetAllTypeBordereauxPiece(int caisseNo)
	{
		TypeBordereauxManager bordereauxTypeManager = SocieteManager.Groupe.BordereauxTypeManager;
		Societe societe = SocieteManager.Societe;
		Caisse caisse = societe.GetCaisse(caisseNo);
		if (caisse == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		return from t in bordereauxTypeManager.GetAll()
			where (from m in t.GetModeReglements()
				where m.Type == ReglementType.Cheque || m.Type == ReglementType.Traite
				select m).Any((ModeReglement m) => caisse.HasModeReglement(m.No))
			select t;
	}

	public IEnumerable<TypeBordereau> GetAllTypeVersements(int caisseNo)
	{
		TypeBordereauxManager bordereauxTypeManager = SocieteManager.Groupe.BordereauxTypeManager;
		Societe societe = SocieteManager.Societe;
		Caisse caisse = societe.GetCaisse(caisseNo);
		if (caisse == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		return from t in bordereauxTypeManager.GetAll()
			where (from m in t.GetModeReglements()
				where m.Type == ReglementType.Espece
				select m).Any((ModeReglement m) => caisse.HasModeReglement(m.No))
			select t;
	}

	public IList<EcritureComptable> GetEcritureEnAttenteEcheance()
	{
		Societe societe = SocieteManager.Societe;
		var list = (from x in _ecritureComptaRepository.GetEcheance(societe.No).ToList()
			group x by new { x.Etape, x.MouvementNo }).ToList();
		List<EcritureComptable> list2 = new List<EcritureComptable>();
		foreach (var item in list)
		{
			if (!_ecritureComptaRepository.GetPreviousEtape(item.Key.MouvementNo.Value, item.Key.Etape, MouvementDomaine.EnteteBordereau).Any((EcritureComptable x) => x.Action != NatureActionEtape.Echeance))
			{
				list2.AddRange(item);
			}
		}
		return list2;
	}

	public IList<EcritureComptable> GetEcrituresBordereauxPiece(int mvtNo)
	{
		if (mvtNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorMvtNo);
		}
		List<EcritureComptable> list = new List<EcritureComptable>();
		Bordereau bordereau = BordereauGet(mvtNo);
		if (bordereau == null)
		{
			throw new ArgumentNullException("bordereau");
		}
		foreach (LigneBordereau item in bordereau.GetLigneBordereaux())
		{
			list.AddRange(_ecritureComptaRepository.GetAll(item.No, MouvementDomaine.EnteteBordereau));
		}
		list.AddRange(_ecritureComptaRepository.GetAll(bordereau.No, MouvementDomaine.EnteteBordereau));
		return list;
	}

	public IEnumerable<EcritureComptable> GetEcrituresEcartChangeDossierFournisseur(int dossierNo)
	{
		DossierReglement dossierReglement = SocieteManager.GetDossierReglement(dossierNo);
		if (dossierReglement == null)
		{
			throw new ApplicationException($"Impossible de charger le dossier de règlement fournisseur [{dossierNo}].");
		}
		return _ecritureComptaRepository.GetEcrituresEcart(dossierNo, dossierReglement.Numero, MouvementDomaine.EcartChangeFournisseur);
	}

	public IEnumerable<EcritureComptable> GetEcrituresEcartChangeDossierClient(int dossierNo)
	{
		DossierReglement dossierReglement = SocieteManager.GetDossierReglement(dossierNo);
		if (dossierReglement == null)
		{
			throw new ApplicationException($"Impossible de charger le dossier de règlement Client [{dossierNo}].");
		}
		return _ecritureComptaRepository.GetEcrituresEcart(dossierNo, dossierReglement.Numero, MouvementDomaine.Ecart);
	}

	public IList<EcritureComptable> GetEcrituresEcarts(int ecartNo)
	{
		if (ecartNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorMvtNo);
		}
		Ecart ecart = SocieteManager.EcartGet(ecartNo);
		if (ecart == null)
		{
			throw new ArgumentNullException($"Impossible de charger l'écart [{ecartNo}].");
		}
		return _ecritureComptaRepository.GetAll(ecart.No, MouvementDomaine.Ecart);
	}

	public IList<EcritureComptable> GetEcrituresEcartsEchange(int ecartNo)
	{
		if (ecartNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorMvtNo);
		}
		EcartEchange ecartEchange = SocieteManager.EcartEchangeGet(ecartNo);
		if (ecartEchange == null)
		{
			throw new ArgumentNullException("Impossible de charger l'écart de change!");
		}
		return _ecritureComptaRepository.GetAll(ecartEchange.No, MouvementDomaine.EcartEcheance);
	}

	public IList<EcritureComptable> GetEcrituresFactureFournisseur(int factureNo)
	{
		if (factureNo <= 0)
		{
			throw new ArgumentException("factureNo");
		}
		Echeance echeance = SocieteManager.EcheanceGet(factureNo);
		if (echeance == null)
		{
			throw new InvalidOperationException("Impossible de charger la facture.");
		}
		return _ecritureComptaRepository.GetAll(echeance.No, MouvementDomaine.FactureFournisseurTresorerie);
	}

	public IList<EcritureComptable> GetEcrituresImpaye(int reglementNo)
	{
		if (reglementNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorMvtNo);
		}
		Impaye impaye = ImpayeGet(reglementNo);
		if (impaye == null)
		{
			throw new ArgumentNullException("Impaye");
		}
		return _ecritureComptaRepository.GetAll(impaye.ReglementNo, MouvementDomaine.Impaye);
	}

	public IList<EcritureComptable> GetEcrituresImpayeFournisseur(int reglementNo)
	{
		if (reglementNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorMvtNo);
		}
		ImpayeFournisseur impayeFournisseur = ImpayeFournisseurGet(reglementNo);
		if (impayeFournisseur == null)
		{
			throw new ArgumentNullException("Impaye");
		}
		return _ecritureComptaRepository.GetAll(impayeFournisseur.No, MouvementDomaine.Impaye);
	}

	public IList<EcritureComptable> GetEcrituresReglementClient(int reglementNo)
	{
		if (reglementNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorReglementNo);
		}
		return _ecritureComptaRepository.GetAll(reglementNo, MouvementDomaine.ReglementClient);
	}

	public IEnumerable<EcritureComptable> GetEcrituresDossierReglementFournisseur(int dossierNo)
	{
		if (!(SocieteManager.GetDossierReglement(dossierNo) ?? throw new ApplicationException($"Impossible de charger le dossier de règlement [{dossierNo}].")).IsComptabilise)
		{
			return new List<EcritureComptable>();
		}
		List<EcritureComptable> list = new List<EcritureComptable>();
		foreach (LigneDossierReglement item in from x in SocieteManager.GetLignesDossier(dossierNo)
			where x.Type == TypeLigneDossier.Reglement || x.Type == TypeLigneDossier.Retenue
			select x)
		{
			list.AddRange(GetEcrituresReglementFournisseur(item.No));
		}
		list.AddRange(GetEcrituresEcartChangeDossierFournisseur(dossierNo));
		return list;
	}

	public IEnumerable<EcritureComptable> GetEcrituresDossierReglementClientComm(int dossierNo)
	{
		DossierReglement dossierReglement = SocieteManager.GetDossierReglement(dossierNo);
		if (dossierReglement == null)
		{
			throw new ApplicationException($"Impossible de charger le dossier de règlement [{dossierNo}].");
		}
		if (!dossierReglement.IsComptabilise)
		{
			return new List<EcritureComptable>();
		}
		List<EcritureComptable> list = new List<EcritureComptable>();
		IEnumerable<LigneDossierReglementComm> source = SocieteManager.LigneDossierReglementCommGetAll(dossierReglement.No);
		foreach (LigneDossierReglementComm item in source.Where((LigneDossierReglementComm x) => x.TypeLigneDossier == TypeLigneDossier.Reglement || x.TypeLigneDossier == TypeLigneDossier.Retenue).ToList())
		{
			ReglementClient reglementClient = ReglementGet(item.EntityNo);
			if (reglementClient == null)
			{
				throw new ApplicationException($"Impossible de charger le règlement [{item.EntityNo}] Dossier [{dossierReglement.Numero}].");
			}
			IList<EcritureComptable> ecrituresReglementClient = GetEcrituresReglementClient(item.EntityNo);
			if (ecrituresReglementClient == null)
			{
				throw new ApplicationException("Impossible de charger les écritures du règlement [" + reglementClient.Numero + "]");
			}
			list.AddRange(ecrituresReglementClient);
			IEnumerable<EcritureComptable> ecrituresEcartChangeDossierClient = GetEcrituresEcartChangeDossierClient(dossierReglement.No);
			list.AddRange(ecrituresEcartChangeDossierClient);
			foreach (LigneDossierReglementComm item2 in source.Where((LigneDossierReglementComm x) => x.TypeLigneDossier == TypeLigneDossier.Echeance).ToList())
			{
				Echeance echeance = SocieteManager.EcheanceGet(item2.EntityNo);
				if (echeance == null)
				{
					throw new ApplicationException($"Impossible de charger l'échéance de la ligne [{item2.EntityNo}] Dossier [{dossierReglement.Numero}].");
				}
				if (echeance.Type != EcheanceType.FactureGR)
				{
					continue;
				}
				Echeance echeance2 = SocieteManager.EcheanceGet(echeance.No);
				if (echeance2 == null)
				{
					throw new ApplicationException("Impossible de charger la facture GR [" + echeance.DocumentNumero + "].");
				}
				List<Echeance> list2 = SocieteManager.EcheanceGetAllByDocument(echeance2.Domaine, echeance2.DocumentNumero).ToList();
				List<EcritureComptable> list3 = new List<EcritureComptable>();
				foreach (Echeance item3 in list2)
				{
					list3.AddRange(SocieteManager.CaisseManager.GetEcritures(item3.No, MouvementDomaine.EcritureTresorerie).ToList());
				}
				list.AddRange(list3);
			}
		}
		return list;
	}

	public IEnumerable<EcritureComptable> GetEcrituresDossierReglementFournisseurComm(int dossierNo)
	{
		DossierReglement dossierReglement = SocieteManager.GetDossierReglement(dossierNo);
		if (dossierReglement == null)
		{
			throw new ApplicationException($"Impossible de charger le dossier de règlement [{dossierNo}].");
		}
		if (!dossierReglement.IsComptabilise)
		{
			return new List<EcritureComptable>();
		}
		List<EcritureComptable> list = new List<EcritureComptable>();
		IEnumerable<LigneDossierReglementComm> source = SocieteManager.LigneDossierReglementCommGetAll(dossierReglement.No);
		foreach (LigneDossierReglementComm item in source.Where((LigneDossierReglementComm x) => x.TypeLigneDossier == TypeLigneDossier.Reglement || x.TypeLigneDossier == TypeLigneDossier.Retenue).ToList())
		{
			ReglementFournisseur reglementFournisseur = ReglementFournisseurGet(item.EntityNo);
			if (reglementFournisseur == null)
			{
				throw new ApplicationException($"Impossible de charger le règlement [{item.EntityNo}] Dossier [{dossierReglement.Numero}].");
			}
			IEnumerable<EcritureComptable> ecrituresReglementFournisseur = GetEcrituresReglementFournisseur(item.EntityNo);
			if (ecrituresReglementFournisseur == null)
			{
				throw new ApplicationException("Impossible de charger les écritures du règlement [" + reglementFournisseur.Numero + "]");
			}
			list.AddRange(ecrituresReglementFournisseur);
		}
		IEnumerable<EcritureComptable> ecrituresEcartChangeDossierFournisseur = GetEcrituresEcartChangeDossierFournisseur(dossierReglement.No);
		list.AddRange(ecrituresEcartChangeDossierFournisseur);
		foreach (LigneDossierReglementComm item2 in source.Where((LigneDossierReglementComm x) => x.TypeLigneDossier == TypeLigneDossier.Echeance).ToList())
		{
			Echeance echeance = SocieteManager.EcheanceGet(item2.EntityNo);
			if (echeance == null)
			{
				throw new ApplicationException($"Impossible de charger l'échéance de la ligne [{item2.EntityNo}] Dossier [{dossierReglement.Numero}].");
			}
			switch (echeance.Type)
			{
			case EcheanceType.FactureFrsTresorerie:
			{
				Facture facture = SocieteManager.FactureFournisseurGet(echeance.No);
				if (facture == null)
				{
					throw new ApplicationException("Impossible de charger la facture trésorerie [" + echeance.DocumentNumero + "].");
				}
				IList<EcritureComptable> ecrituresFactureFournisseur = GetEcrituresFactureFournisseur(facture.No);
				list.AddRange(ecrituresFactureFournisseur);
				break;
			}
			case EcheanceType.RemboursementFournisseur:
			{
				RemboursementFournisseur remboursementFournisseur = RemboursementFournisseurGet(echeance.No);
				if (remboursementFournisseur == null)
				{
					throw new ApplicationException("Impossible de charger le remboursement fournisseur [" + echeance.DocumentNumero + "].");
				}
				IList<EcritureComptable> ecrituresRemboursementFournisseur = GetEcrituresRemboursementFournisseur(remboursementFournisseur.No);
				list.AddRange(ecrituresRemboursementFournisseur);
				break;
			}
			case EcheanceType.ImpayeFournisseur:
			{
				ImpayeFournisseur impayeFournisseur = ImpayeFournisseurGetByEcheance(echeance.No);
				if (impayeFournisseur == null)
				{
					throw new ApplicationException("Impossible de charger l'impayé fournisseur [" + echeance.DocumentNumero + "].");
				}
				IList<EcritureComptable> ecrituresImpayeFournisseur = GetEcrituresImpayeFournisseur(impayeFournisseur.No);
				list.AddRange(ecrituresImpayeFournisseur);
				break;
			}
			case EcheanceType.FactureGR:
			{
				Echeance echeance2 = SocieteManager.EcheanceGet(echeance.No);
				if (echeance2 == null)
				{
					throw new ApplicationException("Impossible de charger la facture GR [" + echeance.DocumentNumero + "].");
				}
				List<Echeance> list2 = SocieteManager.EcheanceGetAllByDocument(echeance2.Domaine, echeance2.DocumentNumero).ToList();
				List<EcritureComptable> list3 = new List<EcritureComptable>();
				foreach (Echeance item3 in list2)
				{
					list3.AddRange(SocieteManager.CaisseManager.GetEcritures(item3.No, MouvementDomaine.EcritureTresorerie).ToList());
				}
				list.AddRange(list3);
				break;
			}
			}
		}
		return list;
	}

	public IEnumerable<EcritureComptable> GetEcrituresReglementFournisseur(int reglementNo)
	{
		return _ecritureComptaRepository.GetAll(reglementNo, MouvementDomaine.ReglementFournisseur);
	}

	public IEnumerable<EcritureComptable> GetEcrituresReglementFournisseurTraite(int reglementNo, string banqueJournal)
	{
		if (string.IsNullOrEmpty(banqueJournal))
		{
			throw new ArgumentNullException("banqueJournal");
		}
		return from e in GetEcrituresReglementFournisseur(reglementNo)
			where e.CodeJournal == banqueJournal
			select e;
	}

	public IList<EcritureComptable> GetEcrituresRemboursement(int remboursementNo)
	{
		if (remboursementNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorMvtNo);
		}
		return _ecritureComptaRepository.GetEcrituresRemboursement(remboursementNo);
	}

	public IList<EcritureComptable> GetEcrituresRemboursementFournisseur(int no)
	{
		if (no <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorMvtNo);
		}
		RemboursementFournisseur remboursementFournisseur = _remboursementFournisseurRepository.Get(no);
		if (remboursementFournisseur == null)
		{
			throw new ArgumentNullException($"Impossible de charger le remboursement fournisseur [{no}].");
		}
		return _ecritureComptaRepository.GetEcrituresRemboursementFournisseur(remboursementFournisseur.ReglementNo);
	}

	public IList<EcritureComptable> GetEcrituresRemboursementClient(int no)
	{
		if (no <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorMvtNo);
		}
		return _ecritureComptaRepository.GetEcrituresRemboursement(no);
	}

	public IList<EcritureComptable> GetEcrituresRemplacement(int reglementNo)
	{
		if (reglementNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorReglementNo);
		}
		return _ecritureComptaRepository.GetEcritureRemplacement(reglementNo);
	}

	public IList<EcritureComptable> GetEcrituresVersement(int mvtNo)
	{
		if (mvtNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorMvtNo);
		}
		Bordereau bordereau = BordereauGet(mvtNo);
		if (bordereau == null)
		{
			throw new ArgumentNullException("bordereau");
		}
		return _ecritureComptaRepository.GetAll(bordereau.No, MouvementDomaine.EnteteBordereau);
	}

	public IList<EcritureComptable> GetEcrituresVirementInterne(int no)
	{
		if (no <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorMvtNo);
		}
		VirementInterne virementInterne = VirementInterneGet(no);
		if (virementInterne == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorVirementInterneMvtInvalid);
		}
		return _ecritureComptaRepository.GetAll(virementInterne.No, MouvementDomaine.VirementInterne);
	}

	public IList<EcritureComptable> GetEcrituresVirementTiers(int no)
	{
		if (no <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorMvtNo);
		}
		VirementTiers virementTiers = VirementTiersGet(no);
		if (virementTiers == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorVirementInterneMvtInvalid);
		}
		return _ecritureComptaRepository.GetEcrituresVirMasse(virementTiers.No);
	}

	public EcritureComptable GetHistoriqueCompta(int no)
	{
		return _ecritureComptaRepository.Get(no);
	}

	public IEnumerable<LotsSoldeCaisse> GetLotsCaisse(int caisseNo, int modeNo, int deviseNo)
	{
		Societe societe = SocieteManager.Societe;
		Caisse caisse = Get(caisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException("caisseNo");
		}
		SocieteModeReglement mode = societe.GetMode(modeNo);
		if (mode == null)
		{
			throw new ArgumentNullException("modeNo");
		}
		if (!caisse.HasModeReglement(modeNo))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorCaisseMode, caisse.Code, mode.Code));
		}
		return _mouvementCaisseRepository.GetLotsCaisse(caisseNo, modeNo, deviseNo);
	}

	public decimal GetSoldeCaisse(int caisseNo, int modeNo, int deviseNo)
	{
		if ((SocieteManager.Societe.GetCaisse(caisseNo) ?? throw new InvalidOperationException("La caisse est invalide")).GetMode(modeNo) == null)
		{
			throw new InvalidOperationException("Le mode est invalide");
		}
		return SocieteManager.HistoriqueMvtManager.GetSolde(caisseNo, modeNo, deviseNo);
	}

	public IEnumerable<SoldeCaisse> GetSoldeCaisseAllMode(int caisseNo, int deviseNo)
	{
		if (deviseNo < 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorDeviseNo);
		}
		if (caisseNo < 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (Get(caisseNo) == null)
		{
			throw new ArgumentNullException("caisse!");
		}
		return _mouvementCaisseRepository.GetAllSolde(caisseNo, deviseNo);
	}

	public IEnumerable<SoldeCaisse> GetSoldeCaisseOnlyEspece(int caisseNo, int deviseNo)
	{
		if (Get(caisseNo) == null)
		{
			throw new ArgumentNullException("caisse!");
		}
		return _mouvementCaisseRepository.GetAllSoldeEspece(caisseNo, deviseNo);
	}

	public decimal GetSoldeReglementClient(int clientNo)
	{
		Societe societe = SocieteManager.Societe;
		return _reglementClientRepository.GetSoldeReglementClient(societe.No, clientNo);
	}

	public decimal GetSoldeReglementFournisseur(int fournisseurNo)
	{
		Societe societe = SocieteManager.Societe;
		return _reglementFournisseurRepository.GetSoldeReglementFournisseur(societe.No, fournisseurNo);
	}

	public bool HasMouvment(int caisseNo)
	{
		return _caisseRepository.IsCaisseUsed(caisseNo);
	}

	public HistoriqueMvt HistoriqueGet(int no)
	{
		if (no < 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorHistoriqueNo);
		}
		return _historiqueRepository.Get(no);
	}

	public IEnumerable<HistoriqueMvt> HistoriqueGetAll(int mouvementNo)
	{
		if (mouvementNo < 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorMvtNo);
		}
		if (ReglementGet(mouvementNo) == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorMvtNo);
		}
		return _historiqueRepository.GetAllByMouvement(mouvementNo);
	}

	public IEnumerable<HistoriqueMvt> HistoriqueGetAllByCaisse(int caisseNo, int deviseNo)
	{
		if (caisseNo < 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (_caisseRepository.Get(caisseNo) == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		return _historiqueRepository.GetAllByCaisse(caisseNo, deviseNo);
	}

	public IEnumerable<HistoriqueMvt> HistoriqueGetAllByMouvement(int mouvementNo)
	{
		if (mouvementNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorMvtNo);
		}
		return _historiqueRepository.GetAllByMouvement(mouvementNo);
	}

	public ImpayeFournisseur ImpayeFournisseurGet(int reglementNo)
	{
		ReglementFournisseur reglementFournisseur = ReglementFournisseurGet(reglementNo);
		if (reglementFournisseur == null)
		{
			throw new InvalidOperationException(rcRessources.ReglementsInvalides);
		}
		if (reglementFournisseur.IsImpaye == ImpayeEtat.NonImpaye)
		{
			throw new InvalidOperationException("Impossible de charger l'impayé [" + reglementFournisseur.Numero + "].");
		}
		return _impayeFournisseurRepository.Get(reglementNo);
	}

	public ImpayeFournisseur ImpayeFournisseurGetByEcheance(int echeanceNo)
	{
		return _impayeFournisseurRepository.GetByEcheance(echeanceNo);
	}

	public IEnumerable<ImpayeFournisseur> ImpayeFournisseurComptaGetAll()
	{
		Societe societe = SocieteManager.Societe;
		return _impayeFournisseurRepository.GetAllCompta(societe.No);
	}

	public IEnumerable<ImpayeFournisseur> ImpayeFournisseurGetAll()
	{
		Societe societe = SocieteManager.Societe;
		return _impayeFournisseurRepository.GetAll(societe.No);
	}

	public Task<IEnumerable<ImpayeFournisseur>> GetAllImpayeFournisseurAComptaAsync(DateTime dateImpayeMin, DateTime dateImpayeMax, bool isComptabilise, int[] caissesNo, int[] modesNo, CancellationToken cancellationToken)
	{
		Societe societe = SocieteManager.Societe;
		return _impayeFournisseurRepository.GetAllAComptaAsync(dateImpayeMin.Date, dateImpayeMax.Date.AddDays(1.0), isComptabilise, caissesNo, modesNo, societe.No, cancellationToken);
	}

	public Impaye ImpayeGet(int reglementNo)
	{
		if ((ReglementGet(reglementNo) ?? throw new InvalidOperationException(rcRessources.ReglementsInvalides)).IsImpaye == ImpayeEtat.NonImpaye)
		{
			throw new InvalidOperationException(rcRessources.ImpayeMsg);
		}
		return _impayeRepository.Get(reglementNo);
	}

	public IEnumerable<Impaye> ImpayeGetAll()
	{
		Societe societe = SocieteManager.Societe;
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		IEnumerable<AutorisationSouche> allAutorizedSouches = SocieteManager.GetAllAutorizedSouches(ErpDomaine.Vente);
		if (!utilisateur.IsAdmin)
		{
			return _impayeRepository.GetAll(societe.No, allAutorizedSouches.Select((AutorisationSouche a) => a.SoucheNo).ToArray());
		}
		return _impayeRepository.GetAll(societe.No);
	}

	public Task<IEnumerable<Impaye>> ImpayeGetAllAsync(int tiersNo, Societe societe = null)
	{
		societe = societe ?? SocieteManager.Societe;
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		IEnumerable<AutorisationSouche> allAutorizedSouches = SocieteManager.GetAllAutorizedSouches(ErpDomaine.Vente);
		if (!utilisateur.IsAdmin)
		{
			return _impayeRepository.GetAllAsync(tiersNo, societe.No, allAutorizedSouches.Select((AutorisationSouche a) => a.SoucheNo).ToArray());
		}
		return _impayeRepository.GetAllAsync(tiersNo, societe.No);
	}

	public IEnumerable<ImpayeFournisseur> ImpayeFournisseurGetAll(int tiersNo, Societe societe = null)
	{
		societe = societe ?? SocieteManager.Societe;
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		IEnumerable<AutorisationSouche> allAutorizedSouches = SocieteManager.GetAllAutorizedSouches(ErpDomaine.Achat);
		if (!utilisateur.IsAdmin)
		{
			return _impayeFournisseurRepository.GetAll(tiersNo, societe.No, allAutorizedSouches.Select((AutorisationSouche a) => a.SoucheNo).ToArray());
		}
		return _impayeFournisseurRepository.GetAll(tiersNo, societe.No);
	}

	public Impaye ImpayeGetByEcheance(int echeanceNo)
	{
		Echeance echeance = SocieteManager.EcheanceGet(echeanceNo);
		if (echeance == null)
		{
			throw new InvalidOperationException(rcRessources.ImpayeInvalide);
		}
		if (echeance.Type != EcheanceType.Impaye)
		{
			return null;
		}
		return _impayeRepository.Get(echeance.ReglementImpayeNo);
	}

	public int Imputer(int reglementNo, int echeanceNo, decimal montant, int deviseSocieteNo, bool isDossierImpaye, bool isDossierReglementClient = false, bool isImporterFromErp = false, bool useNotification = true)
	{
		if (reglementNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorReglementNo);
		}
		if (echeanceNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorEcheanceNo);
		}
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		ReglementClient reglementClient = _reglementClientRepository.Get(reglementNo);
		if (reglementClient == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementNo);
		}
		Caisse caisse = Get(reglementClient.CaisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Imputation invalide! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grc))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (reglementClient.IsAnnule)
		{
			throw new ApplicationException("Le règlement est annulé!");
		}
		if (reglementClient.ReglementNature != ReglementNature.Reglement)
		{
			throw new ApplicationException("Opération invalide! C'est un règlement d'avance.");
		}
		if (!isDossierReglementClient && reglementClient.IsReserveDossierClt)
		{
			throw new ApplicationException("Le règlement [" + reglementClient.Numero + "] est réservé dans un dossier de règlement client.");
		}
		Echeance echeance = _echeanceRepository.Get(echeanceNo);
		if (echeance == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorEcheanceNo);
		}
		if (echeance.IsReglementAvoir)
		{
			throw new ApplicationException("Opération invalide. [Echéance avoir].");
		}
		if (!isDossierReglementClient && echeance.IsReserveDossierFrs)
		{
			throw new ApplicationException("L'échéance [" + echeance.DocumentNumero + "] est réservée dans un dossier de règlement client.");
		}
		if (!SocieteManager.UserHasAutorisationSouche(echeance.Domaine, echeance.SoucheNo))
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorAutorisationSouche);
		}
		if (echeance.PayeurNo != reglementClient.ClientNo)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorEcheanceClient);
		}
		Societe societe = SocieteManager.Societe;
		if (!isDossierImpaye)
		{
			DossierImpayeManager dossierImpayeManager = SocieteManager.DossierImpayeManager;
			if (dossierImpayeManager.GetLigneImpayeByReglementNo(reglementNo) != null)
			{
				throw new ApplicationException("Le règlement [" + reglementClient.Numero + "] appartient à un dossier règlement impayé!");
			}
			if (echeance.Type == EcheanceType.Impaye && dossierImpayeManager.GetLignesImpayeByEcheanceNo(echeance.No).Any())
			{
				throw new ApplicationException("L'échéance [" + echeance.DocumentNumero + "] appartient à un dossier règlement impayé!");
			}
			if (echeance.Type == EcheanceType.Impaye && societe.HasDossierImpRestriction)
			{
				throw new InvalidOperationException("l'imputation de l'impayé [" + echeance.DocumentNumero + "] n'est possible qu'à partir d'un dossier règlement impayé!");
			}
			if (echeance.Type == EcheanceType.CommissionImpaye || echeance.Type == EcheanceType.InteretImpaye)
			{
				throw new InvalidOperationException($"Opération invalide pour l'échéance [{echeance.DocumentNumero}][{echeance.Type}]!");
			}
		}
		if (isDossierImpaye)
		{
			if (echeance.Type != EcheanceType.Impaye && echeance.Type != EcheanceType.CommissionImpaye && echeance.Type != EcheanceType.InteretImpaye)
			{
				throw new InvalidOperationException($"Opération invalide pour l'échéance [{echeance.DocumentNumero}][{echeance.Type}]!");
			}
			if (echeance.DeviseNo != deviseSocieteNo || reglementClient.DeviseNo != deviseSocieteNo)
			{
				throw new ApplicationException("Dossier règlement impayé ne supporte pas la devise!");
			}
			if (reglementClient.IsComptabilise != EtatComptabilite.NonComptabilise)
			{
				throw new ApplicationException("Le règlement [" + reglementClient.Numero + "] est comptabilisé !");
			}
		}
		if (reglementClient.Solde - montant < 0m)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorMontantInavlide);
		}
		if (echeance.Montant < 0m && echeance.Montant != montant)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorEcheanceNegative);
		}
		if (echeance.Montant > 0m && echeance.Solde < montant)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorEcheanceNegative);
		}
		if (reglementClient.GetAffectations().Any((Affectation x) => x.EcheanceNo == echeanceNo))
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglementAffecteEcheances);
		}
		if (reglementClient.DeviseNo != echeance.DeviseNo)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorDeviseEcheanceReglement);
		}
		SocieteDevise devise = societe.GetDevise(deviseSocieteNo);
		if (devise == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorDeviseNo);
		}
		decimal num = ((societe.UseObjetMetier && reglementClient.Solde - montant == 0m) ? reglementClient.SoldeDeviseSociete : Math.Round(montant * reglementClient.DeviseCours, devise.NombreDecimales, MidpointRounding.AwayFromZero));
		decimal num2 = ((societe.UseObjetMetier && echeance.Solde - montant == 0m) ? echeance.SoldeDeviseSociete : Math.Round(montant * echeance.CoursDevise, devise.NombreDecimales, MidpointRounding.AwayFromZero));
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		int num3 = 0;
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		Affectation affectation = reglementClient.InitNewAffectation(montant, num2, echeanceNo, null);
		affectation.IsImporterFromErp = isImporterFromErp;
		if (echeance.Type == EcheanceType.Solde || echeance.Type == EcheanceType.Erp || echeance.Type == EcheanceType.Impaye || echeance.Type == EcheanceType.FactureGR)
		{
			affectation.NombreJourReglement = (int)Math.Round((reglementClient.DateEcheance.Date - echeance.DocumentDate.Date).TotalDays, MidpointRounding.AwayFromZero);
			affectation.DelaisMoyenPayement = (int)Math.Round((decimal)affectation.NombreJourReglement * montant / echeance.Montant, MidpointRounding.AwayFromZero);
		}
		int? num4 = _affectationRepository.Create(affectation);
		if (!num4.HasValue)
		{
			throw new ApplicationException("Impossible de crée une affectation");
		}
		num3 = num4.Value;
		echeance.Solde -= montant;
		echeance.SoldeDeviseSociete -= num2;
		echeance.DelaisMoyenPayement = EcheanceCalculerDelaisMoyenPayement(echeance.No);
		_echeanceRepository.Update(echeance);
		reglementClient.Solde -= montant;
		reglementClient.SoldeDeviseSociete -= num2;
		reglementClient.DateModification = DateTime.Now;
		reglementClient.ModificateurNo = utilisateur.No;
		_reglementClientRepository.Update(reglementClient);
		if (deviseSocieteNo != reglementClient.DeviseNo && reglementClient.DeviseCours != echeance.CoursDevise)
		{
			DateTime dateTime = DateTime.Now;
			switch (societe.DateAjustement)
			{
			case TypeDateAjustement.DateEcheance:
				dateTime = echeance.DocumentDate;
				break;
			case TypeDateAjustement.DateOperation:
				dateTime = DateTime.Now;
				break;
			case TypeDateAjustement.DateReglement:
				dateTime = reglementClient.Date;
				break;
			}
			EcheanceType type = ((!(echeance.Montant < 0m)) ? ((reglementClient.DeviseCours > echeance.CoursDevise) ? EcheanceType.GainEchange : EcheanceType.PerteEchange) : ((reglementClient.DeviseCours > echeance.CoursDevise) ? EcheanceType.PerteEchange : EcheanceType.GainEchange));
			decimal num5 = default(decimal);
			num5 = num - num2;
			Echeance echeance2 = new Echeance(0, echeance.DocumentNumero, echeance.Domaine, echeance.DocumentType, dateTime, 0m, 0m, dateTime, type, echeance.ModeReglementNo, echeance.SocieteNo, echeance.ClientNo, echeance.ClientCode, echeance.ClientIntitule, echeance.PayeurNo, echeance.PayeurCode, echeance.PayeurIntitule, echeance.DeviseNo, echeance.SoucheNo, echeance.CollaborateurNo, 1m, echeance.Commentaire, num5, 0m)
			{
				UtilisateurNo = utilisateur.No,
				EcheanceReporte = dateTime
			};
			int? num6 = _echeanceRepository.Create(echeance2);
			if (!num6.HasValue || num6.Value <= 0)
			{
				throw new ArgumentException("Impossible de générer une échéance d'écart!");
			}
			if (useNotification)
			{
				_notifyService.Notify(TypeEntity.Echeance, num6.Value, TypeAction.Ajout, reglementClient.SocieteNo);
			}
			Affectation affectation2 = reglementClient.InitNewAffectation(0m, echeance2.MontantDeviseSociete, num6.Value, echeance.No);
			_affectationRepository.Create(affectation2);
			reglementClient.SoldeDeviseSociete -= num5;
			_reglementClientRepository.Update(reglementClient);
		}
		if (useNotification)
		{
			_notifyService.Notify(TypeEntity.Reglement, reglementNo, TypeAction.Modification, reglementClient.SocieteNo);
			_notifyService.Notify(TypeEntity.Echeance, echeanceNo, TypeAction.Modification, reglementClient.SocieteNo);
		}
		transactionScope.Complete();
		return num3;
	}

	public void Imputer_(int reglementNo, int echeanceNo, decimal montant, int deviseSocieteNo)
	{
		if (reglementNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorReglementNo);
		}
		if (echeanceNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorEcheanceNo);
		}
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		ReglementClient reglementClient = _reglementClientRepository.Get(reglementNo);
		if (reglementClient == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementNo);
		}
		Echeance echeance = _echeanceRepository.Get(echeanceNo);
		if (echeance == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorEcheanceNo);
		}
		if (reglementClient.Solde - montant < 0m)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorMontantInavlide);
		}
		Log.Verbose($"IMPUTER_: MT : {montant}");
		Log.Verbose($"IMPUTER_: ECH: {echeance.SoldeDeviseSociete}");
		if (echeance.Montant > 0m && echeance.SoldeDeviseSociete < montant)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorEcheanceNegative);
		}
		SocieteDevise devise = SocieteManager.Societe.GetDevise(deviseSocieteNo);
		if (devise == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorDeviseNo);
		}
		decimal num = Math.Round(montant * echeance.CoursDevise, devise.NombreDecimales, MidpointRounding.AwayFromZero);
		Affectation affectation = reglementClient.InitNewAffectation(montant, num, echeanceNo, null);
		if (echeance.Type == EcheanceType.Solde || echeance.Type == EcheanceType.Erp || echeance.Type == EcheanceType.Impaye || echeance.Type == EcheanceType.FactureGR)
		{
			affectation.NombreJourReglement = (int)Math.Round((reglementClient.DateEcheance.Date - echeance.DocumentDate.Date).TotalDays, MidpointRounding.AwayFromZero);
			affectation.DelaisMoyenPayement = (int)Math.Round((decimal)affectation.NombreJourReglement * montant / echeance.Montant, MidpointRounding.AwayFromZero);
		}
		_affectationRepository.Create(affectation);
		echeance.Solde -= montant;
		echeance.SoldeDeviseSociete -= num;
		echeance.DelaisMoyenPayement = EcheanceCalculerDelaisMoyenPayement(echeance.No);
		_echeanceRepository.Update(echeance);
		reglementClient.Solde -= montant;
		reglementClient.SoldeDeviseSociete -= num;
		reglementClient.DateModification = DateTime.Now;
		reglementClient.ModificateurNo = utilisateur.No;
		_reglementClientRepository.Update(reglementClient);
	}

	public bool IsModeUsed(int modeNo, int caisseNo)
	{
		return _caisseModeReglementRepository.IsModeUsed(modeNo, caisseNo);
	}

	public void LettreEcriture(IEnumerable<EcritureComptable> ecritures, string lettre)
	{
		_ecritureComptaRepository.LettreEcritures(ecritures.Select((EcritureComptable x) => x.No).ToArray(), lettre);
	}

	public void DeLettrerEcriture(IEnumerable<EcritureComptable> ecritures)
	{
		_ecritureComptaRepository.DeLettrerEcritures(ecritures.Select((EcritureComptable x) => x.No).ToArray());
	}

	public void MettreEnSommeilCaisse(int caisseNo)
	{
		Caisse caisse = _caisseRepository.Get(caisseNo);
		if (caisse == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("La caisse est déjà en sommeil!");
		}
		if (!_mouvementCaisseRepository.IsCaisseVide(caisseNo))
		{
			throw new ApplicationException("La caisse n'est pas vide!");
		}
		caisse.EnSommeil = true;
		_caisseRepository.Update(caisse);
	}

	public IEnumerable<CaisseModeReglement> ModeGetAll(int caisseNo)
	{
		return _caisseModeReglementRepository.GetAll(caisseNo);
	}

	public IEnumerable<MouvementCaisseEspece> MouvementCaisseGetAllEspece(int[] caisseNo, int deviseNo, DateTime dateDu, DateTime dateA, bool useDateCreation)
	{
		return _mouvementCaisseEspeceRepository.GetAll(caisseNo, deviseNo, dateDu, dateA, useDateCreation);
	}

	public IEnumerable<LotSoldeCaisseEspece> MouvementCaisseGetAllLotEspece(int[] caisseNo, int deviseNo, DateTime dateDu, DateTime dateA, bool useDateCreation)
	{
		return _mouvementCaisseEspeceRepository.GetAllLot(caisseNo, deviseNo, dateDu, dateA, useDateCreation);
	}

	public void NoteCreate(Note note)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		if (note == null)
		{
			throw new ArgumentNullException("note");
		}
		if (note.EntiteNo == 0)
		{
			throw new ApplicationException("Entité non spécifiée dans la note!");
		}
		if (string.IsNullOrEmpty(note.Text))
		{
			throw new ApplicationException("Le texte de la note est obligatoire!");
		}
		if (note.HasPieceJointe && (string.IsNullOrEmpty(note.NomPieceJointe) || note.PieceJointe == null))
		{
			throw new ApplicationException("Piece jointe obligatoire!");
		}
		note.No = _noteRepository.Create(note);
		NotificationListeEntite(note, TypeAction.Ajout);
	}

	public void NoteDelete(Note note)
	{
		if (note == null)
		{
			throw new ArgumentNullException("note");
		}
		_noteRepository.Delete(note);
		NotificationListeEntite(note, TypeAction.Suppression);
	}

	public void NoteUpdate(Note note)
	{
		if (note == null)
		{
			throw new ArgumentNullException("note");
		}
		_noteRepository.Update(note);
		NotificationListeEntite(note, TypeAction.Modification);
	}

	public void NotificationListeEntite(Note note, TypeAction action)
	{
		switch (note.EntiteType)
		{
		case TypeEntity.Bordereau:
		{
			Bordereau bordereau = _bordereauRepository.Get(note.EntiteNo);
			if (bordereau == null)
			{
				throw new ApplicationException(TresorerieCoreMessages.ErrorBordereauNo);
			}
			_notifyService.Notify(TypeEntity.Bordereau, bordereau.No, TypeAction.Modification, bordereau.SocieteNo);
			_notifyService.Notify(TypeEntity.Note, note.No, action, note.SocieteNo);
			break;
		}
		case TypeEntity.BordereauVirement:
		{
			BordereauVirement bordereauVirement = _bordereauVirementRepository.Get(note.EntiteNo);
			if (bordereauVirement == null)
			{
				throw new ApplicationException(TresorerieCoreMessages.ErrorBordereauNo);
			}
			_notifyService.Notify(TypeEntity.BordereauVirement, bordereauVirement.No, TypeAction.Modification, bordereauVirement.SocieteNo);
			break;
		}
		case TypeEntity.Echeance:
		{
			Echeance echeance = _echeanceRepository.Get(note.EntiteNo);
			if (echeance == null)
			{
				throw new ArgumentException(TresorerieCoreMessages.ErrorEcheanceNo);
			}
			_notifyService.Notify(TypeEntity.Echeance, echeance.No, TypeAction.Modification, echeance.SocieteNo);
			_notifyService.Notify(TypeEntity.Note, note.No, action, note.SocieteNo);
			break;
		}
		case TypeEntity.Reglement:
		{
			ReglementClient reglementClient = _reglementClientRepository.Get(note.EntiteNo);
			if (reglementClient == null)
			{
				throw new ArgumentException(TresorerieCoreMessages.ErrorReglementNo);
			}
			_notifyService.Notify(TypeEntity.Reglement, reglementClient.No, TypeAction.Modification, reglementClient.SocieteNo);
			_notifyService.Notify(TypeEntity.Note, note.No, action, note.SocieteNo);
			break;
		}
		case TypeEntity.Dossier:
		{
			DossierReglement dossier2 = _dossierReglementRepository.GetDossier(note.EntiteNo);
			if (dossier2 == null)
			{
				throw new ArgumentException("Impossible de charger le dossier de règlement.");
			}
			_notifyService.Notify(TypeEntity.Dossier, dossier2.No, TypeAction.Modification, dossier2.SocieteNo);
			break;
		}
		case TypeEntity.DossierClient:
		{
			DossierReglement dossier = _dossierReglementRepository.GetDossier(note.EntiteNo);
			if (dossier == null)
			{
				throw new ArgumentException("Impossible de charger le dossier de règlement.");
			}
			_notifyService.Notify(TypeEntity.DossierClient, dossier.No, TypeAction.Modification, dossier.SocieteNo);
			break;
		}
		case TypeEntity.ReglementFournisseur:
		{
			ReglementFournisseur reglementFournisseur = _reglementFournisseurRepository.Get(note.EntiteNo);
			if (reglementFournisseur == null)
			{
				throw new ArgumentException(TresorerieCoreMessages.ErrorReglementNo);
			}
			_notifyService.Notify(TypeEntity.ReglementFournisseur, reglementFournisseur.No, TypeAction.Modification, reglementFournisseur.SocieteNo);
			break;
		}
		case TypeEntity.Tiers:
			_notifyService.Notify(TypeEntity.Tiers, note.EntiteNo, TypeAction.Modification, note.SocieteNo);
			_notifyService.Notify(TypeEntity.Note, note.No, action, note.SocieteNo);
			break;
		default:
			throw new ApplicationException(TresorerieCoreMessages.EntiteNonImplementee);
		}
	}

	public void PointerTraiteFournisseur(int reglementNo, bool isDecaisse, DateTime dateValeur)
	{
		ReglementFournisseur reglementFournisseur = _reglementFournisseurRepository.Get(reglementNo);
		if (reglementFournisseur == null)
		{
			throw new ApplicationException($"Impossible de charger le règlement [{reglementNo}]");
		}
		if (reglementFournisseur.IsAnnule)
		{
			throw new ApplicationException("Le règlement est annulé.");
		}
		_reglementFournisseurRepository.PointerTraite(reglementFournisseur.No, isDecaisse, dateValeur);
	}

	public void ReglementAjuster(ReglementClient reglement)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		if (reglement == null)
		{
			throw new ArgumentNullException("reglement");
		}
		reglement.Ajuste = true;
		reglement.ModificateurNo = utilisateur.No;
		reglement.DateModification = DateTime.Now;
		_reglementClientRepository.Update(reglement);
	}

	public void ReglementFournisseurAjuster(ReglementFournisseur reglement)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		if (reglement == null)
		{
			throw new ArgumentNullException("reglement");
		}
		reglement.Ajuste = true;
		reglement.ModificateurNo = utilisateur.No;
		reglement.DateModification = DateTime.Now;
		_reglementFournisseurRepository.Update(reglement);
	}

	public void ReglementClientAnnuler(int reglementNo)
	{
		ReglementClient reglementClient = _reglementClientRepository.Get(reglementNo);
		if (reglementClient == null)
		{
			throw new ApplicationException($"Impossible de charger le règlement [{reglementNo}].");
		}
		if (reglementClient.IsAnnule)
		{
			throw new ApplicationException("Le règlement [" + reglementClient.Numero + "] est déjà annulé!");
		}
		if (reglementClient.IsImpaye == ImpayeEtat.Impaye)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglementImpaye);
		}
		if (reglementClient.IsRemis != Remis.NonRemis)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglementRemis);
		}
		if (reglementClient.IsReglementAvoir)
		{
			throw new ApplicationException("Opération invalide. Règlement d'avoir.");
		}
		List<HistoriqueMvt> list = reglementClient.GetHistoriques().ToList();
		if (list.Any((HistoriqueMvt x) => x.Statut == StatutTransfert.Envoye || x.Statut == StatutTransfert.Saisie))
		{
			throw new ApplicationException("Le règlement [" + reglementClient.Numero + "] existe dans un transfert non encore validé!");
		}
		if (reglementClient.Type == ReglementType.Espece && list.Count > 1)
		{
			throw new ApplicationException("Le règlement espèce [" + reglementClient.Numero + "] possède des mouvements !");
		}
		if (reglementClient.GetAffectations().Any())
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementAffecteEcheances);
		}
		if (reglementClient.GetRemplacements().Any())
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementRemplace);
		}
		if (reglementClient.GetMesRemplacants().Any())
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementRemplace);
		}
		if (reglementClient.IsPointe)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglementRapproche);
		}
		if (reglementClient.Solde != reglementClient.Montant)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglementAffecteEcheances);
		}
		if (reglementClient.IsRemplacer)
		{
			throw new ApplicationException("Impossible d'annuler un règlement remplacé!");
		}
		if (reglementClient.IsSynchroniser)
		{
			throw new ApplicationException("Le règlement est synchronisé dans l'ERP.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		HistoriqueMvt historiqueMvt = list.SingleOrDefault((HistoriqueMvt x) => x.Sens == SensMouvement.Entree && !x.Lot.IsEpuise);
		if (historiqueMvt != null)
		{
			historiqueMvt.Lot.MontantRestant = 0m;
			_historiqueRepository.Update(historiqueMvt);
		}
		reglementClient.IsAnnule = true;
		_reglementClientRepository.AnnulerReglement(reglementNo, isAnnule: true);
		_notifyService.Notify(TypeEntity.Reglement, reglementNo, TypeAction.Modification, reglementClient.SocieteNo);
		transactionScope.Complete();
	}

	public void ReglementClientAnnulerChangementDevise(int reglementNo)
	{
		Societe societe = SocieteManager.Societe;
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		DeviseManager deviseManager = SocieteManager.Groupe.DeviseManager;
		ReglementClient reglementClient = _reglementClientRepository.Get(reglementNo);
		if (reglementClient == null)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorReglementNo);
		}
		if (!reglementClient.IsAffDevise)
		{
			throw new InvalidOperationException("Opération invalide!");
		}
		Caisse caisse = Get(reglementClient.CaisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grc))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (reglementClient.IsAnnule)
		{
			throw new ApplicationException("Le règlement est annulé!");
		}
		if (reglementClient.IsReglementAvoir)
		{
			throw new ApplicationException("Opération invalide. Règlement d'avoir.");
		}
		if (reglementClient.IsRemis != Remis.NonRemis)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementRemis);
		}
		if (reglementClient.GetAffectations().ToList().Any())
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementAffecteEcheances);
		}
		if (reglementClient.GetRemplacements().ToList().Any())
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementRemplace);
		}
		if (reglementClient.GetMesRemplacants().ToList().Any())
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementRemplace);
		}
		if (reglementClient.IsComptabilise == EtatComptabilite.Comptabilise)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorrReglementTransfert);
		}
		if (reglementClient.IsPointe)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglementRapproche);
		}
		Devise devise = deviseManager.Get(reglementClient.DeviseNo);
		if (devise == null)
		{
			throw new InvalidOperationException(rcRessources.DeviseNotFound);
		}
		reglementClient.ChangeDeviseAffectation(reglementClient.DeviseOrigineNo, reglementClient.DeviseCoursOrigine, reglementClient.MtDevOrigine, devise.NombreDecimales);
		reglementClient.DeviseOrigineNo = 0;
		reglementClient.MtDevOrigine = 0m;
		reglementClient.DeviseCoursOrigine = 0m;
		reglementClient.DateModification = DateTime.Now;
		reglementClient.ModificateurNo = utilisateur.No;
		reglementClient.IsAffDevise = false;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_reglementClientRepository.Update(reglementClient);
		_notifyService.Notify(TypeEntity.Reglement, reglementNo, TypeAction.Modification, societe.No);
		transactionScope.Complete();
	}

	public void ReglementClientAnnulerPreavis(int reglementNo)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		ReglementClient reglementClient = _reglementClientRepository.Get(reglementNo);
		if (reglementClient == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorReglementNo);
		}
		if (reglementClient.Preavis != EtatPreavis.Preavis)
		{
			throw new ApplicationException("Le règlement " + reglementClient.Numero + " est à l'étape " + reglementClient.Preavis.GetDisplayDescription() + ".");
		}
		if (reglementClient.TiersType != TiersType.Client)
		{
			throw new InvalidOperationException("Le tiers [" + reglementClient.ClientCode + "] du règlement [" + reglementClient.Numero + "] est [" + reglementClient.TiersType.GetDisplayDescription() + "]!");
		}
		Caisse caisse = Get(reglementClient.CaisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Opération invalide! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grc))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (reglementClient.GetModeReglement().Type != ReglementType.Cheque)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorOperationInvalide);
		}
		reglementClient.DateModification = DateTime.Now;
		reglementClient.ModificateurNo = utilisateur.No;
		reglementClient.Preavis = EtatPreavis.NonPreavis;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_reglementClientRepository.Update(reglementClient);
		_notifyService.Notify(TypeEntity.Reglement, reglementNo, TypeAction.Modification, reglementClient.SocieteNo);
		_notifyService.Notify(TypeEntity.Bordereau, reglementClient.EnteteBordereauNo.GetValueOrDefault(), TypeAction.Modification, reglementClient.SocieteNo);
		transactionScope.Complete();
	}

	public void ReglementClientAjouterPreavis(int reglementNo, DateTime datePreavis)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		ReglementClient reglementClient = _reglementClientRepository.Get(reglementNo);
		if (reglementClient == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorReglementNo);
		}
		if (reglementClient.Preavis != EtatPreavis.NonPreavis)
		{
			throw new ApplicationException("Le règlement " + reglementClient.Numero + " est préavisé");
		}
		RemboursementFournisseur byReglementNo = _remboursementFournisseurRepository.GetByReglementNo(reglementNo);
		if (byReglementNo != null)
		{
			throw new ApplicationException("Le règlement est lié à le remboursement fournisseur [" + byReglementNo.Numero + "]!");
		}
		if (reglementClient.TiersType != TiersType.Client)
		{
			throw new InvalidOperationException("Le tiers [" + reglementClient.ClientCode + "] du règlement [" + reglementClient.Numero + "] est [" + reglementClient.TiersType.GetDisplayDescription() + "]!");
		}
		Caisse caisse = Get(reglementClient.CaisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Opération invalide! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grc))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (reglementClient.IsAnnule)
		{
			throw new ApplicationException("Le règlement est annulé!");
		}
		if (reglementClient.GetModeReglement().Type != ReglementType.Cheque)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorOperationInvalide);
		}
		if (reglementClient.IsRemis != Remis.RemisBanque)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglementNonRemis);
		}
		if (reglementClient.BordereauNature == 1 && reglementClient.IsPointe)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglementRapprocheInfo);
		}
		if ((reglementClient.BordereauNature == 0 || reglementClient.BordereauNature == 2) && reglementClient.IsEscompteRegle)
		{
			throw new InvalidOperationException("Opération invalide! Règlement est déjà réglé.");
		}
		if (reglementClient.IsImpaye != ImpayeEtat.NonImpaye)
		{
			throw new ApplicationException("Le règlement " + reglementClient.Numero + " est impayé.");
		}
		if (reglementClient.IsRemis != Remis.RemisBanque)
		{
			throw new ApplicationException("Le règlement " + reglementClient.Numero + " non remis à la banque.");
		}
		reglementClient.DateModification = DateTime.Now;
		reglementClient.ModificateurNo = utilisateur.No;
		reglementClient.Preavis = EtatPreavis.Preavis;
		reglementClient.PreavisDate = datePreavis;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_reglementClientRepository.Update(reglementClient);
		_notifyService.Notify(TypeEntity.Reglement, reglementNo, TypeAction.Modification, reglementClient.SocieteNo);
		_notifyService.Notify(TypeEntity.Bordereau, reglementClient.EnteteBordereauNo.GetValueOrDefault(), TypeAction.Modification, reglementClient.SocieteNo);
		transactionScope.Complete();
	}

	public decimal GetTotalPreavisClient(int clientNo)
	{
		Societe societe = SocieteManager.Societe;
		return _reglementClientRepository.GetMontantPreavisClient(societe.No, clientNo);
	}

	public void ReglementClientPreavisRegulariserPayer(int reglementNo)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		ReglementClient reglementClient = _reglementClientRepository.Get(reglementNo);
		if (reglementClient == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorReglementNo);
		}
		if (reglementClient.Preavis != EtatPreavis.Preavis)
		{
			throw new ApplicationException("Le règlement " + reglementClient.Numero + " n'est pas préavisé.");
		}
		RemboursementFournisseur byReglementNo = _remboursementFournisseurRepository.GetByReglementNo(reglementNo);
		if (byReglementNo != null)
		{
			throw new ApplicationException("Le règlement est lié à le remboursement fournisseur [" + byReglementNo.Numero + "]!");
		}
		if (reglementClient.TiersType != TiersType.Client)
		{
			throw new InvalidOperationException("Le tiers [" + reglementClient.ClientCode + "] du règlement [" + reglementClient.Numero + "] est [" + reglementClient.TiersType.GetDisplayDescription() + "]!");
		}
		Caisse caisse = Get(reglementClient.CaisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Opération invalide! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grc))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (reglementClient.IsAnnule)
		{
			throw new ApplicationException("Le règlement est annulé!");
		}
		ReglementType type = reglementClient.GetModeReglement().Type;
		if (type != ReglementType.Traite && type != ReglementType.Cheque)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorOperationInvalide);
		}
		if (reglementClient.IsRemis != Remis.RemisBanque)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglementNonRemis);
		}
		if (reglementClient.BordereauNature == 1 && reglementClient.IsPointe)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglementRapprocheInfo);
		}
		if ((reglementClient.BordereauNature == 0 || reglementClient.BordereauNature == 2) && reglementClient.IsEscompteRegle)
		{
			throw new InvalidOperationException("Opération invalide! Règlement est déjà réglé.");
		}
		if (reglementClient.IsImpaye != ImpayeEtat.NonImpaye)
		{
			throw new ApplicationException("Le règlement " + reglementClient.Numero + " est impayé.");
		}
		if (reglementClient.IsRemis != Remis.RemisBanque)
		{
			throw new ApplicationException("Le règlement " + reglementClient.Numero + " non remis à la banque.");
		}
		reglementClient.DateModification = DateTime.Now;
		reglementClient.ModificateurNo = utilisateur.No;
		reglementClient.Preavis = EtatPreavis.RegularisePaye;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_reglementClientRepository.Update(reglementClient);
		_notifyService.Notify(TypeEntity.Reglement, reglementNo, TypeAction.Modification, reglementClient.SocieteNo);
		_notifyService.Notify(TypeEntity.Bordereau, reglementClient.EnteteBordereauNo.GetValueOrDefault(), TypeAction.Modification, reglementClient.SocieteNo);
		transactionScope.Complete();
	}

	public void ReglementClientChangerDevise(int reglementNo, decimal montantDevise, int deviseNo, decimal coursDevise)
	{
		DeviseManager deviseManager = SocieteManager.Groupe.DeviseManager;
		if (deviseNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorDeviseNo, "deviseNo");
		}
		if (montantDevise.Equals(0m))
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorMontant);
		}
		ReglementClient reglementClient = _reglementClientRepository.Get(reglementNo);
		if (reglementClient == null)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorReglementNo);
		}
		if (reglementClient.IsAffDevise)
		{
			throw new InvalidOperationException("Opération invalide!");
		}
		if (reglementClient.DeviseNo == deviseNo)
		{
			throw new InvalidOperationException("Opération invalide!");
		}
		Caisse caisse = Get(reglementClient.CaisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		Devise devise = deviseManager.Get(deviseNo);
		if (devise == null)
		{
			throw new InvalidOperationException(rcRessources.DeviseNotFound);
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grc))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (reglementClient.IsAnnule)
		{
			throw new ApplicationException("Le règlement est annulé!");
		}
		if (reglementClient.IsReglementAvoir)
		{
			throw new ApplicationException("Opération invalide. Règlement d'avoir.");
		}
		reglementClient.DeviseOrigineNo = reglementClient.DeviseNo;
		reglementClient.MtDevOrigine = reglementClient.Montant;
		reglementClient.DeviseCoursOrigine = reglementClient.DeviseCours;
		if (reglementClient.IsRemis != Remis.NonRemis)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementRemis);
		}
		if (reglementClient.GetHistoriques().ToList().Count() != 1)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorrReglementTransfert);
		}
		if (reglementClient.GetAffectations().ToList().Any())
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementAffecteEcheances);
		}
		if (reglementClient.GetRemplacements().ToList().Any())
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementRemplace);
		}
		if (reglementClient.GetMesRemplacants().ToList().Any())
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementRemplace);
		}
		if (reglementClient.IsComptabilise == EtatComptabilite.Comptabilise)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorrReglementTransfert);
		}
		if (reglementClient.IsPointe)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglementRapproche);
		}
		reglementClient.ChangeDeviseAffectation(deviseNo, coursDevise, montantDevise, devise.NombreDecimales);
		Societe societe = SocieteManager.Societe;
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		reglementClient.DateModification = DateTime.Now;
		reglementClient.ModificateurNo = utilisateur.No;
		reglementClient.IsAffDevise = true;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_reglementClientRepository.Update(reglementClient);
		_notifyService.Notify(TypeEntity.Reglement, reglementNo, TypeAction.Modification, societe.No);
		transactionScope.Complete();
	}

	public Tiers TiersViewGet(int no)
	{
		Societe societe = SocieteManager.Societe;
		return _viewTiersRepository.Get(societe.No, no);
	}

	public void DeleteRemboursementFournisseur(int remboursementNo)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		RemboursementFournisseur remboursementFournisseur = _remboursementFournisseurRepository.Get(remboursementNo);
		if (remboursementFournisseur == null)
		{
			throw new ApplicationException($"Impossible de charger le remboursement fournisseur [{remboursementNo}].");
		}
		ReglementClient reglementClient = _reglementClientRepository.Get(remboursementFournisseur.ReglementNo);
		if (reglementClient == null)
		{
			throw new ApplicationException($"Impossible de charger le règlement [{remboursementFournisseur.ReglementNo}].");
		}
		if (reglementClient.TiersType != TiersType.Fournisseur)
		{
			throw new InvalidOperationException("Le type tiers du règlement [" + reglementClient.Numero + "] est invalide!");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		ReglementClientDeleteInterne(remboursementFournisseur.ReglementNo);
		Caisse caisse = Get(remboursementFournisseur.CaisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Opération invalide! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grc))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (remboursementFournisseur.Comptabilise != EtatComptabilite.NonComptabilise)
		{
			throw new InvalidOperationException("Le remboursement est comptabilisé!");
		}
		Echeance echeance = _echeanceRepository.Get(remboursementFournisseur.No);
		if (echeance == null)
		{
			throw new ApplicationException("Echéance remboursement fournisseur invalide!");
		}
		if (echeance.Type != EcheanceType.RemboursementFournisseur)
		{
			throw new ApplicationException("Type échéance invalide!");
		}
		if (echeance.IsComptaRemboursementClient || echeance.IsComptaVirementTiers)
		{
			throw new ApplicationException("Le remboursement est comptabilisé!");
		}
		if (echeance.Solde != remboursementFournisseur.Montant || echeance.SoldeDeviseSociete != remboursementFournisseur.MontantDeviseSociete)
		{
			throw new ApplicationException("Echéance remboursement est partiellement payée!");
		}
		if (echeance.IsReserveDossierFrs)
		{
			throw new ApplicationException("Le remboursement [" + echeance.DocumentNumero + "] est réservée pour un dossier de règlement fournisseur.");
		}
		if (SocieteManager.IsEcheanceUtiliseDossierFrs(echeance.No))
		{
			throw new ApplicationException("Le remboursement [" + echeance.DocumentNumero + "] est utilisée dans un dossier de règlement fournisseur.");
		}
		_echeanceRepository.Delete(echeance);
		_notifyService.Notify(TypeEntity.Echeance, echeance.No, TypeAction.Suppression, echeance.SocieteNo);
		_notifyService.Notify(TypeEntity.RemboursementFournisseur, echeance.No, TypeAction.Suppression, remboursementFournisseur.SocieteNo);
		transactionScope.Complete();
	}

	private void ReglementClientDeleteInterne(int reglementNo)
	{
		ReglementClient reglementClient = _reglementClientRepository.Get(reglementNo);
		if (reglementClient == null)
		{
			throw new ApplicationException($"Impossible de charger le règlement [{reglementNo}].");
		}
		Caisse caisse = Get(reglementClient.CaisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Impossible de supprimer le règlement! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grc))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (reglementClient.IsAnnule)
		{
			throw new ApplicationException("Le règlement est annulé!");
		}
		if (reglementClient.IsComptabilise == EtatComptabilite.Comptabilise)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglementComptabilise);
		}
		if (reglementClient.IsRemis != Remis.NonRemis)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglementRemis);
		}
		if (reglementClient.IsImpaye != ImpayeEtat.NonImpaye)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglementImpaye);
		}
		if (reglementClient.GetAffectations().Any((Affectation x) => x.GetEcheance().Type != EcheanceType.DroitTimbreClient))
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglementAffecteEcheances);
		}
		if (reglementClient.GetRemplacements().Any())
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglementRemplace);
		}
		if (reglementClient.GetMesRemplacants().Any())
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglementRemplace);
		}
		Caution byReglementNo = _cautionRepository.GetByReglementNo(reglementNo);
		if (byReglementNo != null)
		{
			throw new ApplicationException("Le règlement [" + reglementClient.Numero + "] est associé au caution [" + byReglementNo.Numero + "]!");
		}
		if (reglementClient.GetHistoriques().Count() != 1)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorrReglementTransfert);
		}
		if (reglementClient.IsPointe)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglementRapproche);
		}
		if (reglementClient.SoumisDroitTimbre)
		{
			Affectation affectation = reglementClient.GetAffectations().SingleOrDefault((Affectation x) => x.GetEcheance().Type == EcheanceType.DroitTimbreClient);
			if (affectation == null)
			{
				throw new ApplicationException("Impossible de charger l'affectation de l'échéance du droit de timbre.");
			}
			Echeance echeance = _echeanceRepository.Get(affectation.EcheanceNo);
			if (echeance == null)
			{
				throw new ApplicationException("Impossible de déterminer l'échéance du droit de timbre.[" + reglementClient.Numero + "]");
			}
			_affectationRepository.Delete(affectation);
			_echeanceRepository.Delete(echeance);
			_notifyService.Notify(TypeEntity.Echeance, echeance.No, TypeAction.Suppression, reglementClient.SocieteNo);
		}
		HistoriqueMvt historiqueMvt = reglementClient.GetHistoriques().Single();
		_historiqueRepository.Delete(historiqueMvt);
		_reglementClientRepository.Delete(reglementClient);
		foreach (Note item in _noteRepository.GetAllNotesByEntite(reglementClient.No, TypeEntity.Reglement))
		{
			_noteRepository.Delete(item);
		}
		if (reglementClient.IsReglementAvoir)
		{
			Echeance echeance2 = _echeanceRepository.Get(reglementClient.EcheanceAvoirNo);
			if (echeance2 == null)
			{
				throw new ApplicationException("Impossible de charger l'avoir n°[" + reglementClient.EcheanceAvoirNumero + "].");
			}
			echeance2.Solde = echeance2.Montant;
			echeance2.SoldeDeviseSociete = echeance2.MontantDeviseSociete;
			echeance2.ReglementAvoirNo = 0;
			echeance2.ReglementAvoirNumero = string.Empty;
			_echeanceRepository.Update(echeance2);
			_notifyService.Notify(TypeEntity.Echeance, echeance2.No, TypeAction.Modification, reglementClient.SocieteNo);
		}
		_notifyService.Notify(TypeEntity.Reglement, reglementNo, TypeAction.Suppression, reglementClient.SocieteNo);
	}

	public void ReglementClientDelete(int reglementNo)
	{
		RemboursementFournisseur byReglementNo = _remboursementFournisseurRepository.GetByReglementNo(reglementNo);
		if (byReglementNo != null)
		{
			throw new ApplicationException("Le règlement est lié à le remboursement fournisseur [" + byReglementNo.Numero + "]!");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		ReglementClientDeleteInterne(reglementNo);
		transactionScope.Complete();
	}

	public void ReglementFournisseurUpdateInfoLibre(int reglementNo, string info1, string info2, string info3, string info4)
	{
		if (reglementNo <= 0)
		{
			throw new ApplicationException("reglementNo");
		}
		ReglementFournisseur reglementFournisseur = _reglementFournisseurRepository.Get(reglementNo);
		if (reglementFournisseur == null)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorReglementNo);
		}
		if (reglementFournisseur.IsAnnule)
		{
			throw new ApplicationException("Le règlement est annulé.");
		}
		Caisse caisse = Get(reglementFournisseur.CaisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grf))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (reglementFournisseur.IsComptabilise == EtatComptabilite.Comptabilise)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementComptabilise);
		}
		Societe societe = SocieteManager.Societe;
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		reglementFournisseur.Info1 = info1;
		reglementFournisseur.Info2 = info2;
		reglementFournisseur.Info3 = info3;
		reglementFournisseur.Info4 = info4;
		reglementFournisseur.DateModification = DateTime.Now;
		reglementFournisseur.ModificateurNo = utilisateur.No;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_reglementFournisseurRepository.Update(reglementFournisseur);
		_notifyService.Notify(TypeEntity.ReglementFournisseur, reglementNo, TypeAction.Modification, societe.No);
		transactionScope.Complete();
	}

	public void ReglementClientUpdateInfoLibre(int reglementNo, string info1, string info2, string info3, string info4)
	{
		if (reglementNo <= 0)
		{
			throw new ApplicationException("reglementNo");
		}
		ReglementClient reglementClient = _reglementClientRepository.Get(reglementNo);
		if (reglementClient == null)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorReglementNo);
		}
		Caisse caisse = Get(reglementClient.CaisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grc))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (reglementClient.IsAnnule)
		{
			throw new ApplicationException("Le règlement est annulé!");
		}
		if (reglementClient.IsComptabilise == EtatComptabilite.Comptabilise)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementComptabilise);
		}
		Societe societe = SocieteManager.Societe;
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		reglementClient.Info1 = info1;
		reglementClient.Info2 = info2;
		reglementClient.Info3 = info3;
		reglementClient.Info4 = info4;
		reglementClient.DateModification = DateTime.Now;
		reglementClient.ModificateurNo = utilisateur.No;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_reglementClientRepository.Update(reglementClient);
		_notifyService.Notify(TypeEntity.Reglement, reglementNo, TypeAction.Modification, societe.No);
		transactionScope.Complete();
	}

	public void ReglementClientUpdateInfoRetenue(int reglementNo, decimal baseRetenue, decimal tauxRetenue)
	{
		if (reglementNo <= 0)
		{
			throw new ApplicationException("reglementNo");
		}
		if (baseRetenue <= 0m)
		{
			throw new ApplicationException("Base retenue invalide.");
		}
		if (tauxRetenue <= 0m)
		{
			throw new ApplicationException("Taux retenue invalide;");
		}
		ReglementClient reglementClient = _reglementClientRepository.Get(reglementNo);
		if (reglementClient == null)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorReglementNo);
		}
		Caisse caisse = Get(reglementClient.CaisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grc))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (reglementClient.GetModeReglement().Type != ReglementType.Autre)
		{
			throw new ApplicationException("Le règlement [" + reglementClient.Numero + "] n'est pas de type retenue.");
		}
		if (reglementClient.IsAnnule)
		{
			throw new ApplicationException("Le règlement est annulé!");
		}
		if (reglementClient.IsComptabilise == EtatComptabilite.Comptabilise)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorrReglementTransfert);
		}
		if (reglementClient.IsPointe)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglementRapproche);
		}
		Societe societe = SocieteManager.Societe;
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		reglementClient.BaseRetenue = baseRetenue;
		reglementClient.TauxRetenue = tauxRetenue;
		reglementClient.DateModification = DateTime.Now;
		reglementClient.ModificateurNo = utilisateur.No;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_reglementClientRepository.Update(reglementClient);
		_notifyService.Notify(TypeEntity.Reglement, reglementNo, TypeAction.Modification, societe.No);
		transactionScope.Complete();
	}

	public void ReglementClientUpdateNumero(int reglementNo, string newNumero)
	{
		if (reglementNo <= 0)
		{
			throw new ArgumentNullException("reglementNo");
		}
		if (string.IsNullOrEmpty(newNumero))
		{
			throw new ArgumentNullException("newNumero");
		}
		Societe societe = SocieteManager.Societe;
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		ReglementClient reglementClient = _reglementClientRepository.Get(reglementNo);
		if (reglementClient == null)
		{
			throw new ArgumentNullException("reglementNo");
		}
		if (!(reglementClient.Numero == newNumero))
		{
			if (_reglementClientRepository.Get(societe.No, newNumero) != null)
			{
				throw new ApplicationException("Numéro règlement existe déjà.");
			}
			if (reglementClient.IsAnnule)
			{
				throw new ApplicationException("Le règlement est annulé!");
			}
			if (reglementClient.IsReglementAvoir)
			{
				throw new ApplicationException("Opération invalide. Règlement d'avoir.");
			}
			if (reglementClient.IsComptabilise == EtatComptabilite.Comptabilise)
			{
				throw new ApplicationException(TresorerieCoreMessages.ErrorReglementComptabilise);
			}
			if (reglementClient.IsImpaye == ImpayeEtat.Impaye)
			{
				throw new ApplicationException(TresorerieCoreMessages.ErrorReglementImpaye);
			}
			if (reglementClient.IsRemis != Remis.NonRemis)
			{
				throw new ApplicationException(TresorerieCoreMessages.ErrorReglementRemis);
			}
			if (reglementClient.GetHistoriques().ToList().Count() != 1)
			{
				throw new InvalidOperationException(TresorerieCoreMessages.ErrorrReglementTransfert);
			}
			if (reglementClient.GetAffectations().ToList().Any())
			{
				throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementAffecteEcheances);
			}
			if (reglementClient.GetRemplacements().ToList().Any())
			{
				throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementRemplace);
			}
			if (reglementClient.GetMesRemplacants().ToList().Any())
			{
				throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementRemplace);
			}
			if (reglementClient.IsPointe)
			{
				throw new ApplicationException(TresorerieCoreMessages.ErrorReglementRapproche);
			}
			if (reglementClient.Solde != reglementClient.Montant)
			{
				throw new ApplicationException(TresorerieCoreMessages.ErrorReglementAffecteEcheances);
			}
			if (reglementClient.IsRemplacer)
			{
				throw new ApplicationException("Le règlement est remplacé!");
			}
			reglementClient.ChangeNumero(newNumero);
			reglementClient.DateModification = DateTime.Now;
			reglementClient.ModificateurNo = utilisateur.No;
			_reglementClientRepository.Update(reglementClient);
			_notifyService.Notify(TypeEntity.Reglement, reglementNo, TypeAction.Modification, reglementClient.SocieteNo);
		}
	}

	public int ReglementCreate(string numero, DateTime date, decimal montant, int deviseNo, int clientNo, string clientCode, string clientIntitule, int modeNo, int caisseNo, string piece, string libelle, string tire, DateTime echeance, int? banqueNo, string banqueClient, decimal coursDevise, int deviseSocieteNo, string affaireNumero, string ribClient, string infoLibre1, string infoLibre2, string infoLibre3, string infoLibre4, string reference, int collaborateurNo, bool isCertifier, DateTime? dateValidite, decimal? montantPlafond, decimal baseRetenue, decimal tauxRetenue, ReglementNature reglementNature, bool soumisDroitTimbre, decimal montantDroitTimbre, bool isImporterFromErp = false, bool isImporterComptabiliser = false, bool useNotification = true)
	{
		Societe societe = SocieteManager.Societe;
		if (societe.DelaiPaiementClient)
		{
			int num = 0;
			if (societe.DelaiPaiementClient)
			{
				Tiers tiers = TiersViewGet(clientNo);
				if (tiers != null)
				{
					num = tiers.DelaiReg;
				}
				if ((echeance - date).TotalDays > (double)num)
				{
					throw new ApplicationException($"Dépassement dans le délai de paiement [{num}], la date d'échéance doivent être au plutard le [{date.AddDays(num).Date.ToShortDateString()}]!");
				}
			}
		}
		return ReglementCreateInterne(numero, date, montant, deviseNo, clientNo, clientCode, clientIntitule, TiersType.Client, modeNo, caisseNo, piece, libelle, tire, echeance, banqueNo, banqueClient, coursDevise, deviseSocieteNo, affaireNumero, ribClient, infoLibre1, infoLibre2, infoLibre3, infoLibre4, reference, collaborateurNo, isCertifier, dateValidite, montantPlafond, baseRetenue, tauxRetenue, reglementNature, soumisDroitTimbre, montantDroitTimbre, isImporterFromErp, isImporterComptabiliser, useNotification);
	}

	public int CalculerDelaiMoyenReg(IList<ReglementClient> reglements)
	{
		if ((reglements == null) | (reglements.Count == 0))
		{
			return 0;
		}
		decimal d = default(decimal);
		decimal num = reglements.Sum((ReglementClient r) => r.Montant);
		if (num == 0m)
		{
			return 0;
		}
		foreach (ReglementClient reglement in reglements)
		{
			if ((reglement.DateEcheance - reglement.Date).TotalDays > 0.0)
			{
				decimal num2 = (decimal)((reglement.DateEcheance - reglement.Date).TotalDays + 1.0) * reglement.Montant / num;
				d += num2;
			}
		}
		return (int)Math.Round(d, MidpointRounding.AwayFromZero);
	}

	public IList<int> ReglementCreate(IList<ReglementClient> reglements)
	{
		Societe societe = SocieteManager.Societe;
		if (societe.DelaiPaiementClient && reglements.Sum((ReglementClient r) => r.Montant) != 0m)
		{
			int num = CalculerDelaiMoyenReg(reglements);
			int num2 = 0;
			if (societe.DelaiPaiementClient)
			{
				int clientNo = reglements.FirstOrDefault().ClientNo;
				Tiers tiers = TiersViewGet(clientNo);
				if (tiers != null)
				{
					num2 = tiers.DelaiReg;
				}
				if (num > num2)
				{
					throw new ApplicationException($"Dépassement dans le délai de paiement [{num2}]!");
				}
			}
		}
		List<int> list = new List<int>();
		foreach (ReglementClient reglement in reglements)
		{
			string numeroPieceCourante = SocieteManager.GetNumeroPieceCourante(EntityNumerotation.ReglementClient);
			int item = ReglementCreateInterne(numeroPieceCourante, reglement.Date, reglement.Montant, reglement.DeviseNo, reglement.ClientNo, reglement.ClientCode, reglement.ClientIntitule, TiersType.Client, reglement.ModeReglementNo, reglement.CaisseNo, reglement.PieceNumero, reglement.Libelle, reglement.Tire, reglement.DateEcheance, reglement.BanqueNo, reglement.BanqueTier, reglement.DeviseCours, reglement.DeviseNo, reglement.AffaireNumero, reglement.RibClient, reglement.Info1, reglement.Info2, reglement.Info3, reglement.Info4, reglement.Reference, reglement.CollaborateurNo, reglement.IsCertifier, reglement.DateValiditer, reglement.MontantPlafond, reglement.BaseRetenue, reglement.TauxRetenue, ReglementNature.Reglement, reglement.SoumisDroitTimbre, reglement.MontantDroitTimbre);
			list.Add(item);
		}
		return list;
	}

	private int ReglementCreateInterne(string numero, DateTime date, decimal montant, int deviseNo, int clientNo, string clientCode, string clientIntitule, TiersType tiersType, int modeNo, int caisseNo, string piece, string libelle, string tire, DateTime echeance, int? banqueNo, string banqueClient, decimal coursDevise, int deviseSocieteNo, string affaireNumero, string ribClient, string infoLibre1, string infoLibre2, string infoLibre3, string infoLibre4, string reference, int collaborateurNo, bool isCertifier, DateTime? dateValidite, decimal? montantPlafond, decimal baseRetnue, decimal tauxRetenue, ReglementNature reglementNature, bool soumisDroitTimbre, decimal montantDroitTimbre, bool isImporterFromErp = false, bool isImporterComptabiliser = false, bool useNotification = true)
	{
		if (string.IsNullOrEmpty(numero))
		{
			throw new ArgumentNullException("numero", TresorerieCoreMessages.ErrorNumeroInvalide);
		}
		if (montant <= 0m)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorMontant, "montant");
		}
		if (clientNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorClientNo, "clientNo");
		}
		if (string.IsNullOrEmpty(clientCode))
		{
			throw new ArgumentNullException("clientCode");
		}
		if (string.IsNullOrEmpty(clientIntitule))
		{
			throw new ArgumentNullException("clientIntitule");
		}
		if (deviseNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorDeviseNo, "deviseNo");
		}
		if (modeNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorModeNo);
		}
		if (caisseNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorModeNo);
		}
		if (deviseSocieteNo < 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorDeviseNo);
		}
		if (soumisDroitTimbre && (montantDroitTimbre <= 0m || montantDroitTimbre > montant))
		{
			throw new ApplicationException("Montant droit de timbre invalide.");
		}
		if (soumisDroitTimbre && deviseNo != deviseSocieteNo)
		{
			throw new ApplicationException("Opération invalide! Droit de timbre en devise.");
		}
		Societe societe = SocieteManager.Societe;
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		Caisse caisse = societe.GetCaisse(caisseNo);
		if (caisse == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Impossible de créer le règlement! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisseNo, ProfilType.Grc))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (_reglementClientRepository.Get(caisse.SocieteNo, numero) != null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorNumeroReglement);
		}
		if (societe.IsAffaireClientRequired && string.IsNullOrEmpty(affaireNumero))
		{
			throw new ApplicationException(rcRessources.AffaireObligatoire);
		}
		if (!caisse.HasModeReglement(modeNo))
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorModeReglementInvalide);
		}
		CaisseModeReglement mode = caisse.GetMode(modeNo);
		if (mode == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorModeNo);
		}
		if (mode.EnSommeil)
		{
			throw new InvalidOperationException("Impossible de créer le règlement! Le mode est en sommeil!");
		}
		if ((mode.Type == ReglementType.Cheque || mode.Type == ReglementType.Traite || mode.Type == ReglementType.Virement) && string.IsNullOrEmpty(banqueClient))
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorBanqueClientInvalide);
		}
		if ((mode.Type == ReglementType.Cheque || mode.Type == ReglementType.Traite) && string.IsNullOrEmpty(piece))
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorNumeroPieceInvalide);
		}
		if (mode.Type == ReglementType.Virement && (!banqueNo.HasValue || banqueNo <= 0))
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorBanqueInvalide);
		}
		if (mode.Type == ReglementType.Virement)
		{
			InformationsBanque byBanqueId = SocieteManager.Groupe.InformationBanqueManager.GetByBanqueId(banqueNo.Value);
			if (byBanqueId != null && byBanqueId.EnSommeil)
			{
				throw new ApplicationException("Opération invalide. La banque est en sommeil");
			}
		}
		if (mode.Type == ReglementType.Cheque && societe.LegislationType == Legislation.Tunisie)
		{
			if (!montantPlafond.HasValue)
			{
				throw new ApplicationException("Le montant du plafond obligatoire.");
			}
			decimal? num = montantPlafond;
			if (((montant > num.GetValueOrDefault()) & num.HasValue) && !isCertifier)
			{
				throw new ApplicationException("Le montant ne doit pas dépasser le plafond.");
			}
			if (!dateValidite.HasValue)
			{
				throw new ApplicationException("La date de validité obligatoire.");
			}
			if (echeance.Date > dateValidite.Value.Date)
			{
				throw new ApplicationException("La date d'échéance ne doit pas dépasser la date de validité.");
			}
		}
		if (mode.IsModeAvoir)
		{
			throw new ApplicationException("Opération invalide. Mode avoir.");
		}
		if (mode.IsReferenceReglementClientObligatoire && string.IsNullOrEmpty(reference))
		{
			throw new ApplicationException("La référence règlement est obligatoire.");
		}
		if (mode.ControllerUniciteReferenceReglementClient && !ReglementClientIsReferenceUnique(mode.No, reference, 0))
		{
			throw new ApplicationException("La référence règlement existe déja.");
		}
		if (societe.RestrictionTypeReglementClient)
		{
			Tiers tiers = TiersViewGet(clientNo);
			if (tiers == null)
			{
				throw new ApplicationException($"Impossible de charger le client [{clientNo}] depuis vTiers!");
			}
			if (tiers.EnSommeil)
			{
				throw new ApplicationException($"Le client [{clientNo}] est en sommeil!");
			}
			if ((tiers.NiveauReg == NiveauModeReg.CHEQUE) & (mode.Type == ReglementType.Traite))
			{
				throw new ApplicationException("Le mode de paiement [traite] n'est pas autorisé pour le client [" + tiers.Intitule + "]!");
			}
			if ((tiers.NiveauReg == NiveauModeReg.ESPECE) & ((mode.Type == ReglementType.Traite) | (mode.Type == ReglementType.Cheque)))
			{
				throw new ApplicationException("Les modes de paiement [traite] et [chèque] ne sont pas autorisés pour le client [" + tiers.Intitule + "]!");
			}
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		SocieteDevise devise = societe.GetDevise(deviseSocieteNo);
		if (devise == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorDeviseNo);
		}
		coursDevise = ((deviseNo == deviseSocieteNo) ? 1m : coursDevise);
		decimal num2 = Math.Round(montant * coursDevise, devise.NombreDecimales, MidpointRounding.AwayFromZero);
		ReglementClient reglementClient = new ReglementClient(numero, clientNo, clientCode, clientIntitule, tiersType, caisse.No, caisse.SocieteNo, deviseNo, modeNo, mode.Type, date, montant, echeance, coursDevise, num2)
		{
			BanqueTier = banqueClient,
			PieceNumero = piece,
			BanqueNo = ((mode.Type == ReglementType.Virement) ? banqueNo : ((int?)null)),
			Solde = montant,
			SoldeToRemplace = montant,
			Libelle = libelle,
			Tire = tire,
			UtilisateurNo = utilisateur.No,
			StatutTransfert = StatutTransfert.None,
			CaisseOrigine = caisseNo,
			DateCreation = DateTime.Now,
			DateModification = DateTime.Now,
			ModificateurNo = utilisateur.No,
			SoldeDeviseSociete = num2,
			DateEcheance = echeance,
			AffaireNumero = affaireNumero,
			RibClient = ribClient,
			Info1 = infoLibre1,
			Info2 = infoLibre2,
			Info3 = infoLibre3,
			Info4 = infoLibre4,
			Reference = reference,
			IsReport = false,
			CollaborateurNo = collaborateurNo,
			IsCertifier = isCertifier,
			DateValiditer = dateValidite,
			MontantPlafond = montantPlafond,
			TauxRetenue = tauxRetenue,
			BaseRetenue = baseRetnue,
			ReglementNature = reglementNature,
			IsOrigineAvance = (reglementNature == ReglementNature.Avance),
			SoumisDroitTimbre = (societe.LegislationType == Legislation.Maroc && soumisDroitTimbre),
			MontantDroitTimbre = ((societe.LegislationType == Legislation.Maroc && soumisDroitTimbre) ? montantDroitTimbre : 0m),
			IsImporterComptabiliseErp = isImporterComptabiliser,
			IsImporterFromErp = isImporterFromErp
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		VerifierReglement(reglementClient, mode.Type);
		int? num3 = _reglementClientRepository.Create(reglementClient);
		if (!num3.HasValue || num3.Value == 0)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorCreationReglement);
		}
		HistoriqueMvt historiqueMvt = new HistoriqueMvt(num3.Value, caisseNo, SensMouvement.Entree, MouvementDomaine.ReglementClient, mode.No, num3.Value, 0, StatutTransfert.None, reglementClient.DeviseNo, new Lot(num3.Value, montant, montant));
		_historiqueRepository.Create(historiqueMvt);
		if (useNotification)
		{
			_notifyService.Notify(TypeEntity.Reglement, num3.Value, TypeAction.Ajout, caisse.SocieteNo);
		}
		transactionScope.Complete();
		return num3.Value;
	}

	public int ReglementAutreCreate(string numero, DateTime date, decimal montant, int deviseNo, int clientNo, string clientCode, string clientIntitule, TiersType typeTiers, int modeNo, int caisseNo, string piece, string libelle, string tire, DateTime echeance, int? banqueNo, string banqueClient, decimal coursDevise, int deviseSocieteNo, string affaireNumero, string ribClient, string infoLibre1, string infoLibre2, string infoLibre3, string infoLibre4, bool isCertifier, DateTime? dateValidite, decimal? montantPlafond)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		return ReglementAutreCreate0(numero, date, montant, deviseNo, clientNo, clientCode, clientIntitule, typeTiers, modeNo, caisseNo, piece, libelle, tire, echeance, banqueNo, banqueClient, coursDevise, deviseSocieteNo, affaireNumero, ribClient, infoLibre1, infoLibre2, infoLibre3, infoLibre4, isCertifier, dateValidite, montantPlafond);
	}

	private int ReglementAutreCreate0(string numero, DateTime date, decimal montant, int deviseNo, int clientNo, string clientCode, string clientIntitule, TiersType tiersType, int modeNo, int caisseNo, string piece, string libelle, string tire, DateTime echeance, int? banqueNo, string banqueClient, decimal coursDevise, int deviseSocieteNo, string affaireNumero, string ribClient, string infoLibre1, string infoLibre2, string infoLibre3, string infoLibre4, bool isCertifier, DateTime? dateValidite, decimal? montantPlafond)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		if (string.IsNullOrEmpty(numero))
		{
			throw new ArgumentNullException("numero", TresorerieCoreMessages.ErrorNumeroInvalide);
		}
		if (montant <= 0m)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorMontant, "montant");
		}
		if (clientNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorClientNo, "clientNo");
		}
		if (deviseNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorDeviseNo, "deviseNo");
		}
		if (modeNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorModeNo);
		}
		if (caisseNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorModeNo);
		}
		if (deviseSocieteNo < 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorDeviseNo);
		}
		Societe societe = SocieteManager.Societe;
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		Caisse caisse = societe.GetCaisse(caisseNo);
		if (caisse == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Impossible de créer le règlement! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisseNo, ProfilType.Grc))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (_reglementClientRepository.Get(caisse.SocieteNo, numero) != null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorNumeroReglement);
		}
		if (!caisse.HasModeReglement(modeNo))
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorModeReglementInvalide);
		}
		CaisseModeReglement mode = caisse.GetMode(modeNo);
		if (mode == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorModeNo);
		}
		if (mode.EnSommeil)
		{
			throw new InvalidOperationException("Impossible de créer le règlement! Le mode est en sommeil!");
		}
		if ((mode.Type == ReglementType.Cheque || mode.Type == ReglementType.Traite || mode.Type == ReglementType.Virement) && string.IsNullOrEmpty(banqueClient))
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorBanqueClientInvalide);
		}
		if ((mode.Type == ReglementType.Cheque || mode.Type == ReglementType.Traite || mode.Type == ReglementType.Virement) && string.IsNullOrEmpty(piece))
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorNumeroPieceInvalide);
		}
		if (mode.Type == ReglementType.Virement && (!banqueNo.HasValue || banqueNo <= 0))
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorBanqueInvalide);
		}
		if (mode.Type == ReglementType.Virement && banqueNo.HasValue && banqueNo > 0)
		{
			InformationsBanque byBanqueId = SocieteManager.Groupe.InformationBanqueManager.GetByBanqueId(banqueNo.Value);
			if (byBanqueId != null && byBanqueId.EnSommeil)
			{
				throw new ApplicationException("Impossible de créer le règlement! La banque en sommeil.");
			}
		}
		if (mode.IsModeAvoir)
		{
			throw new ApplicationException("Opération invalide. Mode avoir.");
		}
		if (societe.RestrictionTypeReglementClient)
		{
			Tiers tiers = TiersViewGet(clientNo);
			if (tiers == null)
			{
				throw new ApplicationException($"Impossible de charger le client [{clientNo}] depuis vTiers!");
			}
			if ((tiers.NiveauReg == NiveauModeReg.CHEQUE) & (mode.Type == ReglementType.Traite))
			{
				throw new ApplicationException("Le mode de paiement [traite] n'est pas autorisé pour le client [" + tiers.Intitule + "]!");
			}
			if ((tiers.NiveauReg == NiveauModeReg.ESPECE) & ((mode.Type == ReglementType.Traite) | (mode.Type == ReglementType.Cheque)))
			{
				throw new ApplicationException("Les modes de paiement [traite] et [chèque] ne sont pas autorisés pour le client [" + tiers.Intitule + "]!");
			}
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		SocieteDevise devise = societe.GetDevise(deviseSocieteNo);
		if (devise == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorDeviseNo);
		}
		coursDevise = ((deviseNo == deviseSocieteNo) ? 1m : coursDevise);
		decimal montantDevise = Math.Round(montant * coursDevise, devise.NombreDecimales, MidpointRounding.AwayFromZero);
		ReglementClient reglementClient = new ReglementClient(numero, clientNo, clientCode, clientIntitule, tiersType, caisse.No, caisse.SocieteNo, deviseNo, modeNo, mode.Type, date, montant, echeance, coursDevise, montantDevise)
		{
			BanqueTier = banqueClient,
			PieceNumero = piece,
			BanqueNo = ((mode.Type == ReglementType.Virement) ? banqueNo : ((int?)null)),
			Solde = 0m,
			SoldeToRemplace = montant,
			Libelle = libelle,
			Tire = tire,
			UtilisateurNo = utilisateur.No,
			StatutTransfert = StatutTransfert.None,
			CaisseOrigine = caisseNo,
			DateCreation = DateTime.Now,
			DateModification = DateTime.Now,
			ModificateurNo = utilisateur.No,
			SoldeDeviseSociete = 0m,
			DateEcheance = echeance,
			AffaireNumero = affaireNumero,
			RibClient = ribClient,
			Info1 = infoLibre1,
			Info2 = infoLibre2,
			Info3 = infoLibre3,
			Info4 = infoLibre4,
			IsReport = false,
			IsCertifier = isCertifier,
			MontantPlafond = montantPlafond,
			DateValiditer = dateValidite
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		VerifierReglement(reglementClient, mode.Type);
		int? num = _reglementClientRepository.Create(reglementClient);
		if (!num.HasValue || num.Value == 0)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorCreationReglement);
		}
		HistoriqueMvt historiqueMvt = new HistoriqueMvt(num.Value, caisseNo, SensMouvement.Entree, MouvementDomaine.ReglementClient, mode.No, num.Value, 0, StatutTransfert.None, reglementClient.DeviseNo, new Lot(num.Value, montant, montant));
		_historiqueRepository.Create(historiqueMvt);
		_notifyService.Notify(TypeEntity.Reglement, num.Value, TypeAction.Ajout, caisse.SocieteNo);
		transactionScope.Complete();
		return num.Value;
	}

	public int ReglementCreate_(Caisse caisse, ModeReglement mode, string numero, DateTime date, decimal montant, int deviseNo, int clientNo, string clientCode, string clientIntitule, string piece, string libelle, string tire, DateTime echeance, int? banqueNo, string banqueClient, decimal coursDevise, int deviseSocieteNo, string affaireNumero, string ribClient, string infoLibre1, string infoLibre2, string infoLibre3, string infoLibre4)
	{
		if (string.IsNullOrEmpty(numero))
		{
			throw new ArgumentNullException("numero", TresorerieCoreMessages.ErrorNumeroInvalide);
		}
		if (montant <= 0m)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorMontant, "montant");
		}
		if (clientNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorClientNo, "clientNo");
		}
		if (deviseNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorDeviseNo, "deviseNo");
		}
		if (deviseSocieteNo < 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorDeviseNo);
		}
		Societe societe = SocieteManager.Societe;
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		if ((mode.Type == ReglementType.Cheque || mode.Type == ReglementType.Traite || mode.Type == ReglementType.Virement) && string.IsNullOrEmpty(banqueClient))
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorBanqueClientInvalide);
		}
		if ((mode.Type == ReglementType.Cheque || mode.Type == ReglementType.Traite || mode.Type == ReglementType.Virement) && string.IsNullOrEmpty(piece))
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorNumeroPieceInvalide);
		}
		if (mode.Type == ReglementType.Virement && (!banqueNo.HasValue || banqueNo <= 0))
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorBanqueInvalide);
		}
		if (mode.IsModeAvoir)
		{
			throw new ApplicationException("Opération invalide. Mode avoir.");
		}
		SocieteDevise devise = societe.GetDevise(deviseSocieteNo);
		if (devise == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorDeviseNo);
		}
		coursDevise = ((deviseNo == deviseSocieteNo) ? 1m : coursDevise);
		decimal num = Math.Round(montant * coursDevise, devise.NombreDecimales, MidpointRounding.AwayFromZero);
		ReglementClient reglementClient = new ReglementClient(numero, clientNo, clientCode, clientIntitule, TiersType.Client, caisse.No, caisse.SocieteNo, deviseNo, mode.No, mode.Type, date, montant, echeance, coursDevise, num)
		{
			BanqueTier = banqueClient,
			PieceNumero = piece,
			BanqueNo = ((mode.Type == ReglementType.Virement) ? banqueNo : ((int?)null)),
			Solde = montant,
			SoldeToRemplace = montant,
			Libelle = libelle,
			Tire = tire,
			UtilisateurNo = utilisateur.No,
			StatutTransfert = StatutTransfert.None,
			CaisseOrigine = caisse.No,
			DateCreation = DateTime.Now,
			DateModification = DateTime.Now,
			ModificateurNo = utilisateur.No,
			SoldeDeviseSociete = num,
			DateEcheance = echeance,
			AffaireNumero = affaireNumero,
			RibClient = ribClient,
			Info1 = infoLibre1,
			Info2 = infoLibre2,
			Info3 = infoLibre3,
			Info4 = infoLibre4
		};
		int? num2 = _reglementClientRepository.Create(reglementClient);
		if (!num2.HasValue || num2.Value == 0)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorCreationReglement);
		}
		HistoriqueMvt historiqueMvt = new HistoriqueMvt(num2.Value, caisse.No, SensMouvement.Entree, MouvementDomaine.ReglementClient, mode.No, num2.Value, 0, StatutTransfert.None, reglementClient.DeviseNo, new Lot(num2.Value, montant, montant));
		_historiqueRepository.Create(historiqueMvt);
		return num2.Value;
	}

	public void ReglementDeremettre(int no)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		ReglementClient reglementClient = ReglementGet(no);
		if (reglementClient == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementNo);
		}
		Bordereau bordereau = BordereauGet(reglementClient.EnteteBordereauNo.Value);
		if (bordereau == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorBordereauNo);
		}
		Caisse caisse = Get(reglementClient.CaisseNo);
		if (caisse == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Opération invalide! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grc))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (reglementClient.IsAnnule)
		{
			throw new ApplicationException("Le règlement est annulé!");
		}
		reglementClient.ChangeToNonImpaye();
		reglementClient.DateModification = DateTime.Now;
		reglementClient.ModificateurNo = utilisateur.No;
		reglementClient.IsPointe = bordereau.IsPointe;
		_reglementClientRepository.Update(reglementClient);
	}

	public void ReglementClientChangeCours(int reglementNo, int deviseSocieteNo, decimal newCours)
	{
		DeviseManager deviseManager = SocieteManager.Groupe.DeviseManager;
		if (newCours <= 0m)
		{
			throw new ApplicationException("Le nouveau cours est invalide.");
		}
		ReglementClient reglementClient = _reglementClientRepository.Get(reglementNo);
		if (reglementClient == null)
		{
			throw new ArgumentNullException("reglementNo");
		}
		if (reglementClient.IsAffDevise && reglementClient.DeviseOrigineNo == deviseSocieteNo)
		{
			throw new ApplicationException("Opération invalide! Affectation en devise et le règlement " + reglementClient.Numero + " est en devise société.");
		}
		if (reglementClient.DeviseNo == deviseSocieteNo)
		{
			throw new ApplicationException("Opération invalide! Le règlement " + reglementClient.Numero + " est en devise société.");
		}
		if (reglementClient.DeviseCours == newCours)
		{
			throw new ApplicationException("Opération invalide.");
		}
		Caisse caisse = Get(reglementClient.CaisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grc))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (reglementClient.IsAnnule)
		{
			throw new ApplicationException("Le règlement est annulé!");
		}
		if (reglementClient.IsReglementAvoir)
		{
			throw new ApplicationException("Opération invalide. Règlement d'avoir.");
		}
		if (reglementClient.GetRemplacements().ToList().Any())
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementRemplace);
		}
		if (reglementClient.IsComptabilise != EtatComptabilite.NonComptabilise)
		{
			throw new ApplicationException("Le règlement " + reglementClient.Numero + " est comptabilisé.");
		}
		if (reglementClient.IsPointe)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglementRapproche);
		}
		if (reglementClient.IsRemis == Remis.RemisBanque)
		{
			throw new ApplicationException("Le règlement " + reglementClient.Numero + " est remis en banque.");
		}
		Devise devise = deviseManager.Get(reglementClient.DeviseNo);
		if (devise == null)
		{
			throw new InvalidOperationException(rcRessources.DeviseNotFound);
		}
		Societe societe = SocieteManager.Societe;
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		List<Affectation> list = (from x in reglementClient.GetAffectations()
			where !x.EcartEcheanceNo.HasValue
			select x).ToList();
		foreach (Affectation item in list.OrderByDescending((Affectation x) => x.No).ToList())
		{
			AffectationDelete(item.ReglementNo, item.No, deviseSocieteNo, isDossierImpaye: false);
		}
		ReglementClient reglementClient2 = _reglementClientRepository.Get(reglementNo);
		if (reglementClient2 == null)
		{
			throw new ArgumentNullException("Impossible de charger le règlement " + reglementClient.Numero + ".");
		}
		if (reglementClient2.GetAffectations().Any())
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementAffecteEcheances);
		}
		reglementClient2.DateModification = DateTime.Now;
		reglementClient2.ModificateurNo = utilisateur.No;
		reglementClient2.ChangeCours(newCours, devise.NombreDecimales);
		_reglementClientRepository.Update(reglementClient2);
		foreach (Affectation item2 in list)
		{
			Imputer(item2.ReglementNo, item2.EcheanceNo, item2.Montant, deviseSocieteNo, isDossierImpaye: false);
		}
		if (reglementClient.IsRemis == Remis.RemisBordereau && reglementClient.EnteteBordereauNo.HasValue)
		{
			Bordereau bordereau = _bordereauRepository.Get(reglementClient.EnteteBordereauNo.Value);
			if (bordereau == null)
			{
				throw new ApplicationException("Impossible de charger le bordereau " + reglementClient.EnteteBordereauNumero + ".");
			}
			bordereau.MontantDeviseSociete = bordereau.MontantDeviseSociete - reglementClient.MontantDeviseSociete + reglementClient2.MontantDeviseSociete;
			_bordereauRepository.Update(bordereau);
			_notifyService.Notify(TypeEntity.Bordereau, bordereau.No, TypeAction.Modification, societe.No);
		}
		_notifyService.Notify(TypeEntity.Reglement, reglementNo, TypeAction.Modification, societe.No);
		transactionScope.Complete();
	}

	public void ReglementFournisseurAnnulerImpaye(int reglementNo)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		ReglementFournisseur reglementFournisseur = _reglementFournisseurRepository.Get(reglementNo);
		if (reglementFournisseur == null)
		{
			throw new ApplicationException("Impossible de charger le règlement.");
		}
		if (reglementFournisseur.IsImpaye != ImpayeEtat.Impaye)
		{
			throw new ApplicationException("Le règlement n'est pas un impayé.");
		}
		if (reglementFournisseur.Type != ReglementType.Cheque && reglementFournisseur.Type != ReglementType.Traite)
		{
			throw new ApplicationException("Le règlement n'est pas de type pièce.");
		}
		if (reglementFournisseur.IsComptabiliseImpaye)
		{
			throw new ApplicationException("L'impayé est compatabilisé.");
		}
		if (reglementFournisseur.IsReserveDossierFrs)
		{
			throw new ApplicationException("L'impayé est réservé dans un dossier de règlement.");
		}
		Caisse caisse = Get(reglementFournisseur.CaisseNo);
		if (caisse == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Opération invalide! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grf))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		reglementFournisseur.DateModification = DateTime.Now;
		reglementFournisseur.ModificateurNo = utilisateur.No;
		reglementFournisseur.ChangeEtatImpaye(ImpayeEtat.NonImpaye, DateHelper.GetMinSqlDateTime());
		ImpayeFournisseur impayeFournisseur = _impayeFournisseurRepository.Get(reglementNo);
		if (impayeFournisseur == null)
		{
			throw new ApplicationException("Impossible de charger l'impayé [" + reglementFournisseur.Numero + "].");
		}
		if (impayeFournisseur.EcheanceNo <= 0)
		{
			throw new ArgumentException("Impossible de charger l'échéance de l'impayé [" + reglementFournisseur.Numero + "].");
		}
		Echeance echeance = _echeanceRepository.Get(impayeFournisseur.EcheanceNo);
		if (echeance == null)
		{
			throw new ArgumentException("Impossible de charger l'échéance de l'impayé [" + reglementFournisseur.Numero + "].");
		}
		if (!SocieteManager.UserHasAutorisationSouche(echeance.Domaine, echeance.SoucheNo))
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorAutorisationSouche);
		}
		if (echeance.Type != EcheanceType.ImpayeFournisseur)
		{
			throw new InvalidOperationException("L'échéance de l'impayé [" + reglementFournisseur.Numero + "] est de type invalide [" + echeance.Type.GetDisplayDescription() + "].");
		}
		if (echeance.IsReserveDossierFrs)
		{
			throw new ApplicationException("L'échéance de l'impayé [" + reglementFournisseur.Numero + "] est réservée pour un dossier de règlement fournisseur.");
		}
		if (SocieteManager.IsEcheanceUtiliseDossierFrs(echeance.No))
		{
			throw new ApplicationException("L'échéance de l'impayé [" + reglementFournisseur.Numero + "] est utilisée dans un dossier de règlement fournisseur");
		}
		if (echeance.Solde != echeance.Montant || echeance.SoldeDeviseSociete != echeance.MontantDeviseSociete)
		{
			throw new ApplicationException("L'échéance de l'impayé [" + reglementFournisseur.Numero + "] est partiellement soldée.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_echeanceRepository.Delete(echeance);
		_reglementFournisseurRepository.UpdateEtatImpaye(reglementFournisseur);
		_notifyService.Notify(TypeEntity.Echeance, impayeFournisseur.EcheanceNo, TypeAction.Suppression, echeance.SocieteNo);
		_notifyService.Notify(TypeEntity.ReglementFournisseur, reglementFournisseur.No, TypeAction.Modification, reglementFournisseur.SocieteNo);
		_notifyService.Notify(TypeEntity.ImpayeFournisseur, reglementFournisseur.No, TypeAction.Suppression, reglementFournisseur.SocieteNo);
		transactionScope.Complete();
	}

	public void ReglementFournisseurAnnulerImpayeCompta(int reglementNo)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		ReglementFournisseur reglementFournisseur = _reglementFournisseurRepository.Get(reglementNo);
		if (reglementFournisseur == null)
		{
			throw new ApplicationException("Impossible de charger le règlement.");
		}
		if (reglementFournisseur.IsImpaye != ImpayeEtat.Impaye)
		{
			throw new ApplicationException("Le règlement n'est pas un impayé.");
		}
		if (reglementFournisseur.Type != ReglementType.Cheque && reglementFournisseur.Type != ReglementType.Traite)
		{
			throw new ApplicationException("Le règlement n'est pas de type pièce.");
		}
		if (reglementFournisseur.IsComptabilise != EtatComptabilite.Comptabilise)
		{
			throw new ApplicationException("Le règlement doit être comptabilisé.");
		}
		if (reglementFournisseur.IsComptabiliseImpaye)
		{
			throw new ApplicationException("L'impayé est compatabilisé.");
		}
		reglementFournisseur.DateModification = DateTime.Now;
		reglementFournisseur.ModificateurNo = utilisateur.No;
		reglementFournisseur.ChangeEtatImpaye(ImpayeEtat.NonImpaye, DateHelper.GetMinSqlDateTime());
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_reglementFournisseurRepository.UpdateEtatImpaye(reglementFournisseur);
		_notifyService.Notify(TypeEntity.ReglementFournisseur, reglementFournisseur.No, TypeAction.Modification, reglementFournisseur.SocieteNo);
		_notifyService.Notify(TypeEntity.ImpayeFournisseur, reglementFournisseur.No, TypeAction.Suppression, reglementFournisseur.SocieteNo);
		transactionScope.Complete();
	}

	public void ReglementFournisseurAnnuler(int reglementNo)
	{
		ReglementFournisseur reglementFournisseur = _reglementFournisseurRepository.Get(reglementNo);
		if (reglementFournisseur == null)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorReglement);
		}
		if ((Get(reglementFournisseur.CaisseNo) ?? throw new ArgumentNullException(TresorerieCoreMessages.ErrorCaisseNo)).EnSommeil)
		{
			throw new ApplicationException("Opération invalide! La caisse est en sommeil!");
		}
		if (reglementFournisseur.IsPointe)
		{
			throw new ApplicationException("Le règlement [" + reglementFournisseur.Numero + "] est rapproché.");
		}
		if (reglementFournisseur.IsAnnule)
		{
			throw new ApplicationException("Le règlement [" + reglementFournisseur.Numero + "] est annulé.");
		}
		if (reglementFournisseur.IsImpaye != ImpayeEtat.NonImpaye)
		{
			throw new ApplicationException("Le règlement [" + reglementFournisseur.Numero + "] est un impayé.");
		}
		if (reglementFournisseur.IsReserveDossierFrs)
		{
			throw new ApplicationException("Le règlement est reservé pour un dossier de règlement fournisseur.");
		}
		if (ReglementFournisseurUsedInDossier(reglementFournisseur.No))
		{
			throw new ApplicationException("Le règlement est utilisé dans un dossier de règlement fournisseur");
		}
		if (reglementFournisseur.GetAffectations().Any())
		{
			throw new ApplicationException("Le règlement a subit une affectation.");
		}
		if (reglementFournisseur.DossierNo.HasValue)
		{
			throw new ApplicationException("Le règlement [" + reglementFournisseur.Numero + "] est associé à un dossier de règlement fournisseur.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		if (reglementFournisseur.Type == ReglementType.Espece)
		{
			AnnulerMouvementSortieReglementFrs(reglementFournisseur);
		}
		_reglementFournisseurRepository.AnnulerReglement(reglementNo, isAnnule: true);
		_notifyService.Notify(TypeEntity.ReglementFournisseur, reglementNo, TypeAction.Modification, reglementFournisseur.SocieteNo);
		transactionScope.Complete();
	}

	public void ReglementFournisseurDelete(int reglementNo, bool isDossierCommercial)
	{
		ReglementFournisseur reglementFournisseur = _reglementFournisseurRepository.Get(reglementNo);
		if (reglementFournisseur == null)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorReglement);
		}
		Caisse caisse = Get(reglementFournisseur.CaisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Opération invalide! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (reglementFournisseur.IsComptabilise != EtatComptabilite.NonComptabilise)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglementComptabilise);
		}
		if (reglementFournisseur.IsPointe)
		{
			throw new ApplicationException("Le règlement [" + reglementFournisseur.Numero + "] est rapproché.");
		}
		if (reglementFournisseur.IsAnnule)
		{
			throw new ApplicationException("Le règlement [" + reglementFournisseur.Numero + "] est annulé.");
		}
		if (reglementFournisseur.IsImpaye != ImpayeEtat.NonImpaye)
		{
			throw new ApplicationException("Le règlement [" + reglementFournisseur.Numero + "] est un impayé.");
		}
		if (!string.IsNullOrEmpty(reglementFournisseur.EnteteBordereauNumero))
		{
			throw new ApplicationException("Le règlement [" + reglementFournisseur.Numero + "] est utilisé dans un bordereau.");
		}
		if (reglementFournisseur.Recuperer != RemisFournisseur.NonRecuperer)
		{
			throw new ApplicationException("Le règlement [" + reglementFournisseur.Numero + "] est récupéré.");
		}
		if (isDossierCommercial)
		{
			if (reglementFournisseur.IsReserveDossierFrs)
			{
				throw new ApplicationException("Le règlement [" + reglementFournisseur.Numero + "] est reservé pour un dossier de règlement fournisseur.");
			}
			if (ReglementFournisseurUsedInDossier(reglementFournisseur.No))
			{
				throw new ApplicationException("Le règlement [" + reglementFournisseur.Numero + "] est utilisé dans un dossier de règlement fournisseur");
			}
			if (reglementFournisseur.GetAffectations().Any())
			{
				throw new ApplicationException("Le règlement [" + reglementFournisseur.Numero + "] a subit une affectation.");
			}
			if (reglementFournisseur.DossierNo.HasValue)
			{
				throw new ApplicationException("Le règlement [" + reglementFournisseur.Numero + "] est associé à un dossier de règlement fournisseur.");
			}
			Caution byReglementNo = _cautionRepository.GetByReglementNo(reglementNo);
			if (byReglementNo != null)
			{
				throw new ApplicationException("Le règlement [" + reglementFournisseur.Numero + "] est associé au caution [" + byReglementNo.Numero + "]!");
			}
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		if (reglementFournisseur.Type == ReglementType.Espece)
		{
			AnnulerMouvementSortieReglementFrs(reglementFournisseur);
		}
		_reglementFournisseurRepository.Delete(reglementFournisseur);
		_notifyService.Notify(TypeEntity.ReglementFournisseur, reglementNo, TypeAction.Suppression, reglementFournisseur.SocieteNo);
		transactionScope.Complete();
	}

	public ReglementFournisseur ReglementFournisseurGet(int no)
	{
		return _reglementFournisseurRepository.Get(no);
	}

	public ReglementFournisseur ReglementFournisseurGet(string numero, Societe societe = null)
	{
		societe = societe ?? SocieteManager.Societe;
		return _reglementFournisseurRepository.Get(societe.No, numero);
	}

	public IEnumerable<ReglementFournisseur> ReglementFournisseurGetAll(int tiersNo, Societe societe = null)
	{
		societe = societe ?? SocieteManager.Societe;
		if (tiersNo <= 0)
		{
			throw new ArgumentNullException("tiersNo");
		}
		return _reglementFournisseurRepository.GetReglementFournisseurs(societe.No, tiersNo);
	}

	public IEnumerable<ReglementFournisseur> ReglementFournisseurGetAllByDateEcheance(DateTime echeanceDebut, DateTime echeanceFin)
	{
		Societe societe = SocieteManager.Societe;
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		int[] caissesNo = (utilisateur.IsAdmin ? (from x in societe.GetCaisses()
			select x.No).ToArray() : (from a in _autorisationCaisseRepository.GetAll(utilisateur.No, societe.No, ProfilType.Grf)
			select a.CaisseNo).ToArray());
		return _reglementFournisseurRepository.GetAllByDateEcheance(societe.No, echeanceDebut, echeanceFin, caissesNo);
	}

	public IEnumerable<ReglementFournisseur> ReglementFournisseurGetAll(Societe societe = null)
	{
		societe = societe ?? SocieteManager.Societe;
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		if (utilisateur.IsAdmin)
		{
			return from x in _reglementFournisseurRepository.GetAll(societe.No)
				orderby x.Date descending
				select x;
		}
		int[] caisses = (from a in _autorisationCaisseRepository.GetAll(utilisateur.No, societe.No, ProfilType.Grf)
			select a.CaisseNo).ToArray();
		return (from x in _reglementFournisseurRepository.GetAll(societe.No)
			where caisses.Any((int y) => y == x.CaisseNo)
			orderby x.Date descending
			select x).ToList();
	}

	public IEnumerable<ReglementFournisseur> ReglementFournisseurGetAll(DateTime dateDebut, DateTime dateFin, Societe societe = null)
	{
		societe = societe ?? SocieteManager.Societe;
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		if (utilisateur.IsAdmin)
		{
			return from x in _reglementFournisseurRepository.GetAll(societe.No, dateDebut, dateFin)
				orderby x.Date descending
				select x;
		}
		int[] caisses = (from a in _autorisationCaisseRepository.GetAll(utilisateur.No, societe.No, ProfilType.Grf)
			select a.CaisseNo).ToArray();
		return (from x in _reglementFournisseurRepository.GetAll(societe.No, dateDebut, dateFin)
			where caisses.Any((int y) => y == x.CaisseNo)
			orderby x.Date descending
			select x).ToList();
	}

	public IEnumerable<ReglementFournisseur> ReglementFournisseurGetAll(int societeNo, EtatComptabilite etat)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		if (utilisateur.IsAdmin)
		{
			return from x in _reglementFournisseurRepository.GetAll(societeNo, etat)
				orderby x.Date descending
				select x;
		}
		int[] caisses = (from a in _autorisationCaisseRepository.GetAll(utilisateur.No, societeNo, ProfilType.Grf)
			select a.CaisseNo).ToArray();
		return (from x in _reglementFournisseurRepository.GetAll(societeNo, etat)
			where caisses.Any((int y) => y == x.CaisseNo)
			orderby x.Date descending
			select x).ToList();
	}

	public IEnumerable<ReglementFournisseur> ReglementFournisseurSynchronisationGetAll(int societeNo, DateTime dateDebut, DateTime dateFin, bool isSynchroniser)
	{
		return _reglementFournisseurRepository.GetAllNonSynchroErp(societeNo, dateDebut, dateFin, isSynchroniser);
	}

	public IEnumerable<ReglementFournisseur> ReglementFournisseurGetAllToDeclarationTvaEncaissement(int societeNo, DateTime dateDebutExercice, DateTime dateFin)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		if (utilisateur.IsAdmin)
		{
			return from x in _reglementFournisseurRepository.GetAllToDeclarationTvaEncaissement(societeNo, dateDebutExercice, dateFin)
				orderby x.Date descending
				select x;
		}
		int[] caisses = (from a in _autorisationCaisseRepository.GetAll(utilisateur.No, societeNo, ProfilType.Grf)
			select a.CaisseNo).ToArray();
		return (from x in _reglementFournisseurRepository.GetAllToDeclarationTvaEncaissement(societeNo, dateDebutExercice, dateFin)
			where caisses.Any((int y) => y == x.CaisseNo)
			orderby x.Date descending
			select x).ToList();
	}

	public IEnumerable<ReglementFournisseur> ReglementFournisseurGetAllPieceNonEchu(Societe societe = null)
	{
		societe = societe ?? SocieteManager.Societe;
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		int[] caissesNo = (utilisateur.IsAdmin ? (from x in societe.GetCaisses()
			select x.No).ToArray() : (from a in _autorisationCaisseRepository.GetAll(utilisateur.No, societe.No, ProfilType.Grf)
			select a.CaisseNo).ToArray());
		return _reglementFournisseurRepository.GetAllPieceNonEchu(societe.No, caissesNo);
	}

	public IEnumerable<ReglementFournisseur> ReglementFournisseurGetAllPieceNonEchuByFournisseur(int fournisseurNo, Societe societe = null)
	{
		societe = societe ?? SocieteManager.Societe;
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		int[] caissesNo = (utilisateur.IsAdmin ? (from x in societe.GetCaisses()
			select x.No).ToArray() : (from a in _autorisationCaisseRepository.GetAll(utilisateur.No, societe.No, ProfilType.Grf)
			select a.CaisseNo).ToArray());
		return _reglementFournisseurRepository.GetAllPieceNonEchuByFournisseur(societe.No, fournisseurNo, caissesNo);
	}

	public void ReglementFournisseurRecupererPiece(int reglementNo, DateTime dateRecuperation, string cinRecouvreur, string nomRecouvreur)
	{
		Societe societe = SocieteManager.Societe;
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		ReglementFournisseur reglementFournisseur = ReglementFournisseurGet(reglementNo);
		if (reglementFournisseur == null)
		{
			throw new ApplicationException("Impossible de charger le règlement.");
		}
		CaisseModeReglement mode = (societe.GetCaisse(reglementFournisseur.CaisseNo) ?? throw new ApplicationException("Impossible de charger la caisse.")).GetMode(reglementFournisseur.ModeReglementNo);
		if (mode == null)
		{
			throw new ApplicationException("Impossible de charger le mode de règlement.");
		}
		if (mode.Type != ReglementType.Cheque && mode.Type != ReglementType.Traite && !mode.IsRetenu)
		{
			throw new ApplicationException("Le règlement n'est pas de type pièce.");
		}
		if (reglementFournisseur.Recuperer != RemisFournisseur.NonRecuperer)
		{
			throw new ApplicationException("Le règlement est récuperé.");
		}
		if (reglementFournisseur.IsAnnule)
		{
			throw new ApplicationException("Le règlement [" + reglementFournisseur.Numero + "] est annulé.");
		}
		if (string.IsNullOrEmpty(cinRecouvreur))
		{
			throw new ApplicationException("Vérifier le cin du récouvreur.");
		}
		if (string.IsNullOrEmpty(nomRecouvreur))
		{
			throw new ApplicationException("Vérifier le nom du recouvreur.");
		}
		reglementFournisseur.Recuperer = RemisFournisseur.Recuperer;
		reglementFournisseur.RecouvreurNom = nomRecouvreur;
		reglementFournisseur.RecouvreurCin = cinRecouvreur;
		reglementFournisseur.DateRecuperation = dateRecuperation;
		_reglementFournisseurRepository.UpdateRecuperation(reglementFournisseur.No, reglementFournisseur.Recuperer, reglementFournisseur.DateRecuperation, reglementFournisseur.RecouvreurCin, reglementFournisseur.RecouvreurNom, utilisateur.No);
		_notifyService.Notify(TypeEntity.ReglementFournisseur, reglementFournisseur.No, TypeAction.Modification, reglementFournisseur.SocieteNo);
	}

	public void ReglementFournisseursSignePiece(int reglementNo)
	{
		Societe societe = SocieteManager.Societe;
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		ReglementFournisseur reglementFournisseur = ReglementFournisseurGet(reglementNo);
		if (reglementFournisseur == null)
		{
			throw new ApplicationException("Impossible de charger le règlement.");
		}
		CaisseModeReglement mode = (societe.GetCaisse(reglementFournisseur.CaisseNo) ?? throw new ApplicationException("Impossible de charger la caisse.")).GetMode(reglementFournisseur.ModeReglementNo);
		if (mode == null)
		{
			throw new ApplicationException("Impossible de charger le mode de règlement.");
		}
		if (mode.Type != ReglementType.Cheque && mode.Type != ReglementType.Traite)
		{
			throw new ApplicationException("Le règlement n'est pas de type pièce.");
		}
		if (reglementFournisseur.Recuperer != RemisFournisseur.NonRecuperer)
		{
			throw new ApplicationException("Le règlement est récuperé.");
		}
		if (reglementFournisseur.IsAnnule)
		{
			throw new ApplicationException("Le règlement [" + reglementFournisseur.Numero + "] est annulé.");
		}
		reglementFournisseur.ChequeSigne = true;
		_reglementFournisseurRepository.UpdateSignature(reglementFournisseur.No, pieceSigne: true, utilisateur.No);
		_notifyService.Notify(TypeEntity.ReglementFournisseur, reglementFournisseur.No, TypeAction.Modification, reglementFournisseur.SocieteNo);
	}

	public void ReglementFournisseursAnnulerSignePiece(int reglementNo)
	{
		Societe societe = SocieteManager.Societe;
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		ReglementFournisseur reglementFournisseur = ReglementFournisseurGet(reglementNo);
		if (reglementFournisseur == null)
		{
			throw new ApplicationException("Impossible de charger le règlement.");
		}
		CaisseModeReglement mode = (societe.GetCaisse(reglementFournisseur.CaisseNo) ?? throw new ApplicationException("Impossible de charger la caisse.")).GetMode(reglementFournisseur.ModeReglementNo);
		if (mode == null)
		{
			throw new ApplicationException("Impossible de charger le mode de règlement.");
		}
		if (mode.Type != ReglementType.Cheque && mode.Type != ReglementType.Traite)
		{
			throw new ApplicationException("Le règlement n'est pas de type pièce.");
		}
		if (reglementFournisseur.Recuperer != RemisFournisseur.NonRecuperer)
		{
			throw new ApplicationException("Le règlement est récuperé.");
		}
		if (reglementFournisseur.IsAnnule)
		{
			throw new ApplicationException("Le règlement [" + reglementFournisseur.Numero + "] est annulé.");
		}
		if (!reglementFournisseur.ChequeSigne)
		{
			throw new ApplicationException("Le règlement [" + reglementFournisseur.Numero + "] n'est pas signé.");
		}
		reglementFournisseur.ChequeSigne = false;
		_reglementFournisseurRepository.UpdateSignature(reglementFournisseur.No, pieceSigne: false, utilisateur.No);
		_notifyService.Notify(TypeEntity.ReglementFournisseur, reglementFournisseur.No, TypeAction.Modification, reglementFournisseur.SocieteNo);
	}

	public void ReglementFournisseurAnnulerRecupererPiece(int reglementNo)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		ReglementFournisseur reglementFournisseur = ReglementFournisseurGet(reglementNo);
		if (reglementFournisseur == null)
		{
			throw new ApplicationException("Impossible de charger le règlement.");
		}
		if (reglementFournisseur.Recuperer != RemisFournisseur.Recuperer)
		{
			throw new ApplicationException("Le règlement est n'est pas récupéré.");
		}
		if (reglementFournisseur.IsImpaye == ImpayeEtat.Impaye)
		{
			throw new ApplicationException("Le règlement est un impayé.");
		}
		if (reglementFournisseur.IsPointe)
		{
			throw new ApplicationException("Le règlement est rapproché.");
		}
		if (reglementFournisseur.Type == ReglementType.Traite && reglementFournisseur.IsDecaisse)
		{
			throw new ApplicationException("La pièce du règlement est décaissée.");
		}
		reglementFournisseur.Recuperer = RemisFournisseur.NonRecuperer;
		reglementFournisseur.RecouvreurNom = string.Empty;
		reglementFournisseur.RecouvreurCin = string.Empty;
		reglementFournisseur.DateRecuperation = DateHelper.GetMinSqlDateTime();
		_reglementFournisseurRepository.UpdateRecuperation(reglementFournisseur.No, reglementFournisseur.Recuperer, reglementFournisseur.DateRecuperation, reglementFournisseur.RecouvreurCin, reglementFournisseur.RecouvreurNom, utilisateur.No);
		_notifyService.Notify(TypeEntity.ReglementFournisseur, reglementFournisseur.No, TypeAction.Modification, reglementFournisseur.SocieteNo);
	}

	public void ReglementFournisseurComptaTransformerImpaye(int reglementNo, DateTime dateImp)
	{
		Societe societe = SocieteManager.Societe;
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		ReglementFournisseur reglementFournisseur = _reglementFournisseurRepository.Get(reglementNo);
		if (reglementFournisseur == null)
		{
			throw new ApplicationException("Impossible de charger le règlement.");
		}
		if (reglementFournisseur.IsImpaye != ImpayeEtat.NonImpaye)
		{
			throw new ApplicationException("Le règlement est déjà un impayé.");
		}
		if (reglementFournisseur.IsAnnule)
		{
			throw new ApplicationException("Le règlement est annulé.");
		}
		if (reglementFournisseur.Type != ReglementType.Cheque && reglementFournisseur.Type != ReglementType.Traite)
		{
			throw new ApplicationException("Le règlement n'est pas de type pièce.");
		}
		if (reglementFournisseur.IsComptabilise != EtatComptabilite.Comptabilise)
		{
			throw new ApplicationException("Le règlement doit être comptabilisé.");
		}
		if (societe.RecupererPieceFrs && reglementFournisseur.Recuperer != RemisFournisseur.Recuperer)
		{
			throw new ApplicationException("Le règlement doit être récupéré.");
		}
		if (reglementFournisseur.IsPointe)
		{
			throw new ApplicationException("Le règlement est rapproché.");
		}
		if (reglementFournisseur.Type == ReglementType.Traite && reglementFournisseur.IsDecaisse)
		{
			throw new ApplicationException("La pièce du règlement ne doit pas être décaissé.");
		}
		if (reglementFournisseur.Type == ReglementType.Traite && reglementFournisseur.DateEcheance.Date > DateTime.Now.Date)
		{
			throw new ApplicationException("Le règlement doit être échu.");
		}
		if (reglementFournisseur.Type == ReglementType.Cheque && reglementFournisseur.DateRecuperation.Date > DateTime.Now.Date)
		{
			throw new ApplicationException("Le règlement doit être échu.");
		}
		reglementFournisseur.DateModification = DateTime.Now;
		reglementFournisseur.ModificateurNo = utilisateur.No;
		reglementFournisseur.ChangeEtatImpaye(ImpayeEtat.Impaye, dateImp);
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_reglementFournisseurRepository.UpdateEtatImpaye(reglementFournisseur);
		_notifyService.Notify(TypeEntity.ReglementFournisseur, reglementFournisseur.No, TypeAction.Modification, reglementFournisseur.SocieteNo);
		_notifyService.Notify(TypeEntity.ImpayeFournisseur, reglementFournisseur.No, TypeAction.Ajout, reglementFournisseur.SocieteNo);
		transactionScope.Complete();
	}

	public void ReglementFournisseurTransformerImpaye(int reglementNo, DateTime dateImp, string commentaire, int soucheNo, int representantNo)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		Societe societe = SocieteManager.Societe;
		SocieteDevise defaultDeviseSociete = societe.GetDefaultDeviseSociete();
		if (defaultDeviseSociete == null)
		{
			throw new ApplicationException("Impossible de charger la devise scoiété.");
		}
		ReglementFournisseur reglementFournisseur = _reglementFournisseurRepository.Get(reglementNo);
		if (reglementFournisseur == null)
		{
			throw new ApplicationException($"Impossible de charger le règlement [{reglementNo}].");
		}
		if (reglementFournisseur.TiersType != TiersType.Fournisseur && reglementFournisseur.TiersType != TiersType.Autre)
		{
			throw new InvalidOperationException("Le type de tiers [" + reglementFournisseur.FournisseurCode + "] du règlement [" + reglementFournisseur.Numero + "] est [" + reglementFournisseur.TiersType.GetDisplayDescription() + "]!");
		}
		Caisse caisse = Get(reglementFournisseur.CaisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Opération invalide! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grf))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (!SocieteManager.UserHasAutorisationSouche(ErpDomaine.Achat, soucheNo))
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorAutorisationSouche);
		}
		if (reglementFournisseur.IsImpaye != ImpayeEtat.NonImpaye)
		{
			throw new ApplicationException("Le règlement est déjà un impayé.");
		}
		if (reglementFournisseur.IsAnnule)
		{
			throw new ApplicationException("Le règlement est annulé.");
		}
		if (reglementFournisseur.Type != ReglementType.Cheque && reglementFournisseur.Type != ReglementType.Traite)
		{
			throw new ApplicationException("Le règlement n'est pas de type pièce.");
		}
		if (societe.RecupererPieceFrs && reglementFournisseur.Recuperer != RemisFournisseur.Recuperer)
		{
			throw new ApplicationException("Le règlement doit être récupéré.");
		}
		if (reglementFournisseur.IsPointe)
		{
			throw new ApplicationException("Le règlement est rapproché.");
		}
		if (reglementFournisseur.Type == ReglementType.Traite && reglementFournisseur.IsDecaisse)
		{
			throw new ApplicationException("La pièce du règlement ne doit pas être décaissé.");
		}
		if (reglementFournisseur.Type == ReglementType.Traite && reglementFournisseur.DateEcheance.Date > DateTime.Now.Date)
		{
			throw new ApplicationException("Le règlement doit être échu.");
		}
		if (reglementFournisseur.IsReserveDossierFrs)
		{
			throw new ApplicationException("Le règlement [" + reglementFournisseur.Numero + "] est réservé dans un dossier de règlement fournisseur.");
		}
		decimal num = Math.Round(reglementFournisseur.Montant * reglementFournisseur.DeviseCours, defaultDeviseSociete.NombreDecimales, MidpointRounding.AwayFromZero);
		Echeance echeance = new Echeance(reglementNo, reglementFournisseur.Numero, ErpDomaine.Achat, ErpDocumentType.None, dateImp, reglementFournisseur.Montant, reglementFournisseur.Montant, reglementFournisseur.DateEcheance, EcheanceType.ImpayeFournisseur, reglementFournisseur.ModeReglementNo, reglementFournisseur.SocieteNo, reglementFournisseur.FournisseurNo, reglementFournisseur.FournisseurCode, reglementFournisseur.FournisseurIntitule, reglementFournisseur.FournisseurNo, reglementFournisseur.FournisseurCode, reglementFournisseur.FournisseurIntitule, reglementFournisseur.DeviseNo, soucheNo, representantNo, reglementFournisseur.DeviseCours, commentaire, num, num)
		{
			UtilisateurNo = utilisateur.No,
			EcheanceReporte = reglementFournisseur.DateEcheance
		};
		reglementFournisseur.DateModification = DateTime.Now;
		reglementFournisseur.ModificateurNo = utilisateur.No;
		reglementFournisseur.ChangeEtatImpaye(ImpayeEtat.Impaye, dateImp);
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_reglementFournisseurRepository.UpdateEtatImpaye(reglementFournisseur);
		if (reglementFournisseur.TiersType == TiersType.Fournisseur)
		{
			int? num2 = _echeanceRepository.Create(echeance);
			_notifyService.Notify(TypeEntity.Echeance, num2.GetValueOrDefault(), TypeAction.Ajout, reglementFournisseur.SocieteNo);
		}
		_notifyService.Notify(TypeEntity.ReglementFournisseur, reglementFournisseur.No, TypeAction.Modification, reglementFournisseur.SocieteNo);
		_notifyService.Notify(TypeEntity.ImpayeFournisseur, reglementFournisseur.No, TypeAction.Ajout, reglementFournisseur.SocieteNo);
		transactionScope.Complete();
	}

	public void RetenuFourisseurUpdateInfoLibre(int retenuNo, string info1, string info2, string info3, string info4)
	{
		RetenuALaSource retenuALaSource = _retenuALaSourceRepository.Get(retenuNo);
		if (retenuALaSource == null)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorReglement);
		}
		InformationLibre informationLibre = SocieteManager.InfoLibreGrfGet() ?? throw new ApplicationException("Impossible de charger les paramétrages des informations libres");
		string text = (informationLibre.IsPersonnaliserInfoLibre1 ? informationLibre.IntituleInfoLibre1 : "Info 1");
		if (informationLibre.IsObligatoireInfoLibre1 && string.IsNullOrEmpty(info1))
		{
			throw new ApplicationException(text + " est obligatoire");
		}
		string text2 = (informationLibre.IsPersonnaliserInfoLibre2 ? informationLibre.IntituleInfoLibre2 : "Info 2");
		if (informationLibre.IsObligatoireInfoLibre2 && string.IsNullOrEmpty(info2))
		{
			throw new ApplicationException(text2 + " est obligatoire");
		}
		string text3 = (informationLibre.IsPersonnaliserInfoLibre3 ? informationLibre.IntituleInfoLibre3 : "Info 3");
		if (informationLibre.IsObligatoireInfoLibre3 && string.IsNullOrEmpty(info3))
		{
			throw new ApplicationException(text3 + " est obligatoire");
		}
		string text4 = (informationLibre.IsPersonnaliserInfoLibre4 ? informationLibre.IntituleInfoLibre4 : "Info 4");
		if (informationLibre.IsObligatoireInfoLibre4 && string.IsNullOrEmpty(info4))
		{
			throw new ApplicationException(text4 + " est obligatoire");
		}
		_retenuALaSourceRepository.Update(retenuNo, info1, info2, info3, info4);
		_notifyService.Notify(TypeEntity.ReglementFournisseur, retenuALaSource.No, TypeAction.Modification, retenuALaSource.SocieteNo);
	}

	public void RetenueFournisseurUpdateCours(int retenueNo, int deviseSocieteNo, decimal cours, bool isCommercial)
	{
		RetenuALaSource retenuALaSource = _retenuALaSourceRepository.Get(retenueNo);
		if (retenuALaSource == null)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorReglement);
		}
		if (retenuALaSource.IsComptabilise)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglementComptabilise);
		}
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		SocieteDevise devise = SocieteManager.Societe.GetDevise(deviseSocieteNo);
		if (devise == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorDeviseNo);
		}
		cours = ((retenuALaSource.DeviseNo == deviseSocieteNo) ? 1m : cours);
		retenuALaSource.Cours = cours;
		retenuALaSource.MontantDeviseSociete = Math.Round(retenuALaSource.Montant * cours, devise.NombreDecimales, MidpointRounding.AwayFromZero);
		retenuALaSource.ModificateurNo = utilisateur.No;
		if (isCommercial && retenuALaSource.Montant != retenuALaSource.Solde)
		{
			throw new ApplicationException("Retenue soldé.");
		}
		if (retenuALaSource.TiersType == TiersType.Fournisseur)
		{
			retenuALaSource.SoldeDeviseSociete = retenuALaSource.MontantDeviseSociete;
		}
		_retenuALaSourceRepository.UpdateCours(retenuALaSource);
		_notifyService.Notify(TypeEntity.ReglementFournisseur, retenuALaSource.No, TypeAction.Modification, retenuALaSource.SocieteNo);
	}

	public bool EcheanceHasRetenue(int echeanceNo)
	{
		return _retenuALaSourceRepository.EcheanceHasRetenue(echeanceNo);
	}

	public bool EcheanceHasRetenue(int echeanceNo, int dossierNo)
	{
		return _retenuALaSourceRepository.EcheanceHasRetenue(echeanceNo, dossierNo);
	}

	public void ReglementFournisseurUpdateCours(int reglementNo, int deviseSocieteNo, decimal cours, bool isCommercial)
	{
		ReglementFournisseur reglementFournisseur = _reglementFournisseurRepository.Get(reglementNo);
		if (reglementFournisseur == null)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorReglement);
		}
		if (reglementFournisseur.IsComptabilise == EtatComptabilite.Comptabilise)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglementComptabilise);
		}
		if (reglementFournisseur.IsAnnule)
		{
			throw new ApplicationException("Le règlement est annulé.");
		}
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		SocieteDevise devise = SocieteManager.Societe.GetDevise(deviseSocieteNo);
		if (devise == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorDeviseNo);
		}
		cours = ((reglementFournisseur.DeviseNo == deviseSocieteNo) ? 1m : cours);
		reglementFournisseur.ModificateurNo = utilisateur.No;
		reglementFournisseur.DateModification = DateTime.Now;
		reglementFournisseur.DeviseCours = cours;
		reglementFournisseur.MontantDeviseSociete = Math.Round(reglementFournisseur.Montant * cours, devise.NombreDecimales, MidpointRounding.AwayFromZero);
		if (isCommercial && reglementFournisseur.Montant != reglementFournisseur.Solde)
		{
			throw new ApplicationException("Règlement partiellement soldé.");
		}
		if (reglementFournisseur.TiersType == TiersType.Fournisseur)
		{
			reglementFournisseur.SoldeDevise = reglementFournisseur.MontantDeviseSociete;
		}
		_reglementFournisseurRepository.Update(reglementFournisseur);
		_notifyService.Notify(TypeEntity.ReglementFournisseur, reglementFournisseur.No, TypeAction.Modification, reglementFournisseur.SocieteNo);
	}

	public ReglementClient ReglementGet(int no)
	{
		if (no <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorNumeroInvalide);
		}
		return _reglementClientRepository.Get(no);
	}

	public Mouvement MouvementGet(int no)
	{
		return _mouvementRepository.Get(no);
	}

	public ReglementClient ReglementGet(int societeNo, string numero)
	{
		if (societeNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorSocieteNo);
		}
		if (string.IsNullOrEmpty(numero))
		{
			throw new ArgumentNullException("Le numéro est invalide!");
		}
		return _reglementClientRepository.Get(societeNo, numero);
	}

	public void SetSynchroniserReglementClient(int reglementNo, int ErpReglementNo, bool isSynchroniser, bool isImportationFromErp = false, bool useNotification = true)
	{
		ReglementClient reglementClient = _reglementClientRepository.Get(reglementNo);
		if (reglementClient == null)
		{
			throw new Exception($"Impossible de charger le règlement client | N° {reglementClient.No}");
		}
		if ((reglementClient.IsComptabilise == EtatComptabilite.Comptabilise || isImportationFromErp) && reglementClient.IsSynchroniser != isSynchroniser)
		{
			reglementClient.IsSynchroniser = isSynchroniser;
			reglementClient.SynchroErpNo = ErpReglementNo;
			_reglementClientRepository.UpdateErpSynchro(reglementClient);
			if (useNotification)
			{
				_notifyService.Notify(TypeEntity.Reglement, reglementClient.No, TypeAction.Modification, reglementClient.SocieteNo);
			}
		}
	}

	public void SetSynchroniserReglementFournisseur(int reglementNo, int ErpReglementNo, bool isSynchroniser, bool isImportationFromErp = false)
	{
		ReglementFournisseur reglementFournisseur = _reglementFournisseurRepository.Get(reglementNo);
		if (reglementFournisseur == null)
		{
			throw new Exception($"Impossible de charger le règlement fournisseur. | N° {reglementNo}");
		}
		if ((reglementFournisseur.IsComptabilise == EtatComptabilite.Comptabilise || isImportationFromErp) && reglementFournisseur.IsSynchroniser != isSynchroniser)
		{
			reglementFournisseur.IsSynchroniser = isSynchroniser;
			reglementFournisseur.SynchroErpNo = ErpReglementNo;
			_reglementFournisseurRepository.UpdateErpSynchro(reglementFournisseur);
			_notifyService.Notify(TypeEntity.ReglementFournisseur, reglementFournisseur.No, TypeAction.Modification, reglementFournisseur.SocieteNo);
		}
	}

	public IEnumerable<ReglementClient> ReglementGetAll(int societeNo, TiersType[] tiersType = null, Utilisateur utilisateur = null, Etat? etat = null, Remis? remis = null, DateTime? dateDebut = null, params int[] modesNo)
	{
		if (societeNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorSocieteNo);
		}
		tiersType = tiersType ?? System.Enum.GetValues(typeof(TiersType)).OfType<TiersType>().ToArray();
		utilisateur = utilisateur ?? SocieteManager.Utilisateur;
		if (!utilisateur.IsAdmin)
		{
			return _reglementClientRepository.GetAll(societeNo, tiersType, dateDebut, (from a in _autorisationCaisseRepository.GetAll(utilisateur.No, societeNo, ProfilType.Grc)
				select a.CaisseNo).ToArray());
		}
		return _reglementClientRepository.GetAll(societeNo, tiersType, etat, remis, dateDebut, modesNo);
	}

	public IEnumerable<ReglementClient> ReglementGetAllByClient(int societeNo, int clientNo, Etat? etat = null, Utilisateur utilisateur = null)
	{
		if (societeNo <= 0)
		{
			throw new ArgumentException("societeNo");
		}
		if (clientNo <= 0)
		{
			throw new ArgumentException("clientNo");
		}
		utilisateur = utilisateur ?? SocieteManager.Utilisateur;
		if (!utilisateur.IsAdmin)
		{
			return _reglementClientRepository.GetAllByClient(societeNo, clientNo, etat, (from a in _autorisationCaisseRepository.GetAll(utilisateur.No, societeNo, ProfilType.Grc)
				select a.CaisseNo).ToArray());
		}
		return _reglementClientRepository.GetAllByClient(societeNo, clientNo, etat);
	}

	public IEnumerable<ReglementClient> ReglementGetAll(int societeNo, EtatComptabilite etat)
	{
		if (societeNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorSocieteNo);
		}
		return _reglementClientRepository.GetAll(societeNo, etat);
	}

	public IEnumerable<ReglementClient> ReglementClientSynchronisationGetAll(int societeNo, DateTime dateDebut, DateTime dateFin, bool isSynchroniser)
	{
		if (societeNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorSocieteNo);
		}
		return _reglementClientRepository.GetAllSynchroErp(societeNo, dateDebut, dateFin, isSynchroniser);
	}

	public IEnumerable<ReglementClient> GetAllReglementClientHasErpNo(DateTime dateFin, Societe societe = null)
	{
		int societeNo = societe?.No ?? SocieteManager.Societe.No;
		IReglementClientRepository reglementClientRepository = _reglementClientRepository;
		DateTime? dateFin2 = dateFin;
		return reglementClientRepository.GetAllReglementHasErpNo(societeNo, null, dateFin2);
	}

	public IEnumerable<ReglementFournisseur> GetAllReglementFournisseurHasErpNo(DateTime dateFin, Societe societe = null)
	{
		int societeNo = societe?.No ?? SocieteManager.Societe.No;
		IReglementFournisseurRepository reglementFournisseurRepository = _reglementFournisseurRepository;
		DateTime? dateFin2 = dateFin;
		return reglementFournisseurRepository.GetAllReglementHasErpNo(societeNo, null, dateFin2);
	}

	public IEnumerable<ReglementClient> GetAllReglementClientHasErpNo(Societe societe = null)
	{
		int societeNo = societe?.No ?? SocieteManager.Societe.No;
		return _reglementClientRepository.GetAllReglementHasErpNo(societeNo);
	}

	public IEnumerable<ReglementFournisseur> GetAllReglementFournisseurHasErpNo(Societe societe = null)
	{
		int societeNo = societe?.No ?? SocieteManager.Societe.No;
		return _reglementFournisseurRepository.GetAllReglementHasErpNo(societeNo);
	}

	public IEnumerable<ReglementClient> ReglementClientGetAllToDeclarationTvaEncaissement(int societeNo, DateTime dateDebutExercice, DateTime dateFin)
	{
		if (societeNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorSocieteNo);
		}
		return _reglementClientRepository.GetAllToDeclarationTvaEncaissement(societeNo, dateDebutExercice, dateFin);
	}

	public IEnumerable<ReglementClient> ReglementGetAll(int societeNo, EtatComptabilite etat, int exercice, int caisseNo, int modeNo, DateTime dateMin, DateTime dateMax, DateTime echeanceMin, DateTime echeanceMax, bool parRapportDateRemise, Remis[] remis)
	{
		if (societeNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorSocieteNo);
		}
		TiersType[] tiersType = new List<TiersType>
		{
			TiersType.Client,
			TiersType.Autre
		}.ToArray();
		return _reglementClientRepository.GetAll(societeNo, etat, exercice, caisseNo, modeNo, dateMin, dateMax, echeanceMin, echeanceMax, parRapportDateRemise, remis, tiersType);
	}

	public IEnumerable<ReglementClient> ReglementGetAll(int societeNo, int clientNo, Etat? etat = null, Remis? remis = null, params int[] modesNo)
	{
		if (societeNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorSocieteNo);
		}
		if (clientNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorClientNo);
		}
		return _reglementClientRepository.GetAllByClient(societeNo, clientNo, etat, remis, modesNo);
	}

	public IEnumerable<ReglementClient> ReglementGetAllByClient(int societeNo, int clientNo, DateTime dateDu, DateTime dateAu, Etat? etat = null, Remis? remis = null, params int[] modesNo)
	{
		if (societeNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorSocieteNo);
		}
		if (clientNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorClientNo);
		}
		return _reglementClientRepository.GetAllByClient(societeNo, clientNo, dateDu, dateAu, etat, remis, modesNo);
	}

	public IEnumerable<ReglementClient> ReglementGetAll(int societeNo, int clientNo, EtatComptabilite etat, DateTime dateMin, DateTime dateMax)
	{
		if (societeNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorSocieteNo);
		}
		if (clientNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorClientNo);
		}
		return _reglementClientRepository.GetAll(societeNo, clientNo, etat, dateMin, dateMax);
	}

	public IEnumerable<ReglementClient> ReglementGetAllByBanque(int societeNo, int banqueNo)
	{
		if (societeNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorSocieteNo);
		}
		if (banqueNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorBanqueInvalide);
		}
		return _reglementClientRepository.GetAllByBanque(societeNo, banqueNo);
	}

	public IEnumerable<ReglementClient> ReglementGetAllByCaisse(int caisseNo, Etat? etat = null, Remis? remis = null, params int[] modesNo)
	{
		if (caisseNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		return _reglementClientRepository.GetAllByCaisse(caisseNo, etat, remis, modesNo);
	}

	public IEnumerable<ReglementClient> ReglementGetAllByCaisse(int[] caissesNo, Etat? etat = null, Remis? remis = null, params int[] modesNo)
	{
		if (caissesNo == null)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		return _reglementClientRepository.GetAllByCaisse(caissesNo, etat, remis, modesNo);
	}

	public IEnumerable<ReglementClient> ReglementGetAllByCaisseNonRemplace(int caisseNo, Etat? etat = null, Remis? remis = null, params int[] modesNo)
	{
		if (caisseNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		return _reglementClientRemplaceRepository.GetAllByCaisseNonRemplace(caisseNo, etat, remis, modesNo);
	}

	public IEnumerable<ReglementFournisseur> ReglementFournisseurGetAllByCaisseNonRemplace(int caisseNo, int banqueNo, int deviseNo)
	{
		if (caisseNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		Societe societe = SocieteManager.Societe;
		return _reglementFournisseurRepository.GetAll(societe.No, caisseNo, banqueNo, deviseNo);
	}

	public IEnumerable<ReglementClient> ReglementGetAllByEcheance(int echeanceNo)
	{
		Echeance echeance = _echeanceRepository.Get(echeanceNo);
		if (echeance == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorEcheanceNo);
		}
		return _reglementClientRepository.GetReglements((from x in echeance.GetAffectations()
			select x.ReglementNo).ToArray());
	}

	public IEnumerable<ReglementClient> ReglementGetAllByInfo4Libelle(int societeNo, string exprSearch)
	{
		if (societeNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorSocieteNo);
		}
		if (string.IsNullOrEmpty(exprSearch))
		{
			throw new ArgumentNullException("exprSearch");
		}
		return _reglementClientRepository.GetAllByInfo4Libelle(societeNo, exprSearch);
	}

	public IEnumerable<ReglementClient> ReglementGetAllToRemplace(int caisseNo, int tierNo, params int[] modesNo)
	{
		if (caisseNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		return _reglementClientRemplaceRepository.GetAllRemplacer(caisseNo, tierNo, modesNo);
	}

	public void ReglementUpdate(int reglementNo, DateTime date, decimal montantDevise, string libelle, string piece, string banqueClient, DateTime echeance, string tire, int? banqueNo, int deviseNo, decimal coursDevise, string affaireNumero, string ribClient, string infoLibre1, string infoLibre2, string infoLibre3, string infoLibre4, string reference, int collaborateurNo, bool isCertifier, DateTime? dateValidite, decimal? montantPlafond, ReglementNature reglementNature)
	{
		DeviseManager deviseManager = SocieteManager.Groupe.DeviseManager;
		if (deviseNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorDeviseNo, "deviseNo");
		}
		if (montantDevise.Equals(0m))
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorMontant);
		}
		ReglementClient reglementClient = _reglementClientRepository.Get(reglementNo);
		if (reglementClient == null)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorReglementNo);
		}
		RemboursementFournisseur byReglementNo = _remboursementFournisseurRepository.GetByReglementNo(reglementNo);
		if (byReglementNo != null)
		{
			throw new ApplicationException("Le règlement est lié à le remboursement fournisseur [" + byReglementNo.Numero + "]!");
		}
		if (reglementClient.TiersType == TiersType.Fournisseur)
		{
			throw new ApplicationException("Le règlement est lié à un remboursement fournisseur!");
		}
		if (reglementClient.IsReglementAvoir)
		{
			throw new ApplicationException("Opération invalide. Règlement d'avoir.");
		}
		Caisse caisse = Get(reglementClient.CaisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grc))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		ModeReglement modeReglement = reglementClient.GetModeReglement();
		if (modeReglement.Type == ReglementType.Traite || modeReglement.Type == ReglementType.Cheque)
		{
			if (string.IsNullOrEmpty(piece))
			{
				throw new ApplicationException(TresorerieCoreMessages.ErrorNumeroPieceInvalide);
			}
			if (string.IsNullOrEmpty(banqueClient))
			{
				throw new ApplicationException(TresorerieCoreMessages.ErrorBanqueClientInvalide);
			}
		}
		if (modeReglement.Type == ReglementType.Virement && !banqueNo.HasValue)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorBanqueInvalide);
		}
		if (modeReglement.Type == ReglementType.Virement)
		{
			if (banqueNo != reglementClient.BanqueNo)
			{
				InformationsBanque byBanqueId = SocieteManager.Groupe.InformationBanqueManager.GetByBanqueId(banqueNo.Value);
				if (byBanqueId != null && byBanqueId.EnSommeil)
				{
					throw new ApplicationException("Opération invalide. La banque est en sommeil");
				}
			}
			if (string.IsNullOrEmpty(banqueClient))
			{
				throw new ApplicationException(TresorerieCoreMessages.ErrorBanqueClientInvalide);
			}
		}
		if (modeReglement.IsReferenceReglementClientObligatoire && string.IsNullOrEmpty(reference))
		{
			throw new ApplicationException("La référence règlement est obligatoire.");
		}
		if (modeReglement.ControllerUniciteReferenceReglementClient && !ReglementClientIsReferenceUnique(modeReglement.No, reference, reglementNo))
		{
			throw new ApplicationException("La référence règlement existe déja.");
		}
		if (reglementClient.IsAnnule)
		{
			throw new ApplicationException("Le règlement est annulé!");
		}
		Devise devise = deviseManager.Get(deviseNo);
		if (devise == null)
		{
			throw new InvalidOperationException(rcRessources.DeviseNotFound);
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		bool flag = false;
		if (reglementClient.Montant != montantDevise)
		{
			if (reglementClient.IsRemis != Remis.NonRemis)
			{
				throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementRemis);
			}
			if (reglementClient.IsAffDevise)
			{
				throw new InvalidOperationException("La devise règlement est changé!");
			}
			if (reglementClient.GetHistoriques().ToList().Count() != 1)
			{
				throw new InvalidOperationException(TresorerieCoreMessages.ErrorrReglementTransfert);
			}
			if (reglementClient.GetAffectations().ToList().Any())
			{
				throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementAffecteEcheances);
			}
			if (reglementClient.GetRemplacements().ToList().Any())
			{
				throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementRemplace);
			}
			if (reglementClient.GetMesRemplacants().ToList().Any())
			{
				throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementRemplace);
			}
			if (reglementClient.IsComptabilise == EtatComptabilite.Comptabilise)
			{
				throw new InvalidOperationException(TresorerieCoreMessages.ErrorrReglementTransfert);
			}
			if (reglementClient.IsPointe)
			{
				throw new ApplicationException(TresorerieCoreMessages.ErrorReglementRapproche);
			}
			flag = true;
			reglementClient.UpdateMontant(montantDevise, coursDevise);
		}
		Societe societe = SocieteManager.Societe;
		if (reglementClient.DateEcheance.Date != echeance.Date && societe.DelaiPaiementClient)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorControleDelaiMoyenPaiement);
		}
		if (reglementClient.Date.Date != date.Date)
		{
			reglementClient.ChangeDate(date);
		}
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		reglementClient.Libelle = libelle;
		reglementClient.PieceNumero = piece;
		reglementClient.Tire = tire;
		reglementClient.DateEcheance = echeance;
		reglementClient.BanqueTier = banqueClient;
		reglementClient.BanqueNo = banqueNo;
		reglementClient.DateModification = DateTime.Now;
		reglementClient.ModificateurNo = utilisateur.No;
		reglementClient.AffaireNumero = affaireNumero;
		reglementClient.RibClient = ribClient;
		reglementClient.Info1 = infoLibre1;
		reglementClient.Info2 = infoLibre2;
		reglementClient.Info3 = infoLibre3;
		reglementClient.Info4 = infoLibre4;
		reglementClient.Reference = reference;
		reglementClient.CollaborateurNo = collaborateurNo;
		reglementClient.IsCertifier = isCertifier;
		reglementClient.DateValiditer = dateValidite;
		reglementClient.MontantPlafond = montantPlafond;
		reglementClient.ReglementNature = reglementNature;
		if (reglementClient.DeviseNo != deviseNo || reglementClient.DeviseCours != coursDevise)
		{
			if (reglementClient.IsAffDevise)
			{
				throw new InvalidOperationException("La devise règlement est changé!");
			}
			reglementClient.ChangeDevise(deviseNo, coursDevise, devise.NombreDecimales);
		}
		if (flag)
		{
			HistoriqueMvt historiqueMvt = reglementClient.GetHistoriques().Single();
			Lot lot = historiqueMvt.Lot;
			lot.Montant = montantDevise;
			lot.MontantRestant = montantDevise;
			_historiqueRepository.Update(historiqueMvt);
		}
		_reglementClientRepository.Update(reglementClient);
		_notifyService.Notify(TypeEntity.Reglement, reglementNo, TypeAction.Modification, societe.No);
		transactionScope.Complete();
	}

	public void DeleteRemboursementCheque(int no, bool reutiliserCheque)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		Echeance echeance = _echeanceRepository.Get(no);
		if (echeance == null)
		{
			throw new ApplicationException("Echéance remboursement invalide!");
		}
		if (echeance.Type != EcheanceType.RemboursementDivers)
		{
			throw new ApplicationException("Type échéance invalide!");
		}
		if (!echeance.CaisseNo.HasValue)
		{
			throw new ApplicationException("Caisse remboursement client invalde!");
		}
		Caisse caisse = Get(echeance.CaisseNo.Value);
		if (caisse == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Opération invalide! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grc))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		CaisseModeReglement mode = caisse.GetMode(echeance.ModeReglementNo);
		if (mode == null)
		{
			throw new ApplicationException("Mode règlemnent invalide!");
		}
		if (mode.Type != ReglementType.Cheque)
		{
			throw new ApplicationException("Type mode invalide!");
		}
		if (!caisse.HasModeReglement(mode.No))
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorCaisseModeInvalide);
		}
		if (echeance.IsComptaRemboursementClient || echeance.IsComptaVirementTiers)
		{
			throw new ApplicationException("Le remboursement client est comptabilisé!");
		}
		if (echeance.IsRemboursementChequePointe)
		{
			throw new InvalidOperationException("Le remboursement est rapproché!");
		}
		if (echeance.Solde != echeance.Montant)
		{
			throw new ApplicationException("Echéance remboursement est partiellement payée!");
		}
		if (!echeance.ChequeNo.HasValue)
		{
			throw new ApplicationException("Remboursement client invalide![Cheque]");
		}
		Cheque cheque = _chequeRepository.Get(echeance.ChequeNo.Value);
		if (cheque == null)
		{
			throw new ApplicationException("Le chèque du remboursement est invalide!");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_echeanceRepository.Delete(echeance);
		if (reutiliserCheque)
		{
			ReUtiliserChequeFrs(cheque.No);
		}
		else
		{
			string motifAnnulation = "Suppression remboursement n°[" + echeance.DocumentNumero + "]";
			AnnulerChequeFrs(cheque.No, motifAnnulation, isUsed: true);
		}
		_notifyService.Notify(TypeEntity.RemboursementClient, echeance.No, TypeAction.Suppression, echeance.SocieteNo);
		transactionScope.Complete();
	}

	public IEnumerable<RemboursementClient> RemboursementClientAllByLibelle(int societeNo, string exprSearch)
	{
		if (societeNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorSocieteNo);
		}
		if (string.IsNullOrEmpty(exprSearch))
		{
			throw new ArgumentNullException("exprSearch");
		}
		return _remboursementClientRepository.GetAllByLibelle(societeNo, exprSearch);
	}

	public RemboursementClientAllType RemboursementClientAllTypeGet(int no)
	{
		return _remboursementClientAllTypeRepository.Get(no);
	}

	public IEnumerable<RemboursementClientAllType> RemboursementClientAllTypeGetAll(Societe societe = null)
	{
		societe = societe ?? SocieteManager.Societe;
		ProfilType profilType = SocieteManager.Groupe.ProfilType;
		if (SocieteManager.Utilisateur.IsAdmin)
		{
			return _remboursementClientAllTypeRepository.GetAll(societe.No);
		}
		int[] caisses = (from a in _autorisationCaisseRepository.GetAll(SocieteManager.Utilisateur.No, societe.No, profilType)
			select a.CaisseNo).ToArray();
		return (from x in _remboursementClientAllTypeRepository.GetAll(societe.No)
			where caisses.Any((int y) => y == x.CaisseNo)
			orderby x.Date descending
			select x).ToList();
	}

	public IEnumerable<RemboursementClientAllType> RemboursementClientAllTypeGetAll(DateTime dateDebut, DateTime dateFin, Societe societe = null)
	{
		societe = societe ?? SocieteManager.Societe;
		ProfilType profilType = SocieteManager.Groupe.ProfilType;
		if (SocieteManager.Utilisateur.IsAdmin)
		{
			return _remboursementClientAllTypeRepository.GetAll(societe.No, dateDebut, dateFin);
		}
		int[] caisses = (from a in _autorisationCaisseRepository.GetAll(SocieteManager.Utilisateur.No, societe.No, profilType)
			select a.CaisseNo).ToArray();
		return (from x in _remboursementClientAllTypeRepository.GetAll(societe.No, dateDebut, dateFin)
			where caisses.Any((int y) => y == x.CaisseNo)
			orderby x.Date descending
			select x).ToList();
	}

	public void DeleteRemboursementClientRS(int remboursementNo)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		if (remboursementNo <= 0)
		{
			throw new ArgumentException("RemboursementNo");
		}
		RemboursementClient remboursementClient = _remboursementClientRepository.Get(remboursementNo);
		if (remboursementClient == null)
		{
			throw new InvalidOperationException("Impossible de charger le remboursement client!");
		}
		Caisse caisse = Get(remboursementClient.CaisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Opération invalide! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grc))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (remboursementClient.Comptabilise != EtatComptabilite.NonComptabilise)
		{
			throw new InvalidOperationException("Le remboursement est comptabilisé!");
		}
		int echeanceRemboursement = _echeanceRepository.GetEcheanceRemboursement(remboursementClient.No);
		if (echeanceRemboursement <= 0)
		{
			throw new ApplicationException("Echéance remboursement n'existe pas!");
		}
		Echeance echeance = _echeanceRepository.Get(echeanceRemboursement);
		if (echeance == null)
		{
			throw new ApplicationException("Echéance remboursement invalide!");
		}
		if (echeance.Type != EcheanceType.RemboursementClientRS)
		{
			throw new ApplicationException("Type échéance invalide!");
		}
		if (echeance.IsComptaRemboursementClient || echeance.IsComptaVirementTiers)
		{
			throw new ApplicationException("Le remboursement est comptabilisé!");
		}
		if (echeance.Solde != remboursementClient.Montant || echeance.SoldeDeviseSociete != remboursementClient.MontantDeviseSociete)
		{
			throw new ApplicationException("Echéance remboursement est partiellement payée!");
		}
		Echeance echeance2 = null;
		if (echeance.Type == EcheanceType.RemboursementClientRS)
		{
			int echeanceRemboursementAvoirRS = _echeanceRepository.GetEcheanceRemboursementAvoirRS(remboursementClient.No);
			if (echeanceRemboursementAvoirRS <= 0)
			{
				throw new ApplicationException("Echéance remboursement n'existe pas!");
			}
			echeance2 = _echeanceRepository.Get(echeanceRemboursementAvoirRS);
			if (echeance2 == null)
			{
				throw new ApplicationException("Impossible de charger la facture d'avoir liée au remboursement [" + remboursementClient.Numero + "].");
			}
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		if (echeance2 != null)
		{
			echeance2.RemoursementEcheanceAvoirNo = null;
			_echeanceRepository.Update(echeance2);
		}
		_remboursementClientRepository.Delete(remboursementClient);
		_echeanceRepository.Delete(echeance);
		if (echeance2 != null)
		{
			_notifyService.Notify(TypeEntity.Echeance, echeance2.No, TypeAction.Modification, echeance2.SocieteNo);
		}
		_notifyService.Notify(TypeEntity.Echeance, echeanceRemboursement, TypeAction.Suppression, echeance.SocieteNo);
		_notifyService.Notify(TypeEntity.RemboursementClient, echeanceRemboursement, TypeAction.Suppression, remboursementClient.SocieteNo);
		transactionScope.Complete();
	}

	public void DeleteRemboursementClientEspece(int remboursementNo)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		if (remboursementNo <= 0)
		{
			throw new ArgumentException("RemboursementNo");
		}
		RemboursementClient remboursementClient = _remboursementClientRepository.Get(remboursementNo);
		if (remboursementClient == null)
		{
			throw new InvalidOperationException("Impossible de charger le remboursement client!");
		}
		Caisse caisse = Get(remboursementClient.CaisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Opération invalide! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grc))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (remboursementClient.Comptabilise != EtatComptabilite.NonComptabilise)
		{
			throw new InvalidOperationException("Le remboursement est comptabilisé!");
		}
		int echeanceRemboursement = _echeanceRepository.GetEcheanceRemboursement(remboursementClient.No);
		if (echeanceRemboursement <= 0)
		{
			throw new ApplicationException("Echéance remboursement n'existe pas!");
		}
		Echeance echeance = _echeanceRepository.Get(echeanceRemboursement);
		if (echeance == null)
		{
			throw new ApplicationException("Echéance remboursement invalide!");
		}
		if (echeance.Type != EcheanceType.RemboursementClientEspece)
		{
			throw new ApplicationException("Type échéance invalide!");
		}
		if (echeance.IsComptaRemboursementClient || echeance.IsComptaVirementTiers)
		{
			throw new ApplicationException("Le remboursement est comptabilisé!");
		}
		if (echeance.Solde != remboursementClient.Montant)
		{
			throw new ApplicationException("Echéance remboursement est partiellement payée!");
		}
		List<HistoriqueMvt> list = (from x in _historiqueRepository.GetAllByCaisse(remboursementClient.CaisseNo, remboursementClient.DeviseNo)
			where x.MouvementOutNo == remboursementNo
			select x).ToList();
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		foreach (HistoriqueMvt item in list)
		{
			HistoriqueMvt historiqueMvt = _historiqueRepository.GetAllByMouvement(item.MouvementInNo).Single((HistoriqueMvt x) => x.Sens == SensMouvement.Entree);
			if (historiqueMvt == null)
			{
				throw new ArgumentNullException("Historique d'entree invalide!");
			}
			historiqueMvt.Lot.MontantRestant += item.Montant;
			_historiqueRepository.Update(historiqueMvt);
			_historiqueRepository.Delete(item);
		}
		_remboursementClientRepository.Delete(remboursementClient);
		_echeanceRepository.Delete(echeance);
		_notifyService.Notify(TypeEntity.Echeance, echeanceRemboursement, TypeAction.Suppression, echeance.SocieteNo);
		_notifyService.Notify(TypeEntity.RemboursementClient, echeanceRemboursement, TypeAction.Suppression, remboursementClient.SocieteNo);
		transactionScope.Complete();
	}

	public RemboursementClient RemboursementClientGet(int no)
	{
		return _remboursementClientRepository.Get(no);
	}

	public void RemboursementDeComptabiliser(int remboursementNo)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		RemboursementClientAllType remboursementClientAllType = _remboursementClientAllTypeRepository.Get(remboursementNo);
		if (remboursementClientAllType == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorRemboursementInvalid);
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		if (remboursementClientAllType.Comptabilise == EtatComptabilite.NonComptabilise)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorRemboursementNonComptabilise);
		}
		_ecritureComptaRepository.DeleteLigneRemboursementClient(remboursementClientAllType.No);
		_remboursementClientAllTypeRepository.UpdateEtatComptabilisation(remboursementNo, EtatComptabilite.NonComptabilise);
		_notifyService.Notify(TypeEntity.RemboursementClient, remboursementNo, TypeAction.Modification, remboursementClientAllType.SocieteNo);
		transactionScope.Complete();
	}

	public void RemboursementFournisseurDeComptabiliser(int remboursementNo)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		RemboursementFournisseur remboursementFournisseur = _remboursementFournisseurRepository.Get(remboursementNo);
		if (remboursementFournisseur == null)
		{
			throw new InvalidOperationException($"Impossible de charger le remboursement fournisseur [{remboursementNo}]");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		if (remboursementFournisseur.Comptabilise != EtatComptabilite.Comptabilise)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorRemboursementNonComptabilise);
		}
		_ecritureComptaRepository.DeleteLigneRemboursementFournisseur(remboursementFournisseur.ReglementNo);
		_remboursementFournisseurRepository.UpdateEtatComptabilisation(remboursementNo, EtatComptabilite.NonComptabilise);
		ReglementClient reglementClient = _reglementClientRepository.Get(remboursementFournisseur.ReglementNo);
		if (reglementClient == null)
		{
			throw new InvalidOperationException("Impossible de charger le règlement du remboursement [" + remboursementFournisseur.Numero + "].");
		}
		if (reglementClient.IsComptabilise != EtatComptabilite.Comptabilise)
		{
			throw new ApplicationException($"Le règlement [{reglementClient}] est non comptabilisé.");
		}
		if (reglementClient.TiersType != TiersType.Fournisseur)
		{
			throw new ApplicationException("Le type tiers du règlement [" + reglementClient.Numero + "] est invalide!");
		}
		reglementClient.ChangeEtatComptabilise(EtatComptabilite.NonComptabilise);
		reglementClient.DateModification = DateTime.Now;
		reglementClient.ModificateurNo = utilisateur.No;
		_reglementClientRepository.Update(reglementClient);
		_notifyService.Notify(TypeEntity.Reglement, reglementClient.No, TypeAction.Modification, reglementClient.SocieteNo);
		_notifyService.Notify(TypeEntity.RemboursementFournisseur, remboursementNo, TypeAction.Modification, remboursementFournisseur.SocieteNo);
		transactionScope.Complete();
	}

	public void RemplacementDelete(int reglementOldNo, int reglementNewNo)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		ReglementClient reglementOld = ReglementGet(reglementOldNo);
		if (reglementOld == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementNo);
		}
		ReglementClient reglementNew = ReglementGet(reglementNewNo);
		if (reglementNew == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementNo);
		}
		Caisse caisse = Get(reglementOld.CaisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Opération invalide! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grc))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (reglementOld.IsAnnule)
		{
			throw new ApplicationException("Le règlement [" + reglementOld.Numero + "] est annulé!");
		}
		if (reglementNew.IsAnnule)
		{
			throw new ApplicationException("Le règlement [" + reglementNew.Numero + "] est annulé!");
		}
		if (reglementOld.IsRemplacmentComptabilise == EtatComptabilite.Comptabilise)
		{
			throw new InvalidOperationException("Opération invalide! L'action du remplacement est comptabilisée!");
		}
		Remplacement remplacement = reglementOld.GetMesRemplacants().SingleOrDefault((Remplacement x) => x.ReglementRemplacantNo == reglementNew.No);
		if (remplacement == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementRemplacantInvalide);
		}
		if (reglementOld.DeviseNo != reglementNew.DeviseNo)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorDeviseReglementRemplacant);
		}
		decimal num = remplacement.Montant * reglementNew.DeviseCours;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_remplacementRepository.Delete(remplacement);
		Remplace isRemplace = reglementOld.IsRemplace;
		reglementOld.SoldeToRemplace += remplacement.Montant;
		reglementOld.DateModification = DateTime.Now;
		reglementOld.ModificateurNo = utilisateur.No;
		_reglementClientRepository.Update(reglementOld);
		reglementNew.Solde += remplacement.Montant;
		reglementNew.SoldeDeviseSociete += num;
		reglementNew.DateModification = DateTime.Now;
		reglementNew.ModificateurNo = utilisateur.No;
		_reglementClientRepository.Update(reglementNew);
		if (isRemplace == Remplace.TotalementRemplace)
		{
			HistoriqueMvt historiqueMvt = reglementOld.GetHistoriques().Single((HistoriqueMvt x) => x.Sens == SensMouvement.Sortie && x.Domaine == MouvementDomaine.ReglementClient);
			_historiqueRepository.Delete(historiqueMvt);
			HistoriqueMvt historiqueMvt2 = (from x in (from x in reglementOld.GetHistoriques()
					where x.CaisseNo == reglementOld.CaisseNo && x.Sens == SensMouvement.Entree && x.Lot.IsEpuise
					select x).ToList()
				orderby x.No
				select x).Last();
			historiqueMvt2.Lot.MontantRestant = reglementOld.Montant;
			_historiqueRepository.Update(historiqueMvt2);
		}
		_notifyService.Notify(TypeEntity.Reglement, reglementNewNo, TypeAction.Modification, reglementNew.SocieteNo);
		_notifyService.Notify(TypeEntity.Reglement, reglementOldNo, TypeAction.Modification, reglementNew.SocieteNo);
		transactionScope.Complete();
	}

	public void RemplacementUpdate(int reglementOldNo, int reglementNewNo, decimal montant, int deviseSocieteNo)
	{
		if (reglementOldNo <= 0 || reglementNewNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorReglementNo);
		}
		if (montant <= 0m)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorMontant);
		}
		if (deviseSocieteNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorDeviseNo);
		}
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		Societe societe = SocieteManager.Societe;
		ReglementClient reglementOld = ReglementGet(reglementOldNo);
		if (reglementOld == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementNo);
		}
		ReglementClient reglementNew = ReglementGet(reglementNewNo);
		if (reglementNew == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementNo);
		}
		Caisse caisse = Get(reglementOld.CaisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Opération invalide! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grc))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (reglementOld.IsAnnule)
		{
			throw new ApplicationException("Le règlement [" + reglementOld.Numero + "] est annulé!");
		}
		if (reglementNew.IsAnnule)
		{
			throw new ApplicationException("Le règlement [" + reglementNew.Numero + "] est annulé!");
		}
		Remplacement remplacement = reglementOld.GetMesRemplacants().SingleOrDefault((Remplacement x) => x.ReglementRemplacantNo == reglementNew.No);
		if (remplacement == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglementRemplacantInvalide);
		}
		if (montant == remplacement.Montant)
		{
			return;
		}
		if (reglementNew.Solde + remplacement.Montant < montant)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementMontantRemplacant);
		}
		if (reglementOld.SoldeToRemplace + remplacement.Montant < montant)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementMontantRemplacant);
		}
		if (reglementOld.DeviseNo != deviseSocieteNo || reglementNew.DeviseNo != deviseSocieteNo)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorDeviseReglementRemplacant);
		}
		SocieteDevise devise = societe.GetDevise(deviseSocieteNo);
		if (devise == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorDeviseNo);
		}
		decimal num = Math.Round(montant * reglementNew.DeviseCours, devise.NombreDecimales, MidpointRounding.AwayFromZero);
		decimal num2 = Math.Round(remplacement.Montant * reglementNew.DeviseCours, devise.NombreDecimales, MidpointRounding.AwayFromZero);
		decimal montant2 = remplacement.Montant;
		remplacement.Montant = montant;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_remplacementRepository.Update(remplacement);
		Remplace isRemplace = reglementOld.IsRemplace;
		reglementOld.SoldeToRemplace = reglementOld.SoldeToRemplace + montant2 - montant;
		reglementOld.DateModification = DateTime.Now;
		reglementOld.ModificateurNo = utilisateur.No;
		_reglementClientRepository.Update(reglementOld);
		reglementNew.Solde = reglementNew.Solde + montant2 - montant;
		reglementNew.SoldeDeviseSociete = reglementNew.SoldeDeviseSociete + num2 - num;
		reglementNew.DateModification = DateTime.Now;
		reglementNew.ModificateurNo = utilisateur.No;
		_reglementClientRepository.Update(reglementNew);
		if (reglementOld.IsRemplace == Remplace.TotalementRemplace)
		{
			HistoriqueMvt historiqueMvt = new HistoriqueMvt(reglementOldNo, reglementOld.CaisseNo, SensMouvement.Sortie, MouvementDomaine.ReglementClient, reglementOld.ModeReglementNo, reglementOldNo, reglementNewNo, StatutTransfert.None, reglementOld.DeviseNo, new Lot(reglementOldNo, reglementNew.Montant, 0m));
			_historiqueRepository.Create(historiqueMvt);
			HistoriqueMvt historiqueMvt2 = reglementOld.GetHistoriques().Single((HistoriqueMvt x) => x.CaisseNo == reglementOld.CaisseNo && x.Sens == SensMouvement.Entree && !x.Lot.IsEpuise);
			historiqueMvt2.Lot.MontantRestant = 0m;
			_historiqueRepository.Update(historiqueMvt2);
		}
		else if (isRemplace == Remplace.TotalementRemplace && reglementOld.IsRemplace != Remplace.TotalementRemplace)
		{
			HistoriqueMvt historiqueMvt3 = reglementOld.GetHistoriques().Single((HistoriqueMvt x) => x.Sens == SensMouvement.Sortie && x.Domaine == MouvementDomaine.ReglementClient);
			_historiqueRepository.Delete(historiqueMvt3);
			HistoriqueMvt historiqueMvt4 = reglementOld.GetHistoriques().Single((HistoriqueMvt x) => x.CaisseNo == reglementOld.CaisseNo && x.Sens == SensMouvement.Entree && x.Lot.IsEpuise);
			historiqueMvt4.Lot.MontantRestant = reglementOld.Montant;
			_historiqueRepository.Update(historiqueMvt4);
		}
		_notifyService.Notify(TypeEntity.Reglement, reglementNewNo, TypeAction.Modification, reglementNew.SocieteNo);
		_notifyService.Notify(TypeEntity.Reglement, reglementOldNo, TypeAction.Modification, reglementNew.SocieteNo);
		transactionScope.Complete();
	}

	public int Remplacer(int reglementNewNo, int reglementOldNo, decimal montant, int deviseSocieteNo, Societe societe = null)
	{
		if (deviseSocieteNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorDeviseNo);
		}
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		societe = societe ?? SocieteManager.Societe;
		if (montant <= 0m)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorMontantInavlide);
		}
		if (reglementNewNo <= 0)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementRemplace);
		}
		if (reglementOldNo <= 0)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementNo);
		}
		ReglementClient reglementClient = _reglementClientRepository.Get(reglementNewNo);
		if (reglementClient == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementNo);
		}
		ReglementClient reglementOld = _reglementClientRepository.Get(reglementOldNo);
		if (reglementOld == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementNo);
		}
		if (reglementOld.IsReglementAvoir)
		{
			throw new ApplicationException("Opération invalide. Règlement d'avoir.[Remplacé]");
		}
		Caisse caisse = Get(reglementOld.CaisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Opération invalide! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grc))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (reglementOld.IsAnnule)
		{
			throw new ApplicationException("Le règlement[" + reglementOld.Numero + "] est annulé!");
		}
		if (reglementClient.IsAnnule)
		{
			throw new ApplicationException("Le règlement [" + reglementClient.Numero + "] est annulé!");
		}
		if (reglementClient.ClientNo != reglementOld.ClientNo)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglementClient);
		}
		if (reglementOld.GetRemplacements().Any())
		{
			throw new InvalidOperationException(string.Format(TresorerieCoreMessages.InfoReglementReplacant, reglementOld.Numero));
		}
		if (reglementClient.GetMesRemplacants().Any())
		{
			throw new InvalidOperationException(string.Format(TresorerieCoreMessages.InfoReglementRemplace, reglementClient.Numero));
		}
		SocieteModeReglement mode = societe.GetMode(reglementOld.ModeReglementNo);
		if (mode == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorModeReglementInvalide);
		}
		if (mode.Type != ReglementType.Traite && mode.Type != ReglementType.Cheque)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorTypeDuReglementRemplace);
		}
		if (reglementClient.CaisseNo != reglementOld.CaisseNo)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglementCaisse);
		}
		if (reglementClient.IsRemplace != Remplace.NonRemplace)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglementRemplacantInvalide);
		}
		if (reglementOld.GetMesRemplacants().Any((Remplacement x) => x.ReglementRemplacantNo == reglementNewNo))
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorOperationInvalide);
		}
		if (reglementOld.IsRemis != Remis.NonRemis)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementInclutBordereau);
		}
		if (reglementOld.SoldeToRemplace < montant)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementMontant);
		}
		if (reglementClient.Solde < montant)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorSoldeReglementRemplacant);
		}
		if (reglementOld.DeviseNo != deviseSocieteNo || reglementClient.DeviseNo != deviseSocieteNo)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorDeviseReglementRemplacant);
		}
		Remplacement remplacement = new Remplacement(reglementNewNo, montant, reglementOldNo, societe.No);
		SocieteDevise devise = societe.GetDevise(deviseSocieteNo);
		if (devise == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorDeviseNo);
		}
		decimal num = Math.Round(montant * reglementClient.DeviseCours, devise.NombreDecimales, MidpointRounding.AwayFromZero);
		reglementOld.SoldeToRemplace -= montant;
		reglementOld.DateModification = DateTime.Now;
		reglementOld.ModificateurNo = utilisateur.No;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		int? num2 = _remplacementRepository.Create(remplacement);
		_reglementClientRepository.Update(reglementOld);
		reglementClient.Solde -= montant;
		reglementClient.SoldeDeviseSociete -= num;
		reglementClient.DateModification = DateTime.Now;
		reglementClient.ModificateurNo = utilisateur.No;
		_reglementClientRepository.Update(reglementClient);
		if (reglementOld.IsRemplace == Remplace.TotalementRemplace)
		{
			HistoriqueMvt historiqueMvt = new HistoriqueMvt(reglementOldNo, reglementOld.CaisseNo, SensMouvement.Sortie, MouvementDomaine.ReglementClient, reglementOld.ModeReglementNo, reglementOldNo, reglementNewNo, StatutTransfert.None, reglementOld.DeviseNo, new Lot(reglementOldNo, reglementClient.Montant, 0m));
			_historiqueRepository.Create(historiqueMvt);
			HistoriqueMvt historiqueMvt2 = reglementOld.GetHistoriques().Single((HistoriqueMvt x) => x.CaisseNo == reglementOld.CaisseNo && x.Sens == SensMouvement.Entree && !x.Lot.IsEpuise);
			historiqueMvt2.Lot.MontantRestant = 0m;
			_historiqueRepository.Update(historiqueMvt2);
		}
		_notifyService.Notify(TypeEntity.Reglement, reglementNewNo, TypeAction.Modification, reglementClient.SocieteNo);
		_notifyService.Notify(TypeEntity.Reglement, reglementOldNo, TypeAction.Modification, reglementClient.SocieteNo);
		transactionScope.Complete();
		return num2.Value;
	}

	public void RetenueChangeToImprime(int retenueNo, bool isImprimer, DateTime dateImpression)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		RetenuALaSource retenuALaSource = _retenuALaSourceRepository.Get(retenueNo);
		if (retenuALaSource == null)
		{
			throw new ArgumentException("La retenue est invalide!");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_retenuALaSourceRepository.Update(retenueNo, isImprimer, dateImpression, utilisateur.No);
		_notifyService.Notify(TypeEntity.ReglementFournisseur, retenueNo, TypeAction.Modification, retenuALaSource.SocieteNo);
		transactionScope.Complete();
	}

	public int RetenueCreate(string numero, DateTime date, decimal montant, int deviseNo, int fournisseurNo, int modeNo, int caisseNo, string libelle, int nbTimbre, decimal baseRetenue, decimal taux, decimal montantTimbre, string beneficiaire, int dossierNo, string dossierNumero, decimal cours, string fournisseurCode, string fournisseurIntitule, TiersType tiersType, string affaireNumero, decimal montantHt, decimal tauxTva, decimal montantTva, int exerciceFacturation, bool isPrisCharge, bool isConvention, int? operationRetenueNo, string info1, string info2, string info3, string info4, int? retenuePourEcheanceNo = null)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		Societe societe = SocieteManager.Societe;
		if (string.IsNullOrEmpty(numero))
		{
			throw new ArgumentNullException("Le numéro est invalide");
		}
		if (fournisseurNo <= 0)
		{
			throw new ArgumentException("Le fournisseur No est invalide!");
		}
		if (deviseNo <= 0)
		{
			throw new ArgumentException("La devise No est invalide!");
		}
		if (montant <= 0m)
		{
			throw new ApplicationException("Le montant est invalide!");
		}
		Caisse caisse = Get(caisseNo);
		if (caisse == null)
		{
			throw new InvalidOperationException("La caisse No est invalide!");
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Opération invalide! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisseNo))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (_reglementFournisseurRepository.GetRetenue(caisse.SocieteNo, numero) != null)
		{
			throw new ApplicationException("Le numéro de retenue existe déjà!");
		}
		if (!caisse.HasModeReglement(modeNo))
		{
			throw new ApplicationException("Le mode de règlement est invalide!");
		}
		CaisseModeReglement mode = caisse.GetMode(modeNo);
		if (mode == null)
		{
			throw new InvalidOperationException("Le mode No est invalide!");
		}
		if (mode.Type != ReglementType.Autre || !mode.IsRetenu)
		{
			throw new ArgumentException("Le type de retenue est invalide!");
		}
		if (mode.EnSommeil)
		{
			throw new InvalidOperationException("Le mode  est en sommeil!");
		}
		if (nbTimbre < 0 || nbTimbre > 100)
		{
			throw new ArgumentException("Le nombre de timbre est invalide!");
		}
		if (taux <= 0m || taux > 100m)
		{
			throw new ArgumentException("Le taux est invalide!");
		}
		if (montantTimbre < 0m || (nbTimbre > 0 && montantTimbre <= 0m))
		{
			throw new ArgumentException("Montant du timbre invalide!");
		}
		if (baseRetenue <= 0m)
		{
			throw new ArgumentException("La base de retenue est invalide!");
		}
		if (!_retenuALaSourceRepository.CanAddRetenue(societe.No, date))
		{
			throw new ApplicationException("Opération invalide ! \n Impossible d'ajouter une retenue dont la période de déclaration est clôturée.");
		}
		if (retenuePourEcheanceNo.HasValue)
		{
			Echeance echeance = SocieteManager.EcheanceGet(retenuePourEcheanceNo.Value);
			if (echeance == null)
			{
				throw new ApplicationException("Impossible de charger l'échéance.");
			}
			if (_retenuALaSourceRepository.GetAllByEcheance(echeance.No).ToList().Sum((RetenuALaSource x) => x.Montant) + montant > echeance.Solde)
			{
				throw new ApplicationException("Le total des retenues doit être inférieur ou égal au reste à payer de l'échéance.");
			}
		}
		DossierReglement dossier = _dossierReglementRepository.GetDossier(dossierNo);
		if (dossier == null)
		{
			throw new ApplicationException("Impossible de charger le dossier de règlement.");
		}
		if (societe.IsAffaireFournisseurRequired && string.IsNullOrEmpty(affaireNumero))
		{
			throw new ApplicationException(rcRessources.AffaireObligatoire);
		}
		SocieteDevise defaultDeviseSociete = societe.GetDefaultDeviseSociete();
		if (defaultDeviseSociete == null)
		{
			throw new ApplicationException("Impossible de charger la devise société.");
		}
		decimal num = Math.Round(montant * cours, defaultDeviseSociete.NombreDecimales, MidpointRounding.AwayFromZero);
		RetenuALaSource retenuALaSource = new RetenuALaSource
		{
			Numero = numero,
			FournisseurNo = fournisseurNo,
			CaisseNo = caisseNo,
			DeviseNo = deviseNo,
			ModeNo = modeNo,
			Date = date,
			Montant = montant,
			Libelle = libelle,
			NbTimbre = nbTimbre,
			Base = baseRetenue,
			Taux = taux,
			MtTimbre = montantTimbre,
			UtilisateurNo = utilisateur.No,
			SocieteNo = societe.No,
			Beneficiaire = beneficiaire,
			DateImprime = DateHelper.GetMinSqlDateTime(),
			DossierNumero = dossierNumero,
			DossierNo = dossierNo,
			ModificateurNo = utilisateur.No,
			Cours = cours,
			FournisseurCode = fournisseurCode,
			FournisseurIntitule = fournisseurIntitule,
			TiersType = tiersType,
			Solde = montant,
			AffaireNumero = affaireNumero,
			MontantDeviseSociete = num,
			SoldeDeviseSociete = num,
			RetenuePourEcheanceNo = retenuePourEcheanceNo,
			MontantHorsTaxe = montantHt,
			TauxTva = tauxTva,
			MontantTva = montantTva,
			IsConvention = isConvention,
			IsPrisCharge = isPrisCharge,
			AnneeFacturation = exerciceFacturation,
			OperationRetenueNo = operationRetenueNo,
			InfoLibre1 = info1,
			InfoLibre2 = info2,
			InfoLibre3 = info3,
			InfoLibre4 = info4
		};
		if (dossier.IsDossierCommercial && dossier.TiersType != TiersType.Fournisseur)
		{
			retenuALaSource.Solde = 0m;
			retenuALaSource.SoldeDeviseSociete = 0m;
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		int? num2 = _retenuALaSourceRepository.Create(retenuALaSource);
		if (!num2.HasValue || num2.Value == 0)
		{
			throw new ApplicationException("La création de la retenue a échoué!");
		}
		_notifyService.Notify(TypeEntity.ReglementFournisseur, num2.Value, TypeAction.Ajout, societe.No);
		transactionScope.Complete();
		return num2.Value;
	}

	public int RetenueCreate(string numero, DateTime date, decimal montant, int deviseNo, int fournisseurNo, int modeNo, int caisseNo, string libelle, int nbTimbre, decimal baseRetenue, decimal taux, decimal montantTimbre, string beneficiaire, decimal cours, string fournisseurCode, string fournisseurIntitule, TiersType typeTiers, string affaireNumero, bool isDossierCommercial, decimal montantHt, decimal tauxTva, decimal montantTva, int exerciceFacturation, bool isPrisCharge, bool isConvention, int? operationRetenueNo, string info1, string info2, string info3, string info4)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		Societe societe = SocieteManager.Societe;
		if (string.IsNullOrEmpty(numero))
		{
			throw new ArgumentNullException("Le numéro est invalide");
		}
		if (fournisseurNo <= 0)
		{
			throw new ArgumentException("Le fournisseur No est invalide!");
		}
		if (deviseNo <= 0)
		{
			throw new ArgumentException("La devise No est invalide!");
		}
		if (montant <= 0m)
		{
			throw new ApplicationException("Le montant est invalide!");
		}
		if (!isDossierCommercial)
		{
			IErpExercice exercice = SocieteManager.Exercice;
			if (date.Date < exercice.Debut.Date || date.Date > exercice.Fin.Date)
			{
				throw new ApplicationException("La date comptabilisation du retenue n'appartient pas à l'intervalle de l'exercice courant");
			}
		}
		Caisse caisse = Get(caisseNo);
		if (caisse == null)
		{
			throw new InvalidOperationException("La caisse No est invalide!");
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Opération invalide! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisseNo))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (_reglementFournisseurRepository.GetRetenue(caisse.SocieteNo, numero) != null)
		{
			throw new ApplicationException("Le numéro de retenue existe déjà!");
		}
		if (!caisse.HasModeReglement(modeNo))
		{
			throw new ApplicationException("Le mode de règlement est invalide!");
		}
		CaisseModeReglement mode = caisse.GetMode(modeNo);
		if (mode == null)
		{
			throw new InvalidOperationException("Le mode No est invalide!");
		}
		if (mode.Type != ReglementType.Autre || !mode.IsRetenu)
		{
			throw new ArgumentException("Le type de retenue est invalide!");
		}
		if (mode.EnSommeil)
		{
			throw new ArgumentException("Le mode est en sommeil!");
		}
		if (nbTimbre < 0 || nbTimbre > 100)
		{
			throw new ArgumentException("Le nombre de timbre est invalide!");
		}
		if (taux <= 0m || taux > 100m)
		{
			throw new ArgumentException("Le taux est invalide!");
		}
		if (montantTimbre < 0m || (nbTimbre > 0 && montantTimbre <= 0m))
		{
			throw new ArgumentException("Montant du timbre invalide!");
		}
		if (baseRetenue <= 0m)
		{
			throw new ArgumentException("La base de retenue est invalide!");
		}
		if (societe.IsAffaireFournisseurRequired && string.IsNullOrEmpty(affaireNumero))
		{
			throw new ApplicationException(rcRessources.AffaireObligatoire);
		}
		if (!_retenuALaSourceRepository.CanAddRetenue(societe.No, date))
		{
			throw new ApplicationException("Opération invalide ! \n Impossible d'ajouter une retenue dont la période de déclaration est clôturée.");
		}
		SocieteDevise defaultDeviseSociete = societe.GetDefaultDeviseSociete();
		if (defaultDeviseSociete == null)
		{
			throw new ApplicationException("Impossible de charger la devise société.");
		}
		decimal num = Math.Round(montant * cours, defaultDeviseSociete.NombreDecimales, MidpointRounding.AwayFromZero);
		RetenuALaSource retenuALaSource = new RetenuALaSource
		{
			Numero = numero,
			FournisseurNo = fournisseurNo,
			CaisseNo = caisseNo,
			DeviseNo = deviseNo,
			ModeNo = modeNo,
			Date = date,
			Montant = montant,
			Libelle = libelle,
			NbTimbre = nbTimbre,
			Base = baseRetenue,
			Taux = taux,
			MtTimbre = montantTimbre,
			UtilisateurNo = utilisateur.No,
			SocieteNo = societe.No,
			Beneficiaire = beneficiaire,
			DateImprime = DateHelper.GetMinSqlDateTime(),
			ModificateurNo = utilisateur.No,
			Cours = cours,
			FournisseurCode = fournisseurCode,
			FournisseurIntitule = fournisseurIntitule,
			TiersType = typeTiers,
			Solde = montant,
			AffaireNumero = affaireNumero,
			MontantDeviseSociete = num,
			SoldeDeviseSociete = num,
			MontantHorsTaxe = montantHt,
			TauxTva = tauxTva,
			MontantTva = montantTva,
			IsConvention = isConvention,
			IsPrisCharge = isPrisCharge,
			AnneeFacturation = exerciceFacturation,
			OperationRetenueNo = operationRetenueNo,
			InfoLibre1 = info1,
			InfoLibre2 = info2,
			InfoLibre3 = info3,
			InfoLibre4 = info4
		};
		if (isDossierCommercial && typeTiers != TiersType.Fournisseur)
		{
			retenuALaSource.Solde = 0m;
			retenuALaSource.SoldeDeviseSociete = 0m;
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		int? num2 = _retenuALaSourceRepository.Create(retenuALaSource);
		if (!num2.HasValue || num2.Value == 0)
		{
			throw new ApplicationException("La création de la retenue a échoué!");
		}
		_notifyService.Notify(TypeEntity.ReglementFournisseur, num2.Value, TypeAction.Ajout, societe.No);
		transactionScope.Complete();
		return num2.Value;
	}

	public void RetenueFournisseurAnnulerImputationDossier(int retenueNo)
	{
		if (retenueNo <= 0)
		{
			throw new ArgumentNullException("retenueNo");
		}
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		RetenuALaSource retenuALaSource = _retenuALaSourceRepository.Get(retenueNo);
		if (retenuALaSource == null)
		{
			throw new ApplicationException("Impossible de charger la retenue à la source.");
		}
		if (!retenuALaSource.DossierNo.HasValue)
		{
			throw new ApplicationException("La retenue n'est pas parmis les lignes du dossier de règlement.");
		}
		if ((_dossierReglementRepository.GetDossier(retenuALaSource.DossierNo.Value) ?? throw new ApplicationException("Impossible de charger le dossier de règlement.")).StatutDossier != StatutDossierReglement.Encours)
		{
			throw new ApplicationException("Le dossier n'est pas en cours de saisie.[Statut]");
		}
		if (retenuALaSource.DeclarationNo.HasValue)
		{
			throw new ApplicationException("La retenue est intégrée dans une déclaration RAS.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_retenuALaSourceRepository.UpdateDossier(retenueNo, null, string.Empty, utilisateur.No);
		if (_licenceApplicationVersion.Application == ApplicationRunning.TresoClient)
		{
			_notifyService.Notify(TypeEntity.Reglement, retenueNo, TypeAction.Modification, retenuALaSource.SocieteNo);
		}
		else
		{
			_notifyService.Notify(TypeEntity.ReglementFournisseur, retenueNo, TypeAction.Modification, retenuALaSource.SocieteNo);
		}
		transactionScope.Complete();
	}

	public IEnumerable<RetenuALaSource> RetenueFournisseurGetAllNonSolde(int tiersNo, int caisseNo, Societe societe = null)
	{
		societe = societe ?? SocieteManager.Societe;
		if (tiersNo <= 0)
		{
			throw new ArgumentNullException("tiersNo");
		}
		return _retenuALaSourceRepository.GetAllNonSolde(societe.No, tiersNo, caisseNo, ErpDomaine.Achat);
	}

	public void RetenueFournisseurImputerDossier(int retenueNo, int dossierNo)
	{
		if (retenueNo <= 0)
		{
			throw new ArgumentNullException("retenueNo");
		}
		if (dossierNo <= 0)
		{
			throw new ArgumentNullException("dossierNo");
		}
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		RetenuALaSource retenue = _retenuALaSourceRepository.Get(retenueNo);
		if (retenue == null)
		{
			throw new ApplicationException("Impossible de charger la retenue à la source.");
		}
		if (retenue.DossierNo.HasValue)
		{
			throw new ApplicationException("La retenue est inclut dans le dossier [" + retenue.DossierNumero + "].");
		}
		DossierReglement dossier = _dossierReglementRepository.GetDossier(dossierNo);
		if (dossier == null)
		{
			throw new ApplicationException("Impossible de charger le dossier de règlement.");
		}
		if (dossier.StatutDossier != StatutDossierReglement.Encours)
		{
			throw new ApplicationException("Le dossier n'est pas en cours de saisie.[Statut]");
		}
		if ((from x in SocieteManager.LigneDossierReglementCommGetAll(dossierNo)
			where x.IsRetenue
			select x).Any((LigneDossierReglementComm x) => x.EntityNo == retenue.No))
		{
			throw new ApplicationException("La retenue fait partie des lignes du dossier de règlement.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_retenuALaSourceRepository.UpdateDossier(retenueNo, dossier.No, dossier.Numero, utilisateur.No);
		_notifyService.Notify(TypeEntity.ReglementFournisseur, retenueNo, TypeAction.Modification, retenue.SocieteNo);
		transactionScope.Complete();
	}

	public RetenuALaSource RetenueGet(int no)
	{
		return _retenuALaSourceRepository.Get(no);
	}

	public RetenuALaSource RetenueGet(string numero)
	{
		Societe societe = SocieteManager.Societe;
		return _retenuALaSourceRepository.Get(numero, societe.No);
	}

	public IEnumerable<RetenuALaSource> RetenueGetAll(int tiersNo, Societe societe = null)
	{
		societe = societe ?? SocieteManager.Societe;
		return _retenuALaSourceRepository.GetAll(tiersNo, societe.No);
	}

	public void ReUtiliserChequeFrs(int chequeNo)
	{
		Cheque cheque = _chequeRepository.Get(chequeNo);
		if (cheque == null)
		{
			throw new ApplicationException("Chèque invalide!");
		}
		if (cheque.Statut != ChequeStatut.Utilise && cheque.Statut != ChequeStatut.Annuler)
		{
			throw new ApplicationException("Statut chèque invalide!");
		}
		_chequeRepository.Reutiliser(chequeNo);
		Societe societe = SocieteManager.Societe;
		_notifyService.Notify(TypeEntity.Cheques, chequeNo, TypeAction.Modification, societe.No);
	}

	public void ReUtiliserTraiteFrs(int traiteNo)
	{
		Traite traite = _traiteRepository.Get(traiteNo);
		if (traite == null)
		{
			throw new ApplicationException("Traite invalide!");
		}
		if (traite.Statut != ChequeStatut.Utilise && traite.Statut != ChequeStatut.Annuler)
		{
			throw new ApplicationException("Statut de la traite invalide!");
		}
		_traiteRepository.Reutiliser(traiteNo);
		Societe societe = SocieteManager.Societe;
		_notifyService.Notify(TypeEntity.Traites, traiteNo, TypeAction.Modification, societe.No);
		_notifyService.Notify(TypeEntity.CarnetTraite, traite.CarnetTraiteNo, TypeAction.Modification, societe.No);
	}

	public void Update(Caisse caisse)
	{
		if (caisse == null)
		{
			throw new ArgumentNullException("caisse");
		}
		if (SocieteManager.Societe.GetCaisse(caisse.No) == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorCaisseInvalid);
		}
		if (string.IsNullOrEmpty(caisse.Intitule))
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorCaisseIntitule);
		}
		_caisseRepository.Update(caisse);
	}

	public void UpdateAnnexeReglementRS(int no, int annexeType, string codeAnnexe)
	{
		_reglementFournisseurRepository.UpdateAnnexe(no, annexeType, codeAnnexe);
	}

	public void UpdateEcritures(List<EcritureComptable> ecrituresReg)
	{
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		foreach (EcritureComptable item in ecrituresReg)
		{
			_ecritureComptaRepository.UpdateEcriture(item);
		}
		transactionScope.Complete();
	}

	public void UpdateLibelleImpaye(int echeanceNo, string libelle, int societeNo)
	{
		Echeance echeance = _echeanceRepository.Get(echeanceNo);
		if (echeance == null)
		{
			throw new ApplicationException($"Impossible de charger l'échéance n°[{echeanceNo}]");
		}
		echeance.Commentaire = libelle;
		_echeanceRepository.Update(echeance);
		_notifyService.Notify(TypeEntity.Echeance, echeanceNo, TypeAction.Modification, societeNo);
		_notifyService.Notify(TypeEntity.Reglement, echeance.ReglementImpayeNo, TypeAction.Modification, societeNo);
	}

	public bool VerifyCanDecomptabiliserReglementFournisseur(int reglemmentNo, int societeNo)
	{
		return _reglementFournisseurRepository.VerifyCanDecompatabiliser(reglemmentNo, societeNo);
	}

	[Obsolete]
	public int VersementEspeceCreate(string numero, DateTime date, int deviseNo, int typeBordereauNo, int caisseNo, string pieceNumero, int banqueNo, string libelle, decimal montant, string infoLibre1, string infoLibre2, string infoLibre3, string infoLibre4, Utilisateur utilisateur = null, Societe societe = null)
	{
		return VersementEspeceCreate(date, deviseNo, typeBordereauNo, caisseNo, pieceNumero, banqueNo, libelle, montant, infoLibre1, infoLibre2, infoLibre3, infoLibre4, utilisateur, societe);
	}

	public int VersementEspeceCreate(DateTime date, int deviseNo, int typeBordereauNo, int caisseNo, string pieceNumero, int banqueNo, string libelle, decimal montant, string infoLibre1, string infoLibre2, string infoLibre3, string infoLibre4, Utilisateur utilisateur = null, Societe societe = null)
	{
		utilisateur = utilisateur ?? SocieteManager.Utilisateur;
		if (utilisateur == null)
		{
			throw new ApplicationException("Impossible de charger l'utilisateur courant");
		}
		societe = societe ?? SocieteManager.Societe;
		if (societe == null)
		{
			throw new ApplicationException("Impossible de charger la societe courante");
		}
		IEnumerable<ModeReglement> source = from x in SocieteManager.Groupe.BordereauxTypeManager.Get(typeBordereauNo).GetModeReglements()
			where x.Type == ReglementType.Espece
			select x;
		Caisse caisse = _caisseRepository.Get(caisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException("Caisse invalide!");
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Impossible de créer le versement! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisseNo, ProfilType.Grc, utilisateur, societe))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (!source.All((ModeReglement x) => x.Type == ReglementType.Espece))
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorBordereuTypeVersementInvalide);
		}
		List<int> list = (from x in caisse.GetAllModes()
			where x.Type == ReglementType.Espece
			select x.No).Intersect(source.Select((ModeReglement t) => t.No)).ToList();
		HistoriqueMvtManager historiqueManager = SocieteManager.HistoriqueMvtManager;
		decimal num = list.Sum((int x) => historiqueManager.GetSolde(caisse.No, x, deviseNo, societe));
		if (montant <= 0m || montant > num)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorMontantInavlide);
		}
		List<HistoriqueMvt> list2 = new List<HistoriqueMvt>();
		foreach (int item in list)
		{
			IEnumerable<HistoriqueMvt> allNonEpuise = _historiqueRepository.GetAllNonEpuise(caisse.No, item, deviseNo);
			list2.AddRange(allNonEpuise);
		}
		int num2 = 0;
		int num3 = BordereauCreate(date, deviseNo, typeBordereauNo, caisseNo, pieceNumero, banqueNo, libelle, infoLibre1, infoLibre2, infoLibre3, infoLibre4, utilisateur, societe);
		Bordereau bordereau = BordereauGet(num3);
		if (bordereau == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorBordereauNo);
		}
		bordereau.Montant = montant;
		bordereau.MontantDeviseSociete = montant;
		bordereau.DateModification = DateTime.Now;
		bordereau.ModificateurNo = utilisateur.No;
		_bordereauRepository.Update(bordereau);
		while (montant > 0m && num2 < list2.Count)
		{
			HistoriqueMvt historiqueMvt = list2[num2];
			Lot lot = historiqueMvt.Lot;
			decimal montantRestant = lot.MontantRestant;
			lot.MontantRestant = ((lot.MontantRestant >= montant) ? (lot.MontantRestant - montant) : 0m);
			_historiqueRepository.Update(historiqueMvt);
			HistoriqueMvt historiqueMvt2 = new HistoriqueMvt(lot.No, caisse.No, SensMouvement.Sortie, MouvementDomaine.EnteteBordereau, historiqueMvt.ModeReglementNo, lot.No, num3, StatutTransfert.None, deviseNo, new Lot(lot.No, (montantRestant >= montant) ? montant : montantRestant, 0m));
			_historiqueRepository.Create(historiqueMvt2);
			montant -= montantRestant;
			num2++;
		}
		return num3;
	}

	[Obsolete]
	public int VersementEspeceCreate(string numero, DateTime date, int deviseNo, int typeBordereauNo, int caisseNo, string pieceNumero, int banqueNo, string libelle, string infoLibre1, string infoLibre2, string infoLibre3, string infoLibre4, Dictionary<int, decimal> lots)
	{
		return VersementEspeceCreate(date, deviseNo, typeBordereauNo, caisseNo, pieceNumero, banqueNo, libelle, infoLibre1, infoLibre2, infoLibre3, infoLibre4, lots);
	}

	public int VersementEspeceCreate(DateTime date, int deviseNo, int typeBordereauNo, int caisseNo, string pieceNumero, int banqueNo, string libelle, string infoLibre1, string infoLibre2, string infoLibre3, string infoLibre4, Dictionary<int, decimal> lots)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		Caisse caisse = _caisseRepository.Get(caisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException("Caisse invalide!");
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Impossible de crée le versement! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisseNo))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		InformationsBanque byBanqueId = SocieteManager.Groupe.InformationBanqueManager.GetByBanqueId(banqueNo);
		if (byBanqueId != null && byBanqueId.EnSommeil)
		{
			throw new ApplicationException("Impossible de crée le versement! La banque est en sommeil!");
		}
		if (lots.Any((KeyValuePair<int, decimal> x) => x.Key <= 0 || x.Value <= 0m))
		{
			throw new InvalidOperationException("Montant lot invalide!");
		}
		List<HistoriqueMvt> list = new List<HistoriqueMvt>();
		foreach (int key in lots.Keys)
		{
			IEnumerable<HistoriqueMvt> allByMouvement = _historiqueRepository.GetAllByMouvement(key);
			list.AddRange(allByMouvement);
		}
		int num = BordereauCreate(date, deviseNo, typeBordereauNo, caisseNo, pieceNumero, banqueNo, libelle, infoLibre1, infoLibre2, infoLibre3, infoLibre4);
		Bordereau bordereau = BordereauGet(num);
		if (bordereau == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorBordereauNo);
		}
		SocieteDevise devise = SocieteManager.Societe.GetDevise(deviseNo);
		if (devise == null)
		{
			throw new ApplicationException($"Impossible de charger la devise [{deviseNo}].");
		}
		decimal num2 = lots.Sum((KeyValuePair<int, decimal> x) => x.Value);
		decimal montantDeviseSociete = Math.Round(num2 * devise.Cours, devise.NombreDecimales, MidpointRounding.AwayFromZero);
		bordereau.Montant = num2;
		bordereau.MontantDeviseSociete = montantDeviseSociete;
		bordereau.DateModification = DateTime.Now;
		bordereau.ModificateurNo = utilisateur.No;
		_bordereauRepository.Update(bordereau);
		foreach (HistoriqueMvt item in list)
		{
			if (item.Sens != SensMouvement.Sortie)
			{
				Lot lot = item.Lot;
				decimal value = lots.Single((KeyValuePair<int, decimal> x) => x.Key == lot.No).Value;
				lot.MontantRestant -= value;
				_historiqueRepository.Update(item);
				HistoriqueMvt historiqueMvt = new HistoriqueMvt(lot.No, caisseNo, SensMouvement.Sortie, MouvementDomaine.EnteteBordereau, item.ModeReglementNo, lot.No, num, StatutTransfert.None, deviseNo, new Lot(lot.No, value, 0m));
				_historiqueRepository.Create(historiqueMvt);
			}
		}
		return bordereau.No;
	}

	public void VersementEspeceDelete(int bordereauNo, int caisseNo)
	{
		Bordereau bordereau = _bordereauRepository.Get(bordereauNo);
		if (bordereau == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorBordereauNo);
		}
		Caisse caisse = _caisseRepository.Get(caisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException("Caisse invalide!");
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Impossible de supprimer le versement! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisseNo, ProfilType.Grc))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (bordereau.IsRemis)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.InfoBordereauRemis);
		}
		if (bordereau.IsPointe)
		{
			throw new ApplicationException(TresorerieCoreMessages.InfoBordereauRapproche);
		}
		if (bordereau.GetLigneBordereaux().Any())
		{
			throw new ApplicationException(TresorerieCoreMessages.InfoBordoreauInformations);
		}
		List<HistoriqueMvt> list = (from x in _historiqueRepository.GetAllByCaisse(caisseNo, bordereau.DeviseNo)
			where x.MouvementOutNo == bordereauNo
			select x).ToList();
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		foreach (HistoriqueMvt item in list)
		{
			HistoriqueMvt historiqueMvt = _historiqueRepository.GetAllByMouvement(item.MouvementInNo).Single((HistoriqueMvt x) => x.Sens == SensMouvement.Entree);
			if (historiqueMvt == null)
			{
				throw new ArgumentNullException("Historique d'entree invalide!");
			}
			historiqueMvt.Lot.MontantRestant += item.Montant;
			_historiqueRepository.Update(historiqueMvt);
			_historiqueRepository.Delete(item);
		}
		_bordereauRepository.Delete(bordereau);
		_notifyService.Notify(TypeEntity.Bordereau, bordereauNo, TypeAction.Suppression, bordereau.SocieteNo);
		transactionScope.Complete();
	}

	public void VirementInterneUpdate(int virementInterneNo, string libelle)
	{
		VirementInterne virementInterne = VirementInterneGet(virementInterneNo);
		if (virementInterne == null)
		{
			throw new ApplicationException($"Impossible de charger le virement interne [{virementInterneNo}].");
		}
		if (string.IsNullOrEmpty(libelle))
		{
			throw new ApplicationException("Libellé est obligatoire!");
		}
		if (virementInterne.IsComptabilise == EtatComptabilite.Comptabilise)
		{
			throw new ApplicationException("Le virement interne [" + virementInterne.Numero + "] est comptabilisé!");
		}
		virementInterne.Libelle = libelle;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_virementInterneRepository.Update(virementInterne);
		_notifyService.Notify(TypeEntity.VirementInterne, virementInterneNo, TypeAction.Modification, virementInterne.SocieteNo);
		transactionScope.Complete();
	}

	public int VirementInterneCreate(string numero, DateTime date, decimal montant, int deviseInNo, int deviseOutNo, decimal deviseCours, string pieceNumero, int modeReglementNo, int caisseNo, string libelle, int compteInNo, int compteOutNo, int? chequeNo)
	{
		Societe societe = SocieteManager.Societe;
		VirementInterne virementInterne = _virementInterneRepository.GetVirementInterne(numero, societe.No);
		if (string.IsNullOrEmpty(numero))
		{
			throw new InvalidOperationException("Numéro invalide!");
		}
		if (virementInterne != null)
		{
			throw new InvalidOperationException("Numéro virement déjà existe!");
		}
		if (string.IsNullOrEmpty(pieceNumero))
		{
			throw new InvalidOperationException("Numéro pièce est obligatoire!");
		}
		CaisseModeReglement mode = (societe.GetCaisse(caisseNo) ?? throw new InvalidOperationException(rcRessources.CaisseInvalide)).GetMode(modeReglementNo);
		if (mode == null)
		{
			throw new InvalidOperationException("Mode règlement invalide!");
		}
		if (mode.EnSommeil)
		{
			throw new InvalidOperationException("Opération invalide! Le mode est en sommeil");
		}
		if (mode.Type != ReglementType.Virement && mode.Type != ReglementType.Cheque)
		{
			throw new InvalidOperationException("Mode règlement n'est pas de type virement/chèque!");
		}
		InformationsBanque byBanqueId = SocieteManager.Groupe.InformationBanqueManager.GetByBanqueId(compteInNo);
		if (byBanqueId != null && byBanqueId.EnSommeil)
		{
			throw new InvalidOperationException("La banque destination est en sommeil");
		}
		InformationsBanque byBanqueId2 = SocieteManager.Groupe.InformationBanqueManager.GetByBanqueId(compteOutNo);
		if (byBanqueId2 != null && byBanqueId2.EnSommeil)
		{
			throw new InvalidOperationException("La banque source est en sommeil");
		}
		if (compteInNo == compteOutNo)
		{
			throw new InvalidOperationException("La banque source doit être différente de la banque destinataire!");
		}
		SocieteDevise defaultDeviseSociete = societe.GetDefaultDeviseSociete();
		if (defaultDeviseSociete == null)
		{
			throw new ApplicationException("Impossible de charger la devise société.");
		}
		VirementInterne virementInterne2 = new VirementInterne
		{
			Date = date,
			CaisseNo = caisseNo,
			CompteInNo = compteInNo,
			CompteOutNo = compteOutNo,
			DateCreation = DateTime.Now,
			DatePointage = date,
			DeviseCours = deviseCours,
			DeviseInNo = deviseInNo,
			DeviseOutNo = deviseOutNo,
			IsComptabilise = EtatComptabilite.NonComptabilise,
			IsPointer = false,
			Libelle = libelle,
			ModeReglementNo = modeReglementNo,
			Montant = montant,
			MontantDeviseSociete = Math.Round(montant * deviseCours, defaultDeviseSociete.NombreDecimales, MidpointRounding.AwayFromZero),
			Numero = numero,
			PieceNumero = pieceNumero,
			SocieteNo = societe.No,
			ChequeNo = chequeNo,
			UtilisateurNo = SocieteManager.Utilisateur.No
		};
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		if (chequeNo.HasValue)
		{
			int valueOrDefault = chequeNo.GetValueOrDefault();
			Cheque cheque = _chequeRepository.Get(valueOrDefault);
			if (cheque == null)
			{
				throw new InvalidOperationException("Chèque invalide!");
			}
			if (cheque.Statut != ChequeStatut.NonUtilise)
			{
				throw new InvalidOperationException("Chèque est déjà utilisé");
			}
			cheque.Echeance = virementInterne2.Date;
			cheque.Date = DateTime.Now;
			cheque.Statut = ChequeStatut.Utilise;
			cheque.Montant = montant;
			cheque.Tire = societe.RaisonSociale;
			virementInterne2.PieceNumero = cheque.Numero;
			_chequeRepository.Update(cheque);
		}
		int num = _virementInterneRepository.Create(virementInterne2);
		_notifyService.Notify(TypeEntity.VirementInterne, num, TypeAction.Ajout, virementInterne2.SocieteNo);
		transactionScope.Complete();
		return num;
	}

	public void VirementInterneDeComptabiliser(int mvtNo)
	{
		VirementInterne virementInterne = VirementInterneGet(mvtNo);
		if (virementInterne == null)
		{
			throw new InvalidOperationException($"Impossible de charger le virement interne [{mvtNo}].");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		if (virementInterne.IsComptabilise == EtatComptabilite.NonComptabilise)
		{
			throw new ApplicationException("Le virement interne [" + virementInterne.Numero + "] n'est pas comptabilisé!");
		}
		_ecritureComptaRepository.DeleteLigneVirementInterne(virementInterne.No);
		_virementInterneRepository.Decomptabiliser(mvtNo);
		_notifyService.Notify(TypeEntity.VirementInterne, mvtNo, TypeAction.Modification, virementInterne.SocieteNo);
		transactionScope.Complete();
	}

	public void VirementInterneDelete(int virementNo, bool reutiliser)
	{
		VirementInterne virementInterne = _virementInterneRepository.GetVirementInterne(virementNo);
		if (virementInterne == null)
		{
			throw new InvalidOperationException("Virement invalide!");
		}
		if (virementInterne.IsPointer)
		{
			throw new InvalidOperationException("Opération invalide ! Virement interne est rapproché !");
		}
		if (virementInterne.IsComptabilise == EtatComptabilite.Comptabilise)
		{
			throw new InvalidOperationException("Opération invalide ! Virement interne est comptabilisé !");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		if (virementInterne.ChequeNo.HasValue)
		{
			int valueOrDefault = virementInterne.ChequeNo.GetValueOrDefault();
			if (reutiliser)
			{
				ReUtiliserChequeFrs(valueOrDefault);
			}
			else
			{
				string motifAnnulation = "Suppression virement interne n°[" + virementInterne.Numero + "]";
				AnnulerChequeFrs(valueOrDefault, motifAnnulation, isUsed: true);
			}
		}
		_virementInterneRepository.Delete(virementNo);
		_notifyService.Notify(TypeEntity.VirementInterne, virementNo, TypeAction.Suppression, virementInterne.SocieteNo);
		transactionScope.Complete();
	}

	public VirementInterne VirementInterneGet(string numero)
	{
		return _virementInterneRepository.GetVirementInterne(numero, SocieteManager.Societe.No);
	}

	public VirementInterne VirementInterneGet(int no)
	{
		return _virementInterneRepository.GetVirementInterne(no);
	}

	public List<VirementInterne> VirementInterneGetAll()
	{
		Societe societe = SocieteManager.Societe;
		return _virementInterneRepository.GetAll(societe.No).ToList();
	}

	public Task<IEnumerable<VirementInterne>> GetAllVirementInterneAComptaAsync(DateTime dateDe, DateTime dateA, EtatComptabilite etat, CancellationToken cancellationToken)
	{
		Societe societe = SocieteManager.Societe;
		return _virementInterneRepository.GetAllAComptaAsync(dateDe.Date, dateA.Date.AddDays(1.0), etat, societe.No, cancellationToken);
	}

	public void VirementTiersAddLigne(int virementNo, string numero, int tiersNo, string tiersCode, string tiersIntitule, decimal montant, string banqueClient, string rib, string libelle, string reference, string infoLibre1 = "")
	{
		VirementTiers virementTiers = VirementTiersGet(virementNo);
		if (virementTiers == null)
		{
			throw new ArgumentNullException("virement tiers invalide");
		}
		if (virementTiers.IsPointer)
		{
			throw new ApplicationException("Le virement est rapproché!");
		}
		if (virementTiers.IsComptabilise != EtatComptabilite.NonComptabilise)
		{
			throw new ApplicationException("Le virement est comptabilisé!");
		}
		Societe societe = SocieteManager.Societe;
		SocieteDevise defaultDeviseSociete = societe.GetDefaultDeviseSociete();
		decimal num = Math.Round(montant * virementTiers.Cours, defaultDeviseSociete.NombreDecimales, MidpointRounding.AwayFromZero);
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		if (virementTiers.TypeTiers == TiersType.Client)
		{
			if (!societe.VirementDefaultSouche.HasValue)
			{
				throw new InvalidOperationException("Souche invalide");
			}
			Echeance echeance = new Echeance(0, numero.ToUpper(), ErpDomaine.Vente, ErpDocumentType.None, virementTiers.Date, montant, montant, virementTiers.Date, EcheanceType.VirementTiers, virementTiers.ModeReglementNo, virementTiers.SocieteNo, tiersNo, tiersCode, tiersIntitule, tiersNo, tiersCode, tiersIntitule, virementTiers.DeviseNo, societe.VirementDefaultSouche.Value, 0, virementTiers.Cours, libelle, num, num)
			{
				VirementTiersNo = virementTiers.No,
				VirementTiersNumero = virementTiers.Numero,
				Reference = reference,
				Rib = rib,
				BanqueClient = banqueClient,
				BanqueNo = virementTiers.CompteNo,
				CaisseNo = virementTiers.CaisseNo,
				DatePointeRemboursementCheque = DateHelper.GetMinSqlDateTime(),
				EcheanceReporte = virementTiers.Date,
				Info1 = infoLibre1
			};
			int? num2 = _echeanceRepository.Create(echeance);
			if (!num2.HasValue)
			{
				throw new ApplicationException("Echeance");
			}
			_notifyService.Notify(TypeEntity.Echeance, num2.Value, TypeAction.Ajout, virementTiers.SocieteNo);
			_notifyService.Notify(TypeEntity.RemboursementClient, num2.Value, TypeAction.Ajout, virementTiers.SocieteNo);
			_notifyService.Notify(TypeEntity.VirementTiers, virementNo, TypeAction.Modification, virementTiers.SocieteNo);
		}
		else
		{
			if (_reglementFournisseurRepository.Get(virementTiers.SocieteNo, numero) != null)
			{
				throw new ApplicationException("Le numéro de règlement existe déjà!");
			}
			ReglementFournisseur reglementFournisseur = new ReglementFournisseur(numero, tiersNo, tiersCode, tiersIntitule, virementTiers.TypeTiers, virementTiers.CaisseNo, virementTiers.SocieteNo, virementTiers.DeviseNo, virementTiers.ModeReglementNo, ReglementType.Virement, virementTiers.Date, montant, virementTiers.Date)
			{
				Rib = rib,
				VirementTiersNo = virementTiers.No,
				VirementTiersNumero = virementTiers.Numero,
				DateCreation = DateTime.Now,
				DateModification = DateTime.Now,
				Libelle = libelle,
				BanqueNo = virementTiers.CompteNo,
				PieceNumero = virementTiers.PieceNumero,
				UserNo = SocieteManager.Utilisateur.No,
				DeviseCours = virementTiers.Cours,
				BanqueTier = banqueClient,
				Beneficiaire = tiersIntitule,
				EnteteBordereauNumero = string.Empty
			};
			if (virementTiers.TypeTiers == TiersType.Salarie || virementTiers.TypeTiers == TiersType.Autre)
			{
				reglementFournisseur.Solde = 0m;
				reglementFournisseur.SoldeDevise = 0m;
			}
			int? num3 = _reglementFournisseurRepository.Create(reglementFournisseur);
			if (num3.HasValue)
			{
				_notifyService.Notify(TypeEntity.ReglementFournisseur, num3.Value, TypeAction.Ajout, reglementFournisseur.SocieteNo);
			}
		}
		virementTiers.Montant += montant;
		virementTiers.MontantDeviseSociete += num;
		_virementTiersRepository.Update(virementTiers.No, virementTiers.Libelle, virementTiers.PieceNumero, virementTiers.Montant, virementTiers.MontantDeviseSociete);
		_notifyService.Notify(TypeEntity.VirementTiers, virementNo, TypeAction.Modification, virementTiers.SocieteNo);
		transactionScope.Complete();
	}

	public int VirementTiersCreate(string numero, TiersType typeTiers, DateTime date, int deviseNo, decimal deviseCours, string pieceNumero, int modeReglementNo, int caisseNo, string libelle, int compteNo)
	{
		Societe societe = SocieteManager.Societe;
		VirementTiers virement = _virementTiersRepository.GetVirement(numero, societe.No);
		if (string.IsNullOrEmpty(numero))
		{
			throw new InvalidOperationException("Numéro invalide!");
		}
		if (virement != null)
		{
			throw new InvalidOperationException("Numéro virement déjà existe!");
		}
		CaisseModeReglement obj = (societe.GetCaisse(caisseNo) ?? throw new InvalidOperationException(rcRessources.CaisseInvalide)).GetMode(modeReglementNo) ?? throw new InvalidOperationException("Mode règlement invalide!");
		if (obj.EnSommeil)
		{
			throw new InvalidOperationException("Opération invalide! Le mode est en sommeil!");
		}
		if (obj.Type != ReglementType.Virement)
		{
			throw new InvalidOperationException("Mode règlement n'est pas de type virement!");
		}
		InformationsBanque byBanqueId = SocieteManager.Groupe.InformationBanqueManager.GetByBanqueId(compteNo);
		if (byBanqueId != null && byBanqueId.EnSommeil)
		{
			throw new InvalidOperationException("Impossible de créer le virement! La banque est en sommeil!");
		}
		VirementTiers virementTiers = new VirementTiers
		{
			TypeTiers = typeTiers,
			Date = date,
			CaisseNo = caisseNo,
			CompteNo = compteNo,
			DateCreation = DateTime.Now,
			DatePointage = date,
			Cours = deviseCours,
			DeviseNo = deviseNo,
			IsComptabilise = EtatComptabilite.NonComptabilise,
			IsPointer = false,
			Libelle = libelle,
			ModeReglementNo = modeReglementNo,
			Numero = numero,
			PieceNumero = pieceNumero,
			SocieteNo = societe.No,
			UtilisateurNo = SocieteManager.Utilisateur.No
		};
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		int num = _virementTiersRepository.Create(virementTiers);
		_notifyService.Notify(TypeEntity.VirementTiers, num, TypeAction.Ajout, virementTiers.SocieteNo);
		transactionScope.Complete();
		return num;
	}

	public void VirementTiersDeComptabiliser(int mvtNo)
	{
		VirementTiers virementTiers = VirementTiersGet(mvtNo);
		if (virementTiers == null)
		{
			throw new InvalidOperationException($"Impossible de charger le virement en masse [{mvtNo}].");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		if (virementTiers.IsComptabilise == EtatComptabilite.NonComptabilise)
		{
			throw new ApplicationException("Le virement en masse [" + virementTiers.Numero + "] n'est pas comptabilisé!");
		}
		_ecritureComptaRepository.DeleteLigneVirementTiers(virementTiers.No);
		if (virementTiers.TypeTiers == TiersType.Client)
		{
			foreach (LigneVirementTiers ligne in virementTiers.GetLignes())
			{
				_ligneVirementTiersRepository.VirClientUpdateEtatCompta(ligne.No, EtatComptabilite.NonComptabilise);
				_notifyService.Notify(TypeEntity.RemboursementClient, ligne.No, TypeAction.Modification, virementTiers.SocieteNo);
			}
		}
		else
		{
			foreach (LigneVirementTiers ligne2 in virementTiers.GetLignes())
			{
				_reglementFournisseurRepository.UpdateEtatCompta(ligne2.No, EtatComptabilite.NonComptabilise, utilisateur.No);
				_notifyService.Notify(TypeEntity.ReglementFournisseur, ligne2.No, TypeAction.Modification, virementTiers.SocieteNo);
			}
		}
		_virementTiersRepository.Decomptabiliser(mvtNo);
		_notifyService.Notify(TypeEntity.VirementTiers, mvtNo, TypeAction.Modification, virementTiers.SocieteNo);
		transactionScope.Complete();
	}

	public void VirementTiersDelete(int virementNo)
	{
		VirementTiers virement = _virementTiersRepository.GetVirement(virementNo);
		if (virement == null)
		{
			throw new InvalidOperationException("Virement invalide!");
		}
		if (virement.GetLignes().Any())
		{
			throw new InvalidOperationException("Opération invalide! Virement tiers contient des lignes.");
		}
		if (virement.IsPointer)
		{
			throw new InvalidOperationException("Opération invalide ! Virement tiers est rapproché !");
		}
		if (virement.IsComptabilise == EtatComptabilite.Comptabilise)
		{
			throw new InvalidOperationException("Opération invalide ! Virement tiers est comptabilisé !");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_virementTiersRepository.Delete(virementNo);
		_notifyService.Notify(TypeEntity.VirementTiers, virementNo, TypeAction.Suppression, virement.SocieteNo);
		transactionScope.Complete();
	}

	public void VirementTiersDeleteLigne(int virementNo, int ligneNo)
	{
		VirementTiers virementTiers = VirementTiersGet(virementNo);
		if (virementTiers == null)
		{
			throw new ApplicationException($"Virement tiers invalide [{virementNo}]");
		}
		LigneVirementTiers ligne = virementTiers.GetLigne(ligneNo);
		if (ligne == null)
		{
			throw new ApplicationException($"Ligne virement invalide [{ligneNo}]");
		}
		SocieteDevise defaultDeviseSociete = SocieteManager.Societe.GetDefaultDeviseSociete();
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		if (virementTiers.TypeTiers == TiersType.Client)
		{
			Echeance echeance = SocieteManager.EcheanceGet(ligneNo);
			if (echeance == null)
			{
				throw new ArgumentNullException("echeance");
			}
			if (echeance.IsComptaEcart)
			{
				throw new InvalidOperationException("Virement client est comtabilisé!");
			}
			if (echeance.GetAffectations().Any())
			{
				throw new InvalidOperationException("Virement est partiellement payé!");
			}
			_echeanceRepository.Delete(echeance);
			_notifyService.Notify(TypeEntity.Echeance, echeance.No, TypeAction.Suppression, virementTiers.SocieteNo);
			_notifyService.Notify(TypeEntity.RemboursementClient, echeance.No, TypeAction.Suppression, virementTiers.SocieteNo);
			_notifyService.Notify(TypeEntity.VirementTiers, virementNo, TypeAction.Modification, virementTiers.SocieteNo);
		}
		else
		{
			ReglementFournisseur reglementFournisseur = _reglementFournisseurRepository.Get(ligneNo);
			if (reglementFournisseur == null)
			{
				throw new ArgumentNullException("reglement");
			}
			if (reglementFournisseur.IsComptabilise == EtatComptabilite.Comptabilise)
			{
				throw new InvalidOperationException("Règlement fournisseur est comptabilisé!");
			}
			if (reglementFournisseur.IsPointe)
			{
				throw new InvalidOperationException("Règlement fournisseur est rapproché");
			}
			if (reglementFournisseur.IsAnnule)
			{
				throw new ApplicationException("Règlement fournisseur est annulé.");
			}
			_reglementFournisseurRepository.Delete(reglementFournisseur);
			_notifyService.Notify(TypeEntity.ReglementFournisseur, reglementFournisseur.No, TypeAction.Suppression, reglementFournisseur.SocieteNo);
			_notifyService.Notify(TypeEntity.VirementTiers, virementNo, TypeAction.Modification, virementTiers.SocieteNo);
		}
		virementTiers.Montant -= ligne.Montant;
		virementTiers.MontantDeviseSociete = Math.Round(virementTiers.Montant * virementTiers.Cours, defaultDeviseSociete.NombreDecimales, MidpointRounding.AwayFromZero);
		_virementTiersRepository.Update(virementTiers.No, virementTiers.Libelle, virementTiers.PieceNumero, virementTiers.Montant, virementTiers.MontantDeviseSociete);
		transactionScope.Complete();
	}

	public VirementTiers VirementTiersGet(string numero)
	{
		return _virementTiersRepository.GetVirement(numero, SocieteManager.Societe.No);
	}

	public VirementTiers VirementTiersGet(int no)
	{
		return _virementTiersRepository.GetVirement(no);
	}

	public List<VirementTiers> VirementTiersGetAll()
	{
		Societe societe = SocieteManager.Societe;
		return _virementTiersRepository.GetAll(societe.No).ToList();
	}

	public List<VirementTiers> VirementTiersGetAll(DateTime dateMin, DateTime dateMax)
	{
		Societe societe = SocieteManager.Societe;
		return _virementTiersRepository.GetAll(societe.No, dateMin, dateMax).ToList();
	}

	public Task<IEnumerable<VirementTiers>> GetAllVirementTiersAComptaAsync(DateTime dateDe, DateTime dateA, EtatComptabilite etat, CancellationToken cancellationToken)
	{
		Societe societe = SocieteManager.Societe;
		return _virementTiersRepository.GetAllAComptaAsync(dateDe.Date, dateA.Date.AddDays(1.0), etat, societe.No, cancellationToken);
	}

	public IEnumerable<VirementTiers> VirementTiersGetAllByLibelle(int societeNo, string exprSearch)
	{
		if (societeNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorSocieteNo);
		}
		if (string.IsNullOrEmpty(exprSearch))
		{
			throw new ArgumentNullException("exprSearch");
		}
		return _virementTiersRepository.GetAllByLibelle(societeNo, exprSearch);
	}

	public void VirementTiersUpdate(int no, string libelle, string piece)
	{
		if (no <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorMvtNo);
		}
		VirementTiers virementTiers = VirementTiersGet(no);
		if (virementTiers == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorVirementInterneMvtInvalid);
		}
		_virementTiersRepository.Update(no, libelle, piece, virementTiers.Montant, virementTiers.MontantDeviseSociete);
	}

	private void VerifierReglement(ReglementClient reglement, ReglementType type)
	{
		if (reglement == null)
		{
			throw new ArgumentNullException("reglement");
		}
		if (string.IsNullOrEmpty(reglement.Numero))
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorNumeroInvalide);
		}
		if (reglement.ModeReglementNo <= 0)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorNumeroInvalide);
		}
		switch (type)
		{
		case ReglementType.Cheque:
		case ReglementType.Traite:
			if (string.IsNullOrEmpty(reglement.BanqueTier))
			{
				throw new ApplicationException(TresorerieCoreMessages.ErrorBanqueClientInvalide);
			}
			if (string.IsNullOrEmpty(reglement.PieceNumero))
			{
				throw new ApplicationException(TresorerieCoreMessages.ErrorNumeroPieceInvalide);
			}
			break;
		case ReglementType.Virement:
			if (string.IsNullOrEmpty(reglement.BanqueTier))
			{
				throw new ApplicationException(TresorerieCoreMessages.ErrorBanqueClientInvalide);
			}
			if (!reglement.BanqueNo.HasValue || reglement.BanqueNo <= 0)
			{
				throw new ApplicationException(TresorerieCoreMessages.ErrorBanqueInvalide);
			}
			break;
		}
		if (SocieteManager.Societe.LegislationType == Legislation.Tunisie && type == ReglementType.Cheque)
		{
			if (!reglement.MontantPlafond.HasValue)
			{
				throw new ApplicationException("Le montant du plafond obligatoire.");
			}
			decimal montant = reglement.Montant;
			decimal? montantPlafond = reglement.MontantPlafond;
			if (((montant > montantPlafond.GetValueOrDefault()) & montantPlafond.HasValue) && !reglement.IsCertifier)
			{
				throw new ApplicationException("Le montant ne doit pas dépasser le plafond.");
			}
			if (!reglement.DateValiditer.HasValue)
			{
				throw new ApplicationException("La date de validité obligatoire.");
			}
			if (reglement.DateEcheance.Date > reglement.DateValiditer.Value.Date)
			{
				throw new ApplicationException("La date d'échéance ne doit pas dépasser la date de validité.");
			}
		}
	}

	public void SetAffectationSynchronizer(int affectationNo, int eprNo, bool isSynchronizer)
	{
		Affectation affectation = _affectationRepository.Get(affectationNo) ?? throw new ApplicationException("Impossible de charger l'affectation.");
		Log.Information($"Set l'affectation AF_id:[{affectationNo}] cbMarq:[{eprNo}] Synchroniser :[{isSynchronizer}]");
		_affectationRepository.SetSynchronized(affectation, eprNo, isSynchronizer);
	}

	public void AffectationSetDeclarationTva(int affectationNo, int declarationTvaNo)
	{
		Affectation affectation = _affectationRepository.Get(affectationNo);
		if (affectation == null)
		{
			throw new ApplicationException("Impossible de charger l'affectation.");
		}
		ReglementClient reglementClient = _reglementClientRepository.Get(affectation.ReglementNo);
		if (reglementClient == null)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorReglementNo);
		}
		Echeance echeance = affectation.GetEcheance();
		if (echeance == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorEcheanceNo);
		}
		if (affectation.DeclarationTvaEncaissementNo.HasValue)
		{
			throw new ApplicationException("L'affectation [" + reglementClient.Numero + "/" + echeance.DocumentNumero + "] est déja associée à une déclaration.");
		}
		DeclarationTvaEncaissement declarationTvaEncaissement = SocieteManager.DeclarationTvaEncaissementGet(declarationTvaNo);
		if (declarationTvaEncaissement == null)
		{
			throw new ApplicationException("Impossible de charger la déclaration.");
		}
		if (declarationTvaEncaissement.Statut != StatutDeclaration.EnCours)
		{
			throw new ApplicationException("La déclaration [" + declarationTvaEncaissement.Numero + "] est clôturée.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		affectation.DeclarationTvaEncaissementNo = declarationTvaNo;
		_affectationRepository.SetDeclarationTva(affectation);
		_notifyService.Notify(TypeEntity.Reglement, reglementClient.No, TypeAction.Modification, reglementClient.SocieteNo);
		_notifyService.Notify(TypeEntity.Echeance, echeance.No, TypeAction.Modification, reglementClient.SocieteNo);
		transactionScope.Complete();
	}

	public void AffectationAnnulerDeclarationTva(int affectationNo)
	{
		Affectation affectation = _affectationRepository.Get(affectationNo);
		if (affectation == null)
		{
			throw new ApplicationException("Impossible de charger l'affectation.");
		}
		ReglementClient reglementClient = _reglementClientRepository.Get(affectation.ReglementNo);
		if (reglementClient == null)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorReglementNo);
		}
		Echeance echeance = affectation.GetEcheance();
		if (echeance == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorEcheanceNo);
		}
		if (!affectation.DeclarationTvaEncaissementNo.HasValue)
		{
			throw new ApplicationException("L'affectation [" + reglementClient.Numero + "/" + echeance.DocumentNumero + "] n'est pas associée à une déclaration.");
		}
		DeclarationTvaEncaissement declarationTvaEncaissement = SocieteManager.DeclarationTvaEncaissementGet(affectation.DeclarationTvaEncaissementNo.Value);
		if (declarationTvaEncaissement == null)
		{
			throw new ApplicationException("Impossible de charger la déclaration.");
		}
		if (declarationTvaEncaissement.Statut != StatutDeclaration.EnCours)
		{
			throw new ApplicationException("La déclaration [" + declarationTvaEncaissement.Numero + "] est clôturée.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		affectation.DeclarationTvaEncaissementNo = null;
		_affectationRepository.SetDeclarationTva(affectation);
		_notifyService.Notify(TypeEntity.Reglement, reglementClient.No, TypeAction.Modification, reglementClient.SocieteNo);
		_notifyService.Notify(TypeEntity.Echeance, echeance.No, TypeAction.Modification, reglementClient.SocieteNo);
		transactionScope.Complete();
	}

	private void AffectationUpdateInternal(int reglementNo, int affectationNo, decimal montant, int deviseSocieteNo)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		if (montant <= 0m)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorMontantNegatif);
		}
		ReglementClient reglementClient = _reglementClientRepository.Get(reglementNo);
		if (reglementClient == null)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorReglementNo);
		}
		Caisse caisse = Get(reglementClient.CaisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Impossible de modifier l'imputation! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grc))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (reglementClient.IsAnnule)
		{
			throw new ApplicationException("Le règlement est annulé!");
		}
		Affectation affectation = reglementClient.GetAffectations().SingleOrDefault((Affectation x) => x.No == affectationNo);
		if (affectation == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorOperationInvalide);
		}
		if (affectation.IsSynchro)
		{
			throw new ApplicationException("L'affectation est synchronisée");
		}
		Echeance echeance = affectation.GetEcheance();
		if (echeance == null)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorEcheanceNo);
		}
		if (echeance.Montant < 0m)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorEcheanceNegative);
		}
		if (!SocieteManager.UserHasAutorisationSouche(echeance.Domaine, echeance.SoucheNo))
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorAutorisationSouche);
		}
		if (reglementClient.DeviseNo != echeance.DeviseNo)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorDeviseEcheanceReglement);
		}
		decimal solde = echeance.Solde;
		decimal soldeDeviseSociete = echeance.SoldeDeviseSociete;
		if (solde + affectation.Montant < montant)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorMontant);
		}
		if (soldeDeviseSociete + affectation.MontantDeviseSociete < montant * echeance.CoursDevise)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorMontant);
		}
		decimal newMontantDeviseSocieteReglement = montant * reglementClient.DeviseCours;
		decimal num = montant * echeance.CoursDevise;
		reglementClient.Solde += affectation.Montant - montant;
		reglementClient.SoldeDeviseSociete += affectation.MontantDeviseSociete - num;
		reglementClient.DateModification = DateTime.Now;
		reglementClient.ModificateurNo = utilisateur.No;
		if (reglementClient.Solde < 0m)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorMontant);
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		RecalculerEcartChange(reglementClient, echeance, deviseSocieteNo, newMontantDeviseSocieteReglement, num);
		if (echeance.Type == EcheanceType.Solde || echeance.Type == EcheanceType.Erp || echeance.Type == EcheanceType.Impaye || echeance.Type == EcheanceType.FactureGR)
		{
			affectation.NombreJourReglement = (int)Math.Round((reglementClient.DateEcheance.Date - echeance.DocumentDate.Date).TotalDays, MidpointRounding.AwayFromZero);
			affectation.DelaisMoyenPayement = (int)Math.Round((decimal)affectation.NombreJourReglement * montant / echeance.Montant, MidpointRounding.AwayFromZero);
		}
		_affectationRepository.Update(affectation, montant, num);
		echeance.Solde += affectation.Montant - montant;
		echeance.SoldeDeviseSociete += affectation.MontantDeviseSociete - num;
		echeance.DelaisMoyenPayement = EcheanceCalculerDelaisMoyenPayement(echeance.No);
		_echeanceRepository.Update(echeance);
		_reglementClientRepository.Update(reglementClient);
		_notifyService.Notify(TypeEntity.Reglement, reglementNo, TypeAction.Modification, reglementClient.SocieteNo);
		_notifyService.Notify(TypeEntity.Echeance, echeance.No, TypeAction.Modification, reglementClient.SocieteNo);
		transactionScope.Complete();
	}

	private void AffectationUpdateFournisseurInternal(int reglementNo, int affectationNo, decimal montant, int deviseSocieteNo)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		if (montant <= 0m)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorMontantNegatif);
		}
		ReglementFournisseur reglementFournisseur = _reglementFournisseurRepository.Get(reglementNo);
		if (reglementFournisseur == null)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorReglementNo);
		}
		Caisse caisse = Get(reglementFournisseur.CaisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Impossible de modifier l'imputation! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grf))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (reglementFournisseur.IsAnnule)
		{
			throw new ApplicationException("Le règlement est annulé!");
		}
		Affectation affectation = reglementFournisseur.GetAffectations().SingleOrDefault((Affectation x) => x.No == affectationNo);
		if (affectation == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorOperationInvalide);
		}
		if (affectation.IsSynchro)
		{
			throw new ApplicationException("L'affectation est synchronisée");
		}
		Echeance echeance = affectation.GetEcheance();
		if (echeance == null)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorEcheanceNo);
		}
		if (echeance.Montant < 0m)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorEcheanceNegative);
		}
		if (!SocieteManager.UserHasAutorisationSouche(echeance.Domaine, echeance.SoucheNo))
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorAutorisationSouche);
		}
		if (reglementFournisseur.DeviseNo != echeance.DeviseNo)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorDeviseEcheanceReglement);
		}
		decimal solde = echeance.Solde;
		decimal soldeDeviseSociete = echeance.SoldeDeviseSociete;
		if (solde + affectation.Montant < montant)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorMontant);
		}
		if (soldeDeviseSociete + affectation.MontantDeviseSociete < montant * echeance.CoursDevise)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorMontant);
		}
		decimal newMontantDeviseSocieteReglement = montant * reglementFournisseur.DeviseCours;
		decimal num = montant * echeance.CoursDevise;
		reglementFournisseur.Solde += affectation.Montant - montant;
		reglementFournisseur.SoldeDevise += affectation.MontantDeviseSociete - num;
		reglementFournisseur.DateModification = DateTime.Now;
		reglementFournisseur.ModificateurNo = utilisateur.No;
		if (reglementFournisseur.Solde < 0m)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorMontant);
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		RecalculerEcartChangeFournisseur(reglementFournisseur, echeance, deviseSocieteNo, newMontantDeviseSocieteReglement, num);
		if (echeance.Type == EcheanceType.Solde || echeance.Type == EcheanceType.Erp || echeance.Type == EcheanceType.ImpayeFournisseur || echeance.Type == EcheanceType.FactureGR)
		{
			affectation.NombreJourReglement = (int)Math.Round((reglementFournisseur.DateEcheance.Date - echeance.DocumentDate.Date).TotalDays, MidpointRounding.AwayFromZero);
			affectation.DelaisMoyenPayement = (int)Math.Round((decimal)affectation.NombreJourReglement * montant / echeance.Montant, MidpointRounding.AwayFromZero);
		}
		_affectationRepository.Update(affectation, montant, num);
		echeance.Solde += affectation.Montant - montant;
		echeance.SoldeDeviseSociete += affectation.MontantDeviseSociete - num;
		echeance.DelaisMoyenPayement = EcheanceCalculerDelaisMoyenPayement(echeance.No);
		_echeanceRepository.Update(echeance);
		_reglementFournisseurRepository.Update(reglementFournisseur);
		_notifyService.Notify(TypeEntity.ReglementFournisseur, reglementNo, TypeAction.Modification, reglementFournisseur.SocieteNo);
		_notifyService.Notify(TypeEntity.Echeance, echeance.No, TypeAction.Modification, reglementFournisseur.SocieteNo);
		transactionScope.Complete();
	}

	private void AnnulerMouvementSortieReglementFrs(ReglementFournisseur reglement)
	{
		if (reglement == null)
		{
			throw new ArgumentNullException("reglement");
		}
		foreach (HistoriqueMvt item in _historiqueRepository.GetReglementFournisseurEspece(reglement.No))
		{
			HistoriqueMvt historiqueMvt = _historiqueRepository.GetAllByMouvement(item.MouvementInNo).Single((HistoriqueMvt x) => x.Sens == SensMouvement.Entree);
			if (historiqueMvt == null)
			{
				throw new ArgumentException("Lot entrée invalide!");
			}
			historiqueMvt.Lot.MontantRestant += item.Montant;
			_historiqueRepository.Update(historiqueMvt);
			_historiqueRepository.Delete(item);
		}
	}

	private void DeleteEcartChange(ReglementClient reglement, Echeance echeance, int deviseSocieteNo)
	{
		if (reglement == null)
		{
			throw new ArgumentNullException("reglement");
		}
		if (echeance == null)
		{
			throw new ArgumentNullException("echeance");
		}
		if (reglement.DeviseNo != deviseSocieteNo && !(reglement.DeviseCours == echeance.CoursDevise))
		{
			Affectation affectation = reglement.GetAffectations().SingleOrDefault((Affectation x) => x.EcartEcheanceNo == echeance.No);
			if (affectation == null)
			{
				throw new ApplicationException("Impossible de charger l'affectation d'écart!");
			}
			Echeance echeance2 = _echeanceRepository.Get(affectation.EcheanceNo);
			if (echeance2 == null)
			{
				throw new ApplicationException("Impossible de charger l'échéance d'écart!");
			}
			if (echeance2.Type != EcheanceType.GainEchange && echeance2.Type != EcheanceType.PerteEchange)
			{
				throw new ApplicationException("Echéance d'écart invalide!");
			}
			EcartEchange ecartEchange = _ecartEchangeRepository.Get(echeance2.No);
			if (ecartEchange == null)
			{
				throw new ApplicationException("Impossible de charger l'écart [" + echeance2.DocumentNumero + "].");
			}
			if (ecartEchange.IsComptabilise != EtatComptabilite.NonComptabilise)
			{
				throw new ApplicationException("L'échéance d'écart [" + ecartEchange.Numero + "] est comptabilisée!");
			}
			reglement.Solde += affectation.Montant;
			reglement.SoldeDeviseSociete += affectation.MontantDeviseSociete;
			_affectationRepository.Delete(affectation);
			_echeanceRepository.Delete(echeance2);
			_notifyService.Notify(TypeEntity.Echeance, echeance2.No, TypeAction.Suppression, reglement.SocieteNo);
			_notifyService.Notify(TypeEntity.Reglement, reglement.No, TypeAction.Modification, reglement.SocieteNo);
		}
	}

	private void DeleteEcartChangeFournisseur(ReglementFournisseur reglement, Echeance echeance, int deviseSocieteNo)
	{
		if (reglement == null)
		{
			throw new ArgumentNullException("reglement");
		}
		if (echeance == null)
		{
			throw new ArgumentNullException("echeance");
		}
		if (reglement.DeviseNo != deviseSocieteNo && !(reglement.DeviseCours == echeance.CoursDevise))
		{
			Affectation affectation = reglement.GetAffectations().SingleOrDefault((Affectation x) => x.EcartEcheanceNo == echeance.No);
			if (affectation == null)
			{
				throw new ApplicationException("Impossible de charger l'affectation d'écart!");
			}
			Echeance echeance2 = _echeanceRepository.Get(affectation.EcheanceNo);
			if (echeance2 == null)
			{
				throw new ApplicationException("Impossible de charger l'échéance d'écart!");
			}
			if (echeance2.Type != EcheanceType.GainEchange && echeance2.Type != EcheanceType.PerteEchange)
			{
				throw new ApplicationException("Echéance d'écart invalide!");
			}
			EcartEchange ecartEchange = _ecartEchangeRepository.Get(echeance2.No);
			if (ecartEchange == null)
			{
				throw new ApplicationException("Impossible de charger l'écart [" + echeance2.DocumentNumero + "].");
			}
			if (ecartEchange.IsComptabilise != EtatComptabilite.NonComptabilise)
			{
				throw new ApplicationException("L'échéance d'écart [" + ecartEchange.Numero + "] est comptabilisée!");
			}
			reglement.Solde += affectation.Montant;
			reglement.SoldeDevise += affectation.MontantDeviseSociete;
			_affectationRepository.Delete(affectation);
			_echeanceRepository.Delete(echeance2);
			_notifyService.Notify(TypeEntity.Echeance, echeance2.No, TypeAction.Suppression, reglement.SocieteNo);
			_notifyService.Notify(TypeEntity.ReglementFournisseur, reglement.No, TypeAction.Modification, reglement.SocieteNo);
		}
	}

	private void RecalculerEcartChange(ReglementClient reglement, Echeance echeance, int deviseSocieteNo, decimal newMontantDeviseSocieteReglement, decimal newMontantDeviseSocieteEcheance)
	{
		if (reglement == null)
		{
			throw new ArgumentNullException("reglement");
		}
		if (echeance == null)
		{
			throw new ArgumentNullException("echeance");
		}
		if (reglement.DeviseNo != deviseSocieteNo && !(reglement.DeviseCours == echeance.CoursDevise))
		{
			Affectation affectation = reglement.GetAffectations().SingleOrDefault((Affectation x) => x.EcartEcheanceNo == echeance.No);
			if (affectation == null)
			{
				throw new ApplicationException("Impossible de charger l'affectation d'écart!");
			}
			Echeance echeance2 = _echeanceRepository.Get(affectation.EcheanceNo);
			if (echeance2 == null)
			{
				throw new ApplicationException("Impossible de charger l'échéance d'écart!");
			}
			if (echeance2.IsComptaEcart)
			{
				throw new ApplicationException("L'écart de change est comptabilisé!");
			}
			decimal num = newMontantDeviseSocieteReglement - newMontantDeviseSocieteEcheance;
			reglement.SoldeDeviseSociete = reglement.SoldeDeviseSociete + affectation.MontantDeviseSociete - num;
			_reglementClientRepository.Update(reglement);
			echeance2.MontantDeviseSociete = num;
			_echeanceRepository.Update(echeance2);
			_affectationRepository.Update(affectation, 0m, num);
			_notifyService.Notify(TypeEntity.Echeance, echeance2.No, TypeAction.Modification, reglement.SocieteNo);
		}
	}

	private void RecalculerEcartChangeFournisseur(ReglementFournisseur reglement, Echeance echeance, int deviseSocieteNo, decimal newMontantDeviseSocieteReglement, decimal newMontantDeviseSocieteEcheance)
	{
		if (reglement == null)
		{
			throw new ArgumentNullException("reglement");
		}
		if (echeance == null)
		{
			throw new ArgumentNullException("echeance");
		}
		if (reglement.DeviseNo != deviseSocieteNo && !(reglement.DeviseCours == echeance.CoursDevise))
		{
			Affectation affectation = reglement.GetAffectations().SingleOrDefault((Affectation x) => x.EcartEcheanceNo == echeance.No);
			if (affectation == null)
			{
				throw new ApplicationException("Impossible de charger l'affectation d'écart!");
			}
			Echeance echeance2 = _echeanceRepository.Get(affectation.EcheanceNo);
			if (echeance2 == null)
			{
				throw new ApplicationException("Impossible de charger l'échéance d'écart!");
			}
			if (echeance2.IsComptaEcart)
			{
				throw new ApplicationException("L'écart de change est comptabilisé!");
			}
			decimal num = newMontantDeviseSocieteReglement - newMontantDeviseSocieteEcheance;
			reglement.SoldeDevise = reglement.SoldeDevise + affectation.MontantDeviseSociete - num;
			_reglementFournisseurRepository.Update(reglement);
			echeance2.MontantDeviseSociete = num;
			_echeanceRepository.Update(echeance2);
			_affectationRepository.Update(affectation, 0m, num);
			_notifyService.Notify(TypeEntity.Echeance, echeance2.No, TypeAction.Modification, reglement.SocieteNo);
		}
	}

	private void VersementRemis(int bordereauNo, string pieceNumero, DateTime dateRemis)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		if (bordereauNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorBanqueInvalide);
		}
		if (string.IsNullOrEmpty(pieceNumero))
		{
			throw new ArgumentNullException("piece");
		}
		Bordereau bordereau = _bordereauRepository.Get(bordereauNo);
		if (bordereau == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorBanqueInvalide);
		}
		Caisse caisse = Get(bordereau.CaisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Opération invalide! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grc))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (bordereau.IsRemis)
		{
			throw new ApplicationException(TresorerieCoreMessages.InfoBordereauRemis);
		}
		if (bordereau.IsPointe)
		{
			throw new ApplicationException(TresorerieCoreMessages.InfoBordereauRapproche);
		}
		bordereau.IsRemis = true;
		bordereau.PieceNumero = pieceNumero;
		bordereau.DateRemis = dateRemis;
		bordereau.DateModification = DateTime.Now;
		bordereau.ModificateurNo = utilisateur.No;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_bordereauRepository.Update(bordereau);
		_notifyService.Notify(TypeEntity.Bordereau, bordereauNo, TypeAction.Modification, bordereau.SocieteNo);
		transactionScope.Complete();
	}

	public IEnumerable<Affectation> AffectationGetAllByReglement(int reglementNo)
	{
		if (reglementNo <= 0)
		{
			throw new ArgumentNullException("reglementNo");
		}
		return _affectationRepository.GetByReglement(reglementNo);
	}

	public IEnumerable<Affectation> AffectationGetAllByEcheance(int echeanceNo)
	{
		if (echeanceNo <= 0)
		{
			throw new ArgumentNullException("echeanceNo");
		}
		return _affectationRepository.GetByEcheance(echeanceNo);
	}

	public void ReglementClientReserver(int reglementNo)
	{
		ReglementClient reglementClient = _reglementClientRepository.Get(reglementNo);
		if (reglementClient == null)
		{
			throw new ApplicationException("Impossible de charger le règlement.");
		}
		if (reglementClient.IsReserveDossierClt)
		{
			throw new ApplicationException("Le règlement [" + reglementClient.Numero + "] est déjà réservée.");
		}
		if (reglementClient.IsAnnule)
		{
			throw new ApplicationException("Le règlement [" + reglementClient.Numero + "] est annulé.");
		}
		if (reglementClient.Solde == 0m)
		{
			throw new ApplicationException("Opération invalide. Le règlement [" + reglementClient.Numero + "] est totalement payée.[Reservation]");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_reglementClientRepository.UpdateReservation(reglementClient.No, IsReserver: true);
		_notifyService.Notify(TypeEntity.Reglement, reglementClient.No, TypeAction.Modification, reglementClient.SocieteNo);
		transactionScope.Complete();
	}

	public void ReglementFournisseurReserver(int reglementNo)
	{
		ReglementFournisseur reglementFournisseur = _reglementFournisseurRepository.Get(reglementNo);
		if (reglementFournisseur == null)
		{
			throw new ApplicationException("Impossible de charger le règlement.");
		}
		if (reglementFournisseur.IsReserveDossierFrs)
		{
			throw new ApplicationException("Le règlement [" + reglementFournisseur.Numero + "] est déjà réservée.");
		}
		if (reglementFournisseur.IsAnnule)
		{
			throw new ApplicationException("Le règlement [" + reglementFournisseur.Numero + "] est annulé.");
		}
		if (reglementFournisseur.Solde == 0m)
		{
			throw new ApplicationException("Opération invalide. Le règlement [" + reglementFournisseur.Numero + "] est totalement payée.[Reservation]");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_reglementFournisseurRepository.UpdateReservation(reglementFournisseur.No, reserve: true);
		_notifyService.Notify(TypeEntity.ReglementFournisseur, reglementFournisseur.No, TypeAction.Modification, reglementFournisseur.SocieteNo);
		transactionScope.Complete();
	}

	public void ReglementClientAnnulerReservation(int reglementNo)
	{
		ReglementClient reglementClient = _reglementClientRepository.Get(reglementNo);
		if (reglementClient == null)
		{
			throw new ApplicationException("Impossible de charger le règlement.");
		}
		if (!reglementClient.IsReserveDossierClt)
		{
			throw new ApplicationException("Le règlement [" + reglementClient.Numero + "] est déjà non réservée.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_reglementClientRepository.UpdateReservation(reglementClient.No, IsReserver: false);
		_notifyService.Notify(TypeEntity.Reglement, reglementClient.No, TypeAction.Modification, reglementClient.SocieteNo);
		transactionScope.Complete();
	}

	public void ReglementFournisseurAnnulerReservation(int reglementNo)
	{
		ReglementFournisseur reglementFournisseur = _reglementFournisseurRepository.Get(reglementNo);
		if (reglementFournisseur == null)
		{
			throw new ApplicationException("Impossible de charger le règlement.");
		}
		if (!reglementFournisseur.IsReserveDossierFrs)
		{
			throw new ApplicationException("Le règlement [" + reglementFournisseur.Numero + "] est déjà non réservée.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_reglementFournisseurRepository.UpdateReservation(reglementFournisseur.No, reserve: false);
		_notifyService.Notify(TypeEntity.ReglementFournisseur, reglementFournisseur.No, TypeAction.Modification, reglementFournisseur.SocieteNo);
		transactionScope.Complete();
	}

	public int ReglementFournisseurImputer(int reglementNo, int echeanceNo, decimal montant, int deviseSocieteNo, bool isDossierImpaye, bool isImporterFromErp = false)
	{
		if (reglementNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorReglementNo);
		}
		if (echeanceNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorEcheanceNo);
		}
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		ReglementFournisseur reglementFournisseur = _reglementFournisseurRepository.Get(reglementNo);
		if (reglementFournisseur == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementNo);
		}
		if (reglementFournisseur.IsAnnule)
		{
			throw new ApplicationException("Le règlement est annulé.");
		}
		if (reglementFournisseur.ReglementNature != ReglementNature.Reglement)
		{
			throw new ApplicationException("Opération invalide! C'est un règlement d'avance.");
		}
		Caisse caisse = Get(reglementFournisseur.CaisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Imputation invalide! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grf))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		Echeance echeance = _echeanceRepository.Get(echeanceNo);
		if (echeance == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorEcheanceNo);
		}
		if (!SocieteManager.UserHasAutorisationSouche(echeance.Domaine, echeance.SoucheNo))
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorAutorisationSouche);
		}
		if (echeance.PayeurNo != reglementFournisseur.FournisseurNo)
		{
			throw new ApplicationException("L'échéance fournisseur est invalide.");
		}
		Societe societe = SocieteManager.Societe;
		if (!isDossierImpaye)
		{
			DossierImpayeManager dossierImpayeManager = SocieteManager.DossierImpayeManager;
			if (dossierImpayeManager.GetLigneImpayeByReglementNo(reglementNo) != null)
			{
				throw new ApplicationException("Le règlement [" + reglementFournisseur.Numero + "] appartient à un dossier règlement impayé!");
			}
			if (echeance.Type == EcheanceType.ImpayeFournisseur && dossierImpayeManager.GetLignesImpayeByEcheanceNo(echeance.No).Any())
			{
				throw new ApplicationException("L'échéance [" + echeance.DocumentNumero + "] appartient à un dossier règlement impayé!");
			}
			if (echeance.Type == EcheanceType.CommissionImpaye || echeance.Type == EcheanceType.InteretImpaye)
			{
				throw new InvalidOperationException($"Opération invalide pour l'échéance [{echeance.DocumentNumero}][{echeance.Type}]!");
			}
		}
		if (isDossierImpaye)
		{
			if (echeance.Type != EcheanceType.ImpayeFournisseur && echeance.Type != EcheanceType.CommissionImpaye && echeance.Type != EcheanceType.InteretImpaye)
			{
				throw new InvalidOperationException($"Opération invalide pour l'échéance [{echeance.DocumentNumero}][{echeance.Type}]!");
			}
			if (echeance.DeviseNo != deviseSocieteNo || reglementFournisseur.DeviseNo != deviseSocieteNo)
			{
				throw new ApplicationException("Dossier règlement impayé ne supporte pas la devise!");
			}
			if (reglementFournisseur.IsComptabilise != EtatComptabilite.NonComptabilise)
			{
				throw new ApplicationException("Le règlement [" + reglementFournisseur.Numero + "] est comptabilisé !");
			}
		}
		if (reglementFournisseur.Solde - montant < 0m)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorMontantInavlide);
		}
		if (echeance.Montant > 0m && echeance.Solde < montant)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorEcheanceNegative);
		}
		if (_affectationRepository.GetByReglement(reglementNo).Any((Affectation x) => x.EcheanceNo == echeanceNo))
		{
			throw new ApplicationException("Le règlement [" + reglementFournisseur.Numero + "] est déjà imputé sur l'échéance [" + echeance.DocumentNumero + "] dans un autre dossier de règlement fournisseur.");
		}
		if (reglementFournisseur.DeviseNo != echeance.DeviseNo)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorDeviseEcheanceReglement);
		}
		SocieteDevise devise = societe.GetDevise(deviseSocieteNo);
		if (devise == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorDeviseNo);
		}
		decimal num = Math.Round(montant * echeance.CoursDevise, devise.NombreDecimales, MidpointRounding.AwayFromZero);
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		Affectation affectation = new Affectation(0, DateTime.Now, montant, reglementNo, echeanceNo, num, null);
		affectation.IsImporterFromErp = isImporterFromErp;
		if (echeance.Type == EcheanceType.Solde || echeance.Type == EcheanceType.Erp || echeance.Type == EcheanceType.ImpayeFournisseur || echeance.Type == EcheanceType.FactureFrsTresorerie || echeance.Type == EcheanceType.FactureGR)
		{
			affectation.NombreJourReglement = (int)Math.Round((reglementFournisseur.DateEcheance.Date - echeance.DocumentDate.Date).TotalDays, MidpointRounding.AwayFromZero);
			affectation.DelaisMoyenPayement = (int)Math.Round((decimal)affectation.NombreJourReglement * montant / echeance.Montant, MidpointRounding.AwayFromZero);
		}
		int? num2 = _affectationRepository.Create(affectation);
		if (!num2.HasValue)
		{
			throw new ApplicationException("Impossible de crée une affectation");
		}
		int value = num2.Value;
		echeance.Solde -= montant;
		echeance.SoldeDeviseSociete -= num;
		echeance.DelaisMoyenPayement = EcheanceCalculerDelaisMoyenPayement(echeance.No);
		_echeanceRepository.Update(echeance);
		decimal num3 = Math.Round(montant * reglementFournisseur.DeviseCours, devise.NombreDecimales, MidpointRounding.AwayFromZero);
		reglementFournisseur.Solde -= montant;
		reglementFournisseur.SoldeDevise -= num3;
		reglementFournisseur.DateModification = DateTime.Now;
		reglementFournisseur.ModificateurNo = utilisateur.No;
		_reglementFournisseurRepository.Update(reglementFournisseur);
		_notifyService.Notify(TypeEntity.ReglementFournisseur, reglementNo, TypeAction.Modification, reglementFournisseur.SocieteNo);
		_notifyService.Notify(TypeEntity.Echeance, echeanceNo, TypeAction.Modification, reglementFournisseur.SocieteNo);
		if (echeance.Type == EcheanceType.ImpayeFournisseur)
		{
			_notifyService.Notify(TypeEntity.ImpayeFournisseur, echeance.ReglementImpayeNo, TypeAction.Modification, echeance.SocieteNo);
		}
		transactionScope.Complete();
		return value;
	}

	public void AffectationFournisseurDelete(int reglementNo, int affectationNo, int deviseSocieteNo, bool isDossierImpaye, int dossierNo = 0)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		SocieteDevise devise = SocieteManager.Societe.GetDevise(deviseSocieteNo);
		if (devise == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorDeviseNo);
		}
		ReglementFournisseur reglementFournisseur = _reglementFournisseurRepository.Get(reglementNo);
		if (reglementFournisseur == null)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorReglementNo);
		}
		if (dossierNo != 0 && (_dossierReglementRepository.GetDossier(dossierNo) ?? throw new ApplicationException("Impossible de charger le dossier de règlement.")).StatutDossier != StatutDossierReglement.Valide)
		{
			throw new ApplicationException("Le dossier n'est pas validé.");
		}
		Caisse caisse = Get(reglementFournisseur.CaisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Impossible de supprimer l'imputation! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grf))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		Affectation affectation = _affectationRepository.GetByReglement(reglementNo).SingleOrDefault((Affectation x) => x.No == affectationNo);
		if (affectation == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorOperationInvalide);
		}
		Echeance echeance = affectation.GetEcheance();
		if (echeance == null)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorEcheanceNo);
		}
		if (!SocieteManager.UserHasAutorisationSouche(echeance.Domaine, echeance.SoucheNo))
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorAutorisationSouche);
		}
		if (!isDossierImpaye)
		{
			DossierImpayeManager dossierImpayeManager = SocieteManager.DossierImpayeManager;
			if (dossierImpayeManager.GetLigneImpayeByReglementNo(reglementNo) != null)
			{
				throw new ApplicationException("Le règlement [" + reglementFournisseur.Numero + "] appartient à un dossier règlement impayé!");
			}
			if (echeance.Type == EcheanceType.ImpayeFournisseur && dossierImpayeManager.GetLignesImpayeByEcheanceNo(echeance.No).Any())
			{
				throw new ApplicationException("L'échéance [" + echeance.DocumentNumero + "] appartient à un dossier règlement impayé!");
			}
			if (echeance.Type == EcheanceType.CommissionImpaye || echeance.Type == EcheanceType.InteretImpaye)
			{
				throw new InvalidOperationException($"Opération invalide pour l'échéance [{echeance.DocumentNumero}][{echeance.Type}]!");
			}
		}
		if (isDossierImpaye && echeance.Type != EcheanceType.ImpayeFournisseur && echeance.Type != EcheanceType.CommissionImpaye && echeance.Type != EcheanceType.InteretImpaye)
		{
			throw new InvalidOperationException($"Opération invalide pour l'échéance [{echeance.DocumentNumero}][{echeance.Type}]!");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		reglementFournisseur.Solde += affectation.Montant;
		reglementFournisseur.SoldeDevise = Math.Round(reglementFournisseur.Solde * reglementFournisseur.DeviseCours, devise.NombreDecimales, MidpointRounding.AwayFromZero);
		reglementFournisseur.DateModification = DateTime.Now;
		reglementFournisseur.ModificateurNo = utilisateur.No;
		if (reglementFournisseur.Solde < 0m || reglementFournisseur.SoldeDevise < 0m)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorEcheanceNegative);
		}
		_reglementFournisseurRepository.Update(reglementFournisseur);
		_affectationRepository.Delete(affectation);
		echeance.Solde += affectation.Montant;
		echeance.SoldeDeviseSociete = Math.Round(echeance.Solde * echeance.CoursDevise, devise.NombreDecimales, MidpointRounding.AwayFromZero);
		echeance.Ajuste = false;
		echeance.DelaisMoyenPayement = EcheanceCalculerDelaisMoyenPayement(echeance.No);
		_echeanceRepository.Update(echeance);
		_notifyService.Notify(TypeEntity.ReglementFournisseur, reglementNo, TypeAction.Modification, reglementFournisseur.SocieteNo);
		_notifyService.Notify(TypeEntity.Echeance, echeance.No, TypeAction.Modification, reglementFournisseur.SocieteNo);
		transactionScope.Complete();
	}

	public void ReglementFournisseurUpdate(int reglementNo, string beneficiaire, string libelle, string affaireNumero, string info1, string info2, string info3, string info4)
	{
		ReglementFournisseur reglementFournisseur = _reglementFournisseurRepository.Get(reglementNo);
		if (reglementFournisseur == null)
		{
			throw new ApplicationException($"Impossible de charger le règlement fournisseur [{reglementNo}].");
		}
		Societe societe = SocieteManager.Societe;
		if (string.IsNullOrEmpty(affaireNumero) && societe.IsAffaireFournisseurRequired)
		{
			throw new ApplicationException(rcRessources.AffaireObligatoire);
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		if (string.IsNullOrEmpty(beneficiaire) && (reglementFournisseur.Type == ReglementType.Cheque || reglementFournisseur.Type == ReglementType.Traite || reglementFournisseur.Type == ReglementType.Virement))
		{
			throw new ApplicationException("Le bénéficiaire est obligatoire.");
		}
		reglementFournisseur.Beneficiaire = beneficiaire;
		reglementFournisseur.Libelle = libelle;
		reglementFournisseur.AffaireNumero = affaireNumero;
		reglementFournisseur.Info1 = info1;
		reglementFournisseur.Info2 = info2;
		reglementFournisseur.Info3 = info3;
		reglementFournisseur.Info4 = info4;
		_reglementFournisseurRepository.Update(reglementFournisseur);
		_notifyService.Notify(TypeEntity.ReglementFournisseur, reglementFournisseur.No, TypeAction.Modification, reglementFournisseur.SocieteNo);
		transactionScope.Complete();
	}

	public int ReglementFournisseurCreate(string numero, DateTime date, decimal montant, int deviseNo, int fournisseurNo, string fournisseurCode, string fournisseurIntitule, TiersType typeTiers, int modeNo, int caisseNo, string piece, string libelle, DateTime echeance, int? banqueNo, string banqueClient, bool isBarre, string beneficiaire, int? chequeNo, decimal cours, decimal montantDevise, string affaireNumero, int? traiteNo, bool isCertifier, DateTime? dateValidite, decimal? montantPlafond, string info1, string info2, string info3, string info4, ReglementNature reglementNature, bool isImporterFromErp = false, bool isImporterComptabiliser = false)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		Societe societe = SocieteManager.Societe;
		if (string.IsNullOrEmpty(numero))
		{
			throw new ArgumentNullException("numero");
		}
		if (fournisseurNo <= 0)
		{
			throw new ArgumentException("Le fournisseur No est invalide!");
		}
		if (string.IsNullOrEmpty(fournisseurCode))
		{
			throw new ArgumentNullException("fournisseurCode");
		}
		if (string.IsNullOrEmpty(fournisseurIntitule))
		{
			throw new ArgumentNullException("fournisseurIntitule");
		}
		if (deviseNo <= 0)
		{
			throw new ArgumentException("La devise No est invalide!");
		}
		if (montant <= 0m)
		{
			throw new ApplicationException("Le montant est invalide!");
		}
		Caisse caisse = Get(caisseNo);
		if (caisse == null)
		{
			throw new InvalidOperationException("La caisse No est invalide!");
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Opération invalide! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisseNo))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (_reglementFournisseurRepository.Get(caisse.SocieteNo, numero) != null)
		{
			throw new ApplicationException("Le numéro de règlement existe déjà!");
		}
		if (!caisse.HasModeReglement(modeNo))
		{
			throw new ApplicationException("le mode de règlement est invalide!");
		}
		CaisseModeReglement mode = caisse.GetMode(modeNo);
		if (mode == null)
		{
			throw new InvalidOperationException("Le mode No est invalide!");
		}
		if (mode.EnSommeil)
		{
			throw new InvalidOperationException("Opération invalide! Le mode est en sommeil!");
		}
		if ((mode.Type == ReglementType.Cheque || mode.Type == ReglementType.Traite || mode.Type == ReglementType.Virement) && !banqueNo.HasValue)
		{
			throw new ApplicationException("La banque est invalide!");
		}
		if ((mode.Type == ReglementType.Cheque || mode.Type == ReglementType.Traite || mode.Type == ReglementType.Virement) && string.IsNullOrEmpty(piece))
		{
			throw new ApplicationException("La pièce de règlement est invalide!");
		}
		if (mode.Type == ReglementType.Virement && string.IsNullOrEmpty(banqueClient))
		{
			throw new ApplicationException("La banque client est invalide!");
		}
		if ((mode.Type == ReglementType.Cheque || mode.Type == ReglementType.Traite || mode.Type == ReglementType.Virement) && banqueNo.HasValue)
		{
			InformationsBanque byBanqueId = SocieteManager.Groupe.InformationBanqueManager.GetByBanqueId(banqueNo.Value);
			if (byBanqueId != null && byBanqueId.EnSommeil)
			{
				throw new ApplicationException("La banque est en sommeil!");
			}
		}
		if (mode.Type == ReglementType.Cheque && chequeNo.HasValue)
		{
			Cheque cheque = SocieteManager.ChequeGet(chequeNo.Value);
			if (cheque == null)
			{
				throw new ArgumentException("Le Chèque est invalide!");
			}
			if (cheque.Statut != ChequeStatut.NonUtilise && cheque.Statut != ChequeStatut.Reserve)
			{
				throw new ArgumentException("Le statut du Chèque est invalide!");
			}
			if (cheque.Statut == ChequeStatut.Reserve && cheque.TiersNo != fournisseurNo)
			{
				throw new ApplicationException("Le chèque est réservé pour le compte d'un autre fournisseur.");
			}
			if (societe.LegislationType == Legislation.Tunisie)
			{
				if (!montantPlafond.HasValue)
				{
					throw new ApplicationException("Le montant du plafond obligatoire.");
				}
				decimal num = montant;
				decimal? montantPlafond2 = cheque.MontantPlafond;
				if (((num > montantPlafond2.GetValueOrDefault()) & montantPlafond2.HasValue) && !isCertifier)
				{
					throw new ApplicationException("Le montant ne doit pas dépasser le plafond.");
				}
				if (!dateValidite.HasValue)
				{
					throw new ApplicationException("La date de validité obligatoire.");
				}
				if (echeance.Date > dateValidite.Value.Date)
				{
					throw new ApplicationException("La date d'échéance ne doit pas dépasser la date de validité.");
				}
			}
		}
		if (societe.LegislationType == Legislation.Maroc && mode.Type == ReglementType.Traite && traiteNo.HasValue)
		{
			Traite traite = SocieteManager.TraiteGet(traiteNo.Value);
			if (traite == null)
			{
				throw new ArgumentException("La traite est invalide!");
			}
			if (traite.Statut != ChequeStatut.NonUtilise && traite.Statut != ChequeStatut.Reserve)
			{
				throw new ArgumentException("Le statut de la traite est invalide!");
			}
			if (traite.Statut == ChequeStatut.Reserve && traite.TiersNo != fournisseurNo)
			{
				throw new ApplicationException("La traite est réservée pour le compte d'un autre fournisseur.");
			}
		}
		if (societe.IsAffaireFournisseurRequired && string.IsNullOrEmpty(affaireNumero))
		{
			throw new ApplicationException(rcRessources.AffaireObligatoire);
		}
		ReglementFournisseur reglementFournisseur = new ReglementFournisseur(numero, fournisseurNo, fournisseurCode, fournisseurIntitule, typeTiers, caisse.No, caisse.SocieteNo, deviseNo, modeNo, mode.Type, date, montant, echeance)
		{
			BanqueTier = banqueClient,
			PieceNumero = piece,
			BanqueNo = banqueNo,
			Libelle = libelle,
			IsBarre = isBarre,
			Beneficiaire = beneficiaire,
			ChequeNo = chequeNo,
			UserNo = utilisateur.No,
			DateCreation = DateTime.Now,
			DateModification = DateTime.Now,
			ModificateurNo = utilisateur.No,
			DeviseCours = cours,
			MontantDeviseSociete = montantDevise,
			Solde = montant,
			SoldeDevise = montantDevise,
			AffaireNumero = affaireNumero,
			TraiteNo = traiteNo,
			EnteteBordereauNumero = string.Empty,
			Info1 = info1,
			Info2 = info2,
			Info3 = info3,
			Info4 = info4,
			IsCertifier = (societe.LegislationType == Legislation.Tunisie && mode.Type == ReglementType.Cheque && isCertifier),
			DateValiditer = ((societe.LegislationType == Legislation.Tunisie && mode.Type == ReglementType.Cheque) ? dateValidite : ((DateTime?)null)),
			MontantPlafond = ((societe.LegislationType == Legislation.Tunisie && mode.Type == ReglementType.Cheque) ? montantPlafond : ((decimal?)null)),
			ReglementNature = reglementNature,
			IsImporterComptabiliseErp = isImporterComptabiliser,
			IsImporterFromErp = isImporterFromErp
		};
		if (typeTiers == TiersType.Salarie || typeTiers == TiersType.Autre)
		{
			reglementFournisseur.Solde = 0m;
			reglementFournisseur.SoldeDevise = 0m;
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		int? num2 = _reglementFournisseurRepository.Create(reglementFournisseur);
		if (!num2.HasValue || num2.Value == 0)
		{
			throw new ApplicationException("La création du règlement a échouée!");
		}
		if (mode.Type == ReglementType.Espece)
		{
			decimal solde = SocieteManager.HistoriqueMvtManager.GetSolde(caisseNo, modeNo, deviseNo);
			if (montant <= 0m || montant > solde)
			{
				throw new ArgumentException(TresorerieCoreMessages.ErrorMontantInavlide);
			}
			List<HistoriqueMvt> list = _historiqueRepository.GetAllNonEpuise(caisseNo, modeNo, deviseNo).ToList();
			int num3 = 0;
			while (montant > 0m && num3 < list.Count)
			{
				HistoriqueMvt historiqueMvt = list[num3];
				Lot lot = historiqueMvt.Lot;
				decimal montantRestant = lot.MontantRestant;
				lot.MontantRestant = ((lot.MontantRestant >= montant) ? (lot.MontantRestant - montant) : 0m);
				_historiqueRepository.Update(historiqueMvt);
				HistoriqueMvt historiqueMvt2 = new HistoriqueMvt(lot.No, caisseNo, SensMouvement.Sortie, MouvementDomaine.ReglementFournisseur, modeNo, lot.No, num2.Value, StatutTransfert.None, deviseNo, new Lot(lot.No, (montantRestant >= montant) ? montant : montantRestant, 0m));
				_historiqueRepository.Create(historiqueMvt2);
				montant -= montantRestant;
				num3++;
			}
		}
		_notifyService.Notify(TypeEntity.ReglementFournisseur, num2.Value, TypeAction.Ajout, reglementFournisseur.SocieteNo);
		transactionScope.Complete();
		return num2.Value;
	}

	public int ReglementFournisseurCreate(string numero, DateTime date, decimal montant, int deviseNo, int fournisseurNo, string fournisseurCode, string fournisseurIntitule, TiersType typeTiers, int modeNo, int caisseNo, string piece, string libelle, DateTime echeance, int? banqueNo, string banqueClient, bool isBarre, string beneficiaire, int? chequeNo, int dossierNo, string dossierNumero, decimal cours, decimal montantDevise, string affaireNumero, int? traiteNo, bool isChequeCertifie, DateTime? dateValidite, decimal? montantPlafond, string info1, string info2, string info3, string info4)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		DossierReglement obj = _dossierReglementRepository.GetDossier(dossierNo) ?? throw new ApplicationException("Impossible de charger le dossier de règlement.");
		int num = ReglementFournisseurCreate(numero, date, montant, deviseNo, fournisseurNo, fournisseurCode, fournisseurIntitule, typeTiers, modeNo, caisseNo, piece, libelle, echeance, banqueNo, banqueClient, isBarre, beneficiaire, chequeNo, cours, montantDevise, affaireNumero, traiteNo, isChequeCertifie, dateValidite, montantPlafond, info1, info2, info3, info4, ReglementNature.Reglement);
		ReglementFournisseur reglementFournisseur = _reglementFournisseurRepository.Get(num);
		if (reglementFournisseur == null)
		{
			throw new ApplicationException("Imossible de charger le règlement.");
		}
		if (!obj.IsDossierCommercial)
		{
			reglementFournisseur.Solde = 0m;
			reglementFournisseur.SoldeDevise = 0m;
			_reglementFournisseurRepository.Update(reglementFournisseur);
		}
		_reglementFournisseurRepository.UpdateDossierReglement(num, dossierNo, dossierNumero, utilisateur.No);
		_notifyService.Notify(TypeEntity.ReglementFournisseur, num, TypeAction.Modification, reglementFournisseur.SocieteNo);
		return num;
	}

	public bool ReglementFournisseurUsedInDossier(int reglementNo)
	{
		ReglementFournisseur reglementFournisseur = _reglementFournisseurRepository.Get(reglementNo);
		if (reglementFournisseur == null)
		{
			throw new ArgumentException("Impossible de charger le règlement.");
		}
		if (!reglementFournisseur.IsReserveDossierFrs)
		{
			return _reglementFournisseurRepository.IsUsedInDossierFrs(reglementFournisseur.No, reglementFournisseur.SocieteNo);
		}
		return true;
	}

	public IEnumerable<ReglementFournisseur> ReglementFournisseurGetAllNonSolde(int tiersNo, int caisseNo, Societe societe = null)
	{
		societe = societe ?? SocieteManager.Societe;
		if (tiersNo <= 0)
		{
			throw new ArgumentNullException("tiersNo");
		}
		return _reglementFournisseurRepository.GetAllReglementFournisseursNonSolde(societe.No, tiersNo, caisseNo);
	}

	public IEnumerable<ReglementClient> ReglementClientGetAllNonSolde(int tiersNo, int caisseNo, Societe societe = null)
	{
		societe = societe ?? SocieteManager.Societe;
		if (tiersNo <= 0)
		{
			throw new ArgumentNullException("tiersNo");
		}
		return _reglementClientRepository.GetAllReglementClientNonSolde(societe.No, tiersNo, caisseNo);
	}

	public IEnumerable<ReglementFournisseur> ReglementFournisseurGetAllNonSolde(Societe societe = null)
	{
		societe = societe ?? SocieteManager.Societe;
		return _reglementFournisseurRepository.GetAllReglementFournisseursNonSolde(societe.No);
	}

	public bool ReglementClientIsReferenceUnique(int modeNo, string reference, int reglementNo, Societe societe = null)
	{
		societe = societe ?? SocieteManager.Societe;
		SocieteModeReglement obj = societe.GetMode(modeNo) ?? throw new ApplicationException($"Impossible de charger le mode de règlement [{modeNo}].");
		if (obj.IsReferenceReglementClientObligatoire && string.IsNullOrEmpty(reference))
		{
			throw new ApplicationException("La référence est obligatoire.");
		}
		if (!obj.ControllerUniciteReferenceReglementClient)
		{
			return true;
		}
		return !_reglementClientRepository.IsReferenceExiste(societe.No, reference, reglementNo);
	}

	public void ReglementClientReporter(int reglementNo, DateTime echeance, Societe societe = null)
	{
		if (reglementNo <= 0)
		{
			throw new ArgumentNullException("reglementNo");
		}
		if (echeance == DateTime.MinValue)
		{
			throw new ArgumentNullException("echeance");
		}
		societe = societe ?? SocieteManager.Societe;
		ReglementClient reglementClient = _reglementClientRepository.Get(reglementNo);
		if (reglementClient == null)
		{
			throw new ApplicationException($"Impossible de charger le règlement [{reglementNo}]!");
		}
		if (reglementClient.DateEcheance.Date == echeance.Date)
		{
			return;
		}
		ModeReglement modeReglement = reglementClient.GetModeReglement();
		if (modeReglement.Type != ReglementType.Traite && modeReglement.Type != ReglementType.Cheque && modeReglement.Type != ReglementType.Virement)
		{
			return;
		}
		if (reglementClient.IsReglementAvoir)
		{
			throw new ApplicationException("Opération invalide. Règlement d'avoir.");
		}
		if (reglementClient.IsAnnule)
		{
			throw new ApplicationException("Le règlement [" + reglementClient.Numero + "] est annulé!");
		}
		if (reglementClient.IsRemis != Remis.NonRemis)
		{
			Bordereau bordereau = _bordereauRepository.Get(reglementClient.EnteteBordereauNo.GetValueOrDefault());
			if (bordereau == null)
			{
				throw new InvalidOperationException(TresorerieCoreMessages.ErrorBordereauNo);
			}
			if (bordereau.BordereauNature != NatureTypeBordereau.Escompte && bordereau.BordereauNature != NatureTypeBordereau.Factoring)
			{
				string text = ((reglementClient.IsRemis == Remis.RemisBordereau) ? " un bordereau" : "la banque");
				throw new InvalidOperationException("Le règlement [" + reglementClient.Numero + "] est remis à " + text + "!");
			}
		}
		if (reglementClient.IsEscompteRegle)
		{
			throw new ApplicationException("Le règlement [" + reglementClient.Numero + "] est réglé!");
		}
		if (reglementClient.IsPointe)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglementRapproche);
		}
		if (reglementClient.Type == ReglementType.Cheque && societe.LegislationType == Legislation.Tunisie && reglementClient.DateValiditer.HasValue && echeance.Date > reglementClient.DateValiditer.Value)
		{
			throw new ApplicationException("La date d'échéance ne doit pas dépasser la date de validité.");
		}
		IEnumerable<Affectation> affectations = reglementClient.GetAffectations();
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		reglementClient.AncienneDateEch = reglementClient.DateEcheance;
		_reglementClientRepository.UpdateAncienneDateEcheance(reglementClient);
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		reglementClient.UserReportNo = utilisateur.No;
		reglementClient.IsReport = true;
		reglementClient.DateEcheance = echeance;
		_reglementClientRepository.ReporterReglementClient(reglementClient);
		foreach (Affectation item in affectations)
		{
			Echeance echeance2 = item.GetEcheance();
			if (echeance2 == null)
			{
				throw new ApplicationException(TresorerieCoreMessages.ErrorEcheanceNo);
			}
			if (echeance2.Type == EcheanceType.Solde || echeance2.Type == EcheanceType.Erp || echeance2.Type == EcheanceType.Impaye || echeance2.Type == EcheanceType.FactureGR)
			{
				item.NombreJourReglement = (int)Math.Round((reglementClient.DateEcheance.Date - echeance2.DocumentDate.Date).TotalDays, MidpointRounding.AwayFromZero);
				item.DelaisMoyenPayement = (int)Math.Round((decimal)item.NombreJourReglement * item.Montant / echeance2.Montant, MidpointRounding.AwayFromZero);
				_affectationRepository.Update(item, item.NombreJourReglement, item.DelaisMoyenPayement);
			}
			echeance2.DelaisMoyenPayement = EcheanceCalculerDelaisMoyenPayement(echeance2.No);
			_echeanceRepository.Update(echeance2);
			_notifyService.Notify(TypeEntity.Echeance, echeance2.No, TypeAction.Modification, reglementClient.SocieteNo);
		}
		_notifyService.Notify(TypeEntity.Reglement, reglementClient.No, TypeAction.Modification, societe.No);
		transactionScope.Complete();
	}

	public void ReglementFournisseurReporter(int reglementNo, DateTime echeance, Societe societe = null)
	{
		if (reglementNo <= 0)
		{
			throw new ArgumentNullException("reglementNo");
		}
		if (echeance == DateTime.MinValue)
		{
			throw new ArgumentNullException("echeance");
		}
		societe = societe ?? SocieteManager.Societe;
		ReglementFournisseur reglementFournisseur = _reglementFournisseurRepository.Get(reglementNo);
		if (reglementFournisseur == null)
		{
			throw new ApplicationException($"Impossible de charger le règlement [{reglementNo}]!");
		}
		if (reglementFournisseur.DateEcheance.Date == echeance.Date)
		{
			return;
		}
		SocieteModeReglement mode = societe.GetMode(reglementFournisseur.ModeReglementNo);
		if (mode == null)
		{
			throw new ApplicationException($"Impossible de charger le mode de règlement [{reglementFournisseur.ModeReglementNo}].");
		}
		if (mode.Type != ReglementType.Traite && mode.Type != ReglementType.Cheque && mode.Type != ReglementType.Virement)
		{
			return;
		}
		if (reglementFournisseur.IsAnnule)
		{
			throw new ApplicationException("Le règlement [" + reglementFournisseur.Numero + "] est annulé!");
		}
		if (reglementFournisseur.IsPointe)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorReglementRapproche);
		}
		if (societe.LegislationType == Legislation.Tunisie && mode.Type == ReglementType.Cheque)
		{
			if (!reglementFournisseur.DateValiditer.HasValue)
			{
				throw new ApplicationException("Date validité invalide.");
			}
			if (echeance.Date > reglementFournisseur.DateValiditer.Value.Date)
			{
				throw new ApplicationException("Date invalide.");
			}
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		IEnumerable<Affectation> affectations = reglementFournisseur.GetAffectations();
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		reglementFournisseur.AncienneDateEch = reglementFournisseur.DateEcheance;
		_reglementFournisseurRepository.UpdateAncienneDateEcheance(reglementFournisseur);
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		reglementFournisseur.UserReportNo = utilisateur.No;
		reglementFournisseur.IsReport = true;
		reglementFournisseur.DateEcheance = echeance;
		_reglementFournisseurRepository.ReporterReglement(reglementFournisseur);
		foreach (Affectation item in affectations)
		{
			Echeance echeance2 = item.GetEcheance();
			if (echeance2 == null)
			{
				throw new ApplicationException(TresorerieCoreMessages.ErrorEcheanceNo);
			}
			if (echeance2.Type == EcheanceType.Solde || echeance2.Type == EcheanceType.Erp || echeance2.Type == EcheanceType.ImpayeFournisseur || echeance2.Type == EcheanceType.FactureFrsTresorerie || echeance2.Type == EcheanceType.FactureGR)
			{
				item.NombreJourReglement = (int)Math.Round((reglementFournisseur.DateEcheance.Date - echeance2.DocumentDate.Date).TotalDays, MidpointRounding.AwayFromZero);
				item.DelaisMoyenPayement = (int)Math.Round((decimal)item.NombreJourReglement * item.Montant / echeance2.Montant, MidpointRounding.AwayFromZero);
				_affectationRepository.Update(item, item.NombreJourReglement, item.DelaisMoyenPayement);
			}
			echeance2.DelaisMoyenPayement = EcheanceCalculerDelaisMoyenPayement(echeance2.No);
			_echeanceRepository.Update(echeance2);
			_notifyService.Notify(TypeEntity.Echeance, echeance2.No, TypeAction.Modification, reglementFournisseur.SocieteNo);
		}
		_notifyService.Notify(TypeEntity.ReglementFournisseur, reglementFournisseur.No, TypeAction.Modification, societe.No);
		transactionScope.Complete();
	}

	public int ReglementAvoirCreate(int echeanceNo, int caisseNo, int modeAvoirNo, string libelle)
	{
		if (echeanceNo <= 0)
		{
			throw new ArgumentException("echeanceNo");
		}
		if (modeAvoirNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorModeNo);
		}
		if (caisseNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorModeNo);
		}
		Societe societe = SocieteManager.Societe;
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		Caisse caisse = societe.GetCaisse(caisseNo);
		if (caisse == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Impossible de créer le règlement! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisseNo, ProfilType.Grc))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (!caisse.HasModeReglement(modeAvoirNo))
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorModeReglementInvalide);
		}
		CaisseModeReglement mode = caisse.GetMode(modeAvoirNo);
		if (mode == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorModeNo);
		}
		if (mode.Type != ReglementType.Autre || !mode.IsModeAvoir)
		{
			throw new ApplicationException("Le mode doit être de type avoir.");
		}
		Echeance echeance = _echeanceRepository.Get(echeanceNo);
		if (echeance == null)
		{
			throw new ApplicationException("Impossible de charger l'échéance.");
		}
		if (!SocieteManager.UserHasAutorisationSouche(echeance.Domaine, echeance.SoucheNo))
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorAutorisationSouche);
		}
		if (echeance.Montant != echeance.Solde && echeance.Montant >= 0m)
		{
			throw new ApplicationException("L'échéance doit être de type avoir.");
		}
		if (echeance.ReglementAvoirNo != 0 || !string.IsNullOrEmpty(echeance.ReglementAvoirNumero))
		{
			throw new ApplicationException("L'échéance [" + echeance.DocumentNumero + "] est associée à un règlement d'avoir.");
		}
		decimal num = Math.Abs(echeance.Solde);
		decimal num2 = Math.Abs(echeance.SoldeDeviseSociete);
		string numeroPieceCourante = SocieteManager.GetNumeroPieceCourante(EntityNumerotation.ReglementClient);
		ReglementClient reglementClient = new ReglementClient(numeroPieceCourante, echeance.PayeurNo, echeance.PayeurCode, echeance.PayeurIntitule, TiersType.Client, caisse.No, caisse.SocieteNo, echeance.DeviseNo, modeAvoirNo, mode.Type, echeance.DocumentDate, num, echeance.DocumentDate, echeance.CoursDevise, num2)
		{
			Solde = num,
			SoldeToRemplace = num,
			Libelle = libelle,
			Tire = echeance.PayeurIntitule,
			UtilisateurNo = utilisateur.No,
			StatutTransfert = StatutTransfert.None,
			CaisseOrigine = caisseNo,
			DateCreation = DateTime.Now,
			DateModification = DateTime.Now,
			ModificateurNo = utilisateur.No,
			SoldeDeviseSociete = num2,
			EcheanceAvoirNo = echeance.No,
			EcheanceAvoirNumero = echeance.DocumentNumero,
			IsReglementAvoir = true
		};
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		VerifierReglement(reglementClient, mode.Type);
		int? num3 = _reglementClientRepository.Create(reglementClient);
		if (!num3.HasValue || num3.Value == 0)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorCreationReglement);
		}
		HistoriqueMvt historiqueMvt = new HistoriqueMvt(num3.Value, caisseNo, SensMouvement.Entree, MouvementDomaine.ReglementClient, mode.No, num3.Value, 0, StatutTransfert.None, reglementClient.DeviseNo, new Lot(num3.Value, num, num));
		_historiqueRepository.Create(historiqueMvt);
		echeance.ReglementAvoirNo = num3.Value;
		echeance.ReglementAvoirNumero = numeroPieceCourante;
		echeance.Solde = 0m;
		echeance.SoldeDeviseSociete = 0m;
		_echeanceRepository.Update(echeance);
		_notifyService.Notify(TypeEntity.Reglement, num3.Value, TypeAction.Ajout, caisse.SocieteNo);
		_notifyService.Notify(TypeEntity.Echeance, echeanceNo, TypeAction.Modification, reglementClient.SocieteNo);
		transactionScope.Complete();
		return num3.Value;
	}

	public IEnumerable<DetailAffectation> GetAllDetailAffectationClient(int tiersNo, int representantNo = 0, DateTime? dateDebut = null, DateTime? dateFin = null)
	{
		Societe societe = SocieteManager.Societe;
		return _detailAffectationRepository.GetByClient(societe.No, tiersNo, representantNo, dateDebut, dateFin);
	}

	public IEnumerable<DetailAffectation> GetAllDetailAffectationFournisseur(int tiersNo, int representantNo = 0, DateTime? dateDebut = null, DateTime? dateFin = null)
	{
		Societe societe = SocieteManager.Societe;
		return _detailAffectationRepository.GetByFournisseur(societe.No, tiersNo, representantNo, dateDebut, dateFin);
	}

	public IEnumerable<CaisseAuthorisation> GetAllCaisseAuthorisation(int caisseNo)
	{
		return _caisseAuthorisationRepository.GetAll(caisseNo);
	}

	public CaisseAuthorisation GetCaisseAuthorisation(int caisseNo, int caisseAutNo)
	{
		return _caisseAuthorisationRepository.Get(caisseNo, caisseAutNo);
	}

	public CaisseAuthorisation GetCaisseAuthorisation(int no)
	{
		return _caisseAuthorisationRepository.Get(no);
	}

	public int? CreateCaisseAuth(CaisseAuthorisation caisse)
	{
		if (caisse == null)
		{
			throw new ArgumentNullException("caisse");
		}
		Caisse caisse2 = SocieteManager.Societe.GetCaisse(caisse.CaisseNo);
		if (caisse2 == null)
		{
			throw new ArgumentNullException("Caisse!");
		}
		Caisse caisse3 = SocieteManager.Societe.GetCaisse(caisse.CaisseAuthNo);
		if (caisse3 == null)
		{
			throw new ArgumentNullException("Caisse!");
		}
		if (caisse2.No == caisse3.No)
		{
			throw new ApplicationException("Caisse non autorisée");
		}
		if (SocieteManager.CaisseManager.GetCaisseAuthorisation(caisse.CaisseNo, caisse3.No) != null)
		{
			throw new ApplicationException("La caisse [" + caisse3.Code + "] est déja affectée à la caisse [" + caisse2.Code + "]");
		}
		return _caisseAuthorisationRepository.Create(caisse);
	}

	public void DeleteCaisseAuth(CaisseAuthorisation caisse)
	{
		_caisseAuthorisationRepository.Delete(caisse);
	}
}
