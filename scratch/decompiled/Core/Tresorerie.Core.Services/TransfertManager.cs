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

public class TransfertManager
{
	private readonly IAutorisationCaisseRepository _autorisationCaisseRepository;

	private readonly IEcritureComptaRepository _ecritureComptaRepository;

	private readonly IHistoriqueMvtRepository _historiqueRepository;

	private readonly IMouvementRepository _mouvementRepository;

	private readonly NotifyService _notifyService;

	private readonly IReglementClientRepository _reglementClientRepo;

	private readonly ITransfertRepository _transfertRepo;

	private readonly IClotureCaisseRepository _clotureCaisseRepository;

	private readonly ILigneClotureCaisseRepository _ligneClotureCaisseRepository;

	private readonly ILicenceApplicationVersion _licenceApplicationVersion;

	public SocieteManager SocieteManager { get; set; }

	public TransfertManager(ITransfertRepository transfertRepo, IReglementClientRepository reglementClientRepo, IHistoriqueMvtRepository historiqueRepository, IMouvementRepository mouvementRepository, NotifyService notifyService, IAutorisationCaisseRepository autorisationCaisseRepository, IEcritureComptaRepository ecritureComptaRepository, IClotureCaisseRepository clotureCaisseRepository, ILigneClotureCaisseRepository ligneClotureCaisseRepository, ILicenceApplicationVersion licenceApplicationVersion)
	{
		_transfertRepo = transfertRepo ?? throw new ArgumentNullException("transfertRepo");
		_reglementClientRepo = reglementClientRepo ?? throw new ArgumentNullException("reglementClientRepo");
		_historiqueRepository = historiqueRepository ?? throw new ArgumentNullException("historiqueRepository");
		_mouvementRepository = mouvementRepository ?? throw new ArgumentNullException("mouvementRepository");
		_notifyService = notifyService ?? throw new ArgumentNullException("notifyService");
		_autorisationCaisseRepository = autorisationCaisseRepository ?? throw new ArgumentNullException("autorisationCaisseRepository");
		_ecritureComptaRepository = ecritureComptaRepository ?? throw new ArgumentNullException("ecritureComptaRepository");
		_clotureCaisseRepository = clotureCaisseRepository ?? throw new ArgumentNullException("clotureCaisseRepository");
		_ligneClotureCaisseRepository = ligneClotureCaisseRepository ?? throw new ArgumentNullException("ligneClotureCaisseRepository");
		_licenceApplicationVersion = licenceApplicationVersion ?? throw new ArgumentNullException("licenceApplicationVersion");
	}

	public Transfert Get(int no)
	{
		if (no <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorTransfertNo);
		}
		return _transfertRepo.Get(no);
	}

	public Transfert Get(string numero, Societe societe = null)
	{
		if (string.IsNullOrEmpty(numero))
		{
			throw new ArgumentNullException("numero");
		}
		societe = societe ?? SocieteManager.Societe;
		if (societe == null)
		{
			throw new ApplicationException("Impossible de charger la societe");
		}
		return _transfertRepo.Get(societe.No, numero);
	}

	public IEnumerable<Transfert> GetAll(Societe societe = null, Utilisateur utilisateur = null)
	{
		societe = societe ?? SocieteManager.Societe;
		utilisateur = utilisateur ?? SocieteManager.Utilisateur;
		if (!utilisateur.IsAdmin)
		{
			return _transfertRepo.GetAll((from a in _autorisationCaisseRepository.GetAll(utilisateur.No, societe.No, ProfilType.Grc)
				select a.CaisseNo).ToArray());
		}
		return _transfertRepo.GetAll(societe.No);
	}

	public IList<EcritureComptable> GetEcrituresTransfert(int transfertNo)
	{
		if (transfertNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorMvtNo);
		}
		Transfert transfert = Get(transfertNo);
		if (transfert == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorTransfertInvalid);
		}
		return _ecritureComptaRepository.GetAll(transfert.No, MouvementDomaine.Transfert);
	}

	public Mouvement MouvementGet(int no)
	{
		return _mouvementRepository.Get(no);
	}

	public Mouvement MouvementGet(string numero)
	{
		return _mouvementRepository.Get(numero);
	}

	public IList<ClotureCaisse> GetAllClotureCaisse(Societe societe = null, Utilisateur utilisateur = null)
	{
		societe = societe ?? SocieteManager.Societe;
		utilisateur = utilisateur ?? SocieteManager.Utilisateur;
		if (!utilisateur.IsAdmin)
		{
			return _clotureCaisseRepository.GetAll((from a in _autorisationCaisseRepository.GetAll(utilisateur.No, societe.No, ProfilType.Grc)
				select a.CaisseNo).ToArray());
		}
		return _clotureCaisseRepository.GetAll(societe.No);
	}

	public ClotureCaisse GetClotureCaisse(int no)
	{
		return _clotureCaisseRepository.Get(no);
	}

	public IList<LigneClotureCaisse> GetAllLigneClotureCaisse(int clotureNo)
	{
		return _ligneClotureCaisseRepository.GetAll(clotureNo);
	}

	public LigneClotureCaisse GetLigneClotureCaisse(int no)
	{
		return _ligneClotureCaisseRepository.Get(no);
	}

	public void AccepterTransfertEspece(int transfertNo, int[] lignesNo, Societe societe = null)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		societe = societe ?? SocieteManager.Societe;
		Transfert transfert = _transfertRepo.Get(transfertNo);
		if (transfert == null)
		{
			throw new ApplicationException($"Impossible de charger le transfert [{transfertNo}].");
		}
		Caisse caisse = societe.GetCaisse(transfert.CaisseDestinataireNo);
		if (caisse == null)
		{
			throw new ApplicationException("Caisse destinataire du transfert [" + transfert.Numero + "] est invalide!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grc))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (transfert.Statut != StatutTransfert.Envoye)
		{
			throw new InvalidOperationException("Opération invalide! Transfert [" + transfert.Numero + "] statut [" + transfert.Statut.GetDisplayDescription() + "]");
		}
		if (transfert.Montant == 0m)
		{
			throw new ApplicationException("Le transfert [" + transfert.Numero + "] ne contient aucune pièce");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		transfert.Statut = StatutTransfert.Valide;
		transfert.DateModification = DateTime.Now;
		transfert.ModificateurNo = utilisateur.No;
		_transfertRepo.Update(transfert);
		foreach (int mouvementNo in lignesNo)
		{
			HistoriqueMvt ligneSortieTransfert = _historiqueRepository.GetLigneSortieTransfert(transfertNo, mouvementNo, transfert.ModeNo);
			ligneSortieTransfert.Statut = StatutTransfert.Valide;
			_historiqueRepository.Update(ligneSortieTransfert);
		}
		HistoriqueMvt historiqueMvt = new HistoriqueMvt(transfertNo, transfert.CaisseDestinataireNo, SensMouvement.Entree, MouvementDomaine.Transfert, transfert.ModeNo, transfertNo, 0, StatutTransfert.None, transfert.DeviseNo, new Lot(transfertNo, transfert.Montant, transfert.Montant));
		_historiqueRepository.Create(historiqueMvt);
		_notifyService.Notify(TypeEntity.Transfert, transfertNo, TypeAction.Modification, societe.No);
		transactionScope.Complete();
	}

	public async Task<IList<Transfert>> GetAllAComptaAsync(DateTime dateMin, DateTime dateMax, EtatComptabilite etat, int[] caissesDestinataireNo, int[] caissesNo, int[] modesNo, CancellationToken cancellationToken)
	{
		Societe societe = SocieteManager.Societe;
		return (await _transfertRepo.GetAllAComptaAsync(dateMin.Date, dateMax.Date.AddDays(1.0), etat, caissesDestinataireNo, caissesNo, modesNo, societe.No, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).ToList();
	}

	public void AccepterTransfertPiece(int transfertNo, int societeNo, int[] lignesNo)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		Societe societe = SocieteManager.Societe;
		Transfert transfert = _transfertRepo.Get(transfertNo);
		if (transfert == null)
		{
			throw new ApplicationException($"Impossible de charger le transfert [{transfertNo}].");
		}
		Caisse caisse = societe.GetCaisse(transfert.CaisseDestinataireNo);
		if (caisse == null)
		{
			throw new ArgumentNullException("Caisse destinataire invalide!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grc))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (transfert.Statut != StatutTransfert.Envoye)
		{
			throw new InvalidOperationException("Opération invalide!");
		}
		if (transfert.Montant == 0m)
		{
			throw new ApplicationException("Transfert ne contient aucune pièce");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		foreach (int num in lignesNo)
		{
			ReglementClient reglementClient = _reglementClientRepo.Get(num);
			if (reglementClient == null)
			{
				throw new ArgumentNullException("Règlement invalide!");
			}
			if (reglementClient.StatutTransfert != StatutTransfert.Envoye)
			{
				throw new ApplicationException("Statut règlement invalide!");
			}
			reglementClient.CaisseNo = transfert.CaisseDestinataireNo;
			reglementClient.StatutTransfert = StatutTransfert.None;
			reglementClient.DateModification = DateTime.Now;
			reglementClient.ModificateurNo = utilisateur.No;
			_reglementClientRepo.Update(reglementClient);
			transfert.Statut = StatutTransfert.Valide;
			transfert.DateModification = DateTime.Now;
			transfert.ModificateurNo = utilisateur.No;
			_transfertRepo.Update(transfert);
			HistoriqueMvt ligneSortieTransfert = _historiqueRepository.GetLigneSortieTransfert(transfertNo, num, transfert.ModeNo);
			ligneSortieTransfert.Statut = StatutTransfert.Valide;
			_historiqueRepository.Update(ligneSortieTransfert);
			HistoriqueMvt historiqueMvt = new HistoriqueMvt(num, transfert.CaisseDestinataireNo, SensMouvement.Entree, MouvementDomaine.Transfert, transfert.ModeNo, transfertNo, 0, StatutTransfert.None, reglementClient.DeviseNo, new Lot(num, reglementClient.Montant, reglementClient.Montant));
			_historiqueRepository.Create(historiqueMvt);
			_notifyService.Notify(TypeEntity.Reglement, num, TypeAction.Modification, societeNo);
			_notifyService.Notify(TypeEntity.Transfert, transfertNo, TypeAction.Modification, societeNo);
		}
		transactionScope.Complete();
	}

	public void AnnulerTransfertEspece(int transfertNo)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		Societe societe = SocieteManager.Societe;
		Transfert transfert = _transfertRepo.Get(transfertNo);
		if (transfert == null)
		{
			throw new ApplicationException("Transfert invalide!");
		}
		Caisse caisse = societe.GetCaisse(transfert.CaisseNo);
		if (caisse == null)
		{
			throw new ApplicationException("Caisse source du transfert [" + transfert.Numero + "] est invalide!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grc))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		List<HistoriqueMvt> list = _historiqueRepository.GetAllLignesTransfert(transfertNo).ToList();
		if (!list.Any())
		{
			throw new ApplicationException("Transfert ne contient aucune lignes!");
		}
		if (transfert.Statut != StatutTransfert.Envoye)
		{
			throw new ApplicationException("Statut transfert invalide!");
		}
		if (transfert.Montant <= 0m)
		{
			throw new ApplicationException("Le montant du transfert est invalide!");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		transfert.Statut = StatutTransfert.Saisie;
		transfert.DateModification = DateTime.Now;
		transfert.ModificateurNo = utilisateur.No;
		_transfertRepo.Update(transfert);
		foreach (HistoriqueMvt item in list)
		{
			item.Statut = StatutTransfert.Saisie;
			_historiqueRepository.Update(item);
		}
		_notifyService.Notify(TypeEntity.Transfert, transfertNo, TypeAction.Modification, societe.No);
		transactionScope.Complete();
	}

	public void AnnulerTransfertPiece(int transfertNo)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		Societe societe = SocieteManager.Societe;
		Transfert transfert = _transfertRepo.Get(transfertNo);
		if (transfert == null)
		{
			throw new ApplicationException("Transfert invalide!");
		}
		Caisse caisse = societe.GetCaisse(transfert.CaisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException("Caisse source invalide!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grc))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		List<HistoriqueMvt> list = _historiqueRepository.GetAllLignesTransfert(transfertNo).ToList();
		if (!list.Any())
		{
			throw new ApplicationException("Transfert ne contient aucune lignes!");
		}
		if (transfert.Statut != StatutTransfert.Envoye)
		{
			throw new ApplicationException("Statut transfert invalide!");
		}
		if (transfert.Montant <= 0m)
		{
			throw new ApplicationException("Le montant du transfert est invalide!");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		foreach (HistoriqueMvt item in list)
		{
			ReglementClient reglement = _reglementClientRepo.Get(item.Lot.No);
			if (reglement == null)
			{
				throw new ArgumentNullException("Règlement invalide!");
			}
			if (reglement.StatutTransfert != StatutTransfert.Envoye)
			{
				throw new InvalidOperationException("Statut règlement est invalide!");
			}
			reglement.StatutTransfert = StatutTransfert.Saisie;
			reglement.DateModification = DateTime.Now;
			reglement.ModificateurNo = utilisateur.No;
			_reglementClientRepo.Update(reglement);
			transfert.Statut = StatutTransfert.Saisie;
			transfert.DateModification = DateTime.Now;
			transfert.ModificateurNo = utilisateur.No;
			_transfertRepo.Update(transfert);
			HistoriqueMvt ligneSortieTransfert = _historiqueRepository.GetLigneSortieTransfert(transfertNo, item.Lot.No, transfert.ModeNo);
			ligneSortieTransfert.Statut = StatutTransfert.Saisie;
			_historiqueRepository.Update(ligneSortieTransfert);
			HistoriqueMvt historiqueMvt = (from x in _historiqueRepository.GetAllByMouvement(item.Lot.No)
				where x.Sens == SensMouvement.Entree && x.CaisseNo == reglement.CaisseNo
				orderby x.No
				select x).Last();
			Lot lot = historiqueMvt.Lot;
			lot.MontantRestant = lot.Montant;
			_historiqueRepository.Update(historiqueMvt);
			_notifyService.Notify(TypeEntity.Reglement, item.Lot.No, TypeAction.Modification, transfert.SocieteNo);
		}
		_notifyService.Notify(TypeEntity.Transfert, transfertNo, TypeAction.Modification, transfert.SocieteNo);
		transactionScope.Complete();
	}

	public void Comptabiliser(int transfertNo, List<EcritureComptable> erpEcritures)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		Transfert transfert = Get(transfertNo);
		if (transfert == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorTransfertInvalid);
		}
		if (transfert.IsComptabilise == EtatComptabilite.Comptabilise)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorTransfertMvtComptabilise);
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
		transfert.IsComptabilise = EtatComptabilite.Comptabilise;
		transfert.DateModification = DateTime.Now;
		transfert.ModificateurNo = utilisateur.No;
		_transfertRepo.Update(transfert);
		_notifyService.Notify(TypeEntity.Transfert, transfert.No, TypeAction.Modification, transfert.SocieteNo);
		transactionScope.Complete();
	}

	public void CreateLigneTransPiece(int transfertNo, int societeNo, int mouvementNo, int deviseSocieteNo, Utilisateur utilisateur = null)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		utilisateur = utilisateur ?? SocieteManager.Utilisateur;
		Societe societe = SocieteManager.Get(societeNo);
		Transfert transfert = _transfertRepo.Get(transfertNo);
		if (transfert == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorTransfertNo);
		}
		if (transfert.IsComptabilise == EtatComptabilite.Comptabilise)
		{
			throw new InvalidOperationException("Opération invalide! Transfert est comptabilisé.");
		}
		Caisse caisse = societe.GetCaisse(transfert.CaisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException("Caisse source invalide!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grc, utilisateur, societe))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (transfert.Statut != StatutTransfert.Saisie)
		{
			throw new ApplicationException("Statut transfert invalide!");
		}
		ReglementClient reglementClient = _reglementClientRepo.Get(mouvementNo);
		if (reglementClient == null)
		{
			throw new InvalidOperationException("Règlement invalide!");
		}
		if (reglementClient.IsReglementAvoir)
		{
			throw new ApplicationException("Opération invalide. Règlement d'avoir.");
		}
		if (reglementClient.StatutTransfert != StatutTransfert.None)
		{
			throw new ApplicationException("Statut règlement invalide!");
		}
		if (reglementClient.CaisseNo != transfert.CaisseNo)
		{
			throw new ApplicationException("Caisse règlement invalide!");
		}
		if (reglementClient.ModeReglementNo != transfert.ModeNo)
		{
			throw new ApplicationException("Le mode du règlement est invalide!");
		}
		if (reglementClient.DeviseNo != deviseSocieteNo)
		{
			throw new InvalidOperationException("Transfert invalide! Règlement en devise!");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		reglementClient.StatutTransfert = StatutTransfert.Saisie;
		reglementClient.DateModification = DateTime.Now;
		reglementClient.ModificateurNo = utilisateur.No;
		_reglementClientRepo.Update(reglementClient);
		HistoriqueMvt historiqueMvt = new HistoriqueMvt(mouvementNo, transfert.CaisseNo, SensMouvement.Sortie, MouvementDomaine.Transfert, transfert.ModeNo, mouvementNo, transfertNo, StatutTransfert.Saisie, reglementClient.DeviseNo, new Lot(mouvementNo, reglementClient.Montant, 0m));
		_historiqueRepository.Create(historiqueMvt);
		transfert.Montant += reglementClient.Montant;
		transfert.DateModification = DateTime.Now;
		transfert.ModificateurNo = utilisateur.No;
		_transfertRepo.Update(transfert);
		_notifyService.Notify(TypeEntity.Reglement, mouvementNo, TypeAction.Modification, societeNo);
		_notifyService.Notify(TypeEntity.Transfert, transfertNo, TypeAction.Modification, societeNo);
		transactionScope.Complete();
	}

	public int CreateTransEspece(string numero, int caisseNoOut, int caisseNoIn, decimal montant, int modeNo, int deviseNo, Dictionary<int, decimal> lots, Societe societe = null)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		societe = societe ?? SocieteManager.Societe;
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		if (Get(numero) != null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorTransfertNumero);
		}
		Caisse caisse = societe.GetCaisse(caisseNoOut);
		if (caisse == null)
		{
			throw new ArgumentNullException("Caisse source invalide!");
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Opération invalide! La caisse source est en sommeil!");
		}
		Caisse caisse2 = societe.GetCaisse(caisseNoIn);
		if (caisse2 == null)
		{
			throw new ArgumentNullException("Caisse destinataire invalide!");
		}
		if (caisse2.EnSommeil)
		{
			throw new ApplicationException("Opération invalide! La caisse destinataire est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grc))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (caisse.SocieteNo != societe.No || caisse2.SocieteNo != societe.No)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorCaisse);
		}
		if (caisse2.No == caisse.No)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorMemeCaisse);
		}
		CaisseModeReglement mode = caisse.GetMode(modeNo);
		if (mode == null)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorModeInvalide);
		}
		if (mode.EnSommeil)
		{
			throw new ArgumentException("Opération invalide! Le mode est en sommeil!");
		}
		if (!caisse2.HasModeReglement(modeNo))
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorCaisseDestinataire);
		}
		if (mode.Type != ReglementType.Espece)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorModeReglementType);
		}
		decimal solde = SocieteManager.HistoriqueMvtManager.GetSolde(caisseNoOut, modeNo, deviseNo);
		if (lots.Sum((KeyValuePair<int, decimal> m) => m.Value) > solde)
		{
			throw new ApplicationException("Solde caisse insuffisant pour le transfert!");
		}
		if (SocieteManager.CaisseManager.GetCaisseAuthorisation(caisseNoOut, caisseNoIn) == null)
		{
			throw new ApplicationException("Aucune caisse autorisé");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		Transfert transfert = new Transfert
		{
			Numero = numero,
			Date = DateTime.Now,
			CaisseNo = caisseNoOut,
			CaisseDestinataireNo = caisseNoIn,
			Montant = montant,
			SocieteNo = societe.No,
			ModeNo = modeNo,
			Type = mode.Type,
			DeviseNo = deviseNo,
			CreateurNo = utilisateur.No,
			Statut = StatutTransfert.Saisie,
			DateCreation = DateTime.Now,
			DateModification = DateTime.Now,
			ModificateurNo = utilisateur.No
		};
		int num = _transfertRepo.Create(transfert);
		if (num <= 0)
		{
			throw new ApplicationException("Insertion transfert invalide!");
		}
		foreach (KeyValuePair<int, decimal> lot2 in lots)
		{
			HistoriqueMvt historiqueMvt = _historiqueRepository.Get(lot2.Key);
			if (historiqueMvt == null)
			{
				throw new ApplicationException("Mouvement invalide!");
			}
			Lot lot = historiqueMvt.Lot;
			lot.MontantRestant -= lot2.Value;
			_historiqueRepository.Update(historiqueMvt);
			HistoriqueMvt historiqueMvt2 = new HistoriqueMvt(lot.No, transfert.CaisseNo, SensMouvement.Sortie, MouvementDomaine.Transfert, transfert.ModeNo, lot.No, num, StatutTransfert.Saisie, deviseNo, new Lot(lot.No, lot2.Value, 0m));
			_historiqueRepository.Create(historiqueMvt2);
		}
		_notifyService.Notify(TypeEntity.Transfert, num, TypeAction.Ajout, societe.No);
		transactionScope.Complete();
		return num;
	}

	public int CreateTransfertPiece(string numero, int caisseNoOut, int caisseNoIn, decimal montant, int societeNo, int modeNo, int deviseNo, int deviseSocieteNo, Utilisateur utilisateur = null)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		if (string.IsNullOrEmpty(numero))
		{
			throw new ArgumentNullException("numero");
		}
		if (deviseNo <= 0)
		{
			throw new ApplicationException("DeviseNo invalide!");
		}
		if (deviseSocieteNo <= 0)
		{
			throw new ApplicationException("Devise societe invalide!");
		}
		utilisateur = utilisateur ?? SocieteManager.Utilisateur;
		if (utilisateur == null)
		{
			throw new ApplicationException("Impossible de charger l'utilisateur");
		}
		Societe societe = SocieteManager.Get(societeNo);
		if (societe == null)
		{
			throw new ApplicationException("Impossible de charger la societe");
		}
		if (societe.GetDevise(deviseSocieteNo) == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorDeviseNo);
		}
		if (deviseNo != deviseSocieteNo)
		{
			throw new InvalidOperationException("Transfert en devise est invalide!");
		}
		if (Get(numero, societe) != null)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorTransfertNumero);
		}
		Caisse caisse = societe.GetCaisse(caisseNoOut);
		if (caisse == null)
		{
			throw new ArgumentNullException("Caisse source invalide!");
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Opération invalide! La caisse source est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grc, utilisateur, societe))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		Caisse caisse2 = societe.GetCaisse(caisseNoIn);
		if (caisse2 == null)
		{
			throw new ArgumentNullException("Caisse destinataire invalide!");
		}
		if (caisse2.EnSommeil)
		{
			throw new ApplicationException("Opération invalide! La caisse destinataire est en sommeil!");
		}
		if (caisse.SocieteNo != societeNo || caisse2.SocieteNo != societeNo)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorCaisse);
		}
		if (caisse2.No == caisse.No)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorMemeCaisse);
		}
		CaisseModeReglement mode = caisse.GetMode(modeNo);
		if (mode == null)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorModeInvalide);
		}
		if (mode.EnSommeil)
		{
			throw new ArgumentException("Opération invalide! Le mode est en sommeil!");
		}
		if (!caisse2.HasModeReglement(modeNo))
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorCaisseDestinataire);
		}
		if (!mode.IsTransferable || (mode.Type != ReglementType.Cheque && mode.Type != ReglementType.Traite && mode.Type != ReglementType.Autre))
		{
			throw new ApplicationException("Mode reglement non transférable!");
		}
		if (mode.IsModeAvoir)
		{
			throw new ApplicationException("Opération invalide. Mode avoir.");
		}
		if (SocieteManager.CaisseManager.GetCaisseAuthorisation(caisseNoOut, caisseNoIn) == null)
		{
			throw new ApplicationException("Aucune caisse autorisé");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		Transfert transfert = new Transfert
		{
			Numero = numero,
			Date = DateTime.Now,
			CaisseNo = caisseNoOut,
			CaisseDestinataireNo = caisseNoIn,
			Montant = montant,
			SocieteNo = societeNo,
			ModeNo = modeNo,
			Type = mode.Type,
			DeviseNo = deviseNo,
			CreateurNo = utilisateur.No,
			Statut = StatutTransfert.Saisie,
			DateCreation = DateTime.Now,
			DateModification = DateTime.Now,
			ModificateurNo = utilisateur.No
		};
		int num = _transfertRepo.Create(transfert);
		_notifyService.Notify(TypeEntity.Transfert, num, TypeAction.Ajout, societeNo);
		transactionScope.Complete();
		return num;
	}

	public void DeComptabiliserTransfert(int mvtNo)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		Transfert transfert = Get(mvtNo);
		if (transfert == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorTransfertInvalid);
		}
		if (transfert.IsComptabilise == EtatComptabilite.NonComptabilise)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorTransfertMvtNonComptabilise);
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_ecritureComptaRepository.DeleteLigneTransfert(transfert.No);
		transfert.IsComptabilise = EtatComptabilite.NonComptabilise;
		transfert.DateModification = DateTime.Now;
		transfert.ModificateurNo = utilisateur.No;
		_transfertRepo.Update(transfert);
		_notifyService.Notify(TypeEntity.Transfert, transfert.No, TypeAction.Modification, transfert.SocieteNo);
		transactionScope.Complete();
	}

	public void DeleteLigneTransPiece(int transfertNo, int societeNo, int mouvementNo)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		Societe societe = SocieteManager.Societe;
		Transfert transfert = _transfertRepo.Get(transfertNo);
		if (transfert == null)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorTransfertNo);
		}
		if (transfert.IsComptabilise == EtatComptabilite.Comptabilise)
		{
			throw new InvalidOperationException("Opération invalide! Transfert est comptabilisé.");
		}
		Caisse caisse = societe.GetCaisse(transfert.CaisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException("Caisse source invalide!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grc))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (transfert.Statut != StatutTransfert.Saisie)
		{
			throw new ApplicationException("Statut transfert invalide!");
		}
		ReglementClient reglementClient = _reglementClientRepo.Get(mouvementNo);
		if (reglementClient == null)
		{
			throw new InvalidOperationException("Règlement invalide!");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		reglementClient.StatutTransfert = StatutTransfert.None;
		reglementClient.DateModification = DateTime.Now;
		reglementClient.ModificateurNo = utilisateur.No;
		_reglementClientRepo.Update(reglementClient);
		HistoriqueMvt ligneSortieTransfert = _historiqueRepository.GetLigneSortieTransfert(transfertNo, reglementClient.No, transfert.ModeNo);
		if (ligneSortieTransfert == null)
		{
			throw new ApplicationException("Mouvement de sortie inexistant!");
		}
		_historiqueRepository.Delete(ligneSortieTransfert);
		transfert.Montant -= reglementClient.Montant;
		transfert.DateModification = DateTime.Now;
		transfert.ModificateurNo = utilisateur.No;
		_transfertRepo.Update(transfert);
		_notifyService.Notify(TypeEntity.Reglement, mouvementNo, TypeAction.Modification, societeNo);
		_notifyService.Notify(TypeEntity.Transfert, transfertNo, TypeAction.Modification, societeNo);
		transactionScope.Complete();
	}

	public void DeleteTransfertEspece(int transfertNo)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		Societe societe = SocieteManager.Societe;
		Transfert transfert = _transfertRepo.Get(transfertNo);
		if (transfert == null)
		{
			throw new ApplicationException("Transfert invalide!");
		}
		Caisse caisse = societe.GetCaisse(transfert.CaisseNo);
		if (caisse == null)
		{
			throw new ApplicationException("Caisse source invalide!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grc))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		List<HistoriqueMvt> list = _historiqueRepository.GetAllLignesTransfert(transfertNo).ToList();
		List<HistoriqueMvt> list2 = _historiqueRepository.GetAllLignesEntreeTransfert(transfertNo).ToList();
		if (list2.Any((HistoriqueMvt x) => x.Lot.IsEpuise || x.Lot.Montant != x.Lot.MontantRestant))
		{
			throw new InvalidOperationException("Opération invalide! Mouvement utilisé");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		foreach (HistoriqueMvt item in list2)
		{
			_historiqueRepository.Delete(item);
		}
		foreach (HistoriqueMvt item2 in list)
		{
			HistoriqueMvt historiqueMvt = (from x in _historiqueRepository.GetAllByMouvement(item2.MouvementInNo)
				where x.Sens == SensMouvement.Entree && x.CaisseNo == transfert.CaisseNo
				orderby x.No
				select x).Last();
			if (historiqueMvt == null)
			{
				throw new ApplicationException("Mouvement d'entrée invalide!");
			}
			historiqueMvt.Lot.MontantRestant += item2.Montant;
			_historiqueRepository.Update(historiqueMvt);
			_historiqueRepository.Delete(item2);
		}
		_transfertRepo.Delete(transfert);
		_notifyService.Notify(TypeEntity.Transfert, transfertNo, TypeAction.Suppression, transfert.SocieteNo);
		transactionScope.Complete();
	}

	public void DeleteTransfertPiece(int transfertNo)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		Societe societe = SocieteManager.Societe;
		Transfert transfert = _transfertRepo.Get(transfertNo);
		if (transfert == null)
		{
			throw new ApplicationException("Transfert invalide!");
		}
		Caisse caisse = societe.GetCaisse(transfert.CaisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException("Caisse source invalide!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grc))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		List<HistoriqueMvt> list = _historiqueRepository.GetAllLignesEntreeTransfert(transfertNo).ToList();
		if (list.Any((HistoriqueMvt x) => x.Lot.IsEpuise || x.Montant != x.Lot.MontantRestant))
		{
			throw new ApplicationException("Opération invalide!");
		}
		IEnumerable<HistoriqueMvt> allLignesTransfert = _historiqueRepository.GetAllLignesTransfert(transfertNo);
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		foreach (HistoriqueMvt item in list)
		{
			_historiqueRepository.Delete(item);
		}
		foreach (HistoriqueMvt item2 in allLignesTransfert)
		{
			_historiqueRepository.Delete(item2);
			HistoriqueMvt historiqueMvt = (from x in _historiqueRepository.GetAllByMouvement(item2.Lot.No)
				where x.Sens == SensMouvement.Entree
				orderby x.No
				select x).LastOrDefault();
			if (historiqueMvt == null)
			{
				throw new InvalidOperationException("Opération invalide!");
			}
			historiqueMvt.Lot.MontantRestant = historiqueMvt.Lot.Montant;
			_historiqueRepository.Update(historiqueMvt);
			ReglementClient reglementClient = _reglementClientRepo.Get(item2.Lot.No);
			if (reglementClient == null)
			{
				throw new InvalidOperationException("Règlement invalide");
			}
			reglementClient.CaisseNo = transfert.CaisseNo;
			reglementClient.StatutTransfert = StatutTransfert.None;
			_reglementClientRepo.Update(reglementClient);
		}
		_transfertRepo.Delete(transfert);
		_notifyService.Notify(TypeEntity.Transfert, transfertNo, TypeAction.Suppression, transfert.SocieteNo);
		transactionScope.Complete();
	}

	public void EnvoyeTransfertEspece(int transfertNo, int[] lignesNo, Societe societe = null)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		societe = societe ?? SocieteManager.Societe;
		Transfert transfert = _transfertRepo.Get(transfertNo);
		if (transfert == null)
		{
			throw new ApplicationException("transfertNo");
		}
		Caisse caisse = societe.GetCaisse(transfert.CaisseNo);
		if (caisse == null)
		{
			throw new ApplicationException("Caisse source invalide!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grc))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (transfert.Statut != StatutTransfert.Saisie)
		{
			throw new InvalidOperationException("Opération invalide! [Statut transfert]");
		}
		if (transfert.Montant == 0m)
		{
			throw new ApplicationException("Transfert ne contient aucune pièce");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		transfert.Statut = StatutTransfert.Envoye;
		transfert.DateModification = DateTime.Now;
		transfert.ModificateurNo = utilisateur.No;
		_transfertRepo.Update(transfert);
		foreach (int mouvementNo in lignesNo)
		{
			HistoriqueMvt ligneSortieTransfert = _historiqueRepository.GetLigneSortieTransfert(transfertNo, mouvementNo, transfert.ModeNo);
			ligneSortieTransfert.Statut = StatutTransfert.Envoye;
			_historiqueRepository.Update(ligneSortieTransfert);
		}
		_notifyService.Notify(TypeEntity.Transfert, transfertNo, TypeAction.Modification, societe.No);
		transactionScope.Complete();
	}

	public void EnvoyeTransfertPiece(int transfertNo, int societeNo, int[] lignesNo)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		Societe societe = SocieteManager.Societe;
		Transfert transfert = _transfertRepo.Get(transfertNo);
		if (transfert == null)
		{
			throw new ApplicationException($"Impossible de charger le transfert n° [{transfertNo}]");
		}
		Caisse caisse = societe.GetCaisse(transfert.CaisseNo);
		if (caisse == null)
		{
			throw new ArgumentNullException("Caisse source invalide!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No, ProfilType.Grc))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (transfert.Statut != StatutTransfert.Saisie)
		{
			throw new InvalidOperationException("Opération invalide! [Statut transfert]");
		}
		if (transfert.Montant == 0m)
		{
			throw new ApplicationException("Transfert ne contient aucune pièce");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		foreach (int num in lignesNo)
		{
			ReglementClient reglementClient = _reglementClientRepo.Get(num);
			if (reglementClient == null)
			{
				throw new ArgumentNullException("Règlement invalide!");
			}
			if (reglementClient.StatutTransfert != StatutTransfert.Saisie)
			{
				throw new InvalidOperationException("Statut règlement invalide!");
			}
			reglementClient.StatutTransfert = StatutTransfert.Envoye;
			reglementClient.DateModification = DateTime.Now;
			reglementClient.ModificateurNo = utilisateur.No;
			_reglementClientRepo.Update(reglementClient);
			transfert.Statut = StatutTransfert.Envoye;
			transfert.DateModification = DateTime.Now;
			transfert.ModificateurNo = utilisateur.No;
			_transfertRepo.Update(transfert);
			HistoriqueMvt ligneSortieTransfert = _historiqueRepository.GetLigneSortieTransfert(transfertNo, num, transfert.ModeNo);
			ligneSortieTransfert.Statut = StatutTransfert.Envoye;
			_historiqueRepository.Update(ligneSortieTransfert);
			HistoriqueMvt nonEpuise = _historiqueRepository.GetNonEpuise(transfert.CaisseNo, transfert.ModeNo, transfert.DeviseNo, num);
			nonEpuise.Lot.MontantRestant = 0m;
			_historiqueRepository.Update(nonEpuise);
			_notifyService.Notify(TypeEntity.Reglement, num, TypeAction.Modification, societeNo);
			_notifyService.Notify(TypeEntity.Transfert, transfertNo, TypeAction.Modification, societeNo);
		}
		transactionScope.Complete();
	}

	public int AddClotureCaisse(ClotureCaisse cloture)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		if (cloture == null)
		{
			throw new ArgumentNullException("cloture");
		}
		if (cloture.CaisseOrigineNo == 0)
		{
			throw new ApplicationException("la caisse d'origine est invalide!");
		}
		if ((cloture.CaisseDestinationNo == cloture.CaisseOrigineNo) | (cloture.CaisseDestinationNo == 0))
		{
			throw new ApplicationException("la caisse destination est invalide!");
		}
		if (cloture.Montant < 0m)
		{
			throw new ApplicationException("Montant invalide!");
		}
		if (string.IsNullOrEmpty(cloture.Numero))
		{
			throw new ApplicationException("Numéro invalide!");
		}
		if (cloture.DateDebut.Date > cloture.DateFin.Date)
		{
			throw new ApplicationException("Date invalide!");
		}
		CaisseManager caisseManager = SocieteManager.CaisseManager;
		if (caisseManager.Get(cloture.CaisseOrigineNo) == null)
		{
			throw new ApplicationException($"Impossible de charger la caisse d'origine [{cloture.CaisseOrigineNo}]");
		}
		if (caisseManager.Get(cloture.CaisseDestinationNo) == null)
		{
			throw new ApplicationException($"Impossible de charger la caisse destination [{cloture.CaisseDestinationNo}]");
		}
		return _clotureCaisseRepository.Add(cloture);
	}

	public void DeleteClotureCaisse(ClotureCaisse cloture)
	{
		_licenceApplicationVersion.ThrowIfGratuit();
		if (cloture == null)
		{
			throw new ArgumentNullException("cloture");
		}
		if (_ligneClotureCaisseRepository.GetAll(cloture.No).ToList().Any(delegate(LigneClotureCaisse l)
		{
			Transfert transfert2 = Get(l.TransfertNo);
			return transfert2 != null && transfert2.Statut == StatutTransfert.Valide;
		}))
		{
			throw new ApplicationException("Impossible de supprimer la clôture de la caisse n°[" + cloture.Numero + "]");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		foreach (LigneClotureCaisse item in _ligneClotureCaisseRepository.GetAll(cloture.No))
		{
			Transfert transfert = Get(item.TransfertNo);
			if (transfert != null)
			{
				if (transfert.Type == ReglementType.Espece)
				{
					DeleteTransfertEspece(item.TransfertNo);
				}
				else
				{
					DeleteTransfertPiece(item.TransfertNo);
				}
			}
		}
		_clotureCaisseRepository.Delete(cloture);
		transactionScope.Complete();
	}

	public int AddLigneClotureCaisse(LigneClotureCaisse ligneCloture)
	{
		if (ligneCloture == null)
		{
			throw new ArgumentNullException("ligneCloture");
		}
		if (SocieteManager.Groupe.ModeReglementManager.Get(ligneCloture.ModeReglementNo) == null)
		{
			throw new ApplicationException($"Impossible de charger le mode de règlement [{ligneCloture.ModeReglementNo}]!");
		}
		if (_clotureCaisseRepository.Get(ligneCloture.ClotureCaisseNo) == null)
		{
			throw new ApplicationException($"Impossible de charger la clôture de la caisse [{ligneCloture.ClotureCaisseNo}]!");
		}
		if ((Get(ligneCloture.TransfertNo) ?? throw new ApplicationException($"Impossible de charger le transfert [{ligneCloture.TransfertNo}]")).ModeNo != ligneCloture.ModeReglementNo)
		{
			throw new ApplicationException("Mode de règlement invalide!");
		}
		if (ligneCloture.Montant <= 0m)
		{
			throw new ApplicationException("Montant invalide!");
		}
		return _ligneClotureCaisseRepository.Add(ligneCloture);
	}

	public bool Exist(int transfertNo)
	{
		return _transfertRepo.Exist(transfertNo);
	}

	public void UpdateNbEspeceCloture(ClotureCaisse clotureCaisse)
	{
		_clotureCaisseRepository.UpdateNbEspece(clotureCaisse);
	}
}
