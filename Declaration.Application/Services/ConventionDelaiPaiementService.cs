using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Declaration.Application.Entities;
using Declaration.Application.Interfaces;
using Declaration.Core;

namespace Declaration.Application.Services;

/// <summary>
/// TASK-129 (Délai de Paiement Maroc — Convention par tiers) : requête de création d'une convention.
/// Reproduit les paramètres de <c>SocieteManager.Complement.cs:471-483</c>
/// (<c>ConventionDelaisPaiementTiersCreate</c>). La pièce jointe reste optionnelle au niveau service
/// (décision PO 19/07/2026) — seule la cohérence FileName/File est contrôlée (legacy l.489), jamais
/// leur présence.
/// </summary>
public sealed class CreerConventionDelaiPaiementRequest
{
    public required int SocieteId { get; init; }
    public required int TiersNo { get; init; }
    public required string TiersCode { get; init; }
    public required DateTime Date { get; init; }
    public required string Numero { get; init; }
    public DateTime? DateDebut { get; init; }
    public DateTime? DateFin { get; init; }
    public required int NombreJoursDelaisPaiement { get; init; }
    public required DomaineDelaiPaiement Domaine { get; init; }
    public required TypeConventionDelaiPaiement Type { get; init; }
    public int? FactureNo { get; init; }
    public string? FileName { get; init; }
    public byte[]? File { get; init; }
}

/// <summary>
/// TASK-129 (Délai de Paiement Maroc — Convention par tiers) : orchestration CRUD +  contrôles métier
/// sur <c>RT_CONVENTIONTIERS</c>, reproduisant <c>SocieteManager.Complement.cs:471-589</c>
/// (<c>ConventionDelaisPaiementTiersCreate</c>/<c>Terminer</c>/<c>Delete</c>), avec la correction PO
/// du contrôle de chevauchement (bidirectionnel, cf. <see cref="ConventionDelaiPaiementValidator"/>).
/// Tout le métier PUR (plafond, chevauchement, unicité facture, bornes clôture anticipée) est délégué
/// à <see cref="ConventionDelaiPaiementValidator"/> (Declaration.Core, testable hors DB) ; ce service
/// n'apporte que l'orchestration des lectures/écritures.
/// </summary>
public interface IConventionDelaiPaiementService
{
    Task<int> CreerAsync(CreerConventionDelaiPaiementRequest request);
    Task<ConventionDelaiPaiementTiers?> GetAsync(int cpId);
    Task<IReadOnlyList<ConventionDelaiPaiementTiers>> GetAllAsync(int societeId, DomaineDelaiPaiement domaine);
    Task TerminerAsync(int cpId, DateTime nouvelleDateFin);
    Task DeleteAsync(int cpId);

    // ─── TASK-130 (front) : projections de lecture additives pour l'écran conventions ───────────
    Task<IReadOnlyList<ConventionDelaiPaiementListItem>> GetAllForListAsync(int societeId, DomaineDelaiPaiement domaine);
    Task<IReadOnlyList<FactureNonPayeeItem>> GetFacturesNonPayeesAsync(int societeId, int tiersNo, DomaineDelaiPaiement domaine);
    Task<IReadOnlyList<TiersRechercheItem>> SearchTiersAsync(int societeId, DomaineDelaiPaiement domaine, string? recherche);
}

/// <inheritdoc cref="IConventionDelaiPaiementService"/>
public sealed class ConventionDelaiPaiementService : IConventionDelaiPaiementService
{
    private readonly IConventionDelaiPaiementTiersRepository _repository;

    public ConventionDelaiPaiementService(IConventionDelaiPaiementTiersRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<int> CreerAsync(CreerConventionDelaiPaiementRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (request.TiersNo <= 0) throw new ArgumentException("Le tiers est obligatoire.", nameof(request.TiersNo));
        if (string.IsNullOrEmpty(request.TiersCode)) throw new ArgumentException("Le code tiers est obligatoire.", nameof(request.TiersCode));
        if (string.IsNullOrEmpty(request.Numero)) throw new ArgumentException("Le numéro de la convention est obligatoire.", nameof(request.Numero));
        // Legacy l.489 : cohérence FileName/File uniquement (jamais leur présence — décision PO 19/07/2026).
        if (!string.IsNullOrEmpty(request.FileName) && request.File == null)
            throw new ArgumentException("Le fichier est requis lorsqu'un nom de fichier est fourni.", nameof(request.File));

        // Plafond 180 jours (legacy l.490-492), reproduit à l'identique.
        ConventionDelaiPaiementValidator.ValiderPlafond(request.NombreJoursDelaisPaiement);

        // Legacy l.495-496 : une convention de même numéro existe déjà pour ce tiers/domaine ?
        if (await _repository.ExisteNumeroAsync(request.SocieteId, request.TiersNo, request.Numero, request.Domaine))
            throw new InvalidOperationException($"La convention [{request.Numero}] existe déjà.");

        var conventionsExistantes = await _repository.GetAllForTiersAsync(request.SocieteId, request.TiersNo, request.Domaine);

        if (request.Type == TypeConventionDelaiPaiement.Convention)
        {
            if (!request.DateDebut.HasValue)
                throw new ArgumentException("La date début est obligatoire pour une convention.", nameof(request.DateDebut));
            if (!request.DateFin.HasValue)
                throw new ArgumentException("La date fin est obligatoire pour une convention.", nameof(request.DateFin));

            ConventionDelaiPaiementValidator.ValiderDatesConvention(request.DateDebut.Value, request.DateFin.Value);

            // Contrôle de chevauchement CORRIGÉ (bidirectionnel, décision PO 19/07/2026) contre
            // TOUTES les conventions existantes du tiers/domaine ayant une plage de dates.
            var resumes = conventionsExistantes
                .Where(x => x.DateDebut.HasValue && x.DateFin.HasValue)
                .Select(x => new ConventionExistanteResume
                {
                    CpId = x.CpId,
                    Numero = x.Numero,
                    DateDebut = x.DateDebut!.Value,
                    DateFin = x.DateFin!.Value
                });

            var conflit = ConventionDelaiPaiementValidator.TrouverChevauchement(
                request.DateDebut.Value, request.DateFin.Value, resumes);

            if (conflit != null)
                throw new InvalidOperationException(
                    $"Il existe une convention pour la même période (convention [{conflit.Numero}], " +
                    $"{conflit.DateDebut:dd/MM/yyyy} - {conflit.DateFin:dd/MM/yyyy}).");
        }
        else
        {
            if (!request.FactureNo.HasValue)
                throw new ArgumentException("Le numéro de facture est obligatoire pour une convention de type Facture.", nameof(request.FactureNo));

            // Legacy l.523-524 : la facture référencée doit exister et être NonPayée.
            var existeFactureNonPayee = await _repository.EcheanceNonPayeeExisteAsync(
                request.SocieteId, request.TiersNo, request.Domaine, request.FactureNo.Value);
            if (!existeFactureNonPayee)
                throw new InvalidOperationException("Impossible de charger la facture (introuvable ou déjà payée).");

            // Legacy l.526-528 : une seule convention par facture.
            ConventionDelaiPaiementValidator.ValiderUniciteFacture(
                request.FactureNo.Value, conventionsExistantes.Select(x => x.FactureNo));
        }

        var entite = new ConventionDelaiPaiementTiers
        {
            SocieteId = request.SocieteId,
            TiersNo = request.TiersNo,
            TiersCode = request.TiersCode,
            Date = request.Date,
            Numero = request.Numero,
            DateDebut = request.DateDebut?.Date,
            DateFin = request.DateFin?.Date,
            FileName = request.FileName,
            File = request.File,
            NombreJoursDelaisPaiement = request.NombreJoursDelaisPaiement,
            Domaine = request.Domaine,
            Type = request.Type,
            FactureNo = request.FactureNo
        };

        return await _repository.CreateAsync(entite);
    }

    public Task<ConventionDelaiPaiementTiers?> GetAsync(int cpId) => _repository.GetAsync(cpId);

    public Task<IReadOnlyList<ConventionDelaiPaiementTiers>> GetAllAsync(int societeId, DomaineDelaiPaiement domaine)
        => _repository.GetAllAsync(societeId, domaine);

    public async Task TerminerAsync(int cpId, DateTime nouvelleDateFin)
    {
        var convention = await _repository.GetAsync(cpId)
            ?? throw new InvalidOperationException("Impossible de charger la convention.");

        // Legacy l.570-571 : la clôture anticipée ne s'applique qu'au type Convention.
        if (convention.Type != TypeConventionDelaiPaiement.Convention)
            throw new InvalidOperationException("Type convention invalide.");
        if (!convention.DateDebut.HasValue)
            throw new InvalidOperationException("La date début invalide.");
        if (!convention.DateFin.HasValue)
            throw new InvalidOperationException("La date fin invalide.");

        ConventionDelaiPaiementValidator.ValiderTerminer(convention.DateDebut.Value, convention.DateFin.Value, nouvelleDateFin);

        await _repository.UpdateDateFinAsync(cpId, nouvelleDateFin.Date);
    }

    /// <summary>
    /// Suppression DIRECTE, SANS GARDE — reproduit le legacy à l'identique (aucune vérification
    /// d'utilisation ailleurs). Décision assumée, documentée dans le VERIFY TASK-129 : on ne durcit
    /// pas un comportement non demandé.
    /// </summary>
    public Task DeleteAsync(int cpId) => _repository.DeleteAsync(cpId);

    // ─── TASK-130 (front) : pur passe-plat vers le repository, aucun métier additionnel ici ─────
    public Task<IReadOnlyList<ConventionDelaiPaiementListItem>> GetAllForListAsync(int societeId, DomaineDelaiPaiement domaine)
        => _repository.GetAllForListAsync(societeId, domaine);

    public Task<IReadOnlyList<FactureNonPayeeItem>> GetFacturesNonPayeesAsync(int societeId, int tiersNo, DomaineDelaiPaiement domaine)
        => _repository.GetFacturesNonPayeesAsync(societeId, tiersNo, domaine);

    public Task<IReadOnlyList<TiersRechercheItem>> SearchTiersAsync(int societeId, DomaineDelaiPaiement domaine, string? recherche)
        => _repository.SearchTiersAsync(societeId, domaine, recherche);
}
