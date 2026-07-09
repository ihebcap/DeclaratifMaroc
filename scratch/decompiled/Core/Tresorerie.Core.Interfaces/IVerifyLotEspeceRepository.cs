using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IVerifyLotEspeceRepository
{
	VerifyLotEspece Get(int no);

	IEnumerable<VerifyLotEspece> GetAll();

	void Update(VerifyLotEspece lot);
}
