using System;
using System.Linq;
using System.Threading.Tasks;
using Declaration.API.Dtos;
using Declaration.Application.Services;
using Declaration.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Declaration.API.Controllers;

/// <summary>
/// TASK-130 (Délai de Paiement Maroc — Convention par tiers, FRONT) : contrôleur HTTP manquant,
/// identifié comme « reste à valider » par le VERIFY TASK-129 (service + repository livrés, aucun
/// endpoint posé). Périmètre « endpoints CRUD minimal » explicitement demandé par TASK-130 :
/// create/get/getAll/delete/terminer sur <c>IConventionDelaiPaiementService</c> (TASK-129, non
/// modifié), plus TROIS endpoints de lecture additifs strictement nécessaires à l'Étape 2 de
/// TASK-130 (formulaire de création : sélection tiers, sélection facture non payée) et à l'Étape 1
/// (téléchargement de la pièce jointe) — décision documentée en VERIFY, pas une dette silencieuse.
///
/// Protection cohérente avec le reste des endpoints de paramétrage GRF (TASK-074/TASK-128) :
/// <c>[Authorize]</c> + garde société (claim "UT_Admin"=1 ou société présente dans le claim CSV
/// "Societes").
/// </summary>
[ApiController]
[Route("api/conventions-delai-paiement")]
[Authorize]
public class ConventionsDelaiPaiementController : ControllerBase
{
    private readonly IConventionDelaiPaiementService _service;

    public ConventionsDelaiPaiementController(IConventionDelaiPaiementService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    /// <summary>Même garde que <c>DelaiPaiementParametrageController.EstSocieteAutorisee</c> (dupliquée : pas de base controller partagée dans ce projet).</summary>
    private bool EstSocieteAutorisee(int societeId)
    {
        if (User.HasClaim("UT_Admin", "1")) return true;

        var societes = User.FindFirst("Societes")?.Value ?? "";
        return societes.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Any(s => int.TryParse(s, out var id) && id == societeId);
    }

    private static bool TryParseDomaine(string? domaine, out DomaineDelaiPaiement value)
    {
        if (string.Equals(domaine, "achat", StringComparison.OrdinalIgnoreCase))
        {
            value = DomaineDelaiPaiement.Achat;
            return true;
        }
        if (string.Equals(domaine, "vente", StringComparison.OrdinalIgnoreCase))
        {
            value = DomaineDelaiPaiement.Vente;
            return true;
        }
        value = DomaineDelaiPaiement.Achat;
        return false;
    }

    private static bool TryParseType(string? type, out TypeConventionDelaiPaiement value)
    {
        if (string.Equals(type, "convention", StringComparison.OrdinalIgnoreCase))
        {
            value = TypeConventionDelaiPaiement.Convention;
            return true;
        }
        if (string.Equals(type, "facture", StringComparison.OrdinalIgnoreCase))
        {
            value = TypeConventionDelaiPaiement.Facture;
            return true;
        }
        value = TypeConventionDelaiPaiement.Convention;
        return false;
    }

    /// <summary>Liste des conventions d'une société pour un domaine (Étape 1 de TASK-130).</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int soId, [FromQuery] string? domaine)
    {
        if (soId <= 0)
            return BadRequest(new { Message = "'soId' est obligatoire." });
        if (!EstSocieteAutorisee(soId))
            return Forbid();
        if (!TryParseDomaine(domaine, out var d))
            return BadRequest(new { Message = "'domaine' doit valoir 'achat' ou 'vente'." });

        var items = await _service.GetAllForListAsync(soId, d);
        return Ok(items.Select(ConventionDelaiPaiementDto.From));
    }

    /// <summary>Détail unitaire (fourni pour complétude CRUD — voir note sur <see cref="ConventionDelaiPaiementDto.FromDetail"/>).</summary>
    [HttpGet("{cpId:int}")]
    public async Task<IActionResult> Get(int cpId)
    {
        var convention = await _service.GetAsync(cpId);
        if (convention == null)
            return NotFound(new { Message = $"Convention CP_Id={cpId} introuvable." });
        if (!EstSocieteAutorisee(convention.SocieteId))
            return Forbid();

        return Ok(ConventionDelaiPaiementDto.FromDetail(convention));
    }

    /// <summary>
    /// Téléchargement de la pièce jointe (Étape 1 de TASK-130 : « lien de téléchargement si
    /// présente, jamais bloquant si absente »). 404 explicite si la convention ou le fichier
    /// n'existe pas — jamais un flux vide silencieux.
    /// </summary>
    [HttpGet("{cpId:int}/fichier")]
    public async Task<IActionResult> GetFichier(int cpId)
    {
        var convention = await _service.GetAsync(cpId);
        if (convention == null)
            return NotFound(new { Message = $"Convention CP_Id={cpId} introuvable." });
        if (!EstSocieteAutorisee(convention.SocieteId))
            return Forbid();
        if (convention.File == null || convention.File.Length == 0)
            return NotFound(new { Message = "Aucune pièce jointe pour cette convention." });

        var fileName = string.IsNullOrWhiteSpace(convention.FileName) ? $"convention-{cpId}.pdf" : convention.FileName;
        return File(convention.File, "application/pdf", fileName);
    }

    /// <summary>
    /// Recherche de tiers (formulaire de création, Étape 2 de TASK-130) — endpoint additif non listé
    /// dans le périmètre « CRUD minimal » de départ, mais strictement nécessaire : sans lui, le
    /// formulaire ne peut désigner AUCUN tiers (décision documentée en VERIFY, cf. commentaire de
    /// classe).
    /// </summary>
    [HttpGet("tiers")]
    public async Task<IActionResult> SearchTiers([FromQuery] int soId, [FromQuery] string? domaine, [FromQuery] string? recherche)
    {
        if (soId <= 0)
            return BadRequest(new { Message = "'soId' est obligatoire." });
        if (!EstSocieteAutorisee(soId))
            return Forbid();
        if (!TryParseDomaine(domaine, out var d))
            return BadRequest(new { Message = "'domaine' doit valoir 'achat' ou 'vente'." });

        var items = await _service.SearchTiersAsync(soId, d, recherche);
        return Ok(items.Select(t => new { t.TiersNo, t.TiersCode, t.TiersIntitule }));
    }

    /// <summary>
    /// Factures non payées d'un tiers (formulaire de création, type Facture, Étape 2 de TASK-130) —
    /// même justification que <see cref="SearchTiers"/> ci-dessus.
    /// </summary>
    [HttpGet("factures-non-payees")]
    public async Task<IActionResult> GetFacturesNonPayees([FromQuery] int soId, [FromQuery] int tiersNo, [FromQuery] string? domaine)
    {
        if (soId <= 0 || tiersNo <= 0)
            return BadRequest(new { Message = "'soId' et 'tiersNo' sont obligatoires." });
        if (!EstSocieteAutorisee(soId))
            return Forbid();
        if (!TryParseDomaine(domaine, out var d))
            return BadRequest(new { Message = "'domaine' doit valoir 'achat' ou 'vente'." });

        var items = await _service.GetFacturesNonPayeesAsync(soId, tiersNo, d);
        return Ok(items.Select(f => new { f.EcId, f.DoNumero, f.DoDate, f.Montant, f.Solde }));
    }

    /// <summary>
    /// Création (Étape 2/3 de TASK-130). Le métier (plafond 180j, chevauchement bidirectionnel,
    /// unicité numéro/facture) est intégralement délégué à <c>ConventionDelaiPaiementService</c>
    /// (TASK-129, non modifié) — ce contrôleur ne fait que router les exceptions vers des codes HTTP
    /// explicites : <see cref="ArgumentException"/> → 400 (saisie invalide/incomplète),
    /// <see cref="InvalidOperationException"/> → 409 (conflit métier — chevauchement, doublon,
    /// plafond dépassé, facture introuvable/déjà payée). Le message d'erreur du service (TASK-129)
    /// cite déjà explicitement la convention en conflit (numéro + période) — Étape 3 de TASK-130.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreerConventionRequestBody body)
    {
        if (body == null)
            return BadRequest(new { Message = "Corps de requête manquant." });
        if (body.SoId <= 0)
            return BadRequest(new { Message = "'soId' est obligatoire." });
        if (!EstSocieteAutorisee(body.SoId))
            return Forbid();
        if (!TryParseDomaine(body.Domaine, out var domaine))
            return BadRequest(new { Message = "'domaine' doit valoir 'achat' ou 'vente'." });
        if (!TryParseType(body.Type, out var type))
            return BadRequest(new { Message = "'type' doit valoir 'convention' ou 'facture'." });

        byte[]? fileBytes = null;
        if (!string.IsNullOrEmpty(body.FileBase64))
        {
            try
            {
                fileBytes = Convert.FromBase64String(body.FileBase64);
            }
            catch (FormatException)
            {
                return BadRequest(new { Message = "Le fichier joint est invalide (contenu base64 mal formé)." });
            }
        }

        try
        {
            var cpId = await _service.CreerAsync(new CreerConventionDelaiPaiementRequest
            {
                SocieteId = body.SoId,
                TiersNo = body.TiersNo,
                TiersCode = body.TiersCode,
                Date = body.Date,
                Numero = body.Numero,
                DateDebut = body.DateDebut,
                DateFin = body.DateFin,
                NombreJoursDelaisPaiement = body.NombreJoursDelaisPaiement,
                Domaine = domaine,
                Type = type,
                FactureNo = body.FactureNo,
                FileName = body.FileName,
                File = fileBytes,
            });
            return Ok(new { CpId = cpId });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { Message = ex.Message });
        }
    }

    /// <summary>Clôture anticipée (Étape 4 de TASK-130). 409 si les bornes sont invalides (message explicite du service).</summary>
    [HttpPut("{cpId:int}/terminer")]
    public async Task<IActionResult> Terminer(int cpId, [FromBody] TerminerConventionRequestBody body)
    {
        var convention = await _service.GetAsync(cpId);
        if (convention == null)
            return NotFound(new { Message = $"Convention CP_Id={cpId} introuvable." });
        if (!EstSocieteAutorisee(convention.SocieteId))
            return Forbid();
        if (body == null || body.NouvelleDateFin == default)
            return BadRequest(new { Message = "'nouvelleDateFin' est obligatoire." });

        try
        {
            await _service.TerminerAsync(cpId, body.NouvelleDateFin);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { Message = ex.Message });
        }
    }

    /// <summary>Suppression (reproduit le legacy sans garde, décision TASK-129 assumée à nouveau ici).</summary>
    [HttpDelete("{cpId:int}")]
    public async Task<IActionResult> Delete(int cpId)
    {
        var convention = await _service.GetAsync(cpId);
        if (convention == null)
            return NotFound(new { Message = $"Convention CP_Id={cpId} introuvable." });
        if (!EstSocieteAutorisee(convention.SocieteId))
            return Forbid();

        await _service.DeleteAsync(cpId);
        return NoContent();
    }
}
