using System;
using System.Collections.Generic;
using System.Linq;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Interfaces;
using Tresorerie.Core.Models;
using Tresorerie.Ressources;

namespace Tresorerie.Core.Services;

public class HistoriqueMvtManager
{
	private readonly IHistoriqueMvtRepository _repository;

	public SocieteManager SocieteManager { get; set; }

	public HistoriqueMvtManager(IHistoriqueMvtRepository historiqueRepository)
	{
		if (historiqueRepository == null)
		{
			throw new ArgumentNullException("historiqueRepository");
		}
		_repository = historiqueRepository;
	}

	public void Create(HistoriqueMvt historiqueMvt)
	{
		if (historiqueMvt == null)
		{
			throw new ArgumentNullException("historiqueMvt");
		}
		_repository.Create(historiqueMvt);
	}

	public void Delete(HistoriqueMvt historiqueMvt)
	{
		if (historiqueMvt == null)
		{
			throw new ArgumentNullException("historiqueMvt");
		}
		_repository.Delete(historiqueMvt);
	}

	public HistoriqueMvt Get(int no)
	{
		if (no < 0)
		{
			throw new ArgumentException("no");
		}
		return _repository.Get(no);
	}

	public IEnumerable<HistoriqueMvt> GetAll(int caisseNo, int deviseNo, Societe societe = null)
	{
		if (caisseNo < 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		societe = societe ?? SocieteManager.Societe;
		if (societe.GetCaisse(caisseNo) == null)
		{
			throw new ArgumentNullException("Caisse invalide!");
		}
		return _repository.GetAllByCaisse(caisseNo, deviseNo);
	}

	public IEnumerable<HistoriqueMvt> GetAll(int[] caissesNo, int deviseNo)
	{
		if (caissesNo == null)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		return _repository.GetAllByCaisse(caissesNo, deviseNo);
	}

	public IEnumerable<HistoriqueMvt> GetAllByMouvementNo(int mouvementNo)
	{
		if (mouvementNo < 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorMvtNo);
		}
		if (SocieteManager.CaisseManager.ReglementGet(mouvementNo) == null)
		{
			throw new ArgumentNullException("Mouvement invalide!");
		}
		return _repository.GetAllByMouvement(mouvementNo);
	}

	public IEnumerable<HistoriqueMvt> GetAllLigneBordereauEspece(int bordereauNo)
	{
		if (bordereauNo < 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorBordereauNo);
		}
		return from x in _repository.GetAllByMouvement(bordereauNo)
			where x.Sens == SensMouvement.Sortie
			select x;
	}

	public IEnumerable<HistoriqueMvt> GetAllLignesTransfert(int transfertNo)
	{
		if (transfertNo < 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorMvtNo);
		}
		if (SocieteManager.TransfertManager.Get(transfertNo) == null)
		{
			throw new ApplicationException("Transfert invalide!");
		}
		return _repository.GetAllLignesTransfert(transfertNo);
	}

	public IEnumerable<HistoriqueMvt> GetAllNonEpuise(int caisseNo, int modeNo, int deviseNo, Societe societe = null)
	{
		if (caisseNo < 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		societe = societe ?? SocieteManager.Societe;
		if (!(societe.GetCaisse(caisseNo) ?? throw new InvalidOperationException(TresorerieCoreMessages.ErrorCaisseNo)).HasModeReglement(modeNo))
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorModeReglementInvalide);
		}
		return _repository.GetAllNonEpuise(caisseNo, modeNo, deviseNo);
	}

	public IEnumerable<HistoriqueMvt> GetAllNonEpuise(int[] caissesNo, int[] modesNo, int deviseNo)
	{
		if (caissesNo == null)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (caissesNo.Length == 0)
		{
			throw new ArgumentNullException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		return _repository.GetAllNonEpuise(caissesNo, modesNo, deviseNo);
	}

	public decimal GetSolde(int caisseNo, int modeNo, int deviseNo, Societe societe = null)
	{
		societe = societe ?? SocieteManager.Societe;
		return GetAllNonEpuise(caisseNo, modeNo, deviseNo, societe).Sum((HistoriqueMvt x) => x.Lot.MontantRestant);
	}

	public decimal GetSolde(int caisseNo, ReglementType typeReglement, int deviseNo)
	{
		if (caisseNo < 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		IEnumerable<CaisseModeReglement> enumerable = from x in (SocieteManager.Societe.GetCaisse(caisseNo) ?? throw new ArgumentNullException("Caisse invalide!")).GetAllModes()
			where x.Type == typeReglement
			select x;
		decimal result = default(decimal);
		foreach (CaisseModeReglement item in enumerable)
		{
			result += GetAllNonEpuise(caisseNo, item.No, deviseNo).Sum((HistoriqueMvt x) => x.Lot.MontantRestant);
		}
		return result;
	}

	public void Update(HistoriqueMvt historiqueMvt)
	{
		if (historiqueMvt == null)
		{
			throw new ArgumentNullException("historiqueMvt");
		}
		_repository.Update(historiqueMvt);
	}
}
