using System;
using System.Collections.Generic;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Interfaces;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Services;

public class RegleRetenueSourceTvaManager
{
	private readonly IRegleRetenueSourceTvaRepository _regleRetenueSourceTvaRepository;

	public GroupeService Groupe { get; set; }

	public RegleRetenueSourceTvaManager(IRegleRetenueSourceTvaRepository regleRetenueSourceTvaRepository)
	{
		_regleRetenueSourceTvaRepository = regleRetenueSourceTvaRepository ?? throw new ArgumentNullException("regleRetenueSourceTvaRepository");
	}

	public int Create(string nom, int designationDocumentNo, NatureFournisseur natureTiers, bool hasAttestation, RegleRetenueTvaCondition condition, decimal montant, int modeReglementNo, decimal montantTotal)
	{
		if (string.IsNullOrEmpty(nom))
		{
			throw new ArgumentNullException("nom");
		}
		if (montant < 0m)
		{
			throw new ApplicationException("Montant invalide.");
		}
		if (montantTotal < 0m)
		{
			throw new ApplicationException("Facturation monsuelle invalide.");
		}
		DesignationDocumentManager designationDocumentManager = Groupe.DesignationDocumentManager;
		ModeReglementManager modeReglementManager = Groupe.ModeReglementManager;
		if (designationDocumentManager.Get(designationDocumentNo) == null)
		{
			throw new ApplicationException("Impossible de charger la désignation document.");
		}
		ModeReglement modeReglement = modeReglementManager.Get(modeReglementNo);
		if (modeReglement == null)
		{
			throw new ApplicationException("Impossible de charger le mode de règlement.");
		}
		if (!modeReglement.IsRetenu)
		{
			throw new ApplicationException("Le mode de règlement [" + modeReglement.Intitule + "] n'est pas de type retenue");
		}
		RegleRetenueSourceTva regleRetenueSourceTva = new RegleRetenueSourceTva
		{
			DesignationDocumentNo = designationDocumentNo,
			Condition = condition,
			HasAttestation = hasAttestation,
			Montant = montant,
			FacturationMensuelle = montantTotal,
			NatureTiers = natureTiers,
			ModeReglementNo = modeReglementNo,
			Nom = nom
		};
		int? num = _regleRetenueSourceTvaRepository.Create(regleRetenueSourceTva);
		if (!num.HasValue)
		{
			throw new InvalidOperationException("Insertion invalide!");
		}
		return num.Value;
	}

	public void Delete(int no)
	{
		if (no <= 0)
		{
			throw new ArgumentNullException("no");
		}
		RegleRetenueSourceTva regleRetenueSourceTva = _regleRetenueSourceTvaRepository.Get(no);
		if (regleRetenueSourceTva == null)
		{
			throw new ArgumentException("Impossible de charger la règle!");
		}
		_regleRetenueSourceTvaRepository.Delete(regleRetenueSourceTva);
	}

	public RegleRetenueSourceTva Get(int no)
	{
		return _regleRetenueSourceTvaRepository.Get(no);
	}

	public List<RegleRetenueSourceTva> GetAll()
	{
		return _regleRetenueSourceTvaRepository.GetAll();
	}

	public void Update(int no, string nom, int designationDocumentNo, NatureFournisseur natureTiers, bool hasAttestation, RegleRetenueTvaCondition condition, decimal montant, int modeReglementNo, decimal montantTotal)
	{
		if (string.IsNullOrEmpty(nom))
		{
			throw new ArgumentNullException("nom");
		}
		if (montant < 0m)
		{
			throw new ApplicationException("Montant invalide.");
		}
		if (montantTotal < 0m)
		{
			throw new ApplicationException("Montant total invalide.");
		}
		DesignationDocumentManager designationDocumentManager = Groupe.DesignationDocumentManager;
		ModeReglementManager modeReglementManager = Groupe.ModeReglementManager;
		if (designationDocumentManager.Get(designationDocumentNo) == null)
		{
			throw new ApplicationException("Impossible de charger la désignation document.");
		}
		ModeReglement modeReglement = modeReglementManager.Get(modeReglementNo);
		if (modeReglement == null)
		{
			throw new ApplicationException("Impossible de charger le mode de règlement.");
		}
		if (!modeReglement.IsRetenu)
		{
			throw new ApplicationException("Le mode de règlement [" + modeReglement.Intitule + "] n'est pas de type retenue");
		}
		RegleRetenueSourceTva regleRetenueSourceTva = _regleRetenueSourceTvaRepository.Get(no);
		if (regleRetenueSourceTva == null)
		{
			throw new ArgumentException("Impossible de charger la règle!");
		}
		regleRetenueSourceTva.DesignationDocumentNo = designationDocumentNo;
		regleRetenueSourceTva.NatureTiers = natureTiers;
		regleRetenueSourceTva.HasAttestation = hasAttestation;
		regleRetenueSourceTva.Condition = condition;
		regleRetenueSourceTva.Montant = montant;
		regleRetenueSourceTva.ModeReglementNo = modeReglementNo;
		regleRetenueSourceTva.FacturationMensuelle = montantTotal;
		regleRetenueSourceTva.Nom = nom;
		_regleRetenueSourceTvaRepository.Update(regleRetenueSourceTva);
	}
}
