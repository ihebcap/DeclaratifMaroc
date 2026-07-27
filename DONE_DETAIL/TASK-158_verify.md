# TASK-158 — VERIFY rétroactif (architecte)

> **Particularité de ce VERIFY** : aucun fichier `VERIFY/TASK-158_verify.md` n'a jamais été soumis.
> Le correctif a été découvert **déjà livré en code** le 27/07/2026, lors d'une revue de recadrage
> du `TODO.md` demandée par le PO (« vérifier avec le code source »). Ce document constitue donc la
> revue architecte a posteriori, à la place du cycle normal IN_PROGRESS→VERIFY. **Ceci ne doit pas
> devenir un précédent** : signalé explicitement au PO comme une entorse au process (cf. note
> d'hygiène en fin de document).

## Constat

`declaration-tva-web/src/ColumnSelector.tsx` contient déjà, en l'état actuel du dépôt, la logique de
flip vertical demandée par la TASK :

- `POPUP_ESTIMATED_HEIGHT`/`VIEWPORT_MARGIN`/`GAP` (l.25-28) + calcul `spaceBelow`/`spaceAbove` et
  décision `openBelow` (l.37-40) dans `computePosition` — bascule le popup au-dessus du bouton
  (`bottom: window.innerHeight - rect.top + GAP`) quand l'espace en dessous est insuffisant,
  symétriquement à la logique horizontale déjà en place (`left`, inchangée).
- Un commentaire explicite en l.25 référence directement `TASK-158`.

`git blame` situe ces lignes dans le commit `cdb37e8a` (23/07/2026, `feat(TASK-161): code activite
TVA resolu en cascade`) — un commit de rattrapage regroupant plusieurs travaux (cf. note dans ce
commit et pattern déjà documenté pour TASK-179/`99ef0fc`). Le correctif TASK-158 s'y est retrouvé
noyé, jamais tracé via son propre `IN_PROGRESS`/`VERIFY`.

## Vérification menée par l'architecte (27/07/2026)

- **Périmètre respecté** : seul `ColumnSelector.tsx` porte la logique modifiée ; `git show cdb37e8a
  --stat` confirme qu'aucun des 6 écrans appelants (`ReglementsSelection`, `FactureInterrogation`,
  `RapprochementInterrogation`, `AffectationsDrill`, `ControlGrid`, `DomainGrid`) n'a été touché pour
  ce sujet — garde-fou de la TASK (« ne pas dupliquer la logique dans les 6 écrans ») respecté.
- **Logique horizontale non régressée** : `left`/`POPUP_WIDTH`/clamp droite (l.33-35) strictement
  inchangés par rapport à la version pré-correctif.
- **Cas limite couvert** : quand ni le dessus ni le dessous n'offrent assez de place, `openBelow`
  choisit le côté offrant le plus d'espace (`spaceBelow >= spaceAbove`) — conforme à la TASK, pas de
  nouveau mécanisme de scroll inventé (`overflowY: auto` existant conservé plus bas dans le fichier).
- **Build rejoué indépendamment par l'architecte** : `npx tsc -b` → 0 erreur ; `npx vite build` →
  0 erreur (seul avertissement `INEFFECTIVE_DYNAMIC_IMPORT` préexistant, sans rapport).

## Réserve non bloquante

Aucune reproduction visuelle réelle (capture d'écran navigateur) du cas PO original (bouton
« Colonnes » proche du bas de fenêtre) n'a été rejouée dans cette revue — la lecture de code et le
build suffisent à confirmer que la logique demandée est bien celle implémentée, mais la TASK
prévoyait un scénario de reproduction réelle en `## Validation`. À confirmer par le PO en conditions
réelles si jugé utile ; risque résiduel jugé faible (composant isolé, logique symétrique à un
mécanisme horizontal déjà éprouvé).

## Note d'hygiène process

Ce correctif est le **deuxième cas identifié** (après TASK-179/commit `99ef0fc` pour TASK-176) où du
code correctif se retrouve livré dans un commit de rattrapage portant le nom d'une autre TASK, sans
jamais transiter par `IN_PROGRESS`/`VERIFY`. Aucune conséquence sur le contenu ici (le correctif est
conforme), mais le PO devrait envisager de rappeler la règle « un commit = une TASK déclarée » si ce
schéma se répète une troisième fois.

## Verdict

**APPROUVÉE rétroactivement** (27/07/2026) — code déjà conforme à la TASK, build vérifié, aucun
défaut trouvé.
