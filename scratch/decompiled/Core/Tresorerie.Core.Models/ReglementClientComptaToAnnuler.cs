using System;

namespace Tresorerie.Core.Models;

public class ReglementClientComptaToAnnuler : ICanBeComptabilise
{
	private readonly ReglementClient _reglement;

	public ReglementClient Reglement => _reglement;

	public ReglementClientComptaToAnnuler(ReglementClient reglement)
	{
		_reglement = reglement ?? throw new ArgumentNullException("reglement");
	}
}
