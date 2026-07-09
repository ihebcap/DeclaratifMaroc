using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Interfaces;
using Tresorerie.Core.Models;
using Tresorerie.Ressources;

namespace Tresorerie.Core.Services;

public class AlimentationCaisseManager
{
	private readonly IAlimentationCaisseRepository _alimentationCaisseRepository;

	private readonly IHistoriqueMvtRepository _historiqueRepository;

	private readonly NotifyService _notifyService;

	private readonly IEcritureComptaRepository _ecritureComptaRepository;

	private readonly IChequeRepository _chequeRepository;

	private readonly IAutorisationCaisseRepository _autorisationCaisseRepository;

	public GroupeService Groupe { get; set; }

	private SocieteManager SocieteManager => Groupe?.SocieteManager;

	public AlimentationCaisseManager(IAlimentationCaisseRepository alimentationCaisseRepository, IHistoriqueMvtRepository historiqueRepository, NotifyService notifyService, IEcritureComptaRepository ecritureComptaRepository, IChequeRepository chequeRepository, IAutorisationCaisseRepository autorisationCaisseRepository)
	{
		_alimentationCaisseRepository = alimentationCaisseRepository ?? throw new ArgumentNullException("alimentationCaisseRepository");
		_historiqueRepository = historiqueRepository ?? throw new ArgumentNullException("historiqueRepository");
		_notifyService = notifyService ?? throw new ArgumentNullException("notifyService");
		_ecritureComptaRepository = ecritureComptaRepository ?? throw new ArgumentNullException("ecritureComptaRepository");
		_chequeRepository = chequeRepository ?? throw new ArgumentNullException("chequeRepository");
		_autorisationCaisseRepository = autorisationCaisseRepository ?? throw new ArgumentNullException("autorisationCaisseRepository");
	}

	public IList<AlimentationCaisse> GetAllMouvementACompta(IErpExercice exercice, ErpComptaPeriode periode, int caisseNo, DateTime dateMin, DateTime dateMax, EtatComptabilite etat)
	{
		Societe societe = SocieteManager.Societe;
		if (societe.GetCaisse(caisseNo) == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		DateTime minDate = ((exercice.Debut < dateMin) ? dateMin : exercice.Debut);
		DateTime maxDate = ((exercice.Fin < dateMax) ? exercice.Fin : dateMax);
		return _alimentationCaisseRepository.GetAll(periode, caisseNo, minDate, maxDate, etat, societe.No).ToList();
	}

	public void Comptabiliser(int alimentationNo, List<EcritureComptable> erpEcritures)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		AlimentationCaisse alimentationCaisse = Get(alimentationNo);
		if (alimentationCaisse == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorAlimentationInvalid);
		}
		if (alimentationCaisse.IsComptabilise == EtatComptabilite.Comptabilise)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorAlimentationComptabilise);
		}
		foreach (EcritureComptable erpEcriture in erpEcritures)
		{
			_ecritureComptaRepository.Create(erpEcriture);
		}
		alimentationCaisse.ChangeEtatComptabilise(EtatComptabilite.Comptabilise);
		alimentationCaisse.ModificateurNo = utilisateur.No;
		_alimentationCaisseRepository.Update(alimentationCaisse);
		_notifyService.Notify(TypeEntity.Alimentation, alimentationCaisse.No, TypeAction.Modification, alimentationCaisse.SocieteNo);
		transactionScope.Complete();
	}

	public void Accepter(int no)
	{
		AlimentationCaisse alimentationCaisse = _alimentationCaisseRepository.Get(no);
		if (alimentationCaisse == null)
		{
			throw new ApplicationException($"Impossible de charger l'alimentation caisse n°[{no}]");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		alimentationCaisse.StatutAlimentation = StatutAlimentationCaisse.Accepte;
		_alimentationCaisseRepository.UpdateStatutAlimentation(alimentationCaisse);
		HistoriqueMvt historiqueMvt = new HistoriqueMvt(alimentationCaisse.No, alimentationCaisse.CaisseNo, SensMouvement.Entree, MouvementDomaine.AlimentationCaisse, alimentationCaisse.ModeNo, alimentationCaisse.No, 0, StatutTransfert.None, alimentationCaisse.DeviseNo, new Lot(alimentationCaisse.No, alimentationCaisse.Montant, alimentationCaisse.Montant));
		_historiqueRepository.Create(historiqueMvt);
		_notifyService.Notify(TypeEntity.Alimentation, alimentationCaisse.No, TypeAction.Modification, alimentationCaisse.SocieteNo);
		transactionScope.Complete();
	}

	public int Create(string numero, DateTime date, int banqueNo, int caisseNo, int modeNo, decimal montant, decimal cours, string pieceNumero, string libelle, int deviseNo, int? chequeNo, string beneficiaire)
	{
		Societe societe = SocieteManager.Societe;
		CaisseManager caisseManager = SocieteManager.CaisseManager;
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		if (string.IsNullOrEmpty(numero))
		{
			throw new ArgumentException("Le numéro est invalide!");
		}
		if (banqueNo <= 0)
		{
			throw new ApplicationException("La banque invalide!");
		}
		if (modeNo <= 0)
		{
			throw new ApplicationException("Le mode de règlement est invalide!");
		}
		if (deviseNo <= 0)
		{
			throw new ApplicationException("La devise est invalide!");
		}
		if (_alimentationCaisseRepository.Get(societe.No, numero) != null)
		{
			throw new ArgumentException("Le numéro d'alimentation de la caisse existe déjà!");
		}
		if (caisseNo <= 0)
		{
			throw new ArgumentException("La caisse No est invalide!");
		}
		Caisse caisse = caisseManager.Get(caisseNo);
		if (caisse == null)
		{
			throw new InvalidOperationException("La caisse est invalid!");
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Opération invalide! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisseNo))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		CaisseModeReglement mode = caisse.GetMode(modeNo);
		if (mode == null)
		{
			throw new InvalidOperationException("Le mode est invalide!");
		}
		if (mode.EnSommeil)
		{
			throw new InvalidOperationException("Opération invalide! Le mode est en sommeil!");
		}
		if (!caisse.HasModeReglement(modeNo))
		{
			throw new InvalidOperationException($"La caisse [{caisse.Code}] n'accepte pas le mode [{mode.Code}]!");
		}
		if (montant <= 0m)
		{
			throw new ApplicationException("Le montant est invalide!");
		}
		if (cours <= 0m)
		{
			throw new ApplicationException("Le cours est invalide!");
		}
		if (string.IsNullOrEmpty(pieceNumero))
		{
			throw new ArgumentException("Le numéro de la pièce est invalide!");
		}
		if (chequeNo.HasValue && string.IsNullOrEmpty(beneficiaire))
		{
			throw new ApplicationException("Bénéficiaire invalide.");
		}
		if (banqueNo > 0)
		{
			InformationsBanque byBanqueId = SocieteManager.Groupe.InformationBanqueManager.GetByBanqueId(banqueNo);
			if (byBanqueId != null && byBanqueId.EnSommeil)
			{
				throw new ApplicationException("Impossible de créer l'alimentation! La banque est en sommeil");
			}
		}
		SocieteDevise defaultDeviseSociete = societe.GetDefaultDeviseSociete();
		decimal montantDeviseSociete = Math.Round(montant * cours, defaultDeviseSociete.NombreDecimales, MidpointRounding.AwayFromZero);
		AlimentationCaisse alimentationCaisse = new AlimentationCaisse
		{
			Date = date,
			PieceNumero = pieceNumero,
			CaisseNo = caisseNo,
			BanqueNo = banqueNo,
			ModeNo = modeNo,
			Libelle = libelle,
			Montant = montant,
			Cours = cours,
			MontantDeviseSociete = montantDeviseSociete,
			Numero = numero,
			DeviseNo = deviseNo,
			SocieteNo = societe.No,
			UtilisateurNo = utilisateur.No,
			ModificateurNo = utilisateur.No,
			ChequeNo = chequeNo,
			Beneficiaire = beneficiaire,
			StatutAlimentation = ((!societe.IsAcceptAlimentationCaisse) ? StatutAlimentationCaisse.Accepte : StatutAlimentationCaisse.EnAttente)
		};
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		if (chequeNo.HasValue)
		{
			Cheque cheque = _chequeRepository.Get(chequeNo.Value);
			if (cheque == null)
			{
				throw new InvalidOperationException("Chèque invalide!");
			}
			if (cheque.Statut != ChequeStatut.NonUtilise)
			{
				throw new InvalidOperationException("Chèque est déjà utilisé");
			}
			if (societe.LegislationType == Legislation.Tunisie)
			{
				if (!cheque.MontantPlafond.HasValue)
				{
					throw new InvalidOperationException("Le montant du plafond est inexistant!");
				}
				if (cheque.MontantPlafond.Value < montant)
				{
					throw new InvalidOperationException("Le montant est supérieur au montant plafond!");
				}
			}
			cheque.Echeance = alimentationCaisse.Date;
			cheque.Date = DateTime.Now;
			cheque.Statut = ChequeStatut.Utilise;
			cheque.Montant = montant;
			cheque.Tire = beneficiaire;
			alimentationCaisse.PieceNumero = cheque.Numero;
			_chequeRepository.Update(cheque);
		}
		int num = _alimentationCaisseRepository.Create(alimentationCaisse);
		if (num <= 0)
		{
			throw new InvalidOperationException("L'ajout de l'alimentation de caisse a échoué!");
		}
		if (!societe.IsAcceptAlimentationCaisse)
		{
			HistoriqueMvt historiqueMvt = new HistoriqueMvt(num, caisseNo, SensMouvement.Entree, MouvementDomaine.AlimentationCaisse, modeNo, num, 0, StatutTransfert.None, deviseNo, new Lot(num, montant, montant));
			_historiqueRepository.Create(historiqueMvt);
		}
		_notifyService.Notify(TypeEntity.Alimentation, num, TypeAction.Ajout, caisse.SocieteNo);
		transactionScope.Complete();
		return num;
	}

	public async Task<IList<AlimentationCaisse>> GetAllAComptaAsync(DateTime dateMin, DateTime dateMax, EtatComptabilite etat, int[] caissesNo, int[] modesNo, CancellationToken cancellationToken)
	{
		Societe societe = SocieteManager.Societe;
		return (await _alimentationCaisseRepository.GetAllAComptaAsync(dateMin.Date, dateMax.Date.AddDays(1.0), etat, caissesNo, modesNo, societe.No, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).ToList();
	}

	public void DeComptabiliser(int mvtNo)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		AlimentationCaisse alimentationCaisse = Get(mvtNo);
		if (alimentationCaisse == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorAlimentationInvalid);
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		if (alimentationCaisse.IsComptabilise == EtatComptabilite.NonComptabilise)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorAlimentationNonComptabilise);
		}
		_ecritureComptaRepository.DeleteLigneAlimentation(alimentationCaisse.No);
		alimentationCaisse.ChangeEtatComptabilise(EtatComptabilite.NonComptabilise);
		alimentationCaisse.ModificateurNo = utilisateur.No;
		_alimentationCaisseRepository.Update(alimentationCaisse);
		_notifyService.Notify(TypeEntity.Alimentation, alimentationCaisse.No, TypeAction.Modification, alimentationCaisse.SocieteNo);
		transactionScope.Complete();
	}

	public void Delete(int alimentationNo, bool reutiliser)
	{
		Societe societe = SocieteManager.Societe;
		CaisseManager caisseManager = SocieteManager.CaisseManager;
		AlimentationCaisse alimentationCaisse = _alimentationCaisseRepository.Get(alimentationNo);
		if (alimentationCaisse == null)
		{
			throw new ArgumentNullException("alimentationNo");
		}
		Caisse caisse = societe.GetCaisse(alimentationCaisse.CaisseNo);
		if (caisse == null)
		{
			throw new ApplicationException("Caisse invalide!");
		}
		if (caisse.EnSommeil)
		{
			throw new ApplicationException("Opération invalide! La caisse est en sommeil!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisse.No))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
		}
		if (alimentationCaisse.IsRapproche)
		{
			throw new InvalidOperationException("L'alimentation est rapproché!");
		}
		if (alimentationCaisse.IsComptabilise == EtatComptabilite.Comptabilise)
		{
			throw new InvalidOperationException("L'alimentation est comptabilisé!");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		if (alimentationCaisse.ChequeNo.HasValue)
		{
			int valueOrDefault = alimentationCaisse.ChequeNo.GetValueOrDefault();
			if (reutiliser)
			{
				caisseManager.ReUtiliserChequeFrs(valueOrDefault);
			}
			else
			{
				string motifAnnulation = "Suppression alimentation n°[" + alimentationCaisse.Numero + "]";
				caisseManager.AnnulerChequeFrs(valueOrDefault, motifAnnulation, isUsed: true);
			}
		}
		if (alimentationCaisse.StatutAlimentation == StatutAlimentationCaisse.Accepte)
		{
			HistoriqueMvt historiqueMvt = _historiqueRepository.GetAllByMouvement(alimentationCaisse.No).SingleOrDefault((HistoriqueMvt x) => x.Sens == SensMouvement.Entree);
			if (historiqueMvt == null)
			{
				throw new InvalidOperationException("Historique d'entrée du mouvement est invalide!");
			}
			Lot lot = historiqueMvt.Lot;
			if (lot == null)
			{
				throw new InvalidOperationException("Lot d'entree invalide!");
			}
			if (lot.IsEpuise)
			{
				throw new InvalidOperationException("Le lot d'entrée est totalement utilisée");
			}
			if (lot.Montant != lot.MontantRestant)
			{
				throw new InvalidOperationException("Le lot d'entrée a subit un mouvement!");
			}
			_historiqueRepository.Delete(historiqueMvt);
		}
		_alimentationCaisseRepository.Delete(alimentationCaisse);
		_notifyService.Notify(TypeEntity.Alimentation, alimentationCaisse.No, TypeAction.Suppression, caisse.SocieteNo);
		transactionScope.Complete();
	}

	public AlimentationCaisse Get(int no)
	{
		return _alimentationCaisseRepository.Get(no);
	}

	public AlimentationCaisse Get(string numero, Societe societe = null)
	{
		societe = societe ?? SocieteManager.Societe;
		return _alimentationCaisseRepository.Get(societe.No, numero);
	}

	public IEnumerable<AlimentationCaisse> GetAll()
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		if (utilisateur.IsAdmin)
		{
			return _alimentationCaisseRepository.GetAll(SocieteManager.Societe.No);
		}
		int[] caisses = (from a in _autorisationCaisseRepository.GetAll(utilisateur.No, SocieteManager.Societe.No, ProfilType.Grf)
			select a.CaisseNo).ToArray();
		return (from x in _alimentationCaisseRepository.GetAll(SocieteManager.Societe.No)
			where caisses.Any((int y) => y == x.CaisseNo)
			orderby x.Date descending
			select x).ToList();
	}

	public IList<EcritureComptable> GetEcritures(int alimentationNo)
	{
		if (alimentationNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorMvtNo);
		}
		AlimentationCaisse alimentationCaisse = Get(alimentationNo);
		if (alimentationCaisse == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorAlimentationInvalid);
		}
		return _ecritureComptaRepository.GetAll(alimentationCaisse.No, MouvementDomaine.AlimentationCaisse);
	}
}
