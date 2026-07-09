using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Declaration.Application.Entities;

namespace Declaration.Application.Interfaces;

public interface IDeclarationRepository
{
    Task<DeclarationEntete?> GetByIdAsync(Guid id);
    Task<DeclarationEntete?> GetByNumeroAsync(string numero);
    Task<IEnumerable<DeclarationEntete>> GetAllAsync(string societeId, int? exercice, StatutDeclaration? statut);
    Task CreateAsync(DeclarationEntete declaration);
    Task UpdateStatutAsync(Guid id, StatutDeclaration statut);
    Task<bool> ExistsAsync(string societeId, int exercice, int periode, TypePeriode type);
    
    // Lignes candidates
    Task SaveLignesCandidatesAsync(IEnumerable<LigneCandidate> lignes);
    Task<IEnumerable<LigneCandidate>> GetLignesAsync(Guid declarationId, string domaine, int page, int pageSize, string? sort, string? filter);
    Task<int> GetLignesCountAsync(Guid declarationId, string domaine, string? filter);
    Task UpdateLigneEtatAsync(Guid ligneId, EtatLigne nouvelEtat);
    Task UpdateLignesEtatBulkAsync(Guid declarationId, string domaine, string? filter, EtatLigne nouvelEtat);
    Task UpdateLignesEtatBulkByIdsAsync(IEnumerable<Guid> ligneIds, EtatLigne nouvelEtat);
}
