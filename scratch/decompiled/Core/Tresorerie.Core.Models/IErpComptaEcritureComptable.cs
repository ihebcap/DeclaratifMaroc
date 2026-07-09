using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public interface IErpComptaEcritureComptable
{
	int Annee { get; }

	string CodeJournal { get; }

	string CompteGeneral { get; }

	string ContrePartieCompteG { get; }

	string ContrePartieTiers { get; }

	decimal Cours { get; }

	DateTime Date { get; }

	DateTime DateCreation { get; }

	int DeviseErpNo { get; }

	int? DossierNo { get; }

	DateTime Echeance { get; }

	bool IsLettre { get; }

	bool IsPointe { get; }

	int Jour { get; }

	string Lettrage { get; set; }

	string Libelle { get; }

	int LigneNo { get; }

	decimal MontantDeviseSociete { get; }

	decimal MontantDevise { get; }

	int No { get; }

	int ErpNoLinkRan { get; }

	string NumeroDocument { get; }

	string NumeroPiece { get; }

	ErpComptaPeriode Periode { get; }

	string PieceTresorerie { get; }

	DateTime DateRapprochement { get; }

	bool IsRapproche { get; }

	string Pointage { get; }

	string Reference { get; }

	SensCompta Sens { get; }

	string TiersNumero { get; }

	decimal MontantCreditDevise { get; }

	decimal MontantDebitDevise { get; }

	decimal MontantCreditDeviseSociete { get; }

	decimal MontantDebitDeviseSociete { get; }

	int ModeNo { get; }

	string TaxeCode { get; set; }
}
