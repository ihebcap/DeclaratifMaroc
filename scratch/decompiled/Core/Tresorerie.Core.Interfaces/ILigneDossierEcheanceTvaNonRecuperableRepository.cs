using System.Collections.Generic;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ILigneDossierEcheanceTvaNonRecuperableRepository
{
	LigneDossierEcheanceTvaNonRecuperable Get(int no);

	LigneDossierEcheanceTvaNonRecuperable Get(int echeanceNo, int retenueNo, string codeTaxe);

	List<LigneDossierEcheanceTvaNonRecuperable> GetAllByEcheance(int echeanceNo);

	List<LigneDossierEcheanceTvaNonRecuperable> GetAllByRetenue(int retenueNo);

	int Create(LigneDossierEcheanceTvaNonRecuperable ligne);

	void Delete(LigneDossierEcheanceTvaNonRecuperable ligne);
}
