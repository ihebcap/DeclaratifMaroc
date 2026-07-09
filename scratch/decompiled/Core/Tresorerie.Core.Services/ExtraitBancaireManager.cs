using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Interfaces;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Services;

public class ExtraitBancaireManager
{
	private readonly IExtraitBancaireRepository _extraitBancaireRepository;

	private readonly IInformationsBanqueRepository _informationsBanqueRepository;

	public GroupeService Groupe { get; set; }

	public ExtraitBancaireManager(IExtraitBancaireRepository extraitBancaireRepository, IInformationsBanqueRepository informationsBanqueRepository)
	{
		_extraitBancaireRepository = extraitBancaireRepository ?? throw new ArgumentNullException("extraitBancaireRepository");
		_informationsBanqueRepository = informationsBanqueRepository ?? throw new ArgumentNullException("informationsBanqueRepository");
	}

	public async Task<IList<ExtraitBancaire>> GetAllAsync(int societeNo)
	{
		return (await _extraitBancaireRepository.GetAllAsync(societeNo).ConfigureAwait(continueOnCapturedContext: false)).ToList();
	}

	public async Task<IList<ExtraitBancaire>> GetAllByBanqueAsync(int informationsBanqueNo)
	{
		return (await _extraitBancaireRepository.GetAllByBanqueAsync(informationsBanqueNo).ConfigureAwait(continueOnCapturedContext: false)).ToList();
	}

	public Task<ExtraitBancaire> GetAsync(int no)
	{
		return _extraitBancaireRepository.GetAsync(no);
	}

	public async Task<int> CreateAsync(string reference, int informationsBanqueNo, DateTime ancienneDate, DateTime nouvelleDate, decimal ancienSolde, decimal nouveauSolde, Societe societe = null)
	{
		if (string.IsNullOrEmpty(reference))
		{
			throw new ArgumentNullException("reference");
		}
		SocieteManager societeManager = Groupe.SocieteManager;
		societe = societe ?? societeManager.Societe;
		Utilisateur utilisateur = societeManager.Utilisateur;
		if (ancienneDate > nouvelleDate)
		{
			throw new ApplicationException("Nouvelle date invalide!");
		}
		if (_informationsBanqueRepository.Get(informationsBanqueNo) == null)
		{
			throw new ApplicationException($"Impossible de charger la banque [{informationsBanqueNo}].");
		}
		if (await _extraitBancaireRepository.GetAsync(informationsBanqueNo, reference).ConfigureAwait(continueOnCapturedContext: false) != null)
		{
			throw new ApplicationException("Il existe déjà un extrait [" + reference + "].");
		}
		ExtraitBancaire extraitBancaire = new ExtraitBancaire
		{
			AncienneDate = ancienneDate,
			AncienSolde = ancienSolde,
			DateCreation = DateTime.Now,
			Etat = ExtraitBancaireEtat.NonRapproche,
			InformationsBanqueNo = informationsBanqueNo,
			NouveauSolde = nouveauSolde,
			NouvelleDate = nouvelleDate,
			Reference = reference,
			SocieteNo = societe.No,
			UtilisateurNo = utilisateur.No
		};
		return await _extraitBancaireRepository.CreateAsync(extraitBancaire).ConfigureAwait(continueOnCapturedContext: false);
	}

	public async Task<IList<LigneExtraitBancaire>> GetLignesAsync(int extraitBancaireNo)
	{
		return (await _extraitBancaireRepository.GetLignesAsync(extraitBancaireNo).ConfigureAwait(continueOnCapturedContext: false)).ToList();
	}

	public async Task DeleteAsync(int extraitBancaireNo)
	{
		ExtraitBancaire extraitBancaire = await _extraitBancaireRepository.GetAsync(extraitBancaireNo).ConfigureAwait(continueOnCapturedContext: false);
		if (extraitBancaire == null)
		{
			throw new ApplicationException($"Impossible de charger l'extrait bancaire [{extraitBancaireNo}].");
		}
		if (extraitBancaire.Etat != ExtraitBancaireEtat.NonRapproche)
		{
			throw new ApplicationException("L'extrait [" + extraitBancaire.Reference + "] est rapproché.");
		}
		await _extraitBancaireRepository.DeleteAsync(extraitBancaire).ConfigureAwait(continueOnCapturedContext: false);
	}

	public Task AddLinesAsync(IList<LigneExtraitBancaire> lignesExtrait)
	{
		if (lignesExtrait == null)
		{
			throw new ArgumentNullException("lignesExtrait");
		}
		return _extraitBancaireRepository.AddLinesAsync(lignesExtrait);
	}
}
