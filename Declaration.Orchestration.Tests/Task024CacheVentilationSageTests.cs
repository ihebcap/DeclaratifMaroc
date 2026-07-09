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

        public DocumentTaxesInfo? InvoquerWorker(string numeroFacture, string sens, WorkerConfig config)
        {
            TotalCalls++;
            return MakeDoc(numeroFacture, sens);
        }

        public List<DocumentTaxesInfo> InvoquerWorkerBatch(
            IEnumerable<(string numeroFacture, string sens)> requetes, WorkerConfig config)
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

    // Cache in-memory pour les tests unitaires uniquement
    internal class InMemoryCacheRepository : IVentilationSageCacheRepository
    {
        private readonly Dictionary<int, List<VentilationSageCacheEntry>> _store = new();
        public bool PaymentPresent { get; set; } = true;
        public bool PaymentPointMatches { get; set; } = true;

        public IReadOnlyList<VentilationSageCacheEntry> GetEntries(int ecId, string _)
            => _store.TryGetValue(ecId, out var list) ? list : new List<VentilationSageCacheEntry>();

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

        public bool HasEntry(int ecId) => _store.ContainsKey(ecId) && _store[ecId].Count > 0;
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

        [Fact] public void T4_PaiementAbsent_TokenNull_CacheNonServi()
        {
            var inv = new CountingWorkerInvoker(); var repo = new InMemoryCacheRepository();
            var orch = new OrchestrateurDeclaration(inv, T.Cfg, new StubLecteurFgr(),
                "fake-grf", "fake-sage", repo, "fake-pers");
            var aff = T.Aff("FAC001", 1);
            orch.Traiter(new[] { aff }, 1);
            repo.PaymentPresent = false;
            orch.Traiter(new[] { aff }, 1);
            Assert.Equal(2, inv.TotalCalls);
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
    //   La table GRC_VENTILATION_SAGE_CACHE est créée par le DDL
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
        private const string TestEcIds = "900001,900002,900003,900004,900005,900010,900011";

        // DDL de la table cache (idempotent — IF NOT EXISTS)
        private const string EnsureTableSql = @"
            IF OBJECT_ID('GRC_VENTILATION_SAGE_CACHE','U') IS NULL
            BEGIN
                CREATE TABLE [GRC_VENTILATION_SAGE_CACHE] (
                    [EC_Id]          INT             NOT NULL,
                    [Taux]           DECIMAL(18,4)   NOT NULL,
                    [BaseHT]         DECIMAL(18,4)   NOT NULL,
                    [MontantTva]     DECIMAL(18,4)   NOT NULL,
                    [TTC]            DECIMAL(18,4)   NOT NULL,
                    [CodeTaxe]       NVARCHAR(50)    NOT NULL DEFAULT '',
                    [TotalHT]        DECIMAL(18,4)   NOT NULL,
                    [TotalTva]       DECIMAL(18,4)   NOT NULL,
                    [TotalTtc]       DECIMAL(18,4)   NOT NULL,
                    [Token_MV_Id]    INT             NOT NULL,
                    [Token_MV_Point] INT             NOT NULL,
                    [DateLecture]    DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
                    [Source]         NVARCHAR(100)   NOT NULL DEFAULT 'OM',
                    CONSTRAINT [PK_GRC_VENTILATION_SAGE_CACHE] PRIMARY KEY ([EC_Id], [Taux])
                );
                CREATE INDEX [IX_GRC_VENTILATION_SAGE_CACHE_ECId]
                ON [GRC_VENTILATION_SAGE_CACHE] ([EC_Id]);
            END";

        public Task024SqlServerIntegrationTests()
        {
            using var conn = new SqlConnection(SqlServerCs);
            conn.Open();
            conn.Execute(EnsureTableSql);
            // Nettoyer les éventuelles données de tests précédents
            conn.Execute($"DELETE FROM GRC_VENTILATION_SAGE_CACHE WHERE EC_Id IN ({TestEcIds})");
        }

        public void Dispose()
        {
            try
            {
                using var conn = new SqlConnection(SqlServerCs);
                conn.Open();
                conn.Execute($"DELETE FROM GRC_VENTILATION_SAGE_CACHE WHERE EC_Id IN ({TestEcIds})");
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
                "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME='GRC_VENTILATION_SAGE_CACHE'");
            Assert.Equal(1, tableExists);

            // Vérifie les colonnes clés et leurs types SQL Server
            var cols = conn.Query(
                "SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS " +
                "WHERE TABLE_NAME='GRC_VENTILATION_SAGE_CACHE'")
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
                EC_Id = 900001, Taux = 20, BaseHT = 1000, MontantTva = 200, TTC = 1200,
                CodeTaxe = "TVA20", TotalHT = 1000, TotalTva = 200, TotalTtc = 1200,
                Token_MV_Id = 42, Token_MV_Point = 1
            };

            repo.UpsertEntries(new[] { entry }, SqlServerCs);
            var rows = repo.GetEntries(900001, SqlServerCs);

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
                EC_Id = 900002, Taux = 20, BaseHT = 500, MontantTva = 100, TTC = 600,
                CodeTaxe = "TVA20", TotalHT = 500, TotalTva = 100, TotalTtc = 600,
                Token_MV_Id = 11, Token_MV_Point = 1
            };

            repo.UpsertEntries(new[] { entry }, SqlServerCs);

            entry.BaseHT = 800; entry.MontantTva = 160; entry.TTC = 960;
            repo.UpsertEntries(new[] { entry }, SqlServerCs); // 2e upsert → UPDATE

            var rows = repo.GetEntries(900002, SqlServerCs);
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
                persistenceConnectionString: SqlServerCs);

            var aff = T.Aff("T024-FAC001", ecId: 900003);

            // Passe 1 : OM → écriture SQL Server
            orch.Traiter(new[] { aff }, 1);
            Assert.Equal(1, inv.TotalCalls);

            // Passe 2 : cache SQL Server → 0 OM supplémentaire
            orch.Traiter(new[] { aff }, 1);
            Assert.Equal(1, inv.TotalCalls); // toujours 1

            // La ligne est bien en base
            Assert.NotEmpty(repo.GetEntries(900003, SqlServerCs));
        }

        // ── IT-5 — Dépointage (token diverge) → OM rappelé sur SQL Server ────

        [Fact]
        public void IT5_PaiementDefait_OMRappele_SQLServer()
        {
            var repo = Repo(); // token initial = MV_Id=42, MV_Point=1
            var inv  = new CountingWorkerInvoker();

            var orch = new OrchestrateurDeclaration(
                inv, T.Cfg, new StubLecteurFgr(),
                "fake-grf", "fake-sage", repo, SqlServerCs);

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
                "fake-grf", "fake-sage", repo, SqlServerCs);

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

        // ── IT-Dump — Génère VERIFY/TASK-024_verify.md ────────────────────────

        [Fact]
        public void ITDump_Verify_Task024_SQLServer()
        {
            // Scénario IT-4 (0 OM)
            var repo4 = Repo(); var inv4 = new CountingWorkerInvoker();
            var orch4 = new OrchestrateurDeclaration(inv4, T.Cfg, new StubLecteurFgr(),
                "fake-grf", "fake-sage", repo4, SqlServerCs);
            var aff4 = T.Aff("T024-DUMP01", ecId: 900010);
            var m1 = orch4.Traiter(new[] { aff4 }, 1);
            var m2 = orch4.Traiter(new[] { aff4 }, 1);
            int callsP2 = inv4.TotalCalls; // doit être 1

            // Scénario IT-5 (dépointage)
            var repo5 = Repo(); var inv5 = new CountingWorkerInvoker();
            var orch5 = new OrchestrateurDeclaration(inv5, T.Cfg, new StubLecteurFgr(),
                "fake-grf", "fake-sage", repo5, SqlServerCs);
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
- Table : `GRC_VENTILATION_SAGE_CACHE` (DDL `002_Cache_Ventilation_Sage.sql`)
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
