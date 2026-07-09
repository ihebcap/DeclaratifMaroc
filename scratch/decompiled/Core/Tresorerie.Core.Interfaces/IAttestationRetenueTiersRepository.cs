using System.Collections.Generic;
using System.Threading.Tasks;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IAttestationRetenueTiersRepository
{
	Task<AttestationRetenueTiers> GetAsync(int no);

	Task<AttestationRetenueTiers> GetAsync(int societeNo, int tiersNo, string numero);

	Task<IEnumerable<AttestationRetenueTiers>> GetAllAsync(int societeNo);

	Task<IEnumerable<AttestationRetenueTiers>> GetAllAsync(int societeNo, int tiersNo);

	Task<int> CreateAsync(AttestationRetenueTiers attestationRetenueTiers);

	Task UpdateAsync(AttestationRetenueTiers attestationRetenueTiers);

	Task DeleteAsync(AttestationRetenueTiers attestationRetenueTiers);

	Task<bool> IsAttestationUsedAsync(int attestationNo);
}
