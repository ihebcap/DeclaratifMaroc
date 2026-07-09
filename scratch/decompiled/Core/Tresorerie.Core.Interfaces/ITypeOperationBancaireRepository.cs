using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ITypeOperationBancaireRepository
{
	bool CanDeleted(int no);

	int Create(TypeOperationBancaire type);

	void Delete(TypeOperationBancaire type);

	TypeOperationBancaire Get(int no);

	TypeOperationBancaire Get(string code, int societNo);

	IEnumerable<TypeOperationBancaire> GetAll(int societeNo);

	void Update(TypeOperationBancaire type);

	TypeOperationBancaire Get(int societNo, string compteGeneral);
}
