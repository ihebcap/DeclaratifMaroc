using System;
using System.Collections.Generic;
using System.Linq;
using System.Transactions;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Interfaces;
using Tresorerie.Core.Models;
using Tresorerie.Infrastructure.Helpers;
using Tresorerie.Ressources;

namespace Tresorerie.Core.Services;

public class CreditManager
{
	private readonly ICreditRepository _creditRepository;

	private readonly ILigneCreditRepository _ligneCreditRepository;

	private readonly IGarentieCreditRepository _garentieCreditRepository;

	private readonly NotifyService _notifyService;

	private List<JoursRepos> _joursReposCollection;

	public GroupeService Groupe { get; set; }

	public CreditManager(ICreditRepository creditRepository, NotifyService notifyService, ILigneCreditRepository ligneCreditRepository, IGarentieCreditRepository garentieCreditRepository)
	{
		_creditRepository = creditRepository ?? throw new ArgumentNullException("creditRepository");
		_notifyService = notifyService ?? throw new ArgumentNullException("notifyService");
		_ligneCreditRepository = ligneCreditRepository ?? throw new ArgumentNullException("ligneCreditRepository");
		_garentieCreditRepository = garentieCreditRepository ?? throw new ArgumentNullException("garentieCreditRepository");
	}

	public IEnumerable<Credit> GetAllCredit(Societe societe = null)
	{
		SocieteManager societeManager = Groupe.SocieteManager;
		societe = societe ?? societeManager.Societe;
		return _creditRepository.GetAll(societe.No);
	}

	public IEnumerable<Credit> GetAllCredit(int banqueNo, DateTime dateDebut, DateTime dateFin, NatureTypeCredit nature, Societe societe = null)
	{
		SocieteManager societeManager = Groupe.SocieteManager;
		societe = societe ?? societeManager.Societe;
		return _creditRepository.GetAll(banqueNo, societe.No, dateDebut, dateFin, nature);
	}

	public IEnumerable<Credit> GetAllCreditWithAffaire(Societe societe = null)
	{
		SocieteManager societeManager = Groupe.SocieteManager;
		societe = societe ?? societeManager.Societe;
		return _creditRepository.GetAllWithAffaire(societe.No);
	}

	public Credit GetCredit(int no)
	{
		return _creditRepository.Get(no) ?? throw new ApplicationException($"Impossible de charger le crédit [{no}]");
	}

	public bool IsCreditUtiliseType(int typeNo)
	{
		return _creditRepository.IsCreditUtiliseType(typeNo);
	}

	public bool IsCreditUtiliseGarentie(int garentieNo)
	{
		return _garentieCreditRepository.IsGarentieUtilise(garentieNo);
	}

	public int CreateCredit(Credit credit, IList<LigneCredit> lignes, IList<GarantieCredit> garentieCredits)
	{
		if (credit == null)
		{
			throw new ArgumentNullException("credit");
		}
		SocieteManager societeManager = Groupe.SocieteManager;
		InformationBanqueManager informationBanqueManager = Groupe.InformationBanqueManager;
		int no = societeManager.Societe.No;
		if (_creditRepository.Get(no, credit.Numero) != null)
		{
			throw new ApplicationException("Le crédit [" + credit.Numero + "] existe déja.");
		}
		credit.Date.Verifier("Crédit");
		if (credit.Montant <= 0m)
		{
			throw new ApplicationException("Montant invalide");
		}
		if (string.IsNullOrEmpty(credit.Numero))
		{
			throw new ApplicationException("Numéro invalide.");
		}
		credit.FirstEcheance.Verifier("première échéance");
		credit.DateDeblocage.Verifier("de déblocage");
		if (credit.FirstEcheance.Date < credit.DateDeblocage.Date)
		{
			throw new ApplicationException("La date de déblocage ne peut pas être postérieure à la première échéance.");
		}
		if (string.IsNullOrEmpty(credit.Intitule))
		{
			throw new ApplicationException("Intitulé invalide.");
		}
		if (credit.NbMois <= 0)
		{
			throw new ApplicationException("Le nombre de mois est invalide.");
		}
		if (informationBanqueManager.Get(credit.BanqueNo) == null)
		{
			throw new ApplicationException($"Impossible de charger la banque [{credit.BanqueNo}]");
		}
		if (societeManager.TypeCreditGet(credit.TypeNo) == null)
		{
			throw new ApplicationException("Impossible de charger le type de crédit.");
		}
		if (credit.TauxInterret < 0m || credit.TauxInterret > 100m)
		{
			throw new ApplicationException("Le taux d’intérêt est invalide.");
		}
		if (credit.Statut == StatutCredit.Accorder)
		{
			decimal num = default(decimal);
			foreach (LigneCredit ligne in lignes)
			{
				ValidatorligneCredit(ligne, credit.FirstEcheance);
				num += ligne.MontantCapitalRembourse;
			}
			if (credit.Montant != num)
			{
				throw new ApplicationException("La somme des capitaux remboursés sur les lignes ne correspond pas au montant du crédit.");
			}
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		int num2 = _creditRepository.Create(credit);
		if (credit.DocumentNo.HasValue)
		{
			societeManager.AffecterCreditEcheance(credit.DocumentNo.Value, num2, credit.Numero);
		}
		foreach (LigneCredit ligne2 in lignes)
		{
			ligne2.CreditNo = num2;
			ligne2.No = _ligneCreditRepository.Create(ligne2);
		}
		int num3 = 1;
		foreach (GarantieCredit garentieCredit in garentieCredits)
		{
			if (garentieCredit.GarantieNo == 0)
			{
				throw new ApplicationException($"Impossible de charger la garantie ligne [{num3}].");
			}
			garentieCredit.CreditNo = num2;
			garentieCredit.DateCreation = DateTime.Now;
			garentieCredit.UtilisateurNo = Groupe.SocieteManager.Utilisateur.No;
			garentieCredit.No = _garentieCreditRepository.Create(garentieCredit);
		}
		_notifyService.Notify(TypeEntity.Credit, num2, TypeAction.Ajout, no);
		transactionScope.Complete();
		return num2;
	}

	public void DeleteCredit(int no)
	{
		Credit credit = _creditRepository.Get(no) ?? throw new ApplicationException($"Impossible de charger le crédit [{no}] ");
		if (credit.IsComptabiliser)
		{
			throw new ApplicationException("Le crédit [" + credit.Numero + "] est comptabilisé. ");
		}
		if (credit.Statut == StatutCredit.Accorder)
		{
			throw new ApplicationException("Le crédit [" + credit.Numero + "] est accordé");
		}
		if ((GetAllLigneCredit(credit.No) ?? throw new ApplicationException("Impossible de charger les lignes de crédit")).Any((LigneCredit x) => x.IsPointer))
		{
			throw new ApplicationException("Le crédit [" + credit.Numero + "] contient une ou plusieurs lignes qui sont pointées.");
		}
		SocieteManager societeManager = Groupe.SocieteManager;
		int no2 = societeManager.Societe.No;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		if (credit.DocumentNo.HasValue)
		{
			societeManager.DeleteAffecterCreditEcheance(credit.DocumentNo.Value);
		}
		_ligneCreditRepository.DeleteFromCredit(no);
		_creditRepository.Delete(no);
		_notifyService.Notify(TypeEntity.Credit, no, TypeAction.Suppression, no2);
		transactionScope.Complete();
	}

	public void UpdateCredit(Credit credit)
	{
		if (credit == null)
		{
			throw new ArgumentNullException("credit");
		}
		Credit credit2 = _creditRepository.Get(credit.No) ?? throw new ApplicationException($"Impossible de charger le crédit [{credit.No}]");
		if (credit2.IsComptabiliser)
		{
			throw new ApplicationException("Le crédit [" + credit.Numero + "] est comptabilisé.");
		}
		if (credit2.Statut == StatutCredit.Accorder)
		{
			throw new ApplicationException("Le crédit [" + credit.Numero + "] est accordé.");
		}
		List<LigneCredit> allLigneCredit = GetAllLigneCredit(credit.No);
		if (allLigneCredit.Any((LigneCredit x) => x.IsPointer))
		{
			throw new ApplicationException("Le crédit [" + credit.Numero + "] contient une ou plusieurs lignes qui sont pointées.");
		}
		if (string.IsNullOrEmpty(credit.Intitule))
		{
			throw new ApplicationException("Intitulé obligatoire");
		}
		if (credit.NbMois <= 0)
		{
			throw new ApplicationException("Nombre de mois invalide.");
		}
		if (credit.Montant <= 0m)
		{
			throw new ApplicationException(rcRessources.MontantInvalide);
		}
		credit.FirstEcheance.Verifier("première échéance");
		credit.DateDeblocage.Verifier("déblocage");
		if (credit.FirstEcheance < credit.DateDeblocage)
		{
			throw new ApplicationException("La date de déblocage est postérieure à la date de première échéance. ");
		}
		if (credit.TauxInterret < 0m || credit.TauxInterret > 100m)
		{
			throw new ApplicationException("Taux d’intérêt invalide.");
		}
		if (credit2.Statut != StatutCredit.Accorder && credit.Statut == StatutCredit.Accorder)
		{
			decimal num = default(decimal);
			foreach (LigneCredit item in allLigneCredit)
			{
				ValidatorligneCredit(item, credit.FirstEcheance);
				num += item.MontantCapitalRembourse;
			}
			if (credit.Montant != num)
			{
				throw new ApplicationException("La somme des capitaux remboursés sur les lignes ne correspond pas au montant du crédit.");
			}
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		if (credit.DocumentNo.HasValue)
		{
			Groupe.SocieteManager.AffecterCreditEcheance(credit.DocumentNo.Value, credit.No, credit.Numero);
		}
		else if (!credit.DocumentNo.HasValue && credit2.DocumentNo.HasValue)
		{
			Groupe.SocieteManager.DeleteAffecterCreditEcheance(credit2.DocumentNo.Value);
		}
		_creditRepository.Update(credit);
		_notifyService.Notify(TypeEntity.Credit, credit.No, TypeAction.Modification, credit.SocieteNo);
		transactionScope.Complete();
	}

	public void RembourserCredit(int creditNo, DateTime datePointage, string numExtrait)
	{
		GetCredit(creditNo);
		List<LigneCredit> source = GetAllLigneCredit(creditNo) ?? throw new ApplicationException("Impossible de charger les lignes de crédit");
		IEnumerable<LigneCredit> enumerable = source.Where((LigneCredit x) => !x.IsPointer);
		if (source.IsNullOrEmpty())
		{
			throw new ApplicationException("Le crédit est totalement remboursé.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		foreach (LigneCredit item in enumerable)
		{
			item.IsPointer = true;
			item.DatePointage = datePointage;
			item.NumExtrait = numExtrait;
			_ligneCreditRepository.SetPointer(item);
		}
		Societe societe = Groupe.SocieteManager.Societe;
		_notifyService.Notify(TypeEntity.Credit, creditNo, TypeAction.Modification, societe.No);
		transactionScope.Complete();
	}

	public List<LigneCredit> GetAllLigneCredit(int creditNo)
	{
		return _ligneCreditRepository.GetAll(creditNo).ToList();
	}

	public List<LigneCredit> GetAllLigneCredit(Societe societe = null)
	{
		int societeNo = societe?.No ?? Groupe.SocieteManager.Societe.No;
		return _ligneCreditRepository.GetAllBySocieteNo(societeNo).ToList();
	}

	public void DeleteAllLigneCredit(int creditNo)
	{
		Credit credit = GetCredit(creditNo);
		if (credit.Statut == StatutCredit.Accorder)
		{
			throw new ApplicationException("Credit avec statut accorder");
		}
		if (GetAllLigneCredit(credit.No).Any((LigneCredit x) => x.IsPointer))
		{
			throw new ApplicationException("Le crédit contient une ou plusieurs lignes qui sont pointées.");
		}
		_ligneCreditRepository.DeleteFromCredit(creditNo);
	}

	public void UpdateTauxInteretLigneCredit(LigneCredit ligne)
	{
		LigneCredit ligneCredit = _ligneCreditRepository.Get(ligne.No) ?? throw new ApplicationException($"Impossible de charger la ligne de crédit [{ligne.No}]");
		if ((_creditRepository.Get(ligneCredit.CreditNo) ?? throw new ArgumentNullException("credit")).Statut != StatutCredit.Accorder)
		{
			throw new ApplicationException("Le crédit n’est pas accordé.");
		}
		if (ligne.MontantInteret < 0m)
		{
			throw new ApplicationException("Le montant d’intérêt de la ligne [" + ligne.Numero + "] est invalide.");
		}
		if (ligne.TauxInteret < 0m || ligne.TauxInteret > 100m)
		{
			throw new ApplicationException("Le taux d’intérêt de la ligne [" + ligne.Numero + "] est invalide.");
		}
		_ligneCreditRepository.UpdateTauxInteretLigneCredit(ligne);
	}

	public void UpdateEcheanceLigneCredit(LigneCredit ligne)
	{
		if (_ligneCreditRepository.Get(ligne.No) == null)
		{
			throw new ApplicationException($"Impossible de charger la ligne de crédit [{ligne.No}]");
		}
		_ligneCreditRepository.UpdateEcheance(ligne.No, ligne.DateEcheance, ligne.Numero);
	}

	public void LigneCreditRapproched(int ligneNo, DateTime datePointage, string numExtrait)
	{
		LigneCredit ligneCredit = _ligneCreditRepository.Get(ligneNo);
		if (ligneCredit == null)
		{
			throw new ArgumentNullException("ligneNo");
		}
		if (ligneCredit.IsPointer)
		{
			throw new ApplicationException("La ligne crédit [" + ligneCredit.Numero + "] est déja pointée.");
		}
		Credit credit = _creditRepository.Get(ligneCredit.CreditNo);
		if (credit == null)
		{
			throw new ApplicationException("Impossible de charger le crédit de la ligne [" + ligneCredit.Numero + "].");
		}
		if (credit.Statut != StatutCredit.Accorder)
		{
			throw new ApplicationException("Le crédit [" + credit.Numero + "] n'est pas accordé.");
		}
		if (string.IsNullOrEmpty(numExtrait))
		{
			throw new ApplicationException("Numéro d'extrait invalide.");
		}
		if (datePointage.IsNotLogique())
		{
			throw new ApplicationException("La date de pointage est invalide.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		ligneCredit.IsPointer = true;
		ligneCredit.DatePointage = datePointage;
		ligneCredit.NumExtrait = numExtrait;
		_ligneCreditRepository.SetPointer(ligneCredit);
		_notifyService.Notify(TypeEntity.Credit, credit.No, TypeAction.Modification, credit.SocieteNo);
		transactionScope.Complete();
	}

	public void LigneCreditUnRapproched(int ligneNo)
	{
		LigneCredit ligneCredit = _ligneCreditRepository.Get(ligneNo);
		if (ligneCredit == null)
		{
			throw new ArgumentNullException("ligneNo");
		}
		if (!ligneCredit.IsPointer)
		{
			throw new ApplicationException("La ligne crédit [" + ligneCredit.Numero + "] n'est pas pointée.");
		}
		Credit credit = _creditRepository.Get(ligneCredit.CreditNo);
		if (credit == null)
		{
			throw new ApplicationException("Impossible de charger le crédit de la ligne [" + ligneCredit.Numero + "].");
		}
		if (credit.Statut != StatutCredit.Accorder)
		{
			throw new ApplicationException("Le crédit [" + credit.Numero + "] n'est pas accordé.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		ligneCredit.IsPointer = false;
		ligneCredit.DatePointage = null;
		ligneCredit.NumExtrait = string.Empty;
		_ligneCreditRepository.SetPointer(ligneCredit);
		_notifyService.Notify(TypeEntity.Credit, credit.No, TypeAction.Modification, credit.SocieteNo);
		transactionScope.Complete();
	}

	public IEnumerable<LigneCredit> GenerateLignes(Credit credit)
	{
		if (credit == null)
		{
			throw new ArgumentNullException("credit");
		}
		if (credit.NbMois == 0)
		{
			throw new ApplicationException("Le nombre de mois est invalide.");
		}
		if (credit.Montant == 0m)
		{
			throw new ApplicationException("Le montant du crédit est invalide.");
		}
		credit.FirstEcheance.Verifier("première échéance");
		if (credit.TauxInterret < 0m)
		{
			throw new ApplicationException("Le taux d'intérêt est invalide. ");
		}
		if (credit.No != 0 && (GetAllLigneCredit(credit.No) ?? throw new ApplicationException("Impossible de charger les lignes de crédit [" + credit.Numero + "]")).Any((LigneCredit x) => x.IsPointer))
		{
			throw new ApplicationException("Il existe une ou plusieurs lignes qui sont pointées.");
		}
		List<LigneCredit> list = new List<LigneCredit>();
		DateTime date = credit.DateDeblocage.AddMonths(1);
		int day = Math.Min(date.EndOfMonthDay(), credit.FirstEcheance.Day);
		date = new DateTime(date.Year, date.Month, day);
		int num = (credit.FirstEcheance.Date - date).Days / 30;
		if (credit.TauxInterret != 0m)
		{
			for (int num2 = num; num2 >= 1; num2--)
			{
				int rang = list.Count() + 1;
				DateTime dateEcheance = GetDateEcheance(credit, credit.FirstEcheance.AddMonths(-num2));
				list.Add(new LigneCredit
				{
					CreditNo = credit.No,
					Numero = GetNumeroEcheance(credit.Numero, dateEcheance, rang),
					DateEcheance = dateEcheance,
					TauxInteret = credit.TauxInterret,
					IsEcheanceReporter = true
				});
			}
		}
		for (int num3 = 1; num3 <= credit.NbMois; num3++)
		{
			DateTime dateEcheance2 = GetDateEcheance(credit, credit.FirstEcheance.AddMonths(num3 - 1));
			int rang2 = list.Count() + 1;
			list.Add(new LigneCredit
			{
				CreditNo = credit.No,
				Numero = GetNumeroEcheance(credit.Numero, dateEcheance2, rang2),
				DateEcheance = dateEcheance2,
				TauxInteret = credit.TauxInterret
			});
		}
		if (credit.MethodeCalcul == MethodeCalculTypeCredit.AmortissementConstant)
		{
			CalculAmortissementConstante(credit.Montant, credit.NbMois, credit.TauxInterret, credit.ConventionCalcul, list);
		}
		else
		{
			CalculAnnuiteConstante(credit.Montant, credit.NbMois, credit.TauxInterret, credit.ConventionCalcul, list);
		}
		if (credit.IsAssuranceVentiler)
		{
			CalculAssurance(credit.MontantAssurence, credit.NbMois, list);
		}
		if (credit.IsFraisDossierVentiler)
		{
			CalculFraisDossier(credit.FraisDossier, credit.NbMois, list);
		}
		if (credit.No != 0)
		{
			TransactionOptions transactionOptions = new TransactionOptions
			{
				IsolationLevel = IsolationLevel.ReadCommitted,
				Timeout = TransactionManager.MaximumTimeout
			};
			using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
			DeleteAllLigneCredit(credit.No);
			foreach (LigneCredit item in list)
			{
				int no = CreateLigneCredit(item);
				item.No = no;
			}
			Societe societe = Groupe.SocieteManager.Societe;
			_notifyService.Notify(TypeEntity.Credit, credit.No, TypeAction.Modification, societe.No);
			transactionScope.Complete();
		}
		return list;
	}

	public IEnumerable<LigneCredit> ReporteEcheance(Credit credit, LigneCredit selectedLigne, int nbMois)
	{
		if (credit == null)
		{
			throw new ArgumentNullException("Credit");
		}
		List<LigneCredit> allLigneCredit = GetAllLigneCredit(credit.No);
		if (allLigneCredit.IsNullOrEmpty())
		{
			throw new ApplicationException("Le crédit ne contient aucune ligne.");
		}
		if (selectedLigne == null)
		{
			throw new ApplicationException("Aucune ligne sélectionnée.");
		}
		if (nbMois == 0)
		{
			throw new ApplicationException("Nombre de mois invalide.");
		}
		if (allLigneCredit.Any((LigneCredit x) => x.DateEcheance >= selectedLigne.DateEcheance && x.IsPointer))
		{
			throw new ApplicationException("Certaines lignes postérieures à cette date ont déjà été pointées.");
		}
		int num = allLigneCredit.IndexOf(selectedLigne);
		decimal num2 = default(decimal);
		num2 = ((num != 0) ? allLigneCredit[num - 1].MontantCapitalRestantDu : credit.Montant);
		if (credit.TauxInterret > 0m)
		{
			for (int num3 = 0; num3 < nbMois; num3++)
			{
				allLigneCredit.Insert(num + num3, new LigneCredit
				{
					IsEcheanceReporter = true,
					CreditNo = credit.No,
					Numero = GetNumeroEcheance(credit.Numero, selectedLigne.DateEcheance.AddMonths(num3), num + num3 + 1),
					DateEcheance = selectedLigne.DateEcheance.AddMonths(num3),
					TauxInteret = selectedLigne.TauxInteret
				});
			}
		}
		List<LigneCredit> list = allLigneCredit.Where((LigneCredit x) => x.DateEcheance >= selectedLigne.DateEcheance).ToList();
		if (credit.MethodeCalcul == MethodeCalculTypeCredit.AnnuiteConstante)
		{
			CalculAnnuiteConstante(num2, list.Count(), selectedLigne.TauxInteret, credit.ConventionCalcul, list);
		}
		else
		{
			CalculAmortissementConstante(num2, list.Count() - nbMois, selectedLigne.TauxInteret, credit.ConventionCalcul, list);
		}
		if (credit.IsAssuranceVentiler)
		{
			CalculAssurance(credit.MontantAssurence, credit.NbMois, list);
		}
		if (credit.IsFraisDossierVentiler)
		{
			CalculFraisDossier(credit.FraisDossier, credit.NbMois, list);
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		int num4 = 1;
		foreach (LigneCredit item in list)
		{
			if (!item.IsEcheanceReporter)
			{
				DateTime dateEcheance = GetDateEcheance(credit, item.DateEcheance.AddMonths(nbMois));
				item.DateEcheance = dateEcheance;
			}
			if (item.No != 0)
			{
				UpdateEcheanceLigneCredit(item);
			}
			if (credit.No != 0 && item.IsEcheanceReporter)
			{
				int no = CreateLigneCredit(item);
				item.No = no;
			}
			item.IsEcheanceReporter = false;
			num4++;
		}
		Societe societe = Groupe.SocieteManager.Societe;
		_notifyService.Notify(TypeEntity.Credit, credit.No, TypeAction.Modification, societe.No);
		transactionScope.Complete();
		return allLigneCredit;
	}

	public IEnumerable<LigneCredit> ChangerTaux(Credit credit, LigneCredit selectedLigne, decimal newTaux)
	{
		if (credit == null)
		{
			throw new ArgumentNullException("Credit");
		}
		List<LigneCredit> allLigneCredit = GetAllLigneCredit(credit.No);
		if (allLigneCredit.IsNullOrEmpty())
		{
			throw new ApplicationException("Le crédit ne contient aucune ligne.");
		}
		if (selectedLigne == null)
		{
			throw new ApplicationException("Aucune ligne sélectionnée.");
		}
		if (newTaux < 0m)
		{
			throw new ApplicationException("Taux d'intérêt invalide.");
		}
		IEnumerable<LigneCredit> enumerable = allLigneCredit.Where((LigneCredit x) => x.DateEcheance >= selectedLigne.DateEcheance);
		if (enumerable.Any((LigneCredit x) => x.IsPointer))
		{
			throw new ApplicationException("Certaines lignes postérieures à cette date ont déjà été pointées.");
		}
		int num = allLigneCredit.IndexOf(selectedLigne);
		decimal num2 = default(decimal);
		num2 = ((num != 0) ? allLigneCredit[num - 1].MontantCapitalRestantDu : credit.Montant);
		foreach (LigneCredit item in enumerable)
		{
			item.IsEcheanceReporter = item.MontantCapitalRembourse == 0m;
		}
		if (credit.MethodeCalcul == MethodeCalculTypeCredit.AnnuiteConstante)
		{
			CalculAnnuiteConstante(num2, enumerable.Count(), newTaux, credit.ConventionCalcul, enumerable);
		}
		else
		{
			CalculAmortissementConstante(num2, enumerable.Where((LigneCredit x) => !x.IsEcheanceReporter).Count(), newTaux, credit.ConventionCalcul, enumerable);
		}
		if (credit.IsAssuranceVentiler)
		{
			CalculAssurance(credit.MontantAssurence, credit.NbMois, enumerable);
		}
		if (credit.IsFraisDossierVentiler)
		{
			CalculFraisDossier(credit.FraisDossier, credit.NbMois, enumerable);
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		foreach (LigneCredit item2 in enumerable)
		{
			if (item2.No != 0)
			{
				UpdateTauxInteretLigneCredit(item2);
			}
		}
		Societe societe = Groupe.SocieteManager.Societe;
		_notifyService.Notify(TypeEntity.Credit, credit.No, TypeAction.Modification, societe.No);
		transactionScope.Complete();
		return allLigneCredit;
	}

	public void CreditAnnulerAccord(int creditNo)
	{
		Credit credit = GetCredit(creditNo);
		if (credit == null)
		{
			throw new ArgumentNullException("credit");
		}
		if (credit.Statut != StatutCredit.Accorder)
		{
			throw new ApplicationException("Le crédit n’est pas accordé.");
		}
		if ((GetAllLigneCredit(credit.No) ?? throw new ApplicationException($"Impossible de charger les ligne de crédit [{credit.No}]")).Any((LigneCredit x) => x.IsPointer))
		{
			throw new ApplicationException(" Il existe une ou plusieurs lignes qui sont pointées.");
		}
		credit.Statut = StatutCredit.Confirmer;
		_creditRepository.AnnulerAccord(credit);
		_notifyService.Notify(TypeEntity.Credit, credit.No, TypeAction.Modification, credit.SocieteNo);
	}

	public void UpdateLigneCredit(LigneCredit ligne)
	{
		if (ligne == null)
		{
			throw new ArgumentNullException("ligne");
		}
		LigneCredit ligneCredit = _ligneCreditRepository.Get(ligne.No) ?? throw new ApplicationException("Impossible de charger la ligne [" + ligne.Numero + "]");
		Credit credit = _creditRepository.Get(ligne.CreditNo) ?? throw new ApplicationException($"Impossible de charger le credit [{ligne.CreditNo}]");
		ValidatorligneCredit(ligne, credit.FirstEcheance);
		ligneCredit.DateEcheance = ligne.DateEcheance;
		ligneCredit.TauxInteret = ligne.TauxInteret;
		ligneCredit.MontantMensualite = ligne.MontantMensualite;
		ligneCredit.MontantCapitalRembourse = ligne.MontantCapitalRembourse;
		ligneCredit.MontantInteret = ligne.MontantInteret;
		ligneCredit.MontantCapitalRestantDu = ligne.MontantCapitalRestantDu;
		ligneCredit.MontantAssurance = ligne.MontantAssurance;
		ligneCredit.MontantFraisDossier = ligne.MontantFraisDossier;
		if (credit.Statut == StatutCredit.Accorder)
		{
			throw new ApplicationException("Le crédit est accordé.");
		}
		if (ligneCredit.IsPointer)
		{
			throw new ApplicationException("La ligne de crédit est pointée.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		_ligneCreditRepository.Update(ligneCredit);
		RegenererNumeroLigneCredit(credit.No);
		_notifyService.Notify(TypeEntity.Credit, credit.No, TypeAction.Modification, credit.SocieteNo);
		transactionScope.Complete();
	}

	public void DeleteLigne(int ligneNo)
	{
		LigneCredit ligneCredit = _ligneCreditRepository.Get(ligneNo) ?? throw new ApplicationException($"Impossible de charger la ligne [{ligneNo}]");
		Credit credit = _creditRepository.Get(ligneCredit.CreditNo) ?? throw new ApplicationException($"Impossible de charger le credit [{ligneCredit.CreditNo}]");
		if (ligneCredit.IsPointer)
		{
			throw new ApplicationException("La ligne de crédit est pointée.");
		}
		if (credit.Statut == StatutCredit.Accorder)
		{
			throw new ApplicationException("Le crédit est accordé.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		_ligneCreditRepository.Delete(ligneNo);
		RegenererNumeroLigneCredit(credit.No);
		_notifyService.Notify(TypeEntity.Credit, credit.No, TypeAction.Modification, credit.SocieteNo);
		transactionScope.Complete();
	}

	public int CreateLigneCredit(LigneCredit ligne, bool IsModeRepporterEcheance = false)
	{
		if (ligne == null)
		{
			throw new ArgumentNullException("ligne");
		}
		Credit credit = GetCredit(ligne.CreditNo) ?? throw new ApplicationException($"Impossible de charger le credit [{ligne.CreditNo}]");
		ValidatorligneCredit(ligne, credit.FirstEcheance);
		if (credit.Statut == StatutCredit.Accorder && !IsModeRepporterEcheance)
		{
			throw new ApplicationException("Le crédit [" + credit.Numero + "] est accordé.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		int num = _ligneCreditRepository.Create(ligne);
		if (num <= 0)
		{
			throw new ApplicationException("Impossible d'ajouter la ligne crédit.");
		}
		RegenererNumeroLigneCredit(credit.No);
		_notifyService.Notify(TypeEntity.Credit, credit.No, TypeAction.Modification, credit.SocieteNo);
		transactionScope.Complete();
		return num;
	}

	public List<GarantieCredit> GetGarentieCreditCredit(int creditNo)
	{
		return _garentieCreditRepository.GetAllFromCredit(creditNo).ToList();
	}

	public void DeleteGarentieCredit(int garentieCreditNo)
	{
		GarantieCredit garantieCredit = _garentieCreditRepository.Get(garentieCreditNo) ?? throw new ArgumentNullException($"Impossible de charger la garantie de crédit [{garentieCreditNo}]");
		Credit credit = _creditRepository.Get(garantieCredit.CreditNo) ?? throw new ArgumentNullException($"Impossible de charger le crédit[{garantieCredit.CreditNo}]");
		if (credit.Statut == StatutCredit.Accorder)
		{
			throw new ApplicationException("Le statut de crédit est accordé");
		}
		_garentieCreditRepository.Delete(garentieCreditNo);
		_notifyService.Notify(TypeEntity.Credit, credit.No, TypeAction.Modification, credit.SocieteNo);
	}

	public int AjouterGarentieCredit(int creditNo, GarantieCredit garentieCredit)
	{
		Credit credit = GetCredit(creditNo);
		if (garentieCredit == null)
		{
			throw new ArgumentNullException("GarantieCredit");
		}
		Utilisateur utilisateur = Groupe.SocieteManager.Utilisateur;
		if (credit.Statut == StatutCredit.Accorder)
		{
			throw new ApplicationException("Le crédit est accordé.");
		}
		if (garentieCredit.GarantieNo == 0)
		{
			throw new ApplicationException($"Impossible de charger la garantie n°[{garentieCredit.GarantieNo}].");
		}
		if (garentieCredit.Rang <= 0)
		{
			throw new ApplicationException("Rang invalide.");
		}
		garentieCredit.DateCreation = DateTime.Today;
		garentieCredit.CreditNo = creditNo;
		garentieCredit.UtilisateurNo = utilisateur.No;
		return _garentieCreditRepository.Create(garentieCredit);
	}

	private void CalculAmortissementConstante(decimal montantCredit, int nbMois, decimal taux, ConventionCalculTypeCredit convention, IEnumerable<LigneCredit> collection)
	{
		collection.Any((LigneCredit x) => x.IsEcheanceReporter);
		decimal num = ((convention == ConventionCalculTypeCredit._30_360) ? 0.0833333333333333m : 0.0821917808219178m);
		decimal num2 = RoundValue(montantCredit / (decimal)nbMois);
		decimal num3 = montantCredit;
		int num4 = 1;
		foreach (LigneCredit item in collection)
		{
			decimal num5 = RoundValue(num3 * (taux / 100m) * num);
			decimal num6 = (item.IsEcheanceReporter ? 0m : num2);
			if (num4 == collection.Count())
			{
				num6 = num3;
			}
			num3 -= num6;
			item.MontantCapitalRembourse = num6;
			item.MontantInteret = num5;
			item.MontantCapitalRestantDu = Math.Max(0m, num3);
			item.TauxInteret = taux;
			item.MontantMensualite = num6 + num5;
			num4++;
		}
	}

	private void CalculAnnuiteConstante(decimal montantCredit, int nbMois, decimal taux, ConventionCalculTypeCredit convention, IEnumerable<LigneCredit> collection)
	{
		bool flag = collection.Any((LigneCredit x) => x.IsEcheanceReporter);
		decimal num = RoundValue(GetMonsualiteAnnuiteCanstante(montantCredit, taux, nbMois));
		decimal num2 = montantCredit;
		decimal num3 = ((convention == ConventionCalculTypeCredit._30_360) ? 0.0833333333333333m : 0.0821917808219178m);
		int num4 = 1;
		decimal num5 = default(decimal);
		foreach (LigneCredit item in collection)
		{
			if (flag)
			{
				num = RoundValue(GetMonsualiteAnnuiteCanstante(num2, taux, collection.Count() - num4 + 1));
			}
			decimal num6 = RoundValue(num2 * (taux / 100m) * num3);
			if (item.IsEcheanceReporter)
			{
				num = num6;
			}
			decimal num7 = ((num4 == collection.Count()) ? (montantCredit - num5) : (num - num6));
			num5 += num7;
			num2 -= RoundValue(num7);
			if (num4 == collection.Count())
			{
				num = num6 + num7;
			}
			num4++;
			item.MontantCapitalRestantDu = RoundValue(num2);
			item.MontantInteret = RoundValue(num6);
			item.TauxInteret = taux;
			item.MontantCapitalRembourse = RoundValue(num7);
			item.MontantMensualite = RoundValue(num);
		}
	}

	private decimal GetMonsualiteAnnuiteCanstante(decimal montantCredit, decimal taux, int nbMois)
	{
		double num = (double)taux / 12.0 / 100.0;
		double num2 = ((num != 0.0) ? ((double)montantCredit * num / (1.0 - Math.Pow(1.0 + num, -1 * nbMois))) : ((double)montantCredit / (double)nbMois));
		return (decimal)num2;
	}

	private void CalculFraisDossier(decimal montant, int nbMois, IEnumerable<LigneCredit> collection)
	{
		decimal num = RoundValue(montant / (decimal)nbMois);
		foreach (LigneCredit item in collection)
		{
			item.MontantFraisDossier = ((item.MontantCapitalRembourse == 0m) ? 0m : num);
			item.MontantMensualite += item.MontantFraisDossier;
		}
	}

	private void CalculAssurance(decimal montant, int nbMois, IEnumerable<LigneCredit> collection)
	{
		decimal num = RoundValue(montant / (decimal)nbMois);
		foreach (LigneCredit item in collection)
		{
			item.MontantAssurance = ((item.MontantCapitalRembourse == 0m) ? 0m : num);
			item.MontantMensualite += item.MontantAssurance;
		}
	}

	private decimal RoundValue(decimal value)
	{
		SocieteDevise societeDevise = Groupe.SocieteManager.Societe.GetDefaultDeviseSociete() ?? throw new ArgumentNullException("SocieteDevise");
		return Math.Round(value, societeDevise.NombreDecimales, MidpointRounding.AwayFromZero);
	}

	private string GetNumeroEcheance(string numero, DateTime dateEcheance, int rang)
	{
		return $"{numero}_{rang}/{dateEcheance:MMyy}";
	}

	private DateTime GetFirstDayMonth(DateTime date)
	{
		return new DateTime(date.Year, date.Month, 1);
	}

	private DateTime GetLastDayMonth(DateTime date)
	{
		return GetFirstDayMonth(date).AddMonths(1).AddDays(-1.0);
	}

	private DateTime GetAnnuteDate(DateTime annuteDate)
	{
		SocieteManager societeManager = Groupe.SocieteManager;
		_ = societeManager.Societe;
		if (_joursReposCollection == null)
		{
			_joursReposCollection = societeManager.JoursReposGetAll() ?? new List<JoursRepos>();
		}
		int num = 0;
		DateTime newDateAnnute = annuteDate.Date;
		while (_joursReposCollection.Any((JoursRepos x) => x.Date.Date == newDateAnnute.Date) || newDateAnnute.Date.DayOfWeek == DayOfWeek.Saturday || newDateAnnute.Date.DayOfWeek == DayOfWeek.Sunday)
		{
			newDateAnnute = annuteDate.Date.AddDays(++num);
		}
		return newDateAnnute;
	}

	private DateTime GetDateEcheance(Credit credit, DateTime dateTime)
	{
		return credit.TypeDatePrelevement switch
		{
			TypeDatePrelevelmentCredit.Variable => GetAnnuteDate(dateTime), 
			TypeDatePrelevelmentCredit.DebutMois => GetFirstDayMonth(dateTime), 
			TypeDatePrelevelmentCredit.FinMois => GetLastDayMonth(dateTime), 
			_ => throw new NotImplementedException(), 
		};
	}

	private void RegenererNumeroLigneCredit(int creditNo)
	{
		IEnumerable<LigneCredit> source = _ligneCreditRepository.GetAll(creditNo) ?? throw new ApplicationException("Impossible de charger les lignes de crédit.");
		Credit credit = _creditRepository.Get(creditNo) ?? throw new ApplicationException($"Impossible de charger le crédit [{creditNo}]");
		int num = 1;
		foreach (LigneCredit item in source.OrderBy((LigneCredit x) => x.DateEcheance))
		{
			string numeroEcheance = GetNumeroEcheance(credit.Numero, item.DateEcheance, num);
			_ligneCreditRepository.UpdateNumero(item.No, numeroEcheance);
			num++;
		}
	}

	private void ValidatorligneCredit(LigneCredit ligne, DateTime firstEcheance)
	{
		if (ligne == null)
		{
			throw new ArgumentNullException("ligne");
		}
		firstEcheance.Verifier("de la première échéance du crédit.");
		ligne.DateEcheance.Verifier("d'échéance");
		if (ligne.TauxInteret < 0m || ligne.TauxInteret > 100m)
		{
			throw new ApplicationException("Le taux d’intérêt de la ligne " + ligne.Numero + " est invalide.");
		}
		if (ligne.MontantMensualite <= 0m)
		{
			throw new ApplicationException("Le montant de monsualité de la ligne " + ligne.Numero + " est invalide.");
		}
		if (ligne.MontantCapitalRembourse < 0m)
		{
			throw new ApplicationException("Le montant de capital remboursé de la ligne " + ligne.Numero + " est invalide.");
		}
		if (ligne.MontantInteret < 0m)
		{
			throw new ApplicationException("Le montant d'interret de la ligne " + ligne.Numero + " est invalide.");
		}
		if (ligne.MontantCapitalRestantDu < 0m)
		{
			throw new ApplicationException("Le montant de capital restant dû de la ligne " + ligne.Numero + " est invalide.");
		}
		if (ligne.MontantAssurance < 0m)
		{
			throw new ApplicationException("Le montant d'assurance de la ligne " + ligne.Numero + " est invalide.");
		}
		if (ligne.MontantFraisDossier < 0m)
		{
			throw new ApplicationException("Le montant de frais dossier de la ligne " + ligne.Numero + " est invalide.");
		}
		if (ligne.IsPointer)
		{
			throw new ApplicationException("La ligne " + ligne.Numero + " de crédit est pointée.");
		}
		if (ligne.DateEcheance < firstEcheance && ligne.MontantCapitalRembourse != 0m)
		{
			throw new ApplicationException("La date d’échéance de la ligne " + ligne.Numero + " est antérieure à celle de la première échéance du crédit.");
		}
		decimal num = ligne.MontantInteret + ligne.MontantCapitalRembourse + ligne.MontantFraisDossier + ligne.MontantAssurance;
		if (ligne.MontantMensualite != num)
		{
			throw new ApplicationException("La mensualité de la ligne " + ligne.Numero + " ne correspond pas au montant attendu. ");
		}
	}
}
