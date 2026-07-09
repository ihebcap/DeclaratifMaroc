using Tresorerie.Core.Interfaces;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Services;

public class BanqueTiersValidator : IValidator<BanqueTiers>
{
	public bool Validate(BanqueTiers banqueTiers)
	{
		if (!string.IsNullOrEmpty(banqueTiers.TiersNum) && !string.IsNullOrEmpty(banqueTiers.Banque))
		{
			return banqueTiers.SocieteNo >= 0;
		}
		return false;
	}
}
