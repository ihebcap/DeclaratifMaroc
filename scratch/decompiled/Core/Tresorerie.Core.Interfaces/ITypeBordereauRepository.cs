using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ITypeBordereauRepository
{
	void AddModeReglement(TypeBordereau type, ModeReglement mode);

	void Create(TypeBordereau type);

	void Delete(TypeBordereau type);

	void DeleteModeReglement(TypeBordereau type, ModeReglement mode);

	TypeBordereau Get(int no);

	TypeBordereau Get(string code);

	IEnumerable<TypeBordereau> GetAll();

	bool IsTypeBordUsed(int typeNo);

	bool IsTypeConfigured(int typeNo);

	void Update(TypeBordereau type);
}
