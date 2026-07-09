using System;
using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IDeclarationTvaEncaissementRepository
{
	DeclarationTvaEncaissement Get(int no);

	DeclarationTvaEncaissement Get(int societeNo, int exercice, DateTime dateDebut, DateTime dateFin);

	DeclarationTvaEncaissement Get(int societeNo, string numero);

	IEnumerable<DeclarationTvaEncaissement> GetAll(int societeNo);

	IEnumerable<DeclarationTvaEncaissement> GetAll(int societeNo, int exercice);

	int Create(DeclarationTvaEncaissement declaration);

	void Update(DeclarationTvaEncaissement declaration);

	void Delete(DeclarationTvaEncaissement declaration);

	bool HasLignes(int declarationNo);
}
