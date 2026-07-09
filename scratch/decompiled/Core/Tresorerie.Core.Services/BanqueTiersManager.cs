using System;
using System.Collections.Generic;
using System.Linq;
using System.Transactions;
using Tresorerie.Core.Interfaces;
using Tresorerie.Core.Models;
using Tresorerie.Infrastructure.Helpers;

namespace Tresorerie.Core.Services;

public class BanqueTiersManager
{
	private readonly IBanqueTiersRepository _repoBanqueTiers;

	private readonly IValidator<BanqueTiers> _banqueTiersValidator;

	private readonly ISocieteRepository _societeRepository;

	private readonly IPaysRepository _paysRepository;

	private readonly NotifyService _notifyService;

	public GroupeService Groupe { get; set; }

	public BanqueTiersManager(IBanqueTiersRepository repoBanqueTiers, IValidator<BanqueTiers> banqueTiersValidator, ISocieteRepository societeRepository, IPaysRepository paysRepository, NotifyService notifyService)
	{
		_repoBanqueTiers = repoBanqueTiers ?? throw new ArgumentNullException("repoBanqueTiers");
		_banqueTiersValidator = banqueTiersValidator ?? throw new ArgumentNullException("banqueTiersValidator");
		_societeRepository = societeRepository ?? throw new ArgumentNullException("societeRepository");
		_paysRepository = paysRepository ?? throw new ArgumentNullException("paysRepository");
		_notifyService = notifyService ?? throw new ArgumentNullException("notifyService");
	}

	public int Create(BanqueTiers banqueTiers)
	{
		if (banqueTiers == null)
		{
			throw new ArgumentNullException("banqueTiers");
		}
		if (!_banqueTiersValidator.Validate(banqueTiers))
		{
			throw new InvalidOperationException("Banque tier invalide.");
		}
		if (_repoBanqueTiers.Get(banqueTiers.Banque, banqueTiers.TiersNum, banqueTiers.SocieteNo) != null)
		{
			throw new ApplicationException("La banque [" + banqueTiers.Banque + "] du tier [" + banqueTiers.TiersNum + "] existe déjà !");
		}
		if ((_societeRepository.Get(banqueTiers.SocieteNo) ?? throw new ApplicationException($"Impossible de charger la société [{banqueTiers.SocieteNo}]")).Rib)
		{
			Pays pays = _paysRepository.Get(banqueTiers.PaysNo);
			if (pays == null)
			{
				throw new ApplicationException($"Impossible de charger le pays [{banqueTiers.PaysNo}]");
			}
			if (pays.Rib == 0)
			{
				throw new ApplicationException("Veuillez configurer le nombre de caractères du RIB [" + pays.Intitule + "]");
			}
			if (pays.Rib != banqueTiers.RIB.Count())
			{
				throw new ApplicationException($"RIB invalide: [{pays.Rib}] caractères");
			}
		}
		banqueTiers.SetDefaultStringEmpty();
		int num = _repoBanqueTiers.Create(banqueTiers);
		_notifyService.Notify(TypeEntity.BanqueTiers, num, TypeAction.Ajout, banqueTiers.SocieteNo);
		return num;
	}

	public void Delete(int no)
	{
		if (no < 0)
		{
			throw new ArgumentNullException("no");
		}
		BanqueTiers banqueTiers = _repoBanqueTiers.Get(no);
		if (banqueTiers == null)
		{
			throw new ApplicationException($"Impossible de charger la banque [{no}]!");
		}
		if (_repoBanqueTiers.ExistMouvement(banqueTiers))
		{
			throw new ApplicationException("Banque tier [" + banqueTiers.Banque + "] est déjà utilisée !");
		}
		_repoBanqueTiers.Delete(no);
		_notifyService.Notify(TypeEntity.BanqueTiers, no, TypeAction.Suppression, banqueTiers.SocieteNo);
	}

	public BanqueTiers Get(int no)
	{
		if (no < 0)
		{
			throw new ArgumentNullException("no");
		}
		return _repoBanqueTiers.Get(no);
	}

	public IEnumerable<BanqueTiers> GetAll(string tiersNum, int societeNo)
	{
		if (string.IsNullOrEmpty(tiersNum))
		{
			throw new ArgumentNullException("tiersNum");
		}
		if (societeNo < 0)
		{
			throw new ArgumentNullException("societeNo");
		}
		return _repoBanqueTiers.GetAll(tiersNum, societeNo);
	}

	public IEnumerable<BanqueTiers> GetAllBanqueTiers(int societeNo)
	{
		if (societeNo < 0)
		{
			throw new ArgumentNullException("societeNo");
		}
		return _repoBanqueTiers.GetAll(societeNo);
	}

	public BanqueTiers Get(string banque, string tiersNum, int societeNo)
	{
		if (string.IsNullOrEmpty(banque))
		{
			throw new ArgumentNullException("banque");
		}
		if (string.IsNullOrEmpty(tiersNum))
		{
			throw new ArgumentNullException("tiersNum");
		}
		if (societeNo < 0)
		{
			throw new ArgumentNullException("societeNo");
		}
		return _repoBanqueTiers.Get(banque, tiersNum, societeNo);
	}

	public void Update(BanqueTiers banqueTiers)
	{
		if (banqueTiers == null)
		{
			throw new ArgumentNullException("banqueTiers");
		}
		if (!_banqueTiersValidator.Validate(banqueTiers))
		{
			throw new InvalidOperationException("Banque tier invalide.");
		}
		BanqueTiers banqueTiers2 = _repoBanqueTiers.Get(banqueTiers.Banque, banqueTiers.TiersNum, banqueTiers.SocieteNo);
		if (banqueTiers2 == null)
		{
			throw new ApplicationException("La banque [" + banqueTiers.Banque + "] du tier [" + banqueTiers.TiersNum + "] n'existe pas !");
		}
		if ((_societeRepository.Get(banqueTiers.SocieteNo) ?? throw new ApplicationException($"Impossible de charger la société [{banqueTiers.SocieteNo}]")).Rib)
		{
			Pays pays = _paysRepository.Get(banqueTiers.PaysNo);
			if (pays == null)
			{
				throw new ApplicationException($"Impossible de charger le pays [{banqueTiers.PaysNo}]");
			}
			if (pays.Rib == 0)
			{
				throw new ApplicationException("Veuillez configurer le nombre de caractères du RIB [" + pays.Intitule + "]");
			}
			if (pays.Rib != banqueTiers.RIB.Count())
			{
				throw new ApplicationException($"RIB invalide: [{pays.Rib}] caractères");
			}
		}
		banqueTiers2.Adresse = banqueTiers.Adresse;
		banqueTiers2.IBAN = banqueTiers.IBAN;
		banqueTiers2.BIC = banqueTiers.BIC;
		banqueTiers2.NomAgence = banqueTiers.NomAgence;
		banqueTiers2.RIB = banqueTiers.RIB;
		banqueTiers2.Pays = banqueTiers.Pays;
		banqueTiers2.Ville = banqueTiers.Ville;
		banqueTiers2.CodePostal = banqueTiers.CodePostal;
		banqueTiers2.Telephone = banqueTiers.Telephone;
		banqueTiers2.Telecopie = banqueTiers.Telecopie;
		banqueTiers2.Contact = banqueTiers.Contact;
		banqueTiers2.IsPrincipal = banqueTiers.IsPrincipal;
		banqueTiers2.PaysNo = banqueTiers.PaysNo;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_repoBanqueTiers.Update(banqueTiers2);
		if (banqueTiers2.IsPrincipal)
		{
			_repoBanqueTiers.UpdateIsPrincipal(banqueTiers2);
		}
		_notifyService.Notify(TypeEntity.BanqueTiers, banqueTiers2.No, TypeAction.Modification, banqueTiers2.SocieteNo);
		transactionScope.Complete();
	}

	public IList<Pays> GetAllPays()
	{
		return (from p in _paysRepository.GetAll()
			select new Pays
			{
				Id = p.Id,
				Code = p.Code,
				CodeIso = p.CodeIso,
				Intitule = p.Intitule,
				Rib = p.Rib
			}).ToList();
	}

	public Pays Getpays(int paysNo)
	{
		if (paysNo < 0)
		{
			throw new ArgumentNullException("paysNo");
		}
		return _paysRepository.Get(paysNo);
	}

	public Societe GetCurrentSociete(int societeNo)
	{
		return _societeRepository.Get(societeNo) ?? throw new ApplicationException("Impossible de charger la société courante !");
	}
}
