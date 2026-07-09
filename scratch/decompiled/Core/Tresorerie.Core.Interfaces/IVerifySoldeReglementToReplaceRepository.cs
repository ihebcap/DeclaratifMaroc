using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IVerifySoldeReglementToReplaceRepository
{
	VerifySoldeReglementToReplace Get(int no);

	IEnumerable<VerifySoldeReglementToReplace> GetAll();

	void Update(VerifySoldeReglementToReplace solde);
}
