using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class EcartEchange : ICanBeComptabilise
{
	public int ClientNo { get; set; }

	public string Commentaire { get; set; }

	public DateTime Date { get; set; }

	public decimal FactureCours { get; set; }

	public int FactureDeviseNo { get; set; }

	public int FactureNo { get; set; }

	public string FactureNumero { get; set; }

	public EtatComptabilite IsComptabilise { get; set; }

	public int ModeNo { get; set; }

	public decimal Montant { get; set; }

	public decimal MontantAffectation { get; set; }

	public int No { get; set; }

	public string Numero { get; set; }

	public decimal ReglementCours { get; set; }

	public int ReglementDeviseNo { get; set; }

	public int ReglementNo { get; set; }

	public string ReglementNumero { get; set; }

	public int SocieteNo { get; set; }

	public EcheanceType Type { get; set; }
}
