using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ITransfertRepository
{
	int Create(Transfert transfert);

	void Delete(Transfert transfert);

	Transfert Get(int no);

	Transfert Get(int societeNo, string numero);

	IEnumerable<Transfert> GetAll(int societeNo);

	IEnumerable<Transfert> GetAll(int[] caissesNo);

	void Update(Transfert transfert);

	bool Exist(int transfertNo);

	Task<IEnumerable<Transfert>> GetAllAComptaAsync(DateTime dateMin, DateTime dateMax, EtatComptabilite etat, int[] caissesDestinataireNo, int[] caissesNo, int[] modesNo, int societeNo, CancellationToken cancellationToken);
}
