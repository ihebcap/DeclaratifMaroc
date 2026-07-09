using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ITypePrevisionRepository
{
	bool CanDeleted(int no);

	int Create(TypePrevision type);

	void Delete(TypePrevision type);

	TypePrevision Get(int no);

	TypePrevision Get(string code, int societNo);

	IEnumerable<TypePrevision> GetAll(int societeNo);

	void Update(int no, string intitule, string compteGeneral);
}
