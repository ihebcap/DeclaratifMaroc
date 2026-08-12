using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Declaration.Application.Entities;
using Declaration.Application.Interfaces;
using Declaration.Selection; // RegleDatePeriode : source unique de la date de période (TASK-062)

namespace Declaration.Infrastructure.Repositories;

public class DeclarationRepository : IDeclarationRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DeclarationRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<DeclarationEntete?> GetByIdAsync(Guid id)
    {
        using var connection = _connectionFactory.CreatePersistenceConnection();
        return await connection.QuerySingleOrDefaultAsync<DeclarationEntete>(
            "SELECT * FROM DM_ENTTVA WHERE Id = @Id", new { Id = id.ToString() });
    }

    public async Task<DeclarationEntete?> GetByNumeroAsync(string numero)
    {
        using var connection = _connectionFactory.CreatePersistenceConnection();
        return await connection.QuerySingleOrDefaultAsync<DeclarationEntete>(
            "SELECT * FROM DM_ENTTVA WHERE Numero = @Numero", new { Numero = numero });
    }

    public async Task<IEnumerable<DeclarationEntete>> GetAllAsync(int societeId, int? exercice, StatutDeclaration? statut)
    {
        using var connection = _connectionFactory.CreatePersistenceConnection();
        var sql = "SELECT * FROM DM_ENTTVA WHERE SocieteId = @SocieteId";
        if (exercice.HasValue) sql += " AND Exercice = @Exercice";
        if (statut.HasValue) sql += " AND Statut = @Statut";

        return await connection.QueryAsync<DeclarationEntete>(sql, new { SocieteId = societeId, Exercice = exercice, Statut = statut });
    }

    public async Task CreateAsync(DeclarationEntete declaration)
    {
        using var connection = _connectionFactory.CreatePersistenceConnection();
        var sql = @"INSERT INTO DM_ENTTVA 
                    (Id, Numero, SocieteId, Exercice, Type, Periode, Statut, DateCreation, DateCloture)
                    VALUES (@Id, @Numero, @SocieteId, @Exercice, @Type, @Periode, @Statut, @DateCreation, @DateCloture)";
        await connection.ExecuteAsync(sql, new {
            Id = declaration.Id.ToString(),
            declaration.Numero,
            declaration.SocieteId,
            declaration.Exercice,
            Type = (int)declaration.Type,
            declaration.Periode,
            Statut = (int)declaration.Statut,
            DateCreation = declaration.DateCreation,
            DateCloture = declaration.DateCloture
        });
    }

    public async Task UpdateStatutAsync(Guid id, StatutDeclaration statut)
    {
        using var connection = _connectionFactory.CreatePersistenceConnection();
        var sql = "UPDATE DM_ENTTVA SET Statut = @Statut WHERE Id = @Id";
        await connection.ExecuteAsync(sql, new { Id = id.ToString(), Statut = (int)statut });
    }

    public async Task<bool> ExistsAsync(int societeId, int exercice, int periode, TypePeriode type)
    {
        using var connection = _connectionFactory.CreatePersistenceConnection();
        var count = await connection.ExecuteScalarAsync<int>(
            @"SELECT COUNT(1) FROM DM_ENTTVA
              WHERE SocieteId = @SocieteId AND Exercice = @Exercice AND Periode = @Periode AND Type = @Type",
            new { SocieteId = societeId, Exercice = exercice, Periode = periode, Type = (int)type });
        return count > 0;
    }

    /// <summary>TASK-079 : supprime d'abord les lignes (FK) puis l'entête.</summary>
    public async Task DeleteAsync(Guid declarationId)
    {
        using var connection = _connectionFactory.CreatePersistenceConnection();
        var id = declarationId.ToString();
        await connection.ExecuteAsync("DELETE FROM DM_LGTVA WHERE DeclarationId = @Id", new { Id = id });
        await connection.ExecuteAsync("DELETE FROM DM_SELECTION_REGLEMENT WHERE DeclarationId = @Id", new { Id = id });
        await connection.ExecuteAsync("DELETE FROM DM_ENTTVA WHERE Id = @Id", new { Id = id });
    }

    public async Task SaveSelectionReglementsAsync(Guid declarationId, IEnumerable<string> selectedNumeroReglements)
    {
        using var connection = _connectionFactory.CreatePersistenceConnection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            var idStr = declarationId.ToString();
            await connection.ExecuteAsync("DELETE FROM DM_SELECTION_REGLEMENT WHERE DeclarationId = @Id", new { Id = idStr }, transaction);
            
            var list = selectedNumeroReglements.Distinct().ToList();
            if (list.Count > 0)
            {
                var sql = "INSERT INTO DM_SELECTION_REGLEMENT (DeclarationId, NumeroReglement) VALUES (@DeclarationId, @NumeroReglement)";
                var parameters = list.Select(num => new { DeclarationId = idStr, NumeroReglement = num }).ToList();
                await connection.ExecuteAsync(sql, parameters, transaction);
            }
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public async Task<List<string>> GetSelectionReglementsAsync(Guid declarationId)
    {
        using var connection = _connectionFactory.CreatePersistenceConnection();
        var sql = "SELECT NumeroReglement FROM DM_SELECTION_REGLEMENT WHERE DeclarationId = @Id";
        var result = await connection.QueryAsync<string>(sql, new { Id = declarationId.ToString() });
        return result.ToList();
    }

    private sealed class AgregatRow
    {
        public string DeclarationId { get; set; } = "";
        public int NbLignes { get; set; }
        public int NbFactures { get; set; }
        public decimal MontantTva { get; set; }
    }

    /// <summary>
    /// TASK-079 : agrégats Lignes/Montant TVA (lignes Proposee/Integree uniquement) pour l'écran liste.
    /// NbFactures = nombre de factures DISTINCTES (NumeroFacture) — une facture peut porter plusieurs
    /// lignes DM_LGTVA (taux/code activité différents) ; NULL (frais bancaires, etc., sans NumeroFacture)
    /// n'est jamais compté par COUNT(DISTINCT), donc n'est jamais inclus dans NbFactures (comportement
    /// voulu : ce n'est pas une facture).
    /// </summary>
    public async Task<Dictionary<Guid, (int NbLignes, int NbFactures, decimal MontantTva)>> GetAgregatsListeAsync(IEnumerable<Guid> declarationIds)
    {
        var ids = declarationIds.Select(id => id.ToString()).Distinct().ToList();
        var result = new Dictionary<Guid, (int NbLignes, int NbFactures, decimal MontantTva)>();
        if (ids.Count == 0) return result;

        using var connection = _connectionFactory.CreatePersistenceConnection();
        var rows = await connection.QueryAsync<AgregatRow>(
            @"SELECT DeclarationId, COUNT(*) AS NbLignes, COUNT(DISTINCT NumeroFacture) AS NbFactures, SUM(TVA) AS MontantTva
              FROM DM_LGTVA
              WHERE DeclarationId IN @Ids AND Etat IN @EtatsDeclarables
              GROUP BY DeclarationId",
            new { Ids = ids, EtatsDeclarables = new[] { (int)EtatLigne.Proposee, (int)EtatLigne.Integree } });

        foreach (var r in rows)
            result[Guid.Parse(r.DeclarationId)] = (r.NbLignes, r.NbFactures, r.MontantTva);
        return result;
    }

    /// <summary>
    /// TASK-079 (garde-fou défensif) : nombre d'affectations déjà tamponnées (DT_Id non nul)
    /// parmi les mouvements portant ces numéros de rapprochement. Base GRF (GrfConnection).
    /// </summary>
    public async Task<int> CountAffectationsTamponneesAsync(IEnumerable<string> numerosRapprochement)
    {
        var numeros = numerosRapprochement.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList();
        if (numeros.Count == 0) return 0;

        using var connection = _connectionFactory.CreateGrfConnection();
        return await connection.ExecuteScalarAsync<int>(
            @"SELECT COUNT(*) FROM dbo.RT_AFFECTATION AF
              JOIN dbo.RT_MOUVEMENT M ON M.MV_Id = AF.MV_Id
              WHERE AF.DT_Id IS NOT NULL AND M.MV_Numero IN @Numeros",
            new { Numeros = numeros });
    }

    public async Task SaveLignesCandidatesAsync(IEnumerable<LigneCandidate> lignes)
    {
        if (!lignes.Any()) return;
        using var connection = _connectionFactory.CreatePersistenceConnection();
        var sql = @"INSERT INTO DM_LGTVA
                    (Id, DeclarationId, Etat, Domaine, MotifRejet, NumeroFacture, Reference, NumeroRapprochement, TiersNom,
                     TiersIdentifiantFiscal, TiersICE, HT, Taux, CodeTaxe, TVA, TTC, Prorata, MontantAffecte, ModePaiement, DatePaiement, DateFacture, Source, EcType, EC_Id, MV_Id,
                     CodeActivite, CodeActiviteModifieManuellement, CodeActiviteModifiePar, CodeActiviteModifieLe)
                    VALUES (@Id, @DeclarationId, @Etat, @Domaine, @MotifRejet, @NumeroFacture, @Reference, @NumeroRapprochement, @TiersNom,
                     @TiersIdentifiantFiscal, @TiersICE, @HT, @Taux, @CodeTaxe, @TVA, @TTC, @Prorata, @MontantAffecte, @ModePaiement, @DatePaiement, @DateFacture, @Source, @EcType, @EC_Id, @MV_Id,
                     @CodeActivite, @CodeActiviteModifieManuellement, @CodeActiviteModifiePar, @CodeActiviteModifieLe)";
        foreach (var l in lignes)
        {
            await connection.ExecuteAsync(sql, new {
                Id = l.Id.ToString(),
                DeclarationId = l.DeclarationId.ToString(),
                Etat = (int)l.Etat,
                l.Domaine,
                l.MotifRejet,
                l.NumeroFacture,
                l.Reference,
                l.NumeroRapprochement,
                l.TiersNom,
                l.TiersIdentifiantFiscal,
                l.TiersICE,
                l.HT,
                l.Taux,
                l.CodeTaxe,
                l.TVA,
                l.TTC,
                l.Prorata,
                l.MontantAffecte,
                l.ModePaiement,
                DatePaiement = l.DatePaiement,
                DateFacture = l.DateFacture,
                l.Source,
                l.EcType,
                l.EC_Id,
                l.MV_Id,
                l.CodeActivite,
                l.CodeActiviteModifieManuellement,
                l.CodeActiviteModifiePar,
                l.CodeActiviteModifieLe
            });
        }
    }

    // ─── Revalidation des lignes figées (TASK-077, lecture seule stricte) ──────

    /// <summary>
    /// TASK-077 : lit (sans dupliquer la règle de détection TASK-072/076) les EC_Id actuellement
    /// marqués en erreur (sentinelle CodeTaxe='ERREUR') dans DM_VENTILATION_SAGE_CACHE, sur la
    /// base de persistance. Même table/critère que <see cref="EnrichirFamilleBDepuisCacheAsync"/>.
    /// TASK-118 : borné à <paramref name="soId"/> — EC_Id seul peut collisionner entre deux bases
    /// Sage physiquement distinctes.
    /// </summary>
    public async Task<HashSet<int>> GetEcIdsEnErreurAsync(int soId, IEnumerable<int> ecIds)
    {
        var ids = ecIds.Where(id => id > 0).Distinct().ToList();
        var result = new HashSet<int>();
        if (ids.Count == 0) return result;

        using var connection = _connectionFactory.CreatePersistenceConnection();
        for (int i = 0; i < ids.Count; i += 1000)
        {
            var chunk = ids.Skip(i).Take(1000).ToList();
            var rows = await connection.QueryAsync<int>(
                "SELECT DISTINCT EC_Id FROM DM_VENTILATION_SAGE_CACHE WHERE SO_Id = @soId AND EC_Id IN @chunk AND CodeTaxe = 'ERREUR'",
                new { soId, chunk });
            foreach (var r in rows) result.Add(r);
        }
        return result;
    }

    private class MvPointRow
    {
        public int MV_Id { get; set; }
        public int? MV_Point { get; set; }
    }

    /// <summary>
    /// TASK-077 : état MV_Point courant (RT_MOUVEMENT, GrfConnection) pour les MV_Id fournis.
    /// Lecture seule stricte — aucune écriture RT_*. Un MV_Id absent du dictionnaire retourné
    /// signifie que le mouvement est introuvable (mêmes garanties que
    /// <see cref="Declaration.Orchestration.VentilationSageCacheRepository.GetCurrentPaiementToken"/>,
    /// mais bornée à un MV_Id précis plutôt qu'à un EC_Id — c'est le MV_Id snapshoté au
    /// figeage de la ligne DM_LGTVA qui doit rester pointé, pas n'importe quel règlement de
    /// la facture).
    /// </summary>
    public async Task<Dictionary<int, int?>> GetMvPointsActuelsAsync(IEnumerable<int> mvIds)
    {
        var ids = mvIds.Where(id => id > 0).Distinct().ToList();
        var result = new Dictionary<int, int?>();
        if (ids.Count == 0) return result;

        using var connection = _connectionFactory.CreateGrfConnection();
        var rows = await connection.QueryAsync<MvPointRow>(
            "SELECT MV_Id, MV_Point FROM RT_MOUVEMENT WHERE MV_Id IN @mvIds", new { mvIds = ids });
        foreach (var r in rows) result[r.MV_Id] = r.MV_Point;
        return result;
    }

    /// <summary>
    /// TASK-077 (suite) : auto-guérison des lignes figées AVANT la migration 006 (EC_Id/MV_Id=0,
    /// inconnu à l'origine). Ne touche à AUCUNE colonne financière — uniquement les clés
    /// d'identification, condition nécessaire à la revalidation. Jamais appelée sur une
    /// déclaration clôturée (garde posée par l'appelant, DeclarationWorkflowService).
    /// </summary>
    public async Task UpdateLigneClesAsync(Guid ligneId, int ecId, int mvId)
    {
        using var connection = _connectionFactory.CreatePersistenceConnection();
        await connection.ExecuteAsync(
            "UPDATE DM_LGTVA SET EC_Id = @EcId, MV_Id = @MvId WHERE Id = @LigneId",
            new { EcId = ecId, MvId = mvId, LigneId = ligneId.ToString() });
    }

    /// <summary>
    /// TASK-078 : trace la décision PO de valider une incohérence signalée (TASK-077) pour
    /// TOUTES les lignes de cette pièce (un EC_Id peut porter plusieurs buckets de taux) — ne
    /// modifie ni montant ni état, uniquement la traçabilité de la décision.
    /// </summary>
    public async Task ValiderIncoherenceAsync(Guid declarationId, int ecId, string utilisateur)
    {
        using var connection = _connectionFactory.CreatePersistenceConnection();
        await connection.ExecuteAsync(
            @"UPDATE DM_LGTVA
              SET IncoherenceValidee = 1, IncoherenceValideePar = @Utilisateur, IncoherenceValideeLe = @Maintenant
              WHERE DeclarationId = @DeclarationId AND EC_Id = @EcId",
            new { DeclarationId = declarationId.ToString(), EcId = ecId, Utilisateur = utilisateur, Maintenant = DateTime.UtcNow });
    }

    /// <summary>
    /// TASK-078 : réinitialise la validation d'incohérence (les faits ont changé après une
    /// resynchronisation, l'ancienne décision ne s'applique plus).
    /// </summary>
    public async Task ReinitialiserValidationIncoherenceAsync(Guid declarationId, int ecId)
    {
        using var connection = _connectionFactory.CreatePersistenceConnection();
        await connection.ExecuteAsync(
            @"UPDATE DM_LGTVA
              SET IncoherenceValidee = 0, IncoherenceValideePar = NULL, IncoherenceValideeLe = NULL
              WHERE DeclarationId = @DeclarationId AND EC_Id = @EcId",
            new { DeclarationId = declarationId.ToString(), EcId = ecId });
    }

    // ─── Exclusivité inter-déclaration (TASK-080) ──────────────────────────────

    private sealed class ConflitRow
    {
        public string NumeroFacture { get; set; } = "";
        public string NumeroRapprochement { get; set; } = "";
        public string NumeroDeclaration { get; set; } = "";
    }

    /// <summary>
    /// TASK-080 : clés (NumeroFacture|NumeroRapprochement) déjà Proposee/Integree dans une autre
    /// déclaration de la même société. Base de persistance uniquement (DM_LGTVA JOIN DM_ENTTVA) —
    /// aucune donnée GRF (RT_*) nécessaire, contrairement au tampon DT_Id (TASK-028).
    /// </summary>
    public async Task<Dictionary<string, string>> GetConflitsAutreDeclarationAsync(int societeId, Guid declarationIdExclue)
    {
        using var connection = _connectionFactory.CreatePersistenceConnection();
        var rows = await connection.QueryAsync<ConflitRow>(
            @"SELECT L.NumeroFacture, L.NumeroRapprochement, MIN(E.Numero) AS NumeroDeclaration
              FROM DM_LGTVA L
              JOIN DM_ENTTVA E ON E.Id = L.DeclarationId
              WHERE E.SocieteId = @SocieteId
                AND L.DeclarationId <> @DeclarationIdExclue
                AND L.Etat IN @EtatsActifs
              GROUP BY L.NumeroFacture, L.NumeroRapprochement",
            new
            {
                SocieteId = societeId,
                DeclarationIdExclue = declarationIdExclue.ToString(),
                EtatsActifs = new[] { (int)EtatLigne.Proposee, (int)EtatLigne.Integree }
            });

        var result = new Dictionary<string, string>();
        foreach (var r in rows)
            result[$"{r.NumeroFacture}|{r.NumeroRapprochement}"] = r.NumeroDeclaration;
        return result;
    }

    /// <summary>TASK-080 : suppression ciblée de lignes DM_LGTVA par Id (réintégration après conflit résolu).</summary>
    public async Task DeleteLignesAsync(IEnumerable<Guid> ligneIds)
    {
        var ids = ligneIds.Select(g => g.ToString()).ToList();
        if (ids.Count == 0) return;
        using var connection = _connectionFactory.CreatePersistenceConnection();
        var sql = $"DELETE FROM DM_LGTVA WHERE Id IN ({string.Join(",", ids.Select(id => $"'{id}'"))})";
        await connection.ExecuteAsync(sql);
    }

    public async Task<IEnumerable<LigneCandidate>> GetLignesAsync(Guid declarationId, string domaine, int page, int pageSize, string? sort, string? filter)
    {
        using var connection = _connectionFactory.CreatePersistenceConnection();
        // Whitelist pour ORDER BY (injection SQL proof)
        var allowedSorts = new[] { "DateFacture DESC", "DateFacture ASC", "NumeroFacture ASC" };
        if (!allowedSorts.Contains(sort)) sort = "DateFacture DESC";

        var (whereExtra, p) = BuildLigneFilterWhere(filter);

        var sql = $@"SELECT * FROM DM_LGTVA
                    WHERE DeclarationId = @DeclarationId AND Domaine = @Domaine {whereExtra}
                    ORDER BY {sort}
                    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";

        p.Add("DeclarationId", declarationId.ToString());
        p.Add("Domaine", domaine);
        p.Add("Offset", (page - 1) * pageSize);
        p.Add("PageSize", pageSize);

        return await connection.QueryAsync<LigneCandidate>(sql, p);
    }

    public async Task<int> GetLignesCountAsync(Guid declarationId, string domaine, string? filter)
    {
        using var connection = _connectionFactory.CreatePersistenceConnection();
        var (whereExtra, p) = BuildLigneFilterWhere(filter);

        var sql = $@"SELECT COUNT(*) FROM DM_LGTVA
                    WHERE DeclarationId = @DeclarationId AND Domaine = @Domaine {whereExtra}";

        p.Add("DeclarationId", declarationId.ToString());
        p.Add("Domaine", domaine);

        return await connection.ExecuteScalarAsync<int>(sql, p);
    }

    /// <summary>
    /// Construit le fragment WHERE additionnel + les paramètres Dapper correspondant au filtre JSON
    /// (ou texte libre legacy) envoyé par DomainGrid/les drills. Utilisé À L'IDENTIQUE par
    /// <see cref="GetLignesAsync"/> et <see cref="GetLignesCountAsync"/> (invariant TASK-040 :
    /// TotalCount reste cohérent avec la grille sous n'importe quelle combinaison de filtres).
    ///
    /// TASK-067B — étend l'ancien filtre (numeroRapprochement scalaire, etat) à la MULTI-SÉLECTION
    /// réelle (numeroRapprochement, source, tauxTVA, origine en tableau ⇒ IN), pour que les
    /// colonnes désormais en filterType 'list' filtrent réellement (avant, cocher une case dans
    /// ExcelFilter n'avait aucun effet serveur — la clé JSON était silencieusement ignorée).
    /// </summary>
    private static (string WhereSql, DynamicParameters Params) BuildLigneFilterWhere(string? filter)
    {
        var sql = new System.Text.StringBuilder();
        var p = new DynamicParameters();
        if (string.IsNullOrEmpty(filter)) return (sql.ToString(), p);

        if (filter.StartsWith("{"))
        {
            try
            {
                var json = System.Text.Json.JsonDocument.Parse(filter);

                if (json.RootElement.TryGetProperty("numeroRapprochement", out var nr))
                {
                    var numeros = ExtraireListeChaines(nr);
                    if (numeros.Count > 0)
                    {
                        sql.Append(" AND NumeroRapprochement IN @Numeros ");
                        p.Add("Numeros", numeros);
                    }
                }

                if (json.RootElement.TryGetProperty("ecId", out var ecId))
                {
                    // TASK-146 : filtre précis par ligne (EC_Id), prioritaire sur numeroRapprochement
                    // côté appelant (DeclarationsController.GetCheckup) — un règlement peut couvrir
                    // plusieurs factures, EC_Id isole la ligne réellement en anomalie.
                    var ecIds = ExtraireListeChaines(ecId)
                        .Select(v => int.TryParse(v, out var i) ? (int?)i : null)
                        .Where(v => v.HasValue)
                        .Select(v => v!.Value)
                        .ToList();
                    if (ecIds.Count > 0)
                    {
                        sql.Append(" AND EC_Id IN @EcIds ");
                        p.Add("EcIds", ecIds);
                    }
                }

                if (json.RootElement.TryGetProperty("source", out var src))
                {
                    var sources = ExtraireListeChaines(src);
                    if (sources.Count > 0)
                    {
                        sql.Append(" AND Source IN @Sources ");
                        p.Add("Sources", sources);
                    }
                }

                if (json.RootElement.TryGetProperty("tauxTVA", out var taux))
                {
                    var tauxValues = ExtraireListeChaines(taux)
                        .Select(t => decimal.TryParse(t, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d) ? (decimal?)d : null)
                        .Where(d => d.HasValue)
                        .Select(d => d!.Value)
                        .ToList();
                    if (tauxValues.Count > 0)
                    {
                        sql.Append(" AND Taux IN @TauxValues ");
                        p.Add("TauxValues", tauxValues);
                    }
                }

                if (json.RootElement.TryGetProperty("origine", out var origine))
                {
                    // Réplique EXACTEMENT ReglementRapprochementRow.LibelleEcType (source unique) via
                    // son inverse (EcTypesDepuisLibelle) : aucune duplication de la règle métier.
                    var ecTypes = ExtraireListeChaines(origine)
                        .SelectMany(ReglementRapprochementRow.EcTypesDepuisLibelle)
                        .Distinct()
                        .ToList();
                    if (ecTypes.Count > 0)
                    {
                        sql.Append(" AND EcType IN @EcTypes ");
                        p.Add("EcTypes", ecTypes);
                    }
                }

                if (json.RootElement.TryGetProperty("incoherente", out var incoherente))
                {
                    // TASK-112 : axe « lignes incohérentes » (TTC ≠ HT+TVA, tolérance d'arrondi) —
                    // remplace le filtre `source` tautologique pour isoler l'écart d'équilibre.
                    var valeurs = ExtraireListeChaines(incoherente);
                    if (valeurs.Any(v => string.Equals(v, "true", StringComparison.OrdinalIgnoreCase)))
                    {
                        sql.Append(" AND ABS(TTC - (HT + TVA)) > 0.01 ");
                    }
                    else if (valeurs.Any(v => string.Equals(v, "false", StringComparison.OrdinalIgnoreCase)))
                    {
                        sql.Append(" AND ABS(TTC - (HT + TVA)) <= 0.01 ");
                    }
                }

                if (json.RootElement.TryGetProperty("etat", out var etat))
                {
                    if (etat.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        var etats = new List<int>();
                        foreach (var item in etat.EnumerateArray())
                        {
                            if (item.ValueKind == System.Text.Json.JsonValueKind.Number) etats.Add(item.GetInt32());
                            else if (item.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                var str = item.GetString();
                                if (str == "Proposée") etats.Add(0);
                                else if (str == "Intégrée") etats.Add(1);
                                else if (str == "Exclue") etats.Add(2);
                                else if (str == "Reportée") etats.Add(3);
                                else if (str == "Écartée") etats.Add(4);
                            }
                        }
                        if (etats.Count > 0)
                        {
                            sql.Append(" AND Etat IN @Etats ");
                            p.Add("Etats", etats);
                        }
                    }
                }
            }
            catch { }
        }
        else
        {
            sql.Append(" AND (NumeroFacture LIKE '%' + @FreeText + '%' OR TiersNom LIKE '%' + @FreeText + '%') ");
            p.Add("FreeText", filter);
        }

        return (sql.ToString(), p);
    }

    // Le front peut envoyer une valeur de filtre liste en chaîne unique (legacy : drills) OU en
    // tableau (ExcelFilter multi-sélection, TASK-067B). Normalise vers une liste de chaînes ;
    // les nombres JSON sont acceptés (ex. tauxTVA) et convertis en chaîne pour parsing ultérieur.
    private static List<string> ExtraireListeChaines(System.Text.Json.JsonElement el)
    {
        var result = new List<string>();
        if (el.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var item in el.EnumerateArray())
            {
                if (item.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    var s = item.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) result.Add(s!);
                }
                else if (item.ValueKind == System.Text.Json.JsonValueKind.Number)
                {
                    result.Add(item.ToString());
                }
            }
        }
        else if (el.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            var s = el.GetString();
            if (!string.IsNullOrWhiteSpace(s)) result.Add(s!);
        }
        return result;
    }

    /// <summary>
    /// Valeurs distinctes des colonnes énumérables de DomainGrid (numeroRapprochement, source,
    /// tauxTVA, origine, statutLigne), pour alimenter <c>filterType: 'list'</c> (TASK-067B).
    /// MÊME WHERE que la liste principale — DeclarationId + Domaine (invariant TASK-040) : les
    /// lignes candidates d'une déclaration sont un ensemble déjà BORNÉ (figé au premier appel,
    /// cf. ChargerCandidatesSiNecessaireAsync), donc aucun TOP nécessaire ici (à la différence des
    /// interrogations Rapprochement/Factures qui portent sur un flux global potentiellement large).
    /// Lecture seule stricte.
    /// </summary>
    public async Task<Dictionary<string, List<string>>> GetLignesDistinctsAsync(Guid declarationId, string domaine)
    {
        using var connection = _connectionFactory.CreatePersistenceConnection();
        var rows = (await connection.QueryAsync<LigneDistinctRow>(
            @"SELECT NumeroRapprochement, Source, Taux, EcType, Etat FROM DM_LGTVA
              WHERE DeclarationId = @DeclarationId AND Domaine = @Domaine",
            new { DeclarationId = declarationId.ToString(), Domaine = domaine })).ToList();

        string[] etatLabels = { "Proposée", "Intégrée", "Exclue", "Reportée", "Écartée" };

        return new Dictionary<string, List<string>>
        {
            ["numeroRapprochement"] = rows.Select(r => r.NumeroRapprochement)
                .Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!).Distinct().OrderBy(v => v).ToList(),
            ["source"] = rows.Select(r => r.Source)
                .Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!).Distinct().OrderBy(v => v).ToList(),
            ["tauxTVA"] = rows.Select(r => r.Taux).Distinct().OrderBy(v => v)
                .Select(v => v.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList(),
            ["origine"] = rows.Select(r => ReglementRapprochementRow.LibelleEcType(r.EcType))
                .Distinct().OrderBy(v => v).ToList(),
            ["statutLigne"] = rows.Select(r => r.Etat).Where(e => e >= 0 && e < etatLabels.Length)
                .Distinct().OrderBy(e => e).Select(e => etatLabels[e]).ToList(),
        };
    }

    // Projection minimale pour GetLignesDistinctsAsync (Dapper mappe par nom de colonne).
    private sealed class LigneDistinctRow
    {
        public string? NumeroRapprochement { get; set; }
        public string? Source { get; set; }
        public decimal Taux { get; set; }
        public int EcType { get; set; }
        public int Etat { get; set; }
    }

    public async Task UpdateLigneEtatAsync(Guid ligneId, EtatLigne nouvelEtat)
    {
        using var connection = _connectionFactory.CreatePersistenceConnection();
        var sql = "UPDATE DM_LGTVA SET Etat = @Etat WHERE Id = @Id";
        await connection.ExecuteAsync(sql, new { Id = ligneId.ToString(), Etat = (int)nouvelEtat });
    }

    public async Task UpdateLignesEtatBulkAsync(Guid declarationId, string domaine, string? filter, EtatLigne nouvelEtat)
    {
        using var connection = _connectionFactory.CreatePersistenceConnection();
        var sql = @"UPDATE DM_LGTVA SET Etat = @Etat 
                    WHERE DeclarationId = @DeclarationId AND Domaine = @Domaine ";
        
        if (!string.IsNullOrEmpty(filter))
            sql += " AND (NumeroFacture LIKE '%' + @Filter + '%' OR TiersNom LIKE '%' + @Filter + '%') ";
            
        await connection.ExecuteAsync(sql, new { Etat = (int)nouvelEtat, DeclarationId = declarationId.ToString(), Domaine = domaine, Filter = filter });
    }

    public async Task UpdateLignesEtatBulkByIdsAsync(IEnumerable<Guid> ligneIds, EtatLigne nouvelEtat)
    {
        if (!ligneIds.Any()) return;
        using var connection = _connectionFactory.CreatePersistenceConnection();
        var ids = ligneIds.Select(g => g.ToString()).ToList();
        var sql = $"UPDATE DM_LGTVA SET Etat = @Etat WHERE Id IN ({string.Join(",", ids.Select(id => $"'{id}'"))})";
        await connection.ExecuteAsync(sql, new { Etat = (int)nouvelEtat });
    }

    // ─── Interrogation « Rapprochement bancaire » globale (TASK-036) ────────────────
    //
    // LECTURE SEULE STRICTE : uniquement des SELECT sur la base GRF (RT_MOUVEMENT,
    // RT_AFFECTATION, RT_ECHEANCE). Aucun INSERT/UPDATE/DELETE, aucun appel DLL.
    // Pivot = le règlement (MV_Id) ; les affectations sont agrégées dans une sous-requête.
    // Le tampon DT_Id (TASK-028) est LU (compté), jamais modifié ici.
    // Période bornée obligatoire (perf) ; paramètres Dapper (anti-injection), ORDER BY whitelisté.

    /// <summary>
    /// Fragment FROM + WHERE commun à la liste, au comptage et (partiellement) aux distincts.
    /// La sous-requête agrège RT_AFFECTATION × RT_ECHEANCE par MV_Id ; le LEFT JOIN conserve
    /// les règlements sans affectation (reste à affecter = montant total, aucune ligne masquée).
    /// </summary>
    private const string RapprochementFromWhere = @"
        FROM RT_MOUVEMENT M
        -- Code banque : non porté par RT_MOUVEMENT, récupéré via la vue legacy dbo.vBanque
        -- (source vReglementsFournisseurs.tlp : vBanque.[No] = BN_Id ET vBanque.SocieteNo = SO_Id).
        -- LEFT JOIN obligatoire : un règlement sans banque (espèce/caisse) reste présent, BanqueCode nul.
        -- vBanque.[No] unique par société → aucune duplication de ligne (TotalCount inchangé).
        LEFT JOIN dbo.vBanque B ON B.[No] = M.BN_Id AND B.SocieteNo = M.SO_Id
        LEFT JOIN (
            SELECT AF.MV_Id,
                   COUNT(*)                                          AS NbAffectations,
                   SUM(AF.AF_Montant)                                AS MontantAffecte,
                   MIN(E.EC_Type)                                    AS EcTypeMin,
                   MAX(E.EC_Type)                                    AS EcTypeMax,
                   SUM(CASE WHEN AF.DT_Id IS NOT NULL THEN 1 ELSE 0 END) AS NbDeclare,
                   -- TASK-140 : DT_Id de la déclaration verrou (résolu en numéro côté applicatif).
                   -- MIN + MAX pour détecter une éventuelle incohérence sans la masquer.
                   MAX(AF.DT_Id)                                     AS DtId,
                   MIN(AF.DT_Id)                                     AS DtIdMin
            FROM RT_AFFECTATION AF
            JOIN RT_ECHEANCE E ON AF.EC_Id = E.EC_Id
            GROUP BY AF.MV_Id
        ) A ON A.MV_Id = M.MV_Id
        WHERE M.SO_Id = @so
          AND M.MV_Domaine IN (0, 1) -- 0=encaissement, 1=décaissement : vrais règlements uniquement (exclut bordereaux de remise, virements, alim. caisse ; les frais bancaires MV_Domaine=6 sont dans une autre table)
          -- TASK-196 : CT_Type = 0 (GrfEnums.CtType_Client) si MV_Domaine = 0, CT_Type = 1 (GrfEnums.CtType_Fournisseur) si MV_Domaine = 1
          AND ((M.MV_Domaine = 0 AND M.CT_Type = 0) OR (M.MV_Domaine = 1 AND M.CT_Type = 1))
          -- Période située par la DATE DE RÉFÉRENCE (TASK-062, source unique RegleDatePeriode) :
          -- rapproché → MV_PointDate ; espèce & non-rapproché → MV_Date. Corrige RF26060064 (rapproché
          -- en janvier via MV_PointDate mais MV_Date en juin).
          -- TASK-099 : la règle diffère selon l'appelant (même endpoint, deux usages) —
          --   • écran ① Sélection (@declarationId fourni) : coupure de période SEULE (rattrapage de
          --     l'arriéré jamais déclaré, plus de borne basse) + non-rapprochés hors-espèce MASQUÉS
          --     (gate EstDeclarable) — décision PO 14/07/2026 : uniquement cet écran.
          --   • Interrogation Rapprochement (TASK-037, @declarationId absent) : comportement INCHANGÉ
          --     (fenêtre pleine borne basse/haute, aucun gate — le non-rapproché reste visible).
          AND (
              (@declarationId IS NOT NULL AND " + RegleDatePeriode.DateReferenceSqlM + @" < @finExclude)
              OR
              (@declarationId IS NULL AND " + RegleDatePeriode.DateReferenceSqlM + @" >= @debut AND " + RegleDatePeriode.DateReferenceSqlM + @" < @finExclude)
          )
          AND (@declarationId IS NULL OR " + RegleDatePeriode.EstDeclarableSqlM + @")
          -- Mode : multi-sélection RÉELLE (TASK-063) — liste vide ⇒ pas de contrainte (guard @hasModes).
          AND (@hasModes = 0 OR M.MV_Type IN @modes)
          -- Rapproché = pointage bancaire (MV_Point=1) OU espèce (MV_Type=0, auto-rapprochée) :
          -- réplique EXACTEMENT ReglementRapprochementRow.EstRapprocheBanque (source unique, TASK-042)
          -- pour que le filtre n'exclue jamais une espèce affichée « rapprochée » (pas de filtre menteur).
          -- Multi-sélection réelle (TASK-063) : cocher Oui+Non ⇒ IN (0,1) ⇒ tout (plus de dc[0] menteur).
          AND (@hasRapproche = 0 OR (CASE WHEN M.MV_Point = 1 OR M.MV_Type = 0 THEN 1 ELSE 0 END) IN @rapproches)
          AND (@hasDeclare = 0 OR (CASE WHEN ISNULL(A.NbDeclare, 0) > 0 THEN 1 ELSE 0 END) IN @declares)
          -- Point (pointage brut sur extrait, MV_Point) : distinct de « rapproché » (espèce auto).
          AND (@hasPoint = 0 OR M.MV_Point IN @points)
          AND (@tiers IS NULL OR M.CT_Intitule LIKE '%' + @tiers + '%' OR M.CT_Code LIKE '%' + @tiers + '%')
          -- Filtres TASK-040 déplacés côté serveur (appliqués IDENTIQUEMENT à la liste et au COUNT
          -- pour que TotalCount, pagination et grille coïncident, quelle que soit la page).
          -- TASK-067B : n° règlement en MULTI-SÉLECTION RÉELLE (valeurs distinctes exactes,
          -- cf. GetReglementsRapprochementDistinctsAsync), fin du LIKE scalaire.
          AND (@hasNumeros = 0 OR M.MV_Numero IN @numeros)
          -- origine/domaine : multi-sélection, comparés à la MÊME expression de dérivation que la
          -- projection (ReglementRapprochementRow.LibelleOrigine / LibelleDomaine), sinon le filtre mentirait.
          AND (@hasOrigines = 0 OR " + RapprochementOrigineExpr + @" IN @origines)
          AND (@hasDomaines = 0 OR " + RapprochementDomaineExpr + @" IN @domaines)
          -- ── Filtres par type de donnée (TASK-063/067B), tous NULL-safe (borne vide ⇒ pas de contrainte) ──
          -- N° extrait / code banque (via vBanque) : TASK-067B — multi-sélection réelle (valeurs distinctes),
          -- fin du LIKE scalaire (LEFT JOIN gardé, banque nulle pour espèce/caisse toujours présente).
          AND (@hasNumerosExtrait = 0 OR M.MV_ExtraitNum IN @numerosExtrait)
          AND (@hasBanques = 0 OR B.BanqueCode IN @banques)
          -- Montant → plage sur M.MV_Montant (colonne physique projetée).
          AND (@montantMin IS NULL OR M.MV_Montant >= @montantMin)
          AND (@montantMax IS NULL OR M.MV_Montant <= @montantMax)
          -- Reste à affecter → plage sur la MÊME expression que la projection/ORDER BY (colonne dérivée).
          AND (@resteMin IS NULL OR " + RapprochementResteExpr + @" >= @resteMin)
          AND (@resteMax IS NULL OR " + RapprochementResteExpr + @" <= @resteMax)
          -- Nb factures affectées → plage sur ISNULL(A.NbAffectations,0) (même expression projetée).
          AND (@nbFacturesMin IS NULL OR " + RapprochementNbFacturesExpr + @" >= @nbFacturesMin)
          AND (@nbFacturesMax IS NULL OR " + RapprochementNbFacturesExpr + @" <= @nbFacturesMax)
          -- Date de rapprochement → plage sur la MÊME expression que la projection DateRapprochement
          -- (espèce → MV_Date ; sinon MV_PointDate neutralisé du sentinel 1753). Borne haute exclusive.
          AND (@dateRappMin IS NULL OR " + RapprochementDateRappExpr + @" >= @dateRappMin)
          AND (@dateRappMaxExclude IS NULL OR " + RapprochementDateRappExpr + @" < @dateRappMaxExclude)
          -- Échéance pièce → plage sur MV_Echeance (sentinel 1753 neutralisé). Borne haute exclusive.
          AND (@echeanceMin IS NULL OR " + RapprochementEcheanceExpr + @" >= @echeanceMin)
          AND (@echeanceMaxExclude IS NULL OR " + RapprochementEcheanceExpr + @" < @echeanceMaxExclude)";

    // Expression SQL répliquant EXACTEMENT ReglementRapprochementRow.LibelleOrigine (source unique
    // côté domaine). A.EcTypeMin IS NULL ⇒ aucune affectation (LEFT JOIN) ⇒ « SansAffectation ».
    private const string RapprochementOrigineExpr = @"(CASE
              WHEN A.EcTypeMin IS NULL THEN N'SansAffectation'
              WHEN A.EcTypeMin <> A.EcTypeMax THEN N'Mixte'
              WHEN A.EcTypeMin = 0 THEN N'Sage'
              WHEN A.EcTypeMin = 111 THEN N'FGR'
              WHEN A.EcTypeMin = 4 THEN N'SoldeInitial'
              ELSE N'Autre (' + CAST(A.EcTypeMin AS nvarchar(20)) + N')'
          END)";

    // Expression SQL répliquant EXACTEMENT ReglementRapprochementRow.LibelleDomaine.
    private const string RapprochementDomaineExpr = @"(CASE M.MV_Domaine
              WHEN 0 THEN N'Encaissement'
              WHEN 1 THEN N'Décaissement'
              WHEN 6 THEN N'Frais bancaire'
              ELSE N'Autre (' + CAST(M.MV_Domaine AS nvarchar(20)) + N')'
          END)";

    // Expressions dérivées comparées par les filtres de plage (TASK-063). RÈGLE : chaque plage
    // se compare à la MÊME expression que la projection (SELECT) et le ORDER BY, jamais à une
    // colonne physique absente — sinon le filtre mentirait sur une valeur affichée dérivée.
    private const string RapprochementResteExpr = "(M.MV_Montant - ISNULL(A.MontantAffecte, 0))";
    private const string RapprochementNbFacturesExpr = "ISNULL(A.NbAffectations, 0)";
    // DateRapprochement (source unique ReglementRapprochementRow.DateRapprochement) : espèce → MV_Date,
    // sinon MV_PointDate débarrassé du sentinel SQL min 1753 (non pointé ⇒ NULL ⇒ hors plage).
    private const string RapprochementDateRappExpr =
        "(CASE WHEN M.MV_Type = 0 THEN M.MV_Date ELSE NULLIF(M.MV_PointDate, '17530101') END)";
    private const string RapprochementEcheanceExpr = "NULLIF(M.MV_Echeance, '17530101')";

    // Les colonnes MV_* sont des `datetime` SQL Server (plage 1753-01-01 → 9999-12-31). Une borne
    // saisie hors de cette plage (ex. année partielle « 0026 » tapée dans <input type="date">)
    // provoquerait un « SqlDateTime overflow » à l'exécution. On la neutralise (⇒ pas de contrainte)
    // plutôt que de laisser planter la requête. Lecture seule, aucune écriture.
    private static readonly DateTime SqlDateMin = new DateTime(1753, 1, 1);
    private static readonly DateTime SqlDateMax = new DateTime(9999, 12, 31);

    private static DateTime? SqlSafeDate(DateTime? value)
    {
        if (value is null) return null;
        var d = value.Value.Date;
        return d < SqlDateMin || d > SqlDateMax ? (DateTime?)null : d;
    }

    private static DateTime? SqlSafeUpperExclusive(DateTime? value)
    {
        if (value is null) return null;
        var d = value.Value.Date;
        if (d < SqlDateMin || d >= SqlDateMax) return null; // hors plage ou débordement du +1 jour
        return d.AddDays(1);
    }

    private static object RapprochementParams(int soId, DateTime dateDebut, DateTime dateFin,
        RapprochementFilter filter)
    {
        var f = filter ?? new RapprochementFilter();
        var modes = (f.Modes ?? Array.Empty<int>()).Distinct().ToList();
        var rapproches = RapprochementFilter.ToFlags(f.RapprocheBanque);
        var declares = RapprochementFilter.ToFlags(f.Declare);
        var points = RapprochementFilter.ToFlags(f.Point);
        var orig = (f.Origines ?? Array.Empty<string>()).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
        var dom = (f.Domaines ?? Array.Empty<string>()).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
        // TASK-067B — multi-sélection réelle (valeurs distinctes exactes) pour les colonnes
        // identifiantes converties de LIKE scalaire vers filterType 'list'.
        var numeros = (f.Numeros ?? Array.Empty<string>()).Where(v => !string.IsNullOrWhiteSpace(v)).Distinct().ToList();
        var numerosExtrait = (f.NumerosExtrait ?? Array.Empty<string>()).Where(v => !string.IsNullOrWhiteSpace(v)).Distinct().ToList();
        var banques = (f.Banques ?? Array.Empty<string>()).Where(v => !string.IsNullOrWhiteSpace(v)).Distinct().ToList();
        return new
        {
            so = soId,
            debut = dateDebut.Date,
            declarationId = f.DeclarationId?.ToString(),
            finExclude = dateFin.Date.AddDays(1), // borne haute inclusive sur la date
            // Énumération + booléens en multi-sélection réelle : guard @hasX = 0 si liste vide.
            modes,
            hasModes = modes.Count > 0 ? 1 : 0,
            rapproches,
            hasRapproche = rapproches.Count > 0 ? 1 : 0,
            declares,
            hasDeclare = declares.Count > 0 ? 1 : 0,
            points,
            hasPoint = points.Count > 0 ? 1 : 0,
            tiers = string.IsNullOrWhiteSpace(f.Tiers) ? null : f.Tiers,
            numeros,
            hasNumeros = numeros.Count > 0 ? 1 : 0,
            numerosExtrait,
            hasNumerosExtrait = numerosExtrait.Count > 0 ? 1 : 0,
            banques,
            hasBanques = banques.Count > 0 ? 1 : 0,
            origines = orig,
            hasOrigines = orig.Count > 0 ? 1 : 0,
            domaines = dom,
            hasDomaines = dom.Count > 0 ? 1 : 0,
            // Plages montants / compteur : NULL = pas de contrainte.
            montantMin = f.MontantMin,
            montantMax = f.MontantMax,
            resteMin = f.ResteMin,
            resteMax = f.ResteMax,
            nbFacturesMin = f.NbFacturesMin,
            nbFacturesMax = f.NbFacturesMax,
            // Plages dates : borne haute rendue EXCLUSIVE (+1 jour) pour inclure toute la journée max.
            // Neutralisées si hors plage SQL `datetime` (cf. SqlSafeDate) pour éviter un overflow runtime.
            dateRappMin = SqlSafeDate(f.DateRappMin),
            dateRappMaxExclude = SqlSafeUpperExclusive(f.DateRappMax),
            echeanceMin = SqlSafeDate(f.EcheanceMin),
            echeanceMaxExclude = SqlSafeUpperExclusive(f.EcheanceMax)
        };
    }

    public async Task<IEnumerable<ReglementRapprochementRow>> GetReglementsRapprochementAsync(
        int soId, DateTime dateDebut, DateTime dateFin,
        RapprochementFilter filter, int page, int size, string? sort)
    {
        // Whitelist ORDER BY (anti-injection). Tiebreaker MV_Id → pagination stable.
        var orderBy = sort switch
        {
            "date_asc"     => "M.MV_Date ASC",
            "date_desc"    => "M.MV_Date DESC",
            "montant_asc"  => "M.MV_Montant ASC",
            "montant_desc" => "M.MV_Montant DESC",
            "reste_asc"    => "(M.MV_Montant - ISNULL(A.MontantAffecte, 0)) ASC",
            "reste_desc"   => "(M.MV_Montant - ISNULL(A.MontantAffecte, 0)) DESC",
            _              => "M.MV_Date DESC"
        };

        if (page < 1) page = 1;
        if (size < 1) size = 50;

        var sql = $@"
            SELECT
                M.MV_Numero                        AS MvNumero,
                M.MV_Type                          AS MvType,
                M.MV_Domaine                       AS MvDomaine,
                M.MV_Point                         AS MvPoint,
                M.MV_Date                          AS MvDate,
                -- MV_PointDate / MV_Echeance sont datetime NOT NULL : les lignes non pointées portent
                -- le sentinel SQL min '1753-01-01'. NULLIF le neutralise → NULL → « — » au front
                -- (transparence : jamais de date fictive 1753 affichée comme un vrai pointage — TASK-042).
                NULLIF(M.MV_PointDate, '17530101') AS MvPointDate,
                M.MV_ExtraitNum                    AS MvExtraitNum,
                NULLIF(M.MV_Echeance, '17530101')  AS MvEcheance,
                M.MV_Piece                         AS MvPiece,
                M.MV_Montant                       AS MvMontant,
                M.CT_Intitule                      AS Tiers,
                M.CT_Code                          AS TiersCode,
                B.BanqueCode                       AS BanqueCode,
                ISNULL(A.NbAffectations, 0)        AS NbAffectations,
                A.MontantAffecte                   AS MontantAffecte,
                A.EcTypeMin                        AS EcTypeMin,
                A.EcTypeMax                        AS EcTypeMax,
                ISNULL(A.NbDeclare, 0)             AS NbDeclare,
                A.DtId                             AS DtId,
                A.DtIdMin                          AS DtIdMin,
                (SELECT COUNT(*) FROM dbo.DM_SELECTION_REGLEMENT SR
                 JOIN dbo.DM_ENTTVA D ON SR.DeclarationId = D.Id
                 WHERE D.Statut = 0 
                   AND D.SocieteId = M.SO_Id 
                   AND (@declarationId IS NULL OR D.Id <> @declarationId)
                   AND SR.NumeroReglement = M.MV_Numero) AS NbSelectionAutre
            {RapprochementFromWhere}
            ORDER BY {orderBy}, M.MV_Id
            OFFSET @offset ROWS FETCH NEXT @size ROWS ONLY";

        var p = new DynamicParameters(RapprochementParams(soId, dateDebut, dateFin, filter));
        p.Add("offset", (page - 1) * size);
        p.Add("size", size);

        using var connection = _connectionFactory.CreateGrfConnection();
        var rows = (await connection.QueryAsync<ReglementRapprochementRow>(sql, p)).ToList();
        await ResoudreNumeroDeclarationAsync(rows);
        return rows;
    }

    /// <summary>
    /// TASK-140 — résolution APPLICATIVE du numéro de déclaration verrou. RT_AFFECTATION.DT_Id vit
    /// sur GRF, DM_ENTTVA sur la base de persistance (connexions Dapper distinctes) : un JOIN SQL
    /// direct dépendrait d'un nom de base en dur, fragile selon l'environnement client. On résout
    /// donc en un SEUL appel batché (les DT_Id distincts de la page) puis fusion en mémoire.
    /// Cas incohérent (DtIdMin ≠ DtId, ne devrait pas arriver — verrou atomique TASK-028) : les DEUX
    /// numéros sont remontés explicitement, jamais un choix arbitraire silencieux.
    /// </summary>
    private async Task ResoudreNumeroDeclarationAsync(IReadOnlyList<ReglementRapprochementRow> rows)
    {
        var dtIds = rows
            .SelectMany(r => new[] { r.DtId, r.DtIdMin })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        if (dtIds.Count == 0) return;

        using var pc = _connectionFactory.CreatePersistenceConnection();
        var pairs = await pc.QueryAsync<DeclarationDtNumero>(
            "SELECT DT_Id, Numero FROM DM_ENTTVA WHERE DT_Id IN @dtIds", new { dtIds });

        var map = new Dictionary<int, string?>();
        foreach (var pair in pairs)
            if (pair.DT_Id.HasValue && !map.ContainsKey(pair.DT_Id.Value))
                map[pair.DT_Id.Value] = pair.Numero;

        foreach (var r in rows)
        {
            if (!r.DtId.HasValue) continue;
            map.TryGetValue(r.DtId.Value, out var numMax);
            if (r.DtIdMin.HasValue && r.DtIdMin.Value != r.DtId.Value)
            {
                map.TryGetValue(r.DtIdMin.Value, out var numMin);
                // Incohérence signalée, jamais masquée (transparence — cf. TASK-140 §CONTRAINTE).
                r.NumeroDeclaration =
                    $"⚠ {numMin ?? r.DtIdMin.Value.ToString()} / {numMax ?? r.DtId.Value.ToString()} (incohérent)";
            }
            else
            {
                r.NumeroDeclaration = numMax; // peut rester NULL (déclaration sans Numero) — jamais inventé
            }
        }
    }

    private sealed class DeclarationDtNumero
    {
        public int? DT_Id { get; set; }
        public string? Numero { get; set; }
    }

    public async Task<int> GetReglementsRapprochementCountAsync(
        int soId, DateTime dateDebut, DateTime dateFin, RapprochementFilter filter)
    {
        var sql = $"SELECT COUNT(*) {RapprochementFromWhere}";
        using var connection = _connectionFactory.CreateGrfConnection();
        return await connection.ExecuteScalarAsync<int>(
            sql, RapprochementParams(soId, dateDebut, dateFin, filter));
    }

    public async Task<ReglementRapprochementDistincts> GetReglementsRapprochementDistinctsAsync(
        int soId, DateTime dateDebut, DateTime dateFin)
    {
        using var connection = _connectionFactory.CreateGrfConnection();
        var p = new { so = soId, debut = dateDebut.Date, finExclude = dateFin.Date.AddDays(1) };

        var modes = await connection.QueryAsync<int>(@"
            SELECT DISTINCT M.MV_Type
            FROM RT_MOUVEMENT M
            WHERE M.SO_Id = @so AND M.MV_Domaine IN (0, 1)
              AND ((M.MV_Domaine = 0 AND M.CT_Type = 0) OR (M.MV_Domaine = 1 AND M.CT_Type = 1))
              AND M.MV_Date >= @debut AND M.MV_Date < @finExclude
            ORDER BY M.MV_Type", p);

        var origines = await connection.QueryAsync<int>(@"
            SELECT DISTINCT E.EC_Type
            FROM RT_MOUVEMENT M
            JOIN RT_AFFECTATION AF ON AF.MV_Id = M.MV_Id
            JOIN RT_ECHEANCE E ON AF.EC_Id = E.EC_Id
            WHERE M.SO_Id = @so AND M.MV_Domaine IN (0, 1)
              AND ((M.MV_Domaine = 0 AND M.CT_Type = 0) OR (M.MV_Domaine = 1 AND M.CT_Type = 1))
              AND M.MV_Date >= @debut AND M.MV_Date < @finExclude
            ORDER BY E.EC_Type", p);

        // TASK-067B — colonnes identifiantes (n° règlement, n° extrait, code banque) passées en
        // filterType 'list' : valeurs distinctes calculées sur le MÊME FromWhere/paramètres que la
        // liste principale (invariant TASK-040 : aucune valeur infiltrable, jamais absente du jeu
        // réel de la période), avec un filtre « vide » (RapprochementFilter()) pour ne pas dépendre
        // de la sélection courante de l'utilisateur. Bornées (DistinctsTopBound) : mesure de coût
        // documentée dans VERIFY/TASK-067B_verify.md.
        var baseParams = RapprochementParams(soId, dateDebut, dateFin, new RapprochementFilter());

        var numerosReglement = await connection.QueryAsync<string>(
            $@"SELECT DISTINCT TOP {DistinctsTopBound} M.MV_Numero AS Value {RapprochementFromWhere}
               AND M.MV_Numero IS NOT NULL AND M.MV_Numero <> ''
               ORDER BY M.MV_Numero", baseParams);

        var numerosExtrait = await connection.QueryAsync<string>(
            $@"SELECT DISTINCT TOP {DistinctsTopBound} M.MV_ExtraitNum AS Value {RapprochementFromWhere}
               AND M.MV_ExtraitNum IS NOT NULL AND M.MV_ExtraitNum <> ''
               ORDER BY M.MV_ExtraitNum", baseParams);

        var banques = await connection.QueryAsync<string>(
            $@"SELECT DISTINCT TOP {DistinctsTopBound} B.BanqueCode AS Value {RapprochementFromWhere}
               AND B.BanqueCode IS NOT NULL AND B.BanqueCode <> ''
               ORDER BY B.BanqueCode", baseParams);

        return new ReglementRapprochementDistincts
        {
            Modes = modes.ToList(),
            Origines = origines.ToList(),
            NumerosReglement = numerosReglement.ToList(),
            NumerosExtrait = numerosExtrait.ToList(),
            Banques = banques.ToList()
        };
    }

    public async Task<IEnumerable<AffectationDetailRow>> GetAffectationDetailsRapprochementAsync(
        int soId, IEnumerable<string> mvNumeros)
    {
        var list = mvNumeros.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList();
        if (list.Count == 0) return Array.Empty<AffectationDetailRow>();

        var sql = @"
            SELECT
                AF.MV_Id      AS MvId,
                M.MV_Numero   AS MvNumero,
                AF.AF_Montant AS AfMontant,
                E.EC_Id       AS EcId,
                E.EC_Type     AS EcType,
                COALESCE(E.DO_Numero, CAST(E.EC_Id AS VARCHAR(50))) AS EcNumero,
                E.EC_Montant  AS EcMontant,
                E.DO_Domaine  AS DoDomaine
            FROM dbo.RT_AFFECTATION AF
            JOIN dbo.RT_MOUVEMENT M ON AF.MV_Id = M.MV_Id
            JOIN dbo.RT_ECHEANCE E ON AF.EC_Id = E.EC_Id
            WHERE M.SO_Id = @soId AND M.MV_Numero IN @list";

        using var connection = _connectionFactory.CreateGrfConnection();
        return await connection.QueryAsync<AffectationDetailRow>(sql, new { soId, list });
    }

    // Borne du volume renvoyé par les endpoints « distincts » sur les colonnes identifiantes à
    // cardinalité potentiellement élevée (n° pièce, n° extrait, code banque). ExcelFilter (front)
    // plafonne déjà l'affichage à 200 lignes avec recherche ; 500 laisse une marge de recherche
    // serveur sans renvoyer un volume disproportionné. Mesure/justification : VERIFY/TASK-067B_verify.md.
    private const int DistinctsTopBound = 500;

    // ─── Interrogation « Factures » (pivot facture, lecture seule — TASK-041) ────────
    //
    // LECTURE SEULE STRICTE : SELECT sur la base GRF (RT_ECHEANCE pivot facture, agrégat
    // RT_AFFECTATION). Aucun INSERT/UPDATE/DELETE, aucun appel DLL/Sage. Le tampon DT_Id
    // (TASK-028) est LU (agrégé), jamais modifié. Période bornée obligatoire (perf), bornée
    // sur DO_Date. WHERE partagé liste + COUNT (leçon TASK-040 : compteur cohérent).
    // La famille B (HT/TVA) est enrichie APRÈS coup depuis le cache TASK-024 (autre connexion),
    // jamais jointe en SQL (base de persistance distincte de la base GRF).

    // Facture fournisseur = RT_ECHEANCE.DO_Domaine = 1 (ErpDomaine.Achat). Filtre les factures
    // client (Vente=0) et hors périmètre.
    private const int DoDomaineAchat = 1;

    /// <summary>Fragment FROM + WHERE commun à la liste, au comptage et aux distincts factures.</summary>
    private const string FacturesFromWhere = @"
        FROM RT_ECHEANCE E
        LEFT JOIN (
            SELECT AF.EC_Id,
                   COUNT(*)                                                  AS NbAffectations,
                   SUM(AF.AF_Montant)                                        AS Regle,
                   SUM(CASE WHEN AF.DT_Id IS NOT NULL THEN AF.AF_Montant ELSE 0 END) AS [Declare]
            FROM RT_AFFECTATION AF
            GROUP BY AF.EC_Id
        ) A ON A.EC_Id = E.EC_Id
        WHERE E.SO_Id = @so
          AND E.DO_Domaine = @doDomaine        -- factures fournisseur (Achat) uniquement
          AND E.DO_Date >= @debut AND E.DO_Date < @finExclude
          -- TASK-067B : n° facture / référence en MULTI-SÉLECTION RÉELLE (valeurs distinctes exactes,
          -- cf. GetFacturesInterrogationDistinctsAsync), fin du LIKE scalaire. Fournisseur reste en
          -- LIKE (texte libre, cardinalité non bornée — décision documentée en VERIFY).
          AND (@hasNumeros = 0 OR E.DO_Numero IN @numeros)
          AND (@fournisseur IS NULL OR E.CT_Code LIKE '%' + @fournisseur + '%' OR E.CT_Intitule LIKE '%' + @fournisseur + '%')
          AND (@hasReferences = 0 OR E.DO_Reference IN @references)
          -- origine/statut : multi-sélection, comparés à la MÊME expression de dérivation que la
          -- projection (LibelleEcType / Statut), sinon le filtre mentirait (leçon TASK-040).
          AND (@hasOrigines = 0 OR " + FactureOrigineExpr + @" IN @origines)
          AND (@hasStatuts = 0 OR " + FactureStatutExpr + @" IN @statuts)";

    // Réplique EXACTEMENT ReglementRapprochementRow.LibelleEcType (source unique côté domaine).
    private const string FactureOrigineExpr = @"(CASE E.EC_Type
              WHEN 0 THEN N'Sage'
              WHEN 111 THEN N'FGR'
              WHEN 4 THEN N'SoldeInitial'
              ELSE N'Autre (' + CAST(E.EC_Type AS nvarchar(20)) + N')'
          END)";

    // Réplique EXACTEMENT FactureInterrogationRow.Statut (dérivé des affectations DT_Id).
    private const string FactureStatutExpr = @"(CASE
              WHEN ISNULL(A.[Declare], 0) <= 0 THEN N'NonDeclarable'
              WHEN ISNULL(A.[Declare], 0) < ISNULL(A.Regle, 0) THEN N'Partiel'
              ELSE N'Total'
          END)";

    private static object FacturesParams(int soId, DateTime dateDebut, DateTime dateFin,
        IReadOnlyList<string>? numero, string? fournisseur, IReadOnlyList<string>? reference,
        IReadOnlyList<string>? origines, IReadOnlyList<string>? statuts)
    {
        var orig = (origines ?? Array.Empty<string>()).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
        var stat = (statuts ?? Array.Empty<string>()).Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
        // TASK-067B — multi-sélection réelle (valeurs distinctes exactes) pour les colonnes
        // identifiantes converties de LIKE scalaire vers filterType 'list'.
        var numeros = (numero ?? Array.Empty<string>()).Where(v => !string.IsNullOrWhiteSpace(v)).Distinct().ToList();
        var references = (reference ?? Array.Empty<string>()).Where(v => !string.IsNullOrWhiteSpace(v)).Distinct().ToList();
        return new
        {
            so = soId,
            doDomaine = DoDomaineAchat,
            debut = dateDebut.Date,
            finExclude = dateFin.Date.AddDays(1),
            numeros,
            hasNumeros = numeros.Count > 0 ? 1 : 0,
            fournisseur = string.IsNullOrWhiteSpace(fournisseur) ? null : fournisseur,
            references,
            hasReferences = references.Count > 0 ? 1 : 0,
            origines = orig,
            hasOrigines = orig.Count > 0 ? 1 : 0,
            statuts = stat,
            hasStatuts = stat.Count > 0 ? 1 : 0
        };
    }

    public async Task<IEnumerable<FactureInterrogationRow>> GetFacturesInterrogationAsync(
        int soId, DateTime dateDebut, DateTime dateFin,
        IReadOnlyList<string>? numero, string? fournisseur, IReadOnlyList<string>? reference,
        IReadOnlyList<string>? origines, IReadOnlyList<string>? statuts,
        int page, int size, string? sort)
    {
        // Whitelist ORDER BY (anti-injection). Tiebreaker EC_Id → pagination stable.
        var orderBy = sort switch
        {
            "date_asc"     => "E.DO_Date ASC",
            "date_desc"    => "E.DO_Date DESC",
            "ttc_asc"      => "E.EC_Montant ASC",
            "ttc_desc"     => "E.EC_Montant DESC",
            "solde_asc"    => "(E.EC_Montant - ISNULL(A.Regle, 0)) ASC",
            "solde_desc"   => "(E.EC_Montant - ISNULL(A.Regle, 0)) DESC",
            "numero_asc"   => "E.DO_Numero ASC",
            "numero_desc"  => "E.DO_Numero DESC",
            _              => "E.DO_Date DESC"
        };

        if (page < 1) page = 1;
        if (size < 1) size = 50;

        var sql = $@"
            SELECT
                E.EC_Id                            AS EcId,
                E.DO_Numero                        AS DoNumero,
                E.DO_Date                          AS DoDate,
                E.CT_Code                          AS CtCode,
                E.CT_Intitule                      AS CtIntitule,
                E.DO_Reference                     AS DoReference,
                E.EC_Montant                       AS EcMontant,
                E.EC_Type                          AS EcType,
                E.CT_No                            AS TiersNo,
                ISNULL(A.NbAffectations, 0)        AS NbAffectations,
                A.Regle                            AS Regle,
                A.[Declare]                        AS [Declare]
            {FacturesFromWhere}
            ORDER BY {orderBy}, E.EC_Id
            OFFSET @offset ROWS FETCH NEXT @size ROWS ONLY";

        var p = new DynamicParameters(FacturesParams(soId, dateDebut, dateFin, numero, fournisseur, reference, origines, statuts));
        p.Add("offset", (page - 1) * size);
        p.Add("size", size);

        List<FactureInterrogationRow> rows;
        using (var connection = _connectionFactory.CreateGrfConnection())
        {
            rows = (await connection.QueryAsync<FactureInterrogationRow>(sql, p)).ToList();
        }

        // Famille B : enrichissement depuis le cache de ventilation TASK-024 (connexion de
        // PERSISTANCE, distincte de GRF) — jamais de lecture Sage synchrone ici. Absence de
        // cache ⇒ « non valorisé » + motif (transparence).
        // TASK-118 : borné à soId (EC_Id seul peut collisionner entre deux bases Sage distinctes).
        await EnrichirFamilleBDepuisCacheAsync(soId, rows);
        return rows;
    }

    /// <summary>
    /// TASK-118 : <paramref name="soId"/> borne la lecture du cache à la société courante — sans
    /// ce filtre, un EC_Id partagé par coïncidence entre deux bases Sage physiquement distinctes
    /// ferait remonter la ventilation (ou l'erreur) d'une AUTRE société.
    /// </summary>
    private async Task EnrichirFamilleBDepuisCacheAsync(int soId, List<FactureInterrogationRow> rows)
    {
        if (rows.Count == 0) return;

        var ecIds = rows.Where(r => r.EcId > 0).Select(r => r.EcId).Distinct().ToList();
        var totals = new Dictionary<int, VentilationCacheTotal>();
        var erreurs = new Dictionary<int, VentilationCacheErreur>();

        if (ecIds.Count > 0)
        {
            try
            {
                using var cacheConn = _connectionFactory.CreatePersistenceConnection();
                // Totaux HT/TVA de la facture : le cache stocke les buckets TVA par (SO_Id, EC_Id, Taux).
                // On somme les BaseHT/MontantTva pour reconstituer les totaux de la facture.
                // TASK-072 : la ligne sentinelle d'erreur (CodeTaxe='ERREUR') est exclue de ce
                // SUM — jamais mêlée aux totaux réels (sinon 0 silencieux au lieu de « non valorisé »).
                var cacheRows = await cacheConn.QueryAsync<VentilationCacheTotal>(@"
                    SELECT EC_Id AS EcId, SUM(BaseHT) AS TotalHt, SUM(MontantTva) AS TotalTva
                    FROM DM_VENTILATION_SAGE_CACHE
                    WHERE SO_Id = @soId AND EC_Id IN @ecIds AND CodeTaxe <> 'ERREUR'
                    GROUP BY EC_Id", new { soId, ecIds });
                foreach (var c in cacheRows) totals[c.EcId] = c;

                // TASK-072 : motif précis des pièces exclues (incohérence Sage détectée), porté
                // par la ligne sentinelle — remplace le motif générique « OM non lue ».
                // TASK-076 : la ligne sentinelle porte en plus les montants bruts Sage tels que lus
                // AVANT exclusion (BrutHT/BrutTva/BrutParafiscale/BrutTtc, NULL si non capturés) —
                // donnée d'audit pour investigation manuelle côté ERP, jamais réintroduite dans les
                // totaux déclarables (Ht/Tva ci-dessus restent exclus pour ces factures).
                var erreurRows = await cacheConn.QueryAsync<VentilationCacheErreur>(@"
                    SELECT EC_Id AS EcId, MotifErreur,
                           BrutHT, BrutTva, BrutParafiscale, BrutTtc
                    FROM DM_VENTILATION_SAGE_CACHE
                    WHERE SO_Id = @soId AND EC_Id IN @ecIds AND CodeTaxe = 'ERREUR'", new { soId, ecIds });
                foreach (var e in erreurRows) erreurs[e.EcId] = e;
            }
            catch
            {
                // Cache indisponible (table absente / connexion KO) : on ne masque rien —
                // chaque ligne tombera en « non valorisé » + motif via AppliquerCacheB(null,null).
                totals.Clear();
                erreurs.Clear();
            }
        }

        foreach (var r in rows)
        {
            if (totals.TryGetValue(r.EcId, out var t))
                r.AppliquerCacheB(t.TotalHt, t.TotalTva);
            else if (erreurs.TryGetValue(r.EcId, out var e))
                r.AppliquerCacheB(null, null, e.MotifErreur, e.BrutHT, e.BrutTva, e.BrutParafiscale, e.BrutTtc);
            else
                r.AppliquerCacheB(null, null);
        }
    }

    public async Task<int> GetFacturesInterrogationCountAsync(
        int soId, DateTime dateDebut, DateTime dateFin,
        IReadOnlyList<string>? numero, string? fournisseur, IReadOnlyList<string>? reference,
        IReadOnlyList<string>? origines, IReadOnlyList<string>? statuts)
    {
        var sql = $"SELECT COUNT(*) {FacturesFromWhere}";
        using var connection = _connectionFactory.CreateGrfConnection();
        return await connection.ExecuteScalarAsync<int>(
            sql, FacturesParams(soId, dateDebut, dateFin, numero, fournisseur, reference, origines, statuts));
    }

    public async Task<FactureInterrogationDistincts> GetFacturesInterrogationDistinctsAsync(
        int soId, DateTime dateDebut, DateTime dateFin,
        string? rechercheNumero = null, string? rechercheReference = null)
    {
        using var connection = _connectionFactory.CreateGrfConnection();
        var p = new { so = soId, doDomaine = DoDomaineAchat, debut = dateDebut.Date, finExclude = dateFin.Date.AddDays(1) };

        var origines = await connection.QueryAsync<int>(@"
            SELECT DISTINCT E.EC_Type
            FROM RT_ECHEANCE E
            WHERE E.SO_Id = @so AND E.DO_Domaine = @doDomaine AND E.DO_Date >= @debut AND E.DO_Date < @finExclude
            ORDER BY E.EC_Type", p);

        // TASK-067B — n° facture / référence en filterType 'list' : valeurs distinctes sur le MÊME
        // FromWhere/paramètres que la liste principale (invariant TASK-040), filtre « vide » (pas de
        // dépendance à la sélection courante). Bornées (DistinctsTopBound) — mesure en VERIFY.
        var baseParams = FacturesParams(soId, dateDebut, dateFin, null, null, null, null, null);

        // TASK-168 : recherche serveur appliquée AVANT le TOP {DistinctsTopBound} — rend la
        // recherche exhaustive au-delà des 500 premières valeurs alphabétiques (le plafond reste
        // un garde-fou anti-charge sur le picklist SANS recherche, jamais retiré).
        var numeroSearch = string.IsNullOrWhiteSpace(rechercheNumero) ? null : rechercheNumero.Trim();
        var referenceSearch = string.IsNullOrWhiteSpace(rechercheReference) ? null : rechercheReference.Trim();
        var numerosParams = new DynamicParameters(baseParams);
        numerosParams.Add("recherche", numeroSearch);
        var referencesParams = new DynamicParameters(baseParams);
        referencesParams.Add("recherche", referenceSearch);

        var numeros = (await connection.QueryAsync<string>(
            $@"SELECT DISTINCT TOP {DistinctsTopBound} E.DO_Numero AS Value {FacturesFromWhere}
               AND E.DO_Numero IS NOT NULL AND E.DO_Numero <> ''
               AND (@recherche IS NULL OR E.DO_Numero LIKE @recherche + '%')
               ORDER BY E.DO_Numero", numerosParams)).ToList();

        var references = (await connection.QueryAsync<string>(
            $@"SELECT DISTINCT TOP {DistinctsTopBound} E.DO_Reference AS Value {FacturesFromWhere}
               AND E.DO_Reference IS NOT NULL AND E.DO_Reference <> ''
               AND (@recherche IS NULL OR E.DO_Reference LIKE @recherche + '%')
               ORDER BY E.DO_Reference", referencesParams)).ToList();

        return new FactureInterrogationDistincts
        {
            Origines = origines.ToList(),
            Numeros = numeros,
            References = references,
            // Troncature silencieuse interdite (Objectif §4) : uniquement pertinent pour la vue par
            // défaut (sans recherche) — une recherche qui renvoie 500 résultats reste, elle, un vrai
            // plafond anti-charge documenté (garde-fou), pas une troncature de la vue par défaut.
            NumerosTronque = numeroSearch is null && numeros.Count == DistinctsTopBound,
            ReferencesTronque = referenceSearch is null && references.Count == DistinctsTopBound
        };
    }

    // ─── TASK-135 — Mesure du délai de paiement fournisseur (indicateur de pilotage) ─────
    //
    // Réutilise EXACTEMENT le même principe que le module TVA (RapprochementDateRappExpr,
    // ci-dessus §"Interrogation Rapprochement bancaire globale") : date de rapprochement
    // pertinente = MV_Date pour l'espèce (MV_Type=0, auto-rapprochée) ; sinon MV_PointDate
    // débarrassé du sentinel SQL min 1753 (non pointé ⇒ NULL). Décision PO 19/07/2026 (CDC §3.3) :
    // même source locale que la TVA (RT_MOUVEMENT.MV_Point/MV_PointDate), jamais le mécanisme
    // Sage de l'ancien module RAS.
    //
    // Une facture peut être soldée par PLUSIEURS règlements (affectations partielles cumulées) :
    // on retient le MAX (la date à laquelle la facture a réellement été soldée dans son
    // intégralité), jamais le premier. LECTURE SEULE STRICTE.

    /// <summary>
    /// TASK-135 : dernière date de rapprochement bancaire pertinente par <c>EC_Id</c>, pour les
    /// factures fournies (LECTURE SEULE, base GRF). Un <c>EC_Id</c> absent du dictionnaire, ou
    /// présent avec une valeur NULL, signifie qu'aucune affectation rattachée n'est encore
    /// rapprochée (aucune date inventée — l'appelant doit traiter ce cas explicitement).
    /// </summary>
    public async Task<Dictionary<int, DateTime?>> GetDernieresDatesRapprochementAsync(int soId, IEnumerable<int> ecIds)
    {
        var ids = ecIds.Distinct().ToList();
        var result = new Dictionary<int, DateTime?>();
        if (ids.Count == 0) return result;

        using var connection = _connectionFactory.CreateGrfConnection();
        var rows = await connection.QueryAsync<DerniereDateRapprochementRow>(@"
            SELECT AF.EC_Id AS EcId,
                   MAX(CASE WHEN M.MV_Type = 0 THEN M.MV_Date ELSE NULLIF(M.MV_PointDate, '17530101') END) AS DerniereDateRapprochement
            FROM RT_AFFECTATION AF
            JOIN RT_MOUVEMENT M ON M.MV_Id = AF.MV_Id
            WHERE M.SO_Id = @soId AND AF.EC_Id IN @ecIds
            GROUP BY AF.EC_Id", new { soId, ecIds = ids });

        foreach (var r in rows) result[r.EcId] = r.DerniereDateRapprochement;
        return result;
    }

    private sealed class DerniereDateRapprochementRow
    {
        public int EcId { get; set; }
        public DateTime? DerniereDateRapprochement { get; set; }
    }

    // ─── Backfill rétroactif DateFacture/Reference (TASK-190) ──────────────────

    /// <summary>
    /// TASK-190 : <c>DO_Date</c>/<c>DO_Reference</c> réels pour les <paramref name="ecIds"/> fournis
    /// (LECTURE SEULE, base GRF), scopés à <paramref name="soId"/> (TASK-118 : un EC_Id peut
    /// collisionner entre deux sociétés). Un EC_Id absent du dictionnaire retourné n'existe plus
    /// dans RT_ECHEANCE pour cette société — l'appelant doit le signaler explicitement.
    /// Batché par 1 000 EC_Id (limite SQL Server 2 100 paramètres, même garde que
    /// <see cref="TamponnerAffectationsAsync"/>) — un parc réel (~5148 lignes EnCours, TASK-190)
    /// dépasse largement la limite en un seul <c>IN (...)</c>.
    /// </summary>
    public async Task<Dictionary<int, (DateTime? DoDate, string? DoReference)>> GetDatesFacturesEtReferencesAsync(int soId, IEnumerable<int> ecIds)
    {
        var ids = ecIds.Distinct().ToList();
        var result = new Dictionary<int, (DateTime? DoDate, string? DoReference)>();
        if (ids.Count == 0) return result;

        using var connection = _connectionFactory.CreateGrfConnection();

        const int batchSize = 1_000;
        for (int i = 0; i < ids.Count; i += batchSize)
        {
            var batch = ids.Skip(i).Take(batchSize).ToList();
            var rows = await connection.QueryAsync<DateFactureReferenceRow>(@"
                SELECT EC_Id AS EcId, DO_Date AS DoDate, DO_Reference AS DoReference
                FROM RT_ECHEANCE
                WHERE SO_Id = @soId AND EC_Id IN @ecIds", new { soId, ecIds = batch });

            foreach (var r in rows) result[r.EcId] = (r.DoDate, r.DoReference);
        }

        return result;
    }

    private sealed class DateFactureReferenceRow
    {
        public int EcId { get; set; }
        public DateTime? DoDate { get; set; }
        public string? DoReference { get; set; }
    }

    /// <summary>
    /// TASK-190 : écriture ciblée UNIQUEMENT sur DateFacture/Reference (base de persistance).
    /// Jamais aucune autre colonne dans le SET. <c>DbType.DateTime2</c> posé EXPLICITEMENT sur le
    /// paramètre <c>DateFacture</c> — sans cela, Dapper type par défaut un CLR <c>DateTime</c> en
    /// <c>DbType.DateTime</c> (constaté en rejeu réel), ce qui fait transiter la valeur par le
    /// domaine SQL <c>datetime</c> hérité (résolution 1/300 s) AVANT son écriture dans la colonne
    /// <c>datetime2(7)</c> — arrondi parasite constaté sur ~0,8% des lignes lors du premier rejeu
    /// (dérive sous la milliseconde, invisible à l'affichage mais cassant l'idempotence bit-à-bit
    /// exigée par la TASK). Boucle explicite (pas un seul <c>ExecuteAsync</c> batché sur une liste
    /// d'objets anonymes) car Dapper ne permet pas de typer un paramètre par élément dans ce mode.
    /// </summary>
    public async Task MettreAJourDateFactureEtReferenceAsync(IEnumerable<(Guid LigneId, DateTime? DateFacture, string? Reference)> lignesAMettreAJour)
    {
        var items = lignesAMettreAJour.ToList();
        if (items.Count == 0) return;

        using var connection = _connectionFactory.CreatePersistenceConnection();
        const string sql = "UPDATE DM_LGTVA SET DateFacture = @DateFacture, Reference = @Reference WHERE Id = @LigneId";

        foreach (var item in items)
        {
            var parametres = new Dapper.DynamicParameters();
            parametres.Add("LigneId", item.LigneId);
            parametres.Add("DateFacture", item.DateFacture, System.Data.DbType.DateTime2);
            parametres.Add("Reference", item.Reference);
            await connection.ExecuteAsync(sql, parametres);
        }
    }

    // ─── Tampon DT_Id (TASK-028) ───────────────────────────────────────────────────

    /// <summary>
    /// Pose le tampon DT_Id sur RT_AFFECTATION pour tous les mouvements dont le MV_Numero
    /// figure dans <paramref name="numerosRapprochement"/>.
    /// Set-based : un seul UPDATE avec IN (...) par batch de 1 000 numéros maximum.
    /// Opère sur la base GRF (GrfConnection).
    /// </summary>
    public async Task TamponnerAffectationsAsync(int dtId, IEnumerable<string> numerosRapprochement)
    {
        var numeros = numerosRapprochement.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().ToList();
        if (numeros.Count == 0) return;

        using var connection = _connectionFactory.CreateGrfConnection();

        // Batch de 1 000 numéros pour ne pas dépasser la limite SQL Server (2 100 paramètres).
        const int batchSize = 1_000;
        for (int i = 0; i < numeros.Count; i += batchSize)
        {
            var batch = numeros.Skip(i).Take(batchSize).ToList();

            // Paramètres dynamiques : @p0, @p1, …
            var paramNames = batch.Select((_, idx) => $"@p{idx}").ToList();
            var inClause   = string.Join(", ", paramNames);

            var sql = $@"
                UPDATE dbo.RT_AFFECTATION
                SET    DT_Id = @dtId
                WHERE  DT_Id IS NULL
                  AND  MV_Id IN (
                       SELECT MV_Id FROM dbo.RT_MOUVEMENT
                       WHERE  MV_Numero IN ({inClause})
                  )";

            var dynamicParams = new Dapper.DynamicParameters();
            dynamicParams.Add("dtId", dtId);
            for (int j = 0; j < batch.Count; j++)
                dynamicParams.Add($"p{j}", batch[j]);

            await connection.ExecuteAsync(sql, dynamicParams);
        }
    }

    /// <summary>
    /// Efface le tampon DT_Id (→ NULL) sur les affectations portant ce <paramref name="dtId"/>
    /// ET rattachées aux mouvements <paramref name="numerosRapprochement"/> de la déclaration.
    /// Le bornage par MV_Numero (miroir de <see cref="TamponnerAffectationsAsync"/>) garantit
    /// qu'une éventuelle collision de dtId dérivé ne libère jamais le verrou d'une autre
    /// déclaration. Le trigger autorise explicitement la transition valeur → NULL.
    /// Opère sur la base GRF (GrfConnection).
    /// </summary>
    public async Task DetamponnerAffectationsAsync(int dtId, IEnumerable<string> numerosRapprochement)
    {
        var numeros = numerosRapprochement?.Distinct().ToList() ?? new List<string>();
        if (numeros.Count == 0) return;

        using var connection = _connectionFactory.CreateGrfConnection();

        // Batch de 1 000 numéros (limite SQL Server 2 100 paramètres), miroir de la pose.
        const int batchSize = 1_000;
        for (int i = 0; i < numeros.Count; i += batchSize)
        {
            var batch = numeros.Skip(i).Take(batchSize).ToList();

            var paramNames = batch.Select((_, idx) => $"@p{idx}").ToList();
            var inClause   = string.Join(", ", paramNames);

            var sql = $@"
                UPDATE dbo.RT_AFFECTATION
                SET    DT_Id = NULL
                WHERE  DT_Id = @dtId
                  AND  MV_Id IN (
                       SELECT MV_Id FROM dbo.RT_MOUVEMENT
                       WHERE  MV_Numero IN ({inClause})
                  )";

            var dynamicParams = new Dapper.DynamicParameters();
            dynamicParams.Add("dtId", dtId);
            for (int j = 0; j < batch.Count; j++)
                dynamicParams.Add($"p{j}", batch[j]);

            await connection.ExecuteAsync(sql, dynamicParams);
        }
    }

    // ─── Diagnostic tampon DT_Id (TASK-094, lecture seule stricte) ─────────────

    /// <summary>Toutes les déclarations existantes, quelle que soit la société (diagnostic admin).</summary>
    public async Task<IEnumerable<DeclarationEntete>> GetToutesDeclarationsAsync()
    {
        using var connection = _connectionFactory.CreatePersistenceConnection();
        return await connection.QueryAsync<DeclarationEntete>("SELECT * FROM DM_ENTTVA");
    }

    /// <summary>
    /// Valeurs distinctes de DT_Id non nulles présentes sur RT_AFFECTATION. SELECT seul,
    /// base GRF (GrfConnection) — aucune écriture.
    /// </summary>
    public async Task<IEnumerable<int>> GetDistinctDtIdsAffectationsAsync()
    {
        using var connection = _connectionFactory.CreateGrfConnection();
        return await connection.QueryAsync<int>(
            "SELECT DISTINCT DT_Id FROM dbo.RT_AFFECTATION WHERE DT_Id IS NOT NULL");
    }

    /// <summary>TASK-094 (Option B) : pose ou efface DM_ENTTVA.DT_Id (base de persistance).</summary>
    public async Task SetDtIdDeclarationAsync(Guid declarationId, int? dtId)
    {
        using var connection = _connectionFactory.CreatePersistenceConnection();
        await connection.ExecuteAsync(
            "UPDATE DM_ENTTVA SET DT_Id = @DtId WHERE Id = @Id",
            new { Id = declarationId.ToString(), DtId = dtId });
    }

    // ─── Diagnostic explicatif en ligne d'une anomalie (TASK-144, lecture seule stricte) ──────

    /// <summary>TASK-144 : échéance RT_ECHEANCE (GRF) consultée. SELECT seul, aucune écriture.</summary>
    public async Task<EcheanceDiagnosticRow?> GetEcheanceDiagnosticAsync(int soId, int ecId)
    {
        using var connection = _connectionFactory.CreateGrfConnection();
        return await connection.QuerySingleOrDefaultAsync<EcheanceDiagnosticRow>(@"
            SELECT E.EC_Id, E.EC_No, E.EC_Type, E.DO_Numero, E.CT_Code, E.CT_Intitule, E.EC_MtDevise
            FROM RT_ECHEANCE E
            WHERE E.SO_Id = @so AND E.EC_Id = @ecId",
            new { so = soId, ecId });
    }

    /// <summary>
    /// TASK-144 : toutes les échéances RT_ECHEANCE (GRF) de la société portant ce DO_Numero —
    /// inclut l'échéance consultée. SELECT seul.
    /// </summary>
    public async Task<IReadOnlyList<EcheanceCollisionRow>> GetEcheancesMemeDoNumeroAsync(int soId, string doNumero)
    {
        if (string.IsNullOrWhiteSpace(doNumero)) return Array.Empty<EcheanceCollisionRow>();
        using var connection = _connectionFactory.CreateGrfConnection();
        var rows = await connection.QueryAsync<EcheanceCollisionRow>(@"
            SELECT E.EC_Id, E.EC_No, E.CT_Code, E.CT_Intitule
            FROM RT_ECHEANCE E
            WHERE E.SO_Id = @so AND E.DO_Numero = @doNumero
            ORDER BY E.EC_Id",
            new { so = soId, doNumero });
        return rows.ToList();
    }

    /// <summary>
    /// TASK-144 : motif d'échec déjà persisté (DM_VENTILATION_SAGE_CACHE, sentinelle CodeTaxe='ERREUR').
    /// Ne redéclenche AUCUNE lecture Sage — lit seulement le résultat déjà écrit (base de persistance).
    /// </summary>
    public async Task<string?> GetMotifErreurCacheAsync(int soId, int ecId)
    {
        if (ecId <= 0) return null;
        using var connection = _connectionFactory.CreatePersistenceConnection();
        return await connection.QuerySingleOrDefaultAsync<string?>(
            "SELECT TOP 1 MotifErreur FROM DM_VENTILATION_SAGE_CACHE WHERE SO_Id = @soId AND EC_Id = @ecId AND CodeTaxe = 'ERREUR'",
            new { soId, ecId });
    }

    /// <summary>
    /// TASK-147 : dernière lecture connue du cache (succès OU erreur, peu importe) pour cet EC_Id —
    /// sert à détecter un cache PÉRIMÉ (relu avec succès APRÈS la création de la déclaration alors
    /// que la ligne affiche encore un motif de rejet). Ne redéclenche AUCUNE lecture Sage. Le MERGE
    /// (TASK-076/077, VentilationSageCacheRepository.UpsertEntries) garantit qu'un seul état courant
    /// existe par EC_Id (soit des buckets de succès, soit une unique sentinelle ERREUR) — TOP 1
    /// suffit, TotalHT/TotalTva/TotalTtc sont dupliqués à l'identique sur chaque bucket d'une même
    /// lecture réussie.
    /// </summary>
    public async Task<CacheLectureRow?> GetDerniereLectureCacheAsync(int soId, int ecId)
    {
        if (ecId <= 0) return null;
        using var connection = _connectionFactory.CreatePersistenceConnection();
        return await connection.QuerySingleOrDefaultAsync<CacheLectureRow?>(
            @"SELECT TOP 1 DateLecture, MotifErreur, TotalHT, TotalTva, TotalTtc
              FROM DM_VENTILATION_SAGE_CACHE
              WHERE SO_Id = @soId AND EC_Id = @ecId
              ORDER BY DateLecture DESC",
            new { soId, ecId });
    }

    /// <summary>
    /// TASK-147 : buckets de taux du cache déjà lu avec succès (CodeTaxe &lt;&gt; 'ERREUR') pour cet
    /// EC_Id — sert à reconstruire la ligne candidate sans redéclencher de lecture Sage.
    /// </summary>
    public async Task<IReadOnlyList<CacheBucketRow>> GetBucketsCacheAsync(int soId, int ecId)
    {
        if (ecId <= 0) return Array.Empty<CacheBucketRow>();
        using var connection = _connectionFactory.CreatePersistenceConnection();
        var rows = await connection.QueryAsync<CacheBucketRow>(
            @"SELECT Taux, BaseHT AS HT, MontantTva AS Tva, TTC, CodeTaxe
              FROM DM_VENTILATION_SAGE_CACHE
              WHERE SO_Id = @soId AND EC_Id = @ecId AND CodeTaxe <> 'ERREUR'
              ORDER BY Taux",
            new { soId, ecId });
        return rows.ToList();
    }

    /// <summary>
    /// TASK-147 : supprime la ou les ligne(s) DM_LGTVA périmée(s) d'un EC_Id AVANT de les
    /// reconstruire depuis le cache à jour (jamais appelée hors de ce recalcul ciblé — la
    /// suppression est immédiatement suivie d'une réinsertion dans le même appelant).
    /// </summary>
    public async Task SupprimerLignesParEcIdAsync(Guid declarationId, int ecId)
    {
        using var connection = _connectionFactory.CreatePersistenceConnection();
        await connection.ExecuteAsync(
            "DELETE FROM DM_LGTVA WHERE DeclarationId = @DeclarationId AND EC_Id = @EcId",
            new { DeclarationId = declarationId.ToString(), EcId = ecId });
    }

    /// <summary>
    /// TASK-144 : documents de règlement Sage (F_DOCREGL) pour les EC_No fournis (jointure EC_No = DR_No).
    /// SELECT strictement lecture seule sur la base Sage (chaîne résolue dynamiquement par SO_Id et
    /// fournie par l'appelant — jamais codée en dur). Un EC_No absent du dictionnaire = orphelin.
    /// </summary>
    public async Task<IReadOnlyDictionary<int, DocumentReglementSageRow>> GetDocumentsReglementSageAsync(
        string sageConnectionString, IEnumerable<int> ecNos)
    {
        var ids = ecNos.Where(n => n > 0).Distinct().ToList();
        var result = new Dictionary<int, DocumentReglementSageRow>();
        if (ids.Count == 0) return result;

        using var connection = new Microsoft.Data.SqlClient.SqlConnection(sageConnectionString);
        var rows = await connection.QueryAsync<DocumentReglementSageRow>(@"
            SELECT DR_No, DO_Piece, DR_Date
            FROM F_DOCREGL
            WHERE DR_No IN @ecNos",
            new { ecNos = ids });

        foreach (var r in rows)
        {
            // Une échéance peut porter plusieurs lignes de règlement ; on ne garde que la présence
            // + le premier document (suffit au verdict « a un document Sage réel »).
            if (!result.ContainsKey(r.DR_No)) result[r.DR_No] = r;
        }
        return result;
    }

    /// <summary>
    /// TASK-155 : identifiant fiscal RÉEL de la société (base GRF, P_SOCIETE.SO_Identifiant) —
    /// à ne pas confondre avec SO_Id (clé interne) ni avec SO_DecTvaColNameIdentifiantFrs (nom de
    /// colonne ERP portant l'IF du TIERS/fournisseur, TASK-048). Vérifié en base réelle (23/07/2026) :
    /// colonne existante, siège juste après SO_RaisonSocial et à côté de SO_Ice (IF/ICE société).
    /// Null si la société est introuvable ou si la colonne est vide — jamais un défaut inventé,
    /// l'appelant doit bloquer explicitement (cf. TASK-151, aucune valeur placeholder).
    /// </summary>
    public async Task<string?> GetIdentifiantFiscalSocieteAsync(int soId)
    {
        using var connection = _connectionFactory.CreateGrfConnection();
        return await connection.QuerySingleOrDefaultAsync<string?>(
            "SELECT SO_Identifiant FROM P_SOCIETE WHERE SO_Id = @SoId", new { SoId = soId });
    }

    // ─── Code activité TVA — référentiel (TASK-161, lecture seule GRF) ─────────

    /// <summary>
    /// TASK-172 : "Encaissement"/"Decaissement" (mêmes libellés que <c>DM_LGTVA.Domaine</c>) vers
    /// le codage entier de <c>P_DECTVAACTIVITE.DTA_Domaine</c> (1/2, vérifié en base réelle
    /// 24/07/2026 : seules ces deux valeurs existent, aucun NULL). Toute autre valeur est un
    /// paramètre invalide — jamais mappée silencieusement.
    /// </summary>
    private static int DomaineVersDtaDomaine(string domaine) => domaine switch
    {
        "Encaissement" => 1,
        "Decaissement" => 2,
        _ => throw new ArgumentException($"Domaine '{domaine}' invalide — attendu 'Encaissement' ou 'Decaissement'.")
    };

    /// <summary>
    /// TASK-161/172 : référentiel P_DECTVAACTIVITE, lecture seule (liste déroulante front).
    /// <paramref name="domaine"/> optionnel filtre sur DTA_Domaine (1=Encaissement,
    /// 2=Decaissement) — null retourne le référentiel complet, non filtré.
    /// </summary>
    public async Task<IReadOnlyList<CodeActiviteReferentielRow>> GetReferentielCodesActiviteAsync(string? domaine = null)
    {
        using var connection = _connectionFactory.CreateGrfConnection();
        var sql = "SELECT DTA_Code AS Code, DTA_Intitule AS Libelle, DTA_Domaine AS Domaine FROM P_DECTVAACTIVITE";
        object? parametres = null;
        if (!string.IsNullOrEmpty(domaine))
        {
            sql += " WHERE DTA_Domaine = @DtaDomaine";
            parametres = new { DtaDomaine = DomaineVersDtaDomaine(domaine) };
        }
        sql += " ORDER BY DTA_Code";
        var rows = await connection.QueryAsync<CodeActiviteReferentielRow>(sql, parametres);
        return rows.ToList();
    }

    /// <summary>TASK-172 §4 : domaine (1/2) du code activité, null si absent du référentiel.</summary>
    public async Task<int?> GetDomaineCodeActiviteAsync(string codeActivite)
    {
        using var connection = _connectionFactory.CreateGrfConnection();
        return await connection.QuerySingleOrDefaultAsync<int?>(
            "SELECT DTA_Domaine FROM P_DECTVAACTIVITE WHERE DTA_Code = @Code", new { Code = codeActivite });
    }

    /// <summary>TASK-172 §4 : domaine ("Encaissement"/"Decaissement") de la ligne, null si absente.</summary>
    public async Task<string?> GetDomaineLigneAsync(Guid ligneId)
    {
        using var connection = _connectionFactory.CreatePersistenceConnection();
        return await connection.QuerySingleOrDefaultAsync<string?>(
            "SELECT Domaine FROM DM_LGTVA WHERE Id = @Id", new { Id = ligneId.ToString() });
    }

    /// <summary>
    /// TASK-161 : surcharge manuelle du code activité d'une ligne précise (Id = DM_LGTVA.Id).
    /// Trace qui/quand, même pattern que ValiderIncoherenceAsync (TASK-078).
    /// </summary>
    public async Task UpdateCodeActiviteLigneAsync(Guid ligneId, string codeActivite, string utilisateur)
    {
        using var connection = _connectionFactory.CreatePersistenceConnection();
        await connection.ExecuteAsync(
            @"UPDATE DM_LGTVA
              SET CodeActivite = @CodeActivite, CodeActiviteModifieManuellement = 1,
                  CodeActiviteModifiePar = @Utilisateur, CodeActiviteModifieLe = @Maintenant
              WHERE Id = @Id",
            new { Id = ligneId.ToString(), CodeActivite = codeActivite, Utilisateur = utilisateur, Maintenant = DateTime.UtcNow });
    }

    /// <summary>
    /// TASK-173 : domaines distincts portés par ces lignes — détecte une sélection mixte.
    /// Scopé par <paramref name="declarationId"/> (correctif rejet architecte 24/07/2026) :
    /// un ligneId n'appartenant pas à cette déclaration est ignoré (ne compte pas dans le
    /// résultat), exactement comme s'il n'existait pas — jamais utilisé pour déterminer le
    /// domaine effectif ni écrit par UpdateCodeActiviteBulkByIdsAsync ci-dessous.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetDomainesDistinctsLignesAsync(Guid declarationId, IEnumerable<Guid> ligneIds)
    {
        var ids = ligneIds.Select(g => g.ToString()).ToList();
        if (ids.Count == 0) return Array.Empty<string>();
        using var connection = _connectionFactory.CreatePersistenceConnection();
        var sql = $"SELECT DISTINCT Domaine FROM DM_LGTVA WHERE DeclarationId = @DeclarationId AND Id IN ({string.Join(",", ids.Select(id => $"'{id}'"))})";
        var rows = await connection.QueryAsync<string>(sql, new { DeclarationId = declarationId.ToString() });
        return rows.ToList();
    }

    /// <summary>
    /// TASK-173 : affectation en masse du code activité par liste explicite d'IDs (SQL batch).
    /// Scopé par <paramref name="declarationId"/> (correctif rejet architecte 24/07/2026) : sans
    /// ce filtre, un ligneId appartenant à une autre déclaration (y compris Clôturée) aurait été
    /// écrit malgré le garde-fou de clôture qui ne vérifie que la déclaration de l'URL — c'était
    /// exactement le trou que ce nouvel endpoint dédié devait éviter.
    /// </summary>
    public async Task UpdateCodeActiviteBulkByIdsAsync(Guid declarationId, IEnumerable<Guid> ligneIds, string codeActivite, string utilisateur)
    {
        var ids = ligneIds.Select(g => g.ToString()).ToList();
        if (ids.Count == 0) return;
        using var connection = _connectionFactory.CreatePersistenceConnection();
        var sql = $@"UPDATE DM_LGTVA
                     SET CodeActivite = @CodeActivite, CodeActiviteModifieManuellement = 1,
                         CodeActiviteModifiePar = @Utilisateur, CodeActiviteModifieLe = @Maintenant
                     WHERE DeclarationId = @DeclarationId AND Id IN ({string.Join(",", ids.Select(id => $"'{id}'"))})";
        await connection.ExecuteAsync(sql, new { DeclarationId = declarationId.ToString(), CodeActivite = codeActivite, Utilisateur = utilisateur, Maintenant = DateTime.UtcNow });
    }

    /// <summary>
    /// TASK-173 : affectation en masse du code activité par domaine + filtre texte (fournisseur/
    /// facture) — même mécanique de sélection que UpdateLignesEtatBulkAsync (TASK-012), SQL batch.
    /// </summary>
    public async Task UpdateCodeActiviteBulkAsync(Guid declarationId, string domaine, string? filter, string codeActivite, string utilisateur)
    {
        using var connection = _connectionFactory.CreatePersistenceConnection();
        var sql = @"UPDATE DM_LGTVA
                    SET CodeActivite = @CodeActivite, CodeActiviteModifieManuellement = 1,
                        CodeActiviteModifiePar = @Utilisateur, CodeActiviteModifieLe = @Maintenant
                    WHERE DeclarationId = @DeclarationId AND Domaine = @Domaine ";

        if (!string.IsNullOrEmpty(filter))
            sql += " AND (NumeroFacture LIKE '%' + @Filter + '%' OR TiersNom LIKE '%' + @Filter + '%') ";

        await connection.ExecuteAsync(sql, new
        {
            CodeActivite = codeActivite,
            Utilisateur = utilisateur,
            Maintenant = DateTime.UtcNow,
            DeclarationId = declarationId.ToString(),
            Domaine = domaine,
            Filter = filter
        });
    }
}

