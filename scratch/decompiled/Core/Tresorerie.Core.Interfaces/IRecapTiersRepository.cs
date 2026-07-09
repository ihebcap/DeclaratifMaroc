using System;
using System.Collections.Generic;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IRecapTiersRepository
{
	IEnumerable<RecapTiersDetailAffectation> GetAllRecapAffectationByDateDocument(int societeNo, ErpDomaine domaine, DateTime dateMin, DateTime dateMax);

	IEnumerable<RecapTiersDetailAffectation> GetAllRecapAffectationByDateReglement(int societeNo, ErpDomaine domaine, DateTime dateMin, DateTime dateMax);

	IEnumerable<RecapTiersDocument> GetAllRecapFacture(int societeNo, ErpDomaine domaine, DateTime dateMin, DateTime dateMax);

	IEnumerable<RecapTiersReglement> GetAllRecapReglement(int societeNo, MouvementDomaine domaine, DateTime dateMin, DateTime dateMax);

	IEnumerable<RecapTiersDocument> GetAllRecapImpaye(int societeNo, ErpDomaine domaine, DateTime dateMin, DateTime dateMax);

	Dictionary<int, decimal> GetTotalReglementSansImpayeAllTiers(int societeNo, MouvementDomaine domaine, DateTime dateMin, DateTime dateMax);
}
