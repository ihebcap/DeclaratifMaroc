using System;
using System.Collections.Generic;
using System.Linq;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Declaration.Orchestration
{
    /// <summary>
    /// Implémentation SQL Server du cache des ventilations Sage.
    ///
    /// CIBLE EXCLUSIVE : SQL Server (base de persistance GRF/GRC dédiée).
    /// Aucun SQLite, aucun fallback fichier.
    ///
    /// - Lecture/écriture du cache : table GRC_VENTILATION_SAGE_CACHE (PersistenceConnection SQL Server).
    /// - Validation du paiement : requête locale sur RT_AFFECTATION/RT_MOUVEMENT (GrfConnection SQL Server).
    /// </summary>
    public class VentilationSageCacheRepository : IVentilationSageCacheRepository
    {
        // ──────────────────────────────────────────────────────────────────────
        // Lecture du cache
        // ──────────────────────────────────────────────────────────────────────

        public IReadOnlyList<VentilationSageCacheEntry> GetEntries(int ecId, string persistenceConnectionString)
        {
            using var conn = new SqlConnection(persistenceConnectionString);
            conn.Open();
            var sql = @"
                SELECT EC_Id, Taux, BaseHT, MontantTva, TTC, CodeTaxe,
                       TotalHT, TotalTva, TotalTtc,
                       Token_MV_Id, Token_MV_Point, MotifErreur,
                       BrutHT, BrutTva, BrutParafiscale, BrutTtc
                FROM   GRC_VENTILATION_SAGE_CACHE
                WHERE  EC_Id = @EcId";
            return conn.Query<VentilationSageCacheEntry>(sql, new { EcId = ecId })
                       .ToList();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Validation du paiement (connexion GRF SQL Server, jamais Sage)
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Retourne le token de paiement courant pour l'EC_Id donné depuis RT_AFFECTATION/RT_MOUVEMENT.
        /// Critères identiques à la sélection d'éligibilité : affectation présente + MV_Point = 1.
        /// Retourne null si aucune affectation pointée n'existe (facture non/dépayée).
        /// </summary>
        public virtual PaiementToken? GetCurrentPaiementToken(int ecId, string grfConnectionString)
        {
            using var conn = new SqlConnection(grfConnectionString);
            conn.Open();

            // On sélectionne le premier MV pointé (MV_Point = 1) lié à cette échéance.
            // Si plusieurs affectations → on prend la première ; le token est identique
            // car c'est l'état du mouvement (pointé/dépointé) qui compte.
            var sql = @"
                SELECT TOP 1
                    M.MV_Id    AS MV_Id,
                    M.MV_Point AS MV_Point
                FROM RT_AFFECTATION A
                JOIN RT_MOUVEMENT   M ON M.MV_Id = A.MV_Id
                WHERE A.EC_Id    = @EcId
                  AND M.MV_Point = 1";

            return conn.QuerySingleOrDefault<PaiementToken>(sql, new { EcId = ecId });
        }

        /// <summary>
        /// TASK-072 : montant en devise de l'échéance (RT_ECHEANCE.EC_MtDevise), source GRF
        /// indépendante du TTC lu côté Sage — sert de contrôle croisé.
        /// </summary>
        public virtual decimal? GetEcheanceMontantDevise(int ecId, string grfConnectionString)
        {
            using var conn = new SqlConnection(grfConnectionString);
            conn.Open();

            var sql = "SELECT EC_MtDevise FROM RT_ECHEANCE WHERE EC_Id = @EcId";
            return conn.QuerySingleOrDefault<decimal?>(sql, new { EcId = ecId });
        }

        /// <summary>
        /// TASK-072 : purge toute ventilation existante pour cet EC_Id et la remplace par une
        /// ligne sentinelle unique (Taux=-1, CodeTaxe="ERREUR") portant le motif exact.
        /// DELETE puis INSERT dans la même connexion — jamais de MERGE ici, la sentinelle
        /// doit être seule (aucun bucket réel résiduel ne doit rester mêlé).
        /// </summary>
        public virtual void MarquerEnErreur(int ecId, string motif, string persistenceConnectionString,
            MontantsBrutsErreur? montantsBruts = null)
        {
            using var conn = new SqlConnection(persistenceConnectionString);
            conn.Open();
            using var tx = conn.BeginTransaction();

            conn.Execute("DELETE FROM GRC_VENTILATION_SAGE_CACHE WHERE EC_Id = @EcId", new { EcId = ecId }, tx);

            // TASK-076 : BrutHT/BrutTva/BrutParafiscale/BrutTtc portent les montants Sage tels que
            // lus au moment de la détection (AVANT exclusion) — NULL si non fournis (jamais une
            // valeur inventée). Sert l'investigation manuelle côté ERP sur l'écran Factures.
            conn.Execute(@"
                INSERT INTO GRC_VENTILATION_SAGE_CACHE
                    (EC_Id, Taux, BaseHT, MontantTva, TTC, CodeTaxe,
                     TotalHT, TotalTva, TotalTtc,
                     Token_MV_Id, Token_MV_Point, DateLecture, Source, MotifErreur,
                     BrutHT, BrutTva, BrutParafiscale, BrutTtc)
                VALUES
                    (@EcId, -1, 0, 0, 0, 'ERREUR',
                     0, 0, 0,
                     NULL, NULL, @DateLecture, 'OM', @Motif,
                     @BrutHT, @BrutTva, @BrutParafiscale, @BrutTtc)",
                new
                {
                    EcId = ecId,
                    DateLecture = DateTime.UtcNow,
                    Motif = motif,
                    BrutHT = montantsBruts?.TotalHTNet,
                    BrutTva = montantsBruts?.TotalTva,
                    BrutParafiscale = montantsBruts?.TotalParafiscale,
                    BrutTtc = montantsBruts?.TotalTtc
                }, tx);

            tx.Commit();
        }

        /// <summary>
        /// TASK-078 : purge sans réécrire — force le prochain appel à traiter cet EC_Id comme un
        /// cache miss (relecture Sage réelle), y compris si la sentinelle actuelle porte déjà des
        /// montants bruts capturés (TASK-076, qui sinon la considérerait définitivement réglée).
        /// </summary>
        public virtual void SupprimerEntrees(int ecId, string persistenceConnectionString)
        {
            using var conn = new SqlConnection(persistenceConnectionString);
            conn.Open();
            conn.Execute("DELETE FROM GRC_VENTILATION_SAGE_CACHE WHERE EC_Id = @EcId", new { EcId = ecId });
        }

        // ──────────────────────────────────────────────────────────────────────
        // Écriture du cache — upsert idempotent via MERGE SQL Server
        // ──────────────────────────────────────────────────────────────────────

        public void UpsertEntries(IEnumerable<VentilationSageCacheEntry> entries, string persistenceConnectionString)
        {
            var list = entries.ToList();
            if (list.Count == 0) return;

            using var conn = new SqlConnection(persistenceConnectionString);
            conn.Open();

            // TASK-076/077 : purge toute sentinelle ERREUR résiduente (Taux=-1) pour ces EC_Id AVANT
            // d'écrire des buckets réels — sinon les deux coexistent (clé MERGE (EC_Id,Taux) distincte)
            // et cassent l'hypothèse « une seule ligne ERREUR » utilisée par TryServireDepuisCache
            // (cache miss forcé, revalidation rétroactive). Une écriture normale supplante toujours
            // une sentinelle d'erreur précédente pour le même EC_Id.
            foreach (var ecId in list.Select(e => e.EC_Id).Distinct())
            {
                conn.Execute(
                    "DELETE FROM GRC_VENTILATION_SAGE_CACHE WHERE EC_Id = @EcId AND CodeTaxe = 'ERREUR'",
                    new { EcId = ecId });
            }

            foreach (var e in list)
                UpsertOne(conn, e);
        }

        private static void UpsertOne(SqlConnection conn, VentilationSageCacheEntry e)
        {
            var sql = @"
                MERGE GRC_VENTILATION_SAGE_CACHE AS target
                USING (SELECT @EC_Id AS EC_Id, @Taux AS Taux) AS source
                   ON target.EC_Id = source.EC_Id AND target.Taux = source.Taux
                WHEN MATCHED THEN
                    UPDATE SET
                        BaseHT         = @BaseHT,
                        MontantTva     = @MontantTva,
                        TTC            = @TTC,
                        CodeTaxe       = @CodeTaxe,
                        TotalHT        = @TotalHT,
                        TotalTva       = @TotalTva,
                        TotalTtc       = @TotalTtc,
                        Token_MV_Id    = @Token_MV_Id,
                        Token_MV_Point = @Token_MV_Point,
                        DateLecture    = @DateLecture,
                        Source         = @Source
                WHEN NOT MATCHED THEN
                    INSERT (EC_Id, Taux, BaseHT, MontantTva, TTC, CodeTaxe,
                            TotalHT, TotalTva, TotalTtc,
                            Token_MV_Id, Token_MV_Point, DateLecture, Source)
                    VALUES (@EC_Id, @Taux, @BaseHT, @MontantTva, @TTC, @CodeTaxe,
                            @TotalHT, @TotalTva, @TotalTtc,
                            @Token_MV_Id, @Token_MV_Point, @DateLecture, @Source);";

            conn.Execute(sql, new
            {
                e.EC_Id,
                e.Taux,
                e.BaseHT,
                e.MontantTva,
                e.TTC,
                e.CodeTaxe,
                e.TotalHT,
                e.TotalTva,
                e.TotalTtc,
                e.Token_MV_Id,
                e.Token_MV_Point,
                DateLecture = DateTime.UtcNow,
                Source = "OM"
            });
        }
    }
}
