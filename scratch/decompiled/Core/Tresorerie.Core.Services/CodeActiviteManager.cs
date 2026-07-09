using System;
using System.Collections.Generic;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Interfaces;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Services;

public class CodeActiviteManager
{
	private readonly ICodeActiviteRepository _codeActiviteRepository;

	public GroupeService Groupe { get; set; }

	public CodeActiviteManager(ICodeActiviteRepository codeActiviteRepository)
	{
		_codeActiviteRepository = codeActiviteRepository ?? throw new ArgumentNullException("codeActiviteRepository");
	}

	public int Create(string code, string description, CodeActiviteDomaine domaine, decimal taux, decimal prorata, string famille)
	{
		if (string.IsNullOrEmpty(code))
		{
			throw new ArgumentNullException("code");
		}
		if (string.IsNullOrEmpty(description))
		{
			throw new ArgumentNullException("description");
		}
		if (taux < 0m || taux > 100m)
		{
			throw new ApplicationException("Taux invalide.");
		}
		if (prorata < 0m || prorata > 100m)
		{
			throw new ApplicationException("Prorata invalide.");
		}
		if (_codeActiviteRepository.Get(code) != null)
		{
			throw new InvalidOperationException("Code activité existe déjà!");
		}
		CodeActivite codeActivite = new CodeActivite
		{
			Code = code,
			Intitule = description,
			Domaine = domaine,
			Famille = famille,
			Prorata = prorata,
			Taux = taux
		};
		int? num = _codeActiviteRepository.Create(codeActivite);
		if (!num.HasValue)
		{
			throw new InvalidOperationException("Insertion invalide!");
		}
		return num.Value;
	}

	public void Delete(int CodeActiviteNo)
	{
		if (CodeActiviteNo <= 0)
		{
			throw new ArgumentNullException("CodeActiviteNo");
		}
		CodeActivite codeActivite = _codeActiviteRepository.Get(CodeActiviteNo);
		if (codeActivite == null)
		{
			throw new ArgumentException("Code activité invalide!");
		}
		if (_codeActiviteRepository.IsUsed(CodeActiviteNo))
		{
			throw new ArgumentException("Le code activité est utilisé dans un ou plusieur taxe!");
		}
		_codeActiviteRepository.Delete(codeActivite);
	}

	public CodeActivite Get(string code)
	{
		if (string.IsNullOrEmpty(code))
		{
			throw new ArgumentNullException("code");
		}
		return _codeActiviteRepository.Get(code);
	}

	public CodeActivite Get(int no)
	{
		return _codeActiviteRepository.Get(no);
	}

	public List<CodeActivite> GetAll()
	{
		return _codeActiviteRepository.GetAll();
	}

	public CodeActivite Init()
	{
		return new CodeActivite();
	}

	public bool IsCodeActiviteUsed(int CodeActiviteNo)
	{
		if (_codeActiviteRepository.Get(CodeActiviteNo) == null)
		{
			throw new ArgumentException("Code activité invalide!");
		}
		return _codeActiviteRepository.IsUsed(CodeActiviteNo);
	}

	public void Update(int CodeActiviteNo, string description, CodeActiviteDomaine domaine, decimal taux, decimal prorata, string famille)
	{
		if (CodeActiviteNo <= 0)
		{
			throw new ArgumentNullException("CodeActiviteNo");
		}
		if (string.IsNullOrEmpty(description))
		{
			throw new ArgumentNullException("description");
		}
		if (taux < 0m || taux > 100m)
		{
			throw new ApplicationException("Taux invalide.");
		}
		if (prorata < 0m || prorata > 100m)
		{
			throw new ApplicationException("Prorata invalide.");
		}
		CodeActivite codeActivite = _codeActiviteRepository.Get(CodeActiviteNo);
		if (codeActivite == null)
		{
			throw new ArgumentException("Code activité invalide!");
		}
		codeActivite.Intitule = description;
		codeActivite.Domaine = domaine;
		codeActivite.Taux = taux;
		codeActivite.Prorata = prorata;
		codeActivite.Famille = famille;
		_codeActiviteRepository.Update(codeActivite);
	}
}
