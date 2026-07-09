using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IDossierImpayeRepository
{
	Task<DossierImpaye> GetAsync(int dossierNo);

	Task<DossierImpaye> GetAsync(string dossierNumero, ErpDomaine erpDomaine, int no);

	Task<DossierImpaye> GetByCommissionNoAsync(int commissionNo);

	Task<DossierImpaye> GetByInteretNoAsync(int interetNo);

	Task<IEnumerable<DossierImpaye>> GetAllAsync(int societeNo, ErpDomaine domaine);

	IEnumerable<DossierImpaye> GetAll(int societeNo, ErpDomaine domaine);

	Task<IEnumerable<DossierImpaye>> GetAllAsync(int societeNo, int[] caissesNo, ErpDomaine domaine);

	IEnumerable<DossierImpaye> GetAll(int societeNo, int[] caissesNo, ErpDomaine domaine);

	Task DeleteAsync(DossierImpaye dossier);

	Task UpdateEtatComptabiliteAsync(DossierImpaye dossier);

	Task UpdateEtatLettrageAsync(DossierImpaye dossier);

	Task<int> CreateAsync(DossierImpaye dossier);

	Task UpdateAsync(DossierImpaye dossier);

	Task DeleteCommission(DossierImpaye dossier);

	Task DeleteInteret(DossierImpaye dossier);

	Task<IEnumerable<DossierImpaye>> GetAllAComptaAsync(DateTime dateMin, DateTime dateMax, bool isComptabilise, int[] caissesNo, int societeNo, ErpDomaine domaine, CancellationToken cancellationToken);

	DossierImpaye Get(int dossierNo);

	void Update(DossierImpaye dossier);
}
