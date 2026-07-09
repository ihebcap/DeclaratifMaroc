using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IGarentieRepository
{
	Garantie Get(int no);

	Garantie Get(int societeNo, string code);

	List<Garantie> GetAll(int societeNo);

	int Create(Garantie garentie);

	void Update(Garantie garentie);

	void Delete(Garantie garentie);
}
