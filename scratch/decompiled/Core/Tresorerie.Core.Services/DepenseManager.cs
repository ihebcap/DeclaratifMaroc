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

public class DepenseManager
{
	private readonly IMouvementDepenseRepository _mouvementDepenseRepository;

	private readonly NotifyService _notifyService;

	private readonly IEcritureComptaRepository _ecritureComptaRepository;

	private readonly IHistoriqueMvtRepository _historiqueRepository;

	private readonly ITypeDepenseRepository _typeDepenseRepository;

	private readonly IAutorisationCaisseRepository _autorisationCaisseRepository;

	private readonly IVentilationAnalytiqueRepository _ventilationAnalytiqueRepository;

	public GroupeService Groupe { get; set; }

	private SocieteManager SocieteManager => Groupe?.SocieteManager;

	public DepenseManager(IMouvementDepenseRepository mouvementDepenseRepository, NotifyService notifyService, IEcritureComptaRepository ecritureComptaRepository, IHistoriqueMvtRepository historiqueRepository, ITypeDepenseRepository typeDepenseRepository, IAutorisationCaisseRepository autorisationCaisseRepository, IVentilationAnalytiqueRepository ventilationAnalytiqueRepository)
	{
		_mouvementDepenseRepository = mouvementDepenseRepository ?? throw new ArgumentNullException("mouvementDepenseRepository");
		_notifyService = notifyService ?? throw new ArgumentNullException("notifyService");
		_ecritureComptaRepository = ecritureComptaRepository ?? throw new ArgumentNullException("ecritureComptaRepository");
		_historiqueRepository = historiqueRepository ?? throw new ArgumentNullException("historiqueRepository");
		_typeDepenseRepository = typeDepenseRepository ?? throw new ArgumentNullException("typeDepenseRepository");
		_autorisationCaisseRepository = autorisationCaisseRepository ?? throw new ArgumentNullException("autorisationCaisseRepository");
		_ventilationAnalytiqueRepository = ventilationAnalytiqueRepository ?? throw new ArgumentNullException("ventilationAnalytiqueRepository");
	}

	public async Task<IList<MouvementDepense>> GetAllAComptaAsync(DateTime dateMin, DateTime dateMax, EtatComptabilite etat, int[] caissesNo, int[] modesNo, CancellationToken cancellationToken)
	{
		Societe societe = SocieteManager.Societe;
		return (await _mouvementDepenseRepository.GetAllAComptaAsync(dateMin.Date, dateMax.Date.AddDays(1.0), etat, caissesNo, modesNo, societe.No, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).ToList();
	}

	public void ComptabiliserMouvementDepense(int depenseNo, List<EcritureComptable> erpEcritures)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		MouvementDepense mouvementDepense = _mouvementDepenseRepository.Get(depenseNo);
		if (mouvementDepense == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorDepenseMvtInvalid);
		}
		if (mouvementDepense.IsComptabilise == EtatComptabilite.Comptabilise)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorDepenseMvtComptabilise);
		}
		foreach (EcritureComptable erpEcriture in erpEcritures)
		{
			_ecritureComptaRepository.Create(erpEcriture);
		}
		mouvementDepense.ChangeEtatComptabilise(EtatComptabilite.Comptabilise);
		mouvementDepense.ModificateurNo = utilisateur.No;
		_mouvementDepenseRepository.Update(mouvementDepense);
		_notifyService.Notify(TypeEntity.Depense, depenseNo, TypeAction.Modification, mouvementDepense.SocieteNo);
		transactionScope.Complete();
	}

	public void DeComptabiliserMouvementDepense(int mvtNo)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		MouvementDepense mouvementDepense = _mouvementDepenseRepository.Get(mvtNo);
		if (mouvementDepense == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorDepenseMvtInvalid);
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		if (mouvementDepense.IsComptabilise == EtatComptabilite.NonComptabilise)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorDepenseMvtNonComptabilise);
		}
		_ecritureComptaRepository.DeleteLigneMouvementDepense(mouvementDepense.No);
		mouvementDepense.ChangeEtatComptabilise(EtatComptabilite.NonComptabilise);
		mouvementDepense.ModificateurNo = utilisateur.No;
		_mouvementDepenseRepository.Update(mouvementDepense);
		_notifyService.Notify(TypeEntity.Depense, mvtNo, TypeAction.Modification, mouvementDepense.SocieteNo);
		transactionScope.Complete();
	}

	public IList<EcritureComptable> GetEcrituresDepense(int depenseMvtNo)
	{
		if (depenseMvtNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorMvtNo);
		}
		MouvementDepense mouvementDepense = _mouvementDepenseRepository.Get(depenseMvtNo);
		if (mouvementDepense == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorDepenseMvtInvalid);
		}
		return _ecritureComptaRepository.GetAll(mouvementDepense.No, MouvementDomaine.Depense);
	}

	public MouvementDepense GetMouvementDepense(int no)
	{
		return _mouvementDepenseRepository.Get(no);
	}

	public MouvementDepense GetMouvementDepenseByNumero(int societeNo, string numero)
	{
		return _mouvementDepenseRepository.Get(societeNo, numero);
	}

	public TypeDepense GetTypeDepense(int no)
	{
		return _typeDepenseRepository.Get(no);
	}

	public IEnumerable<TypeDepense> GetTypeDepense(string code, Societe societe = null)
	{
		if (string.IsNullOrEmpty(code))
		{
			throw new ApplicationException("Le code est invalide!");
		}
		societe = societe ?? SocieteManager.Societe;
		return _typeDepenseRepository.GetByCode(code, societe.No);
	}

	public void DepenseSetDeclarationTva(int depenseNo, int declarationNo)
	{
		MouvementDepense mouvementDepense = _mouvementDepenseRepository.Get(depenseNo);
		if (mouvementDepense == null)
		{
			throw new ApplicationException("Impossible de charger la dépense.");
		}
		if (mouvementDepense.DeclarationTvaEncaissementNo.HasValue)
		{
			throw new ApplicationException("La dépense [" + mouvementDepense.Numero + "] est inclut dans une déclaration.");
		}
		if (!mouvementDepense.WithTva)
		{
			throw new ApplicationException("La dépense [" + mouvementDepense.Numero + "] n'a pas de TVA.");
		}
		DeclarationTvaEncaissement declarationTvaEncaissement = SocieteManager.DeclarationTvaEncaissementGet(declarationNo);
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
		mouvementDepense.DeclarationTvaEncaissementNo = declarationNo;
		_mouvementDepenseRepository.Update(mouvementDepense);
		_notifyService.Notify(TypeEntity.Depense, depenseNo, TypeAction.Modification, mouvementDepense.SocieteNo);
		transactionScope.Complete();
	}

	public void DepenseAnnulerDeclarationTva(int depenseNo)
	{
		MouvementDepense mouvementDepense = _mouvementDepenseRepository.Get(depenseNo);
		if (mouvementDepense == null)
		{
			throw new ApplicationException("Impossible de charger la dépense.");
		}
		if (!mouvementDepense.DeclarationTvaEncaissementNo.HasValue)
		{
			throw new ApplicationException("La dépense [" + mouvementDepense.Numero + "] n'est pas inclut dans une déclaration.");
		}
		if (!mouvementDepense.WithTva)
		{
			throw new ApplicationException("La dépense [" + mouvementDepense.Numero + "] n'a pas de TVA.");
		}
		DeclarationTvaEncaissement declarationTvaEncaissement = SocieteManager.DeclarationTvaEncaissementGet(mouvementDepense.DeclarationTvaEncaissementNo.Value);
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
		mouvementDepense.DeclarationTvaEncaissementNo = null;
		_mouvementDepenseRepository.Update(mouvementDepense);
		_notifyService.Notify(TypeEntity.Depense, depenseNo, TypeAction.Modification, mouvementDepense.SocieteNo);
		transactionScope.Complete();
	}

	public void UpdateTypeDepense(int no, string intitule, string compteGeneral)
	{
		TypeDepense typeDepense = _typeDepenseRepository.Get(no);
		if (typeDepense == null)
		{
			throw new ArgumentNullException("La dépense est invalide!");
		}
		typeDepense.Intitule = intitule;
		typeDepense.CompteGeneral = compteGeneral;
		_typeDepenseRepository.Update(typeDepense);
	}

	public void UpdateMouvementDepense(int mvDepenseNo, string pieceNumero, int typeDepenseNo, decimal montant, decimal cours, string libelle, string collaborateur, string affaireNumero, string raisonSociale, string ice, string identifiant)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		Societe societe = SocieteManager.Societe;
		if (mvDepenseNo <= 0)
		{
			throw new ArgumentException("Le mouvement de la dépense No est invalide!");
		}
		if (typeDepenseNo <= 0)
		{
			throw new ArgumentException("Le type de dépense est invalide!");
		}
		if (_typeDepenseRepository.Get(typeDepenseNo) == null)
		{
			throw new ApplicationException("Le type de dépense est inexistant!");
		}
		if (montant <= 0m)
		{
			throw new ApplicationException("Le montant est invalide!");
		}
		if (cours <= 0m)
		{
			throw new ApplicationException("Le cours est invalide!");
		}
		if (societe.VentilationAnalytique == TypeVentilationAnalytique.Affaire && string.IsNullOrEmpty(affaireNumero))
		{
			throw new ApplicationException("Le code affaire est obligatoire !");
		}
		if (string.IsNullOrEmpty(libelle))
		{
			throw new ArgumentException("Le libellé est invalide!");
		}
		MouvementDepense mouvementDepense = _mouvementDepenseRepository.Get(mvDepenseNo);
		if (mouvementDepense == null)
		{
			throw new ApplicationException("Mouvement dépense invalide!");
		}
		if (mouvementDepense.IsComptabilise != EtatComptabilite.NonComptabilise)
		{
			throw new ApplicationException("La dépense est comptabilisée!");
		}
		decimal solde = SocieteManager.HistoriqueMvtManager.GetSolde(mouvementDepense.CaisseNo, mouvementDepense.ModeNo, mouvementDepense.DeviseNo);
		if (montant - mouvementDepense.Montant > solde)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorMontantInavlide);
		}
		SocieteDevise defaultDeviseSociete = societe.GetDefaultDeviseSociete();
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		if (montant > mouvementDepense.Montant)
		{
			List<HistoriqueMvt> list = _historiqueRepository.GetAllNonEpuise(mouvementDepense.CaisseNo, mouvementDepense.ModeNo, mouvementDepense.DeviseNo).ToList();
			int num = 0;
			decimal num2 = montant - mouvementDepense.Montant;
			while (num2 > 0m && num < list.Count)
			{
				HistoriqueMvt historiqueMvt = list[num];
				Lot lot = historiqueMvt.Lot;
				decimal montantRestant = lot.MontantRestant;
				lot.MontantRestant = ((lot.MontantRestant >= num2) ? (lot.MontantRestant - num2) : 0m);
				_historiqueRepository.Update(historiqueMvt);
				HistoriqueMvt historiqueMvt2 = new HistoriqueMvt(lot.No, mouvementDepense.CaisseNo, SensMouvement.Sortie, MouvementDomaine.Depense, mouvementDepense.ModeNo, lot.No, mvDepenseNo, StatutTransfert.None, mouvementDepense.DeviseNo, new Lot(lot.No, (montantRestant >= num2) ? num2 : montantRestant, 0m));
				_historiqueRepository.Create(historiqueMvt2);
				num2 -= montantRestant;
				num++;
			}
		}
		if (montant < mouvementDepense.Montant)
		{
			List<HistoriqueMvt> list2 = (from x in _historiqueRepository.GetAllByCaisse(mouvementDepense.CaisseNo, mouvementDepense.DeviseNo)
				where x.MouvementOutNo == mvDepenseNo
				select x).ToList();
			decimal num3 = mouvementDepense.Montant - montant;
			foreach (HistoriqueMvt item in list2)
			{
				if (!(num3 <= 0m))
				{
					HistoriqueMvt historiqueMvt3 = _historiqueRepository.GetAllByMouvement(item.MouvementInNo).SingleOrDefault((HistoriqueMvt x) => x.Sens == SensMouvement.Entree);
					if (historiqueMvt3 == null)
					{
						throw new ArgumentNullException("L'historique d'entrée est invalide!");
					}
					decimal num4 = Math.Min(item.Montant, num3);
					historiqueMvt3.Lot.MontantRestant += num4;
					_historiqueRepository.Update(historiqueMvt3);
					Lot lot2 = item.Lot;
					lot2.Montant -= num4;
					lot2.MontantRestant -= num4;
					if (lot2.Montant <= 0m)
					{
						_historiqueRepository.Delete(item);
					}
					else
					{
						_historiqueRepository.Update(item);
					}
					num3 -= num4;
				}
			}
		}
		mouvementDepense.TypeDepenseNo = typeDepenseNo;
		mouvementDepense.Libelle = libelle ?? string.Empty;
		mouvementDepense.Collaborateur = collaborateur;
		mouvementDepense.ModificateurNo = utilisateur.No;
		mouvementDepense.PieceNumero = pieceNumero ?? string.Empty;
		mouvementDepense.Montant = montant;
		mouvementDepense.Cours = cours;
		mouvementDepense.MontantDeviseSociete = Math.Round(montant * cours, defaultDeviseSociete.NombreDecimales, MidpointRounding.AwayFromZero);
		mouvementDepense.AffaireNumero = affaireNumero;
		mouvementDepense.RaisonSociale = raisonSociale;
		mouvementDepense.Ice = ice;
		mouvementDepense.Identifiant = identifiant;
		_mouvementDepenseRepository.Update(mouvementDepense);
		_notifyService.Notify(TypeEntity.Depense, mvDepenseNo, TypeAction.Modification, mouvementDepense.SocieteNo);
		transactionScope.Complete();
	}

	public int CreateTypeDepense(string code, string intitule, string compteGeneral, Societe societe = null)
	{
		if (string.IsNullOrEmpty(code))
		{
			throw new ApplicationException("Le code est invalide!");
		}
		if (string.IsNullOrEmpty(intitule))
		{
			throw new ApplicationException("L'intitulé est invalide!");
		}
		if (string.IsNullOrEmpty(compteGeneral))
		{
			throw new ApplicationException("Le compte général est invalide");
		}
		societe = societe ?? SocieteManager.Societe;
		if (_typeDepenseRepository.Get(code, societe.No) != null)
		{
			throw new InvalidOperationException("La dépense existe déjà!");
		}
		TypeDepense depense = new TypeDepense
		{
			Code = code,
			CompteGeneral = compteGeneral,
			Intitule = intitule,
			SocieteNo = societe.No
		};
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		int result = _typeDepenseRepository.Create(depense);
		transactionScope.Complete();
		return result;
	}

	public int CreateMouvementDepense(string numero, DateTime date, decimal montant, string pieceNumero, string affaireNumero, int typeDepenseNo, int caisseNo, int modeNo, int deviseNo, decimal cours, string libelle, string collaborateur, int nbTimbre, decimal montantTimbre, string raisonSociale, string ice, string Identifiant, int? banqueNo, decimal montantTva = 0m, IErpTvaDeductible tva = null)
	{
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
		if (typeDepenseNo <= 0)
		{
			throw new ApplicationException("Le type de la dépense est invalide!");
		}
		if (pieceNumero == null)
		{
			pieceNumero = string.Empty;
		}
		if (montant <= 0m)
		{
			throw new ApplicationException("Le montant est invalide!");
		}
		if (cours <= 0m)
		{
			throw new ApplicationException("Le cours est invalide!");
		}
		if (montantTva < 0m)
		{
			throw new ApplicationException("Le montant TVA est invalide!");
		}
		if (string.IsNullOrEmpty(libelle))
		{
			throw new ApplicationException("Le libellé est invalide!");
		}
		if (nbTimbre < 0)
		{
			throw new ApplicationException("Nombre de timbre invalide!");
		}
		if (montantTimbre < 0m)
		{
			throw new ApplicationException("Montant timbre invalide!");
		}
		Societe societe = SocieteManager.Societe;
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		switch (societe.VentilationAnalytique)
		{
		case TypeVentilationAnalytique.Affaire:
			if (string.IsNullOrEmpty(affaireNumero))
			{
				throw new ApplicationException("Le code affaire est obligatoire !");
			}
			break;
		}
		if (_mouvementDepenseRepository.Get(societe.No, numero) != null)
		{
			throw new ArgumentException("Le numéro de la dépense existe déjà!");
		}
		if (_typeDepenseRepository.Get(typeDepenseNo) == null)
		{
			throw new ApplicationException("Le type de la dépense est invalide!");
		}
		Caisse caisse = SocieteManager.CaisseManager.Get(caisseNo);
		if (caisse == null)
		{
			throw new ArgumentException("La caisse de la dépense est invalide!");
		}
		if (!SocieteManager.UserHasAutorisationCaisse(caisseNo, ProfilType.Grf))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorAutorisationCaisse, caisse.Code));
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
			throw new ArgumentException("Le mode de règlement est invalide!");
		}
		if (mode.EnSommeil)
		{
			throw new ArgumentException("Opération invalide! Le mode est en sommeil!");
		}
		if (societe.GetSocieteDevises().SingleOrDefault((SocieteDevise x) => x.No == deviseNo) == null)
		{
			throw new ApplicationException($"Impossible de charger la devise [{deviseNo}].");
		}
		if (!caisse.HasModeReglement(modeNo))
		{
			throw new ArgumentException("Le mode de règlement est invalide!");
		}
		if (mode.Type != ReglementType.Espece && mode.Type != ReglementType.Virement)
		{
			throw new ArgumentException("Le type du mode de règlement est invalide!");
		}
		if (tva != null)
		{
			if (tva.No <= 0)
			{
				throw new ApplicationException("Impossible de charger le TVA.");
			}
			if (tva.Taux <= 0m)
			{
				throw new ApplicationException("Taux TVA invalide.");
			}
		}
		HistoriqueMvtManager historiqueMvtManager = SocieteManager.HistoriqueMvtManager;
		if (montant + montantTva + montantTimbre <= 0m)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorMontantInavlide);
		}
		if (mode.Type == ReglementType.Espece)
		{
			decimal solde = historiqueMvtManager.GetSolde(caisseNo, modeNo, deviseNo);
			if (montant + montantTva + montantTimbre <= 0m || montant + montantTva + montantTimbre > solde)
			{
				throw new ArgumentException(TresorerieCoreMessages.ErrorMontantInavlide);
			}
		}
		if (mode.Type == ReglementType.Virement && !banqueNo.HasValue)
		{
			throw new ApplicationException("La banque est invalide.");
		}
		SocieteDevise defaultDeviseSociete = societe.GetDefaultDeviseSociete();
		decimal montantDeviseSociete = Math.Round(montant * cours, defaultDeviseSociete.NombreDecimales, MidpointRounding.AwayFromZero);
		MouvementDepense mouvementDepense = new MouvementDepense
		{
			SocieteNo = societe.No,
			DeviseNo = deviseNo,
			UtilisateurNo = utilisateur.No,
			Numero = numero,
			Date = date,
			CaisseNo = caisseNo,
			ModeNo = modeNo,
			TypeDepenseNo = typeDepenseNo,
			PieceNumero = (pieceNumero ?? string.Empty),
			Montant = montant,
			MontantDeviseSociete = montantDeviseSociete,
			Cours = cours,
			Libelle = libelle,
			IsComptabilise = EtatComptabilite.NonComptabilise,
			Collaborateur = collaborateur,
			ModificateurNo = utilisateur.No,
			MontantTva = montantTva,
			WithTva = (tva != null),
			ErpTaxeNo = (tva?.No ?? 0),
			TauxTva = (tva?.Taux ?? 0m),
			NombreTimbre = nbTimbre,
			MontantTimbre = montantTimbre,
			AffaireNumero = affaireNumero,
			RaisonSociale = raisonSociale,
			Ice = ice,
			Identifiant = Identifiant,
			BanqueNo = banqueNo
		};
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		int num = _mouvementDepenseRepository.Create(mouvementDepense);
		if (num <= 0)
		{
			throw new InvalidOperationException("La création de la dépense a échoué!");
		}
		if (mode.Type == ReglementType.Espece)
		{
			List<HistoriqueMvt> list = _historiqueRepository.GetAllNonEpuise(caisseNo, modeNo, deviseNo).ToList();
			int num2 = 0;
			decimal num3 = montant + montantTva + montantTimbre;
			while (num3 > 0m && num2 < list.Count)
			{
				HistoriqueMvt historiqueMvt = list[num2];
				Lot lot = historiqueMvt.Lot;
				decimal montantRestant = lot.MontantRestant;
				lot.MontantRestant = ((lot.MontantRestant >= num3) ? (lot.MontantRestant - num3) : 0m);
				_historiqueRepository.Update(historiqueMvt);
				HistoriqueMvt historiqueMvt2 = new HistoriqueMvt(lot.No, caisseNo, SensMouvement.Sortie, MouvementDomaine.Depense, modeNo, lot.No, num, StatutTransfert.None, deviseNo, new Lot(lot.No, (montantRestant >= num3) ? num3 : montantRestant, 0m));
				_historiqueRepository.Create(historiqueMvt2);
				num3 -= montantRestant;
				num2++;
			}
		}
		_notifyService.Notify(TypeEntity.Depense, num, TypeAction.Ajout, mouvementDepense.SocieteNo);
		transactionScope.Complete();
		return num;
	}

	public void DeleteTypeDepense(int no)
	{
		if (_typeDepenseRepository.VerifDepenseUSed(no))
		{
			throw new InvalidOperationException("La dépense est déjà utilisée!");
		}
		TypeDepense typeDepense = _typeDepenseRepository.Get(no);
		if (typeDepense == null)
		{
			throw new ArgumentNullException("La dépense est invalide!");
		}
		_typeDepenseRepository.Delete(typeDepense);
	}

	public async Task DeleteMouvementDepenseAsync(int no)
	{
		MouvementDepense mvDepense = _mouvementDepenseRepository.Get(no);
		if (mvDepense == null)
		{
			throw new ArgumentNullException("Le mouvement de la dépense est invalide!");
		}
		if (mvDepense.IsComptabilise == EtatComptabilite.Comptabilise)
		{
			throw new InvalidOperationException("La dépense de la caisse est comptabilisée!");
		}
		if (mvDepense.DeclarationTvaEncaissementNo.HasValue)
		{
			throw new ApplicationException("Opération invalide! La dépense est intégrée dans une déclaration TVA/Encaissement.");
		}
		List<HistoriqueMvt> list = (from x in _historiqueRepository.GetAllByCaisse(mvDepense.CaisseNo, mvDepense.DeviseNo)
			where x.MouvementOutNo == no
			select x).ToList();
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope scope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		foreach (HistoriqueMvt item in list)
		{
			HistoriqueMvt historiqueMvt = _historiqueRepository.GetAllByMouvement(item.MouvementInNo).Single((HistoriqueMvt x) => x.Sens == SensMouvement.Entree);
			if (historiqueMvt == null)
			{
				throw new ArgumentNullException("L'historique d'entrée est invalide!");
			}
			historiqueMvt.Lot.MontantRestant += item.Montant;
			_historiqueRepository.Update(historiqueMvt);
			_historiqueRepository.Delete(item);
		}
		await _ventilationAnalytiqueRepository.DeleteByEntityAsync(mvDepense.SocieteNo, no, AnalytiqueDomaine.Depense).ConfigureAwait(continueOnCapturedContext: false);
		_mouvementDepenseRepository.Delete(no);
		_notifyService.Notify(TypeEntity.Depense, no, TypeAction.Suppression, mvDepense.SocieteNo);
		scope.Complete();
	}

	public IEnumerable<TypeDepense> GetAllTypeDepense()
	{
		return _typeDepenseRepository.GetAll(SocieteManager.Societe.No);
	}

	public IEnumerable<MouvementDepense> GetAllMouvementDepense(Societe societe = null)
	{
		societe = societe ?? SocieteManager.Societe;
		if (SocieteManager.Utilisateur.IsAdmin)
		{
			return _mouvementDepenseRepository.GetAll(societe.No);
		}
		int[] caisses = (from a in _autorisationCaisseRepository.GetAll(SocieteManager.Utilisateur.No, societe.No, ProfilType.Grf)
			select a.CaisseNo).ToArray();
		return (from x in _mouvementDepenseRepository.GetAll(societe.No)
			where caisses.Any((int y) => y == x.CaisseNo)
			orderby x.Date descending
			select x).ToList();
	}

	public IEnumerable<MouvementDepense> GetAllMouvementDepense(EtatComptabilite etat, Societe societe = null)
	{
		societe = societe ?? SocieteManager.Societe;
		if (SocieteManager.Utilisateur.IsAdmin)
		{
			return _mouvementDepenseRepository.GetAll(societe.No, etat);
		}
		int[] caisses = (from a in _autorisationCaisseRepository.GetAll(SocieteManager.Utilisateur.No, societe.No, ProfilType.Grf)
			select a.CaisseNo).ToArray();
		return (from x in _mouvementDepenseRepository.GetAll(societe.No, etat)
			where caisses.Any((int y) => y == x.CaisseNo)
			orderby x.Date descending
			select x).ToList();
	}

	public IEnumerable<MouvementDepense> GetAllMouvementDepenseToDeclarationTvaEncaissement(DateTime dateDebutExercice, DateTime dateFin, Societe societe = null)
	{
		societe = societe ?? SocieteManager.Societe;
		if (SocieteManager.Utilisateur.IsAdmin)
		{
			return _mouvementDepenseRepository.GetAllToDeclarationTvaEncaissement(societe.No, dateDebutExercice, dateFin);
		}
		int[] caisses = (from a in _autorisationCaisseRepository.GetAll(SocieteManager.Utilisateur.No, societe.No, ProfilType.Grf)
			select a.CaisseNo).ToArray();
		return (from x in _mouvementDepenseRepository.GetAllToDeclarationTvaEncaissement(societe.No, dateDebutExercice, dateFin)
			where caisses.Any((int y) => y == x.CaisseNo)
			orderby x.Date descending
			select x).ToList();
	}
}
