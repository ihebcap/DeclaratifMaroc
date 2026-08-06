using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Declaration.Application.Interfaces;
using Declaration.Core;

namespace Declaration.Application.Services;

/// <summary>
/// TASK-131 : résultat de la sélection des lignes hors délai pour UNE période de déclaration.
/// Sépare explicitement les lignes exploitables des lignes bloquées par le garde-fou de mise en
/// route (TASK-128) — aucune ligne n'est supprimée silencieusement.
/// </summary>
public sealed class ResultatSelectionDelaiPaiement
{
    public DateTime DateDebutPeriode { get; init; }
    public DateTime DateFinPeriode { get; init; }

    /// <summary>Date de mise en route de la société (TASK-128) ; null = société non encore configurée.</summary>
    public DateTime? DateMiseEnRouteSociete { get; init; }

    /// <summary>
    /// Lignes exploitables : <c>Depassement</c> incrémental calculé et strictement positif.
    /// Consommées par l'intégration (TASK-132).
    /// </summary>
    public IReadOnlyList<LigneSelectionDelaiPaiement> Lignes { get; init; } = Array.Empty<LigneSelectionDelaiPaiement>();

    /// <summary>
    /// Lignes signalées « Antérieure à la mise en route — retard réel inconnu » (TASK-128) : AUCUN
    /// <c>Depassement</c> calculé, à afficher dans l'écran de contrôle (TASK-134) mais JAMAIS
    /// intégrables tant que la reprise manuelle n'est pas saisie.
    /// </summary>
    public IReadOnlyList<LigneSelectionDelaiPaiement> LignesRepriseManuelleRequise { get; init; } = Array.Empty<LigneSelectionDelaiPaiement>();

    /// <summary>Nombre d'échéances lues après application des seuils légaux + devise société (traçabilité).</summary>
    public int NombreEcheancesExaminees { get; init; }

    /// <summary>
    /// AUDIT UX (correctif « faux zéro silencieux ») : nombre d'échéances candidates de cette période
    /// qui figurent DÉJÀ dans une déclaration antérieure (<c>RT_DECLARATIONDELAISPAIEMENTLG</c>). Leur
    /// retard a été compté une première fois et ne peut plus l'être : sans ce compteur, un écran vide
    /// se lit à tort « aucun retard » alors que la bonne lecture est « déjà déclaré ».
    /// </summary>
    public int NombreEcheancesDejaDeclarees { get; init; }

    /// <summary>
    /// Borne la plus récente déjà déclarée parmi ces échéances (max <c>DDP_DateFin</c>). <c>null</c>
    /// quand aucune échéance de la période n'a jamais été déclarée.
    /// </summary>
    public DateTime? DerniereBorneDejaDeclaree { get; init; }
}

/// <summary>
/// TASK-131 — Service de sélection des lignes hors délai de la Déclaration Délai de Paiement Maroc,
/// avec calcul INCRÉMENTAL anti-double-déclaration.
///
/// Rôle strictement limité à l'ORCHESTRATION des lectures (LECTURE SEULE) :
/// <list type="number">
/// <item>devise société + échéances candidates (seuils légaux appliqués côté SQL avec les constantes
/// de <see cref="SeuilsLegauxDelaiPaiement"/>) ;</item>
/// <item>affectations + règlements des échéances retenues ;</item>
/// <item>historique <c>RT_DECLARATIONDELAISPAIEMENTLG</c> (bornes déjà déclarées) ;</item>
/// <item>référentiel de délai (TASK-127, chargé UNE fois) puis échéance légale par échéance ;</item>
/// <item>garde-fou de mise en route + reprises manuelles (TASK-128, lus en lot).</item>
/// </list>
/// Tout le métier (3 cas de figure corrigés + calcul incrémental) vit dans le calculateur PUR
/// <see cref="SelectionDelaiPaiementCalculator"/> (Declaration.Core), testable hors base.
///
/// AUCUNE écriture : l'intégration des lignes dans <c>RT_DECLARATIONDELAISPAIEMENTLG</c> et le cycle
/// de vie de la déclaration relèvent de TASK-132 ; l'export XML/ZIP de TASK-133 ; l'UI de TASK-134.
/// </summary>
public interface ISelectionDelaiPaiementService
{
    /// <summary>
    /// Sélectionne les lignes candidates pour la période <paramref name="dateDebutPeriode"/> ..
    /// <paramref name="dateFinPeriode"/> (bornes d'une déclaration EnCours existante — jamais une
    /// plage libre, cf. TASK-134 ; c'est TASK-132 qui fournira ces bornes depuis
    /// <c>RT_DECLARATIONDELAISPAIEMENT</c>).
    /// </summary>
    Task<ResultatSelectionDelaiPaiement> SelectionnerAsync(int soId, DateTime dateDebutPeriode, DateTime dateFinPeriode);
}

/// <inheritdoc cref="ISelectionDelaiPaiementService"/>
public sealed class SelectionDelaiPaiementService : ISelectionDelaiPaiementService
{
    private readonly ISelectionDelaiPaiementRepository _selection;
    private readonly IDelaiPaiementService _delaiPaiement;
    private readonly IDelaiPaiementBootstrapService _bootstrap;

    public SelectionDelaiPaiementService(
        ISelectionDelaiPaiementRepository selection,
        IDelaiPaiementService delaiPaiement,
        IDelaiPaiementBootstrapService bootstrap)
    {
        _selection = selection ?? throw new ArgumentNullException(nameof(selection));
        _delaiPaiement = delaiPaiement ?? throw new ArgumentNullException(nameof(delaiPaiement));
        _bootstrap = bootstrap ?? throw new ArgumentNullException(nameof(bootstrap));
    }

    public async Task<ResultatSelectionDelaiPaiement> SelectionnerAsync(int soId, DateTime dateDebutPeriode, DateTime dateFinPeriode)
    {
        var dateDebut = dateDebutPeriode.Date;
        var dateFin = dateFinPeriode.Date;
        if (dateFin < dateDebut)
            throw new ArgumentException("Période invalide : la fin de période est antérieure au début de période.", nameof(dateFinPeriode));

        // ── 1. Échéances candidates (seuils légaux + devise société, appliqués côté SQL).
        var deviseSocieteId = await _selection.GetDeviseSocieteIdAsync(soId);
        var echeances = await _selection.GetEcheancesCandidatesAsync(
            soId,
            deviseSocieteId,
            SeuilsLegauxDelaiPaiement.DateDebutDeclarationLoi,
            SeuilsLegauxDelaiPaiement.DateLimiteSeuilMontant,
            SeuilsLegauxDelaiPaiement.SeuilMontant);

        if (echeances.Count == 0)
        {
            return new ResultatSelectionDelaiPaiement
            {
                DateDebutPeriode = dateDebut,
                DateFinPeriode = dateFin,
                DateMiseEnRouteSociete = await _bootstrap.GetDateMiseEnRouteAsync(soId),
                NombreEcheancesExaminees = 0
            };
        }

        var ecIds = echeances.Select(e => e.EcId).ToList();

        // ── 2. Affectations + règlements, et historique des déclarations (anti-double-déclaration).
        var affectations = await _selection.GetAffectationsAsync(soId, ecIds);
        var dernieresBornes = await _selection.GetDernieresBornesDeclareesAsync(soId, ecIds);

        // ── 3. Échéance légale par échéance (TASK-127), référentiel chargé UNE SEULE FOIS.
        // La DDP ne porte que le domaine Achat (factures fournisseur), comme le legacy.
        var contexte = await _delaiPaiement.ChargerContexteAsync(soId, DomaineDelaiPaiement.Achat);
        var echeancesLegales = echeances.ToDictionary(
            e => e.EcId,
            e => contexte.Resoudre(e.DoDate, e.TiersNo, e.DoNumero));

        // ── 4. Garde-fou de mise en route + reprises manuelles (TASK-128), lus en lot.
        var dateMiseEnRoute = await _bootstrap.GetDateMiseEnRouteAsync(soId);
        var reprises = await _bootstrap.GetToutesReprisesAsync(soId);
        var reprisesParEcheance = reprises
            .GroupBy(r => r.EcId)
            .ToDictionary(g => g.Key, g => g.Max(r => r.DateDejaDeclareeJusquau));

        // ── 5. Métier PUR : 3 cas de figure corrigés + calcul incrémental.
        var lignes = SelectionDelaiPaiementCalculator.Selectionner(new ParametresSelectionDelaiPaiement
        {
            DateDebutPeriode = dateDebut,
            DateFinPeriode = dateFin,
            Echeances = echeances,
            Affectations = affectations,
            EcheancesLegales = echeancesLegales,
            DernieresBornesDeclarees = dernieresBornes,
            DateMiseEnRouteSociete = dateMiseEnRoute,
            ReprisesManuelles = reprisesParEcheance
        });

        return new ResultatSelectionDelaiPaiement
        {
            DateDebutPeriode = dateDebut,
            DateFinPeriode = dateFin,
            DateMiseEnRouteSociete = dateMiseEnRoute,
            Lignes = lignes.Where(l => l.Statut == StatutLigneDelaiPaiement.Candidate).ToList(),
            LignesRepriseManuelleRequise = lignes.Where(l => l.Statut == StatutLigneDelaiPaiement.RepriseManuelleRequise).ToList(),
            NombreEcheancesExaminees = echeances.Count,
            // Traçabilité du calcul incrémental : combien d'échéances de cette période ont DÉJÀ été
            // déclarées (leur retard ne peut plus être compté) et jusqu'à quelle date.
            NombreEcheancesDejaDeclarees = dernieresBornes.Count,
            DerniereBorneDejaDeclaree = dernieresBornes.Count == 0 ? null : dernieresBornes.Values.Max()
        };
    }
}
