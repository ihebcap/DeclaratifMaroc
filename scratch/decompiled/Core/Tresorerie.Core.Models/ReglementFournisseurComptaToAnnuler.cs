using System;

namespace Tresorerie.Core.Models;

public class ReglementFournisseurComptaToAnnuler : ICanBeComptabilise
{
	public ReglementFournisseur Reglement { get; }

	public ReglementFournisseurComptaToAnnuler(ReglementFournisseur reglement)
	{
		Reglement = reglement ?? throw new ArgumentNullException("reglement");
	}
}
