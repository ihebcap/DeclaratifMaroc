using System.Threading.Tasks;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface ICommissionImpayeRepository
{
	Task<CommissionImpaye> GetAsync(int commissionNo);

	Task ComptabiliserAsync(int commissionNo);

	Task DecomptabiliserAsync(int commissionNo);
}
