using System;

namespace Tresorerie.Core.Models;

public class ReglementClientRemplace : ICanBeComptabilise
{
	private readonly ReglementClient _reglement;

	public ReglementClient Reglement => _reglement;

	public ReglementClientRemplace(ReglementClient reglement)
	{
		_reglement = reglement ?? throw new ArgumentNullException("reglement");
	}
}
