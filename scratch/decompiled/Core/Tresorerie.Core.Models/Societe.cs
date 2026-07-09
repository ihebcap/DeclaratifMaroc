using System;
using System.Collections.Generic;
using System.Linq;
using Tresorerie.Core.Enum;
using Tresorerie.Ressources;

namespace Tresorerie.Core.Models;

public class Societe
{
	private readonly Func<IEnumerable<Caisse>> _caissesGetterDelegate;

	private readonly Func<IEnumerable<SocieteDevise>> _devisesGetterDelegate;

	private readonly Func<IEnumerable<SocieteModeReglement>> _modeReglementGetterDelegate;

	private readonly Func<IEnumerable<SocieteSouche>> _societeSoucheGetterDelegate;

	private readonly Func<IEnumerable<SocieteTypeBordereau>> _socTypeBordereauGetterDelegate;

	private readonly Func<IEnumerable<Utilisateur>> _utilisateurGetterDelegate;

	private readonly Func<IEnumerable<SocieteDesignationDocument>> _societeDesignationDocumentGetterDelegate;

	private readonly Func<IEnumerable<SocieteCodeActiviteTaxe>> _societeCodeActiviteGetterDelegate;

	private readonly Func<IEnumerable<SocieteCodeActiviteTiers>> _societeCodeActiviteTiersGetterDelegate;

	private IEnumerable<Caisse> _caisses;

	private IEnumerable<SocieteDevise> _devises;

	private Lazy<IEnumerable<Caisse>> _lazyCaisses;

	private Lazy<IEnumerable<SocieteDevise>> _lazyDevises;

	private Lazy<IEnumerable<SocieteModeReglement>> _lazyModeReglement;

	private Lazy<IEnumerable<SocieteSouche>> _lazySocieteSouche;

	private Lazy<IEnumerable<SocieteTypeBordereau>> _lazySocTypeBordoreau;

	private Lazy<IEnumerable<Utilisateur>> _lazyUtilisateurs;

	private Lazy<IEnumerable<SocieteDesignationDocument>> _lazySocieteDesignationDocument;

	private Lazy<IEnumerable<SocieteCodeActiviteTaxe>> _lazySocieteCodeActivite;

	private Lazy<IEnumerable<SocieteCodeActiviteTiers>> _lazySocieteCodeActiviteTiers;

	private IEnumerable<SocieteModeReglement> _modes;

	private IEnumerable<SocieteSouche> _societeSouche;

	private IEnumerable<SocieteTypeBordereau> _socTypeBordereaux;

	private IEnumerable<Utilisateur> _utilisateurs;

	private IEnumerable<SocieteDesignationDocument> _societeDesignationDocument;

	private IEnumerable<SocieteCodeActiviteTaxe> _societeCodeActivite;

	private IEnumerable<SocieteCodeActiviteTiers> _societeCodeActiviteTiers;

	public string Activite { get; set; }

	public string Adresse { get; set; }

	public bool IsAffaireClientRequired { get; set; }

	public bool IsAffaireFournisseurRequired { get; set; }

	public int AlimentationCaisseNumeroCount { get; set; }

	public string AlimentationCaissePrefix { get; set; }

	public bool AutoRapprochementTraitDecaisse { get; set; }

	public int BordereauNumeroCount { get; set; }

	public string BordereauPrefix { get; set; }

	public string Capital { get; set; }

	public int CautionNumeroCount { get; set; }

	public string CautionPrefix { get; set; }

	public bool CloturerEcritureComptable { get; set; }

	public string CodePostal { get; set; }

	public decimal CoefficientCalculEncoursAutoriseClient { get; set; }

	public string Complement { get; set; }

	public string CompteGeneralImpayeEncaissement { get; set; }

	public string CompteGeneralImpayeDecaissement { get; set; }

	public string CompteTimbre { get; set; }

	public string CompteTvaOpBancaire { get; set; }

	public string CompteVirInterne { get; set; }

	public TypeDateAjustement DateAjustement { get; set; }

	public DateTime DateDebutMajClt { get; set; }

	public DateTime DateDebutMajFrs { get; set; }

	public int? DefaultSoucheImpaye { get; set; }

	public int DefaultSoucheImpayeFrs { get; set; }

	public int DepenseNumeroCount { get; set; }

	public string DepensePrefix { get; set; }

	public int DeviseErpNo { get; set; }

	public int DossierFournisseurNumeroCount { get; set; }

	public int DossierClientNumeroCount { get; set; }

	public string DossierFournisseurPrefix { get; set; }

	public string DossierClientPrefix { get; set; }

	public string Email { get; set; }

	public bool EncoursBonLiv { get; set; }

	public bool EncoursTraite { get; set; }

	public ErpConnection ErpConnection { get; set; }

	public bool ErpRibBanque { get; set; }

	public string GainJournal { get; set; }

	public string GainCompteG { get; set; }

	public string PerteEchangeCompteGeneral { get; set; }

	public string PerteEchangeJournal { get; set; }

	public string GainEchangeCompteGeneral { get; set; }

	public string GainEchangeJournal { get; set; }

	public string PerteEchangeCompteGeneralFrs { get; set; }

	public string PerteEchangeJournalFrs { get; set; }

	public string GainEchangeCompteGeneralFrs { get; set; }

	public string GainEchangeJournalFrs { get; set; }

	public bool GenererNoteRappel { get; set; }

	public bool HasSoucheRestrictionClt { get; set; }

	public bool HasSoucheRestrictionFrs { get; set; }

	public string Identifiant { get; set; }

	public TypeIntegrationPrevisionnelleFournisseur InclueBcBlFaAchatPrevisionnel { get; set; }

	public TypeIntegrationPrevisionnelleClient InclueBcBlFaPrevisionnel { get; set; }

	public bool InclueEcheanceClientPrevue { get; set; }

	public bool InclueEcheanceFournisseurPrevue { get; set; }

	public bool IsAcceptAlimentationCaisse { get; set; }

	public bool IsAlimentationCaisseAnneeEnable { get; set; }

	public bool IsAlimentationCaisseMoisEnable { get; set; }

	public bool IsBordereauComptaAutomatique { get; set; }

	public bool IsBordereauNumAnneeEnable { get; set; }

	public bool IsBordereauNumMoisEnable { get; set; }

	public bool IsLettrageAutomatiqueBordereau { get; set; }

	public bool IsCalculAutomatiqueCoursPrevisionnel { get; set; }

	public bool IsCautionNumAnneeEnable { get; set; }

	public bool IsCautionNumMoisEnable { get; set; }

	public bool IsComptaAutomatiqueDossierReglementFrs { get; set; }

	public bool IsDepenseAnneeEnable { get; set; }

	public bool IsDepenseMoisEnable { get; set; }

	public bool IsDossierFournisseurNumAnneeEnable { get; set; }

	public bool IsDossierClientNumAnneeEnable { get; set; }

	public bool IsDossierFournisseurNumMoisEnable { get; set; }

	public bool IsDossierClientNumMoisEnable { get; set; }

	public bool IsImpayeComptaAutomatique { get; set; }

	public bool IsImportStatutConfirmeClt { get; set; }

	public bool IsImportStatutSaisieClt { get; set; }

	public bool IsImportStatutSaisieFrs { get; set; }

	public bool IsImportStatutAComptabiliseeClt { get; set; }

	public bool IsImportStatutAComptabiliseeFrs { get; set; }

	public bool IsImportStatutConfirmeFrs { get; set; }

	public bool IsLettrageAutomatiqueDossierReglementFrs { get; set; }

	public bool IsRappelNumAnneeEnable { get; set; }

	public bool IsRappelNumMoisEnable { get; set; }

	public bool IsReglementComptaAutomatique { get; set; }

	public bool IsReglementFrsNumAnneeEnable { get; set; }

	public bool IsReglementFrsNumMoisEnable { get; set; }

	public bool IsReglementNumAnneeEnable { get; set; }

	public bool IsReglementNumMoisEnable { get; set; }

	public bool IsRemboursementClientAnneeEnable { get; set; }

	public bool IsRemboursementClientMoisEnable { get; set; }

	public bool IsRetenueNumAnneeEnable { get; set; }

	public bool IsRetenueNumMoisEnable { get; set; }

	public bool IsTransfertNumAnneeEnable { get; set; }

	public bool IsTransfertNumMoisEnable { get; set; }

	public string JournalSituation { get; set; }

	public string LibelleAlimentation { get; set; }

	public string LibelleBordereau { get; set; }

	public string LibelleComptaReferenceReglement { get; set; }

	public string LibelleDepense { get; set; }

	public string LibelleImpaye { get; set; }

	public string LibelleImpayeFrs { get; set; }

	public string LibelleLigneVirementTiers { get; set; }

	public string LibelleReglement { get; set; }

	public string LibelleReglementFournisseur { get; set; }

	public string LibelleRemboursement { get; set; }

	public string LibelleRemboursementFournisseur { get; set; }

	public string LibelleTransfert { get; set; }

	public string LibelleVirementInterne { get; set; }

	public string LibelleVirementTiers { get; set; }

	public bool LigneVirementTiersAnnee { get; set; }

	public int LigneVirementTiersCount { get; set; }

	public bool LigneVirementTiersMois { get; set; }

	public string LigneVirementTiersPrefix { get; set; }

	public string LibelleComptaOperationBancaire { get; set; }

	public int MajBonLiv { get; set; }

	public decimal MaxGain { get; set; }

	public decimal MaxPerte { get; set; }

	public decimal MontantTimbre { get; set; }

	public NatureComptaImpaye NatureComptaImpayeEncaissement { get; set; }

	public NatureComptaImpaye NatureComptaImpayeDecaissement { get; set; }

	public int No { get; private set; }

	public int NombreMoisCalculEncoursAutoriseClient { get; set; }

	public string PerteCompteG { get; set; }

	public string PerteJournal { get; set; }

	public string PlageMaxTiers { get; set; }

	public string PlageMinTiers { get; set; }

	public string RaisonSociale { get; set; }

	public int RappelNumeroCount { get; set; }

	public string RappelPrefix { get; set; }

	public bool RecupererPieceFrs { get; set; }

	public int ReglementFrsNumeroCount { get; set; }

	public string ReglementFrsPrefix { get; set; }

	public int ReglementNumeroCount { get; set; }

	public string ReglementPrefix { get; set; }

	public bool ReleveClientBonLiv { get; set; }

	public bool ReleveClientDtLimiteBonLiv { get; set; }

	public int RemboursementClientNumeroCount { get; set; }

	public string RemboursementClientPrefix { get; set; }

	public string ReportANouveauJournal { get; set; }

	public int RetenueNumeroCount { get; set; }

	public string RetenuePrefix { get; set; }

	public string Site { get; set; }

	public bool SumReglementInfSumEcritureDossierFrs { get; set; }

	public string Telecopie { get; set; }

	public string Telephone { get; set; }

	public decimal TMM { get; set; }

	public int TransfertNumeroCount { get; set; }

	public string TransfertPrefix { get; set; }

	public TypeCalculEngagement TypeCalculEngagement { get; set; }

	public TypeComptabilisationReglementFrs TypeComptabilisationReglement { get; set; }

	public bool VerifRapprochementDecaisse { get; set; }

	public string Ville { get; set; }

	public int? VirementDefaultSouche { get; set; }

	public bool VirementInterneAnnee { get; set; }

	public int VirementInterneCount { get; set; }

	public bool VirementInterneMois { get; set; }

	public string VirementInternePrefix { get; set; }

	public bool VirementTiersAnnee { get; set; }

	public int VirementTiersCount { get; set; }

	public bool VirementTiersMois { get; set; }

	public string VirementTiersPrefix { get; set; }

	public bool Visibilite { get; set; }

	public bool UseObjetMetier { get; set; }

	public string FactureFournisseurPrefix { get; set; }

	public bool IsFactureFournisseurNumAnneeEnable { get; set; }

	public bool IsFactureFournisseurNumMoisEnable { get; set; }

	public int FactureFournisseurNumeroCount { get; set; }

	public string LibelleFacture { get; set; }

	public string LibelleGrcFactureGR { get; set; }

	public string LibelleGrfFactureGR { get; set; }

	public string ClotureCaissePrefix { get; set; }

	public bool IsClotureCaisseNumAnneeEnable { get; set; }

	public bool IsClotureCaisseNumMoisEnable { get; set; }

	public int ClotureCaisseNumeroCount { get; set; }

	public string LibelleClotureCaisse { get; set; }

	public bool VentilationAutomatique { get; set; }

	public bool DelaiPaiementClient { get; set; }

	public bool RestrictionTypeReglementClient { get; set; }

	public string CompteGeneralCommissionImpaye { get; set; }

	public string CompteGeneralCommissionImpayeFournisseur { get; set; }

	public string CompteGeneralInteretImpaye { get; set; }

	public string CompteGeneralInteretImpayeFournisseur { get; set; }

	public bool HasDossierImpRestriction { get; set; }

	public string DossierImpayePrefix { get; set; }

	public string DossierImpayeFournisseurPrefix { get; set; }

	public bool IsDossierImpayeNumAnneeEnable { get; set; }

	public bool IsDossierImpayeFournisseurNumAnneeEnable { get; set; }

	public bool IsDossierImpayeNumMoisEnable { get; set; }

	public bool IsDossierImpayeFournisseurNumMoisEnable { get; set; }

	public short DossierImpayeNumeroCount { get; set; }

	public short DossierImpayeFournisseurNumeroCount { get; set; }

	public string CommissionImpayeJournal { get; set; }

	public string CommissionImpayeFournisseurJournal { get; set; }

	public string InteretImpayeJournal { get; set; }

	public string InteretImpayeFournisseurJournal { get; set; }

	public decimal TauxInteret { get; set; }

	public decimal TauxInteretFournisseur { get; set; }

	public string FamilleCentralisatrice { get; set; }

	public bool BloquerRembourcementClient { get; set; }

	public bool ContinueComptaBordereauBeforeComptaImpaye { get; set; }

	public bool IsReportEcheanceAppliedToEcheanceOrigine { get; set; }

	public TypeVentilationAnalytique VentilationAnalytique { get; set; }

	public TypeCalculPrime TypeCalculPrime { get; set; }

	public TypeDeclarationTva TypeDeclarationTva { get; set; }

	public string DossierOperationBancaireNumPrefix { get; set; }

	public bool DossierOperationBancaireNumAnnee { get; set; }

	public bool DossierOperationBancaireNumMois { get; set; }

	public int DossierOperationBancaireNumCount { get; set; }

	public string DeclarationTvaNumPrefix { get; set; }

	public bool DeclarationTvaNumAnnee { get; set; }

	public bool DeclarationTvaNumMois { get; set; }

	public int DeclarationTvaNumCount { get; set; }

	public string DeclarationRetenuSourceNumPrefix { get; set; }

	public bool DeclarationRetenuSourceNumAnnee { get; set; }

	public bool DeclarationRetenuSourceNumMois { get; set; }

	public int DeclarationRetenuSourceNumCount { get; set; }

	public bool SynchroniserReglementClientErp { get; set; }

	public bool SynchroniserReglementFournisseurErp { get; set; }

	public bool SynchroniserRapprochementErp { get; set; }

	public string DeclarationTvaEncaissementErpColumnNameIceFournisseur { get; set; }

	public string DeclarationTvaEncaissementErpColumnNameIdentifiantFournisseur { get; set; }

	public string DeclarationTvaEncaissementErpColumnNameDesignationDocument { get; set; }

	public decimal DeclarationTvaEncaissementProrataAssujettissement { get; set; }

	public string DeclarationTvaEncaissementJournal { get; set; }

	public string DeclarationTvaEncaissementCompteDebiteur { get; set; }

	public string DeclarationTvaEncaissementCompteCrediteur { get; set; }

	public Legislation LegislationType { get; set; }

	public string ErpColumnNameNatureFournisseur { get; set; }

	public string ErpColumnValuePersonneMorale { get; set; }

	public string ErpColumnValuePersonnePhysique { get; set; }

	public string ErpColumnValueDateNaissanceFournisseur { get; set; }

	public bool ControlerIdentifiantFournisseur { get; set; }

	public int? FrequenceWorkerReglementClient { get; set; }

	public int? FrequenceWorkerReglementFournisseur { get; set; }

	public int? FrequenceWorkerRapprochement { get; set; }

	public int? FrequenceWorkerImportationFactureClient { get; set; }

	public int? FrequenceWorkerImportationFactureFournisseur { get; set; }

	public bool SynchroniserImportationFactureClientErp { get; set; }

	public bool SynchroniserImportationFactureFournisseurErp { get; set; }

	public bool SynchroniserBlocageClient { get; set; }

	public int? FrequenceWorkerBlocageClient { get; set; }

	public int? NombreImpayeClient { get; set; }

	public int? NombreJourFactureNonRegleClient { get; set; }

	public int? NombreFactureNonRegleClient { get; set; }

	public bool BloquerClientErpSuitDepassementEncours { get; set; }

	public bool UseDeblocageClient { get; set; }

	public bool MiseAjourEncoursClientErp { get; set; }

	public bool Rib { get; set; }

	public string Ice { get; set; }

	public CategorieSociete Categorie { get; set; }

	public ReglementFournisseurDefaultEcheance ReglementFournisseurDefaultEcheancePropose { get; set; }

	public bool ImportOnlyFactureValideClient { get; set; }

	public bool ImportOnlyFactureValideFournisseur { get; set; }

	public bool DeclarationRetenueUseRapprochementErp { get; set; }

	public string ErpColumnNameActiviteFournisseur { get; set; }

	public string ErpColumnNameMailFournisseur { get; set; }

	public decimal ToleranceTaxeGrc { get; set; }

	public decimal ToleranceTaxeGrf { get; set; }

	public ErpConnection ErpExternConnection { get; set; }

	public string TableNameTiersErpExternVente { get; set; }

	public string TableNameDocumentErpExternVente { get; set; }

	public string TableNameTiersErpExternAchat { get; set; }

	public string TableNameDocumentErpExternAchat { get; set; }

	public string ErpColumnNameCodeActiviteMarroc { get; set; }

	public int NombreJoursDelaisPaiementTiers { get; set; }

	public string TableNameReglementErpExternVente { get; set; }

	public string TableNameReglementErpExternAchat { get; set; }

	public bool SynchroniserReglementClientErpExtern { get; set; }

	public bool SynchroniserReglementFournisseurErpExtern { get; set; }

	public int? FrequenceWorkerReglementClientErpExtern { get; set; }

	public int? FrequenceWorkerReglementFournisseurErpExtern { get; set; }

	public bool SynchroniserDocumentClientErpExtern { get; set; }

	public bool SynchroniserDocumentFournisseurErpExtern { get; set; }

	public int? FrequenceWorkerDocumentClientErpExtern { get; set; }

	public int? FrequenceWorkerDocumentFournisseurErpExtern { get; set; }

	public bool SynchroniserTiersClientErpExtern { get; set; }

	public bool SynchroniserTiersFournisseurErpExtern { get; set; }

	public int? FrequenceWorkerTiersClientErpExtern { get; set; }

	public int? FrequenceWorkerTiersFournisseurErpExtern { get; set; }

	public string BordereauVirementPrefix { get; set; }

	public bool IsBordereauVirementNumAnneeEnable { get; set; }

	public bool IsBordereauVirementNumMoisEnable { get; set; }

	public int BordereauVirementNumeroCount { get; set; }

	public string DeclarationDelaisPaiementNumPrefix { get; set; }

	public bool IsDeclarationDelaisPaiementNumAnneeEnable { get; set; }

	public bool IsDeclarationDelaisPaiementNumMoisEnable { get; set; }

	public int DeclarationDelaisPaiementNumCount { get; set; }

	public TypeDeclarationDelaisPaiement DeclarationDelaisPaiementType { get; set; }

	public bool InterdictionModificationEcheanceCheque { get; set; }

	public TypeMailling TypeMailling { get; set; }

	public PrevisionnelStatutPiece StatutPieceClient { get; set; }

	public PrevisionnelStatutPiece StatutPieceFournisseur { get; set; }

	public DateTime? DateDebutPrevisionnelClient { get; set; }

	public DateTime? DateDebutPrevisionnelFournisseur { get; set; }

	public int DefaultSoucheFGRClient { get; set; }

	public int DefaultSoucheFGRFournisseur { get; set; }

	public string WebhookUrl { get; set; }

	public bool SynchroniserFGRClientErp { get; set; }

	public bool SynchroniserFGRFournisseurErp { get; set; }

	public int? FrequenceWorkerFGRClientErp { get; set; }

	public int? FrequenceWorkerFGRFournisseurErp { get; set; }

	public bool SynchroniserAffectationReglementClientErp { get; set; }

	public int? FrequenceWorkerAffectationReglementClientErp { get; set; }

	public bool UseOnlyOneReglementInDossier { get; set; }

	public bool DoNotComptaDossierIfDocumentNotCompta { get; set; }

	public bool EncoursBonLivFournisseur { get; set; }

	public string ButtonAnalyserFournisseurCaption { get; set; }

	public bool IsSynchroReglementComptabilisation { get; set; }

	public string CompteGeneralDroitTimbreClient { get; set; }

	public DateTime? DateDebutSyncReglementClient { get; set; }

	public DateTime? DateDebutSyncReglementFournisseur { get; set; }

	public string CreditPrefix { get; set; }

	public bool IsCreditNumAnneeEnable { get; set; }

	public bool IsCreditNumMoisEnable { get; set; }

	public int CreditNumeroCount { get; set; }

	public bool UseWorkFlowValidationEcheanceFournisseur { get; set; }

	public decimal MaxGainFrs { get; set; }

	public decimal MaxPerteFrs { get; set; }

	public TypeDateAjustement DateAjustementFrs { get; set; }

	public string GainJournalFrs { get; set; }

	public string GainCompteGFrs { get; set; }

	public string PerteJournalFrs { get; set; }

	public string PerteCompteGFrs { get; set; }

	public string ErpColumnValueNumRegistreCommerceFournisseur { get; set; }

	public decimal ChiffreAffaire { get; set; }

	public DateTime DateJugement { get; set; }

	public string ErpColumnValueNatureMarchandise { get; set; }

	public string ErpColumnValueDateLivraisonMarchandise { get; set; }

	public ActiviteDelaiPaiementMarroc ActiviteDelaiPaiementMarroc { get; set; }

	public string LibelleEcartChange { get; set; }

	public string LibelleEcartAjustement { get; set; }

	public Societe()
	{
		_caisses = new List<Caisse>();
		DateDebutMajClt = new DateTime(1900, 1, 1);
		DateDebutMajFrs = new DateTime(1900, 1, 1);
		DateJugement = new DateTime(1900, 1, 1);
	}

	public Societe(int no, string raisonSocial, ErpConnection erpConnection, Func<IEnumerable<Caisse>> caissesGetterDelegate, Func<IEnumerable<SocieteDevise>> societeDevisesGetterDelegate, Func<IEnumerable<SocieteModeReglement>> societeModeReglementGetterDelegate, Func<IEnumerable<SocieteTypeBordereau>> societeTypeBordereauGetterDelegate, Func<IEnumerable<SocieteSouche>> societeSoucheFuncGetterDelegate, Func<IEnumerable<Utilisateur>> societeUtilisateurGetterDelegate, Func<IEnumerable<SocieteDesignationDocument>> societeDesignationDocumentGetterDelegate, Func<IEnumerable<SocieteCodeActiviteTaxe>> societeCodeActiviteGetterDelegate)
		: this()
	{
		_caissesGetterDelegate = caissesGetterDelegate ?? throw new ArgumentNullException("caissesGetterDelegate");
		_devisesGetterDelegate = societeDevisesGetterDelegate ?? throw new ArgumentNullException("societeDevisesGetterDelegate");
		_modeReglementGetterDelegate = societeModeReglementGetterDelegate ?? throw new ArgumentNullException("societeModeReglementGetterDelegate");
		_socTypeBordereauGetterDelegate = societeTypeBordereauGetterDelegate ?? throw new ArgumentNullException("societeTypeBordereauGetterDelegate");
		_societeSoucheGetterDelegate = societeSoucheFuncGetterDelegate ?? throw new ArgumentNullException("societeSoucheFuncGetterDelegate");
		_utilisateurGetterDelegate = societeUtilisateurGetterDelegate ?? throw new ArgumentNullException("societeUtilisateurGetterDelegate");
		_societeDesignationDocumentGetterDelegate = societeDesignationDocumentGetterDelegate ?? throw new ArgumentNullException("societeDesignationDocumentGetterDelegate");
		_societeCodeActiviteGetterDelegate = societeCodeActiviteGetterDelegate ?? throw new ArgumentNullException("societeCodeActiviteGetterDelegate");
		No = no;
		RaisonSociale = raisonSocial ?? throw new ArgumentNullException("raisonSocial");
		ErpConnection = erpConnection;
	}

	public bool Contain(Caisse caisse)
	{
		if (caisse == null)
		{
			return false;
		}
		return GetCaisses().ToList().Contains(caisse);
	}

	public override bool Equals(object obj)
	{
		if (obj == null)
		{
			return false;
		}
		if (!(obj is Societe societe))
		{
			return false;
		}
		return societe.GetHashCode() == GetHashCode();
	}

	public IEnumerable<SocieteModeReglement> GetAllModes()
	{
		if (_lazyModeReglement == null && _modeReglementGetterDelegate != null)
		{
			_lazyModeReglement = new Lazy<IEnumerable<SocieteModeReglement>>(_modeReglementGetterDelegate);
		}
		if (_lazyModeReglement != null && !_lazyModeReglement.IsValueCreated)
		{
			_modes = _lazyModeReglement.Value.ToList();
		}
		return _modes;
	}

	public Caisse GetCaisse(int no)
	{
		return GetCaisses().SingleOrDefault((Caisse x) => x.No == no);
	}

	public Caisse GetCaisse(string code)
	{
		if (string.IsNullOrEmpty(code))
		{
			throw new ArgumentNullException("code");
		}
		string codeUp = code.ToUpper();
		return GetCaisses().SingleOrDefault((Caisse x) => x.Code.ToUpper().Equals(codeUp));
	}

	public IEnumerable<Caisse> GetCaisses()
	{
		if (_lazyCaisses == null && _caissesGetterDelegate != null)
		{
			_lazyCaisses = new Lazy<IEnumerable<Caisse>>(_caissesGetterDelegate);
		}
		if (_lazyCaisses != null && !_lazyCaisses.IsValueCreated)
		{
			_caisses = _lazyCaisses.Value.ToList();
		}
		return _caisses;
	}

	public SocieteDevise GetDevise(int deviseNo)
	{
		return GetSocieteDevises().SingleOrDefault((SocieteDevise x) => x.No == deviseNo);
	}

	public SocieteDevise GetDevise(string code)
	{
		return GetSocieteDevises().SingleOrDefault((SocieteDevise x) => x.Code == code);
	}

	public SocieteDevise GetDefaultDeviseSociete()
	{
		return GetDeviseErp(DeviseErpNo);
	}

	public SocieteDevise GetDeviseErp(int deviseErpNo)
	{
		return GetSocieteDevises().SingleOrDefault((SocieteDevise x) => x.ErpNo == deviseErpNo);
	}

	public int? GetDeviseErpNo(Devise devise)
	{
		if (devise == null)
		{
			throw new ArgumentNullException("devise");
		}
		return GetSocieteDevises().SingleOrDefault((SocieteDevise x) => x.No == devise.No)?.ErpNo;
	}

	public override int GetHashCode()
	{
		return (17 * 23 + No) * 23 + RaisonSociale.GetHashCode();
	}

	public SocieteModeReglement GetMode(int modeNo)
	{
		return GetAllModes().SingleOrDefault((SocieteModeReglement x) => x.No == modeNo);
	}

	public SocieteModeReglement GetMode(string code)
	{
		code = code.ToUpper();
		return GetAllModes().SingleOrDefault((SocieteModeReglement x) => x.Code.ToUpper().Equals(code));
	}

	public SocieteDesignationDocument GetDesignationDocument(string erpIntitule)
	{
		erpIntitule = erpIntitule.ToUpper();
		return GetSocieteDesignationDocument().SingleOrDefault((SocieteDesignationDocument x) => x.ErpIntitule.ToUpper().Equals(erpIntitule));
	}

	public SocieteDesignationDocument GetDesignationDocument(int designationDocumentNo)
	{
		return GetSocieteDesignationDocument().SingleOrDefault((SocieteDesignationDocument x) => x.DesignationDocumentNo == designationDocumentNo);
	}

	public SocieteCodeActiviteTiers GetCodeActiviteTiers(string erpIntitule)
	{
		erpIntitule = erpIntitule.ToUpper();
		return GetSocieteCodeActiviteTiers().SingleOrDefault((SocieteCodeActiviteTiers x) => x.ErpIntitule.ToUpper().Equals(erpIntitule));
	}

	public SocieteCodeActiviteTiers GetCodeActiviteTiers(int codeActiviteTiersNo)
	{
		return GetSocieteCodeActiviteTiers().SingleOrDefault((SocieteCodeActiviteTiers x) => x.CodeActiviteNo == codeActiviteTiersNo);
	}

	public SocieteModeReglement GetModeErp(int modeErpNo)
	{
		return GetAllModes().FirstOrDefault((SocieteModeReglement x) => x.ErpNo == modeErpNo);
	}

	public Caisse GetNewCaisse(string code)
	{
		if (GetCaisses().Any((Caisse x) => x.Code.ToUpper().Equals(code.ToUpper())))
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorCaisseCode);
		}
		return new Caisse(No, code);
	}

	public IEnumerable<SocieteDevise> GetSocieteDevises()
	{
		if (_lazyDevises == null && _devisesGetterDelegate != null)
		{
			_lazyDevises = new Lazy<IEnumerable<SocieteDevise>>(_devisesGetterDelegate);
		}
		if (_lazyDevises != null && !_lazyDevises.IsValueCreated)
		{
			_devises = _lazyDevises.Value.ToList();
		}
		return _devises;
	}

	public IEnumerable<SocieteTypeBordereau> GetSocTypeBordereaux()
	{
		if (_lazySocTypeBordoreau == null && _socTypeBordereauGetterDelegate != null)
		{
			_lazySocTypeBordoreau = new Lazy<IEnumerable<SocieteTypeBordereau>>(_socTypeBordereauGetterDelegate);
		}
		if (_lazySocTypeBordoreau != null && !_lazySocTypeBordoreau.IsValueCreated)
		{
			_socTypeBordereaux = _lazySocTypeBordoreau.Value.ToList();
		}
		return _socTypeBordereaux;
	}

	public IEnumerable<SocieteCodeActiviteTaxe> GetSocieteCodeActivite()
	{
		if (_lazySocieteCodeActivite == null && _societeCodeActiviteGetterDelegate != null)
		{
			_lazySocieteCodeActivite = new Lazy<IEnumerable<SocieteCodeActiviteTaxe>>(_societeCodeActiviteGetterDelegate);
		}
		if (_lazySocieteCodeActivite != null && !_lazySocieteCodeActivite.IsValueCreated)
		{
			_societeCodeActivite = _lazySocieteCodeActivite.Value.ToList();
		}
		return _societeCodeActivite;
	}

	public IEnumerable<SocieteCodeActiviteTiers> GetSocieteCodeActiviteTiers()
	{
		if (_lazySocieteCodeActiviteTiers == null && _societeCodeActiviteTiersGetterDelegate != null)
		{
			_lazySocieteCodeActiviteTiers = new Lazy<IEnumerable<SocieteCodeActiviteTiers>>(_societeCodeActiviteTiersGetterDelegate);
		}
		if (_lazySocieteCodeActiviteTiers != null && !_lazySocieteCodeActiviteTiers.IsValueCreated)
		{
			_societeCodeActiviteTiers = _lazySocieteCodeActiviteTiers.Value.ToList();
		}
		return _societeCodeActiviteTiers;
	}

	public IEnumerable<SocieteDesignationDocument> GetSocieteDesignationDocument()
	{
		if (_lazySocieteDesignationDocument == null && _societeDesignationDocumentGetterDelegate != null)
		{
			_lazySocieteDesignationDocument = new Lazy<IEnumerable<SocieteDesignationDocument>>(_societeDesignationDocumentGetterDelegate);
		}
		if (_lazySocieteDesignationDocument != null && !_lazySocieteDesignationDocument.IsValueCreated)
		{
			_societeDesignationDocument = _lazySocieteDesignationDocument.Value.ToList();
		}
		return _societeDesignationDocument;
	}

	public SocieteTypeBordereau GetSocTypeBordreau(int no)
	{
		return GetSocTypeBordereaux().SingleOrDefault((SocieteTypeBordereau x) => x.No == no);
	}

	public SocieteTypeBordereau GetSocTypeBordreau(string typeCode)
	{
		return GetSocTypeBordereaux().SingleOrDefault((SocieteTypeBordereau x) => x.Code == typeCode);
	}

	public IEnumerable<SocieteSouche> GetSouches(ErpDomaine domaine)
	{
		if (_lazySocieteSouche == null && _societeSoucheGetterDelegate != null)
		{
			_lazySocieteSouche = new Lazy<IEnumerable<SocieteSouche>>(_societeSoucheGetterDelegate);
		}
		if (_lazySocieteSouche != null && !_lazySocieteSouche.IsValueCreated)
		{
			_societeSouche = _lazySocieteSouche.Value.ToList();
		}
		return _societeSouche.Where((SocieteSouche x) => x.Domaine == domaine);
	}

	public Utilisateur GetUtilisateur(int no)
	{
		return GetUtilisateurs().SingleOrDefault((Utilisateur u) => u.No == no);
	}

	public Utilisateur GetUtilisateur(string login)
	{
		if (string.IsNullOrEmpty(login))
		{
			throw new ArgumentNullException("login");
		}
		return GetUtilisateurs().SingleOrDefault((Utilisateur u) => u.Login.ToUpper().Equals(login.ToUpper()));
	}

	public IEnumerable<Utilisateur> GetUtilisateurs()
	{
		if (_lazyUtilisateurs == null && _utilisateurGetterDelegate != null)
		{
			_lazyUtilisateurs = new Lazy<IEnumerable<Utilisateur>>(_utilisateurGetterDelegate);
		}
		if (_lazyUtilisateurs != null && !_lazyUtilisateurs.IsValueCreated)
		{
			_utilisateurs = _lazyUtilisateurs.Value.ToList();
		}
		return _utilisateurs;
	}

	public bool HasDevise(int no)
	{
		return GetDevise(no) != null;
	}

	public bool HasMode(int no)
	{
		return GetMode(no) != null;
	}

	public bool HasUtilisateur(string login)
	{
		return GetUtilisateur(login) != null;
	}

	public SocieteDevise InitNewDeviseSociete(Devise devise, int deviseErpNo)
	{
		if (devise == null)
		{
			throw new ArgumentNullException("devise");
		}
		return new SocieteDevise(No, devise, deviseErpNo);
	}

	public SocieteModeReglement InitNewMode(ModeReglement mode, int modeErpNo, bool isAvtive)
	{
		if (mode == null)
		{
			throw new ArgumentNullException("mode");
		}
		return new SocieteModeReglement(No, mode, modeErpNo, isAvtive);
	}

	public void Refresh()
	{
		RefreshCaisse();
		RefreshDevise();
		RefreshMode();
		RefreshSocTypeBordereaux();
		RefreshSocieteSouche();
		RefreshUtilisateur();
		RefreshSocCodeActivite();
		RefreshSocDesignationDocument();
		RefreshSocCodeActiviteTiers();
	}

	public void RefreshCaisse()
	{
		if (_caissesGetterDelegate != null)
		{
			_lazyCaisses = null;
			_lazyCaisses = new Lazy<IEnumerable<Caisse>>(_caissesGetterDelegate);
		}
	}

	public void RefreshDevise()
	{
		if (_devisesGetterDelegate != null)
		{
			_lazyDevises = null;
			_lazyDevises = new Lazy<IEnumerable<SocieteDevise>>(_devisesGetterDelegate);
		}
	}

	public void RefreshMode()
	{
		if (_modeReglementGetterDelegate != null)
		{
			_lazyModeReglement = null;
			_lazyModeReglement = new Lazy<IEnumerable<SocieteModeReglement>>(_modeReglementGetterDelegate);
		}
	}

	public void RefreshSocTypeBordereaux()
	{
		if (_socTypeBordereauGetterDelegate != null)
		{
			_lazySocTypeBordoreau = null;
			_lazySocTypeBordoreau = new Lazy<IEnumerable<SocieteTypeBordereau>>(_socTypeBordereauGetterDelegate);
		}
	}

	public void RefreshSocCodeActivite()
	{
		if (_societeCodeActiviteGetterDelegate != null)
		{
			_lazySocieteCodeActivite = null;
			_lazySocieteCodeActivite = new Lazy<IEnumerable<SocieteCodeActiviteTaxe>>(_societeCodeActiviteGetterDelegate);
		}
	}

	public void RefreshSocCodeActiviteTiers()
	{
		if (_societeCodeActiviteTiersGetterDelegate != null)
		{
			_lazySocieteCodeActiviteTiers = null;
			_lazySocieteCodeActiviteTiers = new Lazy<IEnumerable<SocieteCodeActiviteTiers>>(_societeCodeActiviteTiersGetterDelegate);
		}
	}

	public void RefreshSocDesignationDocument()
	{
		if (_societeDesignationDocumentGetterDelegate != null)
		{
			_lazySocieteDesignationDocument = null;
			_lazySocieteDesignationDocument = new Lazy<IEnumerable<SocieteDesignationDocument>>(_societeDesignationDocumentGetterDelegate);
		}
	}

	public void RefreshSocieteSouche()
	{
		if (_societeSoucheGetterDelegate != null)
		{
			_lazySocieteSouche = null;
			_lazySocieteSouche = new Lazy<IEnumerable<SocieteSouche>>(_societeSoucheGetterDelegate);
		}
	}

	public void RefreshUtilisateur()
	{
		if (_utilisateurGetterDelegate != null)
		{
			_lazyUtilisateurs = null;
			_lazyUtilisateurs = new Lazy<IEnumerable<Utilisateur>>(_utilisateurGetterDelegate);
		}
	}

	public override string ToString()
	{
		return string.Format(TresorerieCoreMessages.InfoSociete, Identifiant);
	}
}
