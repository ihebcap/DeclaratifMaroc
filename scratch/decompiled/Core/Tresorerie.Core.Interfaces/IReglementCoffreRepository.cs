using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IReglementCoffreRepository
{
	List<ReglementCoffre> GetAll(int societeNo, params int[] caissesNo);
}
