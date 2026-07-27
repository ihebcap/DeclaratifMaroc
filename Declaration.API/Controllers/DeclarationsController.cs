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
        try
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
        catch (InvalidOperationException ex)
        {
            // TASK-175 : le front charge volontairement les deux domaines (Decaissement/Encaissement)
            // en parallèle pour le même soId — celui qui perd la course au verrou anti-chevauchement
            // (TASK-156, ExecuterAvecVerrouOMAsync) doit recevoir un 409 explicite (retry possible côté
            // front), jamais un 500 générique. Même pattern que Resynchroniser (ligne ~285) — seul ce
            // chemin (le plus fréquemment sollicité, premier chargement d'une déclaration) laissait
            // fuiter l'exception faute de try/catch.
            return Conflict(new { Message = ex.Message });
        }
    }

    [HttpPatch("{id}/lignes/{ligneId}")]
    public async Task<IActionResult> UpdateLigneEtat(Guid id, Guid ligneId, [FromBody] UpdateEtatRequest request)
    {
        await _repository.UpdateLigneEtatAsync(ligneId, request.Etat);
        return NoContent();
    }

    /// <summary>
    /// TASK-161 : surcharge manuelle du code activité d'UNE ligne (écran ② Vérifier & Intégrer) —
    /// cible <paramref name="ligneId"/> (DM_LGTVA.Id), jamais l'EC_Id (contrairement à
    /// valider-incoherence) : une même facture peut porter deux lignes de taux différents avec
    /// deux activités différentes (cas confirmé PO). Bloquée après clôture (409) — le code
    /// activité reste stable une fois la déclaration close, même si le mapping tiers change
    /// ensuite.
    /// </summary>
    [HttpPatch("{id}/lignes/{ligneId}/code-activite")]
    public async Task<IActionResult> UpdateCodeActiviteLigne(Guid id, Guid ligneId, [FromBody] UpdateCodeActiviteRequest request)
    {
        var utilisateur = User.Identity?.Name ?? "inconnu";
        try
        {
            await _workflowService.ModifierCodeActiviteLigneAsync(id, ligneId, request.CodeActivite ?? "", utilisateur);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { Message = ex.Message });
        }
        catch (ApplicationException ex)
        {
            // TASK-172 §4 : code activité inconnu du référentiel ou incompatible avec le domaine de
            // la ligne — rejet explicite (400), distinct du 409 de clôture ci-dessus, jamais un 500
            // ni une acceptation silencieuse.
            return BadRequest(new { Message = ex.Message });
        }
    }

    /// <summary>
    /// TASK-161 : référentiel des codes activité (P_DECTVAACTIVITE, lecture seule GRF) — alimente
    /// la liste déroulante de sélection manuelle côté front. Aucun paramétrage possible ici
    /// (décision PO point 2 : GRF web reste lecture seule, paramétrage laissé à l'écran
    /// Trésorerie WinForms existant).
    /// TASK-172 : <paramref name="domaine"/> optionnel ("Encaissement"/"Decaissement") filtre le
    /// référentiel par onglet actif — omis = référentiel complet (non filtré), non-régression du
    /// comportement TASK-161 pour tout appelant existant.
    /// </summary>
    [HttpGet("/api/codes-activite")]
    public async Task<IActionResult> GetCodesActivite([FromQuery] string? domaine)
    {
        if (!string.IsNullOrEmpty(domaine) && domaine != "Encaissement" && domaine != "Decaissement")
            return BadRequest(new { Message = "'domaine' doit valoir 'Encaissement' ou 'Decaissement'." });

        var referentiel = await _workflowService.GetReferentielCodesActiviteAsync(domaine);
        return Ok(referentiel);
    }

    /// <summary>
    /// TASK-144 : diagnostic explicatif en ligne d'une ligne en anomalie (identifiée par son EC_Id).
    /// LECTURE SEULE STRICTE — aucune écriture, aucune NOUVELLE lecture OM Sage (le motif d'échec
    /// déjà tenté est relu depuis le cache). Le seul accès Sage est un SELECT F_DOCREGL (contrôle
    /// collision DO_Numero), base résolue dynamiquement par SO_Id (TASK-118). Retourne 404 si
    /// l'échéance est introuvable pour cette déclaration/société.
    /// </summary>
    [HttpGet("{id}/lignes/diagnostic/{ecId:int}")]
    public async Task<IActionResult> DiagnostiquerLigne(Guid id, int ecId)
    {
        if (ecId <= 0)
            return BadRequest(new { Message = "'ecId' est obligatoire et doit être positif." });

        var resultat = await _workflowService.DiagnostiquerLigneAsync(id, ecId);
        if (resultat == null)
            return NotFound(new { Message = $"Aucune échéance EC_Id={ecId} trouvée pour cette déclaration." });

        return Ok(new DiagnosticLigneDto(resultat));
    }

    /// <summary>
    /// TASK-147 : recalcule UNE ligne Proposee dont le diagnostic a détecté un cache PÉRIMÉ (relu
    /// avec succès APRÈS la création de la déclaration). Ne redéclenche AUCUNE lecture OM Sage —
    /// reconstruit la ligne depuis le cache déjà persisté. Retourne 409 si les conditions de
    /// staleness ne sont plus réunies (rien écrit dans ce cas).
    /// </summary>
    [HttpPost("{id}/lignes/recalculer-depuis-cache")]
    public async Task<IActionResult> RecalculerLigneDepuisCache(Guid id, [FromBody] ValiderIncoherenceRequest request)
    {
        if (request.EcId <= 0)
            return BadRequest(new { Message = "'ecId' est obligatoire." });
        var (trouvee, recalculee, message) = await _workflowService.RecalculerLigneDepuisCacheAsync(id, request.EcId);
        if (!trouvee)
            return NotFound(new { Message = message });
        if (!recalculee)
            return Conflict(new { Message = message });
        return Ok(new { Message = message });
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
    ///
    /// TASK-167 : ce même chemin est désormais aussi le bouton « Relire depuis Sage » de l'écran
    /// ③ Vérifier & Intégrer (DiagnosticModal) pour toute ligne non valorisée (cache absent/en
    /// erreur), pas seulement les incohérences TASK-078 — <c>ResynchroniserLigneAsync</c> est déjà
    /// générique (aucune règle propre à l'incohérence, purge + relecture Sage pour n'importe quel
    /// EC_Id). Passe par le verrou <c>soId</c> partagé (TASK-156, <c>ExecuterAvecVerrouOMAsync</c>)
    /// — un second appel concurrent pour la même société est rejeté ici en 409, comme les 3 autres
    /// appelants de l'orchestrateur (jusqu'ici seul ce endpoint laissait fuiter l'exception en 500).
    /// </summary>
    [HttpPost("{id}/lignes/resynchroniser")]
    public async Task<IActionResult> Resynchroniser(Guid id, [FromBody] ValiderIncoherenceRequest request)
    {
        if (request.EcId <= 0)
            return BadRequest(new { Message = "'ecId' est obligatoire." });
        try
        {
            var (trouvee, resolue) = await _workflowService.ResynchroniserLigneAsync(id, request.EcId);
            if (!trouvee)
                return NotFound(new { Message = $"Aucune ligne trouvée pour EC_Id={request.EcId} sur cette déclaration." });
            return Ok(new { Resolue = resolue });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { Message = ex.Message });
        }
    }

    /// <summary>
    /// TASK-176 : resynchronise EN MASSE toutes les pièces Sage d'une sélection — même contrat de
    /// sélection que <see cref="UpdateLignesBulk"/>/<see cref="UpdateCodeActiviteBulk"/> (liste de
    /// LigneIds OU Domaine[+Filter]). Réutilise strictement le pipeline unitaire
    /// <c>ResynchroniserLigneAsync</c> pour chaque EC_Id, SÉQUENTIELLEMENT (jamais en parallèle : chaque
    /// relecture prend le verrou soId TASK-156). Retour synthétique agrégé (traitées / résolues /
    /// toujours en anomalie avec motif). Un verrou soId capté par un autre traitement AVANT la première
    /// pièce → 409 propre (rien fait) ; capté ENTRE deux pièces → 200 avec Interrompu=true et les lignes
    /// déjà passées préservées (jamais un 500).
    /// </summary>
    [HttpPost("{id}/lignes/resynchroniser:bulk")]
    public async Task<IActionResult> ResynchroniserBulk(Guid id, [FromBody] BulkResynchroniserRequest request)
    {
        if ((request.LigneIds == null || request.LigneIds.Count == 0) && string.IsNullOrEmpty(request.Domaine))
            return BadRequest(new { Message = "Soit LigneIds soit Domaine doit être renseigné." });

        try
        {
            var resultat = await _workflowService.ResynchroniserLignesBulkAsync(
                id, request.LigneIds, request.Domaine, request.Filter);

            // Verrou concurrent AVANT toute pièce traitée : rien fait → rejet propre 409 (TASK-156),
            // cohérent avec le endpoint unitaire Resynchroniser.
            if (resultat.Interrompu && resultat.Traitees == 0)
                return Conflict(new { Message = resultat.MessageInterruption ?? "Un autre traitement est déjà en cours pour cette société." });

            return Ok(resultat);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
        catch (ApplicationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
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
    /// TASK-173 : affectation en masse du code activité — même mécanique de sélection que
    /// <see cref="UpdateLignesBulk"/> (TASK-012, liste d'IDs OU domaine+filtre), appliquée à
    /// <c>CodeActivite</c> au lieu de l'<c>Etat</c> de la ligne. Blocage explicite (400) si la
    /// sélection mélange Encaissement/Décaissement ou si le code choisi est incompatible avec le
    /// domaine résolu (décision PO §4, même garde-fou que ModifierCodeActiviteLigneAsync/TASK-172),
    /// et 409 si la déclaration est Clôturée (même garde que le PATCH unitaire TASK-161).
    /// </summary>
    [HttpPost("{id}/lignes/code-activite:bulk")]
    public async Task<IActionResult> UpdateCodeActiviteBulk(Guid id, [FromBody] BulkUpdateCodeActiviteRequest request)
    {
        if ((request.LigneIds == null || request.LigneIds.Count == 0) && string.IsNullOrEmpty(request.Domaine))
            return BadRequest(new { Message = "Soit LigneIds soit Domaine doit être renseigné." });

        var utilisateur = User.Identity?.Name ?? "inconnu";
        try
        {
            await _workflowService.ModifierCodeActiviteLignesBulkAsync(
                id, request.LigneIds, request.Domaine, request.Filter, request.CodeActivite ?? "", utilisateur);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { Message = ex.Message });
        }
        catch (ApplicationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    /// <summary>
    /// Vérifie la cohérence de la déclaration avant clôture.
    /// Retourne les alertes (Info/Warning/Error) + contrôle d'équilibre réels.
    /// Les alertes Error bloquent la clôture.
    /// </summary>
    [HttpPost("{id}/selection")]
    public async Task<IActionResult> SaveSelection(Guid id, [FromBody] List<string> selectedNumeros)
    {
        await _repository.SaveSelectionReglementsAsync(id, selectedNumeros);
        return NoContent();
    }

    [HttpGet("{id}/selection")]
    public async Task<IActionResult> GetSelection(Guid id)
    {
        var selection = await _repository.GetSelectionReglementsAsync(id);
        return Ok(selection);
    }

    [HttpGet("{id}/checkup")]
    public async Task<IActionResult> GetCheckup(Guid id)
    {
        try
        {
            var result = await _workflowService.GetCheckupAsync(id);
            var lignesDec = (await _repository.GetLignesAsync(id, "Decaissement", 1, int.MaxValue, null, null)).ToList();
            var lignesEnc = (await _repository.GetLignesAsync(id, "Encaissement", 1, int.MaxValue, null, null)).ToList();
            var lignes = lignesDec.Concat(lignesEnc).ToList();
            var proposees = lignes.Count(l => l.Etat == EtatLigne.Proposee);
            var integreesCount = lignes.Count(l => l.Etat == EtatLigne.Integree);
            var exclues = lignes.Count(l => l.Etat == EtatLigne.Exclue);
            var reportees = lignes.Count(l => l.Etat == EtatLigne.Reportee);
            var ecartees = lignes.Count(l => l.Etat == EtatLigne.Ecartee);

            // TASK-058 : projection de contrôle consommée par l'écran ⑤ (front lecture seule).
            // Aucun recalcul TVA — simple agrégation des lignes déjà valorisées par le recalcul
            // back (TASK-009). Les clés JSON sont alignées sur le contrat front (recapSource /
            // recapTaux / alertes typées / equilibre).

            // TASK-103 : recapSource/recapTaux doivent couvrir le même ensemble que le
            // contrôle d'équilibre (DeclarationWorkflowService.cs:738 — Integree OU Proposee),
            // sinon le détail « Répartition par source » reste invisible tant que la
            // déclaration n'est pas clôturée (les lignes valorisées sont encore Proposee).
            var lignesRecap = lignes.Where(l => l.Etat == EtatLigne.Integree || l.Etat == EtatLigne.Proposee).ToList();

            var recapSource = lignesRecap
                .GroupBy(l => string.IsNullOrWhiteSpace(l.Source) ? "—" : l.Source)
                .Select(g => new
                {
                    source = g.Key,
                    ht = g.Sum(x => x.HT),
                    tva = g.Sum(x => x.TVA),
                    ttc = g.Sum(x => x.TTC),
                    nbLignes = g.Count()
                })
                .OrderByDescending(x => x.ttc)
                .ToList();

            var recapTaux = lignesRecap
                .GroupBy(l => new { l.Taux, l.Domaine })
                .Select(g => new
                {
                    taux = g.Key.Taux,
                    domaine = g.Key.Domaine,
                    ht = g.Sum(x => x.HT),
                    tva = g.Sum(x => x.TVA),
                    ttc = g.Sum(x => x.TTC),
                    nbLignes = g.Count()
                })
                .OrderByDescending(x => x.taux)
                .ToList();

            // Équilibre : dérivé du contrôle back (TotalDeclareTtc vs HT + TVA), les trois
            // termes agrégés sur le MÊME ensemble Integree||Proposee (lignesRecap, TASK-108).
            // Avant TASK-108, totalTva était restreint aux seules lignes Integree alors que
            // TotalDeclareTtc/TotalMontantAffecte couvrent Integree||Proposee : sur une
            // déclaration EnCours (0 Integree) l'écart valait alors ΣTVA Proposee en entier
            // (la TVA totale mal étiquetée « écart détecté »). Écart ~0 = déclaration équilibrée.
            // Tolérance d'arrondi 0,01.
            var totalTva = lignesRecap.Sum(l => l.TVA);
            var ecart = result.ControleEquilibre.TotalDeclareTtc
                        - (result.ControleEquilibre.TotalMontantAffecte + totalTva);

            // TASK-112 : axe réellement discriminant pour isoler l'écart — Source est une
            // tautologie (DM_LGTVA.Source == domaine pour 100% des lignes d'un domaine donné).
            // Par construction ligne à ligne (TTC = HT + TVA attendu à la valorisation), l'écart
            // ci-dessus se décompose EXACTEMENT en Σ résidu des lignes où ce n'est pas vérifié
            // (ex. facture non ventilée : HT affecté mais Taux/TVA/TTC restés à 0,
            // DeclarationWorkflowService.MapLignesCandidates) — les lignes cohérentes contribuent
            // un résidu nul par définition. `ecartExplique` vérifie cette identité pour ne jamais
            // présenter un drill qui ne rendrait pas compte de la totalité de l'écart annoncé
            // (principe « aucune ligne silencieuse »).
            const decimal toleranceResidu = 0.01m;
            var recapIncoherence = lignesRecap
                .GroupBy(l => new { l.Domaine, Incoherente = Math.Abs(l.TTC - (l.HT + l.TVA)) > toleranceResidu })
                .Select(g => new
                {
                    domaine = g.Key.Domaine,
                    incoherente = g.Key.Incoherente,
                    ht = g.Sum(x => x.HT),
                    tva = g.Sum(x => x.TVA),
                    ttc = g.Sum(x => x.TTC),
                    residu = g.Sum(x => x.TTC - (x.HT + x.TVA)),
                    nbLignes = g.Count()
                })
                .OrderByDescending(x => x.incoherente)
                .ToList();
            var residuIncoherentes = recapIncoherence.Where(g => g.incoherente).Sum(g => g.residu);
            var ecartExplique = Math.Abs(ecart - residuIncoherentes) < toleranceResidu;

            // TASK-174 : recapActivite groupé par {CodeActivite, Domaine} sur le même ensemble
            // que recapSource/recapTaux (lignesRecap) — pure agrégation, code activité déjà
            // affecté par TASK-161. Vide → "—" (jamais masquer une ligne sans code, cf. garde-fou).
            var recapActivite = lignesRecap
                .GroupBy(l => new { CodeActivite = string.IsNullOrWhiteSpace(l.CodeActivite) ? "—" : l.CodeActivite, l.Domaine })
                .Select(g => new
                {
                    codeActivite = g.Key.CodeActivite,
                    domaine = g.Key.Domaine,
                    ht = g.Sum(x => x.HT),
                    tva = g.Sum(x => x.TVA),
                    ttc = g.Sum(x => x.TTC)
                })
                .OrderBy(x => x.codeActivite)
                .ToList();

            var equilibre = new { isValid = Math.Abs(ecart) < toleranceResidu, ecart, ecartExplique };

            // Alertes typées : Niveau back → type front (Error → bloquant, Warning/Info →
            // avertissement, aucune masquée). Drill câblé en priorité sur EC_Id (TASK-146, clé
            // précise par ligne) — repli sur numeroRapprochement (TASK-034) si EC_Id absent/0
            // (lignes figées avant TASK-077, cf. commentaire AffectationsDrill.tsx:50).
            var alertes = result.Alertes.Select(a =>
            {
                var ligne = lignes.FirstOrDefault(l => l.NumeroFacture == a.RefLigne);
                var hasEcId = ligne != null && ligne.EC_Id > 0;
                var hasDrill = ligne != null && (hasEcId || !string.IsNullOrWhiteSpace(ligne.NumeroRapprochement));
                object? filtre = !hasDrill ? null
                    : hasEcId ? new { ecId = new[] { ligne!.EC_Id } }
                    : new { numeroRapprochement = ligne!.NumeroRapprochement };
                return new
                {
                    type = a.Niveau == Declaration.Core.Model.NiveauAlerte.Error ? "bloquant" : "avertissement",
                    message = a.Message,
                    code = a.Code,
                    refLigne = a.RefLigne,
                    domaine = hasDrill ? ligne!.Domaine : null,
                    filtre
                };
            }).ToList();

            return Ok(new {
                equilibre,
                recapSource,
                recapTaux,
                recapActivite,
                recapIncoherence,
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
        catch (InvalidOperationException ex)
        {
            // TASK-175 §2/§6 : même lacune structurelle que GetLignes ci-dessus — GetCheckupAsync
            // revalide aussi les deux domaines (RevaliderLignesFigeesAsync, lignes ~1138-1139) et peut
            // donc invoquer ReintegrerReglementsLiberesAsync → ExecuterAvecVerrouOMAsync (verrou soId,
            // TASK-156) et lever la même InvalidOperationException en cas de contention. Sans ce
            // catch, ce chemin renvoyait lui aussi un 500 générique au lieu du 409 prévu par la
            // conception TASK-156 (« propagée en 409 par les contrôleurs », uniforme sur tous les
            // appelants de l'orchestrateur).
            return Conflict(new { Message = ex.Message });
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

    // ─── Génération / téléchargement des fichiers de dépôt (TASK-155) ─────────────
    // Câblage réel de DeclarationXmlExporter (TASK-137) et Declaration.Export.Excel.Exporter
    // (TASK-010) — la construction du DeclarationModele et l'appel aux deux exporters vivent
    // dans DeclarationWorkflowService (ConstruireModeleExportAsync/GenererFichiersExportAsync),
    // ce contrôleur ne fait que traduire les erreurs métier en réponses HTTP explicites.

    /// <summary>
    /// TASK-155 : génère les fichiers XML (DGI, zippé) et Excel (Checkup) d'une déclaration
    /// Clôturée. Aucun 500 générique : chaque cas métier (non clôturée, IF société absent,
    /// fichier déjà existant, IF/ICE tiers invalide, aucune ligne à exporter) est traduit en
    /// réponse explicite.
    /// </summary>
    [HttpPost("{id}/generation")]
    public async Task<IActionResult> Generation(Guid id)
    {
        var declaration = await _repository.GetByIdAsync(id);
        if (declaration == null) return NotFound();

        if (!EstSocieteAutorisee(declaration.SocieteId))
            return Forbid();

        try
        {
            await _workflowService.GenererFichiersExportAsync(id);
            // TASK-155 : clés alignées sur le contrat DÉJÀ attendu par DeclarationFinalePanel.tsx
            // (composant réellement monté par DeclarationStepper.tsx/App.tsx — cf. VERIFY, la
            // cartographie de la task pointait par erreur vers GenerationPanel.tsx, jamais importé
            // nulle part). Valeurs = URLs relatives consommées telles quelles par `api.get(url,
            // { responseType: 'blob' })`, jamais un chemin disque exposé au client.
            return Ok(new
            {
                fichiers = new
                {
                    xmlDecaissement = $"/declarations/{id}/fichiers/xml",
                    excelCheckup = $"/declarations/{id}/fichiers/excel"
                }
            });
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
        catch (ApplicationException ex)
        {
            // TASK-155 : "existe déjà" (conflit — la génération a déjà eu lieu) se distingue des
            // autres refus métier de l'exporter (IF/ICE invalide, aucune ligne) — tous deux
            // délibérément explicites (jamais un 500 générique), jamais un bypass silencieux.
            if (ex.Message.Contains("existe déjà", StringComparison.OrdinalIgnoreCase))
                return Conflict(new { Message = ex.Message });
            return BadRequest(new { Message = ex.Message });
        }
    }

    /// <summary>
    /// TASK-155 : télécharge un fichier déjà généré (<c>type</c> = <c>xml</c> ou <c>excel</c>).
    /// Aucun état "généré" persisté (cf. cartographie TASK-155 point 7) — la présence du fichier
    /// sur disque, à l'emplacement déterministe dérivé du Numero/Exercice/Periode de la
    /// déclaration, EST l'état. 404 si la génération n'a pas encore eu lieu.
    /// </summary>
    [HttpGet("{id}/fichiers/{type}")]
    public async Task<IActionResult> DownloadFichier(Guid id, string type)
    {
        var declaration = await _repository.GetByIdAsync(id);
        if (declaration == null) return NotFound();

        if (!EstSocieteAutorisee(declaration.SocieteId))
            return Forbid();

        string path;
        string contentType;
        switch (type.ToLowerInvariant())
        {
            case "xml":
                contentType = "application/zip";
                break;
            case "excel":
                contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                break;
            default:
                return BadRequest(new { Message = $"Type de fichier inconnu : '{type}'. Valeurs attendues : xml, excel." });
        }

        var (xmlZipPath, excelPath) = await _workflowService.ObtenirCheminsExportAsync(id);
        path = type.ToLowerInvariant() == "xml" ? xmlZipPath : excelPath;

        if (!System.IO.File.Exists(path))
            return NotFound(new { Message = "Fichier non généré — lancez d'abord la génération (POST .../generation)." });

        return PhysicalFile(path, contentType, System.IO.Path.GetFileName(path));
    }

    /// <summary>
    /// TASK-160 : export Excel de contrôle ad-hoc (règlements sélectionnés + factures à déclarer +
    /// détail TVA), disponible dès que la déclaration existe (EnCours ou Clôturée) — contrairement
    /// à <c>/generation</c> (réservé à Clôturée). Généré à la volée (flux en mémoire), aucun
    /// fichier écrit sur disque. Ne remplace pas les grilles interactives du front (décision actée
    /// TASK-010 §1sexies) ni l'export de dépôt existant (TASK-010/155, endpoints inchangés).
    /// </summary>
    [HttpGet("{id}/export-controle")]
    public async Task<IActionResult> ExportControle(Guid id)
    {
        var declaration = await _repository.GetByIdAsync(id);
        if (declaration == null) return NotFound();

        if (!EstSocieteAutorisee(declaration.SocieteId))
            return Forbid();

        var bytes = await _workflowService.GenererExcelControleAsync(id);
        var fileName = $"{declaration.Numero}-export-controle.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

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

    /// <summary>
    /// TASK-094 (Option A) — diagnostic lecture seule d'un tampon DT_Id observé sur
    /// RT_AFFECTATION : identifie sa déclaration d'origine si elle existe encore, ou signale
    /// explicitement un tampon orphelin (déclaration disparue). Réservé UT_Admin=1 (même garde
    /// que réouverture/suppression, TASK-073) — aucune écriture, uniquement recalcul et lecture.
    /// </summary>
    /// <param name="dtIds">
    /// Liste optionnelle de DT_Id (CSV, ex. "425466408,12345"). Absente → diagnostique toutes
    /// les valeurs DT_Id distinctes actuellement présentes sur RT_AFFECTATION.
    /// </param>
    [HttpGet("/api/diagnostic/dt-id")]
    public async Task<IActionResult> DiagnostiquerDtId([FromQuery] string? dtIds)
    {
        if (!User.HasClaim("UT_Admin", "1"))
            return Forbid();

        List<int>? cibles = null;
        if (!string.IsNullOrWhiteSpace(dtIds))
        {
            cibles = dtIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => int.TryParse(s, out var v) ? (int?)v : null)
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToList();
        }

        var resultats = await _workflowService.DiagnostiquerDtIdAsync(cibles);
        return Ok(resultats);
    }

    /// <summary>
    /// TASK-190 : backfill rétroactif DateFacture/Reference sur les lignes DM_LGTVA déjà figées des
    /// déclarations EnCours (jamais Clôturée/Déposée). Écriture de masse ciblée (2 colonnes
    /// uniquement) — déclenchement manuel exclusivement, réservé UT_Admin=1 (même garde que
    /// réouverture/suppression/diagnostic DT_Id, TASK-073/079/094). Jamais appelé automatiquement au
    /// chargement d'une déclaration.
    /// </summary>
    [HttpPost("/api/admin/backfill-datefacture-reference")]
    public async Task<IActionResult> BackfillDateFactureEtReference()
    {
        if (!User.HasClaim("UT_Admin", "1"))
            return Forbid();

        var rapport = await _workflowService.BackfillDateFactureEtReferenceAsync();
        return Ok(rapport);
    }
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

/// <summary>TASK-173 : même sélection que BulkUpdateEtatRequest, appliquée au code activité.</summary>
public class BulkUpdateCodeActiviteRequest
{
    public string? CodeActivite { get; set; }
    public List<Guid>? LigneIds { get; set; }
    public string? Domaine { get; set; }
    public string? Filter { get; set; }
}

/// <summary>TASK-176 — même sélection que BulkUpdateEtatRequest, appliquée à la resynchronisation en masse.</summary>
public class BulkResynchroniserRequest
{
    public List<Guid>? LigneIds { get; set; }
    public string? Domaine { get; set; }
    public string? Filter { get; set; }
}

/// <summary>TASK-078 — cible une pièce Sage (EC_Id) pour valider/resynchroniser une incohérence.</summary>
public class ValiderIncoherenceRequest
{
    public int EcId { get; set; }
}

/// <summary>TASK-161 — corps de la surcharge manuelle de code activité par ligne.</summary>
public class UpdateCodeActiviteRequest
{
    public string? CodeActivite { get; set; }
}

public class SocieteDto
{
    public int SoId { get; set; }
    public string RaisonSociale { get; set; } = "";
}

