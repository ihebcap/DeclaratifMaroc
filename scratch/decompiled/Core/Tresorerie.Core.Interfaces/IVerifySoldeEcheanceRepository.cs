using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IVerifySoldeEcheanceRepository
{
	VerifySoldeEcheance Get(int no);

	IEnumerable<VerifySoldeEcheance> GetAll();

	void Update(VerifySoldeEcheance echeance);
}
