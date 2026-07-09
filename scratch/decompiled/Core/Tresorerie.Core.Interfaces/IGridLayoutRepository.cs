using System;
using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IGridLayoutRepository
{
	void Create(GridLayout gridLayout);

	void Delete(GridLayout gridLayout);

	GridLayout Get(int no);

	GridLayout Get(string name, Guid gridGuid);

	IEnumerable<GridLayout> GetAll(Guid gridGuid);

	void Update(GridLayout gridLayout);
}
