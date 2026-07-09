using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ITypeCautionRepository
{
	void Create(TypeCaution typeCaution);

	void Delete(TypeCaution typeCaution);

	TypeCaution Get(int no);

	TypeCaution Get(string code);

	IList<TypeCaution> GetAll();

	bool IsUsedType(int no);

	void Update(TypeCaution typeCaution);
}
