using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ITypeDepenseRepository
{
	int Create(TypeDepense depense);

	void Delete(TypeDepense depense);

	TypeDepense Get(int no);

	TypeDepense Get(string code, int societeNo);

	IEnumerable<TypeDepense> GetAll(int societeNo);

	IEnumerable<TypeDepense> GetByCode(string code, int societeNo);

	void Update(TypeDepense depense);

	bool VerifDepenseUSed(int id);
}
