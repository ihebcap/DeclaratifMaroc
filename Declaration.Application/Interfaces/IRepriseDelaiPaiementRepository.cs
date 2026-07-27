using System;
using System.Threading.Tasks;
using Declaration.Application.Entities;

namespace Declaration.Application.Interfaces;

/// <summary>
/// TASK-128 : accès à la table neuve <c>DM_REPRISE_DELAIPAIEMENT</c> (propriété exclusive GRF) —
/// reprise manuelle par échéance (<c>SO_Id</c> + <c>EC_Id</c>) pour les échéances antérieures à la
/// date de mise en route de leur société et sans historique legacy.
/// </summary>
public interface IRepriseDelaiPaiementRepository
{
    /// <summary>Retourne la reprise saisie pour cette échéance précise, ou null si aucune reprise n'a été saisie.</summary>
    Task<RepriseDelaiPaiement?> GetAsync(int societeId, int ecId);

    /// <summary>Upsert (une ligne par échéance) de la borne "déjà déclaré jusqu'au".</summary>
    Task SetAsync(int societeId, int ecId, DateTime dateDejaDeclareeJusquau, int? utilisateurId);
}
