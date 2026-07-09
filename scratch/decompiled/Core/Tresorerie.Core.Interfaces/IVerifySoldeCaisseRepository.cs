using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IVerifySoldeCaisseRepository
{
	VerifySoldeCaisse Get(int no);

	IEnumerable<VerifySoldeCaisse> GetAll();
}
