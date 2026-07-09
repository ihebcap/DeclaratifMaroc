using System.Collections.Generic;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ITraiteRepository
{
	void Create(Traite traite);

	void Delete(Traite traite);

	Traite Get(int no);

	Traite Get(string numero, int carnetTraiteNo, int societeNo);

	IEnumerable<Traite> GetAll(int carnetTraiteNo);

	IEnumerable<Traite> GetAll(int carnetTraiteNo, int societeNo, params ChequeStatut[] statuts);

	void Reutiliser(int traiteNo);

	void Update(Traite traite);
}
