using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IEcartRepository
{
	void Comptabiliser(int ecartNo);

	void Decomptabiliser(int ecartNo);

	IEnumerable<Ecart> GetAllGainPerte(int societeNo, ErpDomaine domaine);

	IEnumerable<Ecart> GetAllGainPerte(DateTime dateDebut, DateTime dateFin, EtatComptabilite etat, int societeNo);

	IEnumerable<Ecart> GetAllGainPerte(int societeNo, ErpDomaine domaine, params int[] souchesNo);

	Ecart GetByNo(int no, int societeNo);

	Task<IEnumerable<Ecart>> GetAllAComptaAsync(ErpDomaine domaine, DateTime dateMin, DateTime dateMax, EtatComptabilite comptabilite, EcheanceType[] types, int societeNo, CancellationToken cancellationToken);
}
