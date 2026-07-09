using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ILigneDeclarationDelaisPaiementRepository
{
	LigneDeclarationDelaisPaiement Get(int no);

	IEnumerable<LigneDeclarationDelaisPaiement> GetAll(int declarationNo);

	int Create(LigneDeclarationDelaisPaiement ligne);

	void Delete(LigneDeclarationDelaisPaiement ligne);
}
