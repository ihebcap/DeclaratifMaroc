using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class RemboursementClientAllType : ICanBeComptabilise
{
	public string BanqueClient { get; set; }

	public int? BanqueNo { get; set; }

	public int CaisseNo { get; set; }

	public int? ChequeNo { get; set; }

	public string ChequeNumero { get; set; }

	public int ClientNo { get; set; }

	public EtatComptabilite Comptabilise { get; set; }

	public DateTime Date { get; set; }

	public DateTime DateCreation { get; set; }

	public DateTime DateModification { get; set; }

	public DateTime DatePointeRemboursementCheque { get; set; }

	public decimal Cours { get; set; }

	public int DeviseNo { get; set; }

	public DateTime Echeance { get; set; }

	public Etat Etat { get; set; }

	public bool IsRemboursementChequePointe { get; set; }

	public string Libelle { get; set; }

	public int ModeReglementNo { get; set; }

	public int ModificateurNo { get; set; }

	public decimal Montant { get; set; }

	public decimal MontantDeviseSociete { get; set; }

	public int No { get; set; }

	public string Numero { get; set; }

	public string Reference { get; set; }

	public int? RemboursementClientNo { get; set; }

	public string RemboursementClientNumero { get; set; }

	public string RibClient { get; set; }

	public int SocieteNo { get; set; }

	public decimal Solde { get; set; }

	public TypeRemboursementClient TypeRemboursement { get; set; }

	public int UtilisateurNo { get; set; }

	public string VirementTiersNumero { get; set; }

	public int? VirrementTiersNo { get; set; }

	public string InfoLibre1 { get; set; }

	public string InfoLibre2 { get; set; }

	public string InfoLibre3 { get; set; }

	public string InfoLibre4 { get; set; }
}
