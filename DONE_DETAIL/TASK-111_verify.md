# VERIFY — TASK-111 — Drill « Détail des lignes » (②) : explosion de requêtes HTTP parallèles sur grosse sélection

> Implémentée en **worker exceptionnel** (demande explicite PO/architecte, 17/07/2026) — l'architecte
> ne code pas d'ordinaire (cf. `CLAUDE.md`). Ce document est soumis pour revue, pas auto-approuvé.

## Résumé

Front-only, un seul fichier modifié : `declaration-tva-web/src/AffectationsDrill.tsx`. Option 2 de
la task retenue (« la plus propre ») après vérification effective : le filtre `numeroRapprochement`
accepte déjà une liste côté back (`BuildLigneFilterWhere`/`DeclarationRepository.cs:407-415`,
TASK-067B) — remplacement du `Promise.all` non borné (1 requête/règlement, jusqu'à 150+) par un
regroupement **par domaine** (Encaissement/Decaissement) avec **un seul appel paginé par domaine**
(≤2 requêtes au total, quel que soit N). Aucune modification back nécessaire (le support multi-
valeurs existait déjà). Le chemin `readOnly` (`fetchAllLignesDeclaration`) n'est pas touché.

## Modifications

- **Supprimé** : `fetchLignesReglement` (1 requête HTTP par règlement) — devenu mort après le
  remplacement des deux seuls appelants.
- **Ajouté** : `fetchLignesPourReglements(declarationId, domaine, numeros)` — même boucle de
  pagination que l'ancienne fonction, mais avec `filter: JSON.stringify({ numeroRapprochement: numeros })`
  (liste, pas scalaire) : un seul appel serveur couvre tous les règlements d'un domaine.
- **Ajouté** : `fetchLignesSelection(declarationId, selectedRows)` — regroupe `selectedRows` par
  domaine (`getApiDomaine`), lance les fetches par domaine en parallèle (`Promise.all`, ≤2 branches),
  isole les échecs **par domaine** (`try/catch` individuel, jamais un échec qui efface l'autre
  domaine), et retourne `{ dataByReglement, alertes, echecs }` — `echecs` liste nommément les
  règlements dont le domaine a échoué.
- **Ajouté** : `echecsMessage(echecs)` — message d'erreur nommant le nombre et jusqu'à 10 numéros de
  règlement en échec (jamais le générique « Erreur lors du chargement des affectations » qui masquait
  lequel avait échoué).
- **Modifié** : `reload()` et le `useEffect` de montage (chemin non-`readOnly` uniquement) utilisent
  `fetchLignesSelection` et affichent `echecsMessage(echecs)` via `showToast('error')` si `echecs.length > 0`.
- **Non touché** : chemin `readOnly` (`fetchAllLignesDeclaration`), back (`DeclarationRepository.cs`,
  `DeclarationsController.cs`) — le support multi-valeurs du filtre existait déjà, aucune évolution
  de contrat nécessaire.

## Build

```
npx tsc --noEmit   → 0 erreur
npm run build      (tsc -b && vite build) → 0 erreur (seul avertissement Vite pré-existant,
                    INEFFECTIVE_DYNAMIC_IMPORT sur api.ts, sans lien avec ce changement)
```

## Tests Playwright — preuve réelle (base réelle `GR_EMA_DISTRIBUTION`, `reset.ps1`)

Nouveau fichier `tests/task111.spec.ts` (2 tests, mockage explicitement autorisé par la task :
150+ règlements réels non disponibles dans le jeu de données de test — sélection synthétique
injectée par interception de `GET /rapprochement`, comme déjà pratiqué dans TASK-102/105/110) :

| Test | Résultat |
|---|---|
| Grosse sélection (150 Décaissement + 10 Encaissement = 160 règlements) | ✅ passed |
| Échec réseau isolé sur le domaine Décaissement | ✅ passed |

**Test 1 — grosse sélection** : 160 règlements sélectionnés automatiquement (comportement par défaut
« coche tout » sur nouvelle déclaration) → clic « Détail des lignes » → **4 requêtes** `GET /lignes`
observées (2 domaines × 2, le facteur 2 restant venant du double-montage volontaire de
`React.StrictMode`, actif en dev — `main.tsx`; en production ce serait exactement 2). Avant
correctif : 160 requêtes, `ERR_INSUFFICIENT_RESOURCES`. Assertion `toBeLessThan(reglements.length)`
+ `toBeLessThanOrEqual(4)`. Les 160 lignes s'affichent intégralement (capture
`task111-grosse-selection.png`), aucun message d'erreur.

**Test 2 — échec isolé** : le domaine Décaissement (150 règlements) répond 500, Encaissement (10
règlements) répond normalement. Résultat observé : toast
« Échec du chargement pour 150 règlement(s) : RF-DEC-0000, RF-DEC-0001, … (+140 autre(s)) »
(nomme le nombre ET les numéros, jamais un message générique) — et la grille affiche malgré tout
les **10 lignes Encaissement** (capture `task111-echec-isole.png`) : l'échec d'un domaine n'efface
pas les données de l'autre.

Suite existante rejouée sans modification (aucune régression) :

| Test | Résultat |
|---|---|
| `tests/declaration.spec.ts` | ✅ passed |
| `tests/column-selector.spec.ts` | ✅ passed |
| `tests/task087.spec.ts` | ✅ passed |
| `tests/task088.spec.ts` (drill readonly ②) | ✅ passed |
| `tests/task096.spec.ts` | ✅ passed |
| `tests/task102.spec.ts` | ✅ passed |
| `tests/task107.spec.ts` (drill readonly, `DomainGrid`) | ✅ passed |
| `tests/task109.spec.ts` (drill non-readonly, petite sélection 5 règlements, ouvert en 1er) | ✅ passed |
| `tests/task110.spec.ts` (drill readonly, mise en page) | ✅ passed |

11/11 tests passés (suite complète). Petite sélection (5 règlements, TASK-109/107) : comportement
inchangé, une requête par domaine comme avant (à 5 règlements, l'ancien pattern n'avait jamais posé
de problème — seule la grosse sélection était bloquante).

## Critères de validation (task originale)

- [x] Une sélection de 150+ règlements distincts charge le drill sans aucune requête en échec
      réseau — 160 règlements testés, 0 échec, chargement intégral (test 1).
- [x] Aucune régression sur une petite sélection — `task107`/`task109` (5 règlements) toujours verts.
- [x] Aucune régression sur le chemin `readOnly` — `task088`/`task107`/`task110` toujours verts,
      `fetchAllLignesDeclaration` non touché.
- [x] En cas d'échec réseau isolé, l'utilisateur voit quels règlements n'ont pas pu être chargés —
      test 2, message nommant 150 règlements par leur numéro (tronqué à 10 + compteur, lisible sur
      une grosse sélection).

## Réserves (signalées, non bloquantes)

- **Granularité de l'isolation d'échec = domaine, pas règlement individuel** : avec la fusion en un
  seul appel par domaine, un échec réseau isolé sur UN règlement au milieu d'un domaine de 150 n'est
  plus distinguable d'un échec touchant les 150 — l'ancien code (1 requête/règlement) pouvait en
  théorie isoler un seul numéro en échec, mais c'est précisément ce pattern qui causait
  `ERR_INSUFFICIENT_RESOURCES`. Arbitrage assumé : la task autorise explicitement cette limitation
  (« se limiter à borner la concurrence » était l'option de repli si la fusion s'avérait
  impossible ; ici la fusion réussit et son seul coût est cette granularité, un compromis strictement
  meilleur que 150 requêtes parallèles). En pratique, un échec réseau frappe une requête entière
  (timeout, coupure), donc le cas « 1 règlement sur 150 échoue isolément » au sein d'un même appel
  HTTP est déjà rare avec l'ancien pattern (l'échec observé en réel était un échec de MASSE, pas
  isolé) — non régressif vis-à-vis du signalement d'origine.
- **Cas réel non rejoué** : la déclaration d'origine (`d80b6f0a-c09a-4086-9972-4223e12a0e73`,
  `DESKTOP-5BFKKEP`) n'a pas été retrouvée/rejouée telle quelle ; preuve apportée par sélection
  synthétique de taille comparable (160 vs 152 réels), conformément aux Livrables de la task
  (« mockée ou via jeu de données réel »).

## Statut

**Soumise pour revue** (worker exceptionnel — ne s'auto-approuve pas, conformément à la séparation
des rôles `CLAUDE.md`).
