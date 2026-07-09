using System;
using System.Collections.Generic;
using System.Transactions;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Interfaces;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Services;

public class AnnexeManager
{
	private readonly IAnnexeRepository _annexeRepository;

	public GroupeService Groupe { get; set; }

	public AnnexeManager(IAnnexeRepository annexeRepository)
	{
		if (annexeRepository == null)
		{
			throw new ArgumentNullException("annexeRepository");
		}
		_annexeRepository = annexeRepository;
	}

	public int Create(AnnexeType type, string code, string description)
	{
		if (string.IsNullOrEmpty(code))
		{
			throw new ArgumentNullException("code");
		}
		if (string.IsNullOrEmpty(description))
		{
			throw new ArgumentNullException("description");
		}
		if (_annexeRepository.Get(code) != null)
		{
			throw new InvalidOperationException("Annexe existe déjà!");
		}
		Annexe annexe = new Annexe
		{
			Code = code,
			Intitule = description,
			AnnexeType = type
		};
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		int? num = _annexeRepository.Create(annexe);
		if (!num.HasValue)
		{
			throw new InvalidOperationException("Insertion invalide!");
		}
		transactionScope.Complete();
		return num.Value;
	}

	public void Delete(int annexeNo)
	{
		if (annexeNo <= 0)
		{
			throw new ArgumentNullException("annexeNo");
		}
		Annexe annexe = _annexeRepository.Get(annexeNo);
		if (annexe == null)
		{
			throw new ArgumentException("Annexe invalide!");
		}
		if (_annexeRepository.IsAnnexeUsed(annexeNo))
		{
			throw new ArgumentException("L'annexe est utilisé dans un ou plusieur mode de règlement!");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_annexeRepository.Delete(annexe);
		transactionScope.Complete();
	}

	public Annexe Get(int no)
	{
		if (no <= 0)
		{
			throw new ArgumentNullException("no");
		}
		return _annexeRepository.Get(no);
	}

	public Annexe Get(string code)
	{
		if (string.IsNullOrEmpty(code))
		{
			throw new ArgumentNullException("code");
		}
		return _annexeRepository.Get(code);
	}

	public IEnumerable<Annexe> GetAll()
	{
		return _annexeRepository.GetAll();
	}

	public Annexe Init()
	{
		return new Annexe();
	}

	public bool IsAnnexeUsed(int annexeNo)
	{
		if (_annexeRepository.Get(annexeNo) == null)
		{
			throw new ArgumentException("Annexe invalide!");
		}
		return _annexeRepository.IsAnnexeUsed(annexeNo);
	}

	public void Update(int annexeNo, string description)
	{
		if (annexeNo <= 0)
		{
			throw new ArgumentNullException("annexeNo");
		}
		if (string.IsNullOrEmpty(description))
		{
			throw new ArgumentNullException("description");
		}
		Annexe annexe = _annexeRepository.Get(annexeNo);
		if (annexe == null)
		{
			throw new ArgumentException("Annexe invalide!");
		}
		annexe.Intitule = description;
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_annexeRepository.Update(annexe);
		transactionScope.Complete();
	}
}
