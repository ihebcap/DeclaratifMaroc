using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Declaration.Core;

namespace Declaration.Application.Interfaces;

/// <summary>
/// TASK-131 : contrat de LECTURE SEULE STRICTE pour la sélection des lignes hors délai de la
/// Déclaration Délai de Paiement Maroc. Uniquement des <c>SELECT</c> sur des tables EXISTANTES,
/// possédées GRF (<c>RT_ECHEANCE</c>, <c>RT_AFFECTATION</c>, <c>RT_MOUVEMENT</c>,
/// <c>RT_DECLARATIONDELAISPAIEMENT</c>/<c>LG</c>, <c>P_SOCIETE</c>/<c>P_SOCIETEDEVISE</c>) :
/// AUCUN INSERT/UPDATE/DELETE, AUCUNE table créée, AUCUNE modification de schéma (cf. TASK-131
/// §Périmètre STRICT).
/// </summary>
public interface ISelectionDelaiPaiementRepository
{
    /// <summary>
    /// Devise société (<c>P_DEVISE.DV_Id</c>) résolue comme le legacy
    /// (<c>Societe.GetDefaultDeviseSociete()</c>) : <c>P_SOCIETE.SO_DeviseErpNo</c> →
    /// <c>P_SOCIETEDEVISE.SD_No</c> → <c>DV_Id</c>. Lève une exception explicite si la société ou sa
    /// devise par défaut est introuvable (jamais de repli silencieux qui élargirait la sélection).
    /// </summary>
    Task<int> GetDeviseSocieteIdAsync(int soId);

    /// <summary>
    /// Échéances candidates (factures fournisseur, domaine Achat) après application des SEUILS
    /// LÉGAUX et du filtre devise société. Les seuils sont passés en paramètres depuis
    /// <see cref="SeuilsLegauxDelaiPaiement"/> — point unique de vérité partagé avec le calculateur.
    /// </summary>
    Task<IReadOnlyList<EcheanceDelaiPaiement>> GetEcheancesCandidatesAsync(
        int soId,
        int deviseSocieteId,
        DateTime dateDebutDeclarationLoi,
        DateTime dateLimiteSeuilMontant,
        decimal seuilMontant);

    /// <summary>
    /// Affectations (<c>RT_AFFECTATION</c>) jointes à leur règlement (<c>RT_MOUVEMENT</c>) pour les
    /// échéances fournies. Batché côté implémentation (limite SQL Server de 2 100 paramètres).
    /// </summary>
    Task<IReadOnlyList<AffectationDelaiPaiement>> GetAffectationsAsync(int soId, IEnumerable<int> ecIds);

    /// <summary>
    /// Anti-double-déclaration (correction n°1) : pour chaque <c>EC_Id</c> déjà présent dans
    /// <c>RT_DECLARATIONDELAISPAIEMENTLG</c>, le MAX de <c>RT_DECLARATIONDELAISPAIEMENT.DDP_DateFin</c>
    /// — TOUTES déclarations confondues (aucun filtre de statut), y compris celles produites par
    /// l'ancien applicatif puisque la table est partagée/réutilisée telle quelle. Un <c>EC_Id</c>
    /// absent du dictionnaire n'a JAMAIS été déclaré.
    /// </summary>
    Task<IReadOnlyDictionary<int, DateTime>> GetDernieresBornesDeclareesAsync(int soId, IEnumerable<int> ecIds);
}
