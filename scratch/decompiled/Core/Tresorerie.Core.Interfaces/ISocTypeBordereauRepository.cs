using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ISocTypeBordereauRepository
{
	int? Create(EtapeCompta etape);

	void Delete(EtapeCompta etape);

	EtapeCompta Get(int typeBordereauNo, int societeNo, int order);

	EtapeCompta Get(int no);

	IEnumerable<SocieteTypeBordereau> GetAll(int societeNo);

	bool HasEtapeCompta(int societeNo, int typeBordereauNo, int order);

	void Update(EtapeCompta etape);
}
