using System;
using System.Collections.Generic;
using Tresorerie.Core.Interfaces;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Services;

public class TypeCautionManager
{
	private readonly NotifyService _notifyService;

	private readonly ITypeCautionRepository _typeCautionRepository;

	public GroupeService Groupe { get; set; }

	public TypeCautionManager(ITypeCautionRepository typeCautionRepository, NotifyService notifyService)
	{
		if (typeCautionRepository == null)
		{
			throw new ArgumentNullException("typeCautionRepository");
		}
		if (notifyService == null)
		{
			throw new ArgumentNullException("notifyService");
		}
		_typeCautionRepository = typeCautionRepository;
		_notifyService = notifyService;
	}

	public void Create(TypeCaution typeCaution)
	{
		if (typeCaution == null)
		{
			throw new ArgumentNullException("typeCaution");
		}
		if (string.IsNullOrEmpty(typeCaution.Code))
		{
			throw new ApplicationException("Code est obligatoire!");
		}
		if (string.IsNullOrEmpty(typeCaution.Intitule))
		{
			throw new ApplicationException("Intitulé est obligatoire!");
		}
		if (_typeCautionRepository.Get(typeCaution.Code) != null)
		{
			throw new ApplicationException("Le type de caution [" + typeCaution.Code + "] existe!");
		}
		_typeCautionRepository.Create(typeCaution);
		Societe societe = Groupe.SocieteManager.Societe;
		_notifyService.Notify(TypeEntity.Administration, 0, TypeAction.Suppression, societe.No);
	}

	public void Delete(TypeCaution typeCaution)
	{
		if (typeCaution == null)
		{
			throw new ArgumentNullException("typeCaution");
		}
		if (typeCaution.No == 0)
		{
			throw new ArgumentNullException("numéro type caution");
		}
		if (string.IsNullOrEmpty(typeCaution.Code))
		{
			throw new ApplicationException("Code est obligatoire!");
		}
		TypeCaution typeCaution2 = _typeCautionRepository.Get(typeCaution.Code);
		if (typeCaution2 == null)
		{
			throw new ApplicationException("Impossible de charger le type de caution [" + typeCaution.Code + "]!");
		}
		if (IsUsedType(typeCaution2.No))
		{
			throw new ApplicationException("Le type de caution [" + typeCaution2.Code + "] utilisé!");
		}
		_typeCautionRepository.Delete(typeCaution);
		Societe societe = Groupe.SocieteManager.Societe;
		_notifyService.Notify(TypeEntity.Administration, 0, TypeAction.Suppression, societe.No);
	}

	public TypeCaution Get(int no)
	{
		if (no == 0)
		{
			throw new ArgumentNullException("numéro type caution");
		}
		return _typeCautionRepository.Get(no);
	}

	public TypeCaution Get(string code)
	{
		if (string.IsNullOrEmpty(code))
		{
			throw new ArgumentNullException("code");
		}
		return _typeCautionRepository.Get(code);
	}

	public IEnumerable<TypeCaution> GetAll()
	{
		return _typeCautionRepository.GetAll();
	}

	public bool IsUsedType(int typeNo)
	{
		return _typeCautionRepository.IsUsedType(typeNo);
	}

	public void Update(TypeCaution typeCaution)
	{
		if (typeCaution == null)
		{
			throw new ArgumentNullException("typeCaution");
		}
		if (typeCaution.No == 0)
		{
			throw new ArgumentNullException("numéro type caution");
		}
		if (string.IsNullOrEmpty(typeCaution.Code))
		{
			throw new ApplicationException("Code est obligatoire!");
		}
		TypeCaution typeCaution2 = _typeCautionRepository.Get(typeCaution.Code);
		if (typeCaution2 == null)
		{
			throw new ApplicationException("Impossible de charger le type de caution [" + typeCaution.Code + "]!");
		}
		typeCaution2.Intitule = typeCaution.Intitule;
		typeCaution2.CanChangeToReglement = typeCaution.CanChangeToReglement;
		_typeCautionRepository.Update(typeCaution2);
		Societe societe = Groupe.SocieteManager.Societe;
		_notifyService.Notify(TypeEntity.Administration, 0, TypeAction.Modification, societe.No);
	}
}
