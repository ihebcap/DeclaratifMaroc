using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IEcartEchangeRepository
{
	void Comptabiliser(int ecartNo, EtatComptabilite etatComptabilite);

	EcartEchange Get(int no);

	IEnumerable<EcartEchange> GetAll(int societeNo, params EcheanceType[] types);

	IEnumerable<EcartEchange> GetAll(DateTime dateDebut, DateTime dateFin, int societeNo, EtatComptabilite etat, params EcheanceType[] types);

	Task<IEnumerable<EcartEchange>> GetAllAComptaAsync(DateTime dateMin, DateTime dateMax, EtatComptabilite comptabilite, EcheanceType[] types, int societeNo, CancellationToken cancellationToken);
}
