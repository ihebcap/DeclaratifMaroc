using System.Collections.Generic;
using System.Threading.Tasks;
using Tresorerie.Core.Enum;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ILettreRecouvrementFieldsRepository
{
	Task<LettreRecouvrementFields> GetAsync(int societeNo, LettreRecouvrementFieldValue field);

	Task<IEnumerable<LettreRecouvrementFields>> GetAllAsync(int societeNo);

	Task CreateAsync(LettreRecouvrementFields field);

	Task UpdateAsync(LettreRecouvrementFields field);
}
