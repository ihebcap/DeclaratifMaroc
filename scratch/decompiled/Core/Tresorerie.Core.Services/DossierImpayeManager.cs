using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Interfaces;
using Tresorerie.Core.Models;
using Tresorerie.Infrastructure;
using Tresorerie.Ressources;

namespace Tresorerie.Core.Services;

public class DossierImpayeManager
{
	private readonly IDossierImpayeRepository _dossierImpayeRepository;

	private readonly ILigneDossierImpayeRepository _ligneDossierImpayeRepository;

	private readonly IAutorisationCaisseRepository _autorisationCaisseRepository;

	private readonly IEcheanceRepository _echeanceRepository;

	private readonly ICommissionImpayeRepository _commissionImpayeRepository;

	private readonly IInteretImpayeRepository _interetImpayeRepository;

	private readonly INoteRepository _noteRepository;

	private readonly NotifyService _notifyService;

	private readonly IEcritureComptaRepository _ecritureComptaRepository;

	private readonly ILicenceApplicationVersion _licenceApplicationVersion;

	public SocieteManager SocieteManager { get; set; }

	public DossierImpayeManager(IDossierImpayeRepository dossierImpyaeRepository, IAutorisationCaisseRepository autorisationCaisseRepository, IEcheanceRepository echeanceRepository, ILigneDossierImpayeRepository ligneDossierImpayeRepository, INoteRepository noteRepository, NotifyService notifyService, ICommissionImpayeRepository commissionImpayeRepository, IInteretImpayeRepository interetImpayeRepository, IEcritureComptaRepository ecritureComptaRepository, ILicenceApplicationVersion licenceApplicationVersion)
	{
		_dossierImpayeRepository = dossierImpyaeRepository ?? throw new ArgumentNullException("dossierImpyaeRepository");
		_autorisationCaisseRepository = autorisationCaisseRepository ?? throw new ArgumentNullException("autorisationCaisseRepository");
		_echeanceRepository = echeanceRepository ?? throw new ArgumentNullException("echeanceRepository");
		_ligneDossierImpayeRepository = ligneDossierImpayeRepository ?? throw new ArgumentNullException("ligneDossierImpayeRepository");
		_noteRepository = noteRepository ?? throw new ArgumentNullException("noteRepository");
		_notifyService = notifyService ?? throw new ArgumentNullException("notifyService");
		_commissionImpayeRepository = commissionImpayeRepository ?? throw new ArgumentNullException("commissionImpayeRepository");
		_interetImpayeRepository = interetImpayeRepository ?? throw new ArgumentNullException("interetImpayeRepository");
		_ecritureComptaRepository = ecritureComptaRepository ?? throw new ArgumentNullException("ecritureComptaRepository");
		_licenceApplicationVersion = licenceApplicationVersion ?? throw new ArgumentNullException("licenceApplicationVersion");
	}

	public Task<DossierImpaye> GetByCommissionNoAsync(int commissionNo)
	{
		return _dossierImpayeRepository.GetByCommissionNoAsync(commissionNo);
	}

	public Task<DossierImpaye> GetByInteretNoAsync(int interetNo)
	{
		return _dossierImpayeRepository.GetByInteretNoAsync(interetNo);
	}

	public Task<IList<EcritureComptable>> GetEcrituresCommissionsAsync(int commissionNo)
	{
		return Task.Run(() => _ecritureComptaRepository.GetAll(commissionNo, MouvementDomaine.CommissionImpaye));
	}

	public Task<IList<EcritureComptable>> GetEcrituresInteretsAsync(int interetNo)
	{
		return Task.Run(() => _ecritureComptaRepository.GetAll(interetNo, MouvementDomaine.InteretImpaye));
	}

	public async Task<IList<DossierImpaye>> GetAllAsync(ErpDomaine domaine)
	{
		Societe societe = SocieteManager.Societe;
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		if (utilisateur.IsAdmin)
		{
			return (await _dossierImpayeRepository.GetAllAsync(societe.No, domaine).ConfigureAwait(continueOnCapturedContext: false)).ToList();
		}
		int[] caissesNo = (from a in _autorisationCaisseRepository.GetAll(utilisateur.No, societe.No, (domaine != ErpDomaine.Vente) ? ProfilType.Grf : ProfilType.Grc)
			select a.CaisseNo).ToArray();
		return (await _dossierImpayeRepository.GetAllAsync(societe.No, caissesNo, domaine).ConfigureAwait(continueOnCapturedContext: false)).ToList();
	}

	public IList<DossierImpaye> GetAll(ErpDomaine domaine)
	{
		Societe societe = SocieteManager.Societe;
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		if (utilisateur.IsAdmin)
		{
			return _dossierImpayeRepository.GetAll(societe.No, domaine).ToList();
		}
		int[] caissesNo = (from a in _autorisationCaisseRepository.GetAll(utilisateur.No, societe.No, (domaine != ErpDomaine.Vente) ? ProfilType.Grf : ProfilType.Grc)
			select a.CaisseNo).ToArray();
		return _dossierImpayeRepository.GetAll(societe.No, caissesNo, domaine).ToList();
	}

	public async Task<IList<LigneDossierImpaye>> GetAllLignesAsync(int dossierNo)
	{
		return (await _ligneDossierImpayeRepository.GetAllAsync(dossierNo).ConfigureAwait(continueOnCapturedContext: false)).ToList();
	}

	public async Task<IList<LigneDossierImpaye>> GetAllLignesReglementsAsync(int dossierNo)
	{
		return (await _ligneDossierImpayeRepository.GetAllAsync(dossierNo, TypeLigneDossierImp.Reglement).ConfigureAwait(continueOnCapturedContext: false)).ToList();
	}

	public IList<LigneDossierImpaye> GetAllLignesReglements(int dossierNo)
	{
		return _ligneDossierImpayeRepository.GetAll(dossierNo, TypeLigneDossierImp.Reglement).ToList();
	}

	public async Task<IList<LigneDossierImpaye>> GetAllLignesImpayesAsync(int dossierNo)
	{
		return (await _ligneDossierImpayeRepository.GetAllAsync(dossierNo, TypeLigneDossierImp.Impaye).ConfigureAwait(continueOnCapturedContext: false)).ToList();
	}

	public IList<LigneDossierImpaye> GetAllLignesImpayes(int dossierNo)
	{
		return _ligneDossierImpayeRepository.GetAll(dossierNo, TypeLigneDossierImp.Impaye).ToList();
	}

	public void RetirerLigne(int dossierNo, int ligneNo)
	{
		DossierImpaye dossierImpaye = _dossierImpayeRepository.Get(dossierNo);
		if (dossierImpaye == null)
		{
			throw new ApplicationException($"Impossible de charger le dossier [{dossierNo}]");
		}
		LigneDossierImpaye ligneImpaye = GetLigneImpaye(ligneNo);
		if (ligneImpaye == null)
		{
			throw new ArgumentNullException("ligne");
		}
		if (ligneImpaye.DossierImpayeNo != dossierNo)
		{
			throw new ArgumentNullException($"La ligne [{ligneImpaye.No}] n'appartient pas à la dossier [{dossierImpaye.Numero}]");
		}
		if (dossierImpaye.Statut == StatutDossierImpaye.Valide)
		{
			throw new ApplicationException("Le dossier est validé.");
		}
		CaisseManager caisseManager = SocieteManager.CaisseManager;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_ligneDossierImpayeRepository.DeleteLigne(ligneNo);
		if (ligneImpaye.Type == TypeLigneDossierImp.Impaye)
		{
			SocieteManager.EcheanceAnnulerReservation(ligneImpaye.EntityNo);
		}
		else if (ligneImpaye.Type == TypeLigneDossierImp.Reglement)
		{
			caisseManager.ReglementFournisseurAnnulerReservation(ligneImpaye.EntityNo);
		}
		_notifyService.Notify(TypeEntity.DossierImpayeFournisseur, ligneImpaye.DossierImpayeNo, TypeAction.Modification, dossierImpaye.SocieteNo);
		transactionScope.Complete();
	}

	public void UpdateLigne(int ligneNo, decimal montantAImputer, int ordre)
	{
		LigneDossierImpaye ligneImpaye = GetLigneImpaye(ligneNo);
		if (ligneImpaye == null)
		{
			throw new ArgumentNullException("ligne");
		}
		DossierImpaye dossierImpaye = _dossierImpayeRepository.Get(ligneImpaye.DossierImpayeNo);
		if (dossierImpaye == null)
		{
			throw new ApplicationException($"Impossible de charger le dossier [{ligneImpaye.DossierImpayeNo}]");
		}
		if (dossierImpaye.Statut == StatutDossierImpaye.Valide)
		{
			throw new ApplicationException("Le dossier est validé.");
		}
		if (ligneImpaye.MontantAImputer < 0m)
		{
			throw new ApplicationException("Montant à imputer Invalide");
		}
		_ligneDossierImpayeRepository.UpdateLigne(ligneNo, montantAImputer, ordre);
		_notifyService.Notify(TypeEntity.DossierImpayeFournisseur, ligneImpaye.DossierImpayeNo, TypeAction.Modification, dossierImpaye.SocieteNo);
	}

	public async Task<LigneDossierImpaye> GetLigneImpayeAsync(int ligneNo)
	{
		return await _ligneDossierImpayeRepository.GetAsync(ligneNo).ConfigureAwait(continueOnCapturedContext: false);
	}

	public LigneDossierImpaye GetLigneImpaye(int ligneNo)
	{
		return _ligneDossierImpayeRepository.Get(ligneNo);
	}

	public async Task<IList<LigneDossierImpaye>> GetAllLignesImpayesByEcheanceNoAsync(int echeanceNo, Societe societe = null)
	{
		societe = societe ?? SocieteManager.Societe;
		return (await _ligneDossierImpayeRepository.GetAllByEcheanceNoAsync(echeanceNo, societe.No).ConfigureAwait(continueOnCapturedContext: false)).ToList();
	}

	public Task<DossierImpaye> GetAsync(int dossierNo)
	{
		return _dossierImpayeRepository.GetAsync(dossierNo);
	}

	public DossierImpaye Get(int dossierNo)
	{
		return _dossierImpayeRepository.Get(dossierNo);
	}

	public Task<CommissionImpaye> CommissionImpayeGetAsync(int commissionImpayeNo)
	{
		return _commissionImpayeRepository.GetAsync(commissionImpayeNo);
	}

	public LigneDossierImpaye GetLigneImpayeByReglementNo(int reglementNo)
	{
		Societe societe = SocieteManager.Societe;
		return _ligneDossierImpayeRepository.GetByReglementNo(reglementNo, societe.No);
	}

	public IList<LigneDossierImpaye> GetLignesImpayeByEcheanceNo(int echeanceNo)
	{
		Societe societe = SocieteManager.Societe;
		return _ligneDossierImpayeRepository.GetAllByEcheanceNo(echeanceNo, societe.No).ToList();
	}

	public IList<LigneDossierImpaye> GetLignesImpayeFournisseurByEcheanceNo(int echeanceNo)
	{
		Societe societe = SocieteManager.Societe;
		return _ligneDossierImpayeRepository.GetAllByEcheanceAchatNo(echeanceNo, societe.No).ToList();
	}

	public Task<InteretImpaye> InteretImpayeGetAsync(int interetImpayeNo)
	{
		return _interetImpayeRepository.GetAsync(interetImpayeNo);
	}

	public async Task<IList<EcritureComptable>> GetEcrituresDossierAsync(int dossierNo)
	{
		DossierImpaye dossier = await _dossierImpayeRepository.GetAsync(dossierNo).ConfigureAwait(continueOnCapturedContext: false);
		if (dossier == null)
		{
			throw new ApplicationException($"Impossible de charger le dossier [{dossierNo}].");
		}
		List<EcritureComptable> ecrituresDossier = new List<EcritureComptable>();
		if (!dossier.IsComptabilise)
		{
			return ecrituresDossier;
		}
		IEnumerable<LigneDossierImpaye> source = await _ligneDossierImpayeRepository.GetAllAsync(dossier.No).ConfigureAwait(continueOnCapturedContext: false);
		List<LigneDossierImpaye> list = source.Where((LigneDossierImpaye x) => x.Type == TypeLigneDossierImp.Reglement).ToList();
		CaisseManager caisseManager = SocieteManager.CaisseManager;
		foreach (LigneDossierImpaye item in list)
		{
			IEnumerable<EcritureComptable> enumerable;
			if (dossier.Domaine != ErpDomaine.Vente)
			{
				enumerable = caisseManager.GetEcrituresReglementFournisseur(item.EntityNo);
			}
			else
			{
				IEnumerable<EcritureComptable> ecrituresReglementClient = caisseManager.GetEcrituresReglementClient(item.EntityNo);
				enumerable = ecrituresReglementClient;
			}
			IEnumerable<EcritureComptable> enumerable2 = enumerable;
			if (enumerable2 == null)
			{
				throw new ApplicationException($"Impossible de charger les écritures du règlement [{item.EntityNo}] Dossier [{dossier.Numero}].");
			}
			ecrituresDossier.AddRange(enumerable2);
		}
		foreach (LigneDossierImpaye item2 in source.Where((LigneDossierImpaye x) => x.Type == TypeLigneDossierImp.Impaye).ToList())
		{
			IEnumerable<EcritureComptable> enumerable3;
			if (dossier.Domaine == ErpDomaine.Vente)
			{
				enumerable3 = caisseManager.GetEcrituresImpaye(item2.EntityNo);
			}
			else
			{
				ImpayeFournisseur impayeFournisseur = caisseManager.ImpayeFournisseurGetByEcheance(item2.EntityNo) ?? throw new ApplicationException($"Impossible de charger l'impaye [{item2.EntityNo}] Dossier [{dossier.Numero}].");
				enumerable3 = caisseManager.GetEcrituresImpayeFournisseur(impayeFournisseur.No);
			}
			if (enumerable3 == null)
			{
				throw new ApplicationException($"Impossible de charger les écritures de l'impayé [{item2.EntityNo}] Dossier [{dossier.Numero}].");
			}
			ecrituresDossier.AddRange(enumerable3);
		}
		if (dossier.InteretNo.HasValue)
		{
			int value = dossier.InteretNo.Value;
			IList<EcritureComptable> all = _ecritureComptaRepository.GetAll(value, MouvementDomaine.InteretImpaye);
			if (all == null)
			{
				throw new ApplicationException($"Impossible de charger les écritures de l'intérêt [{value}] Dossier [{dossier.Numero}].");
			}
			ecrituresDossier.AddRange(all);
		}
		if (dossier.CommissionNo.HasValue)
		{
			int value2 = dossier.CommissionNo.Value;
			IList<EcritureComptable> all2 = _ecritureComptaRepository.GetAll(value2, MouvementDomaine.CommissionImpaye);
			if (all2 == null)
			{
				throw new ApplicationException($"Impossible de charger les écritures de la commision [{value2}] Dossier [{dossier.Numero}].");
			}
			ecrituresDossier.AddRange(all2);
		}
		return ecrituresDossier;
	}

	public Task<IEnumerable<DossierImpaye>> GetAllAComptaAsync(DateTime dateMin, DateTime dateMax, bool isComptabilise, int[] caissesNo, ErpDomaine domaine, CancellationToken cancellationToken, Societe societe = null)
	{
		societe = societe ?? SocieteManager.Societe;
		return _dossierImpayeRepository.GetAllAComptaAsync(dateMin.Date, dateMax.Date.AddDays(1.0), isComptabilise, caissesNo, societe.No, domaine, cancellationToken);
	}

	public async Task LettrerAsync(int dossierNo, string lettre)
	{
		if (string.IsNullOrEmpty(lettre))
		{
			throw new ArgumentNullException("lettre");
		}
		DossierImpaye dossier = await _dossierImpayeRepository.GetAsync(dossierNo).ConfigureAwait(continueOnCapturedContext: false);
		if (dossier == null)
		{
			throw new ApplicationException($"Impossible de charger le dossier [{dossierNo}].");
		}
		if (!dossier.IsComptabilise)
		{
			throw new ApplicationException("Le dossier [" + dossier.Numero + "] n'est pas comptabilisé.");
		}
		if (dossier.Lettrage != LettrageType.NonLettre)
		{
			throw new ApplicationException("Le dossier [" + dossier.Numero + "] est lettré.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transaction = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		dossier.Lettrage = LettrageType.Lettre;
		dossier.Lettre = lettre;
		dossier.DateModification = DateTime.Now;
		dossier.ModificateurNo = utilisateur.No;
		await _dossierImpayeRepository.UpdateEtatLettrageAsync(dossier).ConfigureAwait(continueOnCapturedContext: false);
		_notifyService.Notify(TypeEntity.DossierImpaye, dossierNo, TypeAction.Modification, dossier.SocieteNo);
		transaction.Complete();
	}

	public async Task DeLettrerAsync(int dossierNo)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		DossierImpaye dossier = await _dossierImpayeRepository.GetAsync(dossierNo).ConfigureAwait(continueOnCapturedContext: false);
		if (dossier == null)
		{
			throw new ApplicationException($"Impossible de charger le dossier [{dossierNo}].");
		}
		if (!dossier.IsComptabilise)
		{
			throw new ApplicationException("Le dossier [" + dossier.Numero + "] n'est pas comptabilisé.");
		}
		if (dossier.Lettrage == LettrageType.NonLettre)
		{
			throw new ApplicationException("Le dossier [" + dossier.Numero + "] n'est pas lettré.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transaction = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		dossier.Lettrage = LettrageType.NonLettre;
		dossier.Lettre = string.Empty;
		dossier.DateModification = DateTime.Now;
		dossier.ModificateurNo = utilisateur.No;
		await _dossierImpayeRepository.UpdateEtatLettrageAsync(dossier).ConfigureAwait(continueOnCapturedContext: false);
		_notifyService.Notify(TypeEntity.DossierImpaye, dossierNo, TypeAction.Modification, dossier.SocieteNo);
		transaction.Complete();
	}

	public async Task<int> CreateAsync(DateTime date, int caisseNo, int tiersNo, string tiersCode, string tiersIntitule, TiersType tiersType, string commentaire, decimal tauxInteret, decimal montantInteret, decimal montantCommission, int nombreJour, ErpDomaine domaine, IList<LigneDossierImpaye> lignes = null)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		Societe societe = SocieteManager.Societe;
		EntityNumerotation numerotation = EntityNumerotation.DossierImpaye;
		if (domaine == ErpDomaine.Achat)
		{
			numerotation = EntityNumerotation.DossierImpayeFournisseur;
		}
		string numeroPieceCourante = SocieteManager.GetNumeroPieceCourante(numerotation);
		if (string.IsNullOrEmpty(numeroPieceCourante))
		{
			throw new ApplicationException("Veuillez configurer la numérotation du dossier règlement impayé!");
		}
		Caisse caisse = societe.GetCaisse(caisseNo);
		if (caisse == null)
		{
			throw new ApplicationException("Impossible de charger la caisse du dossier règlement impayé [" + numeroPieceCourante + "]!");
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Impossible de créer le dossier règlement impayé [" + numeroPieceCourante + "]! La caisse [" + caisse.Intitule + "] est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisseNo))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		IList<LigneDossierImpaye> lignesImpayes = null;
		IList<LigneDossierImpaye> lignesReglements = null;
		if (lignes != null)
		{
			if (!lignes.Any())
			{
				throw new ApplicationException("Le dossier règlement impayé [" + numeroPieceCourante + "] ne possède aucune ligne!");
			}
			if (lignes.Any((LigneDossierImpaye x) => x.MontantAImputer <= 0m))
			{
				throw new ApplicationException("Une ou plusieurs lignes du dossier règlement impayé [" + numeroPieceCourante + "] possèdent des montants invalide!");
			}
			lignesReglements = (from x in lignes
				where x.Type == TypeLigneDossierImp.Reglement
				orderby x.Ordre
				select x).ToList();
			lignesImpayes = (from x in lignes
				where x.Type == TypeLigneDossierImp.Impaye
				orderby x.Ordre
				select x).ToList();
			decimal num = lignesImpayes.Sum((LigneDossierImpaye x) => x.MontantAImputer) + montantCommission + montantInteret;
			decimal num2 = lignesReglements.Sum((LigneDossierImpaye x) => x.MontantAImputer);
			if (num != num2)
			{
				throw new ApplicationException("Le dossier règlement impayé [" + numeroPieceCourante + "] est non équilibré!");
			}
		}
		SocieteDevise deviseSociete = societe.GetDefaultDeviseSociete();
		if (deviseSociete == null)
		{
			throw new ApplicationException("Impossible de charger la devise société!");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transaction = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		int dossierNo = await CreateEnteteAsync(numeroPieceCourante, date, caisseNo, tiersNo, tiersCode, tiersIntitule, tiersType, commentaire, tauxInteret, montantInteret, montantCommission, nombreJour, deviseSociete.No, domaine).ConfigureAwait(continueOnCapturedContext: false);
		if (lignes != null)
		{
			await CreateLignesAsync(dossierNo, lignes).ConfigureAwait(continueOnCapturedContext: false);
			await CreateAffectationsAsync(dossierNo, lignesReglements, lignesImpayes, deviseSociete.No).ConfigureAwait(continueOnCapturedContext: false);
		}
		_notifyService.Notify((domaine == ErpDomaine.Vente) ? TypeEntity.DossierImpaye : TypeEntity.DossierImpayeFournisseur, dossierNo, TypeAction.Ajout, societe.No);
		transaction.Complete();
		return dossierNo;
	}

	public async Task UpdateAsync(int dossierNo, string commentaire)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		DossierImpaye dossier = await _dossierImpayeRepository.GetAsync(dossierNo).ConfigureAwait(continueOnCapturedContext: false);
		if (dossier == null)
		{
			throw new ApplicationException($"Impossible de charger le dossier règlement impayé [{dossierNo}].");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transaction = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		dossier.Commentaire = commentaire;
		dossier.DateModification = DateTime.Now;
		dossier.ModificateurNo = utilisateur.No;
		await _dossierImpayeRepository.UpdateAsync(dossier).ConfigureAwait(continueOnCapturedContext: false);
		_notifyService.Notify(TypeEntity.DossierImpaye, dossier.No, TypeAction.Modification, dossier.SocieteNo);
		transaction.Complete();
	}

	public void Update(int dossierNo, string commentaire, decimal montantCommission, decimal montantInteret, int nbJour)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		DossierImpaye dossierImpaye = _dossierImpayeRepository.Get(dossierNo);
		if (dossierImpaye == null)
		{
			throw new ApplicationException($"Impossible de charger le dossier règlement impayé [{dossierNo}].");
		}
		if (dossierImpaye.Statut == StatutDossierImpaye.Valide)
		{
			throw new ArgumentNullException("Le dossier n°[" + dossierImpaye.Numero + "] est validé.");
		}
		if (montantCommission < 0m)
		{
			throw new ApplicationException("Montant de commission est invalide.");
		}
		if (montantInteret < 0m)
		{
			throw new ApplicationException("Montant de l'intérêt est invalide.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		dossierImpaye.Commentaire = commentaire;
		dossierImpaye.NombreJour = nbJour;
		dossierImpaye.MontantCommission = montantCommission;
		dossierImpaye.MontantInteret = montantInteret;
		dossierImpaye.DateModification = DateTime.Now;
		dossierImpaye.ModificateurNo = utilisateur.No;
		_dossierImpayeRepository.Update(dossierImpaye);
		_notifyService.Notify(TypeEntity.DossierImpayeFournisseur, dossierImpaye.No, TypeAction.Modification, dossierImpaye.SocieteNo);
		transactionScope.Complete();
	}

	public async Task ComptabiliserAsync(int dossierNo)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		DossierImpaye dossier = await _dossierImpayeRepository.GetAsync(dossierNo).ConfigureAwait(continueOnCapturedContext: false);
		if (dossier == null)
		{
			throw new ApplicationException($"Impossible de charger le dossier [{dossierNo}].");
		}
		if (dossier.IsComptabilise)
		{
			throw new ApplicationException("Le dossier [" + dossier.Numero + "] est comptabilisé.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transaction = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		dossier.IsComptabilise = true;
		dossier.DateModification = DateTime.Now;
		dossier.ModificateurNo = utilisateur.No;
		await _dossierImpayeRepository.UpdateEtatComptabiliteAsync(dossier).ConfigureAwait(continueOnCapturedContext: false);
		_notifyService.Notify((dossier.Domaine == ErpDomaine.Vente) ? TypeEntity.DossierImpaye : TypeEntity.DossierImpayeFournisseur, dossier.No, TypeAction.Modification, dossier.SocieteNo);
		transaction.Complete();
	}

	public async Task ComptabiliserCommissionAsync(int commissionNo, IEnumerable<EcritureComptable> erpEcritures)
	{
		if (erpEcritures == null)
		{
			throw new ArgumentNullException("erpEcritures");
		}
		_licenceApplicationVersion.ThrowIfGratuit();
		CommissionImpaye commissionImpaye = await _commissionImpayeRepository.GetAsync(commissionNo).ConfigureAwait(continueOnCapturedContext: false);
		if (commissionImpaye == null)
		{
			throw new InvalidOperationException($"Impossible de charger la commission [{commissionNo}].");
		}
		if (commissionImpaye.IsComptabilise)
		{
			throw new ApplicationException("La commission [" + commissionImpaye.DocumentNumero + "] est déjà comptabilisé!");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transaction = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		await _ecritureComptaRepository.CreateAsync(erpEcritures).ConfigureAwait(continueOnCapturedContext: false);
		await _commissionImpayeRepository.ComptabiliserAsync(commissionNo).ConfigureAwait(continueOnCapturedContext: false);
		_notifyService.Notify(TypeEntity.Echeance, commissionNo, TypeAction.Modification, commissionImpaye.SocieteNo);
		transaction.Complete();
	}

	public async Task ComptabiliserInteretAsync(int interetNo, IEnumerable<EcritureComptable> erpEcritures)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		if (erpEcritures == null)
		{
			throw new ArgumentNullException("erpEcritures");
		}
		InteretImpaye interetImpaye = await _interetImpayeRepository.GetAsync(interetNo).ConfigureAwait(continueOnCapturedContext: false);
		if (interetImpaye == null)
		{
			throw new InvalidOperationException($"Impossible de charger l'intérêt [{interetNo}].");
		}
		if (interetImpaye.IsComptabilise)
		{
			throw new ApplicationException("L'intérêt [" + interetImpaye.DocumentNumero + "] est déjà comptabilisé!");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transaction = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		await _ecritureComptaRepository.CreateAsync(erpEcritures).ConfigureAwait(continueOnCapturedContext: false);
		await _interetImpayeRepository.ComptabiliserAsync(interetNo).ConfigureAwait(continueOnCapturedContext: false);
		_notifyService.Notify(TypeEntity.Echeance, interetNo, TypeAction.Modification, interetImpaye.SocieteNo);
		transaction.Complete();
	}

	public async Task DeComptabiliserCommissionAsync(int commissionNo)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		CommissionImpaye commissionImpaye = await _commissionImpayeRepository.GetAsync(commissionNo).ConfigureAwait(continueOnCapturedContext: false);
		if (commissionImpaye == null)
		{
			throw new InvalidOperationException($"Impossible de charger la commission [{commissionNo}].");
		}
		if (!commissionImpaye.IsComptabilise)
		{
			throw new ApplicationException("La commission [" + commissionImpaye.DocumentNumero + "] n'est pas comptabilisé!");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transaction = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		_ecritureComptaRepository.DeleteLigne(commissionNo, MouvementDomaine.CommissionImpaye);
		await _commissionImpayeRepository.DecomptabiliserAsync(commissionNo).ConfigureAwait(continueOnCapturedContext: false);
		_notifyService.Notify(TypeEntity.Echeance, commissionNo, TypeAction.Modification, commissionImpaye.SocieteNo);
		transaction.Complete();
	}

	public async Task DeComptabiliserInteretAsync(int interetNo)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		InteretImpaye interetImpaye = await _interetImpayeRepository.GetAsync(interetNo).ConfigureAwait(continueOnCapturedContext: false);
		if (interetImpaye == null)
		{
			throw new InvalidOperationException($"Impossible de charger l'intérêt [{interetNo}].");
		}
		if (!interetImpaye.IsComptabilise)
		{
			throw new ApplicationException("L'intérêt [" + interetImpaye.DocumentNumero + "] n'est pas comptabilisé!");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transaction = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		_ecritureComptaRepository.DeleteLigne(interetNo, MouvementDomaine.InteretImpaye);
		await _interetImpayeRepository.DecomptabiliserAsync(interetNo).ConfigureAwait(continueOnCapturedContext: false);
		_notifyService.Notify(TypeEntity.Echeance, interetNo, TypeAction.Modification, interetImpaye.SocieteNo);
		transaction.Complete();
	}

	public async Task DecomptabiliserAsync(int dossierNo)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		DossierImpaye dossier = await _dossierImpayeRepository.GetAsync(dossierNo).ConfigureAwait(continueOnCapturedContext: false);
		if (dossier == null)
		{
			throw new ApplicationException($"Impossible de charger le dossier [{dossierNo}].");
		}
		if (!dossier.IsComptabilise)
		{
			throw new ApplicationException("Le dossier [" + dossier.Numero + "] n'est pas comptabilisé.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transaction = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		dossier.IsComptabilise = false;
		dossier.DateModification = DateTime.Now;
		dossier.ModificateurNo = utilisateur.No;
		await _dossierImpayeRepository.UpdateEtatComptabiliteAsync(dossier).ConfigureAwait(continueOnCapturedContext: false);
		_notifyService.Notify(TypeEntity.DossierImpaye, dossier.No, TypeAction.Modification, dossier.SocieteNo);
		transaction.Complete();
	}

	public void Delete(int dossierNo)
	{
		Task.Run(() => DeleteAsync(dossierNo));
	}

	public async Task DeleteAsync(int dossierNo)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		DossierImpaye dossier = await _dossierImpayeRepository.GetAsync(dossierNo).ConfigureAwait(continueOnCapturedContext: false);
		if (dossier == null)
		{
			throw new ApplicationException($"Impossible de charger le dossier [{dossierNo}].");
		}
		if (dossier.IsComptabilise)
		{
			throw new ApplicationException("Le dossier n°[" + dossier.Numero + "] est comptabilisé.");
		}
		if (dossier.Statut == StatutDossierImpaye.Valide)
		{
			throw new ApplicationException("Le dossier n°[" + dossier.Numero + "] est validé.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transaction = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		await DeleteAffectationsAsync(dossier).ConfigureAwait(continueOnCapturedContext: false);
		Echeance echeanceCommission = null;
		if (dossier.CommissionNo.HasValue)
		{
			echeanceCommission = _echeanceRepository.Get(dossier.CommissionNo.Value);
			if (echeanceCommission == null)
			{
				throw new ApplicationException("Impossible de charger l'échéance de la commission du dossier [" + dossier.Numero + "].");
			}
			if (echeanceCommission.IsComptabilise)
			{
				throw new ApplicationException("L'échéance de la commission du dossier [" + dossier.Numero + "] est comptabilisée.");
			}
		}
		Echeance echeance = null;
		if (dossier.InteretNo.HasValue)
		{
			echeance = _echeanceRepository.Get(dossier.InteretNo.Value);
			if (echeance == null)
			{
				throw new ApplicationException("Impossible de charger l'échéance de l'intérêt du dossier [" + dossier.Numero + "].");
			}
			if (echeance.IsComptabilise)
			{
				throw new ApplicationException("L'échéance de l'intérêt du dossier [" + dossier.Numero + "] est comptabilisée.");
			}
		}
		if (echeance != null)
		{
			await EcheanceDeleteAsync(dossier, echeance).ConfigureAwait(continueOnCapturedContext: false);
		}
		if (echeanceCommission != null)
		{
			await EcheanceDeleteAsync(dossier, echeanceCommission).ConfigureAwait(continueOnCapturedContext: false);
		}
		await _dossierImpayeRepository.DeleteAsync(dossier).ConfigureAwait(continueOnCapturedContext: false);
		if (dossier.Domaine == ErpDomaine.Vente)
		{
			_notifyService.Notify(TypeEntity.DossierImpaye, dossier.No, TypeAction.Suppression, dossier.SocieteNo);
		}
		else if (dossier.Domaine == ErpDomaine.Achat)
		{
			_notifyService.Notify(TypeEntity.DossierImpayeFournisseur, dossier.No, TypeAction.Suppression, dossier.SocieteNo);
		}
		transaction.Complete();
	}

	private async Task DeleteAffectationsAsync(DossierImpaye dossier)
	{
		if (dossier == null)
		{
			throw new ArgumentNullException("dossier");
		}
		_licenceApplicationVersion.ThrowIfGratuit();
		List<LigneDossierImpaye> source = (await _ligneDossierImpayeRepository.GetAllAsync(dossier.No).ConfigureAwait(continueOnCapturedContext: false)).ToList();
		List<LigneDossierImpaye> list = source.Where((LigneDossierImpaye x) => x.Type == TypeLigneDossierImp.Reglement).ToList();
		List<LigneDossierImpaye> lignesImpayes = source.Where((LigneDossierImpaye x) => x.Type == TypeLigneDossierImp.Impaye).ToList();
		CaisseManager caisseManager = SocieteManager.CaisseManager;
		SocieteDevise defaultDeviseSociete = SocieteManager.Societe.GetDefaultDeviseSociete();
		if (defaultDeviseSociete == null)
		{
			throw new ApplicationException("Impossible de charger la devise société.");
		}
		IList<Echeance> echeances = GetEcheancesDelete(dossier, lignesImpayes);
		foreach (LigneDossierImpaye item in list)
		{
			if (dossier.Domaine == ErpDomaine.Vente)
			{
				ReglementClient reglementClient = caisseManager.ReglementGet(item.EntityNo);
				if (reglementClient == null)
				{
					throw new ApplicationException("Impossible de charger le règlement [" + reglementClient.Numero + "].");
				}
				foreach (Affectation item2 in from aff in reglementClient.GetAffectations()
					where echeances.Any((Echeance ech) => ech.No == aff.EcheanceNo)
					select aff)
				{
					caisseManager.AffectationDelete(item.EntityNo, item2.No, defaultDeviseSociete.No, isDossierImpaye: true);
				}
			}
			else
			{
				if (dossier.Domaine != ErpDomaine.Achat)
				{
					continue;
				}
				ReglementFournisseur reglementFournisseur = caisseManager.ReglementFournisseurGet(item.EntityNo);
				if (reglementFournisseur == null)
				{
					throw new ApplicationException("Impossible de charger le règlement [" + reglementFournisseur.Numero + "].");
				}
				foreach (Affectation item3 in from aff in reglementFournisseur.GetAffectations()
					where echeances.Any((Echeance ech) => ech.No == aff.EcheanceNo)
					select aff)
				{
					caisseManager.AffectationFournisseurDelete(item.EntityNo, item3.No, defaultDeviseSociete.No, isDossierImpaye: true);
				}
			}
		}
	}

	public IList<Echeance> GetEcheancesDelete(DossierImpaye dossier, IList<LigneDossierImpaye> lignesImpayes)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		if (dossier == null)
		{
			throw new ArgumentNullException("dossier");
		}
		if (lignesImpayes == null)
		{
			throw new ArgumentNullException("lignesImpayes");
		}
		CaisseManager caisseManager = SocieteManager.CaisseManager;
		List<Echeance> list = new List<Echeance>();
		if (dossier.CommissionNo.HasValue)
		{
			Echeance echeance = SocieteManager.EcheanceGet(dossier.CommissionNo.Value);
			if (echeance == null)
			{
				throw new ApplicationException("Impossible de charger la commission d'impayé [" + dossier.Numero + "]!");
			}
			list.Add(echeance);
		}
		if (dossier.InteretNo.HasValue)
		{
			Echeance echeance2 = SocieteManager.EcheanceGet(dossier.InteretNo.Value);
			if (echeance2 == null)
			{
				throw new ApplicationException("Impossible de charger l'intérêt d'impayé [" + dossier.Numero + "]!");
			}
			list.Add(echeance2);
		}
		foreach (LigneDossierImpaye lignesImpaye in lignesImpayes)
		{
			int no = 0;
			string text = "";
			if (dossier.Domaine == ErpDomaine.Vente)
			{
				Impaye obj = caisseManager.ImpayeGet(lignesImpaye.EntityNo) ?? throw new ApplicationException($"Impossible de charger la ligne impayé [{lignesImpaye.EntityNo}] du dossier règlement impayé [{dossier.Numero}]!");
				no = obj.EcheanceNo;
				text = obj.Numero;
			}
			else if (dossier.Domaine == ErpDomaine.Achat)
			{
				ImpayeFournisseur obj2 = caisseManager.ImpayeFournisseurGetByEcheance(lignesImpaye.EntityNo) ?? throw new ApplicationException($"Impossible de charger la ligne impayé [{lignesImpaye.EntityNo}] du dossier règlement impayé [{dossier.Numero}]!");
				no = obj2.EcheanceNo;
				text = obj2.Numero;
			}
			Echeance echeance3 = SocieteManager.EcheanceGet(no);
			if (echeance3 == null)
			{
				throw new ApplicationException("Impossible de charger l'impayé [" + text + "]!");
			}
			list.Add(echeance3);
		}
		return list;
	}

	private async Task EcheanceDeleteAsync(DossierImpaye dossier, Echeance echeance)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		if (dossier == null)
		{
			throw new ArgumentNullException("dossier");
		}
		if (echeance == null)
		{
			throw new ArgumentNullException("echeance");
		}
		if (echeance.Type != EcheanceType.CommissionImpaye && echeance.Type != EcheanceType.InteretImpaye)
		{
			throw new InvalidOperationException($"Echéance type[{echeance.DocumentNumero}][{echeance.Type}] invalide!");
		}
		if (echeance.GetAffectations().Any())
		{
			throw new ApplicationException("L'échéance [" + echeance.DocumentNumero + "] a une ou plusieurs affectations!");
		}
		if (echeance.IsComptabilise)
		{
			throw new ApplicationException("L'échéance [" + echeance.DocumentNumero + "] est comptabilisé!");
		}
		foreach (Note item in _noteRepository.GetAllNotesByEntite(echeance.No, TypeEntity.Echeance))
		{
			_noteRepository.Delete(item);
		}
		switch (echeance.Type)
		{
		case EcheanceType.CommissionImpaye:
			await _dossierImpayeRepository.DeleteCommission(dossier).ConfigureAwait(continueOnCapturedContext: false);
			break;
		case EcheanceType.InteretImpaye:
			await _dossierImpayeRepository.DeleteInteret(dossier).ConfigureAwait(continueOnCapturedContext: false);
			break;
		default:
			throw new InvalidOperationException($"Echéance type [{echeance.DocumentNumero}][{echeance.Type}] invalide!");
		}
		_echeanceRepository.Delete(echeance);
		_notifyService.Notify(TypeEntity.Echeance, echeance.No, TypeAction.Suppression, echeance.SocieteNo);
	}

	private async Task<IList<Echeance>> GetEcheancesCreateAsync(DossierImpaye dossier, IList<LigneDossierImpaye> lignesImpayes)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		if (dossier == null)
		{
			throw new ArgumentNullException("dossier");
		}
		if (lignesImpayes == null)
		{
			throw new ArgumentNullException("lignesImpayes");
		}
		CaisseManager caisseManager = SocieteManager.CaisseManager;
		_ = SocieteManager.Societe;
		List<Echeance> listEcheances = new List<Echeance>();
		if (dossier.CommissionNo.HasValue)
		{
			Echeance echeance = SocieteManager.EcheanceGet(dossier.CommissionNo.Value);
			if (echeance == null)
			{
				throw new ApplicationException("Impossible de charger la commission d'impayé [" + dossier.Numero + "]!");
			}
			if (echeance.Montant != echeance.Solde)
			{
				throw new ApplicationException("La commission d'impayé [" + dossier.Numero + "] est partiellement/totalement payée!");
			}
			listEcheances.Add(echeance);
		}
		if (dossier.InteretNo.HasValue)
		{
			Echeance echeance2 = SocieteManager.EcheanceGet(dossier.InteretNo.Value);
			if (echeance2 == null)
			{
				throw new ApplicationException("Impossible de charger l'intérêt d'impayé [" + dossier.Numero + "]!");
			}
			if (echeance2.Montant != echeance2.Solde)
			{
				throw new ApplicationException("L'intérêt d'impayé [" + dossier.Numero + "] est partiellement/totalement payé!");
			}
			listEcheances.Add(echeance2);
		}
		foreach (LigneDossierImpaye lignesImpaye in lignesImpayes)
		{
			if (dossier.Domaine == ErpDomaine.Vente)
			{
				Impaye impaye = caisseManager.ImpayeGet(lignesImpaye.EntityNo);
				if (impaye == null)
				{
					throw new ApplicationException($"Impossible de charger la ligne impayé [{lignesImpaye.EntityNo}] du dossier règlement impayé [{dossier.Numero}]!");
				}
				if (impaye.Etat == Etat.TotalementPaye)
				{
					throw new ApplicationException("L'impayé [" + impaye.Numero + "] est totalement soldé!");
				}
				if (impaye.SoldeImpaye < lignesImpaye.MontantAImputer)
				{
					throw new ApplicationException("Le solde de l'impayé [" + impaye.Numero + "] est insuffisant !");
				}
				Echeance echeance3 = SocieteManager.EcheanceGet(impaye.EcheanceNo);
				if (echeance3 == null)
				{
					throw new ApplicationException("Impossible de charger l'impayé [" + impaye.Numero + "]!");
				}
				listEcheances.Add(echeance3);
				lignesImpaye.No = echeance3.No;
				decimal num = (await _ligneDossierImpayeRepository.GetAllByEcheanceNoAsync(impaye.EcheanceNo, impaye.SocieteNo).ConfigureAwait(continueOnCapturedContext: false)).Where((LigneDossierImpaye x) => x.DossierImpayeNo != dossier.No).Sum((LigneDossierImpaye x) => x.MontantAImputer);
				decimal num2 = echeance3.GetAffectations().Sum((Affectation x) => x.Montant);
				if (num != num2)
				{
					throw new ApplicationException("L'impayé [" + impaye.Numero + "] est déjà utilisé dans une affectation sans dossier !");
				}
			}
			else if (dossier.Domaine == ErpDomaine.Achat)
			{
				ImpayeFournisseur impayeFournisseur = caisseManager.ImpayeFournisseurGetByEcheance(lignesImpaye.EntityNo);
				if (impayeFournisseur == null)
				{
					throw new ApplicationException($"Impossible de charger la ligne impayé [{lignesImpaye.EntityNo}] du dossier règlement impayé [{dossier.Numero}]!");
				}
				Echeance echeance4 = SocieteManager.EcheanceGet(impayeFournisseur.EcheanceNo);
				if (echeance4 == null)
				{
					throw new ApplicationException("Impossible de charger l'impayé [" + impayeFournisseur.Numero + "]!");
				}
				listEcheances.Add(echeance4);
				lignesImpaye.No = echeance4.No;
			}
		}
		return listEcheances;
	}

	private IList<ReglementClient> GetReglementClients(DossierImpaye dossier, IList<LigneDossierImpaye> lignesReglements, int deviseSocieteNo)
	{
		Societe societe = SocieteManager.Societe;
		if (dossier == null)
		{
			throw new ArgumentNullException("dossier");
		}
		if (lignesReglements == null)
		{
			throw new ArgumentNullException("lignesReglements");
		}
		CaisseManager caisseManager = SocieteManager.CaisseManager;
		List<ReglementClient> list = new List<ReglementClient>();
		foreach (LigneDossierImpaye lignesReglement in lignesReglements)
		{
			ReglementClient reglementClient = caisseManager.ReglementGet(lignesReglement.EntityNo);
			if (reglementClient == null)
			{
				throw new ApplicationException($"Impossible de charger la ligne règlement [{lignesReglement.EntityNo}] du dossier règlement impayé [{dossier.Numero}]!");
			}
			if (reglementClient.IsComptabilise != EtatComptabilite.NonComptabilise)
			{
				throw new ApplicationException("Le règlement [" + reglementClient.Numero + "] est comptabilisé!");
			}
			if (societe.HasDossierImpRestriction && reglementClient.Montant != lignesReglement.MontantAImputer)
			{
				throw new ApplicationException("Le règlement [" + reglementClient.Numero + "] doit être totalement imputé!");
			}
			if (reglementClient.Solde < lignesReglement.MontantAImputer)
			{
				throw new ApplicationException("Le solde du règlement [" + reglementClient.Numero + "] est insuffisant!");
			}
			if (reglementClient.DeviseNo != deviseSocieteNo)
			{
				throw new ApplicationException("Le dossier règlement impayé ne supporte pas la devise!");
			}
			if (reglementClient.CaisseNo != dossier.CaisseNo)
			{
				throw new ApplicationException("La caisse du règlement [" + reglementClient.Numero + "] n'appartient pas à la caisse du dossier [" + dossier.Numero + "]!");
			}
			if (societe.HasDossierImpRestriction && reglementClient.Solde != reglementClient.Montant)
			{
				throw new ApplicationException("Le règlement [" + reglementClient.Numero + "] est partiellement/totalement soldé!");
			}
			if (reglementClient.IsAnnule)
			{
				throw new ApplicationException("Le règlement [" + reglementClient.Numero + "] est annulé!");
			}
			list.Add(reglementClient);
		}
		return list;
	}

	private IList<ReglementFournisseur> GetReglementFournisseurs(DossierImpaye dossier, IList<LigneDossierImpaye> lignesReglements, int deviseSocieteNo)
	{
		if (dossier == null)
		{
			throw new ArgumentNullException("dossier");
		}
		if (lignesReglements == null)
		{
			throw new ArgumentNullException("lignesReglements");
		}
		CaisseManager caisseManager = SocieteManager.CaisseManager;
		List<ReglementFournisseur> list = new List<ReglementFournisseur>();
		foreach (LigneDossierImpaye lignesReglement in lignesReglements)
		{
			ReglementFournisseur reglementFournisseur = caisseManager.ReglementFournisseurGet(lignesReglement.EntityNo);
			if (reglementFournisseur == null)
			{
				throw new ApplicationException($"Impossible de charger la ligne règlement [{lignesReglement.EntityNo}] du dossier règlement impayé [{dossier.Numero}]!");
			}
			if (reglementFournisseur.IsComptabilise != EtatComptabilite.NonComptabilise)
			{
				throw new ApplicationException("Le règlement [" + reglementFournisseur.Numero + "] est comptabilisé!");
			}
			if (reglementFournisseur.Montant != lignesReglement.MontantAImputer)
			{
				throw new ApplicationException("Le règlement [" + reglementFournisseur.Numero + "] doit être totalement imputé!");
			}
			if (reglementFournisseur.DeviseNo != deviseSocieteNo)
			{
				throw new ApplicationException("Le dossier règlement impayé ne supporte pas la devise!");
			}
			if (reglementFournisseur.CaisseNo != dossier.CaisseNo)
			{
				throw new ApplicationException("La caisse du règlement [" + reglementFournisseur.Numero + "] n'appartient pas à la caisse du dossier [" + dossier.Numero + "]!");
			}
			if (reglementFournisseur.Solde != reglementFournisseur.Montant)
			{
				throw new ApplicationException("Le règlement [" + reglementFournisseur.Numero + "] est partiellement/totalement soldé!");
			}
			if (reglementFournisseur.IsAnnule)
			{
				throw new ApplicationException("Le règlement [" + reglementFournisseur.Numero + "] est annulé!");
			}
			list.Add(reglementFournisseur);
		}
		return list;
	}

	private Task<int> CreateEnteteAsync(string numero, DateTime date, int caisseNo, int tiersNo, string tiersCode, string tiersIntitule, TiersType tiersType, string commentaire, decimal tauxInteret, decimal montantInteret, decimal montantCommission, int nombreJour, int deviseSocieteNo, ErpDomaine domaine)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		Societe societe = SocieteManager.Societe;
		if (tiersNo <= 0)
		{
			throw new ApplicationException("Le tiers est invalide pour le dossier règlement impayé [" + numero + "]!");
		}
		if (string.IsNullOrEmpty(tiersCode))
		{
			throw new ApplicationException("Le tiers code est invalide pour le dossier règlement impayé [" + numero + "]!");
		}
		if (string.IsNullOrEmpty(tiersIntitule))
		{
			throw new ApplicationException("Le tiers intitulé est invalide pour le dossier règlement impayé [" + numero + "]!");
		}
		int? interetNo = null;
		int? commissionNo = null;
		if (domaine == ErpDomaine.Vente)
		{
			SocieteModeReglement societeModeReglement = societe.GetAllModes().FirstOrDefault();
			if (societeModeReglement == null)
			{
				throw new ApplicationException("Veuillez configurer les modes de la société!");
			}
			if (tauxInteret < 0m)
			{
				throw new ApplicationException("Le taux d'intérêt est invalide pour le dossier règlement impayé [" + numero + "]!");
			}
			int? num = societe.DefaultSoucheImpaye;
			if (domaine == ErpDomaine.Achat)
			{
				num = societe.DefaultSoucheImpayeFrs;
			}
			if ((montantCommission != 0m || montantInteret != 0m) && !num.HasValue)
			{
				throw new ApplicationException("Veuillez configurer la souche par défaut des impayés!");
			}
			if (nombreJour < 0)
			{
				throw new ApplicationException("Le nombre de jour est invalide pour le dossier règlement impayé [" + numero + "]!");
			}
			if (montantInteret < 0m)
			{
				throw new ApplicationException("Le montant d'intérêt est invalide pour le dossier règlement impayé [" + numero + "]!");
			}
			interetNo = ((!(montantInteret == 0m)) ? new int?(CreateInteretEcheance(numero, date, tiersNo, tiersCode, tiersIntitule, montantInteret, modeNo: societeModeReglement.No, soucheNo: num.Value, deviseSocieteNo: deviseSocieteNo, domaine: domaine)) : ((int?)null));
			if (montantCommission < 0m)
			{
				throw new ApplicationException("Le montant de commission est invalide pour le dossier règlement impayé [" + numero + "]!");
			}
			commissionNo = ((!(montantCommission == 0m)) ? new int?(CreateCommissionEcheance(numero, date, tiersNo, tiersCode, tiersIntitule, montantCommission, modeNo: societeModeReglement.No, soucheNo: num.Value, deviseSocieteNo: deviseSocieteNo, domaine: domaine)) : ((int?)null));
		}
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		DossierImpaye dossier = new DossierImpaye
		{
			No = 0,
			TiersType = tiersType,
			CaisseNo = caisseNo,
			Commentaire = (commentaire ?? string.Empty),
			Date = date,
			DateCreation = DateTime.Now,
			DateModification = DateTime.Now,
			IsComptabilise = false,
			Lettrage = LettrageType.NonLettre,
			Lettre = string.Empty,
			Numero = numero,
			TauxInteret = tauxInteret,
			SocieteNo = societe.No,
			UtilisateurNo = utilisateur.No,
			ModificateurNo = utilisateur.No,
			InteretNo = interetNo,
			CommissionNo = commissionNo,
			MontantCommission = montantCommission,
			MontantInteret = montantInteret,
			MontantImpaye = 0m,
			TiersIntitule = tiersIntitule,
			TiersNo = tiersNo,
			TiersNumero = tiersCode,
			NombreJour = nombreJour,
			Domaine = domaine
		};
		return _dossierImpayeRepository.CreateAsync(dossier);
	}

	private int CreateInteretEcheance(string numero, DateTime date, int tiersNo, string tiersCode, string tiersIntitule, decimal montantInteret, int soucheNo, int modeNo, int deviseSocieteNo, ErpDomaine domaine)
	{
		return SocieteManager.EcheanceCreate(0, numero, domaine, ErpDocumentType.None, date, tiersNo, tiersCode, tiersIntitule, tiersNo, tiersCode, tiersIntitule, deviseSocieteNo, 1m, montantInteret, date, modeNo, soucheNo, 0, EcheanceType.InteretImpaye, "Intérêt impayé pour le dossier [" + numero + "]", deviseSocieteNo, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
	}

	private int CreateCommissionEcheance(string numero, DateTime date, int tiersNo, string tiersCode, string tiersIntitule, decimal montantCommission, int soucheNo, int modeNo, int deviseSocieteNo, ErpDomaine domaine)
	{
		return SocieteManager.EcheanceCreate(0, numero, domaine, ErpDocumentType.None, date, tiersNo, tiersCode, tiersIntitule, tiersNo, tiersCode, tiersIntitule, deviseSocieteNo, 1m, montantCommission, date, modeNo, soucheNo, 0, EcheanceType.CommissionImpaye, "Commission impayé pour le dossier [" + numero + "]", deviseSocieteNo, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
	}

	private void CreateAffectations(int dossierNo, IList<LigneDossierImpaye> lignesReglements, IList<LigneDossierImpaye> lignesImpayes, int deviseSocieteNo)
	{
		Task.Run(() => CreateAffectationsAsync(dossierNo, lignesReglements, lignesImpayes, deviseSocieteNo));
	}

	private async Task CreateAffectationsAsync(int dossierNo, IList<LigneDossierImpaye> lignesReglements, IList<LigneDossierImpaye> lignesImpayes, int deviseSocieteNo)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		if (lignesReglements == null)
		{
			throw new ArgumentNullException("lignesReglements");
		}
		if (lignesImpayes == null)
		{
			throw new ArgumentNullException("lignesImpayes");
		}
		DossierImpaye dossier = await _dossierImpayeRepository.GetAsync(dossierNo).ConfigureAwait(continueOnCapturedContext: false);
		if (dossier == null)
		{
			throw new ApplicationException($"Impossible de charger le dossier de règlement impayé [{dossierNo}]!");
		}
		IList<Echeance> source = await GetEcheancesCreateAsync(dossier, lignesImpayes).ConfigureAwait(continueOnCapturedContext: false);
		CaisseManager caisseManager = SocieteManager.CaisseManager;
		if (dossier.Domaine == ErpDomaine.Vente)
		{
			foreach (ReglementClient reglementClient in GetReglementClients(dossier, lignesReglements, deviseSocieteNo))
			{
				decimal solde = reglementClient.Solde;
				while (solde > 0m)
				{
					Echeance echeance = source.FirstOrDefault((Echeance x) => x.Solde > 0m);
					decimal num = echeance.Solde;
					if (echeance.Type == EcheanceType.Impaye)
					{
						decimal montantAImputer = lignesImpayes.Single((LigneDossierImpaye x) => x.No == echeance.No).MontantAImputer;
						num = Math.Min(num, montantAImputer);
					}
					decimal num2 = Math.Min(solde, num);
					caisseManager.Imputer(reglementClient.No, echeance.No, num2, deviseSocieteNo, isDossierImpaye: true);
					solde -= num2;
					echeance.Solde -= num2;
				}
			}
			return;
		}
		if (dossier.Domaine != ErpDomaine.Achat)
		{
			return;
		}
		foreach (ReglementFournisseur reglementFournisseur in GetReglementFournisseurs(dossier, lignesReglements, deviseSocieteNo))
		{
			decimal solde2 = reglementFournisseur.Solde;
			while (solde2 > 0m)
			{
				Echeance echeance2 = source.FirstOrDefault((Echeance x) => x.Solde > 0m);
				decimal num3 = echeance2.Solde;
				if (echeance2.Type == EcheanceType.ImpayeFournisseur)
				{
					decimal montantAImputer2 = lignesImpayes.Single((LigneDossierImpaye x) => x.No == echeance2.No).MontantAImputer;
					num3 = Math.Min(num3, montantAImputer2);
				}
				decimal num4 = Math.Min(solde2, num3);
				caisseManager.ReglementFournisseurImputer(reglementFournisseur.No, echeance2.No, num4, deviseSocieteNo, isDossierImpaye: true);
				solde2 -= num4;
				echeance2.Solde -= num4;
			}
		}
	}

	private async Task CreateLignesAsync(int dossierNo, IList<LigneDossierImpaye> lignes)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		if (lignes == null)
		{
			throw new ArgumentNullException("lignes");
		}
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		foreach (LigneDossierImpaye ligne in lignes)
		{
			ligne.DossierImpayeNo = dossierNo;
			ligne.DateCreation = DateTime.Now;
			ligne.UtilisateurNo = utilisateur.No;
			ligne.DateModification = DateTime.Now;
			ligne.ModificateurNo = utilisateur.No;
		}
		await _ligneDossierImpayeRepository.CreateAsync(lignes);
	}

	public async Task<DossierImpaye> GetDossierAsync(string dossierNumero, ErpDomaine domaine, Societe societe = null)
	{
		societe = societe ?? SocieteManager.Societe;
		return await _dossierImpayeRepository.GetAsync(dossierNumero, domaine, societe.No).ConfigureAwait(continueOnCapturedContext: false);
	}

	public int CreateLigne(int dossierNo, LigneDossierImpaye ligne)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		if (ligne == null)
		{
			throw new ArgumentNullException("ligne");
		}
		DossierImpaye dossierImpaye = _dossierImpayeRepository.Get(dossierNo);
		if (dossierImpaye == null)
		{
			throw new ArgumentNullException($"Impossible de charger le dossier [{dossierNo}] ");
		}
		if (dossierImpaye.Statut == StatutDossierImpaye.Valide)
		{
			throw new ApplicationException("Le dossier est validé.");
		}
		if (ligne.MontantAImputer <= 0m)
		{
			throw new ApplicationException("La ligne du dossier règlement impayé [" + dossierImpaye.Numero + "] possède un montants invalide!");
		}
		CaisseManager caisseManager = SocieteManager.CaisseManager;
		if (ligne.Type == TypeLigneDossierImp.Impaye)
		{
			Echeance echeance = SocieteManager.EcheanceGet(ligne.EntityNo);
			if (echeance == null)
			{
				throw new ApplicationException("Impossible de charger l'échéance.");
			}
			if (echeance.IsReserveDossierFrs)
			{
				throw new ApplicationException("L'échéance [" + echeance.DocumentNumero + "] est réservée.");
			}
		}
		else
		{
			ReglementFournisseur reglementFournisseur = caisseManager.ReglementFournisseurGet(ligne.EntityNo);
			if (reglementFournisseur == null)
			{
				throw new ApplicationException("Impossible de charger le règlement.");
			}
			if (reglementFournisseur.IsReserveDossierFrs)
			{
				throw new ApplicationException("Le règlement [" + reglementFournisseur.Numero + "] est réservé.");
			}
			if (reglementFournisseur.Solde == 0m)
			{
				throw new ApplicationException("Le règlement[" + reglementFournisseur.Numero + "] est totalement soldé.");
			}
			if (reglementFournisseur.CaisseNo != dossierImpaye.CaisseNo)
			{
				throw new ApplicationException("Le règlement et le dossier de règlement doivent partagé la même caisse.");
			}
		}
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		Societe societe = SocieteManager.Societe;
		ligne.DossierImpayeNo = dossierNo;
		ligne.DateCreation = DateTime.Now;
		ligne.UtilisateurNo = utilisateur.No;
		ligne.DateModification = DateTime.Now;
		ligne.ModificateurNo = utilisateur.No;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		int num = 0;
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		num = _ligneDossierImpayeRepository.Create(ligne);
		if (ligne.Type == TypeLigneDossierImp.Impaye)
		{
			SocieteManager.EcheanceReserver(ligne.EntityNo);
		}
		else
		{
			caisseManager.ReglementFournisseurReserver(ligne.EntityNo);
		}
		_notifyService.Notify((dossierImpaye.Domaine == ErpDomaine.Vente) ? TypeEntity.DossierImpaye : TypeEntity.DossierImpayeFournisseur, dossierNo, TypeAction.Modification, societe.No);
		transactionScope.Complete();
		return num;
	}

	public void Valider(int dossierNo)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		Societe societe = SocieteManager.Societe;
		CaisseManager caisseManager = SocieteManager.CaisseManager;
		DossierImpaye dossierImpaye = _dossierImpayeRepository.Get(dossierNo);
		if (dossierImpaye == null)
		{
			throw new ArgumentNullException($"Impossible de charger le dossier [{dossierNo}] ");
		}
		if (dossierImpaye.Statut != StatutDossierImpaye.Encours)
		{
			throw new ApplicationException("Le dossier est validé.");
		}
		IList<LigneDossierImpaye> allLignesImpayes = GetAllLignesImpayes(dossierNo);
		IList<LigneDossierImpaye> allLignesReglements = GetAllLignesReglements(dossierNo);
		if (!allLignesImpayes.Any())
		{
			throw new ApplicationException("Le dossier règlement impayé [" + dossierImpaye.Numero + "] ne possède aucune ligne impaye!");
		}
		if (!allLignesReglements.Any())
		{
			throw new ApplicationException("Le dossier règlement impayé [" + dossierImpaye.Numero + "] ne possède aucune ligne règlement!");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		decimal num = allLignesImpayes.Sum((LigneDossierImpaye x) => x.MontantAImputer) + dossierImpaye.MontantCommission + dossierImpaye.MontantInteret;
		decimal num2 = allLignesReglements.Sum((LigneDossierImpaye x) => x.MontantAImputer);
		if (num != num2)
		{
			throw new ApplicationException("Le dossier règlement impayé [" + dossierImpaye.Numero + "] est non équilibré!");
		}
		SocieteDevise defaultDeviseSociete = societe.GetDefaultDeviseSociete();
		if (defaultDeviseSociete == null)
		{
			throw new ApplicationException("Impossible de charger la devise société!");
		}
		SocieteModeReglement societeModeReglement = societe.GetAllModes().FirstOrDefault();
		if (societeModeReglement == null)
		{
			throw new ApplicationException("Veuillez configurer les modes de la société!");
		}
		if (dossierImpaye.TauxInteret < 0m)
		{
			throw new ApplicationException("Le taux d'intérêt est invalide pour le dossier règlement impayé [" + dossierImpaye.Numero + "]!");
		}
		int? num3 = societe.DefaultSoucheImpaye;
		if (dossierImpaye.Domaine == ErpDomaine.Achat)
		{
			num3 = societe.DefaultSoucheImpayeFrs;
		}
		if (!num3.HasValue)
		{
			throw new ApplicationException("Veuillez configurer la souche par défaut des impayés!");
		}
		if (dossierImpaye.NombreJour < 0)
		{
			throw new ApplicationException("Le nombre de jour est invalide pour le dossier règlement impayé [" + dossierImpaye.Numero + "]!");
		}
		if (dossierImpaye.MontantInteret < 0m)
		{
			throw new ApplicationException("Le montant d'intérêt est invalide pour le dossier règlement impayé [" + dossierImpaye.Numero + "]!");
		}
		if (dossierImpaye.MontantInteret == 0m)
		{
			dossierImpaye.InteretNo = null;
		}
		else
		{
			dossierImpaye.InteretNo = CreateInteretEcheance(dossierImpaye.Numero, dossierImpaye.Date, dossierImpaye.TiersNo, dossierImpaye.TiersNumero, dossierImpaye.TiersIntitule, dossierImpaye.MontantInteret, modeNo: societeModeReglement.No, soucheNo: num3.Value, deviseSocieteNo: defaultDeviseSociete.No, domaine: dossierImpaye.Domaine);
		}
		if (dossierImpaye.MontantCommission < 0m)
		{
			throw new ApplicationException("Le montant de commission est invalide pour le dossier règlement impayé [" + dossierImpaye.Numero + "]!");
		}
		if (dossierImpaye.MontantCommission == 0m)
		{
			dossierImpaye.CommissionNo = null;
		}
		else
		{
			dossierImpaye.CommissionNo = CreateCommissionEcheance(dossierImpaye.Numero, dossierImpaye.Date, dossierImpaye.TiersNo, dossierImpaye.TiersNumero, dossierImpaye.TiersIntitule, dossierImpaye.MontantCommission, modeNo: societeModeReglement.No, soucheNo: num3.Value, deviseSocieteNo: defaultDeviseSociete.No, domaine: dossierImpaye.Domaine);
		}
		dossierImpaye.Statut = StatutDossierImpaye.Valide;
		_dossierImpayeRepository.Update(dossierImpaye);
		CreateAffectations(dossierNo, allLignesReglements, allLignesImpayes, defaultDeviseSociete.No);
		_notifyService.Notify(TypeEntity.DossierImpayeFournisseur, dossierNo, TypeAction.Modification, dossierImpaye.SocieteNo);
		foreach (LigneDossierImpaye item in allLignesReglements)
		{
			caisseManager.ReglementFournisseurAnnulerReservation(item.EntityNo);
		}
		foreach (LigneDossierImpaye item2 in allLignesImpayes)
		{
			SocieteManager.EcheanceAnnulerReservation(item2.EntityNo);
		}
		transactionScope.Complete();
	}
}
