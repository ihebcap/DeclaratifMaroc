# TASK-207 — Fix : dashboard "Erreur lors du chargement des déclarations" (paramètre `soId`/`societeId`)

> **Origine (PO, 07/08/2026)** : le PO a ouvert le front (`npm run dev`) après le retest de TASK-204
> et est tombé sur "Erreur lors du chargement des déclarations" au login. Reproduit et diagnostiqué
> par l'architecte via appel API direct.

## Constat

- `declaration-tva-web/src/DeclarationList.tsx:133` appelle :
  ```ts
  const res = await api.get('/declarations', { params: { soId: societeId } });
  ```
  Le paramètre de requête envoyé s'appelle `soId`.
- Le backend (`GET /api/declarations`) exige `societeId` — confirmé par appel direct :
  - `GET /api/declarations?soId=1` → `400 Bad Request`, `{ "message": "'societeId' est obligatoire." }`
  - `GET /api/declarations?societeId=1` → `200 OK`, liste réelle des déclarations.
- **Régression introduite par le commit TASK-204** (`842f9ef`, migration AG Grid) : `git show
  842f9ef^:declaration-tva-web/src/DeclarationList.tsx` montre que l'appel utilisait déjà
  correctement `{ params: { societeId } }` avant ce commit. La réécriture du fichier pendant la
  migration a introduit une faute de frappe sur le nom du paramètre.
- **Impact réel** : casse le chargement du dashboard pour **tout** utilisateur au login, pas
  seulement les tests automatisés — c'est le premier écran vu après connexion.

## Correction attendue

1. `DeclarationList.tsx:133` : remplacer `{ soId: societeId }` par `{ societeId }`.
2. **Ne pas faire de renommage aveugle** `soId` → `societeId` dans tout le dépôt. `soId` est le nom
   de paramètre correct pour de nombreux autres appels backend (ex. `FactureInterrogation.tsx`,
   `ReglementsSelection.tsx`, endpoints délai de paiement dans `api.ts`), confirmé par leurs
   contrôleurs respectifs. Seul `GET /api/declarations` exige `societeId`.
3. Pour tout autre appel touché par le commit `842f9ef` qui semble suspect, vérifier
   individuellement le nom de paramètre attendu côté contrôleur backend avant de le modifier — ne
   pas extrapoler depuis ce seul cas.

## Fichiers impactés

- [declaration-tva-web/src/DeclarationList.tsx:133](../declaration-tva-web/src/DeclarationList.tsx#L133)

## Critères de validation

- Login réel (Admin/Admin) → dashboard charge la liste des déclarations sans toast d'erreur.
- Appel direct `GET /api/declarations?societeId=<soId réel>` retourne `200` avec la liste (déjà
  vérifié par l'architecte, à revérifier après le fix par le worker).
- Aucune régression sur les autres appels utilisant `soId` (vérifier qu'aucun n'a été renommé par
  erreur — cf. point 2 ci-dessus).

## Dépendances / risques

- Indépendant de TASK-204 (toujours en cours de correction, 4 rejets) — ce fix peut être livré et
  vérifié séparément, sans attendre la clôture de TASK-204.
- Risque faible : changement d'une ligne, portée limitée à un seul appel réseau.
