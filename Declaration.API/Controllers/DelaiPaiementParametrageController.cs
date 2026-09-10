using System;
using System.Linq;
using System.Threading.Tasks;
using Declaration.Application.Interfaces;
using Declaration.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Declaration.API.Controllers;

/// <summary>
/// TASK-128 : endpoint minimal de paramétrage du bootstrap Délai de Paiement Maroc — date de
/// mise en route par société (<c>DM_PARAM_DELAIPAIEMENT_SOCIETE</c>). L'écran de saisie est porté
/// par TASK-134, l'algorithme de sélection qui consomme cette donnée par TASK-131.
///
/// Depuis TASK-220, ne porte plus les endpoints de reprise manuelle par échéance (mécanisme
/// supprimé) — cf. <see cref="Declaration.Application.Services.IDelaiPaiementBootstrapService"/>.
///
/// Protection cohérente avec le reste des endpoints de paramétrage GRF (TASK-074) : <c>[Authorize]</c>
/// + garde société (claim "UT_Admin"=1 ou société présente dans le claim CSV "Societes").
/// </summary>
[ApiController]
[Route("api/delai-paiement")]
[Authorize]
public class DelaiPaiementParametrageController : ControllerBase
{
    private readonly IDelaiPaiementBootstrapService _service;

    public DelaiPaiementParametrageController(IDelaiPaiementBootstrapService service)
    {
        _service = service;
    }

    /// <summary>
    /// TASK-074 : vrai pour UT_Admin=1 (toutes sociétés) ou si societeId figure dans le claim
    /// "Societes" (CSV des SO_Id autorisés, posé au login depuis P_SOCUTILISATEUR). Même garde que
    /// <c>DeclarationsController.EstSocieteAutorisee</c> (dupliquée ici : pas de base controller
    /// partagée dans ce projet — hors périmètre de refactoriser l'existant pour cette tâche).
    /// </summary>
    private bool EstSocieteAutorisee(int societeId)
    {
        if (User.HasClaim("UT_Admin", "1")) return true;

        var societes = User.FindFirst("Societes")?.Value ?? "";
        return societes.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Any(s => int.TryParse(s, out var id) && id == societeId);
    }

    /// <summary>Utilisateur courant (claim JWT "UT_Id", posé au login). Null si absent/invalide.</summary>
    private int? UtilisateurCourantId()
    {
        var claim = User.FindFirst("UT_Id")?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }

    /// <summary>
    /// Date de mise en route configurée pour la société, ou <c>null</c> si aucune n'a encore été
    /// saisie ("pas encore configuré" — jamais une date par défaut arbitraire).
    /// </summary>
    [HttpGet("parametrage/{soId:int}")]
    public async Task<IActionResult> GetParametrage(int soId)
    {
        if (soId <= 0)
            return BadRequest(new { Message = "'soId' est obligatoire." });
        if (!EstSocieteAutorisee(soId))
            return Forbid();

        var dateMiseEnRoute = await _service.GetDateMiseEnRouteAsync(soId);
        return Ok(new { SoId = soId, DateMiseEnRoute = dateMiseEnRoute });
    }

    /// <summary>
    /// Saisie (une fois, ou correction, par société) de la date de mise en route.
    /// </summary>
    [HttpPut("parametrage/{soId:int}")]
    public async Task<IActionResult> SetParametrage(int soId, [FromBody] SetDateMiseEnRouteRequest request)
    {
        if (soId <= 0)
            return BadRequest(new { Message = "'soId' est obligatoire." });
        if (!EstSocieteAutorisee(soId))
            return Forbid();
        if (request.DateMiseEnRoute == default)
            return BadRequest(new { Message = "'dateMiseEnRoute' est obligatoire." });

        await _service.SetDateMiseEnRouteAsync(soId, request.DateMiseEnRoute, UtilisateurCourantId());
        return NoContent();
    }

}

public class SetDateMiseEnRouteRequest
{
    public DateTime DateMiseEnRoute { get; set; }
}
