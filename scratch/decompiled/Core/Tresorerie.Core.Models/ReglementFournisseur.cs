using System;
using System.Collections.Generic;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class ReglementFournisseur : ICanBeComptabilise
{
	private readonly Func<IEnumerable<Affectation>> _affectationsGetterDelegate;

	private Lazy<IEnumerable<Affectation>> _lazyAffectations;

	private IEnumerable<Affectation> _affectations;

	private readonly Func<IEnumerable<DossierReglement>> _dossiersGetterDelegate;

	private Lazy<IEnumerable<DossierReglement>> _lazydossiers;

	private IEnumerable<DossierReglement> _dossiers;

	public int No { get; private set; }

	public int ErpNo { get; private set; }

	public int SocieteNo { get; private set; }

	public string Numero { get; private set; }

	public DateTime Date { get; private set; }

	public decimal Montant { get; private set; }

	public decimal MontantDeviseSociete { get; set; }

	public int DeviseNo { get; private set; }

	public decimal DeviseCours { get; set; }

	public int FournisseurNo { get; private set; }

	public string PieceNumero { get; set; }

	public int ModeReglementNo { get; private set; }

	public int CaisseNo { get; set; }

	public string Libelle { get; set; }

	public RemisFournisseur Recuperer { get; set; }

	public string RecouvreurNom { get; set; }

	public string RecouvreurCin { get; set; }

	public DateTime DateRecuperation { get; set; }

	public bool IsDecaisse { get; set; }

	public EtatComptabilite IsComptabilise { get; private set; }

	public ImpayeEtat IsImpaye { get; set; }

	public DateTime ImpayeDate { get; set; }

	public int? BanqueNo { get; set; }

	public string BanqueTier { get; set; }

	public int UserNo { get; set; }

	public DateTime DateEcheance { get; set; }

	public ReglementType Type { get; private set; }

	public bool IsPointe { get; set; }

	public DateTime DatePointage { get; set; }

	public bool IsBarre { get; set; }

	public string Beneficiaire { get; set; }

	public string DossierNumero { get; set; }

	public int NbTimbre { get; set; }

	public decimal BaseRetenue { get; set; }

	public decimal Taux { get; set; }

	public decimal MontantTimbre { get; set; }

	public int? ChequeNo { get; set; }

	public int? TraiteNo { get; set; }

	public int? DossierNo { get; set; }

	public DateTime DateModification { get; set; }

	public DateTime DateCreation { get; set; }

	public int ModificateurNo { get; set; }

	public int VirementTiersNo { get; set; }

	public string VirementTiersNumero { get; set; }

	public string Rib { get; set; }

	public string FournisseurCode { get; private set; }

	public string FournisseurIntitule { get; private set; }

	public TiersType TiersType { get; private set; }

	public bool IsComptabiliseImpaye { get; set; }

	public int AnnexeType { get; set; }

	public string AnnexeCode { get; set; }

	public decimal Solde { get; set; }

	public decimal SoldeDevise { get; set; }

	public bool IsReserveDossierFrs { get; set; }

	public string AffaireNumero { get; set; }

	public string Info1 { get; set; }

	public string Info2 { get; set; }

	public string Info3 { get; set; }

	public string Info4 { get; set; }

	public bool IsValider { get; set; }

	public bool IsAnnule { get; set; }

	public bool IsReport { get; set; }

	public int UserReportNo { get; set; }

	public DateTime? AncienneDateEch { get; set; }

	public string ExtraitNum { get; set; } = string.Empty;

	public bool ChequeSigne { get; set; }

	public int? DeclarationTvaEncaissementNo { get; set; }

	public int? RetenuePourEcheanceNo { get; set; }

	public bool IsSynchroniser { get; set; }

	public int? SynchroErpNo { get; set; }

	public int? EnteteBordereauNo { get; set; }

	public string EnteteBordereauNumero { get; set; }

	public bool IsCertifier { get; set; }

	public DateTime? DateValiditer { get; set; }

	public decimal? MontantPlafond { get; set; }

	public bool Ajuste { get; set; }

	public ReglementNature ReglementNature { get; set; }

	public bool IsAvanceComptabilise { get; set; }

	public bool IsOrigineAvance { get; set; }

	public bool IsImporterFromErp { get; set; }

	public bool IsImporterComptabiliseErp { get; set; }

	public ReglementFournisseur(string numero, int frsNo, string frsCode, string frsIntitule, TiersType tiersType, int caisseNo, int societeNo, int deviseNo, int modeReglementNo, ReglementType type, DateTime date, decimal montant, DateTime dateEcheance)
	{
		if (string.IsNullOrEmpty(numero))
		{
			throw new ArgumentNullException("numero");
		}
		if (caisseNo == 0)
		{
			throw new ArgumentException("caisseNo");
		}
		if (deviseNo == 0)
		{
			throw new ArgumentException("deviseNo");
		}
		if (modeReglementNo == 0)
		{
			throw new ArgumentException("modeReglementNo");
		}
		Numero = numero;
		FournisseurNo = frsNo;
		FournisseurCode = frsCode;
		FournisseurIntitule = frsIntitule;
		TiersType = tiersType;
		CaisseNo = caisseNo;
		DeviseNo = deviseNo;
		ModeReglementNo = modeReglementNo;
		Date = date;
		Montant = montant;
		SocieteNo = societeNo;
		Type = type;
		DateEcheance = dateEcheance;
	}

	public ReglementFournisseur(int no, int erpNo, string numero, DateTime date, decimal montant, int deviseNo, int frsNo, string frsCode, string frsIntitule, TiersType tiersType, int modeReglementNo, int caisseNo, int societeNo, EtatComptabilite isComptabilise, ImpayeEtat isImpaye, DateTime dateImpaye, DateTime dateEcheance, ReglementType type, Func<IEnumerable<Affectation>> affectationsGetterDelegate, Func<IEnumerable<DossierReglement>> dossiersGetterDelegate)
		: this(numero, frsNo, frsCode, frsIntitule, tiersType, caisseNo, societeNo, deviseNo, modeReglementNo, type, date, montant, dateEcheance)
	{
		_affectationsGetterDelegate = affectationsGetterDelegate ?? throw new ArgumentNullException("affectationsGetterDelegate");
		_dossiersGetterDelegate = dossiersGetterDelegate ?? throw new ArgumentNullException("dossiersGetterDelegate");
		No = no;
		ErpNo = erpNo;
		DateEcheance = dateEcheance;
		IsComptabilise = isComptabilise;
		IsImpaye = isImpaye;
		ImpayeDate = dateImpaye;
	}

	public void ChangeEtatComptabilise(EtatComptabilite etat)
	{
		IsComptabilise = etat;
	}

	public void ChangeEtatImpaye(ImpayeEtat etat, DateTime dateImpaye)
	{
		IsImpaye = etat;
		ImpayeDate = dateImpaye;
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

	public IEnumerable<DossierReglement> GetDossiersReglement()
	{
		if (_lazydossiers == null && _dossiersGetterDelegate != null)
		{
			_lazydossiers = new Lazy<IEnumerable<DossierReglement>>(_dossiersGetterDelegate);
		}
		if (_lazydossiers != null && !_lazydossiers.IsValueCreated)
		{
			_dossiers = _lazydossiers.Value;
		}
		return _dossiers;
	}
}
