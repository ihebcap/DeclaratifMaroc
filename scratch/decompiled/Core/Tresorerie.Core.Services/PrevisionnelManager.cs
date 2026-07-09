using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Interfaces;
using Tresorerie.Core.Models;
using Tresorerie.Ressources;

namespace Tresorerie.Core.Services;

public class PrevisionnelManager
{
	private readonly IPrevisionnelleRepository _previsionnelleRepository;

	private readonly IInformationsBanqueRepository _informationsBanqueRepository;

	private readonly ITypePrevisionRepository _typePrevisionRepository;

	private readonly ITypeOperationBancaireRepository _typeOperationBancaireRepository;

	private readonly IEcritureComptaRepository _ecritureComptaRepository;

	public GroupeService Groupe { get; set; }

	public Societe Societe => Groupe.SocieteManager.Societe;

	public PrevisionnelManager(IPrevisionnelleRepository previsionnelleRepository, IInformationsBanqueRepository informationsBanqueRepository, ITypePrevisionRepository typePrevisionRepository, ITypeOperationBancaireRepository typeOperationBancaireRepository, IEcritureComptaRepository ecritureComptaRepository)
	{
		_previsionnelleRepository = previsionnelleRepository ?? throw new ArgumentNullException("previsionnelleRepository");
		_informationsBanqueRepository = informationsBanqueRepository ?? throw new ArgumentNullException("informationsBanqueRepository");
		_typePrevisionRepository = typePrevisionRepository ?? throw new ArgumentNullException("typePrevisionRepository");
		_typeOperationBancaireRepository = typeOperationBancaireRepository ?? throw new ArgumentNullException("typeOperationBancaireRepository");
		_ecritureComptaRepository = ecritureComptaRepository ?? throw new ArgumentNullException("ecritureComptaRepository");
	}

	public IEnumerable<OperationBancaire> GetAllOperationBancaire(Societe societe = null)
	{
		societe = societe ?? Societe;
		return _previsionnelleRepository.GetAllOperationBancaire(societe.No);
	}

	public IEnumerable<OperationBancaire> GetAllOperationBancaire(string dossierNumero, Societe societe = null)
	{
		societe = societe ?? Societe;
		return _previsionnelleRepository.GetAllOperationBancaire(societe.No, dossierNumero);
	}

	public IEnumerable<OperationBancaire> GetAllOperationBancaire(DateTime dateDe, DateTime dateA, bool isComptabilise, CancellationToken cancellationToken, Societe societe = null)
	{
		societe = societe ?? Societe;
		return _previsionnelleRepository.GetAllOperationBancaire(societe.No, dateDe, dateA, isComptabilise, cancellationToken);
	}

	public IEnumerable<OperationBancaire> GetAllOperationBancaireToDeclaration(DateTime dateDe, DateTime dateA, CancellationToken cancellationToken, Societe societe = null)
	{
		societe = societe ?? Societe;
		return _previsionnelleRepository.GetAllOperationBancaireToDeclaration(societe.No, dateDe, dateA, cancellationToken);
	}

	public OperationBancaire GetOperationBancaire(int no)
	{
		return _previsionnelleRepository.GetOperationBancaire(no);
	}

	public OperationBancaire GetOperationBancaireByBordereau(int bordereauNo)
	{
		return _previsionnelleRepository.GetOperationBancaireByBordereau(bordereauNo);
	}

	public void OperationBancaireSetDeclarationTva(int opNo, int declarationNo)
	{
		OperationBancaire operationBancaire = _previsionnelleRepository.GetOperationBancaire(opNo);
		if (operationBancaire == null)
		{
			throw new ApplicationException("Impossible de charger l'opération bancaire.");
		}
		if (operationBancaire.DeclarationTvaEncaissementNo.HasValue)
		{
			throw new ApplicationException("L'opération bancaire est inclut dans une déclaration.");
		}
		DeclarationTvaEncaissement declarationTvaEncaissement = Groupe.SocieteManager.DeclarationTvaEncaissementGet(declarationNo);
		if (declarationTvaEncaissement == null)
		{
			throw new ApplicationException("Impossible de charger la déclaration.");
		}
		if (declarationTvaEncaissement.Statut != StatutDeclaration.EnCours)
		{
			throw new ApplicationException("La déclaration [" + declarationTvaEncaissement.Numero + "] est clôturée.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		operationBancaire.DeclarationTvaEncaissementNo = declarationNo;
		_previsionnelleRepository.OperationBancaireSetDeclarationTva(opNo, declarationNo);
		transactionScope.Complete();
	}

	public void OperationBancaireAnnulerDeclarationTva(int opNo)
	{
		OperationBancaire operationBancaire = _previsionnelleRepository.GetOperationBancaire(opNo);
		if (operationBancaire == null)
		{
			throw new ApplicationException("Impossible de charger l'opération bancaire.");
		}
		if (!operationBancaire.DeclarationTvaEncaissementNo.HasValue)
		{
			throw new ApplicationException("L'opération bancaire n'est pas inclut dans une déclaration.");
		}
		DeclarationTvaEncaissement declarationTvaEncaissement = Groupe.SocieteManager.DeclarationTvaEncaissementGet(operationBancaire.DeclarationTvaEncaissementNo.Value);
		if (declarationTvaEncaissement == null)
		{
			throw new ApplicationException("Impossible de charger la déclaration.");
		}
		if (declarationTvaEncaissement.Statut != StatutDeclaration.EnCours)
		{
			throw new ApplicationException("La déclaration [" + declarationTvaEncaissement.Numero + "] est clôturée.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		operationBancaire.DeclarationTvaEncaissementNo = null;
		_previsionnelleRepository.OperationBancaireSetDeclarationTva(opNo, null);
		transactionScope.Complete();
	}

	public IEnumerable<Prevision> GetAllPrevision(Societe societe = null)
	{
		societe = societe ?? Societe;
		return _previsionnelleRepository.GetAllPrevision(societe.No);
	}

	public Prevision GetPrevision(int no)
	{
		return _previsionnelleRepository.GetPrevision(no);
	}

	public Task<IEnumerable<Previsionnelle>> GetPrevisionelleAsync(DateTime dateMax, IList<Societe> societes = null)
	{
		if (societes == null || !societes.Any())
		{
			societes = new List<Societe> { Societe };
		}
		dateMax = dateMax.Date.AddDays(1.0).AddSeconds(-1.0);
		return _previsionnelleRepository.GetAllAsync(dateMax, societes.Select((Societe x) => x.No).ToArray());
	}

	public void PrevisionAnnuler(int no, bool isAnnuler)
	{
		Prevision prevision = _previsionnelleRepository.GetPrevision(no);
		if (prevision == null)
		{
			throw new ArgumentNullException("prevision");
		}
		if (isAnnuler && prevision.IsPointe)
		{
			throw new InvalidOperationException("Opération invalide! Prévision est rapproché.");
		}
		_previsionnelleRepository.AnnulerPrevision(no, isAnnuler);
	}

	public void PrevisionnelleChangerBanquePrevue(int[] nos, InformationsBanque banque)
	{
		if (banque == null)
		{
			throw new ArgumentNullException("banque");
		}
		List<TypePrevisionnel> source = new List<TypePrevisionnel>
		{
			TypePrevisionnel.EnCours,
			TypePrevisionnel.Prevision
		};
		foreach (int no in nos)
		{
			Previsionnelle ligne = _previsionnelleRepository.Get(no);
			if (ligne == null)
			{
				throw new InvalidOperationException("Ligne prévisionnelle invalide!");
			}
			if (!source.Any((TypePrevisionnel p) => p == ligne.TypePrevisionnel))
			{
				throw new ApplicationException($"Le type [{ligne.TypePrevisionnel}] de la ligne prévisionnelle [{ligne.Numero}] est non supporté!");
			}
			if (ligne.IsPointe)
			{
				throw new InvalidOperationException("Opération invalide! Ligne prévisionnelle est rapprochée!");
			}
			if (ligne.Domaine != DomainePrevisionnelle.ReglementClient)
			{
				throw new InvalidOperationException("Opération invalide pour le domaine de cette ligne!");
			}
			if (ligne.Type != ReglementType.Cheque && ligne.Type != ReglementType.Traite)
			{
				throw new InvalidOperationException("Opération invalide pour le type de règlement de cette ligne!");
			}
			if ((Groupe.SocieteManager.CaisseManager.ReglementGet(ligne.MouvementNo) ?? throw new ApplicationException("Impossible de charger le règlement [" + ligne.Numero + "]!")).IsRemis != Remis.NonRemis)
			{
				throw new ApplicationException("Le règlement [" + ligne.Numero + "] est remis!");
			}
			_previsionnelleRepository.ChangerBanquePrevue(ligne.No, banque.BanqueNo);
		}
	}

	public void PrevisionnelleDecaler(int[] nos, DateTime echeance)
	{
		foreach (int no in nos)
		{
			Previsionnelle previsionnelle = _previsionnelleRepository.Get(no);
			if (previsionnelle == null)
			{
				throw new InvalidOperationException("Ligne prévisionnelle invalide!");
			}
			if (previsionnelle.IsPointe)
			{
				throw new InvalidOperationException("Opération invalide! Ligne prévisionnelle est rapprochée!");
			}
			switch (previsionnelle.Domaine)
			{
			case DomainePrevisionnelle.ReglementClient:
			case DomainePrevisionnelle.ReglementFourniseur:
				if (previsionnelle.Type != ReglementType.Cheque && previsionnelle.Type != ReglementType.Traite)
				{
					throw new InvalidOperationException("Opération invalide pour le type de règlement de cette ligne!");
				}
				break;
			default:
				throw new InvalidOperationException($"Opération invalide pour le domaine [{previsionnelle.Domaine}] de cette ligne!");
			case DomainePrevisionnelle.Prevision:
				break;
			}
			_previsionnelleRepository.DecalerEcheancePrevue(previsionnelle.No, echeance);
		}
	}

	public void PrevisionSupprimer(int no)
	{
		if (_previsionnelleRepository.Get(no) == null)
		{
			throw new InvalidOperationException("Mouvement invalide!");
		}
		_previsionnelleRepository.Delete(no);
	}

	public void OperationBancaireSupprimer(int no, bool fromDeleteBordereau = false)
	{
		OperationBancaire operationBancaire = _previsionnelleRepository.GetOperationBancaire(no);
		if (operationBancaire == null)
		{
			throw new InvalidOperationException("Mouvement invalide!");
		}
		if (operationBancaire.IsComptabilise)
		{
			throw new ApplicationException("L'opération bancaire est comptabilisée.");
		}
		if (!fromDeleteBordereau && operationBancaire.BordereauNo.HasValue && operationBancaire.BordereauNo.Value != 0)
		{
			throw new ApplicationException("L'opération bancaire est associée au bordereau n°[" + operationBancaire.BordereauNumero + "].");
		}
		_previsionnelleRepository.Delete(no);
	}

	public void OperationBancaireComptabiliser(int no, List<EcritureComptable> erpEcritures)
	{
		if ((_previsionnelleRepository.GetOperationBancaire(no) ?? throw new InvalidOperationException("Mouvement invalide!")).IsComptabilise)
		{
			throw new ApplicationException("L'opération bancaire est comptabilisée.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions, TransactionScopeAsyncFlowOption.Enabled);
		foreach (EcritureComptable erpEcriture in erpEcritures)
		{
			_ecritureComptaRepository.Create(erpEcriture);
		}
		_previsionnelleRepository.OperationBancaireUpdateCompta(no, compta: true, DateTime.Now);
		transactionScope.Complete();
	}

	public void OperationBancaireDeComptabiliser(int no)
	{
		OperationBancaire operationBancaire = _previsionnelleRepository.GetOperationBancaire(no);
		if (operationBancaire == null)
		{
			throw new InvalidOperationException("Mouvement invalide!");
		}
		if (!operationBancaire.IsComptabilise)
		{
			throw new ApplicationException("L'opération bancaire n'est pas comptabilisée.");
		}
		TransactionOptions transactionOptions = new TransactionOptions
		{
			IsolationLevel = IsolationLevel.ReadCommitted,
			Timeout = TransactionManager.MaximumTimeout
		};
		using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
		_ecritureComptaRepository.DeleteLigneOperationBancaire(operationBancaire.No);
		_previsionnelleRepository.OperationBancaireUpdateCompta(no, compta: false, operationBancaire.Date);
		transactionScope.Complete();
	}

	public IList<EcritureComptable> GetEcrituresOperationBancaire(int mvtNo)
	{
		if (mvtNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorMvtNo);
		}
		OperationBancaire operationBancaire = GetOperationBancaire(mvtNo);
		if (operationBancaire == null)
		{
			throw new ArgumentNullException("oprerationBancaire");
		}
		return _ecritureComptaRepository.GetAll(operationBancaire.No, MouvementDomaine.OperationBancaire);
	}

	public void UpdatePrevision(DateTime echeance, decimal montant, IErpCompteBanq banque, string libelle, int no, string affaireNumero)
	{
		if (banque == null)
		{
			throw new InvalidOperationException("Banque invalide!");
		}
		Prevision prevision = _previsionnelleRepository.GetPrevision(no);
		if (prevision == null)
		{
			throw new InvalidOperationException("Prévision invalide!");
		}
		if (prevision.IsAnnuler)
		{
			throw new InvalidOperationException("Impossible de modifier une prévision déjà annulée!");
		}
		if (prevision.IsPointe)
		{
			throw new InvalidOperationException("Impossible de modifier une prévision déjà pointée!");
		}
		if (string.IsNullOrEmpty(libelle))
		{
			throw new InvalidOperationException("Libellé prévision invalide!");
		}
		if (montant <= 0m)
		{
			throw new InvalidOperationException("Montant prévision invalide!");
		}
		prevision.Echeance = echeance;
		prevision.BanqueNo = banque.Id;
		prevision.Montant = montant;
		prevision.Libelle = libelle;
		prevision.AffaireNumero = affaireNumero;
		_previsionnelleRepository.Update(prevision);
	}

	public void PointerPrevision(DateTime echeance, decimal montant, IErpCompteBanq banque, int no)
	{
		if (banque == null)
		{
			throw new InvalidOperationException("Banque invalide!");
		}
		Prevision prevision = _previsionnelleRepository.GetPrevision(no);
		if (prevision == null)
		{
			throw new InvalidOperationException("Prévision invalide!");
		}
		if (prevision.IsAnnuler)
		{
			throw new InvalidOperationException("Impossible de pointer une prévision déjà annulée!");
		}
		if (prevision.IsPointe)
		{
			throw new InvalidOperationException("Prévision est déjà pointée!");
		}
		if (montant <= 0m)
		{
			throw new InvalidOperationException("Montant prévision invalide!");
		}
		prevision.IsPointe = true;
		prevision.DatePointe = echeance;
		prevision.Echeance = echeance;
		prevision.BanqueNo = banque.Id;
		prevision.Montant = montant;
		_previsionnelleRepository.Update(prevision);
	}

	public void MettreAjourCoursPrevisionnel(int no, decimal coursPrevisionnel, Societe societe = null)
	{
		societe = societe ?? Societe;
		if (no <= 0)
		{
			throw new ArgumentNullException("no");
		}
		if (coursPrevisionnel <= 0m)
		{
			throw new ArgumentException("Le cours prévisionnel est invalide.");
		}
		Previsionnelle previsionnelle = _previsionnelleRepository.Get(no);
		if (previsionnelle == null)
		{
			throw new ApplicationException("Impossible de charger la ligne prévisionnelle.");
		}
		SocieteDevise deviseErp = societe.GetDeviseErp(societe.DeviseErpNo);
		if (deviseErp == null)
		{
			throw new ApplicationException("Impossible de déterminer la devise sociéte.");
		}
		if (previsionnelle.Domaine == DomainePrevisionnelle.ErpFournisseur && previsionnelle.DeviseNo != deviseErp.No)
		{
			previsionnelle.CoursPrevisionnel = coursPrevisionnel;
			previsionnelle.MontantDeviseSociete = coursPrevisionnel * previsionnelle.MontantDevise;
			_previsionnelleRepository.UpdateCoursPrevisionnel(previsionnelle);
		}
	}

	private void CreateOperationBancaireSuiteBugCompta(List<IErpCompteBanq> erpCompteBanque, List<IErpComptaJournal> erpComptaJournals, Societe societe = null)
	{
		societe = societe ?? Societe;
		List<EcritureComptable> source = _ecritureComptaRepository.GetEcritureDeletedOpearationBancaire(societe.No).ToList();
		if (!source.Any())
		{
			return;
		}
		foreach (IGrouping<int?, EcritureComptable> gr in (from x in source
			group x by x.MouvementNo).ToList())
		{
			IEnumerable<EcritureComptable> enumerable = source.Where((EcritureComptable x) => x.MouvementNo == gr.Key);
			if (!enumerable.Any())
			{
				continue;
			}
			EcritureComptable firstEc = enumerable.First();
			IErpComptaJournal journal = erpComptaJournals.FirstOrDefault((IErpComptaJournal x) => x.Code == firstEc.CodeJournal);
			if (journal == null)
			{
				continue;
			}
			IErpCompteBanq erpCompteBanq = erpCompteBanque.FirstOrDefault((IErpCompteBanq x) => x.Journal == firstEc.CodeJournal);
			if (erpCompteBanq == null)
			{
				continue;
			}
			IEnumerable<EcritureComptable> source2 = enumerable.Where((EcritureComptable x) => x.CompteGeneral == journal.CompteGeneral);
			if (!source2.Any())
			{
				continue;
			}
			IEnumerable<EcritureComptable> source3 = enumerable.Where((EcritureComptable x) => x.CompteGeneral != journal.CompteGeneral);
			if (!source3.Any())
			{
				continue;
			}
			decimal montantOperation = source3.Max((EcritureComptable x) => x.Montant);
			decimal montantTva = source2.Sum((EcritureComptable x) => x.Montant) - montantOperation;
			EcritureComptable ecritureComptable = source3.SingleOrDefault((EcritureComptable x) => x.Montant == montantOperation);
			if (ecritureComptable == null)
			{
				continue;
			}
			TypeOperationBancaire typeOperationBancaire = _typeOperationBancaireRepository.Get(societe.No, ecritureComptable.CompteGeneral);
			if (typeOperationBancaire == null)
			{
				continue;
			}
			TransactionOptions transactionOptions = new TransactionOptions
			{
				IsolationLevel = IsolationLevel.ReadCommitted,
				Timeout = TransactionManager.MaximumTimeout
			};
			using TransactionScope transactionScope = new TransactionScope(TransactionScopeOption.Required, transactionOptions);
			int num = AjouterOperationBancaire(firstEc.Echeance, firstEc.Reference, montantOperation, erpCompteBanq, typeOperationBancaire.Intitule, typeOperationBancaire.Sens, typeOperationBancaire.No, montantTva, "");
			foreach (EcritureComptable item in enumerable)
			{
				item.MouvementNo = num;
				_ecritureComptaRepository.UpdateEcriture(item);
			}
			_previsionnelleRepository.OperationBancaireUpdateCompta(num, compta: true, firstEc.DateCreation);
			transactionScope.Complete();
		}
	}

	public Task MettreAjourPrevisionnelleAsync(List<IErpCompteBanq> erpCompteBanque, List<IErpComptaJournal> erpComptaJournals, Societe societe = null)
	{
		societe = societe ?? Societe;
		if (_informationsBanqueRepository.GetDefaultBanquePrevisionnel(societe.No) == null)
		{
			throw new ApplicationException("Veuillez paramétrer la banque prévisionnelle par défaut !");
		}
		CreateOperationBancaireSuiteBugCompta(erpCompteBanque, erpComptaJournals, societe);
		return _previsionnelleRepository.MiseAJourPrevisionnelleAsync(societe.No, societe.InclueEcheanceClientPrevue, societe.InclueEcheanceFournisseurPrevue, societe.InclueEcheanceFournisseurPrevue, societe.InclueBcBlFaPrevisionnel, societe.StatutPieceClient, societe.InclueBcBlFaAchatPrevisionnel, societe.StatutPieceFournisseur, societe.IsImportStatutSaisieClt, societe.IsImportStatutConfirmeClt);
	}

	public int AjouterOperationBancaire(DateTime date, string piece, decimal montant, IErpCompteBanq banque, string libelle, SensPrevisionnelle sens, int typeNo, decimal montantTva, string affaireNumero, string dossierNumero = "", int? bordereauNo = null, Societe societe = null)
	{
		societe = societe ?? Societe;
		if (banque == null)
		{
			throw new InvalidOperationException("Banque invalide!");
		}
		TypeOperationBancaire typeOperationBancaire = _typeOperationBancaireRepository.Get(typeNo);
		if (typeOperationBancaire == null)
		{
			throw new InvalidOperationException("Type opération bancaire invalide!");
		}
		InformationsBanque byErpNo = _informationsBanqueRepository.GetByErpNo(societe.No, banque.Id);
		if (banque != null && byErpNo.EnSommeil)
		{
			throw new InvalidOperationException("Imposiible de créer l'opération! La banque est en sommeil.");
		}
		OperationBancaire operation = new OperationBancaire
		{
			BanqueNo = banque.Id,
			Date = date,
			DatePointe = date,
			IsPointe = true,
			Libelle = libelle,
			Montant = montant,
			SocieteNo = societe.No,
			Piece = piece,
			Sens = sens,
			TypeNo = typeNo,
			TypeCode = typeOperationBancaire.Code,
			MontantTva = montantTva,
			IsComptabilise = false,
			DateComptabilisation = date,
			DossierNumero = dossierNumero,
			AffaireNumero = affaireNumero,
			BordereauNo = bordereauNo
		};
		return _previsionnelleRepository.Insert(operation);
	}

	public void AjouterPrevision(DateTime date, DateTime echeance, decimal montant, IErpCompteBanq banque, string libelle, SensPrevisionnelle sens, int typeNo, string affaireNumero, Societe societe = null)
	{
		societe = societe ?? Societe;
		if (banque == null)
		{
			throw new InvalidOperationException("Banque invalide!");
		}
		TypePrevision typePrevision = _typePrevisionRepository.Get(typeNo);
		if (typePrevision == null)
		{
			throw new InvalidOperationException("Type prévision invalide!");
		}
		if (string.IsNullOrEmpty(libelle))
		{
			throw new InvalidOperationException("Libellé prévision invalide!");
		}
		if (montant <= 0m)
		{
			throw new InvalidOperationException("Montant prévision invalide!");
		}
		InformationsBanque byErpNo = _informationsBanqueRepository.GetByErpNo(societe.No, banque.Id);
		if (byErpNo != null && byErpNo.EnSommeil)
		{
			throw new ApplicationException("Banque en sommeil");
		}
		Prevision prevision = new Prevision
		{
			BanqueNo = banque.Id,
			Date = date,
			Echeance = echeance,
			Libelle = libelle,
			Montant = montant,
			SocieteNo = societe.No,
			Sens = sens,
			TypeNo = typeNo,
			TypeCode = typePrevision.Code,
			AffaireNumero = affaireNumero
		};
		_previsionnelleRepository.Insert(prevision);
	}

	public int TypePrevisionCreate(string code, string intitule, string compteGeneral, SensPrevisionnelle sens, Societe societe = null)
	{
		if (string.IsNullOrEmpty(code))
		{
			throw new InvalidOperationException("Code invalide!");
		}
		if (string.IsNullOrEmpty(intitule))
		{
			throw new InvalidOperationException("Intitulé invalide!");
		}
		societe = societe ?? Societe;
		if (_typePrevisionRepository.Get(code, societe.No) != null)
		{
			throw new InvalidOperationException("Code type prévision déjà existe!");
		}
		TypePrevision type = new TypePrevision
		{
			SocieteNo = societe.No,
			Intitule = intitule,
			CompteGeneral = compteGeneral,
			Code = code,
			Sens = sens
		};
		return _typePrevisionRepository.Create(type);
	}

	public void TypePrevisionDeleted(int no)
	{
		TypePrevision typePrevision = _typePrevisionRepository.Get(no);
		if (typePrevision == null)
		{
			throw new InvalidOperationException("Type prévision n'est pas existe!");
		}
		if (!_typePrevisionRepository.CanDeleted(no))
		{
			throw new InvalidOperationException("Opération invalide! Type prévision est déjà utilisé.");
		}
		_typePrevisionRepository.Delete(typePrevision);
	}

	public TypePrevision TypePrevisionGet(int no)
	{
		return _typePrevisionRepository.Get(no);
	}

	public TypePrevision TypePrevisionGet(string code, Societe societe = null)
	{
		societe = societe ?? Societe;
		return _typePrevisionRepository.Get(code, societe.No);
	}

	public IEnumerable<TypePrevision> TypePrevisionGetAll(Societe societe = null)
	{
		societe = societe ?? Societe;
		return _typePrevisionRepository.GetAll(societe.No);
	}

	public void TypePrevisionUpdate(int no, string intitule, string compteGeneral)
	{
		if (string.IsNullOrEmpty(intitule))
		{
			throw new InvalidOperationException("Intitulé invalide!");
		}
		if (_typePrevisionRepository.Get(no) == null)
		{
			throw new InvalidOperationException("Type prévision n'est pas existe!");
		}
		_typePrevisionRepository.Update(no, intitule, compteGeneral);
	}

	public int TypeOperationBancaireCreate(string code, string intitule, string compteGeneral, SensPrevisionnelle sens, int erpTaxeNo, string codeInterBanque, Societe societe = null)
	{
		if (string.IsNullOrEmpty(code))
		{
			throw new InvalidOperationException("Code invalide!");
		}
		if (string.IsNullOrEmpty(intitule))
		{
			throw new InvalidOperationException("Intitulé invalide!");
		}
		societe = societe ?? Societe;
		if (_typeOperationBancaireRepository.Get(code, societe.No) != null)
		{
			throw new InvalidOperationException("Code type prévision déjà existe!");
		}
		TypeOperationBancaire type = new TypeOperationBancaire
		{
			SocieteNo = societe.No,
			Intitule = intitule,
			CompteGeneral = compteGeneral,
			Code = code,
			Sens = sens,
			ErpTaxeNo = erpTaxeNo,
			CodeInterBanque = codeInterBanque
		};
		return _typeOperationBancaireRepository.Create(type);
	}

	public void TypeOperationBancaireDeleted(int no)
	{
		TypeOperationBancaire typeOperationBancaire = _typeOperationBancaireRepository.Get(no);
		if (typeOperationBancaire == null)
		{
			throw new InvalidOperationException("Type prévision n'est pas existe!");
		}
		if (!_typeOperationBancaireRepository.CanDeleted(no))
		{
			throw new InvalidOperationException("Opération invalide! Type prévision est déjà utilisé.");
		}
		_typeOperationBancaireRepository.Delete(typeOperationBancaire);
	}

	public TypeOperationBancaire TypeOperationBancaireGet(int no)
	{
		return _typeOperationBancaireRepository.Get(no);
	}

	public TypeOperationBancaire TypeOperationBancaireGet(string code, Societe societe = null)
	{
		societe = societe ?? Societe;
		return _typeOperationBancaireRepository.Get(code, societe.No);
	}

	public IEnumerable<TypeOperationBancaire> TypeOperationBancaireGetAll(Societe societe = null)
	{
		societe = societe ?? Societe;
		return _typeOperationBancaireRepository.GetAll(societe.No);
	}

	public void TypeOperationBancaireUpdate(int no, string intitule, string compteGeneral, int erpTaxeNo, string codeInterBanque)
	{
		if (string.IsNullOrEmpty(intitule))
		{
			throw new InvalidOperationException("Intitulé invalide!");
		}
		TypeOperationBancaire typeOperationBancaire = _typeOperationBancaireRepository.Get(no);
		if (typeOperationBancaire == null)
		{
			throw new InvalidOperationException("Type prévision n'est pas existe!");
		}
		typeOperationBancaire.Intitule = intitule;
		typeOperationBancaire.CompteGeneral = compteGeneral;
		typeOperationBancaire.ErpTaxeNo = erpTaxeNo;
		typeOperationBancaire.CodeInterBanque = codeInterBanque;
		_typeOperationBancaireRepository.Update(typeOperationBancaire);
	}
}
