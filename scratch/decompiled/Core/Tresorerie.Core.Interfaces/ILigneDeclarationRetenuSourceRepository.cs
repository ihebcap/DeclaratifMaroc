using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ILigneDeclarationRetenuSourceRepository
{
	LigneDeclarationRetenuSource Get(int no);

	LigneDeclarationRetenuSource Get(int retenueNo, int mouvementNo);

	IEnumerable<LigneDeclarationRetenuSource> GetAll(int declarationNo);

	int Create(LigneDeclarationRetenuSource ligne);

	void Delete(LigneDeclarationRetenuSource ligne);
}
