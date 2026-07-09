using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Declaration.Core.Model;

namespace Declaration.Selection
{
    public interface ISelectionExpliqueeService
    {
        Task<IEnumerable<AffectationCandidate>> SelectionnerExpliqueeAsync(
            int soId, 
            DateTime dateDebut, 
            DateTime dateFin, 
            string connectionString,
            Func<string, SensAffectation, Task<(bool Existe, bool TaxeOk)>>? verifierFacture = null);
    }
}
