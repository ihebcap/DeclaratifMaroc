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

    public FacturesController(
        IDeclarationRepository repository,
        Declaration.Application.Services.DeclarationWorkflowService workflowService)
    {
        _repository = repository;
        _workflowService = workflowService;
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

        var rows = await _repository.GetFacturesInterrogationAsync(
            soId, debut.Value, fin.Value, numero, fournisseur, reference, origine, statut, page, size, sort);
        var total = await _repository.GetFacturesInterrogationCountAsync(
            soId, debut.Value, fin.Value, numero, fournisseur, reference, origine, statut);

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
        if (!PeriodeValide(debut.Value, fin.Value, out var erreurPeriode))
            return BadRequest(new { Message = erreurPeriode });

        var d = await _repository.GetFacturesInterrogationDistinctsAsync(soId, debut.Value, fin.Value);

        return Ok(new
        {
            Origines = d.Origines.Select(t => new { code = t, libelle = ReglementRapprochementRow.LibelleEcType(t) }),
            Numero = d.Numeros,
            Reference = d.References
        });
    }

    /// <summary>
    /// Rafraîchit la valorisation TVA (famille B) des factures de la période affichée : lit les OM
    /// Sage et remplit le cache de ventilation (TASK-024). Opération explicite, déclenchée par le
    /// bouton « Rafraîchir » de la liste — synchrone et potentiellement longue (lecture COM Sage).
    /// Aucune écriture de déclaration, aucun DT_Id modifié. Après succès, le front recharge la liste.
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

        var rapport = await _workflowService.RafraichirValorisationAsync(soId, debut.Value, fin.Value);

        return Ok(new
        {
            FacturesTraitees = rapport.FacturesTraitees,
            NbErreurs = rapport.Erreurs.Count,
            Erreurs = rapport.Erreurs
        });
    }
}
