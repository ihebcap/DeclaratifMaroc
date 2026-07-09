using System;
using System.Collections.Generic;
using Tresorerie.Core.Enum;
using Tresorerie.Infrastructure.Helpers;

namespace Tresorerie.Core.Models;

public class Echeance
{
	private readonly Func<IEnumerable<Affectation>> _affectationsGetterDelegate;

	private IEnumerable<Affectation> _affectations;

	private bool _isComptaEcart;

	private Lazy<IEnumerable<Affectation>> _lazyAffectations;

	public bool Ajuste { get; set; }

	public string BanqueClient { get; set; }

	public int? BanqueNo { get; set; }

	public int? CaisseNo { get; set; }

	public int? ChequeNo { get; set; }

	public string ClientCode { get; private set; }

	public string ClientIntitule { get; private set; }

	public int ClientNo { get; private set; }

	public string AffaireNumero { get; set; }

	public int CollaborateurNo { get; set; }

	public string Commentaire { get; set; }

	public decimal CoursDevise { get; set; }

	public DateTime Date { get; set; }

	public DateTime EcheanceReporte { get; set; }

	public DateTime DateCreation { get; set; }

	public DateTime DatePointeRemboursementCheque { get; set; }

	public int DeviseNo { get; set; }

	public DateTime DocumentDate { get; private set; }

	public string DocumentNumero { get; private set; }

	public ErpDocumentType DocumentType { get; private set; }

	public ErpDomaine Domaine { get; private set; }

	public int? EcartNo { get; set; }

	public int ErpNo { get; private set; }

	public Etat Etat
	{
		get
		{
			if (!(Solde == 0m) || !(SoldeDeviseSociete == 0m))
			{
				return Etat.NonPaye;
			}
			return Etat.TotalementPaye;
		}
	}

	public string Info1 { get; set; }

	public string Info2 { get; set; }

	public string Info3 { get; set; }

	public string Info4 { get; set; }

	public bool IsComptabilise { get; set; }

	public bool IsComptaEcart
	{
		get
		{
			if (Type == EcheanceType.GainEchange || Type == EcheanceType.PerteEchange)
			{
				return _isComptaEcart;
			}
			return false;
		}
		set
		{
			_isComptaEcart = (Type == EcheanceType.GainEchange || Type == EcheanceType.PerteEchange) && value;
		}
	}

	public bool IsComptaRemboursementClient { get; set; }

	public bool IsComptaVirementTiers { get; set; }

	public bool IsRemboursementChequePointe { get; set; }

	public bool IsReserveDossierFrs { get; set; }

	public int IsSpe { get; set; }

	public bool IsTimbre { get; set; }

	public int ModeReglementNo { get; set; }

	public decimal Montant { get; private set; }

	public decimal MontantDeviseSociete { get; set; }

	public int No { get; private set; }

	public string PayeurCode { get; set; }

	public string PayeurIntitule { get; set; }

	public int PayeurNo { get; set; }

	public string Reference { get; set; }

	public int ReglementImpayeNo => ErpNo;

	public int? RemboursementClientNo { get; set; }

	public string RemboursementClientNumero { get; set; }

	public string Rib { get; set; }

	public int SocieteNo { get; private set; }

	public decimal Solde { get; set; }

	public decimal SoldeDeviseSociete { get; set; }

	public int SoucheNo { get; private set; }

	public decimal Timbre { get; set; }

	public EcheanceType Type { get; private set; }

	public int UtilisateurNo { get; set; }

	public int? VirementTiersNo { get; set; }

	public string VirementTiersNumero { get; set; }

	public int ReglementAvoirNo { get; set; }

	public bool IsReglementAvoir => ReglementAvoirNo != 0;

	public string ReglementAvoirNumero { get; set; }

	public int? RemoursementEcheanceAvoirNo { get; set; }

	public string ExtraitNum { get; set; } = string.Empty;

	public int DelaisMoyenPayement { get; set; }

	public int? DesignationDocumentNo { get; set; }

	public bool IsFactureGrFromComptaErp
	{
		get
		{
			if (Type == EcheanceType.FactureGR)
			{
				return ErpNo != 0;
			}
			return false;
		}
	}

	public int? CreditNo { get; set; }

	public string CreditNum { get; set; }

	public string FileName { get; set; }

	public byte[] FilePdf { get; set; }

	public EcheanceWorkflowValidationStatut WorkflowStatut { get; set; }

	public Echeance(int erpNo, string documentNumero, ErpDomaine domaine, ErpDocumentType documentType, DateTime documentDate, decimal montant, decimal solde, DateTime date, EcheanceType type, int modeReglementNo, int societeNo, int clientNo, string clientCode, string clientIntitule, int payeurNo, string payeurCode, string payeurIntitule, int deviseNo, int soucheNo, int collaborateurNo, decimal cours, string commentaire, decimal montantDevise, decimal soldeDevise)
	{
		ErpNo = erpNo;
		DocumentNumero = documentNumero;
		DocumentType = documentType;
		DocumentDate = documentDate;
		Domaine = domaine;
		Montant = montant;
		Solde = solde;
		Date = date;
		Type = type;
		ModeReglementNo = modeReglementNo;
		SocieteNo = societeNo;
		ClientNo = clientNo;
		ClientCode = clientCode;
		ClientIntitule = clientIntitule;
		PayeurNo = payeurNo;
		PayeurCode = payeurCode;
		PayeurIntitule = payeurIntitule;
		DeviseNo = deviseNo;
		CoursDevise = cours;
		Commentaire = commentaire;
		SoucheNo = soucheNo;
		CollaborateurNo = collaborateurNo;
		_affectations = new List<Affectation>();
		MontantDeviseSociete = montantDevise;
		SoldeDeviseSociete = soldeDevise;
		DatePointeRemboursementCheque = DateHelper.GetMinSqlDateTime();
		Info1 = string.Empty;
		Info2 = string.Empty;
		Info3 = string.Empty;
		Info4 = string.Empty;
		DateCreation = DateTime.Now;
	}

	public Echeance(int no, int erpNo, string erpDocumentNumero, ErpDomaine domaine, ErpDocumentType documentType, DateTime documentDate, decimal montant, decimal solde, DateTime date, EcheanceType type, int modeReglementNo, int societeNo, int clientNo, string clientCode, string clientIntitule, int payeurNo, string payeurCode, string PayeurIntitule, int deviseNo, int soucheNo, int collaborateurNo, decimal coursDevise, string commentaire, decimal montantDevise, decimal soldeDevise, Func<IEnumerable<Affectation>> affectationsGetterDelegate)
		: this(erpNo, erpDocumentNumero, domaine, documentType, documentDate, montant, solde, date, type, modeReglementNo, societeNo, clientNo, clientCode, clientIntitule, payeurNo, payeurCode, PayeurIntitule, deviseNo, soucheNo, collaborateurNo, coursDevise, commentaire, montantDevise, soldeDevise)
	{
		No = no;
		_affectationsGetterDelegate = affectationsGetterDelegate ?? throw new ArgumentNullException("affectationsGetterDelegate");
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

	public void RefreshAffectations()
	{
		if (_affectationsGetterDelegate != null)
		{
			_lazyAffectations = null;
			_lazyAffectations = new Lazy<IEnumerable<Affectation>>(_affectationsGetterDelegate);
		}
	}

	public void ModifierMontant(decimal montant)
	{
		decimal num = Montant - Solde;
		Montant = montant;
		MontantDeviseSociete = montant * CoursDevise;
		Solde = montant - num;
		SoldeDeviseSociete = Solde * CoursDevise;
	}

	public bool Equals(Echeance item)
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
			B = DocumentNumero,
			C = Domaine
		}.GetHashCode();
	}

	public override bool Equals(object obj)
	{
		if (obj == null)
		{
			return false;
		}
		if (obj is Echeance item)
		{
			return Equals(item);
		}
		return false;
	}

	public override string ToString()
	{
		return $"[Echéance] {No}, {DocumentNumero}, {Domaine}";
	}
}
