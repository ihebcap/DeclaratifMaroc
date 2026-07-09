using System.Collections.Generic;
using SageTaxReader.Contracts;

namespace Declaration.Orchestration
{
    /// <summary>
    /// Entrée du cache : un bucket TVA pour une facture Sage (EC_Type=0).
    /// </summary>
    public class VentilationSageCacheEntry
    {
        public int EC_Id { get; set; }
        public double Taux { get; set; }
        public double BaseHT { get; set; }
        public double MontantTva { get; set; }
        public double TTC { get; set; }
        public string CodeTaxe { get; set; } = "";

        // Totaux de la facture
        public double TotalHT { get; set; }
        public double TotalTva { get; set; }
        public double TotalTtc { get; set; }

        // Token de paiement
        public int Token_MV_Id { get; set; }
        public int Token_MV_Point { get; set; }
    }

    /// <summary>
    /// Résultat de la validation du paiement local.
    /// </summary>
    public class PaiementToken
    {
        public int MV_Id { get; set; }
        public int MV_Point { get; set; }
    }

    /// <summary>
    /// Repository du cache des ventilations Sage.
    /// Toutes les opérations se font sur la base de persistance (jamais Sage).
    /// La validation du paiement se fait sur la connexion GRF (RT_AFFECTATION/RT_MOUVEMENT).
    /// </summary>
    public interface IVentilationSageCacheRepository
    {
        /// <summary>
        /// Récupère toutes les entrées de cache pour une facture donnée.
        /// Retourne une liste vide si la facture n'est pas en cache.
        /// </summary>
        IReadOnlyList<VentilationSageCacheEntry> GetEntries(int ecId, string persistenceConnectionString);

        /// <summary>
        /// Récupère le token de paiement courant depuis la connexion GRF locale
        /// (RT_AFFECTATION + RT_MOUVEMENT, mêmes critères que la sélection d'éligibilité).
        /// Retourne null si la facture n'est plus payée (affectation absente ou MV_Point != 1).
        /// </summary>
        PaiementToken? GetCurrentPaiementToken(int ecId, string grfConnectionString);

        /// <summary>
        /// Écrit les entrées de cache pour une facture (upsert idempotent).
        /// Doit être appelé uniquement après une lecture OM réussie d'une facture payée.
        /// </summary>
        void UpsertEntries(IEnumerable<VentilationSageCacheEntry> entries, string persistenceConnectionString);
    }
}
