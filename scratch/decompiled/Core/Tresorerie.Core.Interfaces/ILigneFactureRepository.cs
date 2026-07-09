using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ILigneFactureRepository
{
	LigneFacture Get(int no);

	int Create(LigneFacture ligne);

	void Delete(LigneFacture ligne);

	void Update(LigneFacture ligne);

	List<LigneFacture> GetAll(int factureNo);
}
