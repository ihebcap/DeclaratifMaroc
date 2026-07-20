using System;
using System.Linq;
using System.Threading.Tasks;
using Declaration.API.Dtos;
using Declaration.Application.Entities;
using Declaration.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Declaration.API.Controllers;

/// <summary>
/// Interrogation « Rapprochement bancaire » globale (TASK-036).
///
/// Endpoint HTTP LECTURE SEULE, INDÉPENDANT de toute déclaration : il expose les
/// règlements (pivot RT_MOUVEMENT.MV_Id) avec leur état de rapprochement banque
/// (MV_Point), leurs affectations agrégées + reste à affecter explicite, leur origine
/// (EC_Type) et leur état déclaré (DT_Id lu, jamais modifié — TASK-028).
///
/// Aucune écriture, aucun appel de DLL métier. Route dédiée hors /declarations/{id}.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RapprochementController : ControllerBase
{
    private readonly IDeclarationRepository _repository;

    public RapprochementController(IDeclarationRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Liste paginée des règlements sur une période bornée (obligatoire, perf).
    /// Filtres optionnels : mode (MV_Type), rapprocheBanque, declare, tiers (texte),
    /// numero (LIKE), origine[] et domaine[] (multi-sélection — TASK-040 : appliqués côté
    /// serveur, donc reflétés dans TotalCount et la pagination, quelle que soit la page).
    /// Tri : date_desc (défaut) | date_asc | montant_desc | montant_asc | reste_desc | reste_asc.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetReglements(
        [FromQuery] DateTime? debut,
        [FromQuery] DateTime? fin,
        [FromQuery] int soId,
        [FromQuery] Guid? declarationId = null,
        [FromQuery] int[]? mode = null,
        [FromQuery] bool[]? rapprocheBanque = null,
        [FromQuery] bool[]? declare = null,
        [FromQuery] bool[]? point = null,
        [FromQuery] string? tiers = null,
        // TASK-067B : n° règlement / n° extrait / code banque en multi-sélection réelle (valeurs
        // distinctes exactes, cf. GET /rapprochement/distincts), fin du LIKE scalaire.
        [FromQuery] string[]? numero = null,
        [FromQuery] string[]? numeroExtrait = null,
        [FromQuery] string[]? banque = null,
        [FromQuery] string[]? origine = null,
        [FromQuery] string[]? domaine = null,
        [FromQuery] decimal? montantMin = null,
        [FromQuery] decimal? montantMax = null,
        [FromQuery] decimal? resteMin = null,
        [FromQuery] decimal? resteMax = null,
        [FromQuery] int? nbFacturesMin = null,
        [FromQuery] int? nbFacturesMax = null,
        [FromQuery] DateTime? dateRappMin = null,
        [FromQuery] DateTime? dateRappMax = null,
        [FromQuery] DateTime? echeanceMin = null,
        [FromQuery] DateTime? echeanceMax = null,
        [FromQuery] int page = 1,
        [FromQuery] int size = 50,
        [FromQuery] string? sort = null)
    {
        if (soId <= 0)
            return BadRequest(new { Message = "'soId' est obligatoire." });
        // Période bornée OBLIGATOIRE : pas de scan intégral non borné.
        if (!debut.HasValue || !fin.HasValue)
            return BadRequest(new { Message = "Les bornes de période 'debut' et 'fin' sont obligatoires." });
        if (fin.Value.Date < debut.Value.Date)
            return BadRequest(new { Message = "'fin' doit être postérieure ou égale à 'debut'." });

        // Filtres par type de donnée (TASK-063), tous appliqués côté serveur au MÊME WHERE
        // (liste + COUNT) : multi-sélection réelle pour les énumérations/booléens, plages NULL-safe.
        var filter = new RapprochementFilter
        {
            DeclarationId = declarationId,
            Modes = mode,
            RapprocheBanque = rapprocheBanque,
            Declare = declare,
            Point = point,
            Tiers = tiers,
            Numeros = numero,
            NumerosExtrait = numeroExtrait,
            Banques = banque,
            Origines = origine,
            Domaines = domaine,
            MontantMin = montantMin,
            MontantMax = montantMax,
            ResteMin = resteMin,
            ResteMax = resteMax,
            NbFacturesMin = nbFacturesMin,
            NbFacturesMax = nbFacturesMax,
            DateRappMin = dateRappMin,
            DateRappMax = dateRappMax,
            EcheanceMin = echeanceMin,
            EcheanceMax = echeanceMax
        };

        var rows = await _repository.GetReglementsRapprochementAsync(
            soId, debut.Value, fin.Value, filter, page, size, sort);
        var total = await _repository.GetReglementsRapprochementCountAsync(
            soId, debut.Value, fin.Value, filter);

        return Ok(new
        {
            Items = rows.Select(r => new ReglementRapprochementDto(r)),
            TotalCount = total,
            Page = page,
            PageSize = size
        });
    }

    /// <summary>
    /// Valeurs distinctes (modes, origines) présentes sur la période, pour alimenter les
    /// filtres de la liste (front TASK-037). Depuis TASK-067B, expose également les numéros de
    /// règlement, numéros d'extrait et codes banque distincts (colonnes passées en filterType
    /// 'list'), calculés sur le MÊME WHERE/période que la liste principale (invariant TASK-040).
    /// Contrat clair { colonne: [valeurs] }. Lecture seule.
    /// </summary>
    [HttpGet("distincts")]
    public async Task<IActionResult> GetDistincts(
        [FromQuery] DateTime? debut,
        [FromQuery] DateTime? fin,
        [FromQuery] int soId)
    {
        if (soId <= 0)
            return BadRequest(new { Message = "'soId' est obligatoire." });
        if (!debut.HasValue || !fin.HasValue)
            return BadRequest(new { Message = "Les bornes de période 'debut' et 'fin' sont obligatoires." });

        var d = await _repository.GetReglementsRapprochementDistinctsAsync(soId, debut.Value, fin.Value);

        return Ok(new
        {
            Modes = d.Modes.Select(m => new { code = m, libelle = ReglementRapprochementRow.LibelleMode(m) }),
            Origines = d.Origines.Select(t => new { code = t, libelle = ReglementRapprochementRow.LibelleOrigine(t, t) }),
            NumeroReglement = d.NumerosReglement,
            NumeroExtrait = d.NumerosExtrait,
            Banque = d.Banques
        });
    }
}
