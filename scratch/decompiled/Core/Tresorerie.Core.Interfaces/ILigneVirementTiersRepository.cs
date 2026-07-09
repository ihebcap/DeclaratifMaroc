using System.Collections.Generic;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ILigneVirementTiersRepository
{
	IEnumerable<LigneVirementTiers> GetAll(TiersType typeTiers, int virementNo, int societeNo);

	IEnumerable<LigneVirementTiers> GetAllLigneClient(int societeNo);

	IEnumerable<LigneVirementTiers> GetAllLigneClient(int societeNo, int[] souchesNo);

	void VirClientUpdateEtatCompta(int no, EtatComptabilite etat);
}
