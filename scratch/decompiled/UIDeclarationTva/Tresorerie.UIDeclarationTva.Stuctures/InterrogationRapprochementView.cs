using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.UIDeclarationTva.Stuctures;

public class InterrogationRapprochementView
{
	public int Annee { get; set; }

	public string CodeJournal { get; set; }

	public string CompteGeneral { get; set; }

	public string ContrePartieCompteG { get; set; }

	public string ContrePartieTiers { get; set; }

	public decimal Cours { get; set; }

	public DateTime Date { get; set; }

	public DateTime DateCreation { get; set; }

	public int DeviseErpNo { get; set; }

	public int? DossierNo { get; set; }

	public DateTime Echeance { get; set; }

	public bool IsLettre { get; set; }

	public bool IsPointe { get; set; }

	public int Jour { get; set; }

	public string Lettrage { get; set; }

	public string Libelle { get; set; }

	public int LigneNo { get; set; }

	public decimal MontantDeviseSociete { get; set; }

	public decimal MontantDevise { get; set; }

	public int No { get; set; }

	public string NumeroDocument { get; set; }

	public string NumeroPiece { get; set; }

	public ErpComptaPeriode Periode { get; set; }

	public string PieceTresorerie { get; set; }

	public DateTime DateRapprochement { get; set; }

	public bool IsRapproche { get; set; }

	public string Pointage { get; set; }

	public string Reference { get; set; }

	public SensCompta Sens { get; set; }

	public string TiersNumero { get; set; }

	public decimal MontantCreditDevise { get; set; }

	public decimal MontantDebitDevise { get; set; }

	public decimal MontantCreditDeviseSociete { get; set; }

	public decimal MontantDebitDeviseSociete { get; set; }

	public bool IsDeclare { get; set; }

	public string DeclarationNumero { get; set; }

	public int? DeclarationNo { get; set; }

	public bool IsTresoEntity { get; set; }
}
