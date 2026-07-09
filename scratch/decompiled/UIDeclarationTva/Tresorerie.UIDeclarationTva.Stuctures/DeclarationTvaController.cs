using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SageBO.Core.Models;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;
using Tresorerie.Core.Services;
using Tresorerie.Erp.ICore;
using Tresorerie.Infrastructure;
using Tresorerie.UICommun.Helper;
using Tresorerie.UICommun.Helper.Views;
using Tresorerie.UICommun.LicenceGratuite;

namespace Tresorerie.UIDeclarationTva.Stuctures;

public class DeclarationTvaController
{
	private readonly ILicenceApplicationVersion _licenceApplicationVersion;

	private readonly IErpComptaService _erpComptaService;

	private readonly IErpCommService _erpCommService;

	private readonly IGroupeService _groupeService;

	private readonly DeviseViewHelper _deviseViewHelper;

	private readonly BanqueViewHelper _banqueHelper;

	private readonly FournisseurErpHelper _fournisseurHelper;

	private readonly ClientErpHelper _clientHelper;

	public DeclarationTvaController(ILicenceApplicationVersion licenceApplicationVersion, IErpComptaService erpComptaService, IErpCommService erpCommService, IGroupeService groupeService, DeviseViewHelper deviseViewHelper, FournisseurErpHelper fournisseurHelper, ClientErpHelper clientHelper, BanqueViewHelper banqueHelper)
	{
		_licenceApplicationVersion = licenceApplicationVersion ?? throw new ArgumentNullException("licenceApplicationVersion");
		_erpComptaService = erpComptaService ?? throw new ArgumentNullException("erpComptaService");
		_erpCommService = erpCommService ?? throw new ArgumentNullException("erpCommService");
		_groupeService = groupeService ?? throw new ArgumentNullException("groupeService");
		_deviseViewHelper = deviseViewHelper ?? throw new ArgumentNullException("deviseViewHelper");
		_fournisseurHelper = fournisseurHelper ?? throw new ArgumentNullException("fournisseurHelper");
		_clientHelper = clientHelper ?? throw new ArgumentNullException("clientHelper");
		_banqueHelper = banqueHelper ?? throw new ArgumentNullException("banqueHelper");
	}

	public LigneDeclarationTvaEncaissementView InitLigneDeclaration(DeclarationTvaEncaissementView view)
	{
		if (view == null)
		{
			throw new ArgumentNullException("view");
		}
		Societe societe = _groupeService.SocieteManager.Societe;
		if (string.IsNullOrEmpty(societe.DeclarationTvaEncaissementErpColumnNameIceFournisseur))
		{
			throw new ApplicationException("Veuillez sélectionner la colonne correspondant à l'ICE du tiers.");
		}
		return new LigneDeclarationTvaEncaissementView
		{
			DeclarationNo = view.No,
			Domaine = LigneDeclarationTvaEncaissementDomaine.Decaissement,
			Type = LigneDeclarationTvaEncaissementEntityType.Manuelle,
			Integration = LigneDeclarationTvaEncaissementIntegrationType.SaisieManuelle,
			DateMouvement = view.DateDebut,
			DateDocument = view.DateDebut,
			Prorata = societe.DeclarationTvaEncaissementProrataAssujettissement
		};
	}

	public List<CodeActivite> GetAllCodeActivite()
	{
		return _groupeService.CodeActiviteManager.GetAll();
	}

	public string GetCodeActivite(string erpTaxeCode)
	{
		SocieteCodeActiviteTaxe societeCodeActiviteTaxe = _groupeService.SocieteManager.CodeActiviteTaxeGet(erpTaxeCode);
		if (societeCodeActiviteTaxe == null)
		{
			return "";
		}
		CodeActivite codeActivite = _groupeService.CodeActiviteManager.Get(societeCodeActiviteTaxe.CodeActiviteNo);
		if (codeActivite == null)
		{
			return "";
		}
		return codeActivite.Code;
	}

	public IErpTiersIce GetIceTiers(string tiersCode, TiersType tiersType)
	{
		Societe societe = _groupeService.SocieteManager.Societe;
		if (string.IsNullOrEmpty(societe.DeclarationTvaEncaissementErpColumnNameIceFournisseur))
		{
			throw new ApplicationException("Veuillez sélectionner la colonne correspondant à l'ICE du tiers.");
		}
		if (string.IsNullOrEmpty(societe.DeclarationTvaEncaissementErpColumnNameIdentifiantFournisseur))
		{
			throw new ApplicationException("Veuillez sélectionner la colonne correspondant à l'identifiant du tiers.");
		}
		if (string.IsNullOrEmpty(societe.ErpColumnNameNatureFournisseur))
		{
			throw new ApplicationException("Veuillez sélectionner la colonne correspondant à la nature du tiers.");
		}
		if (string.IsNullOrEmpty(societe.ErpColumnNameCodeActiviteMarroc))
		{
			throw new ApplicationException("Veuillez sélectionner la colonne correspondant au code activité du tiers.");
		}
		return _erpCommService.GetAllIceTiersToMaroc(societe.DeclarationTvaEncaissementErpColumnNameIceFournisseur, societe.DeclarationTvaEncaissementErpColumnNameIdentifiantFournisseur, societe.ErpColumnNameNatureFournisseur, societe.ErpColumnNameCodeActiviteMarroc, societe.ErpColumnValueNumRegistreCommerceFournisseur).FirstOrDefault((IErpTiersIce x) => x.TiersCode == tiersCode && x.TiersType == tiersType);
	}

	public IList<IErpTaxe> GetAllErpTaxe()
	{
		return _erpCommService.GetAllTaxes().ToList();
	}

	public List<IErpFournisseur> GetAllFournisseur()
	{
		return (from x in _fournisseurHelper.GetAll(reload: true)
			where !x.EnSommeil
			select x).ToList();
	}

	public IEnumerable<ModeView> GetAllModeReglement()
	{
		return from mode in _groupeService.SocieteManager.Societe.GetAllModes()
			select new ModeView
			{
				No = mode.No,
				Type = mode.Type,
				Code = mode.Code,
				Designation = mode.Intitule
			};
	}

	public string GetDefaultDeviseFormat()
	{
		Societe societe = _groupeService.SocieteManager.Societe;
		return _deviseViewHelper.GetSocieteDevise(societe).Format;
	}

	public int GetNombreDecimalDefaultDevise()
	{
		Societe societe = _groupeService.SocieteManager.Societe;
		return _deviseViewHelper.GetSocieteDevise(societe).NombreDecimales;
	}

	public void IntergerLigne(DeclarationTvaEncaissementView declarationView, IEnumerable<DeclarationTvaView> views)
	{
		if (declarationView == null)
		{
			throw new ArgumentNullException("declarationView");
		}
		if (views == null)
		{
			throw new ArgumentNullException("views");
		}
		SocieteManager societeManager = _groupeService.SocieteManager;
		foreach (DeclarationTvaView view in views)
		{
			LigneDeclarationTvaEncaissementEntityType entityType = ((view.MouvementDomaine == MouvementDomaine.Depense) ? LigneDeclarationTvaEncaissementEntityType.Depense : ((view.MouvementDomaine != MouvementDomaine.OperationBancaire) ? LigneDeclarationTvaEncaissementEntityType.Affectation : LigneDeclarationTvaEncaissementEntityType.OperationBancaire));
			ReglementType typePayement = ((view.MouvementDomaine == MouvementDomaine.ReglementClient || view.MouvementDomaine == MouvementDomaine.ReglementFournisseur) ? view.MouvementTypeMode : ((view.MouvementDomaine != MouvementDomaine.Depense) ? ((view.MouvementDomaine == MouvementDomaine.OperationBancaire) ? ReglementType.Virement : ReglementType.Autre) : ReglementType.Espece));
			societeManager.DeclarationTvaEncaissementLigneAjouter(declarationView.No, view.AffectationNo, entityType, LigneDeclarationTvaEncaissementIntegrationType.Integration, view.DomaineDeclarationTva, view.AssietteDeclaration, view.TauxTva, view.MontantDeclaration, view.TiersCode, view.TiersIntitule, view.TiersIdentifiant, view.TiersIce, view.MouvementNumero, view.DocumentNumero, typePayement, view.MouvementDate, view.DocumentDate.Value, view.ErpTaxeCode, view.CodeActivite, view.Prorata, view.DesignationDocument);
		}
	}

	public void AjouterLigneDeclaration(LigneDeclarationTvaEncaissementView view)
	{
		if (view == null)
		{
			throw new ArgumentNullException("view");
		}
		_groupeService.SocieteManager.DeclarationTvaEncaissementLigneAjouter(view.DeclarationNo, null, view.Type, view.Integration, view.Domaine, view.Assiette, view.Taux, view.Montant, view.TiersCode, view.TiersIntitule, view.TiersIdentifiant, view.TiersIce, view.MouvementNumero, view.DocumentNumero, view.TypePayement, view.DateMouvement, view.DateDocument, view.ErpTaxeCode, view.CodeActivite, view.Prorata, view.DesignationDocument);
	}

	public List<DeclarationTvaView> GetDeclaration(DeclarationTvaEncaissementView view, DeclarationTvaSelectedEntityFiltreView filtreView)
	{
		if (view == null)
		{
			throw new ArgumentNullException("view");
		}
		if (filtreView == null)
		{
			throw new ArgumentNullException("filtreView");
		}
		SocieteManager societeManager = _groupeService.SocieteManager;
		CodeActiviteManager codeActiviteManager = _groupeService.CodeActiviteManager;
		Societe societe = societeManager.Societe;
		if (string.IsNullOrEmpty(societe.DeclarationTvaEncaissementErpColumnNameIceFournisseur))
		{
			throw new ApplicationException("Veuillez sélectionner la colonne correspondant à l'ICE du tiers.");
		}
		if (string.IsNullOrEmpty(societe.DeclarationTvaEncaissementErpColumnNameIdentifiantFournisseur))
		{
			throw new ApplicationException("Veuillez sélectionner la colonne correspondant à l'identifiant du tiers.");
		}
		if (string.IsNullOrEmpty(societe.ErpColumnNameNatureFournisseur))
		{
			throw new ApplicationException("Veuillez sélectionner la colonne correspondant à la nature du tiers.");
		}
		if (string.IsNullOrEmpty(societe.ErpColumnNameCodeActiviteMarroc))
		{
			throw new ApplicationException("Veuillez sélectionner la colonne correspondant au code activité du tiers.");
		}
		List<IErpTiersIce> erpTiersIceCollection = _erpCommService.GetAllIceTiersToMaroc(societe.DeclarationTvaEncaissementErpColumnNameIceFournisseur, societe.DeclarationTvaEncaissementErpColumnNameIdentifiantFournisseur, societe.ErpColumnNameNatureFournisseur, societe.ErpColumnNameCodeActiviteMarroc, societe.ErpColumnValueNumRegistreCommerceFournisseur).ToList();
		if (string.IsNullOrEmpty(societe.DeclarationTvaEncaissementErpColumnNameDesignationDocument))
		{
			throw new ApplicationException("Veuillez sélectionner la colonne correspondant à la désignation de la facture.");
		}
		IErpExercice erpExercice = (from x in _erpComptaService.GetAllExercice()
			where !x.IsCloture
			orderby x.Annee
			select x).FirstOrDefault();
		if (erpExercice == null)
		{
			throw new ApplicationException($"Impossible de charger l'exercice [{view.DateDebut.Year}].");
		}
		List<IErpFactureDesignation> erpFactureDesignationCollection = _erpCommService.GetAllDesignationFacture(societe.DeclarationTvaEncaissementErpColumnNameDesignationDocument, societe.ErpColumnValueNatureMarchandise, societe.ErpColumnValueDateLivraisonMarchandise).ToList();
		List<IErpComptaJournal> journalCollection = _erpComptaService.GetAllJournals().ToList();
		List<IErpTaxe> erpTaxeCollection = _erpCommService.GetAllTaxes().ToList();
		List<SocieteCodeActiviteTaxe> societeCodeActiviteTaxeCollection = societeManager.CodeActiviteTaxeGetAll();
		List<SocieteCodeActiviteTiers> societeCodeActiviteTiersCollection = societeManager.SocieteCodeActiviteTiersGetAll();
		List<CodeActivite> all = codeActiviteManager.GetAll();
		List<DesignationDocument> all2 = _groupeService.DesignationDocumentManager.GetAll();
		List<IErpComptaEcritureComptable> rapprochementComptaPeriodeCollection = _erpComptaService.GetEcrituresRapproche(erpExercice.Debut.Date, view.DateFin.Date.AddDays(1.0).AddSeconds(-1.0)).ToList();
		List<DeclarationTvaView> list = new List<DeclarationTvaView>();
		switch (filtreView.SelectedEntity)
		{
		case SelectedEntityFiltre.Decaissement:
			return GetDeclarationDecaissement(view, journalCollection, rapprochementComptaPeriodeCollection, erpTiersIceCollection, erpTaxeCollection, societeCodeActiviteTaxeCollection, all, erpFactureDesignationCollection, societeCodeActiviteTiersCollection, all2, erpExercice);
		case SelectedEntityFiltre.Encaissement:
			return GetDeclarationEncaissement(view, journalCollection, rapprochementComptaPeriodeCollection, erpTiersIceCollection, erpTaxeCollection, societeCodeActiviteTaxeCollection, all, erpFactureDesignationCollection, societeCodeActiviteTiersCollection, all2, erpExercice);
		case SelectedEntityFiltre.MouvementBancire:
			return GetDeclarationCommissionBancaire(view, journalCollection, rapprochementComptaPeriodeCollection, societeCodeActiviteTaxeCollection, all, erpExercice);
		case SelectedEntityFiltre.Tous:
		{
			List<DeclarationTvaView> declarationEncaissement = GetDeclarationEncaissement(view, journalCollection, rapprochementComptaPeriodeCollection, erpTiersIceCollection, erpTaxeCollection, societeCodeActiviteTaxeCollection, all, erpFactureDesignationCollection, societeCodeActiviteTiersCollection, all2, erpExercice);
			List<DeclarationTvaView> declarationDecaissement = GetDeclarationDecaissement(view, journalCollection, rapprochementComptaPeriodeCollection, erpTiersIceCollection, erpTaxeCollection, societeCodeActiviteTaxeCollection, all, erpFactureDesignationCollection, societeCodeActiviteTiersCollection, all2, erpExercice);
			List<DeclarationTvaView> declarationCommissionBancaire = GetDeclarationCommissionBancaire(view, journalCollection, rapprochementComptaPeriodeCollection, societeCodeActiviteTaxeCollection, all, erpExercice);
			list.AddRange(declarationEncaissement);
			list.AddRange(declarationDecaissement);
			list.AddRange(declarationCommissionBancaire);
			return list;
		}
		default:
			throw new NotImplementedException();
		}
	}

	private List<DeclarationTvaView> GetDeclarationEncaissement(DeclarationTvaEncaissementView declaration, List<IErpComptaJournal> journalCollection, List<IErpComptaEcritureComptable> rapprochementComptaPeriodeCollection, List<IErpTiersIce> erpTiersIceCollection, List<IErpTaxe> erpTaxeCollection, List<SocieteCodeActiviteTaxe> societeCodeActiviteTaxeCollection, List<CodeActivite> codeActiviteCollection, List<IErpFactureDesignation> erpFactureDesignationCollection, List<SocieteCodeActiviteTiers> societeCodeActiviteTiersCollection, List<DesignationDocument> designationDocumentCollection, IErpExercice erpExercice)
	{
		if (declaration == null)
		{
			throw new ArgumentNullException("declaration");
		}
		if (erpExercice == null)
		{
			throw new ApplicationException("erpExercice");
		}
		SocieteManager societeManager = _groupeService.SocieteManager;
		CaisseManager caisseManager = societeManager.CaisseManager;
		Societe societe = societeManager.Societe;
		IList<InformationsBanque> allBySociete = _groupeService.InformationBanqueManager.GetAllBySociete(societe);
		List<DeclarationTvaView> list = new List<DeclarationTvaView>();
		List<EcritureComptable> list2 = new List<EcritureComptable>();
		List<EcritureComptable> list3 = new List<EcritureComptable>();
		for (int i = erpExercice.Annee; i <= declaration.Exercice; i++)
		{
			list2.AddRange(caisseManager.GetEcrituresByExercice(i, MouvementDomaine.ReglementClient, societe));
			list3.AddRange(caisseManager.GetEcrituresByExercice(i, MouvementDomaine.EnteteBordereau, societe));
		}
		foreach (ReglementClient reglement in (from x in caisseManager.ReglementClientGetAllToDeclarationTvaEncaissement(societe.No, erpExercice.Debut.Date, declaration.DateFin.Date)
			where x.Solde != x.Montant
			select x).ToList())
		{
			List<Affectation> list4 = (from x in reglement.GetAffectations()
				where !x.DeclarationTvaEncaissementNo.HasValue
				select x).ToList();
			if (!list4.Any())
			{
				continue;
			}
			IErpComptaEcritureComptable erpComptaEcritureComptable = null;
			string mouvementBanqueAbregee = "";
			if (reglement.Type == ReglementType.Cheque || reglement.Type == ReglementType.Traite || reglement.Type == ReglementType.Virement)
			{
				if (((reglement.Type == ReglementType.Cheque || reglement.Type == ReglementType.Traite) && reglement.IsRemis != Remis.RemisBanque) || !reglement.BanqueNo.HasValue)
				{
					continue;
				}
				IErpCompteBanq banque = _banqueHelper.Get(reglement.BanqueNo.Value);
				if (banque == null)
				{
					continue;
				}
				InformationsBanque informationsBanque = allBySociete.FirstOrDefault((InformationsBanque x) => x.BanqueNo == banque.Id);
				if (informationsBanque == null)
				{
					continue;
				}
				IErpComptaJournal erpComptaJournal = journalCollection.FirstOrDefault((IErpComptaJournal x) => x.Code == banque.Journal);
				if (erpComptaJournal == null)
				{
					continue;
				}
				mouvementBanqueAbregee = banque.Abrege;
				string journal = banque.Journal;
				string compte = erpComptaJournal.CompteGeneral;
				int erpNo = 0;
				if (reglement.Type == ReglementType.Virement)
				{
					EcritureComptable ecritureComptable = list2.FirstOrDefault((EcritureComptable x) => x.MouvementNo == reglement.No && x.CompteGeneral == compte && x.CodeJournal == journal);
					if (ecritureComptable == null)
					{
						throw new ApplicationException("Impossible de charger l'écritures du règlement " + reglement.Numero + ".");
					}
					erpNo = ecritureComptable.ErpNo;
				}
				if (reglement.Type == ReglementType.Cheque || reglement.Type == ReglementType.Traite)
				{
					if (!reglement.EnteteBordereauNo.HasValue)
					{
						continue;
					}
					Bordereau bordereau = caisseManager.BordereauGet(reglement.EnteteBordereauNo.Value);
					if (bordereau == null)
					{
						throw new ApplicationException("Impossible de charger le bordereau " + reglement.EnteteBordereauNumero + ".");
					}
					if (!bordereau.IsComptabilise)
					{
						continue;
					}
					bool flag = informationsBanque.Bordereaux.Any((InformationsBanqueBordereau x) => x.TypeBordereauNo == bordereau.TypeNo);
					bool flag2 = informationsBanque.Bordereaux.Any((InformationsBanqueBordereau x) => x.TypeBordereauNo == bordereau.TypeNo && x.IsEcritureCumule);
					if ((informationsBanque.IsEcritureCumule && !flag) || flag2)
					{
						EcritureComptable ecritureComptable2 = list3.FirstOrDefault((EcritureComptable x) => x.MouvementNo == reglement.EnteteBordereauNo.Value && x.CompteGeneral == compte && x.CodeJournal == journal);
						if (ecritureComptable2 == null || ecritureComptable2.ErpNo == 0)
						{
							continue;
						}
						erpNo = ecritureComptable2.ErpNo;
					}
					else
					{
						EcritureComptable ecritureComptable3 = list3.FirstOrDefault((EcritureComptable x) => x.MouvementNo == reglement.No && x.CompteGeneral == compte && x.CodeJournal == journal);
						if (ecritureComptable3 == null || ecritureComptable3.ErpNo == 0)
						{
							continue;
						}
						erpNo = ecritureComptable3.ErpNo;
					}
				}
				erpComptaEcritureComptable = rapprochementComptaPeriodeCollection.FirstOrDefault((IErpComptaEcritureComptable x) => x.No == erpNo);
				if (erpComptaEcritureComptable == null)
				{
					continue;
				}
			}
			IErpClient client = _clientHelper.Get(reglement.ClientCode);
			if (client == null)
			{
				throw new ApplicationException("Impossible de charger le client du règlement " + reglement.ClientCode + ".");
			}
			IErpTiersIce iceClient = erpTiersIceCollection.FirstOrDefault((IErpTiersIce x) => x.TiersCode == client.Numero && x.TiersType == client.Type);
			if (iceClient == null)
			{
				throw new ApplicationException("Impossible de charger l'ICE du client " + client.Numero + ".");
			}
			foreach (Affectation item in list4)
			{
				if (item.DeclarationTvaEncaissementNo.HasValue)
				{
					continue;
				}
				Echeance echeance = item.GetEcheance();
				if (echeance.Type != EcheanceType.Erp && echeance.Type != EcheanceType.FactureGR)
				{
					continue;
				}
				switch (echeance.Type)
				{
				case EcheanceType.Erp:
				{
					if (echeance.DocumentType != ErpDocumentType.FactureClient && echeance.DocumentType != ErpDocumentType.FactureComptaClient)
					{
						break;
					}
					ErpDocument documentErp = _erpCommService.GetDocument(useOm: false, echeance.Domaine, ErpDocumentType.FactureClient, echeance.DocumentNumero) as ErpDocument;
					if (documentErp == null)
					{
						documentErp = _erpCommService.GetDocument(useOm: false, echeance.Domaine, ErpDocumentType.FactureComptaClient, echeance.DocumentNumero) as ErpDocument;
					}
					if (documentErp == null)
					{
						break;
					}
					IEnumerable<ErpLigneTaxe> enumerable2 = documentErp.Taxes.Where((ErpLigneTaxe x) => x.Type == ErpTypeTaxe.TvaDebit || x.Type == ErpTypeTaxe.TvaEncaissement);
					if (!enumerable2.Any())
					{
						break;
					}
					IErpFactureDesignation erpFactureDesignation = erpFactureDesignationCollection.FirstOrDefault((IErpFactureDesignation x) => x.DocumentType == documentErp.Type && x.DocumentNumero == echeance.DocumentNumero);
					if (erpFactureDesignation == null)
					{
						throw new ApplicationException("Impossible de charger la désignation de la facture " + echeance.DocumentNumero + ".");
					}
					foreach (ErpLigneTaxe ligneTaxe2 in enumerable2)
					{
						if (ligneTaxe2.TypeTaux != ErpTypeTauxTaxe.Taux)
						{
							continue;
						}
						decimal num3 = Math.Round(documentErp.TotalTouteTaxe / item.MontantDeviseSociete, 6);
						if (num3 <= 0m)
						{
							continue;
						}
						decimal num4 = ligneTaxe2.GetBase(documentErp) / num3;
						decimal montantDeclaration2 = Math.Round(num4 * (ligneTaxe2.Taux / 100m), societe.GetDefaultDeviseSociete().NombreDecimales);
						CodeActivite codeActivite2 = null;
						SocieteCodeActiviteTiers societeCodeActiviteTiers2 = societeCodeActiviteTiersCollection.FirstOrDefault((SocieteCodeActiviteTiers x) => x.ErpIntitule == iceClient.TiersCodeActiviteMarroc);
						if (societeCodeActiviteTiers2 != null)
						{
							codeActivite2 = codeActiviteCollection.SingleOrDefault((CodeActivite x) => x.No == societeCodeActiviteTiers2.CodeActiviteNo);
						}
						else
						{
							SocieteCodeActiviteTaxe societeCodeActiviteTaxe2 = societeCodeActiviteTaxeCollection.FirstOrDefault((SocieteCodeActiviteTaxe x) => x.ErpCodeTaxe == ligneTaxe2.Code);
							if (societeCodeActiviteTaxe2 != null)
							{
								codeActivite2 = codeActiviteCollection.SingleOrDefault((CodeActivite x) => x.No == societeCodeActiviteTaxe2.CodeActiviteNo);
							}
						}
						list.Add(new DeclarationTvaView
						{
							MouvementTypeMode = reglement.Type,
							AffectationNo = item.No,
							AffectationDate = item.Date,
							AffectationMontant = item.MontantDeviseSociete,
							DateDeclaration = DateTime.Now.Date,
							DateRapprochementComptable = erpComptaEcritureComptable?.DateRapprochement,
							DocumentDate = documentErp.Date,
							DocumentEcheance = echeance.Date,
							DocumentMontantDeviseSociete = documentErp.TotalTouteTaxe,
							DocumentNo = echeance.No,
							DocumentNumero = documentErp.Numero,
							MontantDeclaration = montantDeclaration2,
							MouvementDate = reglement.Date,
							MouvementEcheance = reglement.DateEcheance,
							MouvementMontantDeviseSociete = reglement.MontantDeviseSociete,
							MouvementNo = reglement.No,
							MouvementNumero = reglement.Numero,
							PieceTresorerieComptable = erpComptaEcritureComptable?.PieceTresorerie,
							SocieteNo = societe.No,
							IsDeclare = false,
							TauxTva = ligneTaxe2.Taux,
							AssietteDeclaration = num4,
							MouvementBanqueAbregee = mouvementBanqueAbregee,
							MouvementEnteteBordereauNumero = reglement.EnteteBordereauNumero,
							MouvementModeNo = reglement.ModeReglementNo,
							DomaineDeclarationTva = LigneDeclarationTvaEncaissementDomaine.Encaissement,
							MouvementDomaine = MouvementDomaine.ReglementClient,
							TiersCode = client.Numero,
							TiersIdentifiant = (iceClient.TiersIdentifiant ?? ""),
							TiersIntitule = client.Intitule,
							TiersIce = (iceClient.TiersIce ?? ""),
							ErpTaxeCode = ligneTaxe2.Code,
							CodeActivite = (codeActivite2?.Code ?? ""),
							Prorata = societe.DeclarationTvaEncaissementProrataAssujettissement,
							DesignationDocument = erpFactureDesignation.DocumentDesignation,
							Legislation = societe.LegislationType
						});
					}
					break;
				}
				case EcheanceType.FactureGR:
				{
					if (!echeance.IsComptabilise)
					{
						break;
					}
					List<Echeance> list5 = societeManager.EcheanceGetAllByDocument(echeance.Domaine, echeance.DocumentNumero).ToList();
					List<EcritureComptable> list6 = new List<EcritureComptable>();
					foreach (Echeance item2 in list5)
					{
						list6.AddRange(caisseManager.GetEcritures(item2.No, MouvementDomaine.EcritureTresorerie).ToList());
					}
					IEnumerable<EcritureComptable> enumerable = list6.Where((EcritureComptable x) => !string.IsNullOrEmpty(x.TaxeCode));
					if (!enumerable.Any())
					{
						break;
					}
					DesignationDocument designationDocument = designationDocumentCollection.SingleOrDefault((DesignationDocument x) => x.No == echeance.DesignationDocumentNo);
					if (designationDocument == null)
					{
						throw new ApplicationException("Impossible de charger la désignation de la facture " + echeance.DocumentNumero + ".");
					}
					foreach (EcritureComptable ligneTaxe in enumerable)
					{
						IErpTaxe taxe = erpTaxeCollection.SingleOrDefault((IErpTaxe x) => x.Code == ligneTaxe.TaxeCode);
						if (taxe == null || (taxe.TypeTaxe != ErpTypeTaxe.TvaDebit && taxe.TypeTaxe != ErpTypeTaxe.TvaEncaissement && taxe.TypeTaux != ErpTypeTauxTaxe.Taux))
						{
							continue;
						}
						decimal num = Math.Round(list5.Sum((Echeance x) => x.Montant) / item.Montant, 6);
						if (num <= 0m)
						{
							continue;
						}
						decimal num2 = ligneTaxe.Montant / num;
						decimal montantDeclaration = Math.Round(num2 * (taxe.Taux / 100m), societe.GetDefaultDeviseSociete().NombreDecimales);
						CodeActivite codeActivite = null;
						SocieteCodeActiviteTiers societeCodeActiviteTiers = societeCodeActiviteTiersCollection.FirstOrDefault((SocieteCodeActiviteTiers x) => x.ErpIntitule == iceClient.TiersCodeActiviteMarroc);
						if (societeCodeActiviteTiers != null)
						{
							codeActivite = codeActiviteCollection.SingleOrDefault((CodeActivite x) => x.No == societeCodeActiviteTiers.CodeActiviteNo);
						}
						else
						{
							SocieteCodeActiviteTaxe societeCodeActiviteTaxe = societeCodeActiviteTaxeCollection.FirstOrDefault((SocieteCodeActiviteTaxe x) => x.ErpCodeTaxe == taxe.Code);
							if (societeCodeActiviteTaxe != null)
							{
								codeActivite = codeActiviteCollection.SingleOrDefault((CodeActivite x) => x.No == societeCodeActiviteTaxe.CodeActiviteNo);
							}
						}
						list.Add(new DeclarationTvaView
						{
							MouvementTypeMode = reglement.Type,
							AffectationNo = item.No,
							AffectationDate = item.Date,
							AffectationMontant = item.MontantDeviseSociete,
							DateDeclaration = DateTime.Now.Date,
							DateRapprochementComptable = erpComptaEcritureComptable?.DateRapprochement,
							DocumentDate = echeance.Date,
							DocumentEcheance = echeance.Date,
							DocumentMontantDeviseSociete = echeance.MontantDeviseSociete,
							DocumentNo = echeance.No,
							DocumentNumero = echeance.DocumentNumero,
							MontantDeclaration = montantDeclaration,
							MouvementDate = reglement.Date,
							MouvementEcheance = reglement.DateEcheance,
							MouvementMontantDeviseSociete = reglement.MontantDeviseSociete,
							MouvementNo = reglement.No,
							MouvementNumero = reglement.Numero,
							PieceTresorerieComptable = erpComptaEcritureComptable?.PieceTresorerie,
							SocieteNo = societe.No,
							IsDeclare = false,
							TauxTva = taxe.Taux,
							AssietteDeclaration = num2,
							MouvementBanqueAbregee = mouvementBanqueAbregee,
							MouvementEnteteBordereauNumero = reglement.EnteteBordereauNumero,
							MouvementModeNo = reglement.ModeReglementNo,
							DomaineDeclarationTva = LigneDeclarationTvaEncaissementDomaine.Encaissement,
							MouvementDomaine = MouvementDomaine.ReglementClient,
							TiersCode = client.Numero,
							TiersIntitule = client.Intitule,
							TiersIdentifiant = (iceClient.TiersIdentifiant ?? ""),
							TiersIce = (iceClient.TiersIce ?? ""),
							ErpTaxeCode = taxe.Code,
							CodeActivite = (codeActivite?.Code ?? ""),
							Prorata = societe.DeclarationTvaEncaissementProrataAssujettissement,
							DesignationDocument = designationDocument.Intitule,
							Legislation = societe.LegislationType
						});
					}
					break;
				}
				}
			}
		}
		return list;
	}

	private List<DeclarationTvaView> GetDeclarationDecaissement(DeclarationTvaEncaissementView declaration, List<IErpComptaJournal> journalCollection, List<IErpComptaEcritureComptable> rapprochementComptaPeriodeCollection, List<IErpTiersIce> erpTiersIceCollection, List<IErpTaxe> erpTaxeCollection, List<SocieteCodeActiviteTaxe> societeCodeActiviteTaxeCollection, List<CodeActivite> codeActiviteCollection, List<IErpFactureDesignation> erpFactureDesignationCollection, List<SocieteCodeActiviteTiers> societeCodeActiviteTiersCollection, List<DesignationDocument> designationDocumentCollection, IErpExercice erpExercice)
	{
		if (declaration == null)
		{
			throw new ArgumentNullException("declaration");
		}
		if (erpExercice == null)
		{
			throw new ArgumentNullException("erpExercice");
		}
		SocieteManager societeManager = _groupeService.SocieteManager;
		CaisseManager caisseManager = societeManager.CaisseManager;
		DepenseManager depenseManager = _groupeService.DepenseManager;
		Societe societe = societeManager.Societe;
		List<DeclarationTvaView> list = new List<DeclarationTvaView>();
		List<EcritureComptable> list2 = new List<EcritureComptable>();
		List<EcritureComptable> list3 = new List<EcritureComptable>();
		for (int i = erpExercice.Annee; i <= declaration.Exercice; i++)
		{
			list2.AddRange(caisseManager.GetEcrituresByExercice(i, MouvementDomaine.ReglementFournisseur, societe));
			list3.AddRange(caisseManager.GetEcrituresByExercice(i, MouvementDomaine.Depense, societe));
		}
		List<MouvementDepense> list4 = depenseManager.GetAllMouvementDepenseToDeclarationTvaEncaissement(erpExercice.Debut.Date, declaration.DateFin.Date).ToList();
		foreach (ReglementFournisseur reglement in (from x in caisseManager.ReglementFournisseurGetAllToDeclarationTvaEncaissement(societe.No, erpExercice.Debut.Date, declaration.DateFin.Date)
			where x.Solde != x.Montant
			select x).ToList())
		{
			if (reglement.Type == ReglementType.Espece && reglement.Date.Date < declaration.DateDebut.Date)
			{
				continue;
			}
			List<Affectation> list5 = (from x in reglement.GetAffectations()
				where !x.DeclarationTvaEncaissementNo.HasValue
				select x).ToList();
			if (!list5.Any())
			{
				continue;
			}
			IErpComptaEcritureComptable erpComptaEcritureComptable = null;
			string mouvementBanqueAbregee = "";
			if (reglement.Type == ReglementType.Cheque || reglement.Type == ReglementType.Traite || reglement.Type == ReglementType.Virement)
			{
				if (!reglement.BanqueNo.HasValue)
				{
					continue;
				}
				IErpCompteBanq banque = _banqueHelper.Get(reglement.BanqueNo.Value);
				if (banque == null)
				{
					continue;
				}
				IErpComptaJournal erpComptaJournal = journalCollection.FirstOrDefault((IErpComptaJournal x) => x.Code == banque.Journal);
				if (erpComptaJournal == null)
				{
					throw new ApplicationException("Impossible de charger le journal de la banque " + banque.Abrege + ".");
				}
				if (erpComptaJournal.Rapprochement != ErpComptaTypeRapprochement.Tresorerie)
				{
					continue;
				}
				mouvementBanqueAbregee = banque.Abrege;
				string journal = banque.Journal;
				string compte = erpComptaJournal.CompteGeneral;
				EcritureComptable ecritureReglementBanque = list2.FirstOrDefault((EcritureComptable x) => x.MouvementNo == reglement.No && x.CompteGeneral == compte && x.CodeJournal == journal);
				if (ecritureReglementBanque == null)
				{
					continue;
				}
				erpComptaEcritureComptable = rapprochementComptaPeriodeCollection.FirstOrDefault((IErpComptaEcritureComptable x) => x.No == ecritureReglementBanque.ErpNo);
				if (erpComptaEcritureComptable == null)
				{
					continue;
				}
			}
			IErpFournisseur fournisseur = _fournisseurHelper.Get(reglement.FournisseurCode);
			if (fournisseur == null)
			{
				throw new ApplicationException("Impossible de charger le fournisseur du règlement " + reglement.Numero + ".");
			}
			IErpTiersIce iceFournisseur = erpTiersIceCollection.FirstOrDefault((IErpTiersIce x) => x.TiersCode == fournisseur.Numero && x.TiersType == fournisseur.Type);
			if (iceFournisseur == null)
			{
				throw new ApplicationException("Impossible de charger l'ICE du fournisseur " + fournisseur.Numero + ".");
			}
			foreach (Affectation item in list5)
			{
				if (item.DeclarationTvaEncaissementNo.HasValue)
				{
					continue;
				}
				Echeance echeance = item.GetEcheance();
				if (echeance.Type != EcheanceType.Erp && echeance.Type != EcheanceType.FactureGR)
				{
					continue;
				}
				switch (echeance.Type)
				{
				case EcheanceType.Erp:
				{
					if (echeance.DocumentType != ErpDocumentType.FactureFournisseur && echeance.DocumentType != ErpDocumentType.FactureComptaFournisseur)
					{
						break;
					}
					ErpDocument documentErp = _erpCommService.GetDocument(useOm: false, echeance.Domaine, ErpDocumentType.FactureFournisseur, echeance.DocumentNumero) as ErpDocument;
					if (documentErp == null)
					{
						documentErp = _erpCommService.GetDocument(useOm: false, echeance.Domaine, ErpDocumentType.FactureComptaFournisseur, echeance.DocumentNumero) as ErpDocument;
					}
					if (documentErp == null)
					{
						break;
					}
					IEnumerable<ErpLigneTaxe> enumerable2 = documentErp.Taxes.Where((ErpLigneTaxe x) => x.Type == ErpTypeTaxe.TvaDebit || x.Type == ErpTypeTaxe.TvaEncaissement);
					if (!enumerable2.Any())
					{
						break;
					}
					IErpFactureDesignation erpFactureDesignation = erpFactureDesignationCollection.FirstOrDefault((IErpFactureDesignation x) => x.DocumentType == documentErp.Type && x.DocumentNumero == echeance.DocumentNumero);
					if (erpFactureDesignation == null)
					{
						throw new ApplicationException("Impossible de charger la désignation de la facture " + echeance.DocumentNumero + ".");
					}
					foreach (ErpLigneTaxe ligneTaxe2 in enumerable2)
					{
						if (ligneTaxe2.TypeTaux != ErpTypeTauxTaxe.Taux)
						{
							continue;
						}
						decimal num4 = Math.Round(documentErp.TotalTouteTaxe / item.Montant, 6);
						if (num4 <= 0m)
						{
							continue;
						}
						decimal num5 = ligneTaxe2.GetBase(documentErp) / num4;
						decimal num6 = Math.Round(num5 * (ligneTaxe2.Taux / 100m), societe.GetDefaultDeviseSociete().NombreDecimales);
						if (num5 == 0m)
						{
							continue;
						}
						CodeActivite codeActivite2 = null;
						SocieteCodeActiviteTiers societeCodeActiviteTiers2 = societeCodeActiviteTiersCollection.FirstOrDefault((SocieteCodeActiviteTiers x) => x.ErpIntitule == iceFournisseur.TiersCodeActiviteMarroc);
						if (societeCodeActiviteTiers2 != null)
						{
							codeActivite2 = codeActiviteCollection.SingleOrDefault((CodeActivite x) => x.No == societeCodeActiviteTiers2.CodeActiviteNo);
						}
						else
						{
							SocieteCodeActiviteTaxe societeCodeActiviteTaxe2 = societeCodeActiviteTaxeCollection.FirstOrDefault((SocieteCodeActiviteTaxe x) => x.ErpCodeTaxe == ligneTaxe2.Code);
							if (societeCodeActiviteTaxe2 != null)
							{
								codeActivite2 = codeActiviteCollection.SingleOrDefault((CodeActivite x) => x.No == societeCodeActiviteTaxe2.CodeActiviteNo);
							}
						}
						list.Add(new DeclarationTvaView
						{
							MouvementTypeMode = reglement.Type,
							AffectationNo = item.No,
							AffectationDate = item.Date,
							AffectationMontant = item.MontantDeviseSociete,
							DateDeclaration = DateTime.Now.Date,
							DateRapprochementComptable = erpComptaEcritureComptable?.DateRapprochement,
							DocumentDate = documentErp.Date,
							DocumentEcheance = echeance.Date,
							DocumentMontantDeviseSociete = documentErp.TotalTouteTaxe,
							DocumentNo = echeance.No,
							DocumentNumero = documentErp.Numero,
							MontantDeclaration = num6 * -1m,
							MouvementDate = reglement.Date,
							MouvementEcheance = reglement.DateEcheance,
							MouvementMontantDeviseSociete = reglement.MontantDeviseSociete,
							MouvementNo = reglement.No,
							MouvementNumero = reglement.Numero,
							PieceTresorerieComptable = erpComptaEcritureComptable?.PieceTresorerie,
							SocieteNo = societe.No,
							IsDeclare = false,
							TauxTva = ligneTaxe2.Taux,
							AssietteDeclaration = num5,
							MouvementBanqueAbregee = mouvementBanqueAbregee,
							MouvementModeNo = reglement.ModeReglementNo,
							DomaineDeclarationTva = LigneDeclarationTvaEncaissementDomaine.Decaissement,
							MouvementDomaine = MouvementDomaine.ReglementFournisseur,
							TiersCode = fournisseur.Numero,
							TiersIntitule = fournisseur.Intitule,
							TiersIdentifiant = iceFournisseur.TiersIdentifiant,
							TiersIce = iceFournisseur.TiersIce,
							ErpTaxeCode = ligneTaxe2.Code,
							CodeActivite = (codeActivite2?.Code ?? ""),
							Prorata = societe.DeclarationTvaEncaissementProrataAssujettissement,
							DesignationDocument = erpFactureDesignation.DocumentDesignation,
							Legislation = societe.LegislationType
						});
					}
					break;
				}
				case EcheanceType.FactureGR:
				{
					if (!echeance.IsComptabilise)
					{
						break;
					}
					List<Echeance> list6 = societeManager.EcheanceGetAllByDocument(echeance.Domaine, echeance.DocumentNumero).ToList();
					List<EcritureComptable> list7 = new List<EcritureComptable>();
					foreach (Echeance item2 in list6)
					{
						list7.AddRange(caisseManager.GetEcritures(item2.No, MouvementDomaine.EcritureTresorerie).ToList());
					}
					IEnumerable<EcritureComptable> enumerable = list7.Where((EcritureComptable x) => !string.IsNullOrEmpty(x.TaxeCode));
					if (!enumerable.Any())
					{
						break;
					}
					DesignationDocument designationDocument = designationDocumentCollection.SingleOrDefault((DesignationDocument x) => x.No == echeance.DesignationDocumentNo);
					if (designationDocument == null)
					{
						throw new ApplicationException("Impossible de charger la désignation de la facture " + echeance.DocumentNumero + ".");
					}
					foreach (EcritureComptable ligneTaxe in enumerable)
					{
						IErpTaxe taxe = erpTaxeCollection.SingleOrDefault((IErpTaxe x) => x.Code == ligneTaxe.TaxeCode);
						if (taxe == null || (taxe.TypeTaxe != ErpTypeTaxe.TvaDebit && taxe.TypeTaxe != ErpTypeTaxe.TvaEncaissement && taxe.TypeTaux != ErpTypeTauxTaxe.Taux))
						{
							continue;
						}
						decimal num = Math.Round(list6.Sum((Echeance x) => x.Montant) / item.Montant, 6);
						if (num <= 0m)
						{
							continue;
						}
						decimal num2 = ligneTaxe.Montant / num;
						decimal num3 = Math.Round(num2 * (taxe.Taux / 100m), societe.GetDefaultDeviseSociete().NombreDecimales);
						CodeActivite codeActivite = null;
						SocieteCodeActiviteTiers societeCodeActiviteTiers = societeCodeActiviteTiersCollection.FirstOrDefault((SocieteCodeActiviteTiers x) => x.ErpIntitule == iceFournisseur.TiersCodeActiviteMarroc);
						if (societeCodeActiviteTiers != null)
						{
							codeActivite = codeActiviteCollection.SingleOrDefault((CodeActivite x) => x.No == societeCodeActiviteTiers.CodeActiviteNo);
						}
						else
						{
							SocieteCodeActiviteTaxe societeCodeActiviteTaxe = societeCodeActiviteTaxeCollection.FirstOrDefault((SocieteCodeActiviteTaxe x) => x.ErpCodeTaxe == taxe.Code);
							if (societeCodeActiviteTaxe != null)
							{
								codeActivite = codeActiviteCollection.SingleOrDefault((CodeActivite x) => x.No == societeCodeActiviteTaxe.CodeActiviteNo);
							}
						}
						list.Add(new DeclarationTvaView
						{
							MouvementTypeMode = reglement.Type,
							AffectationNo = item.No,
							AffectationDate = item.Date,
							AffectationMontant = item.MontantDeviseSociete,
							DateDeclaration = DateTime.Now.Date,
							DateRapprochementComptable = erpComptaEcritureComptable?.DateRapprochement,
							DocumentDate = echeance.Date,
							DocumentEcheance = echeance.Date,
							DocumentMontantDeviseSociete = list6.Sum((Echeance x) => x.Montant),
							DocumentNo = echeance.No,
							DocumentNumero = echeance.DocumentNumero,
							MontantDeclaration = num3 * -1m,
							MouvementDate = reglement.Date,
							MouvementEcheance = reglement.DateEcheance,
							MouvementMontantDeviseSociete = reglement.MontantDeviseSociete,
							MouvementNo = reglement.No,
							MouvementNumero = reglement.Numero,
							PieceTresorerieComptable = erpComptaEcritureComptable?.PieceTresorerie,
							SocieteNo = societe.No,
							IsDeclare = false,
							TauxTva = taxe.Taux,
							AssietteDeclaration = num2,
							MouvementBanqueAbregee = mouvementBanqueAbregee,
							MouvementModeNo = reglement.ModeReglementNo,
							DomaineDeclarationTva = LigneDeclarationTvaEncaissementDomaine.Decaissement,
							MouvementDomaine = MouvementDomaine.ReglementFournisseur,
							TiersCode = fournisseur.Numero,
							TiersIntitule = fournisseur.Intitule,
							TiersIdentifiant = iceFournisseur.TiersIdentifiant,
							TiersIce = iceFournisseur.TiersIce,
							ErpTaxeCode = taxe.Code,
							CodeActivite = (codeActivite?.Code ?? ""),
							Prorata = societe.DeclarationTvaEncaissementProrataAssujettissement,
							DesignationDocument = designationDocument.Intitule,
							Legislation = societe.LegislationType
						});
					}
					break;
				}
				}
			}
		}
		foreach (MouvementDepense depense in list4)
		{
			if (depense.DeclarationTvaEncaissementNo.HasValue)
			{
				continue;
			}
			IErpTaxe taxe2 = erpTaxeCollection.FirstOrDefault((IErpTaxe x) => x.No == depense.ErpTaxeNo);
			if (taxe2 == null)
			{
				continue;
			}
			SocieteCodeActiviteTaxe societeCodeActiviteTaxe3 = societeCodeActiviteTaxeCollection.SingleOrDefault((SocieteCodeActiviteTaxe x) => x.ErpCodeTaxe == taxe2.Code);
			if (societeCodeActiviteTaxe3 != null)
			{
				CodeActivite codeActivite3 = codeActiviteCollection.SingleOrDefault((CodeActivite x) => x.No == societeCodeActiviteTaxe3.CodeActiviteNo);
				if (codeActivite3 != null)
				{
					list.Add(new DeclarationTvaView
					{
						DateDeclaration = DateTime.Now.Date,
						MontantDeclaration = depense.MontantTva * -1m,
						MouvementDate = depense.Date,
						MouvementEcheance = depense.Date,
						MouvementMontantDeviseSociete = depense.MontantDeviseSociete,
						MouvementNo = depense.No,
						MouvementNumero = depense.Numero,
						DocumentNumero = depense.PieceNumero,
						DocumentDate = depense.Date,
						MouvementModeNo = depense.ModeNo,
						MouvementTypeMode = ReglementType.Espece,
						SocieteNo = societe.No,
						IsDeclare = false,
						TauxTva = depense.TauxTva,
						AssietteDeclaration = depense.MontantDeviseSociete,
						DomaineDeclarationTva = LigneDeclarationTvaEncaissementDomaine.Decaissement,
						MouvementDomaine = MouvementDomaine.Depense,
						AffectationNo = depense.No,
						TiersCode = depense.RaisonSociale,
						TiersIntitule = depense.RaisonSociale,
						TiersIdentifiant = depense.Identifiant,
						TiersIce = depense.Ice,
						ErpTaxeCode = taxe2.Code,
						CodeActivite = codeActivite3.Code,
						Prorata = societe.DeclarationTvaEncaissementProrataAssujettissement,
						DesignationDocument = depense.Libelle,
						Legislation = societe.LegislationType
					});
				}
			}
		}
		return list;
	}

	private List<DeclarationTvaView> GetDeclarationCommissionBancaire(DeclarationTvaEncaissementView declaration, List<IErpComptaJournal> journalCollection, List<IErpComptaEcritureComptable> rapprochementComptaPeriodeCollection, List<SocieteCodeActiviteTaxe> societeCodeActiviteTaxeCollection, List<CodeActivite> codeActiviteCollection, IErpExercice erpExercice)
	{
		if (declaration == null)
		{
			throw new ArgumentNullException("declaration");
		}
		if (erpExercice == null)
		{
			throw new ArgumentNullException("erpExercice");
		}
		SocieteManager societeManager = _groupeService.SocieteManager;
		Societe societe = societeManager.Societe;
		PrevisionnelManager previsionnelManager = _groupeService.PrevisionnelManager;
		InformationBanqueManager informationBanqueManager = _groupeService.InformationBanqueManager;
		CaisseManager caisseManager = societeManager.CaisseManager;
		List<IErpTaxe> source = _erpCommService.GetAllTaxes().ToList();
		IList<InformationsBanque> allBySociete = informationBanqueManager.GetAllBySociete(societe);
		List<DeclarationTvaView> list = new List<DeclarationTvaView>();
		IEnumerable<OperationBancaire> allOperationBancaireToDeclaration = previsionnelManager.GetAllOperationBancaireToDeclaration(erpExercice.Debut.Date, declaration.DateFin.Date, default(CancellationToken));
		List<EcritureComptable> list2 = new List<EcritureComptable>();
		for (int i = erpExercice.Annee; i <= declaration.Exercice; i++)
		{
			list2.AddRange(caisseManager.GetEcrituresByExercice(i, MouvementDomaine.OperationBancaire, societe));
		}
		foreach (OperationBancaire item in allOperationBancaireToDeclaration)
		{
			if (item.MontantTva == 0m)
			{
				continue;
			}
			TypeOperationBancaire typeOperation = previsionnelManager.TypeOperationBancaireGet(item.TypeNo);
			if (typeOperation == null)
			{
				continue;
			}
			IErpTaxe taxe = source.SingleOrDefault((IErpTaxe x) => x.No == typeOperation.ErpTaxeNo);
			if (taxe == null)
			{
				continue;
			}
			SocieteCodeActiviteTaxe societeCodeActiviteTaxe = societeCodeActiviteTaxeCollection.SingleOrDefault((SocieteCodeActiviteTaxe x) => x.ErpCodeTaxe == taxe.Code);
			if (societeCodeActiviteTaxe == null)
			{
				continue;
			}
			CodeActivite codeActivite = codeActiviteCollection.SingleOrDefault((CodeActivite x) => x.No == societeCodeActiviteTaxe.CodeActiviteNo);
			IErpCompteBanq banque = _banqueHelper.Get(item.BanqueNo);
			if (banque != null)
			{
				InformationsBanque informationsBanque = allBySociete.SingleOrDefault((InformationsBanque x) => x.BanqueNo == banque.Id);
				if (informationsBanque != null)
				{
					list.Add(new DeclarationTvaView
					{
						MouvementBanqueAbregee = banque.Abrege,
						DateDeclaration = DateTime.Now.Date,
						MontantDeclaration = item.MontantTva * (decimal)((typeOperation.Sens == SensPrevisionnelle.Encaissement) ? 1 : (-1)),
						MouvementDate = item.Date,
						MouvementEcheance = item.Date,
						MouvementMontantDeviseSociete = item.Montant,
						MouvementNo = item.No,
						MouvementNumero = item.Piece,
						SocieteNo = societe.No,
						IsDeclare = false,
						TauxTva = taxe.Taux,
						AssietteDeclaration = item.Montant,
						DomaineDeclarationTva = ((typeOperation.Sens == SensPrevisionnelle.Encaissement) ? LigneDeclarationTvaEncaissementDomaine.Encaissement : LigneDeclarationTvaEncaissementDomaine.Decaissement),
						MouvementDomaine = MouvementDomaine.OperationBancaire,
						AffectationNo = item.No,
						TiersCode = societe.RaisonSociale,
						TiersIntitule = societe.RaisonSociale,
						TiersIdentifiant = informationsBanque.BanqueIdentifiant,
						TiersIce = informationsBanque.BanqueIce,
						ErpTaxeCode = taxe.Code,
						CodeActivite = (codeActivite?.Code ?? ""),
						Prorata = societe.DeclarationTvaEncaissementProrataAssujettissement,
						DocumentDate = item.Date,
						DocumentNumero = item.Piece,
						DesignationDocument = item.Libelle,
						Legislation = societe.LegislationType
					});
				}
			}
		}
		return list;
	}

	public List<DeclarationTvaRegroupementView> GetTotalDeclaration(List<DeclarationTvaView> collection)
	{
		SocieteManager societeManager = _groupeService.SocieteManager;
		Societe societe = societeManager.Societe;
		return (from x in collection
			group x by new { x.DomaineDeclarationTva, x.ErpTaxeCode, x.TauxTva } into x
			select new DeclarationTvaRegroupementView
			{
				SocieteNo = societe.No,
				Taux = x.Key.TauxTva,
				ErpTaxeCode = x.Key.ErpTaxeCode,
				Domaine = x.Key.DomaineDeclarationTva,
				TotalAssiette = collection.Where((DeclarationTvaView t) => t.DomaineDeclarationTva == x.Key.DomaineDeclarationTva && t.ErpTaxeCode == x.Key.ErpTaxeCode && t.TauxTva == x.Key.TauxTva).Sum((DeclarationTvaView t) => t.AssietteDeclaration),
				TotalTva = collection.Where((DeclarationTvaView t) => t.DomaineDeclarationTva == x.Key.DomaineDeclarationTva && t.ErpTaxeCode == x.Key.ErpTaxeCode && t.TauxTva == x.Key.TauxTva).Sum((DeclarationTvaView t) => t.MontantDeclaration)
			}).ToList();
	}

	public List<IErpExercice> GetAllExercice()
	{
		return (from x in _erpComptaService.GetAllExercice()
			where !x.IsCloture
			select x).ToList();
	}

	public void AuthorisationExport()
	{
	}

	public bool IsLicenceGratuit()
	{
		try
		{
			_licenceApplicationVersion.ThrowIfGratuit();
			return true;
		}
		catch (Exception ex)
		{
			new FrmMessageBoxLicenceGratuite(ex.Message).ShowDialog();
			return false;
		}
	}
}
