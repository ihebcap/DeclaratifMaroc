using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ITypeCreditRepository
{
	TypeCredit Get(int no);

	TypeCredit Get(int societeNo, string code);

	List<TypeCredit> GetAll(int societeNo);

	int Create(TypeCredit typeCredit);

	void Update(TypeCredit typeCredit);

	void Delete(TypeCredit typeCredit);
}
