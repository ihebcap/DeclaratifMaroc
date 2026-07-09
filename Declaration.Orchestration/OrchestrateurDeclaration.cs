using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Declaration.Core;
using Declaration.Core.Model;
using SageTaxReader.Contracts;

namespace Declaration.Orchestration
{
    public class OrchestrateurDeclaration
    {
        private readonly IWorkerInvoker _invoker;
        private readonly WorkerConfig _config;

        private readonly ILecteurTvaFgr _lecteurFgr;
        private readonly string _connectionString;      // connexion GRF (RT_*)
        private readonly string _sageConnectionString;
        private readonly string _persistenceConnectionString; // base dédiée (cache)

        private readonly IVentilationSageCacheRepository _ventilationCache;

        public OrchestrateurDeclaration(
            IWorkerInvoker invoker,
            WorkerConfig config,
            ILecteurTvaFgr lecteurFgr,
            string connectionString,
            string sageConnectionString,
            IVentilationSageCacheRepository? ventilationCache = null,
            string persistenceConnectionString = "")
        {
            _invoker = invoker;
            _config = config;
            _lecteurFgr = lecteurFgr;
            _connectionString = connectionString;
            _sageConnectionString = sageConnectionString;
            _ventilationCache = ventilationCache ?? new VentilationSageCacheRepository();
            _persistenceConnectionString = persistenceConnectionString;
        }

        public DeclarationModele Traiter(IEnumerable<AffectationADeclarer> affectations, int n)
        {
            var affectationsList = affectations.ToList();

            // ── Batch OM : uniquement les EC_Type=0 non présents/valides en cache SQL ─────
            var sageAffectations = affectationsList.Where(a => a.EC_Type == 0).ToList();

            // Pré-résolution depuis le cache SQL (1 requête par EC_Id distinct)
            var cachedDocs = new Dictionary<int, DocumentTaxesInfo?>();
            var ecIdsToFetch = new HashSet<int>();

            foreach (var a in sageAffectations)
            {
                if (a.EC_Id <= 0 || ecIdsToFetch.Contains(a.EC_Id) || cachedDocs.ContainsKey(a.EC_Id))
                    continue;

                var doc = TryServireDepuisCache(a.EC_Id);
                if (doc != null)
                    cachedDocs[a.EC_Id] = doc;
                else
                    ecIdsToFetch.Add(a.EC_Id);
            }

            // Pour les factures non en cache (ou cache invalide) → batch OM par NumeroFacture
            var requetes = sageAffectations
                .Where(a => a.EC_Id <= 0 || ecIdsToFetch.Contains(a.EC_Id))
                .Select(a => (a.NumeroFacture, a.Sens == SensAffectation.Vente ? "Vente" : "Achat"))
                .Distinct()
                .ToList();

            // in-memory cache OM (par NumeroFacture/Sens) — existant TASK-023
            var omCache = new ConcurrentDictionary<(string Numero, SensAffectation Sens), DocumentTaxesInfo?>();

            if (requetes.Any())
            {
                try
                {
                    var resultats = _invoker.InvoquerWorkerBatch(requetes, _config);
                    foreach (var res in resultats)
                    {
                        var sens = res.Sens.Equals("Vente", StringComparison.OrdinalIgnoreCase)
                            ? SensAffectation.Vente
                            : SensAffectation.Achat;
                        omCache[(res.NumeroPiece, sens)] = res;
                    }
                }
                catch (Exception)
                {
                    // Fallback to individual (même comportement qu'avant)
                }
            }

            // ── Matérialisation : écriture du cache SQL pour les factures lues OM ─────────
            // Uniquement si une connexion de persistance est configurée ET si EC_Id est connu
            // et que la facture est payée (token local non null).
            if (!string.IsNullOrEmpty(_persistenceConnectionString))
            {
                foreach (var a in sageAffectations.Where(x => ecIdsToFetch.Contains(x.EC_Id) && x.EC_Id > 0))
                {
                    var key = (a.NumeroFacture, a.Sens);
                    if (!omCache.TryGetValue(key, out var doc) || doc == null) continue;

                    // Ne pas réécrire si déjà matérialisé pour cet EC_Id dans cette passe
                    if (cachedDocs.ContainsKey(a.EC_Id)) continue;

                    // Vérifier que la facture est bien payée (token local)
                    var token = TryGetToken(a.EC_Id);
                    if (token == null) continue;

                    var entries = BuildEntries(a.EC_Id, doc, token);
                    try
                    {
                        _ventilationCache.UpsertEntries(entries, _persistenceConnectionString);
                    }
                    catch (Exception)
                    {
                        // Écriture du cache best-effort : si elle échoue, on continue sans cache
                    }
                }
            }

            // ── Résolveur unifié ───────────────────────────────────────────────────────────
            Func<AffectationADeclarer, DocumentTaxesInfo?> resoudreFacture = (affectation) =>
            {
                if (affectation.EC_Type == 4)
                    return null; // Alerte gérée dans ConstructeurDeclaration

                if (affectation.EC_Type == 111)
                    return _lecteurFgr.LireTvaFgr(
                        affectation.EC_Id, affectation.NumeroFacture,
                        _connectionString, _sageConnectionString);

                // EC_Type == 0 : cache SQL validé sinon OM
                if (affectation.EC_Id > 0 && cachedDocs.TryGetValue(affectation.EC_Id, out var cached))
                    return cached;

                // Fallback OM (in-memory, puis individuel)
                var k = (affectation.NumeroFacture, affectation.Sens);
                if (omCache.TryGetValue(k, out var omDoc))
                    return omDoc;

                return omCache.GetOrAdd(k, key =>
                {
                    string sensStr = key.Sens == SensAffectation.Vente ? "Vente" : "Achat";
                    try { return _invoker.InvoquerWorker(key.Numero, sensStr, _config); }
                    catch (Exception) { return null; }
                });
            };

            return ConstructeurDeclaration.ConstruireDeclaration(affectationsList, resoudreFacture, n);
        }

        // ── Helpers privés ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Tente de servir la ventilation depuis le cache SQL.
        /// Retourne null si le cache est absent ou si le token de paiement ne correspond plus.
        /// </summary>
        private DocumentTaxesInfo? TryServireDepuisCache(int ecId)
        {
            if (string.IsNullOrEmpty(_persistenceConnectionString))
                return null;

            IReadOnlyList<VentilationSageCacheEntry> entries;
            try
            {
                entries = _ventilationCache.GetEntries(ecId, _persistenceConnectionString);
            }
            catch (Exception)
            {
                return null;
            }

            if (entries.Count == 0)
                return null;

            // Validation du paiement : token stocké == état courant local
            if (!string.IsNullOrEmpty(_connectionString))
            {
                PaiementToken? currentToken;
                try
                {
                    currentToken = _ventilationCache.GetCurrentPaiementToken(ecId, _connectionString);
                }
                catch (Exception)
                {
                    return null; // Impossible de valider → ne pas servir le cache
                }

                if (currentToken == null) return null; // Facture dépayée

                var storedToken = entries[0]; // tous les buckets ont le même token
                if (storedToken.Token_MV_Id != currentToken.MV_Id
                    || storedToken.Token_MV_Point != currentToken.MV_Point)
                    return null; // Token diverge → cache périmé
            }

            // Reconstruire DocumentTaxesInfo depuis les entrées de cache
            var first = entries[0];
            return new DocumentTaxesInfo
            {
                NumeroPiece = "", // non stocké en cache (pas nécessaire pour la ventilation)
                Sens = "",
                TotalHT = first.TotalHT,
                TotalTva = first.TotalTva,
                TotalTtc = first.TotalTtc,
                LignesTaxe = entries.Select(e => new TaxeDetail
                {
                    Taux = e.Taux,
                    BaseHT = e.BaseHT,
                    MontantTva = e.MontantTva,
                    TTC = e.TTC,
                    Code = e.CodeTaxe,
                    Type = "TaxeTypeTVA"
                }).ToList()
            };
        }

        private PaiementToken? TryGetToken(int ecId)
        {
            if (string.IsNullOrEmpty(_connectionString)) return null;
            try { return _ventilationCache.GetCurrentPaiementToken(ecId, _connectionString); }
            catch (Exception) { return null; }
        }

        private static IEnumerable<VentilationSageCacheEntry> BuildEntries(
            int ecId, DocumentTaxesInfo doc, PaiementToken token)
        {
            return doc.LignesTaxe.Select(l => new VentilationSageCacheEntry
            {
                EC_Id        = ecId,
                Taux         = l.Taux,
                BaseHT       = l.BaseHT,
                MontantTva   = l.MontantTva,
                TTC          = l.TTC,
                CodeTaxe     = l.Code,
                TotalHT      = doc.TotalHT,
                TotalTva     = doc.TotalTva,
                TotalTtc     = doc.TotalTtc,
                Token_MV_Id  = token.MV_Id,
                Token_MV_Point = token.MV_Point
            });
        }
    }
}
