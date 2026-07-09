using System;
using System.Collections.Generic;
using System.Linq;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class EcritureComptable
{
	private DateTime _echeance;

	private DateTime _dateRapprochement;

	public NatureActionEtape Action { get; set; }

	public int Annee { get; set; }

	public string CodeJournal { get; set; }

	public string CompteGeneral { get; set; }

	public string ContrePartieCompteG { get; set; }

	public string ContrePartieTiers { get; set; }

	public decimal Cours { get; set; }

	public DateComptaEtape DateComptaEtape { get; set; }

	public DateTime DateCreation { get; set; }

	public int DeviseErpNo { get; set; }

	public int DeviseSocieteNo { get; set; }

	public MouvementDomaine Domaine { get; set; }

	public int? DossierNo { get; set; }

	public DateTime Echeance
	{
		get
		{
			return _echeance.Date;
		}
		set
		{
			_echeance = value.Date;
		}
	}

	public int ErpNo { get; set; }

	public int Etape { get; set; }

	public bool IsComptabiliseErp => ErpNo != 0;

	public bool IsLettre { get; set; }

	public bool IsNowCompta { get; set; }

	public bool IsPointe { get; set; }

	public int Jour { get; set; }

	public string Lettrage { get; set; }

	public string Libelle { get; set; }

	public int? LigneVirementNo { get; set; }

	public int ModeNo { get; set; }

	public decimal Montant { get; set; }

	public decimal MontantCredit
	{
		get
		{
			if (Sens != SensCompta.Debit)
			{
				if (!(MontantDevise == 0m))
				{
					return MontantDevise;
				}
				return Montant;
			}
			return 0m;
		}
	}

	public decimal MontantDebit
	{
		get
		{
			if (Sens != SensCompta.Credit)
			{
				if (!(MontantDevise == 0m))
				{
					return MontantDevise;
				}
				return Montant;
			}
			return 0m;
		}
	}

	public decimal MontantCreditDeviseSociete
	{
		get
		{
			if (Sens != SensCompta.Debit)
			{
				return Montant;
			}
			return 0m;
		}
	}

	public decimal MontantDebitDeviseSociete
	{
		get
		{
			if (Sens != SensCompta.Credit)
			{
				return Montant;
			}
			return 0m;
		}
	}

	public decimal MontantDevise { get; set; }

	public int? MouvementNo { get; set; }

	public int No { get; set; }

	public string NumeroDocument { get; set; }

	public string NumeroPiece { get; set; }

	public DateTime Date => new DateTime(Annee, (short)Periode, Jour);

	public ErpComptaPeriode Periode { get; set; }

	public string PeriodeAnnee => $"{Annee:0000}/{(short)Periode:00}";

	public string PieceTresorerie { get; set; }

	public DateTime DateRapprochement
	{
		get
		{
			return _dateRapprochement.Date;
		}
		set
		{
			_dateRapprochement = value.Date;
		}
	}

	public string Pointage { get; set; }

	public string Reference { get; set; }

	public int? RemboursementNo { get; set; }

	public int? RemplacmentNo { get; set; }

	public SensCompta Sens { get; set; }

	public int SocieteNo { get; set; }

	public string TiersNumero { get; set; }

	public int? VirementTiersNo { get; set; }

	public short TaxeProvenance { get; set; }

	public string TaxeCode { get; set; }

	public bool IsAvoir { get; set; }

	public int Indice { get; set; }

	public bool IsAnalytique { get; set; }

	public decimal Pourcentage { get; set; }

	public virtual IEnumerable<VentilationAnalytique> VentilationAnalytiques { get; set; } = Enumerable.Empty<VentilationAnalytique>();
}
