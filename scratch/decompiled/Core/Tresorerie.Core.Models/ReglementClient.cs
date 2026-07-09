using System;
using System.Collections.Generic;
using System.Linq;
using Tresorerie.Core.Enum;
using Tresorerie.Ressources;

namespace Tresorerie.Core.Models;

public class ReglementClient : ICanBeComptabilise, ICanBeLettre
{
	private readonly Func<IEnumerable<Affectation>> _affectationsGetterDelegate;

	private readonly Func<IEnumerable<HistoriqueMvt>> _historiquesGetterDelegate;

	private readonly Func<ModeReglement> _modeReglementGetterDelegate;

	private readonly Func<IEnumerable<Remplacement>> _remplacentGetterDelegate;

	private readonly Func<IEnumerable<Remplacement>> _remplacerGetterDelegate;

	private IEnumerable<Affectation> _affectations;

	private IEnumerable<HistoriqueMvt> _historiques;

	private Lazy<IEnumerable<Affectation>> _lazyAffectations;

	private Lazy<IEnumerable<HistoriqueMvt>> _lazyHistoriques;

	private Lazy<ModeReglement> _lazyModeReglement;

	private Lazy<IEnumerable<Remplacement>> _lazyRemplacents;

	private Lazy<IEnumerable<Remplacement>> _lazyRemplacer;

	private ModeReglement _modeReglement;

	private IEnumerable<Remplacement> _remplacents;

	private IEnumerable<Remplacement> _remplacer;

	public string AffaireNumero { get; set; }

	public bool Ajuste { get; set; }

	public int? BanqueNo { get; set; }

	public string BanqueTier { get; set; }

	public decimal BaseRetenue { get; set; }

	public int? BordereauNature { get; set; }

	public int CaisseNo { get; set; }

	public int CaisseOrigine { get; set; }

	public string ClientCode { get; private set; }

	public string ClientIntitule { get; private set; }

	public int ClientNo { get; private set; }

	public TiersType TiersType { get; private set; }

	public DateTime Date { get; private set; }

	public DateTime DateCreation { get; set; }

	public DateTime DateEcheance { get; set; }

	public DateTime DateModification { get; set; }

	public DateTime DatePointage { get; set; }

	public DateTime DateRemis { get; set; }

	public decimal DeviseCours { get; set; }

	public decimal DeviseCoursOrigine { get; set; }

	public int DeviseNo { get; private set; }

	public int DeviseOrigineNo { get; set; }

	public int? EnteteBordereauNo { get; private set; }

	public string EnteteBordereauNumero { get; set; }

	public int ErpNo { get; private set; }

	public DateTime EscompteRegleDate { get; set; }

	public EtatMouvement Etat
	{
		get
		{
			if (!Solde.Equals(0m) || !SoldeDeviseSociete.Equals(0m))
			{
				return EtatMouvement.PartiellementSolde;
			}
			return EtatMouvement.Solde;
		}
	}

	public DateTime ImpayeDate { get; private set; }

	public string Info1 { get; set; }

	public string Info2 { get; set; }

	public string Info3 { get; set; }

	public string Info4 { get; set; }

	public bool IsAffDevise { get; set; }

	public bool IsAnnule { get; set; }

	public EtatComptabilite IsComptabilise { get; private set; }

	public bool IsComptabiliseImpaye { get; private set; }

	public bool IsEscompteRegle { get; set; }

	public ImpayeEtat IsImpaye { get; private set; }

	public bool IsPointe { get; set; }

	public Remis IsRemis { get; set; }

	public EtatPreavis Preavis { get; set; }

	public DateTime PreavisDate { get; set; }

	public Remplace IsRemplace
	{
		get
		{
			if (SoldeToRemplace == Montant)
			{
				return Remplace.NonRemplace;
			}
			if (!(SoldeToRemplace == 0m))
			{
				return Remplace.PartiellementRemplace;
			}
			return Remplace.TotalementRemplace;
		}
	}

	public bool IsRemplacer => SoldeToRemplace != Montant;

	public EtatComptabilite IsRemplacmentComptabilise { get; set; }

	public bool IsSolde
	{
		get
		{
			if (Solde == 0m)
			{
				return SoldeDeviseSociete == 0m;
			}
			return false;
		}
	}

	public string Libelle { get; set; }

	public int ModeReglementNo { get; private set; }

	public int ModificateurNo { get; set; }

	public decimal Montant { get; private set; }

	public decimal MontantDeviseSociete { get; private set; }

	public decimal MtDevOrigine { get; set; }

	public int No { get; private set; }

	public string Numero { get; private set; }

	public string PieceNumero { get; set; }

	public string RibClient { get; set; }

	public int SocieteNo { get; private set; }

	public decimal Solde { get; set; }

	public decimal SoldeDeviseSociete { get; set; }

	public decimal SoldeToRemplace { get; set; }

	public StatutTransfert StatutTransfert { get; set; }

	public decimal TauxRetenue { get; set; }

	public string Tire { get; set; }

	public ReglementType Type { get; private set; }

	public int UtilisateurNo { get; set; }

	public int EcheanceAvoirNo { get; set; }

	public string EcheanceAvoirNumero { get; set; }

	public bool IsReglementAvoir { get; set; }

	public bool IsReport { get; set; }

	public int UserReportNo { get; set; }

	public DateTime? AncienneDateEch { get; set; }

	public string ExtraitNum { get; set; } = string.Empty;

	public bool IsLettrer { get; set; }

	public string Lettre { get; set; }

	public DateTime? DateLettrage { get; set; }

	public DateTime? ExerciceLettrage { get; set; }

	public int? DeclarationTvaEncaissementNo { get; set; }

	public bool IsSynchroniser { get; set; }

	public int? SynchroErpNo { get; set; }

	public string Reference { get; set; }

	public int CollaborateurNo { get; set; }

	public bool IsCertifier { get; set; }

	public DateTime? DateValiditer { get; set; }

	public decimal? MontantPlafond { get; set; }

	public string PieceNumeroBordereau { get; set; }

	public ReglementNature ReglementNature { get; set; }

	public bool IsAvanceComptabilise { get; set; }

	public bool IsOrigineAvance { get; set; }

	public bool IsReserveDossierClt { get; set; }

	public int? DossierNo { get; set; }

	public string DossierNumero { get; set; }

	public bool SoumisDroitTimbre { get; set; }

	public decimal MontantDroitTimbre { get; set; }

	public bool IsImporterFromErp { get; set; }

	public bool IsImporterComptabiliseErp { get; set; }

	public ReglementClient(string numero, int clientNo, string clientCode, string clientIntitule, TiersType tiersType, int caisseNo, int societeNo, int deviseNo, int modeReglementNo, ReglementType type, DateTime date, decimal montant, DateTime dateEcheance, decimal coursDevise, decimal montantDevise)
	{
		if (string.IsNullOrEmpty(numero))
		{
			throw new ArgumentNullException("numero");
		}
		if (caisseNo == 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorCaisseNo);
		}
		if (deviseNo == 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorDeviseNo);
		}
		if (modeReglementNo == 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorModeReglementInvalide);
		}
		Numero = numero;
		ClientNo = clientNo;
		ClientCode = clientCode;
		ClientIntitule = clientIntitule;
		TiersType = tiersType;
		CaisseNo = caisseNo;
		DeviseNo = deviseNo;
		ModeReglementNo = modeReglementNo;
		Date = date;
		Montant = montant;
		Solde = montant;
		SoldeToRemplace = montant;
		SocieteNo = societeNo;
		_affectations = new List<Affectation>();
		_historiques = new List<HistoriqueMvt>();
		_remplacents = new List<Remplacement>();
		_remplacer = new List<Remplacement>();
		Libelle = string.Empty;
		PieceNumero = string.Empty;
		Tire = string.Empty;
		ImpayeDate = date;
		DateEcheance = ((type == ReglementType.Espece) ? date : dateEcheance);
		IsImpaye = ImpayeEtat.NonImpaye;
		Type = type;
		EnteteBordereauNumero = string.Empty;
		EscompteRegleDate = new DateTime(1900, 1, 1);
		DateRemis = date;
		DeviseCours = coursDevise;
		MontantDeviseSociete = montantDevise;
		SoldeDeviseSociete = montantDevise;
		Info1 = string.Empty;
		Info2 = string.Empty;
		Info3 = string.Empty;
		Info4 = string.Empty;
	}

	public ReglementClient(int no, int erpNo, string numero, DateTime date, decimal montant, int deviseNo, int clientNo, string clientCode, string clientIntitule, TiersType tiersType, int modeReglementNo, int caisseNo, int societeNo, EtatComptabilite isComptabilise, bool isComptabiliseImpaye, Remis isRemis, ImpayeEtat isImpaye, DateTime dateImpaye, int? enteteBordereauNo, string entetBordereauNumero, DateTime dateEcheance, decimal solde, decimal soldeToReplace, ReglementType type, decimal coursDevise, decimal montantDevise, decimal soldeDevise, Func<IEnumerable<Affectation>> affectationsGetterDelegate, Func<ModeReglement> modeReglementGetterDelegate, Func<IEnumerable<HistoriqueMvt>> historiquesGetterDelegate, Func<IEnumerable<Remplacement>> remplacentGetterDelegate, Func<IEnumerable<Remplacement>> remplacerGetterDelegate)
		: this(numero, clientNo, clientCode, clientIntitule, tiersType, caisseNo, societeNo, deviseNo, modeReglementNo, type, date, montant, dateEcheance, coursDevise, montantDevise)
	{
		_affectationsGetterDelegate = affectationsGetterDelegate ?? throw new ArgumentNullException("affectationsGetterDelegate");
		_modeReglementGetterDelegate = modeReglementGetterDelegate ?? throw new ArgumentNullException("modeReglementGetterDelegate");
		_historiquesGetterDelegate = historiquesGetterDelegate ?? throw new ArgumentNullException("historiquesGetterDelegate");
		_remplacentGetterDelegate = remplacentGetterDelegate ?? throw new ArgumentNullException("remplacentGetterDelegate");
		_remplacerGetterDelegate = remplacerGetterDelegate ?? throw new ArgumentNullException("remplacerGetterDelegate");
		No = no;
		ErpNo = erpNo;
		Solde = solde;
		SoldeToRemplace = soldeToReplace;
		DateEcheance = dateEcheance;
		EnteteBordereauNo = enteteBordereauNo;
		IsRemis = isRemis;
		IsComptabilise = isComptabilise;
		IsComptabiliseImpaye = isComptabiliseImpaye;
		IsImpaye = isImpaye;
		ImpayeDate = dateImpaye;
		EnteteBordereauNumero = entetBordereauNumero;
		SoldeDeviseSociete = soldeDevise;
	}

	public void ChangeDevise(int deviseNo, decimal coursDevise, int nbDecimals)
	{
		if (deviseNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorDeviseNo, "deviseNo");
		}
		if (IsComptabilise == EtatComptabilite.Comptabilise)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementComptabilise);
		}
		if (IsRemis == Remis.RemisBordereau)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementRelie);
		}
		if (GetAffectations().ToList().Count != 0)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementAffecteEcheances);
		}
		if (GetRemplacements().ToList().Count != 0)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementDejaRemplacé);
		}
		if (IsPointe)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementRapproche);
		}
		if (GetHistoriques().ToList().Count != 1)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementTransfere);
		}
		DeviseNo = deviseNo;
		DeviseCours = coursDevise;
		MontantDeviseSociete = decimal.Round(Montant * DeviseCours, nbDecimals);
		SoldeDeviseSociete = decimal.Round(Solde * DeviseCours, nbDecimals);
	}

	public void ChangeCours(decimal newCours, int nbDecimals)
	{
		if (IsComptabilise == EtatComptabilite.Comptabilise)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementComptabilise);
		}
		if (GetAffectations().ToList().Count != 0)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementAffecteEcheances);
		}
		if (GetRemplacements().ToList().Count != 0)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementDejaRemplacé);
		}
		if (IsPointe)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementRapproche);
		}
		DeviseCours = newCours;
		MontantDeviseSociete = decimal.Round(Montant * DeviseCours, nbDecimals);
		SoldeDeviseSociete = decimal.Round(Solde * DeviseCours, nbDecimals);
	}

	public void ChangeDeviseAffectation(int deviseNo, decimal cours, decimal montant, int nbDecimals)
	{
		if (deviseNo <= 0)
		{
			throw new ArgumentException(TresorerieCoreMessages.ErrorDeviseNo, "deviseNo");
		}
		if (IsComptabilise == EtatComptabilite.Comptabilise)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementComptabilise);
		}
		if (IsRemis == Remis.RemisBordereau)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementRelie);
		}
		if (GetAffectations().ToList().Count != 0)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementAffecteEcheances);
		}
		if (GetRemplacements().ToList().Count != 0)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementDejaRemplacé);
		}
		if (IsPointe)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementRapproche);
		}
		DeviseNo = deviseNo;
		DeviseCours = cours;
		Montant = decimal.Round(montant, nbDecimals);
		SoldeToRemplace = decimal.Round(montant, nbDecimals);
		Solde = decimal.Round(montant, nbDecimals);
	}

	public void ChangeEtatComptabilise(EtatComptabilite etat)
	{
		IsComptabilise = etat;
	}

	public void ChangeNumero(string newNumero)
	{
		if (string.IsNullOrEmpty(newNumero))
		{
			throw new ArgumentException("Numéro invalide.");
		}
		if (IsAnnule)
		{
			throw new ApplicationException("Le règlement est annulé.");
		}
		if (IsComptabilise == EtatComptabilite.Comptabilise)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementComptabilise);
		}
		if (IsRemis == Remis.RemisBordereau)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementRelie);
		}
		if (GetAffectations().ToList().Count != 0)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementAffecteEcheances);
		}
		if (GetRemplacements().ToList().Count != 0)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementDejaRemplacé);
		}
		if (IsPointe)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementRapproche);
		}
		Numero = newNumero;
	}

	public void ChangeDate(DateTime newDate)
	{
		if (IsAnnule)
		{
			throw new ApplicationException("Le règlement est annulé.");
		}
		if (IsComptabilise == EtatComptabilite.Comptabilise)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementComptabilise);
		}
		if (IsRemis == Remis.RemisBordereau)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementRelie);
		}
		if (GetAffectations().ToList().Count != 0)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementAffecteEcheances);
		}
		if (GetRemplacements().ToList().Count != 0)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementDejaRemplacé);
		}
		if (IsPointe)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementRapproche);
		}
		Date = newDate;
	}

	public void ChangeToImpaye(DateTime date)
	{
		if (IsRemis == Remis.RemisBanque)
		{
			ImpayeDate = date;
			IsImpaye = ImpayeEtat.Impaye;
		}
	}

	public void ChangeToNonImpaye()
	{
		if (IsRemis == Remis.RemisBanque)
		{
			ImpayeDate = Date;
			IsImpaye = ImpayeEtat.NonImpaye;
			if (Preavis == EtatPreavis.RegulariseImpaye)
			{
				Preavis = EtatPreavis.Preavis;
			}
		}
	}

	public IEnumerable<Affectation> GetAffectations()
	{
		if (_lazyAffectations == null && _affectationsGetterDelegate != null)
		{
			_lazyAffectations = new Lazy<IEnumerable<Affectation>>(_affectationsGetterDelegate);
		}
		if (_lazyAffectations != null && !_lazyAffectations.IsValueCreated)
		{
			_affectations = _lazyAffectations.Value;
		}
		return _affectations;
	}

	public IEnumerable<HistoriqueMvt> GetHistoriques()
	{
		if (_lazyHistoriques == null && _historiquesGetterDelegate != null)
		{
			_lazyHistoriques = new Lazy<IEnumerable<HistoriqueMvt>>(_historiquesGetterDelegate);
		}
		if (_lazyHistoriques != null && !_lazyHistoriques.IsValueCreated)
		{
			_historiques = _lazyHistoriques.Value;
		}
		return _historiques;
	}

	public IEnumerable<Remplacement> GetMesRemplacants()
	{
		if (_lazyRemplacents == null && _remplacentGetterDelegate != null)
		{
			_lazyRemplacents = new Lazy<IEnumerable<Remplacement>>(_remplacentGetterDelegate);
		}
		if (_lazyRemplacents != null && !_lazyRemplacents.IsValueCreated)
		{
			_remplacents = _lazyRemplacents.Value;
		}
		return _remplacents;
	}

	public ModeReglement GetModeReglement()
	{
		if (_lazyModeReglement == null && _modeReglementGetterDelegate != null)
		{
			_lazyModeReglement = new Lazy<ModeReglement>(_modeReglementGetterDelegate);
		}
		if (_lazyModeReglement != null && !_lazyModeReglement.IsValueCreated)
		{
			_modeReglement = _lazyModeReglement.Value;
		}
		return _modeReglement;
	}

	public IEnumerable<Remplacement> GetRemplacements()
	{
		if (_lazyRemplacer == null && _remplacerGetterDelegate != null)
		{
			_lazyRemplacer = new Lazy<IEnumerable<Remplacement>>(_remplacerGetterDelegate);
		}
		if (_lazyRemplacer != null && !_lazyRemplacer.IsValueCreated)
		{
			_remplacer = _lazyRemplacer.Value;
		}
		return _remplacer;
	}

	public Affectation InitNewAffectation(decimal montant, decimal montantDevise, int echeanceNo, int? ecartEcheanceNo)
	{
		if (Solde < montant)
		{
			throw new ApplicationException(TresorerieCoreMessages.ErrorMontantInavlide);
		}
		return new Affectation(0, DateTime.Now, montant, No, echeanceNo, montantDevise, ecartEcheanceNo);
	}

	public void UpdateMontant(decimal montant, decimal cours)
	{
		if (IsComptabilise == EtatComptabilite.Comptabilise)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementComptabilise);
		}
		if (IsRemis == Remis.RemisBordereau)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementRelie);
		}
		if (GetAffectations().ToList().Count != 0)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementAffecteEcheances);
		}
		if (GetRemplacements().ToList().Count != 0)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementDejaRemplacé);
		}
		if (IsPointe)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementRapproche);
		}
		if (GetHistoriques().ToList().Count != 1)
		{
			throw new InvalidOperationException(TresorerieCoreMessages.ErrorReglementTransfere);
		}
		if (cours < 0m)
		{
			throw new ArgumentException("Cours devise invalide!");
		}
		DeviseCours = cours;
		Montant = montant;
		SoldeToRemplace = montant;
		Solde = montant;
		MontantDeviseSociete = montant * DeviseCours;
		SoldeDeviseSociete = montant * DeviseCours;
	}

	public bool Equals(ReglementClient item)
	{
		if (item == null)
		{
			return false;
		}
		return GetHashCode() == item.GetHashCode();
	}

	public override int GetHashCode()
	{
		return new
		{
			A = No,
			B = Numero,
			C = Montant,
			D = SocieteNo,
			E = SynchroErpNo
		}.GetHashCode();
	}

	public override bool Equals(object obj)
	{
		if (obj == null)
		{
			return false;
		}
		if (obj is ReglementClient item)
		{
			return Equals(item);
		}
		return false;
	}

	public override string ToString()
	{
		return $"[Règlement Client] {No}, {Numero}, {Montant} {SocieteNo}";
	}
}
