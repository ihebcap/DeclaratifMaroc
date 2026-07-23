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

        // TASK-118 : société (SO_Id) traitée par cette instance — scope le cache de ventilation
        // Sage (DM_VENTILATION_SAGE_CACHE) pour éviter toute collision d'EC_Id entre deux bases
        // Sage physiquement distinctes. Défaut 0 = comportement historique mono-Sage (tous les
        // appelants antérieurs à TASK-118, notamment les tests unitaires avec des connexions
        // factices, n'ont qu'une seule société implicite).
        private readonly int _soId;

        // Journal optionnel : chaque échec worker/cache y est écrit (jamais avalé en silence).
        private readonly Action<string>? _log;

        public OrchestrateurDeclaration(
            IWorkerInvoker invoker,
            WorkerConfig config,
            ILecteurTvaFgr lecteurFgr,
            string connectionString,
            string sageConnectionString,
            IVentilationSageCacheRepository? ventilationCache = null,
            string persistenceConnectionString = "",
            Action<string>? log = null,
            int soId = 0)
        {
            _invoker = invoker;
            _config = config;
            _lecteurFgr = lecteurFgr;
            _connectionString = connectionString;
            _sageConnectionString = sageConnectionString;
            _ventilationCache = ventilationCache ?? new VentilationSageCacheRepository();
            _persistenceConnectionString = persistenceConnectionString;
            _log = log;
            _soId = soId;
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
                _log?.Invoke($"[VALO] batch OM : {requetes.Count} pièce(s) EC_Type=0 à lire.");
                try
                {
                    var resultats = _invoker.InvoquerWorkerBatch(requetes, _config, _log);
                    foreach (var res in resultats)
                    {
                        var sens = res.Sens.Equals("Vente", StringComparison.OrdinalIgnoreCase)
                            ? SensAffectation.Vente
                            : SensAffectation.Achat;
                        omCache[(res.NumeroPiece, sens)] = res;
                    }
                    _log?.Invoke($"[VALO] batch OM rendu : {resultats.Count} pièce(s), "
                        + $"{resultats.Count(r => r.EnErreur)} en erreur.");

                    // Pièces demandées mais jamais rendues par le batch : ne jamais les perdre en silence.
                    foreach (var req in requetes)
                    {
                        var sens = req.Item2.Equals("Vente", StringComparison.OrdinalIgnoreCase)
                            ? SensAffectation.Vente : SensAffectation.Achat;
                        if (!omCache.ContainsKey((req.Item1, sens)))
                            _log?.Invoke($"[VALO] pièce {req.Item1} ({req.Item2}) absente du résultat batch (non rendue par le worker).");
                    }
                }
                catch (Exception ex)
                {
                    // Fallback to individual (même comportement qu'avant) — mais on trace la cause.
                    _log?.Invoke($"[VALO] batch OM en exception, repli individuel : {ex.Message}");
                }

                // TASK-159 : repli individuel PARALLÉLISÉ (borné), limité aux seules pièces
                // encore absentes d'omCache après le batch (rescue partiel inclus) — jamais celles
                // déjà rendues. Degré de parallélisme (4) mesuré en conditions réelles : 5 process
                // SageTaxReader.Console.exe concurrents contre la même base Sage ont tous abouti
                // sans erreur (session/licence), marge conservée sous ce seuil vérifié plutôt que
                // de le saturer. Si le repli séquentiel classique (resoudreFactureBrute) rencontre
                // encore une clé manquante ensuite (cas résiduel), il reste inchangé en filet de
                // sécurité.
                var manquantes = requetes.Where(req =>
                {
                    var sens = req.Item2.Equals("Vente", StringComparison.OrdinalIgnoreCase)
                        ? SensAffectation.Vente : SensAffectation.Achat;
                    return !omCache.ContainsKey((req.Item1, sens));
                }).ToList();

                if (manquantes.Any())
                {
                    _log?.Invoke($"[VALO] repli individuel parallélisé (degré 4) sur {manquantes.Count} pièce(s) manquante(s).");
                    System.Threading.Tasks.Parallel.ForEach(
                        manquantes,
                        new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 4 },
                        req =>
                        {
                            var sens = req.Item2.Equals("Vente", StringComparison.OrdinalIgnoreCase)
                                ? SensAffectation.Vente : SensAffectation.Achat;
                            DocumentTaxesInfo? doc = null;
                            try
                            {
                                doc = _invoker.InvoquerWorker(req.Item1, req.Item2, _config, _log);
                            }
                            catch (Exception ex)
                            {
                                _log?.Invoke($"[VALO] lecture OM individuelle {req.Item1} ({req.Item2}) en exception : {ex.Message}");
                            }
                            omCache[(req.Item1, sens)] = doc;
                        });
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

                    // Ne jamais matérialiser une pièce isolée en erreur (TASK-023) — motif tracé.
                    // TASK-072 : le motif est en plus persisté (ligne sentinelle) pour rester
                    // visible sur l'écran Factures sans relire l'OM à chaque rafraîchissement.
                    if (doc.EnErreur)
                    {
                        _log?.Invoke($"[VALO] EC_Id={a.EC_Id} pièce {a.NumeroFacture} non mise en cache — "
                            + $"OM en erreur : {doc.MotifErreur}");
                        try
                        {
                            // TASK-076 : montants bruts Sage tels que lus par SageTaxReaderService AVANT
                            // exclusion — capturés ici (jamais recalculés), uniquement si réellement
                            // disponibles (une pièce en échec de lecture pur n'a aucun montant lu).
                            var montantsBruts = doc.MontantsBrutsDisponibles
                                ? new MontantsBrutsErreur
                                {
                                    TotalHTNet = doc.TotalHTNet,
                                    TotalTva = doc.TotalTva,
                                    TotalParafiscale = doc.TotalParafiscale,
                                    TotalTtc = doc.TotalTtc
                                }
                                : null;
                            _ventilationCache.MarquerEnErreur(_soId, a.EC_Id, doc.MotifErreur, _persistenceConnectionString, montantsBruts);
                        }
                        catch (Exception ex)
                        {
                            _log?.Invoke($"[VALO] EC_Id={a.EC_Id} — échec écriture sentinelle d'erreur : {ex.Message}");
                        }
                        continue;
                    }

                    // Ne pas réécrire si déjà matérialisé pour cet EC_Id dans cette passe
                    if (cachedDocs.ContainsKey(a.EC_Id)) continue;

                    // On met en cache TOUTES les factures lues, même sans paiement pointé :
                    // la lecture OM (taux/base TVA) est propre à la facture et indépendante
                    // du règlement — la re-lire plus tard serait du travail perdu.
                    // Le token de paiement (peut être NULL) sert de garde d'invalidation :
                    // une ligne à token NULL est conservée mais jamais servie comme
                    // ventilation déclarable (voir TryServireDepuisCache).
                    var token = TryGetToken(a.EC_Id);
                    if (token == null)
                    {
                        _log?.Invoke($"[VALO] EC_Id={a.EC_Id} pièce {a.NumeroFacture} mise en cache brute — "
                            + "aucun paiement pointé (token MV NULL, non déclarable).");
                    }

                    var entries = BuildEntries(_soId, a.EC_Id, doc, token);
                    try
                    {
                        _ventilationCache.UpsertEntries(entries, _persistenceConnectionString);
                    }
                    catch (Exception ex)
                    {
                        // Écriture du cache best-effort : si elle échoue, on continue — mais on trace.
                        _log?.Invoke($"[VALO] EC_Id={a.EC_Id} pièce {a.NumeroFacture} — échec écriture cache : {ex.Message}");
                    }
                }
            }

            // ── Matérialisation FGR (EC_Type=111) : lecture SQL directe (RT_HISTCOMPTA), ────
            // jamais via le batch OM ci-dessus (celui-ci ne prend que sageAffectations,
            // EC_Type=0). Sans ce bloc, une facture FGR (ex. FF260076) n'est jamais écrite
            // dans DM_VENTILATION_SAGE_CACHE malgré une lecture facture-first correcte —
            // le lecteur FGR n'était appelé qu'au moment de la déclaration finale
            // (resoudreFacture), jamais pour la matérialisation du cache.
            if (!string.IsNullOrEmpty(_persistenceConnectionString))
            {
                var fgrAffectations = affectationsList.Where(a => a.EC_Type == 111 && a.EC_Id > 0).ToList();
                var ecIdsFgrTraites = new HashSet<int>();

                foreach (var a in fgrAffectations)
                {
                    if (!ecIdsFgrTraites.Add(a.EC_Id)) continue;

                    // Déjà servi par le cache SQL (token valide) → rien à réécrire.
                    if (TryServireDepuisCache(a.EC_Id) != null) continue;

                    DocumentTaxesInfo doc;
                    try
                    {
                        var lu = _lecteurFgr.LireTvaFgr(a.EC_Id, a.NumeroFacture, _connectionString, _sageConnectionString);
                        if (lu == null)
                        {
                            _log?.Invoke($"[VALO] EC_Id={a.EC_Id} pièce {a.NumeroFacture} non mise en cache — lecture FGR vide.");
                            continue;
                        }
                        doc = lu;
                    }
                    catch (Exception ex)
                    {
                        _log?.Invoke($"[VALO] EC_Id={a.EC_Id} pièce {a.NumeroFacture} non mise en cache — FGR en erreur : {ex.Message}");
                        continue;
                    }

                    var token = TryGetToken(a.EC_Id);
                    if (token == null)
                    {
                        _log?.Invoke($"[VALO] EC_Id={a.EC_Id} pièce {a.NumeroFacture} mise en cache brute — "
                            + "aucun paiement pointé (token MV NULL, non déclarable).");
                    }

                    var entries = BuildEntries(_soId, a.EC_Id, doc, token);
                    try
                    {
                        _ventilationCache.UpsertEntries(entries, _persistenceConnectionString);
                    }
                    catch (Exception ex)
                    {
                        _log?.Invoke($"[VALO] EC_Id={a.EC_Id} pièce {a.NumeroFacture} — échec écriture cache : {ex.Message}");
                    }
                }
            }

            // ── Résolveur unifié ───────────────────────────────────────────────────────────
            Func<AffectationADeclarer, DocumentTaxesInfo?> resoudreFactureBrute = (affectation) =>
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
                    try { return _invoker.InvoquerWorker(key.Numero, sensStr, _config, _log); }
                    catch (Exception ex)
                    {
                        _log?.Invoke($"[VALO] lecture OM individuelle {key.Numero} ({sensStr}) en exception : {ex.Message}");
                        return null;
                    }
                });
            };

            // TASK-072 : contrôle croisé TTC Sage (OM/FGR/cache) vs TTC GRF (RT_ECHEANCE.EC_MtDevise).
            // Détecte une facture Sage dont le TTC ne correspond plus au montant d'échéance connu
            // côté GRF (cause racine identifiée : en-tête Sage incohérent, ex. FC2501717 — HT >> TTC).
            // Une seule requête par EC_Id (mémoïsée), jamais bloquante si l'échéance est introuvable.
            var echeanceMontantParEcId = new Dictionary<int, decimal?>();

            Func<AffectationADeclarer, DocumentTaxesInfo?> resoudreFacture = (affectation) =>
            {
                var doc = resoudreFactureBrute(affectation);
                if (doc == null || doc.EnErreur || affectation.EC_Id <= 0 || string.IsNullOrEmpty(_connectionString))
                    return doc;

                if (!echeanceMontantParEcId.TryGetValue(affectation.EC_Id, out var mtDevise))
                {
                    try
                    {
                        mtDevise = _ventilationCache.GetEcheanceMontantDevise(affectation.EC_Id, _connectionString);
                    }
                    catch (Exception ex)
                    {
                        _log?.Invoke($"[VALO] EC_Id={affectation.EC_Id} — échec lecture RT_ECHEANCE.EC_MtDevise (contrôle croisé ignoré) : {ex.Message}");
                        mtDevise = null;
                    }
                    echeanceMontantParEcId[affectation.EC_Id] = mtDevise;
                }

                if (mtDevise.HasValue && Math.Abs((double)mtDevise.Value - doc.TotalTtc) > 0.01)
                {
                    doc.EnErreur = true;
                    doc.MotifErreur = $"Incohérence TTC : Sage={doc.TotalTtc:F2} ≠ RT_ECHEANCE.EC_MtDevise={mtDevise.Value:F2} "
                        + $"(EC_Id={affectation.EC_Id}) — pièce exclue de la valorisation.";

                    // TASK-072 : purge toute ventilation déjà matérialisée (écrite avant que ce
                    // contrôle croisé ne s'exécute) et la remplace par la sentinelle d'erreur —
                    // sinon l'écran Factures continuerait d'afficher des valeurs valorisées.
                    if (!string.IsNullOrEmpty(_persistenceConnectionString))
                    {
                        try
                        {
                            // TASK-076 : montants bruts capturés uniquement si réellement lus (doc issu
                            // d'une lecture OM/FGR aboutie) — jamais inventés pour un doc reconstruit
                            // depuis le cache (parafiscal non conservé séparément en cache).
                            var montantsBruts = doc.MontantsBrutsDisponibles
                                ? new MontantsBrutsErreur
                                {
                                    TotalHTNet = doc.TotalHTNet,
                                    TotalTva = doc.TotalTva,
                                    TotalParafiscale = doc.TotalParafiscale,
                                    TotalTtc = doc.TotalTtc
                                }
                                : null;
                            _ventilationCache.MarquerEnErreur(_soId, affectation.EC_Id, doc.MotifErreur, _persistenceConnectionString, montantsBruts);
                        }
                        catch (Exception ex)
                        {
                            _log?.Invoke($"[VALO] EC_Id={affectation.EC_Id} — échec écriture sentinelle d'erreur (contrôle croisé) : {ex.Message}");
                        }
                    }
                }

                return doc;
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
                entries = _ventilationCache.GetEntries(_soId, ecId, _persistenceConnectionString);
            }
            catch (Exception)
            {
                return null;
            }

            if (entries.Count == 0)
                return null;

            // TASK-076/077 : auto-guérison d'un état hérité d'AVANT le correctif d'écriture
            // (UpsertEntries purge désormais la sentinelle ERREUR avant tout bucket réel) — si une
            // sentinelle ERREUR coexiste encore avec de vrais buckets (écrits par l'ancien code),
            // elle est obsolète par construction (un bucket réel supplante toujours une ancienne
            // sentinelle) : on l'ignore ici, sans dépendre de l'ordre de lecture SQL non garanti.
            if (entries.Count > 1 && entries.Any(e => e.CodeTaxe == "ERREUR"))
            {
                _log?.Invoke($"[VALO] EC_Id={ecId} — sentinelle ERREUR obsolète coexistant avec des buckets réels "
                    + "(état hérité pré-correctif) : ignorée, revalidation sur les buckets réels.");
                entries = entries.Where(e => e.CodeTaxe != "ERREUR").ToList();
            }

            // TASK-072 : ligne sentinelle d'erreur — jamais servie comme ventilation, quel que
            // soit l'état du paiement (l'incohérence Sage ne dépend pas du pointage). Évite de
            // relire l'OM à chaque passe pour une pièce durablement cassée.
            //
            // TASK-076 : une sentinelle écrite AVANT ce correctif (ex. EC_Id=21473/FC2501717,
            // marquée par TASK-072) ne porte aucun montant brut (BrutHT/BrutTva/BrutTtc NULL en
            // cache) — sans relecture, elle resterait figée sans bruts indéfiniment (aucun autre
            // chemin ne les recapture pour une ligne déjà ERREUR). On force ici UNE relecture
            // Sage (cache traité comme absent → ecIdsToFetch) pour la seule première fois où les
            // bruts manquent ; une fois capturés (ci-dessous ou via Traiter), la ligne redevient
            // strictement figée par ce court-circuit (auto-cicatrisant, jamais répété).
            if (entries.Count == 1 && entries[0].CodeTaxe == "ERREUR")
            {
                var e = entries[0];
                bool aDejaDesBruts = e.BrutHT != null || e.BrutTva != null
                    || e.BrutParafiscale != null || e.BrutTtc != null;
                if (aDejaDesBruts)
                {
                    return new DocumentTaxesInfo
                    {
                        EnErreur = true,
                        MotifErreur = e.MotifErreur ?? "Pièce exclue de la valorisation (motif non tracé)."
                    };
                }

                _log?.Invoke($"[VALO] EC_Id={ecId} — sentinelle d'erreur préexistante sans montants bruts "
                    + "(antérieure à TASK-076) : relecture Sage forcée une fois pour les capturer.");
                return null;
            }

            // Validation du paiement : token stocké == état courant local.
            //
            // TASK-156 (correctif B) : le token ne court-circuite plus la lecture des MONTANTS
            // (HT/TVA/TTC) quand il est NULL en permanence (facture NonRapproche/NonAffecte,
            // « dépayée ») — le contenu OM d'une facture (taux/base TVA) est indépendant du
            // règlement (cf. commentaire ~163-168 ci-dessus : « la relire plus tard serait du
            // travail perdu »), donc une facture jamais payée n'a AUCUNE raison d'être relue via
            // OM à CHAQUE cycle tant que rien n'a changé (cause racine de la contention constatée
            // en prod, log 23/07/2026 : mêmes pièces FF260xxx relues identiques à 3 reprises en
            // ~6 minutes). La règle métier « non déclarable sans paiement pointé » reste
            // appliquée intégralement ailleurs, par le CONSOMMATEUR du document (résolution
            // EstEligible/EstValorisable en amont, jamais dérivée de ce cache) — retirer ce
            // court-circuit ici ne rend éligible aucune ligne qui ne l'était pas.
            //
            // Le token reste comparé (stocké vs courant, y compris quand l'un des deux est NULL)
            // pour continuer à détecter un changement RÉEL d'état de paiement — apparition
            // (facture qui devient payée entre deux cycles), disparition (dépointage) ou mutation
            // (autre règlement/pointage) — et forcer une relecture OM dans ce cas précis
            // uniquement. Logique de fraîcheur TASK-023/072 inchangée : seul le court-circuit
            // permanent sur « toujours NULL » a été retiré, pas la détection de divergence.
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

                var storedToken = entries[0]; // tous les buckets ont le même token
                if (storedToken.Token_MV_Id != currentToken?.MV_Id
                    || storedToken.Token_MV_Point != currentToken?.MV_Point)
                    return null; // Token apparu/disparu/muté → cache périmé, relecture nécessaire
            }

            var firstEntry = entries[0];

            // TASK-072 : revalidation rétroactive. Une ligne mise en cache AVANT l'introduction
            // de ce garde-fou (ou par tout autre chemin antérieur) peut porter une incohérence
            // Sage jamais détectée à l'écriture — le garde-fou de SageTaxReaderService ne s'exécute
            // qu'à la LECTURE OM, jamais sur une ligne déjà en cache. Sans ce contrôle ici, une
            // pièce cassée resterait servie indéfiniment tant que le token de paiement ne change
            // pas (cas réel observé : EC_Id=21473/FC2501717, caché avant ce correctif).
            if (IncoherenceHtTvaTtc.EstIncoherent(firstEntry.TotalHT, firstEntry.TotalTva, 0, firstEntry.TotalTtc, out _))
            {
                var motif = $"Incohérence Sage détectée rétroactivement en cache : Σ(HT+TVA)={firstEntry.TotalHT + firstEntry.TotalTva:F2} "
                    + $"≠ TTC={firstEntry.TotalTtc:F2} (EC_Id={ecId}) — pièce exclue de la valorisation.";
                try
                {
                    // TASK-076 : les totaux HT/TVA/TTC déjà en cache sont des valeurs réellement lues
                    // (écrites lors d'une lecture OM antérieure) — le parafiscal n'était pas conservé
                    // séparément dans ce format de cache, donc NULL (jamais inventé).
                    var montantsBruts = new MontantsBrutsErreur
                    {
                        TotalHTNet = firstEntry.TotalHT,
                        TotalTva = firstEntry.TotalTva,
                        TotalParafiscale = null,
                        TotalTtc = firstEntry.TotalTtc
                    };
                    _ventilationCache.MarquerEnErreur(_soId, ecId, motif, _persistenceConnectionString, montantsBruts);
                }
                catch (Exception ex)
                {
                    _log?.Invoke($"[VALO] EC_Id={ecId} — échec écriture sentinelle d'erreur (revalidation cache) : {ex.Message}");
                }
                return new DocumentTaxesInfo { EnErreur = true, MotifErreur = motif };
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
            int soId, int ecId, DocumentTaxesInfo doc, PaiementToken? token)
        {
            return doc.LignesTaxe.Select(l => new VentilationSageCacheEntry
            {
                SO_Id        = soId,
                EC_Id        = ecId,
                Taux         = l.Taux,
                BaseHT       = l.BaseHT,
                MontantTva   = l.MontantTva,
                TTC          = l.TTC,
                CodeTaxe     = l.Code,
                TotalHT      = doc.TotalHT,
                TotalTva     = doc.TotalTva,
                TotalTtc     = doc.TotalTtc,
                // NULL si la facture n'est pas encore rattachée à un règlement pointé.
                Token_MV_Id  = token?.MV_Id,
                Token_MV_Point = token?.MV_Point
            });
        }
    }
}
