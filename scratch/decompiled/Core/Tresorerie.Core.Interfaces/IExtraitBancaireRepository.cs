using System.Collections.Generic;
using System.Threading.Tasks;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IExtraitBancaireRepository
{
	Task<IEnumerable<ExtraitBancaire>> GetAllAsync(int societeNo);

	Task<IEnumerable<ExtraitBancaire>> GetAllByBanqueAsync(int informationsBanqueNo);

	Task<IEnumerable<LigneExtraitBancaire>> GetLignesAsync(int extraitBancaireNo);

	Task<ExtraitBancaire> GetAsync(int no);

	Task<ExtraitBancaire> GetAsync(int informationsBanqueNo, string reference);

	Task<int> CreateAsync(ExtraitBancaire extraitBancaire);

	Task DeleteAsync(ExtraitBancaire extraitBancaire);

	Task AddLinesAsync(IList<LigneExtraitBancaire> lignesExtrait);
}
