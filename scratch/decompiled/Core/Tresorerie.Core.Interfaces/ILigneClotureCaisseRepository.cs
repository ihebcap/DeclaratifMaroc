using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ILigneClotureCaisseRepository
{
	IList<LigneClotureCaisse> GetAll(int clotureNo);

	LigneClotureCaisse Get(int no);

	int Add(LigneClotureCaisse cloture);
}
