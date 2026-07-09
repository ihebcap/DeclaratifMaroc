using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ILigneRappelRepository
{
	int Create(LigneRappel ligne);

	void Delete(LigneRappel ligne);

	bool EcheanceInclueRappelNonCloture(int echeanceNo);

	LigneRappel Get(int ligneId);

	LigneRappel Get(int rappelNo, int echeanceNo);

	IEnumerable<LigneRappel> GetAll(int rappelNo);

	IEnumerable<LigneRappel> GetAllLigneRappel(int societeNo);

	void Update(LigneRappel ligne);
}
