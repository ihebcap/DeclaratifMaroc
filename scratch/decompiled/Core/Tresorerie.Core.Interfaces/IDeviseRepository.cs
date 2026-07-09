using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IDeviseRepository
{
	int? Create(Devise devise);

	void Delete(Devise devise);

	Devise Get(int no);

	Devise Get(string code);

	IEnumerable<Devise> GetAll();

	void Update(Devise devise);
}
