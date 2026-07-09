using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Interfaces;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Services;

public class VentilationAnalytiqueManager
{
	private readonly IVentilationAnalytiqueRepository _ventilationRepository;

	private readonly IMouvementDepenseRepository _depenseRepository;

	private readonly ISocieteRepository _societeRepository;

	private readonly IEcheanceRepository _echeanceRepository;

	public GroupeService Groupe { get; set; }

	private SocieteManager SocieteManager => Groupe?.SocieteManager;

	public VentilationAnalytiqueManager(IVentilationAnalytiqueRepository ventilationRepository, IMouvementDepenseRepository depenseRepository, ISocieteRepository societeRepository, IEcheanceRepository echeanceRepository)
	{
		_ventilationRepository = ventilationRepository ?? throw new ArgumentNullException("ventilationRepository");
		_depenseRepository = depenseRepository ?? throw new ArgumentNullException("depenseRepository");
		_societeRepository = societeRepository ?? throw new ArgumentNullException("societeRepository");
		_echeanceRepository = echeanceRepository ?? throw new ArgumentNullException("echeanceRepository");
	}

	public Task<IEnumerable<VentilationAnalytique>> GetAllAsync(int entityNo, AnalytiqueDomaine domaine)
	{
		Societe societe = SocieteManager.Societe;
		return _ventilationRepository.GetAllAsync(societe.No, entityNo, domaine);
	}

	public Task<IEnumerable<VentilationAnalytique>> GetAllAsync(int planNo, int entityNo, AnalytiqueDomaine domaine)
	{
		Societe societe = SocieteManager.Societe;
		return _ventilationRepository.GetAllAsync(societe.No, planNo, entityNo, domaine);
	}

	public Task<IEnumerable<VentilationAnalytique>> GetAllAsync(string compteNumero, int entityNo, AnalytiqueDomaine domaine)
	{
		Societe societe = SocieteManager.Societe;
		return _ventilationRepository.GetAllAsync(societe.No, compteNumero, entityNo, domaine);
	}

	public async Task CreateAsync(VentilationAnalytique ventilation)
	{
		if (ventilation == null)
		{
			throw new ArgumentNullException("ventilation");
		}
		Societe societe = _societeRepository.Get(ventilation.SocieteNo);
		if (societe == null)
		{
			throw new ApplicationException($"Impossible de charger la société [{ventilation.SocieteNo}]!");
		}
		if (societe.VentilationAnalytique != TypeVentilationAnalytique.PlanAnalytique)
		{
			throw new InvalidOperationException("La société [" + societe.RaisonSociale + "] ne supporte pas la ventilation analytique !");
		}
		decimal montantEntity;
		switch (ventilation.Domaine)
		{
		case AnalytiqueDomaine.FactureFournisseurTresorerie:
		{
			Facture facture = await SocieteManager.FactureFournisseurGetAsync(ventilation.EntityNo).ConfigureAwait(continueOnCapturedContext: false);
			if (facture == null)
			{
				throw new ApplicationException($"Impossible de charger la facture trésorerie [{ventilation.EntityNo}]!");
			}
			if (facture.IsComptabilise)
			{
				throw new ApplicationException("La facture [" + facture.DocumentNumero + "] est comptabilisée!");
			}
			montantEntity = Math.Abs(facture.MontantHTDevise);
			break;
		}
		case AnalytiqueDomaine.Depense:
		{
			MouvementDepense mouvementDepense = _depenseRepository.Get(ventilation.EntityNo);
			if (mouvementDepense == null)
			{
				throw new ApplicationException($"Impossible de charger la dépense [{ventilation.EntityNo}]!");
			}
			if (mouvementDepense.IsComptabilise != EtatComptabilite.NonComptabilise)
			{
				throw new ApplicationException("La dépense [" + mouvementDepense.Numero + "] est comptabilisée!");
			}
			montantEntity = Math.Abs(mouvementDepense.Montant);
			break;
		}
		case AnalytiqueDomaine.EcritureTresorerie:
		{
			Echeance echeance = _echeanceRepository.Get(ventilation.EntityNo);
			if (echeance == null)
			{
				throw new ApplicationException($"Impossible de charger l'échéance [{ventilation.EntityNo}]!");
			}
			if (echeance.IsComptabilise)
			{
				throw new ApplicationException("L'échéance [" + echeance.DocumentNumero + "] est comptabilisée!");
			}
			IEnumerable<Echeance> allByDocument = _echeanceRepository.GetAllByDocument(echeance.Domaine, echeance.SocieteNo, echeance.DocumentNumero);
			montantEntity = Math.Abs(allByDocument.Sum((Echeance x) => x.MontantDeviseSociete));
			break;
		}
		default:
			throw new NotImplementedException(ventilation.Domaine.GetDisplayDescription());
		}
		if (ventilation.Montant <= 0m)
		{
			throw new ApplicationException("Montant invalide!");
		}
		IEnumerable<VentilationAnalytique> source = await _ventilationRepository.GetAllAsync(ventilation.SocieteNo, ventilation.PlanAnalytiqueNo, ventilation.EntityNo, ventilation.Domaine, ventilation.CompteNum).ConfigureAwait(continueOnCapturedContext: false);
		if (source.Any((VentilationAnalytique x) => x.SectionNumero == ventilation.SectionNumero))
		{
			throw new ApplicationException("La séction [" + ventilation.SectionNumero + "] existe déjà!");
		}
		if (source.Sum((VentilationAnalytique x) => x.Montant) + ventilation.Montant > montantEntity)
		{
			throw new ApplicationException("Le total des ventilations est supérieur à le montant de l'entité!");
		}
		if (string.IsNullOrEmpty(ventilation.CompteNum))
		{
			ventilation.CompteNum = string.Empty;
		}
		await _ventilationRepository.CreateAsync(ventilation).ConfigureAwait(continueOnCapturedContext: false);
	}

	public async Task UpdateAsync(int ligneNo, decimal montant)
	{
		VentilationAnalytique ventilation = await _ventilationRepository.GetAsync(ligneNo).ConfigureAwait(continueOnCapturedContext: false);
		if (ventilation == null)
		{
			throw new ApplicationException($"Impossible de charger la ligne [{ligneNo}]!");
		}
		Societe societe = _societeRepository.Get(ventilation.SocieteNo);
		if (societe == null)
		{
			throw new ApplicationException($"Impossible de charger la société [{ventilation.SocieteNo}]!");
		}
		if (societe.VentilationAnalytique != TypeVentilationAnalytique.PlanAnalytique)
		{
			throw new InvalidOperationException("La société [" + societe.RaisonSociale + "] ne supporte pas la ventilation analytique !");
		}
		decimal montantEntity;
		switch (ventilation.Domaine)
		{
		case AnalytiqueDomaine.FactureFournisseurTresorerie:
		{
			Facture facture = await SocieteManager.FactureFournisseurGetAsync(ventilation.EntityNo).ConfigureAwait(continueOnCapturedContext: false);
			if (facture == null)
			{
				throw new ApplicationException($"Impossible de charger la facture trésorerie [{ventilation.EntityNo}]!");
			}
			if (facture.IsComptabilise)
			{
				throw new ApplicationException("La facture [" + facture.DocumentNumero + "] est comptabilisée!");
			}
			montantEntity = Math.Abs(facture.MontantHTDevise);
			break;
		}
		case AnalytiqueDomaine.Depense:
		{
			MouvementDepense mouvementDepense = _depenseRepository.Get(ventilation.EntityNo);
			if (mouvementDepense == null)
			{
				throw new ApplicationException($"Impossible de charger la dépense [{ventilation.EntityNo}]!");
			}
			if (mouvementDepense.IsComptabilise != EtatComptabilite.NonComptabilise)
			{
				throw new ApplicationException("La dépense [" + mouvementDepense.Numero + "] est comptabilisée!");
			}
			montantEntity = Math.Abs(mouvementDepense.Montant);
			break;
		}
		default:
			throw new NotImplementedException(ventilation.Domaine.GetDisplayDescription());
		}
		if (montant <= 0m)
		{
			throw new ApplicationException("Montant invalide!");
		}
		if ((await _ventilationRepository.GetAllAsync(ventilation.SocieteNo, ventilation.PlanAnalytiqueNo, ventilation.EntityNo, ventilation.Domaine).ConfigureAwait(continueOnCapturedContext: false)).Where((VentilationAnalytique x) => x.No != ventilation.No).Sum((VentilationAnalytique x) => x.Montant) + montant > montantEntity)
		{
			throw new ApplicationException("Le total des ventilations est supérieur à le montant de l'entité!");
		}
		await _ventilationRepository.UpdateAsync(ligneNo, montant).ConfigureAwait(continueOnCapturedContext: false);
	}

	public async Task DeleteAsync(int ligneNo)
	{
		VentilationAnalytique ventilation = await _ventilationRepository.GetAsync(ligneNo).ConfigureAwait(continueOnCapturedContext: false);
		if (ventilation == null)
		{
			throw new ApplicationException($"Impossible de charger la ligne [{ligneNo}]!");
		}
		Societe societe = _societeRepository.Get(ventilation.SocieteNo);
		if (societe == null)
		{
			throw new ApplicationException($"Impossible de charger la société [{ventilation.SocieteNo}]!");
		}
		if (societe.VentilationAnalytique != TypeVentilationAnalytique.PlanAnalytique)
		{
			throw new InvalidOperationException("La société [" + societe.RaisonSociale + "] ne supporte pas la ventilation analytique !");
		}
		switch (ventilation.Domaine)
		{
		case AnalytiqueDomaine.FactureFournisseurTresorerie:
		{
			Facture facture = await SocieteManager.FactureFournisseurGetAsync(ventilation.EntityNo).ConfigureAwait(continueOnCapturedContext: false);
			if (facture == null)
			{
				throw new ApplicationException($"Impossible de charger la facture trésorerie [{ventilation.EntityNo}]!");
			}
			if (facture.IsComptabilise)
			{
				throw new ApplicationException("La facture [" + facture.DocumentNumero + "] est comptabilisée!");
			}
			break;
		}
		case AnalytiqueDomaine.Depense:
		{
			MouvementDepense mouvementDepense = _depenseRepository.Get(ventilation.EntityNo);
			if (mouvementDepense == null)
			{
				throw new ApplicationException($"Impossible de charger la dépense [{ventilation.EntityNo}]!");
			}
			if (mouvementDepense.IsComptabilise != EtatComptabilite.NonComptabilise)
			{
				throw new ApplicationException("La dépense [" + mouvementDepense.Numero + "] est comptabilisée!");
			}
			break;
		}
		case AnalytiqueDomaine.EcritureTresorerie:
		{
			Echeance echeance = _echeanceRepository.Get(ventilation.EntityNo);
			if (echeance == null)
			{
				throw new ApplicationException($"Impossible de charger l'échéance [{ventilation.EntityNo}]!");
			}
			if (echeance.IsComptabilise)
			{
				throw new ApplicationException("L'échéance [" + echeance.DocumentNumero + "] est comptabilisée!");
			}
			break;
		}
		default:
			throw new NotImplementedException(ventilation.Domaine.GetDisplayDescription());
		}
		await _ventilationRepository.DeleteAsync(ligneNo).ConfigureAwait(continueOnCapturedContext: false);
	}
}
