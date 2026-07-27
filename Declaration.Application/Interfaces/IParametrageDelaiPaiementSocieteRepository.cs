using System;
using System.Threading.Tasks;
using Declaration.Application.Entities;

namespace Declaration.Application.Interfaces;

/// <summary>
/// TASK-128 : accès à la table neuve <c>DM_PARAM_DELAIPAIEMENT_SOCIETE</c> (propriété exclusive
/// GRF). Une lecture/écriture par société — pas d'historique de versions (une nouvelle saisie
/// écrase la précédente, comme un paramètre).
/// </summary>
public interface IParametrageDelaiPaiementSocieteRepository
{
    /// <summary>Retourne le paramétrage de la société, ou null si aucune date de mise en route n'a encore été saisie ("pas encore configuré").</summary>
    Task<ParametrageDelaiPaiementSociete?> GetAsync(int societeId);

    /// <summary>Upsert (une ligne par société) de la date de mise en route.</summary>
    Task SetAsync(int societeId, DateTime dateMiseEnRoute, int? utilisateurId);
}
