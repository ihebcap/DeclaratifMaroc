using System.Collections.Generic;
using System.Threading.Tasks;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IVentilationAnalytiqueRepository
{
	Task<VentilationAnalytique> GetAsync(int ligneNo);

	Task<IEnumerable<VentilationAnalytique>> GetAllAsync(int societeNo, int entityNo, AnalytiqueDomaine domaine);

	Task<IEnumerable<VentilationAnalytique>> GetAllAsync(int societeNo, int planNo, int entityNo, AnalytiqueDomaine domaine);

	Task<IEnumerable<VentilationAnalytique>> GetAllAsync(int societeNo, int planNo, int entityNo, AnalytiqueDomaine domaine, string compteNum);

	Task<IEnumerable<VentilationAnalytique>> GetAllAsync(int societeNo, string compteNumero, int entityNo, AnalytiqueDomaine domaine);

	Task DeleteAsync(int ligneNo);

	Task DeleteByEntityAsync(int societeNo, int entityNo, AnalytiqueDomaine domaine);

	void DeleteByEntity(int societeNo, int entityNo, AnalytiqueDomaine domaine);

	Task CreateAsync(VentilationAnalytique ventilation);

	Task UpdateAsync(int ligneNo, decimal montant);
}
