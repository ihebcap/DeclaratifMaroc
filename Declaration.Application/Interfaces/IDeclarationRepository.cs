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

    // ─── Tampon DT_Id (verrou d'intégration déclaration, TASK-028) ─────────────
    /// <summary>
    /// Pose le tampon DT_Id sur toutes les affectations RT_AFFECTATION correspondant aux
    /// numéros de rapprochement (MV_Numero) intégrés dans la déclaration.
    /// S'exécute sur la base GRF (GrfConnection), dans la même transaction que la clôture.
    /// </summary>
    Task TamponnerAffectationsAsync(int dtId, IEnumerable<string> numerosRapprochement);

    /// <summary>
    /// Efface le tampon DT_Id (→ NULL) sur toutes les affectations liées à ce numéro de déclaration.
    /// Permet la réouverture : l'affectation redevient sélectionnable et modifiable.
    /// </summary>
    Task DetamponnerAffectationsAsync(int dtId);
}
