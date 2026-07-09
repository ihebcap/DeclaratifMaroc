using System.Collections.Generic;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ILigneDeclarationTvaEncaissementRepository
{
	LigneDeclarationTvaEncaissement Get(int no);

	IEnumerable<LigneDeclarationTvaEncaissement> GetAll(int declarationNo);

	IEnumerable<LigneDeclarationTvaEncaissement> GetAll(LigneDeclarationTvaEncaissementEntityType type, int entityNo);

	int Create(LigneDeclarationTvaEncaissement ligne);

	void Update(LigneDeclarationTvaEncaissement ligne);

	void Delete(LigneDeclarationTvaEncaissement ligne);
}
