using System;
using System.Threading.Tasks;
using Declaration.Application.Interfaces;
using Declaration.Core;

namespace Declaration.Application.Services;

/// <summary>
/// TASK-127 (socle Délai de Paiement Maroc) : service partagé de résolution du délai de paiement
/// applicable + calcul de l'échéance légale. Destiné aux consommateurs TASK-129/131/135.
/// Il n'apporte QUE l'orchestration des lectures (conventions filtrées par domaine, jours de repos,
/// délai par défaut société) et délègue tout le métier au calculateur pur
/// <see cref="EcheanceLegaleCalculator"/> (Declaration.Core), testable hors base.
/// </summary>
public interface IDelaiPaiementService
{
    /// <summary>
    /// Résout le délai applicable et l'échéance légale pour un document d'un tiers.
    /// </summary>
    /// <param name="societeId">Société (SO_Id).</param>
    /// <param name="tiersNo">Tiers (CT_No).</param>
    /// <param name="documentNumero">Numéro du document (facture) — sert à l'appariement convention Facture.</param>
    /// <param name="dateDocument">Date du document.</param>
    /// <param name="domaine">Domaine Achat/Vente (filtre les conventions chargées).</param>
    Task<ResultatDelaiPaiement> ResoudreDelaiAsync(
        int societeId,
        int tiersNo,
        string? documentNumero,
        DateTime dateDocument,
        DomaineDelaiPaiement domaine);

    /// <summary>
    /// TASK-131 (ajout additif au socle TASK-127) : charge UNE SEULE FOIS le référentiel de calcul
    /// (conventions filtrées par domaine + jours de repos + délai défaut société) pour résoudre
    /// ensuite N échéances légales sans requête supplémentaire.
    ///
    /// Nécessaire à la sélection DDP (TASK-131), qui traite plusieurs centaines/milliers d'échéances
    /// en une passe : <see cref="ResoudreDelaiAsync"/> émettrait 3 requêtes SQL PAR échéance (N+1).
    /// Le métier reste dans le SEUL calculateur <see cref="EcheanceLegaleCalculator"/>, appelé par
    /// <see cref="ContexteDelaiPaiement.Resoudre"/> — aucune logique dupliquée, aucun changement de
    /// comportement pour les consommateurs existants de <see cref="ResoudreDelaiAsync"/>.
    /// </summary>
    Task<ContexteDelaiPaiement> ChargerContexteAsync(int societeId, DomaineDelaiPaiement domaine);
}

/// <inheritdoc cref="IDelaiPaiementService"/>
public sealed class DelaiPaiementService : IDelaiPaiementService
{
    private readonly IConventionDelaiPaiementRepository _conventions;
    private readonly IJoursReposRepository _joursRepos;
    private readonly IDelaiPaiementParametrageRepository _parametrage;

    public DelaiPaiementService(
        IConventionDelaiPaiementRepository conventions,
        IJoursReposRepository joursRepos,
        IDelaiPaiementParametrageRepository parametrage)
    {
        _conventions = conventions ?? throw new ArgumentNullException(nameof(conventions));
        _joursRepos = joursRepos ?? throw new ArgumentNullException(nameof(joursRepos));
        _parametrage = parametrage ?? throw new ArgumentNullException(nameof(parametrage));
    }

    public async Task<ResultatDelaiPaiement> ResoudreDelaiAsync(
        int societeId,
        int tiersNo,
        string? documentNumero,
        DateTime dateDocument,
        DomaineDelaiPaiement domaine)
    {
        var conventions = await _conventions.GetConventionsActivesAsync(societeId, domaine);
        var joursRepos = await _joursRepos.GetJoursReposAsync(societeId);
        var nombreJoursDefaut = await _parametrage.GetNombreJoursDelaiDefautAsync(societeId);

        return EcheanceLegaleCalculator.Calculer(
            dateDocument,
            tiersNo,
            documentNumero,
            conventions,
            nombreJoursDefaut,
            joursRepos);
    }

    /// <inheritdoc />
    public async Task<ContexteDelaiPaiement> ChargerContexteAsync(int societeId, DomaineDelaiPaiement domaine)
    {
        var conventions = await _conventions.GetConventionsActivesAsync(societeId, domaine);
        var joursRepos = await _joursRepos.GetJoursReposAsync(societeId);
        var nombreJoursDefaut = await _parametrage.GetNombreJoursDelaiDefautAsync(societeId);

        return new ContexteDelaiPaiement
        {
            Conventions = conventions,
            JoursRepos = joursRepos,
            NombreJoursDefautSociete = nombreJoursDefaut
        };
    }
}
