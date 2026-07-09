using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class Impaye : ICanBeComptabilise
{
	public string BanqueClient { get; set; }

	public int BanqueNo { get; set; }

	public int BordereauNo { get; set; }

	public string BordereauNumero { get; set; }

	public int CaisseNo { get; set; }

	public int ClientNo { get; set; }

	public DateTime DateImpaye { get; set; }

	public DateTime DateReglement { get; set; }

	public DateTime DateRemis { get; set; }

	public DateTime Echeance { get; set; }

	public int EcheanceNo { get; set; }

	public Etat Etat { get; set; }

	public EtatComptabilite IsComptabilise { get; set; }

	public ReglementType Type { get; private set; }

	public string Libelle { get; set; }

	public int ModeNo { get; set; }

	public decimal MontantReglement { get; set; }

	public NatureTypeBordereau NatureBordereau { get; set; }

	public string Numero { get; set; }

	public string Piece { get; set; }

	public EtatComptabilite ReglementComptabilise { get; set; }

	public int ReglementNo { get; set; }

	public int RepresentantNo { get; set; }

	public int SocieteNo { get; set; }

	public decimal SoldeImpaye { get; set; }

	public int SoucheNo { get; set; }

	public string Tire { get; set; }

	public decimal DeviseCours { get; set; }

	public int DeviseNo { get; set; }

	public void ChangeEtatComptabilise(EtatComptabilite etat)
	{
		IsComptabilise = etat;
	}
}
