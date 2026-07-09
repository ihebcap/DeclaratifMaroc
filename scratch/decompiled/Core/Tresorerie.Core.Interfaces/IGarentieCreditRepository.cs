using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IGarentieCreditRepository
{
	GarantieCredit Get(int no);

	IEnumerable<GarantieCredit> GetAllFromCredit(int creditNo);

	int Create(GarantieCredit credit);

	void Delete(int no);

	bool IsGarentieUtilise(int garentieNo);
}
