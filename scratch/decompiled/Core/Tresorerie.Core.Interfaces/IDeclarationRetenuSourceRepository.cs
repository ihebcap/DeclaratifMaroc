using System;
using System.Collections.Generic;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IDeclarationRetenuSourceRepository
{
	DeclarationRetenuSource Get(int no);

	DeclarationRetenuSource Get(int societeNo, int exercice, DateTime dateDebut, DateTime dateFin);

	DeclarationRetenuSource Get(int societeNo, string numero);

	DeclarationRetenuSource Get(int societeNo, int exercice, DeclarationMoisPeriode periode);

	IEnumerable<DeclarationRetenuSource> GetAll(int societeNo);

	IEnumerable<DeclarationRetenuSource> GetAll(int societeNo, int exercice);

	int Create(DeclarationRetenuSource declaration);

	void Update(DeclarationRetenuSource declaration);

	void Delete(DeclarationRetenuSource declaration);

	bool HasLignes(int declarationNo);

	bool HasDeclarationPosterieur(int societeNo, int declarationNo);
}
