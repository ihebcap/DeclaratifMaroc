# TASK-161 — Code activité TVA : défaut par tiers (P_SOCIETECODEACTIVITETIERS, lecture seule) + surcharge manuelle par ligne

## Contexte
Demande PO (23/07/2026, session d'analyse architecte) : dans l'ancien applicatif (GénéraFi), chaque code
taxe Sage (taux, ex. C20/D20) est lié à un code activité — modèle jugé **très difficile à maintenir**,
surtout sur un paramétrage Sage déjà en place chez le client. En dehors du fichier XML, l'utilisateur est
contraint de remplir un tableau de correspondance manuel.

**Cause racine identifiée (architecte)** : un code taxe est un *taux*, partagé par toutes les activités de
la société — le lien taxe→activité est donc structurellement un mapping many-to-many, à réentretenir à
chaque évolution du paramétrage Sage. L'axe stable est le **tiers** (fournisseur/client), pas le taux.

**Analyse de l'existant fournie par le PO** :
`D:\_vibe\apbs-gr_winform\analayse\RAPPORT-CODE-ACTIVITE-TVA.md` (module `Tresorerie.UIDeclarationTva`,
ancien applicatif WinForms `apbs-gr_winform`) — a révélé 3 tables déjà en base GRF (même `SO_Id` que
`P_SOCIETE`, connue de `Declaration.Infrastructure`) : `P_DECTVAACTIVITE` (référentiel des codes
activité), `P_SOCIETECODEACTIVITETIERS` (mapping tiers↔activité, **jamais branché sur la TVA jusqu'ici**),
`P_DECTVASOCTAXEACTIVITE` (mapping taxe↔activité, le mécanisme historiquement douloureux).

## Décisions PO actées en session (23/07/2026) — 5 points tranchés un par un
1. **Clé de `P_SOCIETECODEACTIVITETIERS`** : la table clé aujourd'hui le tiers sur un texte libre
   (`SCAT_ErpIntitule`, saisi manuellement, colonne UI "Correspondance ERP") plutôt que sur un identifiant
   stable. **Décision : ajouter le numéro tiers Sage** comme clé de matching fiable (ALTER additif,
   colonne nullable, `SCAT_ErpIntitule` conservée pour l'affichage/compatibilité) — évite qu'un renommage
   du tiers côté Sage casse silencieusement le mapping.
2. **Emplacement front** : **pas de nouvel écran web GRF**. Le paramétrage (référentiel des codes,
   mapping tiers→activité) continue de se faire via l'écran Trésorerie WinForms existant
   (`UcSocieteCodeActiviteTiers`) — GRF web reste en **lecture seule** sur ces tables.
3. **Comportement si aucune résolution** (tiers non affecté) : **non bloquant**. La ligne est déclarée
   quand même, `CodeActivite = ""` / `"(sans activité)"` dans les récaps, corrigible à tout moment.
   Justifié : le code activité n'entre **pas** dans le XML de dépôt DGI (vérifié, aucune occurrence dans
   `Declaration.Export.Xml`) — il n'alimente que les récaps internes. Reproduire le blocage de l'ancien
   applicatif sur un champ non exigé par le DGI serait contre-productif.
4. **`P_DECTVASOCTAXEACTIVITE` : écartée du périmètre.** Le PO a tranché : pas besoin de cette table.
   Le cas confirmé "une même facture peut porter deux codes activité différents" (mélange de lignes/taux)
   est couvert autrement (point 5) — **aucune réintroduction, même partielle, du mapping taxe→activité**.
5. **Cas "même facture, deux activités"** : résolu par **modification manuelle directe sur la ligne**,
   pas par une table de mapping. Le code activité de chaque ligne se pré-remplit avec le défaut tiers
   (`P_SOCIETECODEACTIVITETIERS`) ou, à défaut, la colonne Sage désignée — mais reste **éditable** dans
   l'écran de vérification/intégration (choix dans la liste `P_DECTVAACTIVITE`). Aucune nouvelle table :
   l'exception est une saisie ponctuelle sur la ligne concernée, stockée directement dans la colonne
   `CodeActivite` de `DM_LGTVA` — même pattern que les champs de validation manuelle déjà en place
   (`IncoherenceValidee`/`IncoherenceValideePar`/`IncoherenceValideeLe`, TASK-078).
6. **Client sans l'ancien applicatif WinForms** : `P_DECTVAACTIVITE` et `P_SOCIETECODEACTIVITETIERS`
   restant en lecture seule côté GRF, un futur client 100% GRF web (sans WinForms installé) n'aurait
   aucun moyen de les peupler. **Décision : dette tracée, non bloquante** — tous les clients TVA actuels
   viennent de `apbs-gr_winform` et gardent l'app installée ; pas de sur-ingénierie pour un cas
   hypothétique. À réévaluer le jour où un client 100% GRF web se présente réellement (voir TODO.md).

## Constat code
- `P_SOCIETE` et les 3 tables ci-dessus partagent le même `SO_Id`, dans la base lue via
  `_connectionFactory.CreateGrfConnection()` (`DeclarationRepository.cs:1506`) — aucune nouvelle chaîne de
  connexion nécessaire.
- GRF lit déjà `CodeActivite` par tiers depuis **Sage** (pas encore depuis `P_SOCIETECODEACTIVITETIERS`)
  via la colonne désignée par client (`P_SOCIETE.SO_ErpColNameCodeActiviteMarrocFournisseur`), branché
  dans `Declaration.Selection.SelectionExpliqueeEvaluator.cs:132` et
  `SelectionnerAffectationsService.cs:114`.
- **Gap 1, déjà documenté dans le code** : le chemin réellement utilisé par l'export de dépôt
  (`ConstruireModeleExportAsync`, TASK-155) passe par `LigneCandidate`, qui **ne porte pas** `CodeActivite`
  — `Declaration.Application/Services/DeclarationWorkflowService.cs:1170-1182` produit un seul bucket
  `""` en attendant ("gap connu déjà accepté"). Impacte aussi le recap de l'export de contrôle TASK-160.
- **Gap 2** : `DM_LGTVA` (`DeclarationTVA.sql:143`) n'a pas de colonne `CodeActivite` — rien n'est figé ni
  éditable manuellement aujourd'hui.
- `Declaration.Core.Model.LigneDeclarationEnrichie.CodeActivite`/`RecapParActivite` existent déjà
  (`Declaration.Core/Model.cs:24,55,86`) — aucun changement de modèle C# nécessaire à ce niveau.
- `apbs-gr_winform` : `SocieteCodeActiviteTiersRepository1.cs` clé ses requêtes sur
  `(SO_Id, SCAT_ErpIntitule)` — l'ajout d'une colonne numéro tiers y est additif (aucune requête existante
  cassée), mais **cette colonne ne sera renseignée que si l'écran WinForms est mis à jour pour la
  capturer** (dépendance cross-applicatif, voir Risques).

## Périmètre STRICT
- **Aucune nouvelle table de référentiel/mapping.** `P_DECTVAACTIVITE` et `P_SOCIETECODEACTIVITETIERS`
  sont lues telles quelles depuis la base GRF déjà connectée — **lecture seule** des deux côté GRF web.
- **Un seul ALTER additif** sur `P_SOCIETECODEACTIVITETIERS` (colonne numéro tiers, nullable) — idempotent,
  aucun retrait de colonne existante.
- **`P_DECTVASOCTAXEACTIVITE` : hors périmètre**, non lue, non réutilisée.
- **Résolution en cascade** du `CodeActivite` effectif d'une ligne, dans cet ordre strict, jamais
  bloquant :
  1. Surcharge manuelle sur la ligne (si l'utilisateur l'a modifiée dans l'écran de vérification/
     intégration, avant figeage) — couvre le cas "même facture, deux activités".
  2. `P_SOCIETECODEACTIVITETIERS` (matching par numéro tiers si renseigné, sinon par
     `SCAT_ErpIntitule`/nom tiers en repli) → défaut par tiers.
  3. Colonne Sage désignée (`SO_ErpColNameCodeActiviteMarrocFournisseur`, déjà câblée) → dernier repli ERP.
  4. `""` (jamais de valeur inventée, non bloquant — cf. `ConstructeurDeclaration.cs:129`,
     `"(sans activité)"` déjà en place ailleurs).
- **Combler le gap TASK-155** : faire porter le `CodeActivite` résolu par `LigneCandidate` jusqu'à
  `ConstruireModeleExportAsync`/`ConstruireModeleControleAsync` (TASK-160).
- **Persistance + édition** : ajouter la colonne `CodeActivite` à `DM_LGTVA` — remplie automatiquement
  (cascade) à la valorisation, **éditable manuellement** par l'utilisateur avant figeage (petit endpoint
  de mise à jour ciblée, même pattern que `IncoherenceValidee`, TASK-078), figée telle quelle au moment de
  la clôture (stable ensuite, même si le mapping tiers change après coup).
- **Exclu** : toute écriture dans Sage.
- **Exclu** : tout écran web GRF de gestion du référentiel/mapping (décision PO point 2) — paramétrage
  laissé à l'écran Trésorerie WinForms existant.
- **Exclu** : toute réintroduction, même partielle, d'un mapping taxe→activité.

## Positionnement / architecture
- `Declaration.Infrastructure` : nouvelle méthode repository **lecture seule** sur `P_SOCIETECODEACTIVITETIERS`
  (via `CreateGrfConnection()`, même connexion que `P_SOCIETE`) et sur `P_DECTVAACTIVITE` (pour peupler la
  liste déroulante de sélection manuelle côté front). Batchées (pas de JOIN SQL trois-parties, garde-fou
  TASK-154).
- `DeclarationWorkflowService`/`Declaration.Core` : la cascade de résolution doit être un point **unique**,
  réutilisé par tous les chemins qui construisent `CodeActivite` aujourd'hui dispersés
  (`SelectionExpliqueeEvaluator`, `SelectionnerAffectationsService`, `ConstruireModeleExportAsync` via
  `LigneCandidate`) — éviter une 2ᵉ définition divergente (même risque que l'historique
  TASK-103/108/112).
- `DeclarationTVA.sql` : `ALTER TABLE DM_LGTVA ADD CodeActivite ...` idempotent (pattern déjà en place,
  TASK-055/057) — **seul** changement de schéma côté base GRF pour la persistance.
- Migration séparée (ou coordonnée avec `apbs-gr_winform`) : `ALTER TABLE P_SOCIETECODEACTIVITETIERS ADD`
  colonne numéro tiers, nullable — additive, sans impact sur les requêtes WinForms existantes.
- Endpoint ciblé (ex. `PATCH .../lignes/{id}/code-activite`) pour la surcharge manuelle par ligne, dans
  l'écran ② Vérifier & Intégrer — même emplacement/pattern que la validation d'incohérence TASK-078.

## Étapes
1. Repository : lecture `P_DECTVAACTIVITE` (référentiel pour la liste déroulante) et
   `P_SOCIETECODEACTIVITETIERS` (défaut tiers, matching numéro puis nom en repli).
2. Migration additive `P_SOCIETECODEACTIVITETIERS` (colonne numéro tiers, nullable) — **coordonner avec
   `apbs-gr_winform`** pour que l'écran `UcSocieteCodeActiviteTiers` capture aussi ce numéro à la création/
   modification d'un mapping (sinon la colonne reste vide et le matching retombe sur le nom en repli,
   voir Risques).
3. Écrire la fonction unique de résolution en cascade (surcharge ligne > tiers > Sage > `""`).
4. Combler le gap TASK-155 : brancher cette résolution sur `LigneCandidate`/`ConstruireModeleExportAsync`.
5. Ajouter la colonne `CodeActivite` à `DM_LGTVA` ; endpoint de surcharge manuelle par ligne (écran ②).
6. Rebrancher `SelectionExpliqueeEvaluator`/`SelectionnerAffectationsService` sur la même fonction de
   résolution pour éviter toute divergence.
7. **Tests** : les 4 niveaux de la cascade isolément ; cas confirmé PO — une facture avec 2 lignes de
   taux différents, l'une modifiée manuellement vers une autre activité ; non-régression TASK-160 (recap
   par activité réel, plus le bucket `""`) ; persistance après figeage stable malgré un changement
   ultérieur du mapping tiers ; non-régression `apbs-gr_winform` (lecture additive côté GRF, aucune
   modification cassante des requêtes WinForms existantes).

## Livrables
- Méthodes repository (lecture `P_DECTVAACTIVITE`/`P_SOCIETECODEACTIVITETIERS`).
- Fonction unique de résolution en cascade (Core ou Application, à trancher en dev).
- Colonne `DM_LGTVA.CodeActivite` (auto-remplie + éditable manuellement avant figeage).
- Colonne numéro tiers additive sur `P_SOCIETECODEACTIVITETIERS` (+ coordination `apbs-gr_winform`).
- Endpoint de surcharge manuelle par ligne (écran ② Vérifier & Intégrer).
- Tests couvrant la cascade + le cas de mélange d'activités sur une même facture (via surcharge manuelle).
- `VERIFY/TASK-161_verify.md`.

## Critères de validation
- Cascade de résolution vérifiée par test unitaire sur les 4 niveaux.
- Aucune écriture dans Sage ni dans `P_DECTVAACTIVITE` (lecture seule confirmée dans le VERIFY).
- `P_DECTVASOCTAXEACTIVITE` non lue, non référencée dans le code livré.
- `CodeActivite` réellement porté jusqu'à l'export de dépôt (TASK-155) et l'export de contrôle (TASK-160).
- Persistance stable dans `DM_LGTVA` après figeage ; surcharge manuelle par ligne fonctionnelle et
  traçable (même rigueur que TASK-078).
- Test explicite du cas confirmé PO : une facture, deux codes activité différents, via surcharge manuelle
  sur une seule des deux lignes.
- Non-régression `apbs-gr_winform` : les écrans Trésorerie existants sur `P_SOCIETECODEACTIVITETIERS`/
  `P_DECTVAACTIVITE` continuent de fonctionner sans modification cassante.

## Risques / dépendances
- **Dépend de / impacte TASK-155** : comble un gap explicitement accepté à l'époque
  (`DeclarationWorkflowService.cs:1170-1182`).
- **Impacte TASK-160** (export de contrôle, livré) : le recap par activité, actuellement toujours vide en
  pratique, doit devenir réel — non-régression à vérifier explicitement sur le VERIFY de TASK-160.
- **Dépendance cross-applicatif, à coordonner avec le PO** : l'ALTER ajoutant le numéro tiers sur
  `P_SOCIETECODEACTIVITETIERS` n'a de valeur que si l'écran WinForms `UcSocieteCodeActiviteTiers` est
  également mis à jour pour le capturer à la saisie — sans ce second changement (hors périmètre du dépôt
  GRF), la colonne reste vide et le matching retombe sur `SCAT_ErpIntitule` (texte) pour tous les nouveaux
  mappings créés après cette task. À trancher : qui porte ce correctif côté `apbs-gr_winform` ?
- **Dette tracée, non bloquante** (décision PO point 6) : aucun écran GRF pour peupler
  `P_DECTVAACTIVITE`/`P_SOCIETECODEACTIVITETIERS` — à réévaluer si un client 100% GRF web (sans WinForms)
  se présente.
- **Risque de divergence** si la résolution en cascade n'est pas centralisée en un point unique
  (cf. historique TASK-103/108/112) — point d'attention VERIFY prioritaire.
