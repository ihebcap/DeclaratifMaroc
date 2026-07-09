using System.Threading.Tasks;
using Tresorerie.Core.Models;

namespace Tresorerie.Core.Interfaces;

public interface IInteretImpayeRepository
{
	Task<InteretImpaye> GetAsync(int no);

	Task ComptabiliserAsync(int interetNo);

	Task DecomptabiliserAsync(int interetNo);
}
