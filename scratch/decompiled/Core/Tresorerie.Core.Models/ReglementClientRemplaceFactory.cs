using System;
using Tresorerie.Core.Enum;

namespace Tresorerie.Core.Models;

public class ReglementClientRemplaceFactory
{
	public bool CanGenerateRemplacment(ReglementClient reglement)
	{
		if (reglement.IsRemplace == Remplace.TotalementRemplace)
		{
			return true;
		}
		return false;
	}

	public ReglementClientRemplace Generate(ReglementClient reglement)
	{
		if (!CanGenerateRemplacment(reglement))
		{
			throw new InvalidOperationException("Règlement [" + reglement.Numero + "] n'est pas totalment remplacé!");
		}
		return new ReglementClientRemplace(reglement);
	}
}
