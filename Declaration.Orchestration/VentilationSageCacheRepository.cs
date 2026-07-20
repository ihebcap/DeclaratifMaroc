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
    /// - Lecture/écriture du cache : table DM_VENTILATION_SAGE_CACHE (PersistenceConnection SQL Server).
    ///   TASK-118 : clé (SO_Id, EC_Id, Taux) — SO_Id désambiguïse EC_Id entre bases Sage distinctes.
    /// - Validation du paiement : requête locale sur RT_AFFECTATION/RT_MOUVEMENT (GrfConnection SQL Server).
    /// </summary>
    public class VentilationSageCacheRepository : IVentilationSageCacheRepository
    {
        // ──────────────────────────────────────────────────────────────────────
        // Lecture du cache
        // ──────────────────────────────────────────────────────────────────────

        public IReadOnlyList<VentilationSageCacheEntry> GetEntries(int soId, int ecId, string persistenceConnectionString)
        {
            using var conn = new SqlConnection(persistenceConnectionString);
            conn.Open();
            var sql = @"
                SELECT SO_Id, EC_Id, Taux, BaseHT, MontantTva, TTC, CodeTaxe,
                       TotalHT, TotalTva, TotalTtc,
                       Token_MV_Id, Token_MV_Point, MotifErreur,
                       BrutHT, BrutTva, BrutParafiscale, BrutTtc
                FROM   DM_VENTILATION_SAGE_CACHE
                WHERE  SO_Id = @SoId AND EC_Id = @EcId";
            return conn.Query<VentilationSageCacheEntry>(sql, new { SoId = soId, EcId = ecId })
                       .ToList();
        }

        // ──────────────────────────────────────────────────────────────────────
        // Validation du paiement (connexion GRF SQL Server, jamais Sage)
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Retourne le token de paiement courant pour l'EC_Id donné depuis RT_AFFECTATION/RT_MOUVEMENT.
        /// Critères identiques à la sélection d'éligibilité (<c>SelectionExpliqueeEvaluator</c>) :
        /// affectation présente + (MV_Point = 1 OU règlement fournisseur en espèce — TASK-106).
        /// Retourne null si aucune affectation valide n'existe (facture non/dépayée).
        /// </summary>
        public virtual PaiementToken? GetCurrentPaiementToken(int ecId, string grfConnectionString)
        {
            using var conn = new SqlConnection(grfConnectionString);
            conn.Open();

            // TASK-106 : l'espèce n'a pas d'état de rapprochement bancaire (MV_Point reste à 0),
            // exactement comme SelectionExpliqueeEvaluator.cs:85 l'exempte de MV_Point à l'éligibilité.
            // Bornage STRICT à la source Espèce (MV_Domaine = Domaine_ReglementFournisseur (1) ET
            // MV_Type = ModePaiement_Espece (0)) : jamais généralisé à un non-espèce non rapproché
            // (régression fiscale directe de TASK-099 sinon). Token de fraîcheur = MV_Id + MV_Point
            // tel que lu (pas de valeur figée) : si l'espèce est un jour rapprochée, MV_Point passe à 1
            // et le cache est naturellement invalidé (comparaison stricte côté OrchestrateurDeclaration).
            // On sélectionne le premier MV pointé (MV_Point = 1) lié à cette échéance ; si plusieurs
            // affectations → on prend la première ; le token est identique car c'est l'état du
            // mouvement (pointé/dépointé, ou espèce) qui compte.
            var sql = @"
                SELECT TOP 1
                    M.MV_Id    AS MV_Id,
                    M.MV_Point AS MV_Point
                FROM RT_AFFECTATION A
                JOIN RT_MOUVEMENT   M ON M.MV_Id = A.MV_Id
                WHERE A.EC_Id = @EcId
                  AND (M.MV_Point = 1
                       OR (M.MV_Domaine = 1 AND M.MV_Type = 0))
                ORDER BY M.MV_Point DESC";

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
        public virtual void MarquerEnErreur(int soId, int ecId, string motif, string persistenceConnectionString,
            MontantsBrutsErreur? montantsBruts = null)
        {
            using var conn = new SqlConnection(persistenceConnectionString);
            conn.Open();
            using var tx = conn.BeginTransaction();

            conn.Execute("DELETE FROM DM_VENTILATION_SAGE_CACHE WHERE SO_Id = @SoId AND EC_Id = @EcId",
                new { SoId = soId, EcId = ecId }, tx);

            // TASK-076 : BrutHT/BrutTva/BrutParafiscale/BrutTtc portent les montants Sage tels que
            // lus au moment de la détection (AVANT exclusion) — NULL si non fournis (jamais une
            // valeur inventée). Sert l'investigation manuelle côté ERP sur l'écran Factures.
            conn.Execute(@"
                INSERT INTO DM_VENTILATION_SAGE_CACHE
                    (SO_Id, EC_Id, Taux, BaseHT, MontantTva, TTC, CodeTaxe,
                     TotalHT, TotalTva, TotalTtc,
                     Token_MV_Id, Token_MV_Point, DateLecture, Source, MotifErreur,
                     BrutHT, BrutTva, BrutParafiscale, BrutTtc)
                VALUES
                    (@SoId, @EcId, -1, 0, 0, 0, 'ERREUR',
                     0, 0, 0,
                     NULL, NULL, @DateLecture, 'OM', @Motif,
                     @BrutHT, @BrutTva, @BrutParafiscale, @BrutTtc)",
                new
                {
                    SoId = soId,
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
        public virtual void SupprimerEntrees(int soId, int ecId, string persistenceConnectionString)
        {
            using var conn = new SqlConnection(persistenceConnectionString);
            conn.Open();
            conn.Execute("DELETE FROM DM_VENTILATION_SAGE_CACHE WHERE SO_Id = @SoId AND EC_Id = @EcId",
                new { SoId = soId, EcId = ecId });
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

            // TASK-076/077 : purge toute sentinelle ERREUR résiduente (Taux=-1) pour ces (SO_Id,EC_Id)
            // AVANT d'écrire des buckets réels — sinon les deux coexistent (clé MERGE distincte) et
            // cassent l'hypothèse « une seule ligne ERREUR » utilisée par TryServireDepuisCache
            // (cache miss forcé, revalidation rétroactive). Une écriture normale supplante toujours
            // une sentinelle d'erreur précédente pour la même (société, facture).
            // TASK-118 : bornée à SO_Id — sinon une purge pour une société purgerait aussi la
            // sentinelle d'une AUTRE société dont l'EC_Id collisionne (deux bases Sage distinctes).
            foreach (var (soId, ecId) in list.Select(e => (e.SO_Id, e.EC_Id)).Distinct())
            {
                conn.Execute(
                    "DELETE FROM DM_VENTILATION_SAGE_CACHE WHERE SO_Id = @SoId AND EC_Id = @EcId AND CodeTaxe = 'ERREUR'",
                    new { SoId = soId, EcId = ecId });
            }

            foreach (var e in list)
                UpsertOne(conn, e);
        }

        private static void UpsertOne(SqlConnection conn, VentilationSageCacheEntry e)
        {
            var sql = @"
                MERGE DM_VENTILATION_SAGE_CACHE AS target
                USING (SELECT @SO_Id AS SO_Id, @EC_Id AS EC_Id, @Taux AS Taux) AS source
                   ON target.SO_Id = source.SO_Id AND target.EC_Id = source.EC_Id AND target.Taux = source.Taux
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
                    INSERT (SO_Id, EC_Id, Taux, BaseHT, MontantTva, TTC, CodeTaxe,
                            TotalHT, TotalTva, TotalTtc,
                            Token_MV_Id, Token_MV_Point, DateLecture, Source)
                    VALUES (@SO_Id, @EC_Id, @Taux, @BaseHT, @MontantTva, @TTC, @CodeTaxe,
                            @TotalHT, @TotalTva, @TotalTtc,
                            @Token_MV_Id, @Token_MV_Point, @DateLecture, @Source);";

            conn.Execute(sql, new
            {
                e.SO_Id,
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
