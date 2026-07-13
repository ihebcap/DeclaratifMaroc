using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Declaration.API.Dtos;
using Declaration.Application.Entities;
using Declaration.Application.Interfaces;
using Declaration.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Declaration.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DeclarationsController : ControllerBase
{
    private readonly IDeclarationRepository _repository;
    private readonly DeclarationWorkflowService _workflowService;
    private readonly IDbConnectionFactory _connectionFactory;

    public DeclarationsController(IDeclarationRepository repository, DeclarationWorkflowService workflowService, IDbConnectionFactory connectionFactory)
    {
        _repository = repository;
        _workflowService = workflowService;
        _connectionFactory = connectionFactory;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateDeclarationRequest request)
    {
        try
        {
            var declaration = await _workflowService.CreerDeclarationAsync(request.SocieteId, request.Exercice, request.Periode, request.Type);
            return CreatedAtAction(nameof(GetById), new { id = declaration.Id }, declaration);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { Message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int societeId, [FromQuery] int? exercice, [FromQuery] StatutDeclaration? statut)
    {
        if (societeId <= 0)
            return BadRequest(new { Message = "'societeId' est obligatoire." });

        // TASK-074 périmètre C : un utilisateur non-admin ne peut interroger que les sociétés
        // listées dans son token (P_SOCUTILISATEUR, posées au login) — sans cette garde,
        // societeId en query string donnait accès à n'importe quelle société.
        if (!EstSocieteAutorisee(societeId))
            return Forbid();

        var declarations = (await _repository.GetAllAsync(societeId, exercice, statut)).ToList();

        // TASK-079 : enrichissement Lignes/Montant TVA de l'écran liste — une seule requête
        // d'agrégats pour toutes les déclarations retournées (pas de N+1).
        var agregats = await _repository.GetAgregatsListeAsync(declarations.Select(d => d.Id));

        return Ok(declarations.Select(d =>
        {
            var a = agregats.TryGetValue(d.Id, out var ag) ? ag : (NbLignes: 0, MontantTva: 0m);
            return new
            {
                d.Id,
                d.Numero,
                d.SocieteId,
                d.Exercice,
                d.Type,
                d.Periode,
                d.Statut,
                d.DateCreation,
                d.DateCloture,
                NbLignes = a.NbLignes,
                MontantTva = a.MontantTva
            };
        }));
    }

    /// <summary>
    /// TASK-074 : vrai pour UT_Admin=1 (toutes sociétés) ou si societeId figure dans le claim
    /// "Societes" (CSV des SO_Id autorisés, posé au login depuis P_SOCUTILISATEUR).
    /// </summary>
    private bool EstSocieteAutorisee(int societeId)
    {
        if (User.HasClaim("UT_Admin", "1")) return true;

        var societes = User.FindFirst("Societes")?.Value ?? "";
        return societes.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Any(s => int.TryParse(s, out var id) && id == societeId);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var declaration = await _repository.GetByIdAsync(id);
        if (declaration == null) return NotFound();
        return Ok(declaration);
    }

    /// <summary>
    /// Retourne les lignes candidates d'un domaine, en figeant au premier appel.
    /// Toutes les lignes (éligibles Proposee ET rejetées Exclue+MotifRejet) sont retournées.
    /// Aucun rejet silencieux. Depuis TASK-067B, la réponse inclut aussi <c>Distincts</c> —
    /// les valeurs distinctes des colonnes énumérables (numeroRapprochement, source, tauxTVA,
    /// origine, statutLigne), calculées sur le MÊME WHERE (DeclarationId + Domaine) que la liste
    /// et le total (invariant TASK-040), pour alimenter DomainGrid en filterType 'list' sans
    /// jamais proposer une valeur absente du jeu réel de la déclaration.
    /// </summary>
    [HttpGet("{id}/lignes")]
    public async Task<IActionResult> GetLignes(Guid id, [FromQuery] string domaine, [FromQuery] int page = 1, [FromQuery] int size = 50, [FromQuery] string? sort = null, [FromQuery] string? filter = null)
    {
        // TASK-077 : ChargerCandidatesSiNecessaireAsync revalide désormais AUSSI les lignes déjà
        // figées à chaque appel (sauf déclaration clôturée) et remonte d'éventuelles alertes
        // LIGNE_FIGEE_A_REVERIFIER — signalement seul, aucune ligne/total modifié. Même contrat
        // d'alerte (Niveau/Code/Message/RefLigne) que GetCheckup (TASK-060), pour rester cohérent
        // avec l'existant plutôt qu'un nouveau format.
        var alertesRevalidation = await _workflowService.ChargerCandidatesSiNecessaireAsync(id, domaine);
        var lignes = await _repository.GetLignesAsync(id, domaine, page, size, sort, filter);
        var total = await _repository.GetLignesCountAsync(id, domaine, filter);
        var distincts = await _repository.GetLignesDistinctsAsync(id, domaine);

        // TASK-034 : projection pure vers un DTO au contrat JSON explicite (clés alignées
        // sur le front). Lecture seule, aucun effet de bord ni recalcul.
        return Ok(new
        {
            Items = lignes.Select(l => new LigneCandidateDto(l)),
            TotalCount = total,
            Page = page,
            PageSize = size,
            Distincts = distincts,
            Alertes = alertesRevalidation.Select(a => new
            {
                type = a.Niveau == Declaration.Core.Model.NiveauAlerte.Error ? "bloquant" : "avertissement",
                message = a.Message,
                code = a.Code,
                refLigne = a.RefLigne
            })
        });
    }

    [HttpPatch("{id}/lignes/{ligneId}")]
    public async Task<IActionResult> UpdateLigneEtat(Guid id, Guid ligneId, [FromBody] UpdateEtatRequest request)
    {
        await _repository.UpdateLigneEtatAsync(ligneId, request.Etat);
        return NoContent();
    }

    /// <summary>
    /// TASK-078 : valide explicitement une incohérence déjà signalée (TASK-077) — décision PO
    /// tracée (qui/quand), n'écrit AUCUN montant/état de ligne. L'alerte correspondante ne sera
    /// plus remontée pour cette pièce (EC_Id) tant qu'une resynchronisation ne l'invalide pas.
    /// </summary>
    [HttpPost("{id}/lignes/valider-incoherence")]
    public async Task<IActionResult> ValiderIncoherence(Guid id, [FromBody] ValiderIncoherenceRequest request)
    {
        if (request.EcId <= 0)
            return BadRequest(new { Message = "'ecId' est obligatoire." });
        var utilisateur = User.Identity?.Name ?? "inconnu";
        await _workflowService.ValiderIncoherenceLigneAsync(id, request.EcId, utilisateur);
        return NoContent();
    }

    /// <summary>
    /// TASK-078 : resynchronise UNE pièce Sage (EC_Id) après correction côté ERP — relit
    /// explicitement l'OM (effet de bord : réécrit le cache de ventilation, même pipeline que
    /// TASK-072/076/077) et réinitialise une éventuelle validation antérieure. Retourne l'état
    /// résultant (toujours incohérent ou résolu) pour affichage immédiat.
    /// </summary>
    [HttpPost("{id}/lignes/resynchroniser")]
    public async Task<IActionResult> Resynchroniser(Guid id, [FromBody] ValiderIncoherenceRequest request)
    {
        if (request.EcId <= 0)
            return BadRequest(new { Message = "'ecId' est obligatoire." });
        var (trouvee, resolue) = await _workflowService.ResynchroniserLigneAsync(id, request.EcId);
        if (!trouvee)
            return NotFound(new { Message = $"Aucune ligne trouvée pour EC_Id={request.EcId} sur cette déclaration." });
        return Ok(new { Resolue = resolue });
    }

    [HttpPost("{id}/lignes:bulk")]
    public async Task<IActionResult> UpdateLignesBulk(Guid id, [FromBody] BulkUpdateEtatRequest request)
    {
        if (request.LigneIds != null && request.LigneIds.Count > 0)
            await _repository.UpdateLignesEtatBulkByIdsAsync(request.LigneIds, request.Etat);
        else if (!string.IsNullOrEmpty(request.Domaine))
            await _repository.UpdateLignesEtatBulkAsync(id, request.Domaine, request.Filter, request.Etat);
        else
            return BadRequest("Soit LigneIds soit Domaine doit être renseigné.");
        return NoContent();
    }

    /// <summary>
    /// Vérifie la cohérence de la déclaration avant clôture.
    /// Retourne les alertes (Info/Warning/Error) + contrôle d'équilibre réels.
    /// Les alertes Error bloquent la clôture.
    /// </summary>
    [HttpGet("{id}/checkup")]
    public async Task<IActionResult> GetCheckup(Guid id)
    {
        try
        {
            var result = await _workflowService.GetCheckupAsync(id);
            var lignes = (await _repository.GetLignesAsync(id, "Decaissement", 1, int.MaxValue, null, null)).ToList();
            var proposees = lignes.Count(l => l.Etat == EtatLigne.Proposee);
            var integreesCount = lignes.Count(l => l.Etat == EtatLigne.Integree);
            var exclues = lignes.Count(l => l.Etat == EtatLigne.Exclue);
            var reportees = lignes.Count(l => l.Etat == EtatLigne.Reportee);
            var ecartees = lignes.Count(l => l.Etat == EtatLigne.Ecartee);

            // TASK-058 : projection de contrôle consommée par l'écran ⑤ (front lecture seule).
            // Aucun recalcul TVA — simple agrégation des lignes INTÉGRÉES déjà valorisées par
            // le recalcul back (TASK-009). Les clés JSON sont alignées sur le contrat front
            // (recapSource / recapTaux / alertes typées / equilibre).
            var integrees = lignes.Where(l => l.Etat == EtatLigne.Integree).ToList();

            var recapSource = integrees
                .GroupBy(l => string.IsNullOrWhiteSpace(l.Source) ? "—" : l.Source)
                .Select(g => new
                {
                    source = g.Key,
                    ht = g.Sum(x => x.HT),
                    tva = g.Sum(x => x.TVA),
                    ttc = g.Sum(x => x.TTC)
                })
                .OrderByDescending(x => x.ttc)
                .ToList();

            var recapTaux = integrees
                .GroupBy(l => l.Taux)
                .Select(g => new
                {
                    taux = g.Key,
                    ht = g.Sum(x => x.HT),
                    tva = g.Sum(x => x.TVA),
                    ttc = g.Sum(x => x.TTC)
                })
                .OrderByDescending(x => x.taux)
                .ToList();

            // Équilibre : dérivé du contrôle back (TotalDeclareTtc vs HT + TVA des intégrées).
            // Écart ~0 = déclaration équilibrée. Tolérance d'arrondi 0,01.
            var totalTva = integrees.Sum(l => l.TVA);
            var ecart = result.ControleEquilibre.TotalDeclareTtc
                        - (result.ControleEquilibre.TotalMontantAffecte + totalTva);
            var equilibre = new { isValid = Math.Abs(ecart) < 0.01m, ecart };

            // Alertes typées : Niveau back → type front (Error → bloquant, Warning/Info →
            // avertissement, aucune masquée). Drill câblé sur numeroRapprochement (seule clé
            // supportée par le filtre JSON du repo, TASK-034) retrouvé via la ligne référencée.
            var alertes = result.Alertes.Select(a =>
            {
                var ligne = lignes.FirstOrDefault(l => l.NumeroFacture == a.RefLigne);
                var hasDrill = ligne != null && !string.IsNullOrWhiteSpace(ligne.NumeroRapprochement);
                return new
                {
                    type = a.Niveau == Declaration.Core.Model.NiveauAlerte.Error ? "bloquant" : "avertissement",
                    message = a.Message,
                    code = a.Code,
                    refLigne = a.RefLigne,
                    domaine = hasDrill ? ligne!.Domaine : null,
                    filtre = hasDrill ? (object)new { numeroRapprochement = ligne!.NumeroRapprochement } : null
                };
            }).ToList();

            return Ok(new {
                equilibre,
                recapSource,
                recapTaux,
                alertes,
                controleEquilibre = result.ControleEquilibre,
                reconciliation = new {
                    candidates = lignes.Count,
                    proposees,
                    integrees = integreesCount,
                    exclues,
                    reportees,
                    ecartees
                }
            });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
    }

    [HttpPost("{id}/cloture")]
    public async Task<IActionResult> Cloturer(Guid id)
    {
        try
        {
            await _workflowService.CloturerDeclarationAsync(id);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    /// <summary>
    /// Réouvre une déclaration clôturée : remet le statut à EnCours et efface
    /// le tampon DT_Id sur les affectations (TASK-028).
    /// </summary>
    // TASK-073 périmètre A : réservé aux utilisateurs UT_Admin=1 (décision produit PO 13/07/2026).
    [HttpPost("{id}/reouverture")]
    public async Task<IActionResult> Rouvrir(Guid id)
    {
        if (!User.HasClaim("UT_Admin", "1"))
            return Forbid();

        try
        {
            var utilisateur = User.Identity?.Name ?? "inconnu";
            await _workflowService.ReouvriDeclarationAsync(id, utilisateur);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
    }

    /// <summary>
    /// TASK-079 : suppression physique d'une déclaration EnCours (entête DM_ENTTVA + lignes
    /// DM_LGTVA). Réservé UT_Admin=1 (même garde que la réouverture, TASK-073), journalisé via
    /// JournaliserAudit. Une déclaration Cloturee doit d'abord être rouverte (TASK-073) : pas de
    /// suppression directe pour ne pas contourner sa traçabilité.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Supprimer(Guid id)
    {
        if (!User.HasClaim("UT_Admin", "1"))
            return Forbid();

        try
        {
            var utilisateur = User.Identity?.Name ?? "inconnu";
            await _workflowService.SupprimerDeclarationAsync(id, utilisateur);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
    }

    // ─── Endpoints délégués à TASK-010/011 ────────────────────────────────────
    // Ces endpoints sont présents dans le contrat d'API mais leur implémentation
    // (Export.Xml / Export.Excel / Rapport anomalies) est délivrée par TASK-010/011.
    // Ils renvoient 501 Not Implemented pour l'instant.

    [HttpPost("{id}/generation")]
    public IActionResult Generation(Guid id)
        => StatusCode(501, new { Message = "Non implémenté — délégué à TASK-010/011 (Export.Xml + Export.Excel)." });

    [HttpGet("{id}/fichiers/{type}")]
    public IActionResult DownloadFichier(Guid id, string type)
        => StatusCode(501, new { Message = "Non implémenté — délégué à TASK-010/011." });

    // ─── Endpoints référentiels ────────────────────────────────────────────────

    [HttpGet("/api/societes")]
    [AllowAnonymous]
    public async Task<IActionResult> GetSocietes()
    {
        using var connection = _connectionFactory.CreateGrfConnection();
        var sql = "SELECT SO_Id AS SoId, SO_RaisonSocial AS RaisonSociale FROM P_SOCIETE ORDER BY SO_RaisonSocial";
        var societes = await connection.QueryAsync<SocieteDto>(sql);
        return Ok(societes);
    }

    [HttpGet("/api/controle")]
    public IActionResult GetControle([FromQuery] int societeId, [FromQuery] int periode)
        => StatusCode(501, new { Message = "Non implémenté — délégué à TASK-009 (Contrôle GRF-N)." });
}

public class CreateDeclarationRequest
{
    public int SocieteId { get; set; }
    public int Exercice { get; set; }
    public int Periode { get; set; }
    public TypePeriode Type { get; set; }
}

public class UpdateEtatRequest
{
    public EtatLigne Etat { get; set; }
}

public class BulkUpdateEtatRequest
{
    public EtatLigne Etat { get; set; }
    public List<Guid>? LigneIds { get; set; }
    public string? Domaine { get; set; }
    public string? Filter { get; set; }
}

/// <summary>TASK-078 — cible une pièce Sage (EC_Id) pour valider/resynchroniser une incohérence.</summary>
public class ValiderIncoherenceRequest
{
    public int EcId { get; set; }
}

public class SocieteDto
{
    public int SoId { get; set; }
    public string RaisonSociale { get; set; } = "";
}

