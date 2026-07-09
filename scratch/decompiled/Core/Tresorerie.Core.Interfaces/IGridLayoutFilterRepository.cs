using System;
using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IGridLayoutFilterRepository
{
	void Create(GridLayoutFilter gridLayoutFilter);

	void Delete(string name, Guid guid);

	GridLayoutFilter Get(int no);

	GridLayoutFilter Get(string name, Guid guid);

	IEnumerable<GridLayoutFilter> GetAll(Guid guid);
}
