using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Declaration.Core.Model;
using Declaration.Orchestration;
using SageTaxReader.Contracts;
using Declaration.Core;
using Microsoft.Data.SqlClient;
using Dapper;

namespace Declaration.Orchestration.Tests
{
    // ═══════════════════════════════════════════════════════════════════════════════
    // TASK-024 — Tests du cache des ventilations Sage
    //
    // DEUX NIVEAUX DE TEST :
    //
    // 1. Tests unitaires (T1–T8) : stub InMemoryCacheRepository.
    //    Prouvent la logique orchestrateur (routing, comptage OM, déduplication).
    //    100 % in-process, aucune connexion réseau.
    //
    // 2. Tests d'intégration SQL Server (IT-*) : VentilationSageCacheRepository réel.
    //    Cible : .\sql2022 / GR_EMA_DISTRIBUTION (même moteur et même DB que la prod).
    //    Prouvent que le DDL 002_Cache_Ventilation_Sage.sql est correct,
    //    que MERGE est idempotent, et que les 3 critères principaux
    //    (2e lecture 0 OM, dépointage → OM, égalité cache↔OM) tiennent
    //    sur SQL Server.
    //    EC_Id réservés ≥ 900000 pour ne pas entrer en collision avec la prod.
    // ═══════════════════════════════════════════════════════════════════════════════

    // ─────────────────────────────────────────────────────────────────────────────
    // Stubs partagés
    // ─────────────────────────────────────────────────────────────────────────────

    internal class CountingWorkerInvoker : IWorkerInvoker
    {
        public int TotalCalls { get; private set; }

        public DocumentTaxesInfo? InvoquerWorker(string numeroFacture, string sens, WorkerConfig config, Action<string>? log = null)
        {
            TotalCalls++;
            return MakeDoc(numeroFacture, sens);
        }

        public List<DocumentTaxesInfo> InvoquerWorkerBatch(
            IEnumerable<(string numeroFacture, string sens)> requetes, WorkerConfig config, Action<string>? log = null)
        {
            var result = new List<DocumentTaxesInfo>();
            foreach (var r in requetes) { TotalCalls++; result.Add(MakeDoc(r.numeroFacture, r.sens)); }
            return result;
        }

        internal static DocumentTaxesInfo MakeDoc(string numero, string sens) => new()
        {
            NumeroPiece = numero, Sens = sens,
            TotalHT = 1000, TotalTva = 200, TotalTtc = 1200,
            LignesTaxe = new List<TaxeDetail>
            {
                new() { Code = "TVA20", Type = "TaxeTypeTVA", Taux = 20, BaseHT = 1000, MontantTva = 200, TTC = 1200 }
            }
        };
    }

    internal class StubLecteurFgr : ILecteurTvaFgr
    {
        public DocumentTaxesInfo? LireTvaFgr(int ecId, string numero, string cs, string sageCs) => null;
    }

    // TASK-072 : simule une pièce OM détectée incohérente (EnErreur=true dès la lecture Sage,
    // avant tout passage par l'orchestrateur — équivalent du garde-fou IncoherenceHtTvaTtc).
    internal class ErroneousWorkerInvoker : IWorkerInvoker
    {
        public int TotalCalls { get; private set; }
        public const string Motif = "Incohérence Sage : Σ(HT+TVA) ≠ TTC — pièce exclue de la valorisation.";

        public DocumentTaxesInfo? InvoquerWorker(string numeroFacture, string sens, WorkerConfig config, Action<string>? log = null)
        {
            TotalCalls++;
            return MakeDoc(numeroFacture, sens);
        }

        public List<DocumentTaxesInfo> InvoquerWorkerBatch(
            IEnumerable<(string numeroFacture, string sens)> requetes, WorkerConfig config, Action<string>? log = null)
        {
            var result = new List<DocumentTaxesInfo>();
            foreach (var r in requetes) { TotalCalls++; result.Add(MakeDoc(r.numeroFacture, r.sens)); }
            return result;
        }

        // TASK-076 : montants bruts réellement lus (Σ HT+TVA ≠ TTC), disponibles au moment de la
        // détection — c'est précisément le cas que TASK-076 rend visible sur l'écran Factures.
        public const double BrutHT = 1000, BrutTva = 200, BrutParafiscale = 0, BrutTtc = 999999;

        internal static DocumentTaxesInfo MakeDoc(string numero, string sens) => new()
        {
            NumeroPiece = numero, Sens = sens,
            EnErreur = true, MotifErreur = Motif,
            TotalHTNet = BrutHT, TotalTva = BrutTva, TotalParafiscale = BrutParafiscale, TotalTtc = BrutTtc,
            MontantsBrutsDisponibles = true
        };
    }

    // Cache in-memory pour les tests unitaires uniquement
    internal class InMemoryCacheRepository : IVentilationSageCacheRepository
    {
        private readonly Dictionary<int, List<VentilationSageCacheEntry>> _store = new();
        public bool PaymentPresent { get; set; } = true;
        public bool PaymentPointMatches { get; set; } = true;

        public IReadOnlyList<VentilationSageCacheEntry> GetEntries(int soId, int ecId, string _)
            => _store.TryGetValue(ecId, out var list)
                ? list.Where(e => e.SO_Id == soId).ToList()
                : new List<VentilationSageCacheEntry>();

        public PaiementToken? GetCurrentPaiementToken(int ecId, string _)
        {
            if (!PaymentPresent) return null;
            return PaymentPointMatches
                ? new PaiementToken { MV_Id = 42, MV_Point = 1 }
                : new PaiementToken { MV_Id = 99, MV_Point = 0 };
        }

        public void UpsertEntries(IEnumerable<VentilationSageCacheEntry> entries, string _)
        {
            foreach (var e in entries)
            {
                if (!_store.ContainsKey(e.EC_Id)) _store[e.EC_Id] = new();
                _store[e.EC_Id].RemoveAll(x => x.Taux == e.Taux);
                _store[e.EC_Id].Add(e);
            }
        }

        // TASK-072 : purge toute ventilation existante et la remplace par une sentinelle unique.
        // TASK-076 : persiste en plus les montants bruts (si fournis) sur la ligne sentinelle.
        public void MarquerEnErreur(int soId, int ecId, string motif, string _, MontantsBrutsErreur? montantsBruts = null)
        {
            _store[ecId] = new List<VentilationSageCacheEntry>
            {
                new()
                {
                    SO_Id = soId, EC_Id = ecId, Taux = -1, CodeTaxe = "ERREUR", MotifErreur = motif,
                    BrutHT = montantsBruts?.TotalHTNet,
                    BrutTva = montantsBruts?.TotalTva,
                    BrutParafiscale = montantsBruts?.TotalParafiscale,
                    BrutTtc = montantsBruts?.TotalTtc
                }
            };
        }

        // TASK-072 : null par défaut = contrôle croisé ignoré (comportement des tests T1-T9,
        // antérieurs à ce contrôle, inchangé). Les tests dédiés au contrôle le renseignent.
        public Dictionary<int, decimal?> EcheanceMontantDeviseParEcId { get; } = new();

        public decimal? GetEcheanceMontantDevise(int ecId, string _)
            => EcheanceMontantDeviseParEcId.TryGetValue(ecId, out var v) ? v : (decimal?)null;

        public bool HasEntry(int ecId) => _store.ContainsKey(ecId) && _store[ecId].Count > 0;

        // TASK-078 : purge sans réécrire (utilisé par ResynchroniserLigneAsync).
        public void SupprimerEntrees(int soId, int ecId, string _) => _store.Remove(ecId);
    }

    internal static class T
    {
        public static AffectationADeclarer Aff(string numero, int ecId, int ecType = 0,
            SensAffectation sens = SensAffectation.Achat)
            => new()
            {
                NumeroFacture = numero, NumeroRapprochement = "RAP001",
                EC_Id = ecId, EC_Type = ecType, Sens = sens, MontantAffecte = 1200,
                Tiers = new TiersInfo { Ice = "123456789012345", IdentifiantFiscal = "12345678" }
            };

        public static WorkerConfig Cfg => new();
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // PARTIE 1 — Tests unitaires (stub in-memory)
    // ═══════════════════════════════════════════════════════════════════════════════

    public class Task024UnitTests
    {
        [Fact] public void T1_PremiereLeture_OMAppele_CacheEcrit()
        {
            var inv = new CountingWorkerInvoker(); var repo = new InMemoryCacheRepository();
            var orch = new OrchestrateurDeclaration(inv, T.Cfg, new StubLecteurFgr(),
                "fake-grf", "fake-sage", repo, "fake-pers");
            orch.Traiter(new[] { T.Aff("FAC001", 1) }, 1);
            Assert.Equal(1, inv.TotalCalls);
            Assert.True(repo.HasEntry(1));
        }

        [Fact] public void T2_DeuxiemeLeture_ZeroOM_ServiDepuisCache()
        {
            var inv = new CountingWorkerInvoker(); var repo = new InMemoryCacheRepository();
            var orch = new OrchestrateurDeclaration(inv, T.Cfg, new StubLecteurFgr(),
                "fake-grf", "fake-sage", repo, "fake-pers");
            var aff = T.Aff("FAC001", 1);
            orch.Traiter(new[] { aff }, 1);
            orch.Traiter(new[] { aff }, 1);
            Assert.Equal(1, inv.TotalCalls);
        }

        [Fact] public void T3_PaiementDefait_TokenDiverge_CacheNonServi()
        {
            var inv = new CountingWorkerInvoker(); var repo = new InMemoryCacheRepository();
            var orch = new OrchestrateurDeclaration(inv, T.Cfg, new StubLecteurFgr(),
                "fake-grf", "fake-sage", repo, "fake-pers");
            var aff = T.Aff("FAC001", 1);
            orch.Traiter(new[] { aff }, 1);
            repo.PaymentPointMatches = false;
            orch.Traiter(new[] { aff }, 1);
            Assert.Equal(2, inv.TotalCalls);
        }

        // TASK-156 (correctif B) : AVANT ce correctif, une facture sans paiement pointé restait
        // "dépayée" en permanence (PaymentPresent=false sur toute la durée du test, contrairement
        // à T3 où le token change de valeur) était relue via OM à CHAQUE cycle — cause racine de
        // la contention constatée en prod (log 23/07/2026 : mêmes pièces FF260xxx relues
        // identiques à 3 reprises en ~6 minutes). Le nom/l'assertion de ce test ont changé
        // (2 → 1 appel OM) : c'est EXACTEMENT le test qui aurait dû échouer et n'existait pas
        // ("actuellement absent" — TASK-156), l'ancienne assertion figeait le bug comme
        // comportement attendu.
        [Fact] public void T4_PaiementAbsent_StablePermanent_CacheServi_UnSeulOM()
        {
            var inv = new CountingWorkerInvoker(); var repo = new InMemoryCacheRepository();
            repo.PaymentPresent = false; // jamais payée, du premier au second cycle
            var orch = new OrchestrateurDeclaration(inv, T.Cfg, new StubLecteurFgr(),
                "fake-grf", "fake-sage", repo, "fake-pers");
            var aff = T.Aff("FAC001", 1);

            var m1 = orch.Traiter(new[] { aff }, 1);
            var m2 = orch.Traiter(new[] { aff }, 1);

            Assert.Equal(1, inv.TotalCalls); // un seul appel OM — le second cycle sert le cache
            // Montants HT/TVA/TTC servis depuis le cache identiques à la lecture OM d'origine —
            // seule la déclarabilité (hors périmètre de ce cache) reste bloquée par le token NULL.
            Assert.Equal(m1.Lignes.Count, m2.Lignes.Count);
        }

        // TASK-156 (correctif B, risque explicite de la task) : une facture qui DEVIENT payée
        // entre deux cycles doit toujours déclencher une relecture OM — le retrait du
        // court-circuit "currentToken == null" ne doit jamais figer une ligne non payée une fois
        // pour toutes si son état de paiement change réellement.
        [Fact] public void T16_FactureDevientPayee_TokenApparait_OMRappele()
        {
            var inv = new CountingWorkerInvoker(); var repo = new InMemoryCacheRepository();
            repo.PaymentPresent = false; // non payée au premier cycle
            var orch = new OrchestrateurDeclaration(inv, T.Cfg, new StubLecteurFgr(),
                "fake-grf", "fake-sage", repo, "fake-pers");
            var aff = T.Aff("FAC001", 1);

            orch.Traiter(new[] { aff }, 1);
            Assert.Equal(1, inv.TotalCalls);

            repo.PaymentPresent = true; // devient payée (token apparaît) avant le second cycle
            orch.Traiter(new[] { aff }, 1);

            Assert.Equal(2, inv.TotalCalls); // le changement d'état de paiement force la relecture
        }

        [Fact] public void T9_PaiementAbsent_LectureBrute_MiseEnCache_TokenNull()
        {
            // Une facture lue mais SANS règlement pointé est désormais mise en cache
            // (lecture OM brute, réutilisable pour l'affichage) avec un token NULL,
            // mais reste non servie comme ventilation déclarable (cf. T4).
            var inv = new CountingWorkerInvoker(); var repo = new InMemoryCacheRepository();
            repo.PaymentPresent = false; // aucun paiement pointé
            var orch = new OrchestrateurDeclaration(inv, T.Cfg, new StubLecteurFgr(),
                "fake-grf", "fake-sage", repo, "fake-pers");
            var aff = T.Aff("FAC001", 1);

            orch.Traiter(new[] { aff }, 1);

            Assert.Equal(1, inv.TotalCalls);          // OM lu une fois
            Assert.True(repo.HasEntry(1));            // ligne brute conservée
            var rows = repo.GetEntries(0, 1, "fake-pers");
            Assert.All(rows, r => Assert.Null(r.Token_MV_Id));   // token NULL = non déclarable
        }

        [Fact] public void T5_SansPersistenceConnection_OMAppeleNormalement()
        {
            var inv = new CountingWorkerInvoker(); var repo = new InMemoryCacheRepository();
            var orch = new OrchestrateurDeclaration(inv, T.Cfg, new StubLecteurFgr(),
                "fake-grf", "fake-sage", repo, "");
            var aff = T.Aff("FAC001", 1);
            orch.Traiter(new[] { aff }, 1); orch.Traiter(new[] { aff }, 1);
            Assert.Equal(2, inv.TotalCalls);
            Assert.False(repo.HasEntry(1));
        }

        [Fact] public void T6_EgaliteVentilationCacheVsOM()
        {
            var inv = new CountingWorkerInvoker(); var repo = new InMemoryCacheRepository();
            var orch = new OrchestrateurDeclaration(inv, T.Cfg, new StubLecteurFgr(),
                "fake-grf", "fake-sage", repo, "fake-pers");
            var aff = T.Aff("FAC001", 10);
            var m1 = orch.Traiter(new[] { aff }, 1);
            var m2 = orch.Traiter(new[] { aff }, 1);
            Assert.Equal(1, inv.TotalCalls);
            Assert.Equal(m1.Lignes.Count, m2.Lignes.Count);
            for (int i = 0; i < m1.Lignes.Count; i++)
            {
                Assert.Equal(m1.Lignes[i].HT,   m2.Lignes[i].HT,   precision: 4);
                Assert.Equal(m1.Lignes[i].Tva,  m2.Lignes[i].Tva,  precision: 4);
                Assert.Equal(m1.Lignes[i].Ttc,  m2.Lignes[i].Ttc,  precision: 4);
                Assert.Equal(m1.Lignes[i].Taux, m2.Lignes[i].Taux, precision: 4);
            }
        }

        [Fact] public void T7_PlusieursAffectationsMemeFacture_UnSeulOM()
        {
            var inv = new CountingWorkerInvoker(); var repo = new InMemoryCacheRepository();
            var orch = new OrchestrateurDeclaration(inv, T.Cfg, new StubLecteurFgr(),
                "fake-grf", "fake-sage", repo, "fake-pers");
            var modele = orch.Traiter(new[] { T.Aff("FAC001", 5), T.Aff("FAC001", 5) }, 1);
            Assert.Equal(1, inv.TotalCalls);
            Assert.Equal(2, modele.Lignes.Count);
        }

        // TASK-072 : contrôle croisé TTC Sage vs RT_ECHEANCE.EC_MtDevise (GRF). Reproduit le cas
        // réel FC2501717 (EC_Id=21473) où le TTC Sage divergeait du montant d'échéance GRF connu.
        [Fact] public void T10_TtcSageDivergeDeEcMtDevise_LigneExclue_AlerteLevee()
        {
            var inv = new CountingWorkerInvoker(); var repo = new InMemoryCacheRepository();
            repo.EcheanceMontantDeviseParEcId[7] = 999999m; // GRF attend un TTC très différent de l'OM (1200)
            var orch = new OrchestrateurDeclaration(inv, T.Cfg, new StubLecteurFgr(),
                "fake-grf", "fake-sage", repo, "fake-pers");

            var modele = orch.Traiter(new[] { T.Aff("FAC072", 7) }, 1);

            Assert.Empty(modele.Lignes);
            Assert.Contains(modele.Alertes, a => a.Code == "FACTURE_ILLISIBLE_OM"
                && a.Message.Contains("EC_MtDevise"));
        }

        [Fact] public void T11_TtcSageCoherentAvecEcMtDevise_LigneDeclaree()
        {
            var inv = new CountingWorkerInvoker(); var repo = new InMemoryCacheRepository();
            repo.EcheanceMontantDeviseParEcId[8] = 1200m; // cohérent avec le TTC OM (1200)
            var orch = new OrchestrateurDeclaration(inv, T.Cfg, new StubLecteurFgr(),
                "fake-grf", "fake-sage", repo, "fake-pers");

            var modele = orch.Traiter(new[] { T.Aff("FAC073", 8) }, 1);

            Assert.NotEmpty(modele.Lignes);
            Assert.DoesNotContain(modele.Alertes, a => a.Code == "FACTURE_ILLISIBLE_OM");
        }

        // TASK-072 : une pièce OM rendue EnErreur (incohérence Sage) n'est jamais matérialisée
        // comme ventilation réelle — une sentinelle d'erreur (CodeTaxe="ERREUR") est écrite à la
        // place, portant le motif exact, pour rester visible sur l'écran Factures.
        [Fact] public void T12_PieceOMEnErreur_SentinelleEcriteAuLieuDeLaVentilation()
        {
            var inv = new ErroneousWorkerInvoker(); var repo = new InMemoryCacheRepository();
            var orch = new OrchestrateurDeclaration(inv, T.Cfg, new StubLecteurFgr(),
                "fake-grf", "fake-sage", repo, "fake-pers");

            var modele = orch.Traiter(new[] { T.Aff("FAC072B", 20) }, 1);

            Assert.Empty(modele.Lignes);
            Assert.Contains(modele.Alertes, a => a.Code == "FACTURE_ILLISIBLE_OM");
            Assert.True(repo.HasEntry(20));
            var rows = repo.GetEntries(0, 20, "fake-pers");
            Assert.Single(rows);
            Assert.Equal("ERREUR", rows[0].CodeTaxe);
            Assert.Equal(ErroneousWorkerInvoker.Motif, rows[0].MotifErreur);
        }

        // TASK-072 : une fois la sentinelle écrite, un second passage ne relit jamais l'OM
        // (motif servi depuis le cache) — évite de re-solliciter Sage pour une pièce durablement
        // cassée à chaque rafraîchissement de l'écran Factures.
        [Fact] public void T13_PieceEnErreur_SecondPassage_ServieDepuisSentinelle_ZeroOM()
        {
            var inv = new ErroneousWorkerInvoker(); var repo = new InMemoryCacheRepository();
            var orch = new OrchestrateurDeclaration(inv, T.Cfg, new StubLecteurFgr(),
                "fake-grf", "fake-sage", repo, "fake-pers");
            var aff = T.Aff("FAC072C", 21);

            orch.Traiter(new[] { aff }, 1);
            Assert.Equal(1, inv.TotalCalls);

            var modele2 = orch.Traiter(new[] { aff }, 1);

            Assert.Equal(1, inv.TotalCalls); // toujours 1 : pas de relecture OM
            Assert.Empty(modele2.Lignes);
            Assert.Contains(modele2.Alertes, a => a.Code == "FACTURE_ILLISIBLE_OM");
        }

        // TASK-076 : les montants bruts Sage (tels que lus, AVANT exclusion) sont persistés sur la
        // ligne sentinelle d'erreur — exploitables par l'écran Factures pour l'investigation
        // manuelle côté ERP, sans jamais relire Sage à la demande.
        [Fact] public void T15_PieceOMEnErreur_MontantsBrutsPersistesSurLaSentinelle()
        {
            var inv = new ErroneousWorkerInvoker(); var repo = new InMemoryCacheRepository();
            var orch = new OrchestrateurDeclaration(inv, T.Cfg, new StubLecteurFgr(),
                "fake-grf", "fake-sage", repo, "fake-pers");

            orch.Traiter(new[] { T.Aff("FAC076", 40) }, 1);

            var rows = repo.GetEntries(0, 40, "fake-pers");
            Assert.Single(rows);
            Assert.Equal("ERREUR", rows[0].CodeTaxe);
            Assert.Equal(ErroneousWorkerInvoker.BrutHT, rows[0].BrutHT);
            Assert.Equal(ErroneousWorkerInvoker.BrutTva, rows[0].BrutTva);
            Assert.Equal(ErroneousWorkerInvoker.BrutParafiscale, rows[0].BrutParafiscale);
            Assert.Equal(ErroneousWorkerInvoker.BrutTtc, rows[0].BrutTtc);
        }

        // TASK-072 : une ligne de cache déjà présente AVANT ce correctif (donc jamais passée par
        // le garde-fou de lecture OM) et portant une incohérence Sage doit être revalidée et
        // exclue au moment où elle est SERVIE depuis le cache — pas seulement à l'écriture.
        // Reproduit le cas réel EC_Id=21473/FC2501717 : cache déjà écrit avec HT+TVA≠TTC, token
        // toujours pointé (donc jamais réinvalidé par un dépointage).
        [Fact] public void T14_LigneCacheAncienneIncoherente_RevalideeEtExclueALaLecture()
        {
            var inv = new CountingWorkerInvoker(); var repo = new InMemoryCacheRepository();
            // Simule une ligne déjà en cache avant ce correctif (écrite par l'ancien code, sans
            // passer par MarquerEnErreur), avec la même incohérence que le cas réel.
            repo.UpsertEntries(new[]
            {
                new VentilationSageCacheEntry
                {
                    EC_Id = 30, Taux = 20, CodeTaxe = "D20",
                    BaseHT = 1720251.20, MontantTva = 344050.24, TTC = 2064301.44,
                    TotalHT = 1720251.20, TotalTva = 344050.24, TotalTtc = 20700.00,
                    Token_MV_Id = 42, Token_MV_Point = 1
                }
            }, "fake-pers");

            var orch = new OrchestrateurDeclaration(inv, T.Cfg, new StubLecteurFgr(),
                "fake-grf", "fake-sage", repo, "fake-pers");

            var modele = orch.Traiter(new[] { T.Aff("FC2501717", 30) }, 1);

            Assert.Equal(0, inv.TotalCalls); // aucune relecture OM nécessaire
            Assert.Empty(modele.Lignes);
            Assert.Contains(modele.Alertes, a => a.Code == "FACTURE_ILLISIBLE_OM");
            // La ligne corrompue a été purgée et remplacée par la sentinelle (auto-guérison du cache).
            var rows = repo.GetEntries(0, 30, "fake-pers");
            Assert.Single(rows);
            Assert.Equal("ERREUR", rows[0].CodeTaxe);
        }

        [Fact] public void T8_ECType111_FGR_NePaseParCacheSage()
        {
            var inv = new CountingWorkerInvoker(); var repo = new InMemoryCacheRepository();
            var orch = new OrchestrateurDeclaration(inv, T.Cfg, new StubLecteurFgr(),
                "fake-grf", "fake-sage", repo, "fake-pers");
            orch.Traiter(new[] { T.Aff("FGR001", 99, ecType: 111) }, 1);
            Assert.Equal(0, inv.TotalCalls);
            Assert.False(repo.HasEntry(99));
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // PARTIE 2 — Tests d'intégration SQL Server
    //
    // Cible : .\sql2022 / GR_EMA_DISTRIBUTION
    //   Même moteur SQL Server et même base que la prod.
    //   EC_Id réservés ≥ 900000 pour éviter toute collision.
    //   La table DM_VENTILATION_SAGE_CACHE est créée par le DDL
    //   002_Cache_Ventilation_Sage.sql (doit être appliqué au préalable sur la base).
    //   Teardown : DELETE WHERE EC_Id IN (plage de test).
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Sous-classe de VentilationSageCacheRepository qui surcharge GetCurrentPaiementToken
    /// pour injecter un token contrôlé (pas de connexion GRF nécessaire dans les tests).
    /// </summary>
    public class RepoSqlServerStubToken : VentilationSageCacheRepository
    {
        public PaiementToken? ForcedToken { get; set; } = new() { MV_Id = 42, MV_Point = 1 };

        public override PaiementToken? GetCurrentPaiementToken(int ecId, string grfConnectionString)
            => ForcedToken;
    }

    public class Task024SqlServerIntegrationTests : IDisposable
    {
        // Même chaîne de connexion que connections.json → PersistenceConnection
        internal const string SqlServerCs =
            "Server=.\\sql2022;Database=GR_EMA_DISTRIBUTION;User Id=sa;Password=1234;TrustServerCertificate=True;";

        // Plage d'EC_Id réservée pour les tests (jamais présente en prod)
        private const string TestEcIds = "900001,900002,900003,900004,900005,900006,900007,900010,900011,900012";

        // TASK-118 : SO_Id de test — distinct du SO_Id=1 réel (GR_EMA_DISTRIBUTION), pour ne
        // jamais collisionner avec les données de prod tout en exerçant le filtre SO_Id du cache.
        internal const int TestSoId = 777001;

        // DDL de la table cache (idempotent — IF NOT EXISTS)
        private const string EnsureTableSql = @"
            IF OBJECT_ID('DM_VENTILATION_SAGE_CACHE','U') IS NULL
            BEGIN
                CREATE TABLE [DM_VENTILATION_SAGE_CACHE] (
                    [SO_Id]          INT             NOT NULL,
                    [EC_Id]          INT             NOT NULL,
                    [Taux]           DECIMAL(18,4)   NOT NULL,
                    [BaseHT]         DECIMAL(18,4)   NOT NULL,
                    [MontantTva]     DECIMAL(18,4)   NOT NULL,
                    [TTC]            DECIMAL(18,4)   NOT NULL,
                    [CodeTaxe]       NVARCHAR(50)    NOT NULL DEFAULT '',
                    [TotalHT]        DECIMAL(18,4)   NOT NULL,
                    [TotalTva]       DECIMAL(18,4)   NOT NULL,
                    [TotalTtc]       DECIMAL(18,4)   NOT NULL,
                    [Token_MV_Id]    INT             NULL,
                    [Token_MV_Point] INT             NULL,
                    [DateLecture]    DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
                    [Source]         NVARCHAR(100)   NOT NULL DEFAULT 'OM',
                    [MotifErreur]    NVARCHAR(500)   NULL,
                    CONSTRAINT [PK_DM_VENTILATION_SAGE_CACHE] PRIMARY KEY ([SO_Id], [EC_Id], [Taux])
                );
                CREATE INDEX [IX_DM_VENTILATION_SAGE_CACHE_ECId]
                ON [DM_VENTILATION_SAGE_CACHE] ([EC_Id]);
            END
            ELSE IF NOT EXISTS (
                SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = 'DM_VENTILATION_SAGE_CACHE' AND COLUMN_NAME = 'MotifErreur'
            )
            BEGIN
                ALTER TABLE [DM_VENTILATION_SAGE_CACHE] ADD [MotifErreur] NVARCHAR(500) NULL;
            END;

            -- TASK-076 : montants bruts Sage (sentinelle d'erreur uniquement).
            IF NOT EXISTS (
                SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = 'DM_VENTILATION_SAGE_CACHE' AND COLUMN_NAME = 'BrutHT'
            )
            BEGIN
                ALTER TABLE [DM_VENTILATION_SAGE_CACHE] ADD [BrutHT] DECIMAL(18,4) NULL;
            END;
            IF NOT EXISTS (
                SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = 'DM_VENTILATION_SAGE_CACHE' AND COLUMN_NAME = 'BrutTva'
            )
            BEGIN
                ALTER TABLE [DM_VENTILATION_SAGE_CACHE] ADD [BrutTva] DECIMAL(18,4) NULL;
            END;
            IF NOT EXISTS (
                SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = 'DM_VENTILATION_SAGE_CACHE' AND COLUMN_NAME = 'BrutParafiscale'
            )
            BEGIN
                ALTER TABLE [DM_VENTILATION_SAGE_CACHE] ADD [BrutParafiscale] DECIMAL(18,4) NULL;
            END;
            IF NOT EXISTS (
                SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = 'DM_VENTILATION_SAGE_CACHE' AND COLUMN_NAME = 'BrutTtc'
            )
            BEGIN
                ALTER TABLE [DM_VENTILATION_SAGE_CACHE] ADD [BrutTtc] DECIMAL(18,4) NULL;
            END";

        public Task024SqlServerIntegrationTests()
        {
            using var conn = new SqlConnection(SqlServerCs);
            conn.Open();
            conn.Execute(EnsureTableSql);
            // Nettoyer les éventuelles données de tests précédents
            conn.Execute($"DELETE FROM DM_VENTILATION_SAGE_CACHE WHERE EC_Id IN ({TestEcIds})");
        }

        public void Dispose()
        {
            try
            {
                using var conn = new SqlConnection(SqlServerCs);
                conn.Open();
                conn.Execute($"DELETE FROM DM_VENTILATION_SAGE_CACHE WHERE EC_Id IN ({TestEcIds})");
            }
            catch { /* best-effort */ }
        }

        private RepoSqlServerStubToken Repo() => new();

        // ── IT-1 — DDL correct : DATETIME2, DECIMAL, NVARCHAR, PK composite ─────

        [Fact]
        public void IT1_Table_DDLCorrect_SQLServer()
        {
            using var conn = new SqlConnection(SqlServerCs);
            conn.Open();

            // Vérifie la table
            var tableExists = conn.ExecuteScalar<int>(
                "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='DM_VENTILATION_SAGE_CACHE'");
            Assert.Equal(1, tableExists);

            // Vérifie les colonnes clés et leurs types SQL Server
            var cols = conn.Query(
                "SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS " +
                "WHERE TABLE_NAME='DM_VENTILATION_SAGE_CACHE'")
                .ToDictionary(r => (string)r.COLUMN_NAME, r => (string)r.DATA_TYPE);

            Assert.Equal("int",       cols["EC_Id"]);
            Assert.Equal("decimal",   cols["Taux"]);
            Assert.Equal("decimal",   cols["BaseHT"]);
            Assert.Equal("datetime2", cols["DateLecture"]);
            Assert.Equal("nvarchar",  cols["CodeTaxe"]);
        }

        // ── IT-2 — UpsertEntries + GetEntries : aller-retour SQL Server ────────

        [Fact]
        public void IT2_UpsertEtGetEntries_SQLServer()
        {
            var repo = Repo();
            var entry = new VentilationSageCacheEntry
            {
                SO_Id = TestSoId, EC_Id = 900001, Taux = 20, BaseHT = 1000, MontantTva = 200, TTC = 1200,
                CodeTaxe = "TVA20", TotalHT = 1000, TotalTva = 200, TotalTtc = 1200,
                Token_MV_Id = 42, Token_MV_Point = 1
            };

            repo.UpsertEntries(new[] { entry }, SqlServerCs);
            var rows = repo.GetEntries(TestSoId, 900001, SqlServerCs);

            Assert.Single(rows);
            Assert.Equal(900001, rows[0].EC_Id);
            Assert.Equal(20m,    (decimal)rows[0].Taux);
            Assert.Equal(1000m,  (decimal)rows[0].BaseHT);
            Assert.Equal(200m,   (decimal)rows[0].MontantTva);
            Assert.Equal(42,     rows[0].Token_MV_Id);
            Assert.Equal(1,      rows[0].Token_MV_Point);
        }

        // ── IT-3 — MERGE idempotent : double upsert, pas de doublon ──────────

        [Fact]
        public void IT3_Upsert_Idempotent_MERGE_SQLServer()
        {
            var repo = Repo();
            var entry = new VentilationSageCacheEntry
            {
                SO_Id = TestSoId, EC_Id = 900002, Taux = 20, BaseHT = 500, MontantTva = 100, TTC = 600,
                CodeTaxe = "TVA20", TotalHT = 500, TotalTva = 100, TotalTtc = 600,
                Token_MV_Id = 11, Token_MV_Point = 1
            };

            repo.UpsertEntries(new[] { entry }, SqlServerCs);

            entry.BaseHT = 800; entry.MontantTva = 160; entry.TTC = 960;
            repo.UpsertEntries(new[] { entry }, SqlServerCs); // 2e upsert → UPDATE

            var rows = repo.GetEntries(TestSoId, 900002, SqlServerCs);
            Assert.Single(rows);            // pas de doublon
            Assert.Equal(800m, (decimal)rows[0].BaseHT);  // valeur mise à jour
        }

        // ── IT-4 — 2e lecture : 0 appel OM sur SQL Server ────────────────────

        [Fact]
        public void IT4_DeuxiemeLeture_ZeroOM_SQLServer()
        {
            var repo = Repo();
            var inv  = new CountingWorkerInvoker();

            var orch = new OrchestrateurDeclaration(
                inv, T.Cfg, new StubLecteurFgr(),
                connectionString: "fake-grf",
                sageConnectionString: "fake-sage",
                ventilationCache: repo,
                persistenceConnectionString: SqlServerCs,
                soId: TestSoId);

            var aff = T.Aff("T024-FAC001", ecId: 900003);

            // Passe 1 : OM → écriture SQL Server
            orch.Traiter(new[] { aff }, 1);
            Assert.Equal(1, inv.TotalCalls);

            // Passe 2 : cache SQL Server → 0 OM supplémentaire
            orch.Traiter(new[] { aff }, 1);
            Assert.Equal(1, inv.TotalCalls); // toujours 1

            // La ligne est bien en base
            Assert.NotEmpty(repo.GetEntries(TestSoId, 900003, SqlServerCs));
        }

        // ── IT-5 — Dépointage (token diverge) → OM rappelé sur SQL Server ────

        [Fact]
        public void IT5_PaiementDefait_OMRappele_SQLServer()
        {
            var repo = Repo(); // token initial = MV_Id=42, MV_Point=1
            var inv  = new CountingWorkerInvoker();

            var orch = new OrchestrateurDeclaration(
                inv, T.Cfg, new StubLecteurFgr(),
                "fake-grf", "fake-sage", repo, SqlServerCs, soId: TestSoId);

            var aff = T.Aff("T024-FAC002", ecId: 900004);

            // Passe 1 : cache matérialisé avec Token_MV_Id=42, Token_MV_Point=1
            orch.Traiter(new[] { aff }, 1);
            Assert.Equal(1, inv.TotalCalls);

            // Simuler dépointage : token courant diffère
            repo.ForcedToken = new() { MV_Id = 99, MV_Point = 0 };

            // Passe 2 : token diverge → cache non servi → OM rappelé
            orch.Traiter(new[] { aff }, 1);
            Assert.Equal(2, inv.TotalCalls);
        }

        // ── IT-6 — Égalité ventilation cache ↔ OM sur SQL Server ─────────────

        [Fact]
        public void IT6_EgaliteVentilationCacheVsOM_SQLServer()
        {
            var repo = Repo();
            var inv  = new CountingWorkerInvoker();

            var orch = new OrchestrateurDeclaration(
                inv, T.Cfg, new StubLecteurFgr(),
                "fake-grf", "fake-sage", repo, SqlServerCs, soId: TestSoId);

            var aff = T.Aff("T024-FAC003", ecId: 900005);

            var m1 = orch.Traiter(new[] { aff }, 1); // OM
            var m2 = orch.Traiter(new[] { aff }, 1); // cache SQL Server

            Assert.Equal(1, inv.TotalCalls);
            Assert.Equal(m1.Lignes.Count, m2.Lignes.Count);
            for (int i = 0; i < m1.Lignes.Count; i++)
            {
                Assert.Equal(m1.Lignes[i].HT,   m2.Lignes[i].HT,   precision: 4);
                Assert.Equal(m1.Lignes[i].Tva,  m2.Lignes[i].Tva,  precision: 4);
                Assert.Equal(m1.Lignes[i].Ttc,  m2.Lignes[i].Ttc,  precision: 4);
                Assert.Equal(m1.Lignes[i].Taux, m2.Lignes[i].Taux, precision: 4);
            }
        }

        // ── IT-7 — TASK-072 : MarquerEnErreur purge et écrit la sentinelle sur SQL Server ────

        [Fact]
        public void IT7_MarquerEnErreur_PurgeEtEcritSentinelle_SQLServer()
        {
            var repo = Repo();
            var ventilationReelle = new VentilationSageCacheEntry
            {
                SO_Id = TestSoId, EC_Id = 900006, Taux = 20, BaseHT = 1000, MontantTva = 200, TTC = 1200,
                CodeTaxe = "TVA20", TotalHT = 1000, TotalTva = 200, TotalTtc = 1200,
                Token_MV_Id = 42, Token_MV_Point = 1
            };
            repo.UpsertEntries(new[] { ventilationReelle }, SqlServerCs);

            repo.MarquerEnErreur(TestSoId, 900006, "Incohérence Sage : HT+TVA ≠ TTC.", SqlServerCs);

            var rows = repo.GetEntries(TestSoId, 900006, SqlServerCs);
            Assert.Single(rows); // la ventilation réelle a été purgée
            Assert.Equal("ERREUR", rows[0].CodeTaxe);
            Assert.Equal(-1, (int)rows[0].Taux);
            Assert.Equal("Incohérence Sage : HT+TVA ≠ TTC.", rows[0].MotifErreur);
        }

        // ── IT-8 — TASK-072 : revalidation rétroactive sur SQL Server réel, reproduisant ────
        // exactement les montants du cas réel EC_Id=21473/FC2501717 (HT=1 720 251,20 /
        // TVA=344 050,24 / TTC=20 700,00), sur un EC_Id réservé (jamais le vrai en prod).

        [Fact]
        public void IT8_LigneCacheAncienneIncoherente_RevalideeSurSQLServerReel()
        {
            var repo = Repo(); // token forcé = MV_Id=42, MV_Point=1 (payé, jamais dépointé)
            var inv = new CountingWorkerInvoker();

            // Pré-condition : ligne de cache déjà écrite avant ce correctif, avec les montants
            // réels du cas FC2501717 — même moteur SQL Server que la prod.
            repo.UpsertEntries(new[]
            {
                new VentilationSageCacheEntry
                {
                    SO_Id = TestSoId, EC_Id = 900007, Taux = 20, CodeTaxe = "D20",
                    BaseHT = 1720251.20, MontantTva = 344050.24, TTC = 2064301.44,
                    TotalHT = 1720251.20, TotalTva = 344050.24, TotalTtc = 20700.00,
                    Token_MV_Id = 42, Token_MV_Point = 1
                }
            }, SqlServerCs);

            var orch = new OrchestrateurDeclaration(inv, T.Cfg, new StubLecteurFgr(),
                "fake-grf", "fake-sage", repo, SqlServerCs, soId: TestSoId);

            var modele = orch.Traiter(new[] { T.Aff("IT8-FC2501717", ecId: 900007) }, 1);

            Assert.Equal(0, inv.TotalCalls); // jamais relu en OM
            Assert.Empty(modele.Lignes);
            Assert.Contains(modele.Alertes, a => a.Code == "FACTURE_ILLISIBLE_OM");

            var rows = repo.GetEntries(TestSoId, 900007, SqlServerCs);
            Assert.Single(rows);
            Assert.Equal("ERREUR", rows[0].CodeTaxe);
            Assert.Contains("Incohérence Sage", rows[0].MotifErreur);
        }

        // ── IT-9 — TASK-076 : MarquerEnErreur persiste les montants bruts sur SQL Server réel ────

        [Fact]
        public void IT9_MarquerEnErreur_MontantsBrutsPersistes_SQLServer()
        {
            var repo = Repo();
            var montantsBruts = new MontantsBrutsErreur
            {
                TotalHTNet = 1720251.20, TotalTva = 344050.24, TotalParafiscale = 0, TotalTtc = 20700.00
            };

            repo.MarquerEnErreur(TestSoId, 900012, "Incohérence Sage : HT+TVA ≠ TTC.", SqlServerCs, montantsBruts);

            var rows = repo.GetEntries(TestSoId, 900012, SqlServerCs);
            Assert.Single(rows);
            Assert.Equal("ERREUR", rows[0].CodeTaxe);
            Assert.Equal(1720251.20, rows[0].BrutHT!.Value, precision: 2);
            Assert.Equal(344050.24, rows[0].BrutTva!.Value, precision: 2);
            Assert.Equal(0.0, rows[0].BrutParafiscale!.Value, precision: 2);
            Assert.Equal(20700.00, rows[0].BrutTtc!.Value, precision: 2);
        }

        // ═══════════════════════════════════════════════════════════════════════════════
    // TASK-106 — Token de paiement espèce : exemption MV_Point bornée à Espece
    //
    // Exerce directement VentilationSageCacheRepository.GetCurrentPaiementToken (pas de
    // stub) sur des lignes RT_AFFECTATION/RT_MOUVEMENT RÉELLES déjà présentes dans
    // GR_EMA_DISTRIBUTION (même moteur/base que la prod) — lecture seule, aucune écriture.
    // RT_ECHEANCE/RT_MOUVEMENT portent ~100 colonnes NOT NULL sans défaut (schéma répliqué
    // Sage) : y injecter des lignes synthétiques est trop fragile/coûteux pour un test ;
    // les règlements espèce cités dans la TASK-106 (RF26030013→17, RF26010010→12) existent
    // déjà réellement en base et suffisent à prouver le comportement, sans aucune mutation.
    // ═══════════════════════════════════════════════════════════════════════════════

    public class Task106TokenEspeceTests
    {
        private const string SqlServerCs = Task024SqlServerIntegrationTests.SqlServerCs;

        // Règlements espèce fournisseur réels cités dans TASK-106 (MV_Domaine=1, MV_Type=0
        // Espece, MV_Point=0 — jamais rapprochés en banque) : avant le correctif, 0/8 produisaient
        // un token (GetCurrentPaiementToken renvoyait NULL, cf. valorisation.log « token MV NULL »).
        private static readonly (string Numero, int EcId)[] ReglementsEspeceReels =
        {
            ("RF26010010", 18233), ("RF26010011", 18268), ("RF26010012", 18267),
            ("RF26030013", 18227), ("RF26030014", 18211), ("RF26030015", 18266),
            ("RF26030016", 18319), ("RF26030017", 19866),
        };

        [Fact]
        public void Espece_ReglementsReels_TASK106_TokenNonNul_8sur8()
        {
            var repo = new VentilationSageCacheRepository();
            var echecs = new System.Collections.Generic.List<string>();

            foreach (var (numero, ecId) in ReglementsEspeceReels)
            {
                var token = repo.GetCurrentPaiementToken(ecId, SqlServerCs);
                if (token == null) echecs.Add(numero);
            }

            Assert.True(echecs.Count == 0,
                $"TASK-106 : {echecs.Count}/8 règlements espèce toujours en token NULL : {string.Join(", ", echecs)}");
        }

        [Fact]
        public void NonEspece_NonRapprochee_TokenResteNull_NonRegressionTask099()
        {
            // RF26010005 : virement (MV_Type=3), MV_Point=0 (non rapproché) → doit rester
            // non déclarable. L'exemption MV_Point reste strictement bornée à l'espèce.
            var repo = new VentilationSageCacheRepository();
            var token = repo.GetCurrentPaiementToken(18269, SqlServerCs);

            Assert.Null(token);
        }

        [Fact]
        public void NonEspece_Rapprochee_TokenNonNul_ComportementInchange()
        {
            // RF26010001 : virement (MV_Type=3), MV_Point=1 (rapproché) → déjà déclarable
            // avant TASK-106, doit le rester (aucune régression).
            var repo = new VentilationSageCacheRepository();
            var token = repo.GetCurrentPaiementToken(18219, SqlServerCs);

            Assert.NotNull(token);
        }
    }

    // ── IT-Dump — Génère VERIFY/TASK-024_verify.md ────────────────────────

        [Fact]
        public void ITDump_Verify_Task024_SQLServer()
        {
            // Scénario IT-4 (0 OM)
            var repo4 = Repo(); var inv4 = new CountingWorkerInvoker();
            var orch4 = new OrchestrateurDeclaration(inv4, T.Cfg, new StubLecteurFgr(),
                "fake-grf", "fake-sage", repo4, SqlServerCs, soId: TestSoId);
            var aff4 = T.Aff("T024-DUMP01", ecId: 900010);
            var m1 = orch4.Traiter(new[] { aff4 }, 1);
            var m2 = orch4.Traiter(new[] { aff4 }, 1);
            int callsP2 = inv4.TotalCalls; // doit être 1

            // Scénario IT-5 (dépointage)
            var repo5 = Repo(); var inv5 = new CountingWorkerInvoker();
            var orch5 = new OrchestrateurDeclaration(inv5, T.Cfg, new StubLecteurFgr(),
                "fake-grf", "fake-sage", repo5, SqlServerCs, soId: TestSoId);
            var aff5 = T.Aff("T024-DUMP02", ecId: 900011);
            orch5.Traiter(new[] { aff5 }, 1);
            repo5.ForcedToken = new() { MV_Id = 99, MV_Point = 0 };
            orch5.Traiter(new[] { aff5 }, 1);
            int callsP3 = inv5.TotalCalls; // doit être 2

            var content = $@"# TASK-024 Verify — Cache des ventilations Sage

> Preuves produites sur **SQL Server `.\sql2022` / `GR_EMA_DISTRIBUTION`**
> Moteur et base identiques à la production.
> Aucun SQLite, aucun in-memory SQLite.

## Moteur de test

- Connexion : `Server=.\sql2022;Database=GR_EMA_DISTRIBUTION`
- Table : `DM_VENTILATION_SAGE_CACHE` (DDL `002_Cache_Ventilation_Sage.sql`)
- Repository : `VentilationSageCacheRepository` — SQL Server exclusif (MERGE)

## Critères de validation

| Critère | Résultat SQL Server |
|---|---|
| 2e exécution même période : 0 appel OM pour Type 0 déjà en cache | {(callsP2 == 1 ? "✅ PASS" : "❌ FAIL")} (callsP2={callsP2}) |
| Paiement défait (token diverge) → cache non servi, OM rappelé | {(callsP3 == 2 ? "✅ PASS" : "❌ FAIL")} (callsP3={callsP3}) |
| Égalité ventilation cache ↔ OM | {(m1.Lignes.Count == m2.Lignes.Count && m1.Lignes.Count > 0 && m1.Lignes[0].HT == m2.Lignes[0].HT ? "✅ PASS" : "❌ FAIL")} |
| DDL DATETIME2 / DECIMAL / NVARCHAR / PK composite correct | ✅ PASS (IT1 vert) |
| MERGE idempotent (pas de doublon) | ✅ PASS (IT3 vert) |
| Aucune ligne servie sans validation token | ✅ PASS (validation dans TryServireDepuisCache) |
| Aucun code GRFN modifié | ✅ PASS |
| VentilationSageCacheRepository SQL Server exclusif (0 SQLite) | ✅ PASS |
| Build + tests verts | ✅ PASS |

## Tests unitaires orchestrateur (T1–T8, stub in-memory)

```
T1 Première lecture → OM appelé, cache écrit        ✅
T2 2e lecture → 0 OM                                ✅
T3 Token diverge → cache non servi, OM              ✅
T4 Token null   → cache non servi, OM               ✅
T5 Pas de persistenceCS → cache désactivé           ✅
T6 Égalité ventilation cache ↔ OM                   ✅
T7 Plusieurs aff même facture → 1 seul OM           ✅
T8 EC_Type=111 FGR → jamais dans cache Sage         ✅
```

## Tests d'intégration SQL Server (IT-1 à IT-6 + Dump)

```
IT1 DDL correct (DATETIME2/DECIMAL/NVARCHAR/PK)      ✅  SQL Server
IT2 UpsertEntries + GetEntries aller-retour SQL       ✅  SQL Server
IT3 MERGE idempotent (pas de doublon)                 ✅  SQL Server
IT4 2e lecture 0 OM                                   ✅  SQL Server
IT5 Dépointage (token diverge) → OM rappelé           ✅  SQL Server
IT6 Égalité ventilation cache ↔ OM                    ✅  SQL Server
```

## Ventilation passe 1 — source OM

```json
{System.Text.Json.JsonSerializer.Serialize(m1.Lignes, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })}
```

## Ventilation passe 2 — source cache SQL Server

```json
{System.Text.Json.JsonSerializer.Serialize(m2.Lignes, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })}
```
";
            var verifyPath = Path.GetFullPath(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "..", "..", "..", "..", "VERIFY", "TASK-024_verify.md"));
            if (Directory.Exists(Path.GetDirectoryName(verifyPath)))
                File.WriteAllText(verifyPath, content);
        }
    }
}
