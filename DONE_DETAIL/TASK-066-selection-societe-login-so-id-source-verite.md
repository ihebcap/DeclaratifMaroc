# TASK-066 — Sélection de société au login (combobox `P_SOCIETE`) + `SO_Id` comme clé unique de toutes les requêtes

## Contexte
Aujourd'hui le contexte « société » n'a **aucune source de vérité** et repose sur des **valeurs codées en dur incohérentes** :
- `/api/auth/login` renvoie un `societeId` figé `"001"` (correctif provisoire TASK-diagnostic 13/07, `AuthController.cs:50-59`).
- `/api/societes` renvoie un unique `{ Id = "001", Nom = "Société Test" }` figé (`DeclarationsController.cs:247`).
- Le front n'a **pas d'écran de choix** : `Auth.tsx:21-22` prend `res.data.societeId || '001'` et `societeName || 'GR_EMA_DISTRIBUTION'`.
- Les 2 déclarations réelles ont été persistées avec `SocieteId = '001'` (chaîne paddée), alors que la vraie clé société en base est **`P_SOCIETE.SO_Id = 1` (entier)**, `SO_RaisonSocial = 'NEW_EMA DISTRIBUTION'`.

Conséquence : `'001'` est une valeur **inventée** qui ne correspond à **aucune** clé métier. Le module fonctionne « par accident » tant qu'il n'y a qu'une société et que le même littéral est répété partout.

Demande PO (13/07/2026) :
1. L'**écran de login** doit exposer un **combobox** listant les raisons sociales via `SELECT SO_Id, SO_RaisonSocial FROM P_SOCIETE`.
2. `SO_Id` est **la clé** — **plus aucune valeur en dur** type `'001'`.
3. Toutes les requêtes doivent être **filtrées par `SO_Id`**.

## Périmètre STRICT
- **Inclus** :
  - Endpoint back listant les sociétés depuis `P_SOCIETE` (`SO_Id`, `SO_RaisonSocial`), remplaçant le `/api/societes` figé.
  - Front : combobox de sélection de la société sur l'écran de login (`Auth.tsx`), `SO_Id` sélectionné porté comme `societeId` dans la session.
  - Suppression de **tous** les littéraux de société en dur (`"001"`, `"Société Test"`, `'GR_EMA_DISTRIBUTION'` par défaut, `Selection.tsx` `'1'` si non supprimé).
  - Audit + ajout du filtre `SO_Id` sur **toutes** les requêtes de données qui doivent être bornées à une société (voir Étape 4).
  - Réconciliation des **données existantes** : les 2 déclarations `SocieteId='001'` doivent être ramenées à la vraie clé `SO_Id` (décision de format à trancher, cf. Risques).
- **Exclu** :
  - Toute vraie authentification / gestion de comptes ↔ sociétés (le login reste mock : n'importe quel user + mdp `admin`). La société vient du combobox, pas d'un mapping compte→société.
  - Multi-société **simultanée** (bascule à chaud, agrégation cross-société) — hors scope, une session = une société.
  - Modification du schéma Sage/`P_SOCIETE` (lecture seule).

## Cause racine
Aucune donnée d'entrée « société » n'est demandée à l'utilisateur ni lue depuis `P_SOCIETE` ; un littéral `'001'` sans réalité métier a été propagé comme défaut à plusieurs endroits indépendants (login, `/api/societes`, fallback front), et les requêtes supposent une mono-société implicite plutôt qu'un filtre `SO_Id` explicite.

## Objectif
```
Entrée : au login, l'utilisateur choisit une société dans un combobox alimenté par P_SOCIETE
Traitement : le SO_Id choisi devient l'unique identifiant société de la session ; toutes les requêtes filtrent sur ce SO_Id
Sortie : plus aucun littéral société en dur ; les écrans (déclarations, rapprochement, factures, valorisation) ne montrent que les données du SO_Id sélectionné
```

## Étapes
1. **Back — endpoint sociétés** : remplacer `/api/societes` (`DeclarationsController.cs:244-247`) par une lecture réelle `SELECT SO_Id, SO_RaisonSocial FROM P_SOCIETE` (via `IDbConnectionFactory.CreateGrfConnection()`), retour `[{ soId, raisonSociale }]`. Trier par `SO_RaisonSocial`.
2. **Back — login** : retirer le `SocieteId="001"`/`SocieteName="GR_EMA_DISTRIBUTION"` en dur de `AuthController.cs:50-59`. Le login ne connaît plus la société ; elle est fournie par le front (combobox) et transmise dans chaque requête. (Alternative si un `SO_Id` doit être signé dans le JWT : à trancher — cf. Risques.)
3. **Front — combobox login** : `Auth.tsx` charge `/api/societes` au montage, affiche un `<select>` (raison sociale), stocke le `SO_Id` choisi dans `user.societeId` et la raison sociale dans `user.societeName`. Supprimer les fallbacks `|| '001'` / `|| 'GR_EMA_DISTRIBUTION'`. Bloquer la connexion tant qu'aucune société n'est choisie.
4. **Filtre `SO_Id` sur toutes les requêtes** : auditer et ajouter le filtre société là où il manque. Points de départ (grep `SocieteId`/`societeId`) : `DeclarationRepository.GetAllAsync` (déjà filtré), création (`DeclarationWorkflowService`, `CreateDeclarationRequest.SocieteId`), et **surtout** les requêtes rapprochement / factures / sélection règlements / valorisation (`SelectionnerAffectationsService`, `SelectionExpliqueeService`, `RapprochementFromWhere`, endpoints Factures) qui supposent une mono-société implicite. Chaque endpoint recevant des données doit recevoir et propager `soId`.
5. **Réconciliation données** (format tranché = `INT`, cf. Risques) : script SQL idempotent — (a) `UPDATE DM_ENTTVA SET SocieteId = 1 WHERE SocieteId = '001'` (ramène les 2 déclarations vers `SO_Id=1`), puis (b) `ALTER COLUMN SocieteId INT` gardé par `sys.types`. Ordre impératif : réconcilier **avant** l'ALTER. Vérifier qu'aucune donnée existante ne devient orpheline.
6. **Supprimer le code mort** `Selection.tsx` (défaut `'1'`, non importé) ou l'aligner s'il est réactivé.
7. **Vérification bout-en-bout** : login → choix « NEW_EMA DISTRIBUTION » (`SO_Id=1`) → les 2 déclarations réconciliées apparaissent ; aucune requête ne renvoie de données d'une autre société.

## Livrables
- Endpoint `/api/societes` réel (lecture `P_SOCIETE`), `AuthController` sans littéral société, combobox login fonctionnel.
- Filtre `SO_Id` présent et prouvé sur toutes les requêtes de données bornées société.
- Script de réconciliation des déclarations existantes.
- `VERIFY/TASK-066_verify.md` : preuve réelle (`.\sql2022` / `GR_EMA_DISTRIBUTION`) — combobox alimenté depuis `P_SOCIETE`, login avec `SO_Id=1`, les 2 déclarations visibles après réconciliation, capture d'au moins une requête data (rapprochement ou factures) démontrant le filtre `SO_Id`, et absence de tout littéral `"001"` résiduel (grep).

## Critères de validation
- Le login affiche un combobox listant `SO_RaisonSocial` issu de `P_SOCIETE` ; aucune société ⇒ connexion impossible.
- `SO_Id` sélectionné = unique identifiant société de la session ; **zéro** littéral `'001'`/`'Société Test'`/défaut `'GR_EMA_DISTRIBUTION'` dans le code (grep vide).
- Toutes les requêtes de données bornées société filtrent sur `SO_Id` (preuve par endpoint).
- Les 2 déclarations existantes restent accessibles après réconciliation (aucune perte).
- Build back + front à 0 erreur ; tests existants verts.

## Risques / dépendances
- **Format canonique de `SO_Id` — TRANCHÉ (PO 13/07/2026) : `INT`.** `P_SOCIETE.SO_Id` est un entier ; la clé société est donc un **`INT`** de bout en bout (option (b)). Conséquences à implémenter :
  - **Base** : migrer la colonne `DM_ENTTVA.SocieteId` de `NVARCHAR` → `INT` (bloc idempotent gardé par `sys.columns`/`sys.types`), **après** le renommage TASK-065.
  - **Back** : `CreateDeclarationRequest.SocieteId` `string → int` ; `DeclarationsController.GetAll([FromQuery] string societeId ...)` `string → int` ; `IDeclarationRepository.GetAllAsync` / `CreateAsync` et les littéraux SQL alignés sur un paramètre `int`.
  - **Front** : `User.societeId` `string → number` (`App.tsx:18`), valeur du `<select>` = `SO_Id` numérique ; supprimer tout `|| '001'`.
  - **Réconciliation** : les 2 déclarations `SocieteId = '001'` (chaîne) → `1` (int) — `UPDATE DM_ENTTVA SET SocieteId = 1 WHERE SocieteId = '001'` **avant** l'`ALTER COLUMN`, sinon la conversion `'001'`→int échoue/étonne. Ordonner : reconcilier les valeurs, puis `ALTER COLUMN ... INT`.
- **JWT** : décider si `SO_Id` doit être une claim signée (empêche un client de réclamer les données d'une autre société) ou reste un simple paramètre de requête (mock actuel). En mock, un paramètre suffit ; à durcir avec la vraie auth (dette séparée).
- **Numérotation des déclarations** : `numero` = `TVA{societeName}-{exercice}-{periode}` (`CreateDeclarationModal.tsx:43`). Avec la raison sociale réelle, le numéro deviendrait `TVANEW_EMA DISTRIBUTION-...`. Décider du libellé de numérotation (code société court plutôt que raison sociale ?) — à cadrer, potentiellement tâche distincte.
- **Séquencement** : cohérent avec TASK-065 (renommage `DM_ENTTVA`) et TASK-061 (types `DECIMAL`) — même table `DeclarationEntete`. Séquencer la réconciliation après ces DDL si retenues.
- Annule/remplace le correctif provisoire du 13/07 (`AuthController` `SocieteId="001"` en dur) : cette TASK est la solution de fond.
