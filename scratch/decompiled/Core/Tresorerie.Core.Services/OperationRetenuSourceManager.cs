using System;
using System.Collections.Generic;
using Tresorerie.Core.Models;
using Tresorerie.Dapper.Repositories;

namespace Tresorerie.Core.Services;

public class OperationRetenuSourceManager
{
	private readonly IOperationRetenuSourceRepository _operationRetenuSourceRepository;

	public GroupeService Groupe { get; set; }

	public OperationRetenuSourceManager(IOperationRetenuSourceRepository operationRetenuSourceRepository)
	{
		_operationRetenuSourceRepository = operationRetenuSourceRepository ?? throw new ArgumentNullException("operationRetenuSourceRepository");
	}

	public int Create(string code, string description, string typeOperation, string operation, int? modeNo)
	{
		if (string.IsNullOrEmpty(code))
		{
			throw new ArgumentNullException("code");
		}
		if (string.IsNullOrEmpty(description))
		{
			throw new ArgumentNullException("description");
		}
		if (string.IsNullOrEmpty(typeOperation))
		{
			throw new ArgumentNullException("typeOperation");
		}
		if (string.IsNullOrEmpty(operation))
		{
			throw new ArgumentNullException("operation");
		}
		ModeReglementManager modeReglementManager = Groupe.ModeReglementManager;
		if (modeNo.HasValue && modeReglementManager.Get(modeNo.Value) == null)
		{
			throw new ApplicationException("Impossible de charger le mode de règlement.");
		}
		if (_operationRetenuSourceRepository.Get(code) != null)
		{
			throw new InvalidOperationException("Opération retenue à la source existe déjà!");
		}
		OperationRetenuSource operationRetenuSource = new OperationRetenuSource
		{
			Code = code,
			Intitule = description,
			TypeOperation = typeOperation,
			Operation = typeOperation,
			ModeReglementNo = modeNo
		};
		int? num = _operationRetenuSourceRepository.Create(operationRetenuSource);
		if (!num.HasValue)
		{
			throw new InvalidOperationException("Insertion invalide!");
		}
		return num.Value;
	}

	public void Delete(int operationRetenuSourceNo)
	{
		if (operationRetenuSourceNo <= 0)
		{
			throw new ArgumentNullException("operationRetenuSourceNo");
		}
		OperationRetenuSource operationRetenuSource = _operationRetenuSourceRepository.Get(operationRetenuSourceNo);
		if (operationRetenuSource == null)
		{
			throw new ArgumentException("Opération retenue à la source invalide!");
		}
		if (_operationRetenuSourceRepository.IsUsed(operationRetenuSourceNo))
		{
			throw new ArgumentException("L'opération retenue à la source est utilisé!");
		}
		_operationRetenuSourceRepository.Delete(operationRetenuSource);
	}

	public OperationRetenuSource Get(string code)
	{
		if (string.IsNullOrEmpty(code))
		{
			throw new ArgumentNullException("code");
		}
		return _operationRetenuSourceRepository.Get(code);
	}

	public OperationRetenuSource Get(int no)
	{
		return _operationRetenuSourceRepository.Get(no);
	}

	public List<OperationRetenuSource> GetAll()
	{
		return _operationRetenuSourceRepository.GetAll();
	}

	public OperationRetenuSource Init()
	{
		return new OperationRetenuSource();
	}

	public bool IsOperationRetenuSourceUsed(int operationRetenuSourceNo)
	{
		if (_operationRetenuSourceRepository.Get(operationRetenuSourceNo) == null)
		{
			throw new ArgumentException("Opération retenue à la source invalide!");
		}
		return _operationRetenuSourceRepository.IsUsed(operationRetenuSourceNo);
	}

	public void Update(int no, string description, string typeOperation, string operation, int? modeNo)
	{
		ModeReglementManager modeReglementManager = Groupe.ModeReglementManager;
		if (no <= 0)
		{
			throw new ArgumentNullException("no");
		}
		if (string.IsNullOrEmpty(description))
		{
			throw new ArgumentNullException("description");
		}
		if (string.IsNullOrEmpty(typeOperation))
		{
			throw new ArgumentNullException("typeOperation");
		}
		if (string.IsNullOrEmpty(operation))
		{
			throw new ArgumentNullException("operation");
		}
		OperationRetenuSource operationRetenuSource = _operationRetenuSourceRepository.Get(no);
		if (operationRetenuSource == null)
		{
			throw new ArgumentException("Opération retenue à la source invalide!");
		}
		if (modeNo.HasValue && modeReglementManager.Get(modeNo.Value) == null)
		{
			throw new ApplicationException("Impossible de charger le mode de règlement.");
		}
		operationRetenuSource.Intitule = description;
		operationRetenuSource.TypeOperation = typeOperation;
		operationRetenuSource.Operation = operation;
		operationRetenuSource.ModeReglementNo = modeNo;
		_operationRetenuSourceRepository.Update(operationRetenuSource);
	}
}
