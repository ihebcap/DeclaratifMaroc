using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Declaration.Core.Model;

namespace Declaration.Selection
{
    public interface ISelectionnerAffectationsService
    {
        Task<IEnumerable<AffectationADeclarer>> SelectionnerAffectationsAsync(int soId, DateTime dateDebut, DateTime dateFin, string connectionString, int? dtIdToInclude = null);
    }
}
