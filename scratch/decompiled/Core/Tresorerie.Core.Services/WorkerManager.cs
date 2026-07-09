using System;
using System.Collections.Generic;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Interfaces;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Services;

public class WorkerManager
{
	private readonly IWorkerRepository _workerRepository;

	public SocieteManager SocieteManager { get; set; }

	public WorkerManager(IWorkerRepository workerRepository)
	{
		_workerRepository = workerRepository;
	}

	public void AddWorkerReglementToPile(int reglementNo, ErpDomaine domaine, Societe societe = null)
	{
		int societeNo = societe?.No ?? SocieteManager.Societe.No;
		TypeEntity typeEntity = ((domaine == ErpDomaine.Vente) ? TypeEntity.Reglement : TypeEntity.ReglementFournisseur);
		if (!_workerRepository.Existe(societeNo, domaine, typeEntity, reglementNo))
		{
			CaisseManager caisseManager = SocieteManager.CaisseManager;
			bool flag = false;
			string text = string.Empty;
			switch (domaine)
			{
			case ErpDomaine.Vente:
			{
				ReglementClient obj2 = caisseManager.ReglementGet(reglementNo) ?? throw new ApplicationException($"Impossible de charger le règlement client [{reglementNo}]");
				flag = obj2.IsSynchroniser;
				text = obj2.Numero;
				break;
			}
			case ErpDomaine.Achat:
			{
				ReglementFournisseur obj = caisseManager.ReglementFournisseurGet(reglementNo) ?? throw new ApplicationException($"Impossible de charger le règlement fournisseur [{reglementNo}]");
				flag = obj.IsSynchroniser;
				text = obj.Numero;
				break;
			}
			}
			if (flag)
			{
				throw new ApplicationException("Le règlement [" + text + "] est Synchronisé");
			}
			_workerRepository.Create(new Worker
			{
				SocieteNo = societeNo,
				Domaine = domaine,
				TypeEntity = typeEntity,
				EntityNo = reglementNo,
				EntityNumero = text,
				UtilisateurCreateNo = SocieteManager.Utilisateur.No
			});
		}
	}

	public void AddWorkerDocumentToPile(string documentNumero, ErpDocumentType documentType, ErpDomaine domaine, Societe societe = null)
	{
		int societeNo = societe?.No ?? SocieteManager.Societe.No;
		TypeEntity typeEntity = TypeEntity.Echeance;
		if (!_workerRepository.Existe(societeNo, domaine, typeEntity, documentNumero, documentType))
		{
			_workerRepository.Create(new Worker
			{
				SocieteNo = societeNo,
				Domaine = domaine,
				TypeEntity = typeEntity,
				EntityNumero = documentNumero,
				DocumentType = documentType,
				UtilisateurCreateNo = SocieteManager.Utilisateur.No
			});
		}
	}

	public void AddWorkerFGRToPile(int entityNo, string entityNumero, ErpDomaine domaine, Societe societe = null)
	{
		int societeNo = societe?.No ?? SocieteManager.Societe.No;
		TypeEntity typeEntity = TypeEntity.EcritureTresorerie;
		if (!_workerRepository.Existe(societeNo, domaine, typeEntity, entityNo, entityNumero))
		{
			_workerRepository.Create(new Worker
			{
				SocieteNo = societeNo,
				Domaine = domaine,
				TypeEntity = typeEntity,
				EntityNo = entityNo,
				EntityNumero = entityNumero,
				UtilisateurCreateNo = SocieteManager.Utilisateur.No
			});
		}
	}

	public void AddWorkerAffectationReglementToPile(int entityNo, Societe societe = null)
	{
		int societeNo = societe?.No ?? SocieteManager.Societe.No;
		TypeEntity typeEntity = TypeEntity.Affectation;
		if (!_workerRepository.Existe(societeNo, ErpDomaine.Vente, typeEntity, entityNo))
		{
			_workerRepository.Create(new Worker
			{
				SocieteNo = societeNo,
				Domaine = ErpDomaine.Vente,
				TypeEntity = typeEntity,
				EntityNo = entityNo,
				UtilisateurCreateNo = SocieteManager.Utilisateur.No,
				EntityNumero = ""
			});
		}
	}

	public Worker GetLastWorkerReglementFromPile(ErpDomaine domaine, Societe societe = null)
	{
		int societeNo = societe?.No ?? SocieteManager.Societe.No;
		TypeEntity typeEntity = ((domaine == ErpDomaine.Vente) ? TypeEntity.Reglement : TypeEntity.ReglementFournisseur);
		return _workerRepository.GetFirst(societeNo, typeEntity, domaine);
	}

	public Worker GetLastWorkerFGRFromPile(ErpDomaine domaine, Societe societe = null)
	{
		int societeNo = societe?.No ?? SocieteManager.Societe.No;
		TypeEntity typeEntity = TypeEntity.EcritureTresorerie;
		return _workerRepository.GetFirst(societeNo, typeEntity, domaine);
	}

	public Worker GetLastWorkerAffectationFromPile(Societe societe = null)
	{
		int societeNo = societe?.No ?? SocieteManager.Societe.No;
		TypeEntity typeEntity = TypeEntity.Affectation;
		return _workerRepository.GetFirst(societeNo, typeEntity, ErpDomaine.Vente);
	}

	public Worker GetWorkerFromPile(int workerNo)
	{
		return _workerRepository.Get(workerNo);
	}

	public IEnumerable<Worker> GetAllWorkerNonLocked(TypeEntity typeEntity, ErpDomaine domaine, Societe societe = null)
	{
		int societeNo = societe?.No ?? SocieteManager.Societe.No;
		return _workerRepository.GetALLNonLocked(typeEntity, domaine, societeNo);
	}

	public void DeleteWorkerFromPile(int workerNo)
	{
		_workerRepository.Delete(workerNo);
	}

	public void DeleteAllWorkerLockedByMe(TypeEntity typeEntity, ErpDomaine domaine, Societe societe = null)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		Societe societe2 = societe ?? SocieteManager.Societe;
		if (societe2 != null)
		{
			_workerRepository.DeleteAllLockedByUser(societe2.No, utilisateur.No, typeEntity, domaine);
		}
	}

	public void DeleteAllOldWorkerLocked(int frequence, TypeEntity typeEntity, ErpDomaine domaine, Societe societe = null)
	{
		if (frequence <= 0)
		{
			throw new ApplicationException($"Frequence worker invalide [{frequence}]");
		}
		int societeNo = societe?.No ?? SocieteManager.Societe.No;
		_workerRepository.DeleteAllLocked(societeNo, frequence, typeEntity, domaine);
	}

	public void LockWorker(int workerNo)
	{
		Utilisateur utilisateur = SocieteManager.Utilisateur;
		if (utilisateur != null)
		{
			_workerRepository.SetLoked(workerNo, utilisateur.No);
		}
	}

	public Worker GetLastWorkerDocumentFromPile(ErpDomaine domaine, Societe societe = null)
	{
		int societeNo = societe?.No ?? SocieteManager.Societe.No;
		TypeEntity typeEntity = TypeEntity.Echeance;
		return _workerRepository.GetFirst(societeNo, typeEntity, domaine);
	}
}
