using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IVerifySoldeReglementClientRepository
{
	VerifySoldeReglementClient Get(int no);

	IEnumerable<VerifySoldeReglementClient> GetAll();

	void Update(VerifySoldeReglementClient reglement);
}
