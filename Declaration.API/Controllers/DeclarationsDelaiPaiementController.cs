using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Declaration.API.Dtos;
using Declaration.Application.Entities;
using Declaration.Application.Services;
using Declaration.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Declaration.API.Controllers;

/// <summary>
/// TASK-134 (Délai de Paiement Maroc — front liste/fiche/sélection/contrôle) : contrôleur HTTP du
/// domaine « déclaration DDP ». <b>Aucun endpoint n'existait pour ce domaine</b> — point explicitement
/// laissé à TASK-134 par les VERIFY TASK-131 §9 n°7, TASK-132 §9 n°4 et TASK-133 §Reste à valider n°2
/// (« le contrôle d'autorisation par société reste à implémenter par TASK-134 »). Sans ce contrôleur,
/// aucun des 4 écrans de la TASK n'est possible.
///
/// <b>Pur passe-plat</b> : tout le métier vit dans TASK-131 (<see cref="ISelectionDelaiPaiementService"/>),
/// TASK-132 (<see cref="IDeclarationDelaiPaiementService"/> — bornes de période calculées, numérotation,
/// gardes de transition, contrôle IF/ICE bloquant) et TASK-133
/// (<see cref="IDeclarationDelaiPaiementGenerationService"/> — XML/ZIP). Rien n'est réimplémenté ici :
/// ce contrôleur route les exceptions métier vers des codes HTTP explicites (jamais un 500 générique)
/// et applique la garde d'autorisation par société.
///
/// Protections, identiques au pattern déjà en place (<c>ConventionsDelaiPaiementController</c> TASK-130,
/// <c>DelaiPaiementParametrageController</c> TASK-128, <c>DeclarationsController</c>) :
/// <list type="bullet">
/// <item><c>[Authorize]</c> + garde société <see cref="EstSocieteAutorisee"/> (claim <c>UT_Admin</c>=1
/// ou <c>SO_Id</c> présent dans le claim CSV <c>Societes</c>) — <b>dupliquée volontairement</b> : il
/// n'existe pas de base controller partagé dans ce dépôt, et le refactoriser est hors périmètre ;</item>
/// <item>propagation OBLIGATOIRE du claim JWT <c>UT_Id</c> sur toute opération d'écriture : le service
/// TASK-132 refuse toute mutation sans <c>UT_Id</c> strictement positif (audit trail, ARCHITECTURE §5)
/// — un jeton sans ce claim reçoit un 401 explicite, jamais un <c>0</c> « utilisateur inconnu ».</item>
/// </list>
///
/// <b>Filtre de période : jamais une plage libre</b> (point corrigé PO du 19/07/2026). Les deux seuls
/// chemins de sélection de lignes sont :
/// <list type="number">
/// <item><see cref="GetSelection"/> — période = bornes de la déclaration parente, lues en base ;</item>
/// <item><see cref="GetControle"/> — période CALCULÉE par
/// <see cref="DeclarationDelaiPaiementCycleDeVie.CalculerPeriode"/> à partir d'un exercice + type
/// (+ trimestre), exactement comme la création d'une déclaration.</item>
/// </list>
/// Aucun endpoint de ce contrôleur n'accepte de <c>dateDebut</c>/<c>dateFin</c> en paramètre.
/// </summary>
[ApiController]
[Route("api/declarations-delai-paiement")]
[Authorize]
public class DeclarationsDelaiPaiementController : ControllerBase
{
    private readonly IDeclarationDelaiPaiementService _service;
    private readonly ISelectionDelaiPaiementService _selection;
    private readonly IDeclarationDelaiPaiementGenerationService _generation;

    public DeclarationsDelaiPaiementController(
        IDeclarationDelaiPaiementService service,
        ISelectionDelaiPaiementService selection,
        IDeclarationDelaiPaiementGenerationService generation)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _selection = selection ?? throw new ArgumentNullException(nameof(selection));
        _generation = generation ?? throw new ArgumentNullException(nameof(generation));
    }

    // ─── Gardes ────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// TASK-074 : vrai pour <c>UT_Admin=1</c> (toutes sociétés) ou si <paramref name="societeId"/>
    /// figure dans le claim CSV <c>Societes</c> (posé au login depuis <c>P_SOCUTILISATEUR</c>). Même
    /// garde que <c>ConventionsDelaiPaiementController.EstSocieteAutorisee</c> /
    /// <c>DeclarationsController.EstSocieteAutorisee</c> — dupliquée : pas de base controller partagé
    /// dans ce projet (constat déjà documenté par TASK-128/130, non refactorisé ici).
    /// </summary>
    private bool EstSocieteAutorisee(int societeId)
    {
        if (User.HasClaim("UT_Admin", "1")) return true;

        var societes = User.FindFirst("Societes")?.Value ?? "";
        return societes.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Any(s => int.TryParse(s, out var id) && id == societeId);
    }

    /// <summary>Utilisateur courant (claim JWT <c>UT_Id</c>, posé au login par <c>AuthController</c>). Null si absent/invalide.</summary>
    private int? UtilisateurCourantId()
    {
        var claim = User.FindFirst("UT_Id")?.Value;
        return int.TryParse(claim, out var id) && id > 0 ? id : null;
    }

    /// <summary>
    /// Charge l'entête + le nombre de lignes et vérifie l'autorisation société. Retourne un
    /// <see cref="IActionResult"/> d'échec (404/403) OU l'entête, jamais les deux.
    /// </summary>
    private async Task<(IActionResult? Echec, DeclarationDelaiPaiement? Entete, int NombreLignes)> ChargerAsync(int ddpId)
    {
        var entete = await _service.GetAsync(ddpId);
        if (entete == null)
            return (NotFound(new { Message = $"Déclaration DDP_Id={ddpId} introuvable." }), null, 0);
        if (!EstSocieteAutorisee(entete.SocieteId))
            return (Forbid(), null, 0);

        var lignes = await _service.GetLignesAsync(ddpId);
        return (null, entete, lignes.Count);
    }

    private static bool TryParseType(string? type, out TypeDeclarationDelaiPaiement value)
    {
        if (string.Equals(type, "annuelle", StringComparison.OrdinalIgnoreCase))
        {
            value = TypeDeclarationDelaiPaiement.Annuelle;
            return true;
        }
        if (string.Equals(type, "trimestrielle", StringComparison.OrdinalIgnoreCase))
        {
            value = TypeDeclarationDelaiPaiement.Trimestrielle;
            return true;
        }
        value = TypeDeclarationDelaiPaiement.Trimestrielle;
        return false;
    }

    // ─── Paramétrage société (type par défaut, CDC §7.1) ──────────────────────────────────────────

    /// <summary>
    /// TASK-134 : type de déclaration PAR DÉFAUT de la société (<c>P_SOCIETE.SO_TypeDecDP</c>) —
    /// pré-remplit le formulaire de création et le filtre de période raisonné de l'écran de contrôle.
    /// LECTURE SEULE. Une valeur hors 1..2 (société non paramétrée) est renvoyée telle quelle avec
    /// <c>typeParDefaut = null</c> : l'écran choisit alors son propre repli visible, jamais un défaut
    /// silencieux côté serveur.
    /// </summary>
    [HttpGet("parametrage")]
    public async Task<IActionResult> GetParametrageSociete([FromQuery] int soId)
    {
        if (soId <= 0)
            return BadRequest(new { Message = "'soId' est obligatoire." });
        if (!EstSocieteAutorisee(soId))
            return Forbid();

        var info = await _service.GetParametrageSocieteAsync(soId);
        var code = info.TypeDeclarationParDefautCode;
        var typeParDefaut = code == (int)TypeDeclarationDelaiPaiement.Annuelle ? "Annuelle"
            : code == (int)TypeDeclarationDelaiPaiement.Trimestrielle ? "Trimestrielle"
            : null;

        return Ok(new { SoId = soId, TypeParDefautCode = code, TypeParDefaut = typeParDefaut });
    }

    // ─── Écran de CONTRÔLE (CDC §5.A-9) — visibilité/reporting pur ─────────────────────────────────

    /// <summary>
    /// TASK-134 écran 4 : lignes hors délai d'une période RAISONNÉE (exercice + type + trimestre), sans
    /// aucune déclaration en jeu. <b>Les bornes sont CALCULÉES</b> par
    /// <see cref="DeclarationDelaiPaiementCycleDeVie.CalculerPeriode"/> — la MÊME fonction que la
    /// création d'une déclaration (TASK-132) — et jamais saisies : le legacy
    /// (<c>FrmControleLigneDelaisPaiement.cs:56-60</c>) utilisait deux dates libres, anomalie
    /// explicitement corrigée par la demande PO du 19/07/2026.
    ///
    /// <b>Écran de simple visibilité</b> : cet endpoint est en LECTURE SEULE et n'offre AUCUN chemin
    /// d'intégration (le rattachement d'une ligne à une déclaration passe exclusivement par
    /// <see cref="IntegrerLignes"/>, qui exige un <c>ddpId</c>).
    /// </summary>
    [HttpGet("controle")]
    public async Task<IActionResult> GetControle(
        [FromQuery] int soId,
        [FromQuery] int exercice,
        [FromQuery] string? type,
        [FromQuery] int? trimestre)
    {
        if (soId <= 0)
            return BadRequest(new { Message = "'soId' est obligatoire." });
        if (!EstSocieteAutorisee(soId))
            return Forbid();
        if (!TryParseType(type, out var typeDeclaration))
            return BadRequest(new { Message = "'type' doit valoir 'annuelle' ou 'trimestrielle'." });

        TrimestreDelaiPaiement? trimestreTypé = null;
        if (typeDeclaration == TypeDeclarationDelaiPaiement.Trimestrielle)
        {
            if (trimestre is null or < 1 or > 4)
                return BadRequest(new { Message = "'trimestre' (1..4) est obligatoire pour une déclaration trimestrielle." });
            trimestreTypé = (TrimestreDelaiPaiement)trimestre.Value;
        }

        PeriodeDeclarationDelaiPaiement periode;
        try
        {
            // Bornes CALCULÉES, jamais saisies (exigence PO : aucun filtre de date libre).
            periode = DeclarationDelaiPaiementCycleDeVie.CalculerPeriode(exercice, typeDeclaration, trimestreTypé);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }

        try
        {
            var resultat = await _selection.SelectionnerAsync(soId, periode.DateDebut, periode.DateFin);
            return Ok(SelectionDelaiPaiementDto.From(resultat));
        }
        catch (InvalidOperationException ex)
        {
            // Ex. devise société introuvable — message explicite du repository TASK-131.
            return BadRequest(new { Message = ex.Message });
        }
    }

    // ─── Liste / fiche (écrans 1 et 2) ────────────────────────────────────────────────────────────

    /// <summary>TASK-134 écran 1 : liste des déclarations DDP d'une société (statut, période, nb lignes, dépôt).</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int soId)
    {
        if (soId <= 0)
            return BadRequest(new { Message = "'soId' est obligatoire." });
        if (!EstSocieteAutorisee(soId))
            return Forbid();

        var items = await _service.GetAllAsync(soId);
        return Ok(items.Select(DeclarationDelaiPaiementListItemDto.From));
    }

    /// <summary>TASK-134 écran 2 : entête de la fiche déclaration.</summary>
    [HttpGet("{ddpId:int}")]
    public async Task<IActionResult> Get(int ddpId)
    {
        var (echec, entete, nombreLignes) = await ChargerAsync(ddpId);
        if (echec != null) return echec;

        return Ok(DeclarationDelaiPaiementListItemDto.From(entete!, nombreLignes));
    }

    /// <summary>
    /// TASK-134 écran 2 : création. <b>Ni le numéro ni les bornes ne sont transmis</b> — le numéro est
    /// attribué par le serveur et les bornes calculées par TASK-132. <c>409</c> sur conflit métier
    /// (période déjà déclarée, numéro existant) avec le message du service, qui cite déjà la
    /// déclaration en conflit ; <c>400</c> sur saisie invalide.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreerDeclarationDelaiPaiementRequestBody body)
    {
        if (body == null)
            return BadRequest(new { Message = "Corps de requête manquant." });
        if (body.SoId <= 0)
            return BadRequest(new { Message = "'soId' est obligatoire." });
        if (!EstSocieteAutorisee(body.SoId))
            return Forbid();
        if (!TryParseType(body.Type, out var type))
            return BadRequest(new { Message = "'type' doit valoir 'annuelle' ou 'trimestrielle'." });

        TrimestreDelaiPaiement? trimestre = null;
        if (type == TypeDeclarationDelaiPaiement.Trimestrielle)
        {
            if (body.Trimestre is null or < 1 or > 4)
                return BadRequest(new { Message = "'trimestre' (1..4) est obligatoire pour une déclaration trimestrielle." });
            trimestre = (TrimestreDelaiPaiement)body.Trimestre.Value;
        }

        var utilisateurId = UtilisateurCourantId();
        if (utilisateurId == null)
            return Unauthorized(new { Message = "Utilisateur courant (UT_Id) absent du jeton : opération refusée (audit trail obligatoire)." });

        try
        {
            var ddpId = await _service.CreerAsync(new CreerDeclarationDelaiPaiementRequest
            {
                SocieteId = body.SoId,
                Exercice = body.Exercice,
                Type = type,
                Trimestre = trimestre,
                Libelle = body.Libelle,
                UtilisateurId = utilisateurId.Value,
            });
            return Ok(new { DdpId = ddpId });
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

    /// <summary>TASK-134 écran 2 : seule modification autorisée après création (legacy) — le libellé.</summary>
    [HttpPut("{ddpId:int}/libelle")]
    public async Task<IActionResult> ModifierLibelle(int ddpId, [FromBody] ModifierLibelleRequestBody body)
    {
        var (echec, _, _) = await ChargerAsync(ddpId);
        if (echec != null) return echec;

        var utilisateurId = UtilisateurCourantId();
        if (utilisateurId == null)
            return Unauthorized(new { Message = "Utilisateur courant (UT_Id) absent du jeton : opération refusée (audit trail obligatoire)." });

        return await ExecuterTransitionAsync(() => _service.ModifierLibelleAsync(ddpId, body?.Libelle, utilisateurId.Value));
    }

    /// <summary>TASK-134 écran 1 : suppression — refusée par TASK-132 si clôturée/déposée ou si des lignes sont intégrées.</summary>
    [HttpDelete("{ddpId:int}")]
    public async Task<IActionResult> Supprimer(int ddpId)
    {
        var (echec, _, _) = await ChargerAsync(ddpId);
        if (echec != null) return echec;

        var utilisateurId = UtilisateurCourantId();
        if (utilisateurId == null)
            return Unauthorized(new { Message = "Utilisateur courant (UT_Id) absent du jeton : opération refusée (audit trail obligatoire)." });

        return await ExecuterTransitionAsync(() => _service.SupprimerAsync(ddpId, utilisateurId.Value));
    }

    // ─── Lignes intégrées + popup de sélection (écrans 2 et 3) ────────────────────────────────────

    /// <summary>TASK-134 écran 2 : lignes DÉJÀ intégrées à la déclaration.</summary>
    [HttpGet("{ddpId:int}/lignes")]
    public async Task<IActionResult> GetLignes(int ddpId)
    {
        var (echec, _, _) = await ChargerAsync(ddpId);
        if (echec != null) return echec;

        var lignes = await _service.GetLignesAsync(ddpId);
        return Ok(lignes.Select(LigneDeclarationDelaiPaiementDto.From));
    }

    /// <summary>
    /// TASK-134 écran 3 (popup de sélection) : lignes candidates pour <b>la période de la déclaration
    /// parente</b> — <c>DDP_DateDebut</c>/<c>DDP_DateFin</c> lues en base, jamais transmises par le
    /// client. Renvoie aussi les lignes « reprise manuelle requise » (visibles, non intégrables) et la
    /// date de mise en route de la société (<c>null</c> ⇒ l'écran doit inviter à la saisir, TASK-128).
    /// </summary>
    [HttpGet("{ddpId:int}/selection")]
    public async Task<IActionResult> GetSelection(int ddpId)
    {
        var (echec, entete, _) = await ChargerAsync(ddpId);
        if (echec != null) return echec;

        try
        {
            var resultat = await _selection.SelectionnerAsync(entete!.SocieteId, entete.DateDebut, entete.DateFin);
            return Ok(SelectionDelaiPaiementDto.From(resultat));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    /// <summary>
    /// TASK-134 écran 3 : intégration MANUELLE d'une multi-sélection. <c>selection</c> vide/absente ⇒
    /// toutes les candidates (comportement TASK-132). Le compte rendu est renvoyé INTÉGRALEMENT :
    /// déjà intégrées, refusées pour reprise manuelle requise, introuvables — aucune ligne écartée
    /// silencieusement côté écran.
    /// </summary>
    [HttpPost("{ddpId:int}/lignes")]
    public async Task<IActionResult> IntegrerLignes(int ddpId, [FromBody] IntegrerLignesRequestBody? body)
    {
        var (echec, _, _) = await ChargerAsync(ddpId);
        if (echec != null) return echec;

        var utilisateurId = UtilisateurCourantId();
        if (utilisateurId == null)
            return Unauthorized(new { Message = "Utilisateur courant (UT_Id) absent du jeton : opération refusée (audit trail obligatoire)." });

        IReadOnlyCollection<CleLigneDelaiPaiement>? selection = null;
        if (body?.Selection is { Count: > 0 })
            selection = body.Selection.Select(c => new CleLigneDelaiPaiement(c.EcId, c.AfId)).ToList();

        try
        {
            var resultat = await _service.IntegrerLignesAsync(ddpId, utilisateurId.Value, selection);
            return Ok(ResultatIntegrationLignesDelaiPaiementDto.From(resultat));
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

    /// <summary>TASK-134 écran 2 : retire UNE ligne intégrée (préalable à la suppression d'une déclaration).</summary>
    [HttpDelete("{ddpId:int}/lignes/{ddplId:int}")]
    public async Task<IActionResult> SupprimerLigne(int ddpId, int ddplId)
    {
        var (echec, _, _) = await ChargerAsync(ddpId);
        if (echec != null) return echec;

        var utilisateurId = UtilisateurCourantId();
        if (utilisateurId == null)
            return Unauthorized(new { Message = "Utilisateur courant (UT_Id) absent du jeton : opération refusée (audit trail obligatoire)." });

        return await ExecuterTransitionAsync(() => _service.SupprimerLigneAsync(ddpId, ddplId, utilisateurId.Value));
    }

    // ─── Cycle de vie (écran 2) ───────────────────────────────────────────────────────────────────

    [HttpPost("{ddpId:int}/cloture")]
    public async Task<IActionResult> Cloturer(int ddpId) => await TransitionAsync(ddpId, (id, ut) => _service.CloturerAsync(id, ut));

    [HttpPost("{ddpId:int}/decloture")]
    public async Task<IActionResult> Decloturer(int ddpId) => await TransitionAsync(ddpId, (id, ut) => _service.AnnulerClotureAsync(id, ut));

    /// <summary>
    /// TASK-134 : dépôt = FLAG MANUEL (décision PO §5.A-2 du 19/07/2026). Aucun appel à une
    /// plateforme externe, ni ici ni dans le service.
    /// </summary>
    [HttpPost("{ddpId:int}/depot")]
    public async Task<IActionResult> Deposer(int ddpId) => await TransitionAsync(ddpId, (id, ut) => _service.MarquerDeposeAsync(id, ut));

    // ─── Contrôle IF/ICE + génération du fichier (écran 2, étape 4 de la TASK) ────────────────────

    /// <summary>
    /// TASK-134 : contrôle IF/ICE en mode INFORMATIF (ne lève pas) — permet à l'écran d'annoncer les
    /// fournisseurs fautifs AVANT de tenter la génération. Le verdict et les motifs sont ceux de
    /// TASK-132, repris tels quels : <b>le front n'applique aucune règle de longueur de son côté</b>
    /// (point ouvert PO/fiscaliste, VERIFY TASK-132 §8 n°9).
    /// </summary>
    [HttpGet("{ddpId:int}/controle-identite-fiscale")]
    public async Task<IActionResult> ControlerIdentiteFiscale(int ddpId)
    {
        var (echec, _, _) = await ChargerAsync(ddpId);
        if (echec != null) return echec;

        try
        {
            var controle = await _service.ControlerIdentiteFiscaleAsync(ddpId);
            return Ok(ControleIdentiteFiscaleDelaiPaiementDto.From(controle));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    /// <summary>
    /// TASK-134 étape 4 : déclenche la génération XML/ZIP (TASK-133). Le contrôle IF/ICE bloquant de
    /// TASK-132 est appelé par le service AVANT le premier octet écrit ; en cas de blocage, la réponse
    /// <c>409</c> porte le message serveur TEL QUEL <b>et</b> la liste structurée des fournisseurs
    /// fautifs (relue via le contrôle informatif, aucun effet de bord, aucune règle dupliquée) pour
    /// que l'écran puisse les afficher ligne par ligne.
    /// </summary>
    [HttpPost("{ddpId:int}/generation")]
    public async Task<IActionResult> Generer(int ddpId)
    {
        var (echec, _, _) = await ChargerAsync(ddpId);
        if (echec != null) return echec;

        var utilisateurId = UtilisateurCourantId();
        if (utilisateurId == null)
            return Unauthorized(new { Message = "Utilisateur courant (UT_Id) absent du jeton : opération refusée (audit trail obligatoire)." });

        try
        {
            await _generation.GenererFichierAsync(ddpId, utilisateurId.Value);
            return Ok(new { Fichier = $"/declarations-delai-paiement/{ddpId}/fichier" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // Garde d'état OU contrôle IF/ICE bloquant : le message est déjà le message utilisateur
            // (TASK-132). On y joint le verdict structuré, sans réévaluer la règle nous-mêmes.
            var fautifs = await ControleFautifsSansEffetDeBordAsync(ddpId);
            return Conflict(new { Message = ex.Message, FournisseursFautifs = fautifs });
        }
        catch (ApplicationException ex)
        {
            // Exporter TASK-133 : « le fichier XML/ZIP existe déjà » — conflit, jamais un 500.
            return Conflict(new { Message = ex.Message, FournisseursFautifs = Array.Empty<FournisseurIdentiteFiscaleFautifDto>() });
        }
    }

    /// <summary>
    /// TASK-134 : annulation de génération (TASK-133) — remet <c>DDP_IsGeneretedFile</c> à faux puis
    /// supprime les fichiers physiques (permet une régénération après correction des IF/ICE).
    /// </summary>
    [HttpPost("{ddpId:int}/generation/annulation")]
    public async Task<IActionResult> AnnulerGeneration(int ddpId)
    {
        var (echec, _, _) = await ChargerAsync(ddpId);
        if (echec != null) return echec;

        var utilisateurId = UtilisateurCourantId();
        if (utilisateurId == null)
            return Unauthorized(new { Message = "Utilisateur courant (UT_Id) absent du jeton : opération refusée (audit trail obligatoire)." });

        return await ExecuterTransitionAsync(() => _generation.AnnulerGenerationFichierAsync(ddpId, utilisateurId.Value));
    }

    /// <summary>
    /// TASK-134 : téléchargement du ZIP déjà généré (chemins déterministes exposés par TASK-133
    /// précisément « pour TASK-134 »). 404 explicite si la génération n'a pas eu lieu — jamais un flux
    /// vide silencieux.
    /// </summary>
    [HttpGet("{ddpId:int}/fichier")]
    public async Task<IActionResult> TelechargerFichier(int ddpId)
    {
        var (echec, entete, _) = await ChargerAsync(ddpId);
        if (echec != null) return echec;

        var (_, zipPath) = await _generation.ObtenirCheminsFichiersAsync(ddpId);
        // System.IO.File qualifié : ControllerBase.File(...) masque le type dans ce contexte.
        if (!System.IO.File.Exists(zipPath))
            return NotFound(new { Message = $"Fichier non généré pour la déclaration [{entete!.Numero}] — lancez d'abord la génération." });

        return PhysicalFile(zipPath, "application/zip", Path.GetFileName(zipPath));
    }

    // ─── Utilitaires de routage d'exception ───────────────────────────────────────────────────────

    private async Task<IActionResult> TransitionAsync(int ddpId, Func<int, int, Task> action)
    {
        var (echec, _, _) = await ChargerAsync(ddpId);
        if (echec != null) return echec;

        var utilisateurId = UtilisateurCourantId();
        if (utilisateurId == null)
            return Unauthorized(new { Message = "Utilisateur courant (UT_Id) absent du jeton : opération refusée (audit trail obligatoire)." });

        return await ExecuterTransitionAsync(() => action(ddpId, utilisateurId.Value));
    }

    /// <summary>
    /// Route les refus métier de TASK-132/133 : <see cref="ArgumentException"/> → 400,
    /// <see cref="InvalidOperationException"/> → 409 avec le message serveur TEL QUEL (tournures
    /// legacy volontairement conservées). Jamais de 500 générique, jamais de message reformulé.
    /// </summary>
    private static async Task<IActionResult> ExecuterTransitionAsync(Func<Task> action)
    {
        try
        {
            await action();
            return new NoContentResult();
        }
        catch (ArgumentException ex)
        {
            return new BadRequestObjectResult(new { Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return new ConflictObjectResult(new { Message = ex.Message });
        }
    }

    /// <summary>
    /// Relit le verdict IF/ICE en mode informatif pour enrichir une réponse de refus. Si cette relecture
    /// échoue elle-même (ex. déclaration supprimée entre-temps), on renvoie une liste vide plutôt que de
    /// masquer le refus d'origine — le message principal reste celui du service.
    /// </summary>
    private async Task<IReadOnlyList<FournisseurIdentiteFiscaleFautifDto>> ControleFautifsSansEffetDeBordAsync(int ddpId)
    {
        try
        {
            var controle = await _service.ControlerIdentiteFiscaleAsync(ddpId);
            return ControleIdentiteFiscaleDelaiPaiementDto.From(controle).FournisseursFautifs;
        }
        catch (InvalidOperationException)
        {
            return Array.Empty<FournisseurIdentiteFiscaleFautifDto>();
        }
    }
}

/// <summary>
/// TASK-134 : corps de création. <b>Aucun numéro, aucune borne de période</b> — attribués/calculés par
/// le serveur (TASK-132), et <c>UT_Id</c> vient du jeton, jamais du corps de requête.
/// </summary>
public class CreerDeclarationDelaiPaiementRequestBody
{
    public int SoId { get; set; }
    public int Exercice { get; set; }

    /// <summary>« annuelle » ou « trimestrielle ».</summary>
    public string? Type { get; set; }

    /// <summary>1..4, obligatoire si et seulement si <see cref="Type"/> = trimestrielle.</summary>
    public int? Trimestre { get; set; }

    public string? Libelle { get; set; }
}

public class ModifierLibelleRequestBody
{
    public string? Libelle { get; set; }
}

/// <summary>
/// TASK-134 : multi-sélection de la popup. Liste vide/absente ⇒ toutes les candidates de la période de
/// la déclaration (comportement TASK-132). <b>Aucune borne de date n'est transmise ici</b> : la période
/// est toujours celle de la déclaration parente.
/// </summary>
public class IntegrerLignesRequestBody
{
    public List<CleLigneDelaiPaiementBody>? Selection { get; set; }
}

public class CleLigneDelaiPaiementBody
{
    public int EcId { get; set; }
    public int? AfId { get; set; }
}
