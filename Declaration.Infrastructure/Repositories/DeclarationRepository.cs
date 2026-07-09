using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Declaration.Application.Entities;
using Declaration.Application.Interfaces;

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
            "SELECT * FROM DeclarationEntete WHERE Id = @Id", new { Id = id.ToString() });
    }

    public async Task<DeclarationEntete?> GetByNumeroAsync(string numero)
    {
        using var connection = _connectionFactory.CreatePersistenceConnection();
        return await connection.QuerySingleOrDefaultAsync<DeclarationEntete>(
            "SELECT * FROM DeclarationEntete WHERE Numero = @Numero", new { Numero = numero });
    }

    public async Task<IEnumerable<DeclarationEntete>> GetAllAsync(string societeId, int? exercice, StatutDeclaration? statut)
    {
        using var connection = _connectionFactory.CreatePersistenceConnection();
        var sql = "SELECT * FROM DeclarationEntete WHERE SocieteId = @SocieteId";
        if (exercice.HasValue) sql += " AND Exercice = @Exercice";
        if (statut.HasValue) sql += " AND Statut = @Statut";

        return await connection.QueryAsync<DeclarationEntete>(sql, new { SocieteId = societeId, Exercice = exercice, Statut = statut });
    }

    public async Task CreateAsync(DeclarationEntete declaration)
    {
        using var connection = _connectionFactory.CreatePersistenceConnection();
        var sql = @"INSERT INTO DeclarationEntete 
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
        var sql = "UPDATE DeclarationEntete SET Statut = @Statut WHERE Id = @Id";
        await connection.ExecuteAsync(sql, new { Id = id.ToString(), Statut = (int)statut });
    }

    public async Task<bool> ExistsAsync(string societeId, int exercice, int periode, TypePeriode type)
    {
        using var connection = _connectionFactory.CreatePersistenceConnection();
        var count = await connection.ExecuteScalarAsync<int>(
            @"SELECT COUNT(1) FROM DeclarationEntete 
              WHERE SocieteId = @SocieteId AND Exercice = @Exercice AND Periode = @Periode AND Type = @Type",
            new { SocieteId = societeId, Exercice = exercice, Periode = periode, Type = (int)type });
        return count > 0;
    }

    public async Task SaveLignesCandidatesAsync(IEnumerable<LigneCandidate> lignes)
    {
        if (!lignes.Any()) return;
        using var connection = _connectionFactory.CreatePersistenceConnection();
        var sql = @"INSERT INTO LigneCandidate 
                    (Id, DeclarationId, Etat, Domaine, MotifRejet, NumeroFacture, NumeroRapprochement, TiersNom, 
                     TiersIdentifiantFiscal, TiersICE, HT, Taux, TVA, TTC, ModePaiement, DatePaiement, DateFacture, Source)
                    VALUES (@Id, @DeclarationId, @Etat, @Domaine, @MotifRejet, @NumeroFacture, @NumeroRapprochement, @TiersNom, 
                     @TiersIdentifiantFiscal, @TiersICE, @HT, @Taux, @TVA, @TTC, @ModePaiement, @DatePaiement, @DateFacture, @Source)";
        foreach (var l in lignes)
        {
            await connection.ExecuteAsync(sql, new {
                Id = l.Id.ToString(),
                DeclarationId = l.DeclarationId.ToString(),
                Etat = (int)l.Etat,
                l.Domaine,
                l.MotifRejet,
                l.NumeroFacture,
                l.NumeroRapprochement,
                l.TiersNom,
                l.TiersIdentifiantFiscal,
                l.TiersICE,
                l.HT,
                l.Taux,
                l.TVA,
                l.TTC,
                l.ModePaiement,
                DatePaiement = l.DatePaiement,
                DateFacture = l.DateFacture,
                l.Source
            });
        }
    }

    public async Task<IEnumerable<LigneCandidate>> GetLignesAsync(Guid declarationId, string domaine, int page, int pageSize, string? sort, string? filter)
    {
        using var connection = _connectionFactory.CreatePersistenceConnection();
        // Whitelist pour ORDER BY (injection SQL proof)
        var allowedSorts = new[] { "DateFacture DESC", "DateFacture ASC", "NumeroFacture ASC" };
        if (!allowedSorts.Contains(sort)) sort = "DateFacture DESC";

        var sql = @"SELECT * FROM LigneCandidate 
                    WHERE DeclarationId = @DeclarationId AND Domaine = @Domaine ";
        
        if (!string.IsNullOrEmpty(filter))
        {
            // Check if it's a JSON filter from DomainGrid
            if (filter.StartsWith("{"))
            {
                try
                {
                    var json = System.Text.Json.JsonDocument.Parse(filter);
                    if (json.RootElement.TryGetProperty("numeroRapprochement", out var nr))
                    {
                        sql += $" AND NumeroRapprochement = '{nr.GetString()}' ";
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
                                sql += $" AND Etat IN ({string.Join(",", etats)}) ";
                        }
                    }
                }
                catch {}
            }
            else
            {
                sql += " AND (NumeroFacture LIKE '%' + @Filter + '%' OR TiersNom LIKE '%' + @Filter + '%') ";
            }
        }
            
        sql += $" ORDER BY {sort}";
        // Pagination SQL Server
        sql += " OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
        
        return await connection.QueryAsync<LigneCandidate>(sql, new { 
            DeclarationId = declarationId.ToString(), 
            Domaine = domaine, 
            Filter = filter,
            Offset = (page - 1) * pageSize,
            PageSize = pageSize
        });
    }

    public async Task<int> GetLignesCountAsync(Guid declarationId, string domaine, string? filter)
    {
        using var connection = _connectionFactory.CreatePersistenceConnection();
          var sql = @"SELECT COUNT(*) FROM LigneCandidate 
                      WHERE DeclarationId = @DeclarationId AND Domaine = @Domaine ";
          
          if (!string.IsNullOrEmpty(filter))
          {
              if (filter.StartsWith("{"))
              {
                  try
                  {
                      var json = System.Text.Json.JsonDocument.Parse(filter);
                      if (json.RootElement.TryGetProperty("numeroRapprochement", out var nr))
                      {
                          sql += $" AND NumeroRapprochement = '{nr.GetString()}' ";
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
                                  sql += $" AND Etat IN ({string.Join(",", etats)}) ";
                          }
                      }
                  }
                  catch {}
              }
              else
              {
                  sql += " AND (NumeroFacture LIKE '%' + @Filter + '%' OR TiersNom LIKE '%' + @Filter + '%') ";
              }
          }
            
        return await connection.ExecuteScalarAsync<int>(sql, new { DeclarationId = declarationId.ToString(), Domaine = domaine, Filter = filter });
    }

    public async Task UpdateLigneEtatAsync(Guid ligneId, EtatLigne nouvelEtat)
    {
        using var connection = _connectionFactory.CreatePersistenceConnection();
        var sql = "UPDATE LigneCandidate SET Etat = @Etat WHERE Id = @Id";
        await connection.ExecuteAsync(sql, new { Id = ligneId.ToString(), Etat = (int)nouvelEtat });
    }

    public async Task UpdateLignesEtatBulkAsync(Guid declarationId, string domaine, string? filter, EtatLigne nouvelEtat)
    {
        using var connection = _connectionFactory.CreatePersistenceConnection();
        var sql = @"UPDATE LigneCandidate SET Etat = @Etat 
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
        var sql = $"UPDATE LigneCandidate SET Etat = @Etat WHERE Id IN ({string.Join(",", ids.Select(id => $"'{id}'"))})";
        await connection.ExecuteAsync(sql, new { Etat = (int)nouvelEtat });
    }
}
