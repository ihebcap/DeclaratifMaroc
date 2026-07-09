using System;
using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IDeclarationDelaisPaiementRepository
{
	DeclarationDelaisPaiementTiers Get(int no);

	DeclarationDelaisPaiementTiers Get(int societeNo, int exercice, DateTime dateDebut, DateTime dateFin);

	DeclarationDelaisPaiementTiers Get(int societeNo, string numero);

	IEnumerable<DeclarationDelaisPaiementTiers> GetAll(int societeNo);

	IEnumerable<DeclarationDelaisPaiementTiers> GetAll(int societeNo, int exercice);

	int Create(DeclarationDelaisPaiementTiers declaration);

	void Update(DeclarationDelaisPaiementTiers declaration);

	void Delete(DeclarationDelaisPaiementTiers declaration);

	bool HasLignes(int declarationNo);
}
