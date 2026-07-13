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

        // Token de paiement — NULL tant que la facture n'est rattachée à aucun
        // règlement pointé : la lecture OM est alors mise en cache « brute »
        // (réutilisable pour l'affichage), mais jamais servie comme ventilation
        // déclarable tant qu'un token réel n'est pas présent.
        public int? Token_MV_Id { get; set; }
        public int? Token_MV_Point { get; set; }

        // TASK-072 : renseigné uniquement sur la ligne sentinelle d'erreur (CodeTaxe="ERREUR",
        // Taux=-1) — jamais sur une ligne de ventilation réelle.
        public string? MotifErreur { get; set; }

        // TASK-076 : montants bruts Sage tels que lus au moment de la détection d'incohérence
        // (AVANT exclusion), portés uniquement par la ligne sentinelle d'erreur — jamais sur
        // une ligne de ventilation réelle. Aucune valeur inventée : NULL si non capturée (ex.
        // revalidation rétroactive d'une ligne de cache ancienne, où le parafiscal brut n'était
        // pas conservé séparément). Sert l'investigation manuelle côté ERP sur l'écran Factures.
        public double? BrutHT { get; set; }
        public double? BrutTva { get; set; }
        public double? BrutParafiscale { get; set; }
        public double? BrutTtc { get; set; }
    }

    /// <summary>
    /// TASK-076 : montants bruts Sage capturés au moment de la détection d'une incohérence
    /// HT/TVA/TTC — AVANT que la ligne ne soit exclue et remplacée par la sentinelle d'erreur.
    /// Chaque champ est nullable et n'est jamais recalculé/estimé : NULL si non disponible au
    /// point d'appel (ex. revalidation rétroactive depuis un cache ancien ne portant pas le
    /// parafiscal séparément).
    /// </summary>
    public class MontantsBrutsErreur
    {
        public double? TotalHTNet { get; set; }
        public double? TotalTva { get; set; }
        public double? TotalParafiscale { get; set; }
        public double? TotalTtc { get; set; }
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
        /// Appelé après une lecture OM réussie. Le token de paiement peut être NULL
        /// (facture lue mais non encore rattachée à un règlement pointé) : la
        /// ventilation « brute » est alors conservée mais non déclarable.
        /// </summary>
        void UpsertEntries(IEnumerable<VentilationSageCacheEntry> entries, string persistenceConnectionString);

        /// <summary>
        /// TASK-072 : montant en devise de l'échéance GRF (RT_ECHEANCE.EC_MtDevise) pour un
        /// EC_Id donné — source indépendante de la lecture Sage (OM/FGR), utilisée pour
        /// contrôler que le TTC lu côté Sage correspond bien au TTC connu côté GRF.
        /// Retourne null si l'échéance est introuvable (le contrôle est alors ignoré,
        /// jamais bloquant sur une absence de donnée).
        /// </summary>
        decimal? GetEcheanceMontantDevise(int ecId, string grfConnectionString);

        /// <summary>
        /// TASK-072 : marque une facture comme définitivement en erreur (incohérence Sage
        /// détectée — HT+TVA≠TTC ou TTC Sage≠RT_ECHEANCE.EC_MtDevise). Purge toute ventilation
        /// réelle précédemment mise en cache pour cet EC_Id et la remplace par une ligne
        /// sentinelle unique (Taux=-1, CodeTaxe="ERREUR") portant le motif exact — jamais
        /// mélangée avec des buckets TVA réels. Rend le motif exploitable par l'écran Factures
        /// (GET /factures, famille B) sans avoir à relire l'OM à chaque rafraîchissement.
        ///
        /// TASK-076 : <paramref name="montantsBruts"/> (optionnel) porte les montants Sage tels
        /// que lus au moment de la détection, AVANT exclusion — persistés sur la ligne sentinelle
        /// pour investigation manuelle côté ERP sur l'écran Factures. NULL = non disponible au
        /// point d'appel (jamais inventé).
        /// </summary>
        void MarquerEnErreur(int ecId, string motif, string persistenceConnectionString,
            MontantsBrutsErreur? montantsBruts = null);

        /// <summary>
        /// TASK-078 : supprime toute entrée de cache pour cet EC_Id (sentinelle ERREUR ou buckets
        /// réels), sans rien réécrire. Utilisé avant une resynchronisation explicite demandée par
        /// l'utilisateur : sans cette purge, une sentinelle qui porte déjà des montants bruts
        /// capturés (TASK-076) ferait considérer <c>TryServireDepuisCache</c> la pièce comme
        /// « déjà réglée » et ne relirait jamais Sage, même après correction côté ERP.
        /// </summary>
        void SupprimerEntrees(int ecId, string persistenceConnectionString);
    }
}
