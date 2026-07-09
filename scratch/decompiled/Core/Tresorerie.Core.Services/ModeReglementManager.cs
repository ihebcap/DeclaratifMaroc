using System;
using System.Collections.Generic;
using System.Linq;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Interfaces;
using Tresorerie.Core.Models;
using Tresorerie.Ressources;

namespace Tresorerie.Core.Services;

public class ModeReglementManager
{
	private readonly IModeReglementRepository _repository;

	public GroupeService Groupe { get; set; }

	public ModeReglementManager(IModeReglementRepository repository)
	{
		if (repository == null)
		{
			throw new ArgumentNullException("repository");
		}
		_repository = repository;
	}

	public int? Create(ModeReglement mode)
	{
		if (mode == null)
		{
			throw new ArgumentNullException("mode");
		}
		if (mode.No != 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorModeNo);
		}
		if (!Groupe.SocieteManager.Utilisateur.IsAdmin)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorCreateMode);
		}
		if (Get(mode.Code) != null)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorModeReglementExist);
		}
		if (mode.IsModeAvoir && mode.Type != ReglementType.Autre)
		{
			throw new ApplicationException("Le mode [" + mode.Code + "] est un mode avoir, doit être de type autre.");
		}
		if (mode.IsModeAvoir && (mode.IsRetenu || mode.IsTransferable))
		{
			throw new ApplicationException("Opération invalide. Le mode [" + mode.Code + "] ne peut être ni de type retenue ni de transferable.");
		}
		if (mode.Type == ReglementType.Autre && mode.IsRetenu && Groupe.AnnexeManager.Get(mode.AnnexeType.GetValueOrDefault()) == null)
		{
			throw new InvalidOperationException("Annexe invalide!");
		}
		return _repository.Create(mode);
	}

	public void Delete(int modeNo)
	{
		if (modeNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorModeNo);
		}
		SocieteManager societeManager = Groupe.SocieteManager;
		ModeReglement modeReglement = Get(modeNo);
		if (modeReglement == null)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorModeNo);
		}
		if (!societeManager.Utilisateur.IsAdmin)
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorDeleteMode, modeReglement.Code));
		}
		List<Societe> source = societeManager.GetAll().ToList();
		if (source.SelectMany((Societe x) => x.GetCaisses()).Any((Caisse c) => c.HasModeReglement(modeNo)))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorModeAffecteCaisse, modeReglement.Code));
		}
		if (Groupe.BordereauxTypeManager.GetAll().Any((TypeBordereau t) => t.HasModeReglement(modeNo)))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorModeAffecteBordereau, modeReglement.Code));
		}
		if (source.SelectMany((Societe s) => s.GetAllModes()).Any((SocieteModeReglement m) => m.No == modeNo))
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorModeCorrespondance, modeReglement.Code));
		}
		if (_repository.IsModeUsedByRegleRetenu(modeNo))
		{
			throw new ApplicationException("Le mode [" + modeReglement.Code + "] est associé à une ou plusieurs règles de retenue à la source.");
		}
		if (_repository.IsModeUsedByTypeOperationRetenue(modeNo))
		{
			throw new ApplicationException("Le mode [" + modeReglement.Code + "] est associé à un ou plusieurs opérations de retenue à la source.");
		}
		_repository.Delete(modeReglement);
	}

	public ModeReglement Get(int no)
	{
		return _repository.Get(no);
	}

	public ModeReglement Get(string code)
	{
		if (string.IsNullOrEmpty(code))
		{
			throw new ArgumentNullException("code");
		}
		return _repository.Get(code);
	}

	public IEnumerable<ModeReglement> GetAll()
	{
		return _repository.GetAll();
	}

	public ModeReglement InitNew(string code)
	{
		if (string.IsNullOrEmpty(code))
		{
			throw new ArgumentNullException("code");
		}
		return new ModeReglement
		{
			Code = code
		};
	}

	public bool IsModeUsed(int modeNo)
	{
		if (_repository.Get(modeNo) == null)
		{
			throw new ArgumentNullException("modeNo");
		}
		Societe societe = Groupe.SocieteManager.Societe;
		return _repository.IsModeUsed(modeNo, societe.No);
	}

	public void Update(ModeReglement modeReglement)
	{
		if (modeReglement == null)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorModeReglementInvalide);
		}
		if (modeReglement.No == 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorModeReglementInvalide);
		}
		ModeReglement modeReglement2 = Get(modeReglement.No);
		if (modeReglement2 == null)
		{
			throw new ArgumentNullException("mode");
		}
		if (!Groupe.SocieteManager.Utilisateur.IsAdmin)
		{
			throw new ApplicationException(string.Format(TresorerieCoreMessages.ErrorUpdateMode, modeReglement2.Code));
		}
		if (modeReglement.IsModeAvoir && modeReglement.Type != ReglementType.Autre)
		{
			throw new ApplicationException("Le mode [" + modeReglement.Code + "] est un mode avoir, doit être de type autre.");
		}
		if (modeReglement.IsModeAvoir && (modeReglement.IsRetenu || modeReglement.IsTransferable))
		{
			throw new ApplicationException("Opération invalide. Le mode [" + modeReglement.Code + "] ne peut être ni de type retenue ni de transferable.");
		}
		if (modeReglement2.IsModeAvoir && modeReglement.IsModeAvoir != modeReglement2.IsModeAvoir && IsModeUsed(modeReglement2.No))
		{
			throw new ApplicationException("Opération invalide. Le mode [" + modeReglement.Code + "] est associé à un règlement.");
		}
		_repository.Update(modeReglement);
	}
}
