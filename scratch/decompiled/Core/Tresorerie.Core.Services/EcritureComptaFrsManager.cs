using System;
using System.Collections.Generic;
using System.Transactions;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Interfaces;
using Tresorerie.Core.Models;
using Tresorerie.Ressources;

namespace Tresorerie.Core.Services;

public class EcritureComptaFrsManager
{
	private readonly IEcritureComptaRepository _ecritureComptaRepository;

	public SocieteManager SocieteManager { get; set; }

	public EcritureComptaFrsManager(IEcritureComptaRepository ecritureComptaRepository)
	{
		if (ecritureComptaRepository == null)
		{
			throw new ArgumentNullException("ecritureComptaRepository");
		}
		_ecritureComptaRepository = ecritureComptaRepository;
	}

	public int Create(int erpNo, int dossierNo, int jour, string compteGeneral, decimal montant, string libelle, string numeroPiece, string reference, SensCompta sens, string codeJournal, ErpComptaPeriode periode, int annee, DateTime datePeriode, string tiersNumero, string pieceTresorerie, DateTime echeance, string contrePartieCompteG, string contrePartieTiers, bool isPointe, string pointage, bool isLettre, string lettrage, string numeroDocument, int deviseErp, decimal cours, decimal montantDevise)
	{
		Societe societe = SocieteManager.Societe;
		DateTime dateTime = new DateTime(annee, (int)periode, jour);
		if (dateTime > SocieteManager.Exercice.Fin || dateTime < SocieteManager.Exercice.Debut)
		{
			throw new ApplicationException("Ecriture exercice invalide!");
		}
		SocieteDevise deviseErp2 = societe.GetDeviseErp(deviseErp);
		if (deviseErp2 == null)
		{
			throw new InvalidOperationException(rcRessources.DeviseNotFound);
		}
		EcritureComptable ecriture = new EcritureComptable
		{
			ErpNo = erpNo,
			NumeroPiece = numeroPiece,
			Annee = annee,
			CodeJournal = codeJournal,
			CompteGeneral = compteGeneral,
			DateCreation = DateTime.Now,
			Domaine = MouvementDomaine.ReglementFournisseur,
			Jour = jour,
			Libelle = libelle,
			Montant = montant,
			Periode = periode,
			Reference = reference,
			Sens = sens,
			SocieteNo = societe.No,
			TiersNumero = tiersNumero,
			ContrePartieCompteG = contrePartieCompteG,
			Echeance = echeance,
			PieceTresorerie = pieceTresorerie,
			ContrePartieTiers = contrePartieTiers,
			Action = NatureActionEtape.Automatique,
			Etape = 0,
			IsLettre = isLettre,
			IsPointe = isPointe,
			Lettrage = lettrage,
			Pointage = pointage,
			NumeroDocument = numeroDocument,
			DossierNo = dossierNo,
			MouvementNo = null,
			Cours = cours,
			DeviseErpNo = deviseErp,
			DeviseSocieteNo = deviseErp2.No,
			MontantDevise = montantDevise
		};
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		int num = _ecritureComptaRepository.Create(ecriture);
		if (num <= 0)
		{
			throw new InvalidOperationException("Création de l'écriture comptable invalide!");
		}
		transactionScope.Complete();
		return num;
	}

	public int CreateForImport(int erpNo, int dossierNo, int jour, string compteGeneral, decimal montant, string libelle, string numeroPiece, string reference, SensCompta sens, string codeJournal, ErpComptaPeriode periode, int annee, DateTime datePeriode, string tiersNumero, string pieceTresorerie, DateTime echeance, string contrePartieCompteG, string contrePartieTiers, bool isPointe, string pointage, bool isLettre, string lettrage, string numeroDocument, int deviseErp, decimal cours)
	{
		Societe societe = SocieteManager.Societe;
		SocieteDevise deviseErp2 = societe.GetDeviseErp(deviseErp);
		if (deviseErp2 == null)
		{
			throw new InvalidOperationException(rcRessources.DeviseNotFound);
		}
		EcritureComptable ecriture = new EcritureComptable
		{
			ErpNo = erpNo,
			NumeroPiece = numeroPiece,
			Annee = annee,
			CodeJournal = codeJournal,
			CompteGeneral = compteGeneral,
			DateCreation = DateTime.Now,
			Domaine = MouvementDomaine.ReglementFournisseur,
			Jour = jour,
			Libelle = libelle,
			Montant = montant,
			Periode = periode,
			Reference = reference,
			Sens = sens,
			SocieteNo = societe.No,
			TiersNumero = tiersNumero,
			ContrePartieCompteG = contrePartieCompteG,
			Echeance = echeance,
			PieceTresorerie = pieceTresorerie,
			ContrePartieTiers = contrePartieTiers,
			Action = NatureActionEtape.Automatique,
			Etape = 0,
			IsLettre = isLettre,
			IsPointe = isPointe,
			Lettrage = lettrage,
			Pointage = pointage,
			NumeroDocument = numeroDocument,
			DossierNo = dossierNo,
			MouvementNo = null,
			Cours = cours,
			DeviseErpNo = deviseErp,
			DeviseSocieteNo = deviseErp2.No
		};
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		int num = _ecritureComptaRepository.Create(ecriture);
		if (num <= 0)
		{
			throw new InvalidOperationException("Création de l'écriture comptable invalide!");
		}
		transactionScope.Complete();
		return num;
	}

	public EcritureComptable Get(int no)
	{
		return _ecritureComptaRepository.Get(no);
	}

	public IEnumerable<EcritureComptable> GetAll(string fournisseurNumero, int annee, Societe societe = null)
	{
		societe = societe ?? SocieteManager.Societe;
		return _ecritureComptaRepository.GetByTiers(societe.No, fournisseurNumero, annee);
	}
}
