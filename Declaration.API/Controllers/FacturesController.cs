using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Declaration.API.Dtos;
using Declaration.Application.Entities;
using Declaration.Application.Interfaces;
using Declaration.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Declaration.API.Controllers;

/// <summary>
/// Interrogation « Factures » (TASK-041). Deuxième entrée du menu INTERROGATION, pivot FACTURE.
///
/// Endpoint HTTP LECTURE SEULE : projette la famille A (identité facture, RT_ECHEANCE), la famille C
/// (agrégat RT_AFFECTATION : Réglé / Déclaré = Σ affecté DT_Id non nul / Reste à déclarer / statut à
/// 3 valeurs dérivé), et la famille B (HT/TVA depuis le cache de ventilation TASK-024 quand présent,
/// sinon « non valorisé » + motif — aucun appel Sage synchrone de masse, aucune valeur forfaitaire).
///
/// Période bornée OBLIGATOIRE (perf : borne la valorisation famille B). DT_Id lu, jamais modifié
/// (TASK-028). Aucune écriture.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FacturesController : ControllerBase
{
    private readonly IDeclarationRepository _repository;
    private readonly Declaration.Application.Services.DeclarationWorkflowService _workflowService;

    // TASK-135 (CDC §3.3) : socle TASK-127 consommé DIRECTEMENT (calculateur pur
    // EcheanceLegaleCalculator + repositories), plutôt que via IDelaiPaiementService — pour
    // charger conventions/jours de repos/délai par défaut UNE SEULE FOIS par page au lieu d'une
    // fois par facture (IDelaiPaiementService est pensé pour une résolution facture par facture,
    // adapté à TASK-131/134, pas à une grille paginée). Aucune logique métier dupliquée : le
    // calcul reste délégué au même calculateur statique.
    //
    // ATTENTION CÂBLAGE DI (cf. VERIFY/TASK-127_verify.md « Reste à valider ») :
    // IConventionDelaiPaiementRepository est un contrat SEUL (implémentation = TASK-129, non
    // livrée à ce jour) — tant que Program.cs n'enregistre pas ces trois interfaces (TASK-129),
    // CE CONTRÔLEUR ENTIER échouera à la résolution DI (toutes ses actions, pas seulement
    // GetFactures). Régression temporaire assumée et documentée (VERIFY/TASK-135_verify.md) —
    // décision explicite du donneur d'ordre (ne pas bloquer TASK-135 sur TASK-129, ne pas
    // implémenter TASK-129 depuis cette session).
    private readonly IConventionDelaiPaiementRepository _conventionsDelaiPaiement;
    private readonly IJoursReposRepository _joursRepos;
    private readonly IDelaiPaiementParametrageRepository _delaiPaiementParametrage;

    public FacturesController(
        IDeclarationRepository repository,
        Declaration.Application.Services.DeclarationWorkflowService workflowService,
        IConventionDelaiPaiementRepository conventionsDelaiPaiement,
        IJoursReposRepository joursRepos,
        IDelaiPaiementParametrageRepository delaiPaiementParametrage)
    {
        _repository = repository;
        _workflowService = workflowService;
        _conventionsDelaiPaiement = conventionsDelaiPaiement;
        _joursRepos = joursRepos;
        _delaiPaiementParametrage = delaiPaiementParametrage;
    }

    /// <summary>
    /// TASK-135 (CDC §3.3) : renseigne Échéance légale + Écart (jours) sur chaque ligne de la
    /// page — conventions (Achat/Fournisseur), jours de repos et délai par défaut société chargés
    /// UNE SEULE FOIS pour toute la page, puis <see cref="EcheanceLegaleCalculator.Calculer"/>
    /// (Declaration.Core, hors DB) appliqué ligne par ligne en mémoire. La dernière date de
    /// rapprochement bancaire pertinente (nécessaire uniquement pour les factures soldées) est
    /// lue en un seul aller-retour batché. Indicateur de pilotage interne, jamais lié au workflow
    /// DDP (TASK-131) ni source de vérité réglementaire.
    /// </summary>
    private async Task AppliquerDelaiPaiementAsync(int soId, List<FactureInterrogationRow> rows)
    {
        if (rows.Count == 0) return;

        var conventions = await _conventionsDelaiPaiement.GetConventionsActivesAsync(soId, DomaineDelaiPaiement.Achat);
        var joursRepos = await _joursRepos.GetJoursReposAsync(soId);
        var nombreJoursDefaut = await _delaiPaiementParametrage.GetNombreJoursDelaiDefautAsync(soId);

        // Dernière date de rapprochement bancaire pertinente : uniquement nécessaire pour les
        // factures soldées (solde restant ≤ 0) — seul cas où l'écart se mesure contre un fait déjà
        // survenu (cf. FactureInterrogationRow.AppliquerDelaiPaiement).
        var ecIdsSoldees = rows.Where(r => r.SoldeFacture <= 0m).Select(r => r.EcId).ToList();
        var rapprochements = ecIdsSoldees.Count > 0
            ? await _repository.GetDernieresDatesRapprochementAsync(soId, ecIdsSoldees)
            : new Dictionary<int, DateTime?>();

        foreach (var r in rows)
        {
            var resultat = EcheanceLegaleCalculator.Calculer(
                r.DoDate, r.TiersNo, r.DoNumero, conventions, nombreJoursDefaut, joursRepos);
            rapprochements.TryGetValue(r.EcId, out var derniereDateRapprochement);
            r.AppliquerDelaiPaiement(resultat.EcheanceLegale, derniereDateRapprochement);
        }
    }

    /// <summary>
    /// Garde-fou perte de saisie (ex. input date natif édité chiffre par chiffre, année
    /// transitoire "0014") : borne debut/fin au minimum représentable par SQL Server DATETIME
    /// (1753-01-01), sinon SqlTypeException non gérée jusqu'au client (crash 500 brut observé
    /// sur l'écran Factures). Retourne un message explicite plutôt qu'une exception SQL opaque.
    /// </summary>
    private static bool PeriodeValide(DateTime debut, DateTime fin, out string? erreur)
    {
        var min = new DateTime(1753, 1, 1);
        if (debut < min || fin < min)
        {
            erreur = "Les dates 'debut'/'fin' doivent être postérieures au 01/01/1753 (plage SQL Server valide).";
            return false;
        }
        erreur = null;
        return true;
    }

    /// <summary>
    /// Liste paginée des factures fournisseur sur une période bornée (obligatoire).
    /// Filtres optionnels : numero (LIKE), fournisseur (LIKE code/intitulé), reference (LIKE),
    /// origine[] (EC_Type dérivé), statut[] (NonDeclarable|Partiel|Total, dérivé DT_Id) — appliqués
    /// côté serveur au MÊME WHERE que le COUNT (leçon TASK-040 : compteur cohérent quelle que soit la page).
    /// Tri : date_desc (défaut) | date_asc | ttc_desc | ttc_asc | solde_desc | solde_asc | numero_asc | numero_desc.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetFactures(
        [FromQuery] DateTime? debut,
        [FromQuery] DateTime? fin,
        [FromQuery] int soId,
        // TASK-067B : n° facture / référence en multi-sélection réelle (valeurs distinctes
        // exactes, cf. GET /factures/distincts), fin du LIKE scalaire. Fournisseur reste en LIKE
        // (texte libre, cardinalité non bornée — décision documentée en VERIFY).
        [FromQuery] string[]? numero = null,
        [FromQuery] string? fournisseur = null,
        [FromQuery] string[]? reference = null,
        [FromQuery] string[]? origine = null,
        [FromQuery] string[]? statut = null,
        [FromQuery] int page = 1,
        [FromQuery] int size = 50,
        [FromQuery] string? sort = null)
    {
        if (soId <= 0)
            return BadRequest(new { Message = "'soId' est obligatoire." });
        // Période bornée OBLIGATOIRE : borne la famille B, évite tout scan intégral non borné.
        if (!debut.HasValue || !fin.HasValue)
            return BadRequest(new { Message = "Les bornes de période 'debut' et 'fin' sont obligatoires." });
        if (fin.Value.Date < debut.Value.Date)
            return BadRequest(new { Message = "'fin' doit être postérieure ou égale à 'debut'." });
        if (!PeriodeValide(debut.Value, fin.Value, out var erreurPeriode))
            return BadRequest(new { Message = erreurPeriode });

        var rows = (await _repository.GetFacturesInterrogationAsync(
            soId, debut.Value, fin.Value, numero, fournisseur, reference, origine, statut, page, size, sort)).ToList();
        var total = await _repository.GetFacturesInterrogationCountAsync(
            soId, debut.Value, fin.Value, numero, fournisseur, reference, origine, statut);

        // TASK-135 (CDC §3.3) : extension de la projection — échéance légale + écart (jours).
        await AppliquerDelaiPaiementAsync(soId, rows);

        return Ok(new
        {
            Items = rows.Select(r => new FactureInterrogationDto(r)),
            TotalCount = total,
            Page = page,
            PageSize = size
        });
    }

    /// <summary>
    /// Valeurs distinctes (origines EC_Type) présentes sur la période, pour alimenter le filtre liste.
    /// Le statut est un domaine fixe à 3 valeurs (front). Depuis TASK-067B, expose également les
    /// numéros de facture et références distincts (colonnes passées en filterType 'list'), calculés
    /// sur le MÊME WHERE/période que la liste principale (invariant TASK-040). Lecture seule.
    /// TASK-168 : <paramref name="rechercheNumero"/>/<paramref name="rechercheReference"/> optionnels
    /// rendent la recherche exhaustive au-delà du plafond (`numeroTronque`/`referenceTronque` signale
    /// une troncature de la vue par défaut, jamais silencieuse).
    /// </summary>
    [HttpGet("distincts")]
    public async Task<IActionResult> GetDistincts(
        [FromQuery] DateTime? debut,
        [FromQuery] DateTime? fin,
        [FromQuery] int soId,
        [FromQuery] string? rechercheNumero = null,
        [FromQuery] string? rechercheReference = null)
    {
        if (soId <= 0)
            return BadRequest(new { Message = "'soId' est obligatoire." });
        if (!debut.HasValue || !fin.HasValue)
            return BadRequest(new { Message = "Les bornes de période 'debut' et 'fin' sont obligatoires." });
        if (!PeriodeValide(debut.Value, fin.Value, out var erreurPeriode))
            return BadRequest(new { Message = erreurPeriode });

        var d = await _repository.GetFacturesInterrogationDistinctsAsync(
            soId, debut.Value, fin.Value, rechercheNumero, rechercheReference);

        return Ok(new
        {
            Origines = d.Origines.Select(t => new { code = t, libelle = ReglementRapprochementRow.LibelleEcType(t) }),
            Numero = d.Numeros,
            Reference = d.References,
            NumeroTronque = d.NumerosTronque,
            ReferenceTronque = d.ReferencesTronque
        });
    }

    /// <summary>
    /// Rafraîchit la valorisation TVA (famille B) des factures de la période affichée : lit les OM
    /// Sage et remplit le cache de ventilation (TASK-024). Opération explicite, déclenchée par le
    /// bouton « Rafraîchir » de la liste — synchrone et potentiellement longue (lecture COM Sage).
    /// Aucune écriture de déclaration, aucun DT_Id modifié. Après succès, le front recharge la liste.
    ///
    /// TASK-156 : un second appel concurrent pour le MÊME soId (clics rapprochés sur le bouton
    /// « Rafraîchir ») est rejeté immédiatement par le service (verrou par soId, décision PO
    /// 23/07/2026 — pas d'attente silencieuse) — remonté ici en 409 Conflict avec un message
    /// explicite, même pattern que les autres garde-fous métier (cf. DeclarationsController).
    /// </summary>
    [HttpPost("rafraichir-valorisation")]
    public async Task<IActionResult> RafraichirValorisation(
        [FromQuery] DateTime? debut,
        [FromQuery] DateTime? fin,
        [FromQuery] int soId)
    {
        if (soId <= 0)
            return BadRequest(new { Message = "'soId' est obligatoire." });
        if (!debut.HasValue || !fin.HasValue)
            return BadRequest(new { Message = "Les bornes de période 'debut' et 'fin' sont obligatoires." });
        if (fin.Value.Date < debut.Value.Date)
            return BadRequest(new { Message = "'fin' doit être postérieure ou égale à 'debut'." });
        if (!PeriodeValide(debut.Value, fin.Value, out var erreurPeriode))
            return BadRequest(new { Message = erreurPeriode });

        try
        {
            var rapport = await _workflowService.RafraichirValorisationAsync(soId, debut.Value, fin.Value);

            return Ok(new
            {
                FacturesTraitees = rapport.FacturesTraitees,
                NbErreurs = rapport.Erreurs.Count,
                Erreurs = rapport.Erreurs
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { Message = ex.Message });
        }
    }
}
