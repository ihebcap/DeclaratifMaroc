using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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

    public DeclarationsController(IDeclarationRepository repository, DeclarationWorkflowService workflowService)
    {
        _repository = repository;
        _workflowService = workflowService;
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
    public async Task<IActionResult> GetAll([FromQuery] string societeId, [FromQuery] int? exercice, [FromQuery] StatutDeclaration? statut)
    {
        var declarations = await _repository.GetAllAsync(societeId, exercice, statut);
        return Ok(declarations);
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
    /// Aucun rejet silencieux.
    /// </summary>
    [HttpGet("{id}/lignes")]
    public async Task<IActionResult> GetLignes(Guid id, [FromQuery] string domaine, [FromQuery] int page = 1, [FromQuery] int size = 50, [FromQuery] string? sort = null, [FromQuery] string? filter = null)
    {
        await _workflowService.ChargerCandidatesSiNecessaireAsync(id, domaine);
        var lignes = await _repository.GetLignesAsync(id, domaine, page, size, sort, filter);
        var total = await _repository.GetLignesCountAsync(id, domaine, filter);

        return Ok(new
        {
            Items = lignes,
            TotalCount = total,
            Page = page,
            PageSize = size
        });
    }

    [HttpPatch("{id}/lignes/{ligneId}")]
    public async Task<IActionResult> UpdateLigneEtat(Guid id, Guid ligneId, [FromBody] UpdateEtatRequest request)
    {
        await _repository.UpdateLigneEtatAsync(ligneId, request.Etat);
        return NoContent();
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
            var lignes = await _repository.GetLignesAsync(id, "Decaissement", 1, int.MaxValue, null, null);
            var proposees = lignes.Count(l => l.Etat == EtatLigne.Proposee);
            var integrees = lignes.Count(l => l.Etat == EtatLigne.Integree);
            var exclues = lignes.Count(l => l.Etat == EtatLigne.Exclue);
            var reportees = lignes.Count(l => l.Etat == EtatLigne.Reportee);
            var ecartees = lignes.Count(l => l.Etat == EtatLigne.Ecartee);
            
            return Ok(new {
                ControleEquilibre = result.ControleEquilibre,
                Alertes = result.Alertes,
                Reconciliation = new {
                    Candidates = lignes.Count(),
                    Proposees = proposees,
                    Integrees = integrees,
                    Exclues = exclues,
                    Reportees = reportees,
                    Ecartees = ecartees
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
    [HttpPost("{id}/reouverture")]
    public async Task<IActionResult> Rouvrir(Guid id)
    {
        try
        {
            await _workflowService.ReouvriDeclarationAsync(id);
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
    public IActionResult GetSocietes()
        => Ok(new[] { new { Id = "001", Nom = "Société Test" } });

    [HttpGet("/api/controle")]
    public IActionResult GetControle([FromQuery] string societeId, [FromQuery] int periode)
        => StatusCode(501, new { Message = "Non implémenté — délégué à TASK-009 (Contrôle GRF-N)." });
}

public class CreateDeclarationRequest
{
    public string SocieteId { get; set; } = "";
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
