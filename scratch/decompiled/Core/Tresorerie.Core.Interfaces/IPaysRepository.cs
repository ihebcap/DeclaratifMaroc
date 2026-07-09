using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IPaysRepository
{
	void Create(Pays pays);

	void Delete(Pays pays);

	IList<Pays> GetAll();

	void Update(Pays pays);

	Pays Get(string code);

	Pays Get(int id);
}
