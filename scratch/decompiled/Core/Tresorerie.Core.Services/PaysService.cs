using System;
using System.Collections.Generic;
using System.Linq;
using Tresorerie.Core.Interfaces;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Services;

public class PaysService
{
	private readonly IPaysRepository _paysRepository;

	private readonly IBanqueTiersRepository _repoBanqueTiers;

	public PaysService(IPaysRepository paysRepository, IBanqueTiersRepository repoBanqueTiers)
	{
		_paysRepository = paysRepository ?? throw new ArgumentNullException("paysRepository");
		_repoBanqueTiers = repoBanqueTiers ?? throw new ArgumentNullException("repoBanqueTiers");
	}

	public Pays Get(int no)
	{
		if (no <= 0)
		{
			throw new ArgumentException(no.ToString());
		}
		return _paysRepository.Get(no);
	}

	public Pays Get(string code)
	{
		if (string.IsNullOrEmpty(code))
		{
			throw new ArgumentNullException(code);
		}
		return _paysRepository.Get(code);
	}

	public IList<Pays> GetAll()
	{
		return _paysRepository.GetAll();
	}

	public void Create(Pays pays)
	{
		if (pays == null)
		{
			throw new ArgumentNullException("pays");
		}
		if (string.IsNullOrEmpty(pays.Code))
		{
			throw new InvalidOperationException("Code obligatoire.");
		}
		if (string.IsNullOrEmpty(pays.Intitule))
		{
			throw new InvalidOperationException("Intitilé obligatoire.");
		}
		if (_paysRepository.Get(pays.Code) != null)
		{
			throw new ApplicationException("Le code [" + pays.Code + "] existe déjà !");
		}
		_paysRepository.Create(pays);
	}

	public void Update(Pays pays)
	{
		if (pays == null)
		{
			throw new ArgumentNullException("pays");
		}
		if (string.IsNullOrEmpty(pays.Code))
		{
			throw new InvalidOperationException("Code obligatoire.");
		}
		if (string.IsNullOrEmpty(pays.Intitule))
		{
			throw new InvalidOperationException("Intitilé obligatoire.");
		}
		_paysRepository.Update(pays);
	}

	public void Delete(int paysNo)
	{
		if (paysNo <= 0)
		{
			throw new ArgumentException("Impossible de charger le pays !");
		}
		Pays pays = Get(paysNo);
		if (pays == null)
		{
			throw new InvalidOperationException("Impossible de charger le pays !");
		}
		if (_repoBanqueTiers.GetAllByPays(paysNo).Count() > 0)
		{
			throw new InvalidOperationException("Impossible de supprimer le pays : déjà utilisé !");
		}
		_paysRepository.Delete(pays);
	}
}
