using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ILibelleComptaFormuleRunner
{
	string Run(IList<ParameterLibelleCompta> list);
}
