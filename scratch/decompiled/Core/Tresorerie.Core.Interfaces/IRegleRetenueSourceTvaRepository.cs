using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IRegleRetenueSourceTvaRepository
{
	RegleRetenueSourceTva Get(int no);

	List<RegleRetenueSourceTva> GetAll();

	int? Create(RegleRetenueSourceTva regleRetenueSourceTva);

	void Update(RegleRetenueSourceTva regleRetenueSourceTva);

	void Delete(RegleRetenueSourceTva regleRetenueSourceTva);
}
