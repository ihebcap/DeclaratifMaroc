using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Declaration.Application.Entities;

namespace Declaration.Application.Interfaces;

public interface IDeclarationRepository
{
    Task<DeclarationEntete?> GetByIdAsync(Guid id);
    Task<DeclarationEntete?> GetByNumeroAsync(string numero);
    Task<IEnumerable<DeclarationEntete>> GetAllAsync(int societeId, int? exercice, StatutDeclaration? statut);
    Task CreateAsync(DeclarationEntete declaration);
    Task UpdateStatutAsync(Guid id, StatutDeclaration statut);
    Task<bool> ExistsAsync(int societeId, int exercice, int periode, TypePeriode type);

    /// <summary>
    /// TASK-079 : suppression physique d'une déclaration EnCours — supprime d'abord les lignes
    /// DM_LGTVA (ordre FK) puis l'entête DM_ENTTVA. La garde de statut (EnCours uniquement) est
    /// posée par l'appelant (DeclarationWorkflowService), pas ici.
    /// </summary>
    Task DeleteAsync(Guid declarationId);

    /// <summary>
    /// TASK-079 : agrégats (nb lignes déclarables + montant TVA) par déclaration, pour
    /// l'enrichissement de l'écran liste. Ne compte que les lignes Proposee/Integree (lignes
    /// valorisées réellement déclarables) — Exclue/Reportee/Ecartee portent TVA=0 (jamais
    /// valorisées), les inclure fausserait le nombre de lignes affiché.
    /// </summary>
    Task<Dictionary<Guid, (int NbLignes, decimal MontantTva)>> GetAgregatsListeAsync(IEnumerable<Guid> declarationIds);

    /// <summary>
    /// TASK-079 (garde-fou défensif §4) : nombre d'affectations RT_AFFECTATION déjà tamponnées
    /// (DT_Id non nul) parmi les mouvements portant ces numéros de rapprochement. Doit toujours
    /// retourner 0 pour une déclaration EnCours (rien n'est tamponné avant clôture, TASK-028) —
    /// vérifié avant suppression physique plutôt que supposé.
    /// </summary>
    Task<int> CountAffectationsTamponneesAsync(IEnumerable<string> numerosRapprochement);
    
    // Lignes candidates
    Task SaveLignesCandidatesAsync(IEnumerable<LigneCandidate> lignes);
    Task<IEnumerable<LigneCandidate>> GetLignesAsync(Guid declarationId, string domaine, int page, int pageSize, string? sort, string? filter);
    Task<int> GetLignesCountAsync(Guid declarationId, string domaine, string? filter);

    /// <summary>
    /// Valeurs distinctes des colonnes énumérables (numeroRapprochement, source, tauxTVA, origine,
    /// statutLigne) présentes dans les lignes candidates d'une déclaration/domaine, pour alimenter
    /// <c>filterType: 'list'</c> côté DomainGrid (TASK-067B). MÊME WHERE (DeclarationId + Domaine)
    /// que <see cref="GetLignesAsync"/>/<see cref="GetLignesCountAsync"/> — invariant TASK-040.
    /// </summary>
    Task<Dictionary<string, List<string>>> GetLignesDistinctsAsync(Guid declarationId, string domaine);

    Task UpdateLigneEtatAsync(Guid ligneId, EtatLigne nouvelEtat);
    Task UpdateLignesEtatBulkAsync(Guid declarationId, string domaine, string? filter, EtatLigne nouvelEtat);
    Task UpdateLignesEtatBulkByIdsAsync(IEnumerable<Guid> ligneIds, EtatLigne nouvelEtat);

    // ─── Interrogation « Rapprochement bancaire » globale (TASK-036) ────────────
    /// <summary>
    /// Retourne, en LECTURE SEULE, les règlements (pivot RT_MOUVEMENT.MV_Id) d'une société
    /// sur une période bornée, avec leurs affectations agrégées (nb + montant), le reste à
    /// affecter explicite, l'origine (EC_Type), l'état rapproché banque (MV_Point) et l'état
    /// déclaré (DT_Id, lu jamais modifié). Indépendant de toute déclaration.
    /// Filtres optionnels : mode (MV_Type), rapproché banque, déclaré, tiers (texte),
    /// numero (LIKE), origines[] et domaines[] (multi-sélection, TASK-040 — appliqués
    /// côté serveur au MÊME WHERE que le COUNT pour que TotalCount reste exact).
    /// </summary>
    Task<IEnumerable<ReglementRapprochementRow>> GetReglementsRapprochementAsync(
        int soId, DateTime dateDebut, DateTime dateFin,
        RapprochementFilter filter, int page, int size, string? sort);

    /// <summary>Nombre total de règlements correspondant aux mêmes filtres (pour pagination).</summary>
    Task<int> GetReglementsRapprochementCountAsync(
        int soId, DateTime dateDebut, DateTime dateFin, RapprochementFilter filter);

    /// <summary>
    /// Valeurs distinctes (modes, origines, et depuis TASK-067B : numéros de règlement, numéros
    /// d'extrait, codes banque) présentes sur la période, pour les filtres liste. Calculées sur le
    /// MÊME FromWhere/période que la liste principale (invariant TASK-040).
    /// </summary>
    Task<ReglementRapprochementDistincts> GetReglementsRapprochementDistinctsAsync(
        int soId, DateTime dateDebut, DateTime dateFin);

    // ─── Interrogation « Factures » (pivot facture, lecture seule — TASK-041) ───
    /// <summary>
    /// Retourne, en LECTURE SEULE, les factures fournisseur (pivot RT_ECHEANCE.EC_Id, DO_Domaine=Achat)
    /// d'une société sur une période bornée (borne sur DO_Date). Projette la famille A (identité facture)
    /// et la famille C (agrégat RT_AFFECTATION : Réglé, Déclaré = Σ affecté DT_Id non nul, statut dérivé).
    /// La famille B (HT/TVA) est enrichie ici depuis le cache de ventilation (TASK-024) quand présent,
    /// sinon « non valorisé » + motif (aucun appel Sage synchrone). Filtres optionnels : numero (LIKE),
    /// fournisseur (LIKE code/intitulé), reference (LIKE), origines[] (EC_Type dérivé), statuts[] (dérivé
    /// DT_Id) — appliqués au MÊME WHERE que le COUNT (leçon TASK-040 : compteur cohérent).
    /// </summary>
    Task<IEnumerable<FactureInterrogationRow>> GetFacturesInterrogationAsync(
        int soId, DateTime dateDebut, DateTime dateFin,
        IReadOnlyList<string>? numero, string? fournisseur, IReadOnlyList<string>? reference,
        IReadOnlyList<string>? origines, IReadOnlyList<string>? statuts,
        int page, int size, string? sort);

    /// <summary>Nombre total de factures correspondant aux mêmes filtres (pour pagination).</summary>
    Task<int> GetFacturesInterrogationCountAsync(
        int soId, DateTime dateDebut, DateTime dateFin,
        IReadOnlyList<string>? numero, string? fournisseur, IReadOnlyList<string>? reference,
        IReadOnlyList<string>? origines, IReadOnlyList<string>? statuts);

    /// <summary>
    /// Origines (EC_Type) distinctes, et depuis TASK-067B numéros de facture / références
    /// distincts, présents sur la période, pour le filtre liste. MÊME FromWhere que la liste
    /// principale (invariant TASK-040).
    /// </summary>
    Task<FactureInterrogationDistincts> GetFacturesInterrogationDistinctsAsync(
        int soId, DateTime dateDebut, DateTime dateFin);

    // ─── Tampon DT_Id (verrou d'intégration déclaration, TASK-028) ─────────────
    /// <summary>
    /// Pose le tampon DT_Id sur toutes les affectations RT_AFFECTATION correspondant aux
    /// numéros de rapprochement (MV_Numero) intégrés dans la déclaration.
    /// S'exécute sur la base GRF (GrfConnection), dans la même transaction que la clôture.
    /// </summary>
    Task TamponnerAffectationsAsync(int dtId, IEnumerable<string> numerosRapprochement);

    /// <summary>
    /// Efface le tampon DT_Id (→ NULL) sur les affectations portant ce <paramref name="dtId"/>
    /// ET rattachées aux mouvements de la déclaration (numéros de rapprochement fournis).
    /// Le bornage aux mouvements de la déclaration empêche qu'une collision de dtId dérivé
    /// libère le verrou d'une autre déclaration. Permet la réouverture : l'affectation
    /// redevient sélectionnable et modifiable.
    /// </summary>
    Task DetamponnerAffectationsAsync(int dtId, IEnumerable<string> numerosRapprochement);

    // ─── Revalidation des lignes figées (TASK-077, lecture seule stricte) ──────
    /// <summary>
    /// Parmi les <paramref name="ecIds"/> fournis, retourne ceux actuellement marqués en erreur
    /// dans le cache de ventilation (sentinelle <c>CodeTaxe='ERREUR'</c>, TASK-072/076). Ne
    /// duplique pas la règle de détection : lit seulement le résultat déjà écrit par
    /// l'orchestrateur (<c>GRC_VENTILATION_SAGE_CACHE</c>, PersistenceConnection). Utilisé pour
    /// détecter qu'une ligne déjà figée dans DM_LGTVA est devenue incohérente après coup.
    /// </summary>
    Task<HashSet<int>> GetEcIdsEnErreurAsync(IEnumerable<int> ecIds);

    /// <summary>
    /// État MV_Point courant (RT_MOUVEMENT, GrfConnection) pour les <paramref name="mvIds"/>
    /// fournis. Lecture seule stricte — aucune écriture RT_*. Un MV_Id absent du dictionnaire
    /// retourné signifie que le mouvement est introuvable (supprimé côté Sage/GRF). Utilisé pour
    /// détecter qu'un règlement rattaché à une ligne déjà figée a été dépointé depuis le
    /// figeage (TASK-077).
    /// </summary>
    Task<Dictionary<int, int?>> GetMvPointsActuelsAsync(IEnumerable<int> mvIds);

    /// <summary>
    /// Renseigne rétroactivement <c>EC_Id</c>/<c>MV_Id</c> sur une ligne déjà figée AVANT la
    /// migration 006 (valeurs 0 = inconnu à l'origine). Ne touche à AUCUNE colonne financière
    /// (HT/TVA/TTC/Etat) — uniquement les clés d'identification nécessaires à la revalidation
    /// TASK-077. Jamais appelée sur une déclaration clôturée (garde posée par l'appelant).
    /// </summary>
    Task UpdateLigneClesAsync(Guid ligneId, int ecId, int mvId);

    /// <summary>
    /// TASK-078 : trace la décision PO de valider une incohérence signalée (TASK-077), pour
    /// toutes les lignes portant cet EC_Id dans cette déclaration. N'écrit aucun montant/état.
    /// </summary>
    Task ValiderIncoherenceAsync(Guid declarationId, int ecId, string utilisateur);

    /// <summary>
    /// TASK-078 : réinitialise la validation d'incohérence (déclenché après une
    /// resynchronisation ayant modifié les faits sous-jacents).
    /// </summary>
    Task ReinitialiserValidationIncoherenceAsync(Guid declarationId, int ecId);

    // ─── Exclusivité inter-déclaration (TASK-080) ──────────────────────────────
    /// <summary>
    /// Pour la société donnée, retourne les clés (NumeroFacture|NumeroRapprochement) déjà
    /// Proposee/Integree dans une déclaration AUTRE que <paramref name="declarationIdExclue"/>
    /// (EnCours ou Cloturee — les deux statuts comptent, seule la suppression TASK-079 ou un
    /// changement d'état de la ligne concurrente libère la clé). Valeur = numéro de la première
    /// déclaration concurrente trouvée (déterministe via MIN), pour un message d'exclusion honnête.
    /// Base de persistance (DM_LGTVA/DM_ENTTVA) — aucun JOIN cross-base avec RT_* (GRF).
    /// </summary>
    Task<Dictionary<string, string>> GetConflitsAutreDeclarationAsync(int societeId, Guid declarationIdExclue);

    /// <summary>
    /// TASK-080 : supprime des lignes DM_LGTVA précises par Id (utilisé pour retirer des lignes
    /// Exclue devenues obsolètes — conflit inter-déclaration résolu — avant de les réintégrer via
    /// le même pipeline de figeage que l'initial).
    /// </summary>
    Task DeleteLignesAsync(IEnumerable<Guid> ligneIds);
}
