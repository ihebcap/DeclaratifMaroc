# TASK-109 Verify — « Détail des lignes » (drill ②) peut apparaître vide alors que des règlements sont sélectionnés

> Preuves de bon fonctionnement du correctif (front-only) reproduisant le signalement PO
> (`TVA1-2026-01`, 145 règlements, capture écran 17/07/2026).

## Cause du dysfonctionnement (rappel)

1. Le compteur d'en-tête du drill (`{selectedRows.length} règlements`) est dérivé de l'état
   **front** `selectedRows` — toujours exact vis-à-vis de ce qui est coché à l'écran ①.
2. Le contenu de la grille vient de `GET /declarations/{id}/lignes`, qui déclenche
   `ChargerCandidatesSiNecessaireAsync` → `ConstruireLignesFigeesAsync` au tout premier appel pour
   ce (déclaration, domaine). Cette construction filtre sur la **sélection persistée côté serveur**
   (`GetSelectionReglementsAsync`), jamais sur `selectedRows` (TASK-097 : jamais « tous les
   candidats » par défaut si rien n'est encore persisté).
3. Seul `handlePasserAuCalcul` (bouton « Passer au calcul ») persistait cette sélection
   (`POST /declarations/{id}/selection`). Le bouton « Détail des lignes » (TASK-092) ouvrait le
   drill sans jamais forcer ce `POST` au préalable.
4. **Conséquence** : si le tout premier appel `/lignes` pour ce domaine survenait via « Détail des
   lignes » — donc avant que « Passer au calcul » ait persisté la sélection réellement cochée —
   `ConstruireLignesFigeesAsync` figeait et persistait **zéro ligne**. La grille restait vide avec
   le message trompeur « Aucune affectation ne correspond aux filtres » alors qu'aucun filtre
   n'était actif.

## Solution apportée (front-only, périmètre strict de la TASK)

- **`DeclarationStepper.tsx`** : extraction de la logique de persistance de sélection
  (`persisterSelection`, même appel `POST /declarations/{id}/selection` que
  `handlePasserAuCalcul`) et ajout de `handleOuvrirDrill`, appelé désormais par le bouton
  « Détail des lignes » — la sélection cochée est persistée **avant** `setShowDrill(true)`, sans
  changer d'étape ni contourner le garde-fou serveur TASK-097 (le serveur reste seul juge de ce
  qui est éligible ; on s'assure seulement qu'il voit la bonne sélection au bon moment). Bouton
  également désactivé pendant l'appel (`disabled={... || loading}`), comme « Passer au calcul ».
- **`AffectationsDrill.tsx`** : le message de grille vide distingue désormais explicitement
  `activeFilterCount === 0` (« Aucune affectation trouvée pour les règlements sélectionnés. ») de
  `activeFilterCount > 0` (« Aucune affectation ne correspond aux filtres. », inchangé) — plus
  jamais le message « filtres » quand aucun filtre n'est actif.
- Aucun changement côté back (`DeclarationWorkflowService`, `DeclarationsController`) : le garde-fou
  TASK-097 (jamais tous les candidats par défaut) reste intact, on corrige uniquement le moment où
  la sélection front est reflétée côté serveur.

## Validation automatique (Test Playwright)

Nouveau test `tests/task109.spec.ts`, calqué sur le scénario PO :
1. Connexion, création d'une **nouvelle** déclaration (jamais visitée — aucun appel `/lignes`
   réussi au préalable pour aucun domaine).
2. Sélection de règlements réellement éligibles à l'écran ①.
3. Clic **direct** sur « Détail des lignes » — sans jamais passer par « Passer au calcul ».
4. Assertion : la grille du drill n'affiche ni l'ancien message « filtres » ni le nouveau message
   honnête (donc au moins une ligne chargée), et le compteur de lignes n'est pas à 0.

### Commande de test exécutée
```powershell
npx playwright test task109.spec.ts --reporter=list
```

### Résultat de la console
```text
Running 1 test using 1 worker
Lignes affichées dans le drill (premier appel): 12 lignes
Finished TASK-109 test!
  ok 1 [chromium] › tests\task109.spec.ts:23:1 › Test TASK-109: Détail des lignes ouvert en tout premier reflète la sélection réelle (9.9s)

  1 passed (17.3s)
```

12 lignes chargées et affichées dès le tout premier appel (avant tout clic sur « Passer au
calcul ») — reproduit fidèlement l'attendu (« la grille reflète la sélection réelle, pas 0 ligne
par défaut »).

## Non-régression (suite existante rejouée)

```powershell
npx playwright test task096.spec.ts declaration.spec.ts task087.spec.ts task088.spec.ts task102.spec.ts --reporter=list
```
- `task096.spec.ts` (total sélectionné persiste après retour du drill) : **passed**.
- `declaration.spec.ts` (parcours complet Création → … → Synthèse) : **passed**.
- `task087.spec.ts`, `task088.spec.ts`, `task102.spec.ts` (drill sur incohérences/avertissements,
  workflow TASK-077/078/105) : **passed**.

Aucune régression détectée sur le mode `readOnly` (relecture post-intégration) ni sur le workflow
incohérence.

## Build front

```powershell
npm run build   # tsc -b && vite build
```
`✓ built in 508ms` — 0 erreur. Seul message : warning Vite `[INEFFECTIVE_DYNAMIC_IMPORT]` sur
`src/api.ts`, préexistant (import dynamique dans `Auth.tsx` aussi importé statiquement ailleurs),
sans lien avec ce correctif.

## Validation visuelle (captures d'écran)

| Étape | Capture d'écran |
| --- | --- |
| **Avant le drill** — sélection non vide à l'écran ①, jamais visité auparavant | ![Avant le drill](task109-01-avant-drill.png) |
| **Drill ouvert au tout premier appel** — grille non vide, reflète la sélection réelle | ![Drill premier appel](task109-02-drill-premier-appel.png) |

## Critères de validation

- [x] Ouvrir « Détail des lignes » en tout premier depuis ① (sélection non vide, jamais visité
      auparavant) → la grille reflète la sélection réelle, pas 0 ligne par défaut.
- [x] Message « Aucune affectation ne correspond aux filtres » réservé au cas où un filtre est
      effectivement actif (message distinct sinon).
- [x] Aucune régression sur le mode `readOnly` ni sur le workflow incohérence (suite Playwright
      existante rejouée intégralement, verte).

## Périmètre exclu (rappel, non traité ici)

Le refigeage automatique sur changement de sélection **après** un premier figeage réussi (second
symptôme signalé dans la TASK) reste hors périmètre — dépend d'un arbitrage PO plus large (cf.
TODO.md « CRITIQUE — Figeage/clôture non scopés à la sélection réelle »).
