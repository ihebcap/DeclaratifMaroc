using System;
using System.Collections.Generic;
using System.Linq;
using Tresorerie.Core.Interfaces;
using Tresorerie.Core.Models;
using Tresorerie.Ressources;

namespace Tresorerie.Core.Services;

public class TypeBordereauxManager
{
	private readonly NotifyService _notifyService;

	private readonly ITypeBordereauRepository _repository;

	public GroupeService Groupe { get; set; }

	public TypeBordereauxManager(ITypeBordereauRepository repository, NotifyService notifyService)
	{
		_repository = repository ?? throw new ArgumentNullException("repository");
		_notifyService = notifyService ?? throw new ArgumentNullException("notifyService");
	}

	public void AddModeReglement(TypeBordereau typeBord, ModeReglement modeReglement)
	{
		if (typeBord == null)
		{
			throw new ArgumentNullException("typeBord");
		}
		if (modeReglement == null)
		{
			throw new ArgumentNullException("modeReglement");
		}
		TypeBordereau typeBordereau = Get(typeBord.No);
		if (typeBordereau == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorTypeBordereauInvalide);
		}
		if (!Groupe.SocieteManager.Utilisateur.IsAdmin)
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorUpdateTypeBordereau, typeBordereau.Code));
		}
		ModeReglement obj = Groupe.ModeReglementManager.Get(modeReglement.No) ?? throw new ApplicationException(TresorerieCoreMessages.ErrorModeReglementInvalide);
		if (obj.EnSommeil)
		{
			throw new ApplicationException("Opération invalide! Le mode est en sommeil!");
		}
		if (obj.IsModeAvoir)
		{
			throw new ApplicationException("Opération invalide. mode avoir.");
		}
		if (typeBord.GetModeReglements().Any())
		{
			ModeReglement modeReglement2 = typeBord.GetModeReglements().First();
			if (modeReglement2.Type != modeReglement.Type)
			{
				throw new InvalidOperationException(string.Format(TresorerieCoreMessages.InfoTypeBordereau, modeReglement2.Type));
			}
		}
		if (typeBord.GetModeReglements().Count((ModeReglement x) => x.No == modeReglement.No) != 0)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorModeReglementAffectation);
		}
		_repository.AddModeReglement(typeBord, modeReglement);
	}

	public void Create(TypeBordereau typeBord)
	{
		if (typeBord == null)
		{
			throw new ArgumentNullException("typeBord");
		}
		if (!Groupe.SocieteManager.Utilisateur.IsAdmin)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorAddTypeBordereau);
		}
		if (Get(typeBord.Code) != null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorTypeBordereauExist);
		}
		_repository.Create(typeBord);
	}

	public void Delete(TypeBordereau type)
	{
		if (type == null)
		{
			throw new ArgumentNullException("type");
		}
		if (Get(type.No) == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorTypeBordereauInvalide);
		}
		if (!Groupe.SocieteManager.Utilisateur.IsAdmin)
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorDeleteTypeBordereau, type.Code));
		}
		if (IsTypeBordUsed(type.No))
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorTypeBordereauMouvementee);
		}
		_repository.Delete(type);
		Societe societe = Groupe.SocieteManager.Societe;
		_notifyService.Notify(TypeEntity.Parametrage, 0, TypeAction.Modification, societe.No);
	}

	public void DeleteModeReglement(TypeBordereau typeBord, ModeReglement modeReglement)
	{
		if (typeBord == null)
		{
			throw new ArgumentNullException("typeBord");
		}
		if (modeReglement == null)
		{
			throw new ArgumentNullException("modeReglement");
		}
		TypeBordereau typeBordereau = Get(typeBord.No);
		if (typeBordereau == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorTypeBordereauInvalide);
		}
		if (!Groupe.SocieteManager.Utilisateur.IsAdmin)
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorUpdateTypeBordereau, typeBordereau.Code));
		}
		if (Groupe.ModeReglementManager.Get(modeReglement.No) == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorTypeBordereauInvalide);
		}
		if (IsTypeBordUsed(typeBordereau.No))
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorTypeBordereauMouvementee);
		}
		_repository.DeleteModeReglement(typeBord, modeReglement);
	}

	public TypeBordereau Get(int no)
	{
		return _repository.Get(no);
	}

	public TypeBordereau Get(string code)
	{
		if (string.IsNullOrEmpty(code))
		{
			throw new ArgumentNullException("code");
		}
		return _repository.Get(code);
	}

	public IEnumerable<TypeBordereau> GetAll()
	{
		return _repository.GetAll();
	}

	public TypeBordereau InitNew(string code)
	{
		if (string.IsNullOrEmpty(code))
		{
			throw new ArgumentNullException("code");
		}
		return new TypeBordereau
		{
			Code = code
		};
	}

	public bool IsTypeBordUsed(int typeNo)
	{
		if (typeNo <= 0)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorTypeBordereauInvalide);
		}
		return _repository.IsTypeBordUsed(typeNo);
	}

	public bool IsTypeConfigured(int typeNo)
	{
		if (typeNo <= 0)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorTypeBordereauInvalide);
		}
		return _repository.IsTypeConfigured(typeNo);
	}

	public void Update(TypeBordereau typeBord)
	{
		if (typeBord == null)
		{
			throw new ArgumentNullException("typeBord");
		}
		TypeBordereau typeBordereau = Get(typeBord.No);
		if (typeBordereau == null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorTypeBordereauInvalide);
		}
		if (!Groupe.SocieteManager.Utilisateur.IsAdmin)
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorUpdateTypeBordereau, typeBordereau.Code));
		}
		_repository.Update(typeBord);
	}
}
