using System.Collections.Generic;
using System.Linq;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Declaration.Orchestration
{
    /// <summary>Implémentation SQL Server (table DM_SOLDE_INITIAL_TVA, base de persistance).</summary>
    public class SoldeInitialTvaRepository : ISoldeInitialTvaRepository
    {
        private sealed class Row
        {
            public int EC_Id { get; set; }
            public decimal Taux { get; set; }
            public decimal MontantTva { get; set; }
        }

        public IReadOnlyDictionary<int, SaisieSoldeInitialTva> GetSaisiesBatch(int soId, IEnumerable<int> ecIds, string persistenceConnectionString)
        {
            var result = new Dictionary<int, SaisieSoldeInitialTva>();
            var ids = ecIds.Where(id => id > 0).Distinct().ToList();
            if (ids.Count == 0 || string.IsNullOrEmpty(persistenceConnectionString)) return result;

            using var conn = new SqlConnection(persistenceConnectionString);
            conn.Open();
            var rows = conn.Query<Row>(
                "SELECT EC_Id, Taux, MontantTva FROM DM_SOLDE_INITIAL_TVA WHERE SO_Id = @SoId AND EC_Id IN @Ids",
                new { SoId = soId, Ids = ids });
            foreach (var r in rows)
                result[r.EC_Id] = new SaisieSoldeInitialTva { Taux = r.Taux, MontantTva = r.MontantTva };
            return result;
        }

        public void EnregistrerSaisie(int soId, int ecId, decimal taux, decimal montantTva, string saisiPar, string persistenceConnectionString)
        {
            using var conn = new SqlConnection(persistenceConnectionString);
            conn.Open();
            conn.Execute(@"
                IF EXISTS (SELECT 1 FROM DM_SOLDE_INITIAL_TVA WHERE SO_Id = @SoId AND EC_Id = @EcId)
                    UPDATE DM_SOLDE_INITIAL_TVA SET Taux = @Taux, MontantTva = @MontantTva, SaisiPar = @SaisiPar, SaisiLe = SYSUTCDATETIME()
                    WHERE SO_Id = @SoId AND EC_Id = @EcId
                ELSE
                    INSERT INTO DM_SOLDE_INITIAL_TVA (SO_Id, EC_Id, Taux, MontantTva, SaisiPar, SaisiLe)
                    VALUES (@SoId, @EcId, @Taux, @MontantTva, @SaisiPar, SYSUTCDATETIME())",
                new { SoId = soId, EcId = ecId, Taux = taux, MontantTva = montantTva, SaisiPar = saisiPar });
        }
    }
}
