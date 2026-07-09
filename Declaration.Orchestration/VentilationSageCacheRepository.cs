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
                       Token_MV_Id, Token_MV_Point
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

        // ──────────────────────────────────────────────────────────────────────
        // Écriture du cache — upsert idempotent via MERGE SQL Server
        // ──────────────────────────────────────────────────────────────────────

        public void UpsertEntries(IEnumerable<VentilationSageCacheEntry> entries, string persistenceConnectionString)
        {
            using var conn = new SqlConnection(persistenceConnectionString);
            conn.Open();

            foreach (var e in entries)
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
