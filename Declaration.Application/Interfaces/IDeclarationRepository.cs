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
    /// TASK-168 : <paramref name="rechercheNumero"/>/<paramref name="rechercheReference"/> optionnels
    /// (préfixe, LIKE @x + '%') appliqués AVANT le TOP 500 — rend la recherche exhaustive au-delà
    /// des 500 premières valeurs alphabétiques, sans retirer le plafond par défaut.
    /// </summary>
    Task<FactureInterrogationDistincts> GetFacturesInterrogationDistinctsAsync(
        int soId, DateTime dateDebut, DateTime dateFin,
        string? rechercheNumero = null, string? rechercheReference = null);

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
    /// Parmi les <paramref name="ecIds"/> fournis (pour la société <paramref name="soId"/>),
    /// retourne ceux actuellement marqués en erreur dans le cache de ventilation (sentinelle
    /// <c>CodeTaxe='ERREUR'</c>, TASK-072/076). Ne duplique pas la règle de détection : lit
    /// seulement le résultat déjà écrit par l'orchestrateur (<c>DM_VENTILATION_SAGE_CACHE</c>,
    /// PersistenceConnection). Utilisé pour détecter qu'une ligne déjà figée dans DM_LGTVA est
    /// devenue incohérente après coup. TASK-118 : borné à <paramref name="soId"/> — EC_Id seul
    /// peut collisionner entre deux bases Sage physiquement distinctes.
    /// </summary>
    Task<HashSet<int>> GetEcIdsEnErreurAsync(int soId, IEnumerable<int> ecIds);

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

    // ─── Diagnostic tampon DT_Id (TASK-094, lecture seule stricte) ─────────────
    /// <summary>
    /// Toutes les déclarations existantes, quelle que soit la société — nécessaire pour
    /// recalculer <c>DeriveDtId</c> sur l'ensemble du référentiel lors d'un diagnostic
    /// (contrairement à <see cref="GetAllAsync"/>, borné à une société pour l'écran liste).
    /// Admin uniquement (garde posée par l'appelant).
    /// </summary>
    Task<IEnumerable<DeclarationEntete>> GetToutesDeclarationsAsync();

    /// <summary>
    /// Valeurs distinctes de <c>RT_AFFECTATION.DT_Id</c> (non nulles) présentes en base GRF —
    /// SELECT strictement lecture seule (GrfConnection), aucune écriture. Sert de périmètre par
    /// défaut au diagnostic quand aucun <c>DT_Id</c> n'est fourni explicitement.
    /// </summary>
    Task<IEnumerable<int>> GetDistinctDtIdsAffectationsAsync();

    /// <summary>
    /// TASK-094 (Option B) — persiste (ou efface, <paramref name="dtId"/> = null) le tampon
    /// <c>DT_Id</c> sur <c>DM_ENTTVA.DT_Id</c> pour cette déclaration. Posé à la clôture
    /// (<see cref="Declaration.Application.Services.DeclarationWorkflowService.CloturerDeclarationAsync"/>)
    /// avec la même valeur que celle tamponnée sur <c>RT_AFFECTATION</c>, effacé à la réouverture
    /// (symétrique de <see cref="DetamponnerAffectationsAsync"/>). Base de persistance
    /// (PersistenceConnection) — jamais GRF.
    /// </summary>
    Task SetDtIdDeclarationAsync(Guid declarationId, int? dtId);

    // TASK-097 : Gestion de la sélection des règlements
    Task SaveSelectionReglementsAsync(Guid declarationId, IEnumerable<string> selectedNumeroReglements);
    Task<List<string>> GetSelectionReglementsAsync(Guid declarationId);

    // ─── Diagnostic explicatif en ligne d'une anomalie (TASK-144, lecture seule stricte) ──────
    /// <summary>
    /// TASK-144 : échéance RT_ECHEANCE (base GRF) consultée — identité Sage exacte (EC_No, DO_Numero,
    /// tiers RT_ECHEANCE, montant devise). SELECT strictement lecture seule. Null si l'EC_Id est
    /// introuvable pour cette société.
    /// </summary>
    Task<EcheanceDiagnosticRow?> GetEcheanceDiagnosticAsync(int soId, int ecId);

    /// <summary>
    /// TASK-144 : autres échéances RT_ECHEANCE (même société) partageant le MÊME DO_Numero que
    /// l'échéance consultée — collision potentielle de numéro de pièce entre tiers (mémoire
    /// grf-do-numero-collision-multi-tiers). Inclut l'échéance consultée elle-même. Lecture seule.
    /// </summary>
    Task<IReadOnlyList<EcheanceCollisionRow>> GetEcheancesMemeDoNumeroAsync(int soId, string doNumero);

    /// <summary>
    /// TASK-144 : motif d'échec déjà persisté pour cet EC_Id dans le cache de ventilation
    /// (DM_VENTILATION_SAGE_CACHE.MotifErreur, sentinelle CodeTaxe='ERREUR'). Réutilise le résultat
    /// de la dernière lecture OM — ne redéclenche JAMAIS une lecture Sage. Null si aucune sentinelle.
    /// </summary>
    Task<string?> GetMotifErreurCacheAsync(int soId, int ecId);

    /// <summary>
    /// TASK-144 : présence d'un document de règlement Sage réel (table F_DOCREGL, base Sage résolue
    /// dynamiquement par SO_Id — la chaîne est fournie par l'appelant, jamais codée en dur) pour les
    /// EC_No fournis (jointure EC_No = DR_No, cf. AUDIT-TASK-143). SELECT strictement lecture seule
    /// sur la base Sage. Clé du dictionnaire = EC_No (= DR_No) ; un EC_No absent = échéance orpheline.
    /// </summary>
    Task<IReadOnlyDictionary<int, DocumentReglementSageRow>> GetDocumentsReglementSageAsync(
        string sageConnectionString, IEnumerable<int> ecNos);

    // ─── Recalcul d'une ligne périmée depuis un cache déjà relu (TASK-147) ─────
    /// <summary>
    /// TASK-147 : dernière lecture connue du cache (succès OU erreur) pour cet EC_Id — sert à
    /// détecter un cache PÉRIMÉ (relu avec succès après la création de la déclaration). Ne
    /// redéclenche AUCUNE lecture Sage.
    /// </summary>
    Task<CacheLectureRow?> GetDerniereLectureCacheAsync(int soId, int ecId);

    /// <summary>
    /// TASK-147 : buckets de taux déjà lus avec succès (CodeTaxe &lt;&gt; 'ERREUR') pour cet EC_Id —
    /// sert à reconstruire la ligne candidate sans redéclencher de lecture Sage.
    /// </summary>
    Task<IReadOnlyList<CacheBucketRow>> GetBucketsCacheAsync(int soId, int ecId);

    /// <summary>
    /// TASK-147 : supprime la/les ligne(s) DM_LGTVA périmée(s) d'un EC_Id avant reconstruction
    /// depuis le cache à jour.
    /// </summary>
    Task SupprimerLignesParEcIdAsync(Guid declarationId, int ecId);

    // ─── Identité fiscale société (TASK-155, export XML/Excel) ─────────────────
    /// <summary>
    /// TASK-155 : identifiant fiscal réel de la société (<c>P_SOCIETE.SO_Identifiant</c>, base GRF)
    /// — distinct du <c>SO_Id</c> interne. Null si société introuvable ou colonne vide ; l'appelant
    /// doit bloquer explicitement la génération plutôt que d'exporter une valeur par défaut
    /// (cf. TASK-151, aucune valeur placeholder).
    /// </summary>
    Task<string?> GetIdentifiantFiscalSocieteAsync(int soId);

    // ─── Code activité TVA — référentiel (TASK-161, lecture seule GRF) ─────────
    /// <summary>
    /// TASK-161 : référentiel complet des codes activité (<c>P_DECTVAACTIVITE</c>), lecture seule
    /// — alimente la liste déroulante de sélection manuelle côté front. Table non scopée par
    /// société (aucune colonne SO_Id, vérifié sur le schéma réel).
    /// TASK-172 : <paramref name="domaine"/> optionnel ("Encaissement"/"Decaissement", mêmes
    /// libellés que <c>DM_LGTVA.Domaine</c>) filtre sur <c>DTA_Domaine</c> (1/2, vérifié en base
    /// réelle — aucune autre valeur ni NULL) ; null = référentiel complet, non filtré.
    /// </summary>
    Task<IReadOnlyList<CodeActiviteReferentielRow>> GetReferentielCodesActiviteAsync(string? domaine = null);

    /// <summary>
    /// TASK-172 §4 : domaine (1/2) du code activité <paramref name="codeActivite"/> dans
    /// <c>P_DECTVAACTIVITE</c> — null si le code n'existe pas dans le référentiel. Sert à valider
    /// côté serveur qu'un code affecté à une ligne correspond bien à son domaine.
    /// </summary>
    Task<int?> GetDomaineCodeActiviteAsync(string codeActivite);

    /// <summary>
    /// TASK-172 §4 : domaine ("Encaissement"/"Decaissement") de la ligne <paramref name="ligneId"/>
    /// (<c>DM_LGTVA.Domaine</c>) — null si la ligne n'existe pas. Connexion PersistenceConnection,
    /// distincte de <see cref="GetDomaineCodeActiviteAsync"/> (GrfConnection) : jamais de jointure
    /// cross-base (TASK-154), la comparaison se fait en mémoire côté appelant.
    /// </summary>
    Task<string?> GetDomaineLigneAsync(Guid ligneId);

    /// <summary>
    /// TASK-161 : surcharge manuelle du code activité d'UNE ligne précise (<paramref name="ligneId"/>
    /// = <c>DM_LGTVA.Id</c>, jamais par EC_Id — contrairement à <see cref="ValiderIncoherenceAsync"/>,
    /// une même facture peut porter deux lignes de taux différents avec deux activités différentes,
    /// cas confirmé PO). Trace qui/quand, même pattern que TASK-078.
    /// </summary>
    Task UpdateCodeActiviteLigneAsync(Guid ligneId, string codeActivite, string utilisateur);

    /// <summary>
    /// TASK-173 : domaines distincts portés par ces lignes (<c>DM_LGTVA.Domaine</c>) — sert à
    /// détecter une sélection mixte Encaissement/Décaissement avant une affectation en masse du
    /// code activité (l'appelant bloque si le résultat contient plus d'une valeur). Scopé par
    /// <paramref name="declarationId"/> (correctif rejet architecte 24/07/2026) : un ligneId
    /// n'appartenant pas à cette déclaration est ignoré, jamais pris en compte.
    /// </summary>
    Task<IReadOnlyList<string>> GetDomainesDistinctsLignesAsync(Guid declarationId, IEnumerable<Guid> ligneIds);

    /// <summary>
    /// TASK-173 : affectation en masse du code activité par liste explicite d'IDs — même
    /// traçabilité qui/quand que <see cref="UpdateCodeActiviteLigneAsync"/>, écriture SQL batch
    /// (pas de boucle applicative), patron = <see cref="UpdateLignesEtatBulkByIdsAsync"/>. Scopé
    /// par <paramref name="declarationId"/> (correctif rejet architecte 24/07/2026) : un ligneId
    /// d'une autre déclaration n'est jamais écrit, même s'il figure dans la liste fournie —
    /// empêche de contourner le garde-fou de clôture (vérifié uniquement sur la déclaration de
    /// l'URL) via des IDs appartenant à une déclaration Clôturée.
    /// </summary>
    Task UpdateCodeActiviteBulkByIdsAsync(Guid declarationId, IEnumerable<Guid> ligneIds, string codeActivite, string utilisateur);

    /// <summary>
    /// TASK-173 : affectation en masse du code activité par domaine + filtre texte (fournisseur/
    /// facture) — même mécanique de sélection que <see cref="UpdateLignesEtatBulkAsync"/> (TASK-012),
    /// écriture SQL batch, même traçabilité qui/quand que la surcharge unitaire TASK-161.
    /// </summary>
    Task UpdateCodeActiviteBulkAsync(Guid declarationId, string domaine, string? filter, string codeActivite, string utilisateur);
}
