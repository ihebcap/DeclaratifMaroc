using System;
using System.Collections.Generic;
using Tresorerie.Core.Interfaces;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Services;

public class VerifySoldeManager
{
	private readonly IVerifyLotEspeceRepository _verifyLotEspeceRepository;

	private readonly IVerifySoldeCaisseRepository _verifySoldeCaisseRepository;

	private readonly IVerifySoldeEcheanceRepository _verifySoldeEcheanceRepository;

	private readonly IVerifySoldeReglementClientRepository _verifySoldeReglementClientRepository;

	private readonly IVerifySoldeReglementToReplaceRepository _verifySoldeReglementToReplaceRepository;

	public SocieteManager SocieteManager { get; set; }

	public VerifySoldeManager(IVerifySoldeReglementClientRepository verifySoldeReglementClientRepository, IVerifySoldeReglementToReplaceRepository verifySoldeReglementToReplaceRepository, IVerifySoldeEcheanceRepository verifySoldeEcheanceRepository, IVerifySoldeCaisseRepository verifySoldeCaisseRepository, IVerifyLotEspeceRepository verifyLotEspeceRepository)
	{
		if (verifySoldeReglementClientRepository == null)
		{
			throw new ArgumentNullException("verifySoldeReglementClientRepository");
		}
		if (verifySoldeReglementToReplaceRepository == null)
		{
			throw new ArgumentNullException("verifySoldeReglementToReplaceRepository");
		}
		if (verifySoldeEcheanceRepository == null)
		{
			throw new ArgumentNullException("verifySoldeEcheanceRepository");
		}
		if (verifySoldeCaisseRepository == null)
		{
			throw new ArgumentNullException("verifySoldeCaisseRepository");
		}
		if (verifyLotEspeceRepository == null)
		{
			throw new ArgumentNullException("verifyLotEspeceRepository");
		}
		_verifySoldeReglementClientRepository = verifySoldeReglementClientRepository;
		_verifySoldeReglementToReplaceRepository = verifySoldeReglementToReplaceRepository;
		_verifySoldeEcheanceRepository = verifySoldeEcheanceRepository;
		_verifySoldeCaisseRepository = verifySoldeCaisseRepository;
		_verifyLotEspeceRepository = verifyLotEspeceRepository;
	}

	public IEnumerable<VerifySoldeEcheance> GetAllEcheance()
	{
		return _verifySoldeEcheanceRepository.GetAll();
	}

	public IEnumerable<VerifyLotEspece> GetAllLotEspece()
	{
		return _verifyLotEspeceRepository.GetAll();
	}

	public IEnumerable<VerifySoldeReglementClient> GetAllReglementClient()
	{
		return _verifySoldeReglementClientRepository.GetAll();
	}

	public IEnumerable<VerifySoldeReglementToReplace> GetAllReglementToReplace()
	{
		return _verifySoldeReglementToReplaceRepository.GetAll();
	}

	public IEnumerable<VerifySoldeCaisse> GetAllSoldeCaisse()
	{
		return _verifySoldeCaisseRepository.GetAll();
	}

	public VerifyLotEspece GetlotEspece(int lotNo)
	{
		return _verifyLotEspeceRepository.Get(lotNo);
	}

	public VerifySoldeCaisse GetSoldeCaisse(int caisseNo)
	{
		return _verifySoldeCaisseRepository.Get(caisseNo);
	}

	public VerifySoldeEcheance GetSoldeEcheance(int echeanceNo)
	{
		return _verifySoldeEcheanceRepository.Get(echeanceNo);
	}

	public VerifySoldeReglementClient GetSoldeReglementClient(int reglementNo)
	{
		return _verifySoldeReglementClientRepository.Get(reglementNo);
	}

	public VerifySoldeReglementToReplace GetSoldeReglementToReplace(int reglementNo)
	{
		return _verifySoldeReglementToReplaceRepository.Get(reglementNo);
	}

	public void UpdateLotEspece(int lotNo)
	{
		VerifyLotEspece verifyLotEspece = _verifyLotEspeceRepository.Get(lotNo);
		if (verifyLotEspece == null)
		{
			throw new ArgumentException("Lot espece invalide!");
		}
		if (verifyLotEspece.IsValide)
		{
			throw new ArgumentException("Lot espece est valide!");
		}
		decimal num = (verifyLotEspece.MontantRestant = verifyLotEspece.MontantEntree - verifyLotEspece.TotalSortie);
		verifyLotEspece.Epuise = num == 0m;
		_verifyLotEspeceRepository.Update(verifyLotEspece);
	}

	public void UpdateReglementToReplace(int reglementNo)
	{
		VerifySoldeReglementToReplace soldeReglementToReplace = GetSoldeReglementToReplace(reglementNo);
		if (soldeReglementToReplace == null)
		{
			throw new ArgumentException("Règlement to replace invalide!");
		}
		if (soldeReglementToReplace.IsValide)
		{
			throw new ArgumentException("Le solde du règlement est valide!");
		}
		decimal soldeToReplace = soldeReglementToReplace.Montant - soldeReglementToReplace.TotalMontantRemplacement;
		soldeReglementToReplace.SoldeToReplace = soldeToReplace;
		_verifySoldeReglementToReplaceRepository.Update(soldeReglementToReplace);
	}

	public void UpdateSoldeEcheance(int echeanceNo)
	{
		VerifySoldeEcheance soldeEcheance = GetSoldeEcheance(echeanceNo);
		if (soldeEcheance == null)
		{
			throw new ArgumentException("Echeance invalide!");
		}
		if (soldeEcheance.IsValide)
		{
			throw new ArgumentException("Le solde de l'echeance est valide!");
		}
		decimal num = (soldeEcheance.Solde = soldeEcheance.Montant - soldeEcheance.TotalMontantAffectation);
		soldeEcheance.Etat = num == 0m;
		_verifySoldeEcheanceRepository.Update(soldeEcheance);
	}

	public void UpdateSoldeReglementClient(int reglementNo)
	{
		VerifySoldeReglementClient soldeReglementClient = GetSoldeReglementClient(reglementNo);
		if (soldeReglementClient == null)
		{
			throw new ArgumentException("Règlement invalide!");
		}
		if (soldeReglementClient.IsValide)
		{
			throw new ArgumentException("Le solde du reglement est valide!");
		}
		decimal solde = soldeReglementClient.Montant - soldeReglementClient.TotalMontantAffectation;
		soldeReglementClient.Solde = solde;
		soldeReglementClient.Etat = soldeReglementClient.Solde == 0m;
		_verifySoldeReglementClientRepository.Update(soldeReglementClient);
	}
}
