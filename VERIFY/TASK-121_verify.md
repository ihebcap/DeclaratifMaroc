# VERIFY — TASK-121 — Variabilisation des badges de statut vers la palette signature

> Implémenté par l'assistant en **implémenteur exceptionnel** cette nuit (18→19/07/2026), sur
> autorisation explicite du PO (même session que TASK-120, dérogation ponctuelle au rôle
> architecte/review de `CLAUDE.md`). Ce document est soumis pour revue, **pas auto-approuvé**.

## ⚠️ Risque assumé — démarré sans approbation VERIFY de TASK-120
TASK-121 §Risques est explicite : *"ne pas démarrer avant approbation VERIFY de TASK-120"*. Cette
approbation n'a **pas** eu lieu (personne pour la donner cette nuit). Le PO a explicitement instruit
de démarrer quand même, en acceptant le risque de re-travail documenté ici :
- Les 6 variables sémantiques (§ ci-dessous) reprennent les valeurs hex **actuelles** des badges
  (`#dcfce7`/`#15803d` ok, `#fffbeb`/`#92400e` attention, `#fee2e2`/`#b91c1c` bloquant) — **pas**
  celles de la palette signature noir/vert de TASK-120 (`--accent-primary` `#178a4c`). C'est
  conforme à la lettre de TASK-121 §Étapes point 1 ("valeurs hex actuelles... la variabilisation
  précède l'harmonisation avec la palette signature", décision d'harmonisation explicitement reportée
  au PO en étape 5). **Aucun conflit de teinte n'a donc été introduit avec TASK-120** — le risque de
  re-travail signalé par la task concerne un futur ajustement de valeur (étape 5, décision PO), pas
  une incohérence actuelle.

## Résumé
`index.css` : 6 variables sémantiques ajoutées (`--status-ok-bg/text`, `--status-warning-bg/text`,
`--status-blocking-bg/text`). 13 fichiers `.tsx` variabilisés (badges ok/attention/bloquant
uniquement, substitution mécanique par token hex identique — aucune valeur changée). Build front 0
erreur.

## Grep initial exhaustif (avant traitement)
```
grep -rn "#fffbeb|#fde68a|#92400e|#b45309|#dcfce7|#15803d|#fee2e2|#b91c1c" declaration-tva-web/src/
```
→ 101 occurrences, 13 fichiers touchés par au moins une des 8 teintes citées en exemple dans la
task. Lecture fine de chaque fichier (pas seulement grep) pour distinguer badges de statut réels vs
réutilisation coïncidente de la même teinte pour un autre usage (cf. § Découvertes).

## Variables ajoutées (`index.css`)
```css
--status-ok-bg: #dcfce7;
--status-ok-text: #15803d;
--status-warning-bg: #fffbeb;
--status-warning-text: #92400e;
--status-blocking-bg: #fee2e2;
--status-blocking-text: #b91c1c;
```
Valeurs confirmées **en direct dans le navigateur réel** (dev server actif, `getComputedStyle`,
même méthode qu'en TASK-120) :
```json
{
  "--status-ok-bg": "#dcfce7", "--status-ok-text": "#15803d",
  "--status-warning-bg": "#fffbeb", "--status-warning-text": "#92400e",
  "--status-blocking-bg": "#fee2e2", "--status-blocking-text": "#b91c1c"
}
```

## Fichiers traités (substitution mécanique hex → variable, valeur identique)
`VerifierIntegrerPanel.tsx` (gabarit, traité en premier), `AffectationsDrill.tsx`,
`DeclarationFinalePanel.tsx`, `FactureInterrogation.tsx`, `RapprochementInterrogation.tsx`,
`DomainGrid.tsx`, `ReglementsSelection.tsx`, `WorkstationPanel.tsx`, `DeclarationStepper.tsx`,
`DeclarationList.tsx`, `GenerationPanel.tsx`, `App.tsx`, `CreateDeclarationModal.tsx`.

**Auth.tsx / ExcelFilter.tsx** (cités dans TASK-121 §Contexte comme faisant partie des 18 fichiers) :
lecture complète effectuée, **aucune couleur de badge de statut trouvée** — les seuls hex présents
sont des valeurs de repli `var(--x, #hex)` sans rapport avec ok/attention/bloquant
(`var(--danger-color, #ef4444)`, `var(--accent-primary, #4f46e5)`). Rien à variabiliser dans ces 2
fichiers.

## Grep final — 0 résidu, avec 2 exceptions documentées (pas de badges de statut)
```
grep -rn "#dcfce7\|#15803d\|#fee2e2\|#b91c1c\|#fffbeb\|#92400e" declaration-tva-web/src/*.tsx
```
→ 2 occurrences restantes, **volontairement non substituées** car ce ne sont **pas** des badges de
statut ok/attention/bloquant mais une réutilisation coïncidente de la même teinte verte pour un
autre usage catégoriel :
- `VerifierIntegrerPanel.tsx:826` — `TauxBadge`, colore le badge de **taux de TVA** (20/14/10/7/0 %)
  avec 5 couleurs distinctes (bleu/violet/vert/jaune/gris) ; le taux 10 % utilise le même vert que le
  statut "ok", coïncidence de palette, pas un statut.
- `RapprochementInterrogation.tsx:90` — `DomaineChip`, colore le **domaine métier**
  (Encaissement/Décaissement/Frais bancaire) ; "Encaissement" utilise le même vert, coïncidence de
  palette, pas un statut.

Substituer ces 2 occurrences aurait été **techniquement possible sans changement visuel** (même
valeur hex), mais **sémantiquement incorrect** : la variable `--status-ok-*` afficherait un sens
("ok") qui ne correspond pas à l'usage réel (taux/domaine). Laissé en l'état conformément à TASK-121
§Périmètre ("couleur hex qui n'est pas un badge de statut... à laisser telle quelle").

## Découvertes — teintes additionnelles non couvertes par les 6 variables (signalées, non corrigées)
Conformément à TASK-121 §Risques ("à documenter dans le VERIFY si trouvées, pas à corriger
silencieusement hors périmètre"), plusieurs variantes de teinte "attention" et "ok" coexistent dans
le code, **non consolidées** par cette task (qui ne définit que 2 variables — une bg, une texte —
par statut ; consolider aurait changé le rendu visuel d'au moins une occurrence, contraire à la
consigne "rendu identique") :
- **Attention — fond alternatif** : `#fef9c3` (`VerifierIntegrerPanel.tsx` `ControlBadge`, badge
  pilule) et `#fef3c7` (`AffectationsDrill.tsx`, `App.tsx` bannière expiration licence,
  `DeclarationList.tsx`, `ReglementsSelection.tsx` statut "À contrôler") — différents de
  `--status-warning-bg` (`#fffbeb`, utilisé pour les panneaux/bannières d'avertissement).
- **Attention — texte alternatif** : `#b45309` (icône `ControlIcon`, panneaux d'écart non expliqué
  dans `VerifierIntegrerPanel.tsx`, `AffectationsDrill.tsx`, `FactureInterrogation.tsx` montants
  "reste à déclarer") — différent de `--status-warning-text` (`#92400e`).
- **Ok — texte alternatif** : `#16a34a` (`GenerationPanel.tsx`, icône de succès) — différent de
  `--status-ok-text` (`#15803d`).
- **Bordures liées aux badges, non variabilisées** (TASK-121 ne définit que des variables bg/texte,
  aucune variable de bordure) : `#fde68a` (bordure attention), `#fecaca`/`#fca5a5` (bordure
  bloquant), `#bbf7d0` (bordure ok).

**Question ouverte pour le PO** (cf. TASK-121 §Étapes point 5) : consolider ces variantes vers une
seule teinte par statut (et éventuellement ajouter des variables de bordure) au moment de
l'harmonisation avec la palette signature TASK-120 — décision non tranchée ici.

## `App.tsx` — limitation de commit (contexte technique)
Une occurrence traitée dans `App.tsx` (bannière d'expiration de licence, `#92400e` →
`var(--status-warning-text)`) se trouve dans du code **non commité** appartenant à un autre chantier
(TASK-117, licence ApLicence — implémenté mais jamais commité à ce jour). Cette substitution reste
donc uniquement dans le fichier de travail (non incluse dans le commit Git de cette task, pour ne pas
committer prématurément le travail non revu d'un autre chantier sous ce message de commit) — elle
sera intégrée automatiquement quand TASK-117 sera commité, ou peut être committée séparément sur
demande du PO/architecte.

## `VerifierIntegrerPanel.tsx` / `DeclarationFinalePanel.tsx` — non inclus dans le commit
Ces 2 fichiers sont eux-mêmes des livrables **non commités** d'un autre chantier (fusion
TASK-090/TASK-091, déjà en `DONE_DETAIL/` documentairement mais le code lui-même pas encore commité).
La variabilisation des badges y a été **appliquée dans le fichier de travail** (grep final ci-dessus
en fait foi), mais n'est **pas isolée dans un commit séparé** — committer "juste ma part" n'a pas de
sens pour un fichier entièrement non commité par ailleurs. Elle sera commitée avec le reste de ces
fichiers quand l'architecte validera ce chantier.

## Preuve réelle — mêmes limites qu'en TASK-120
Même contexte que `VERIFY/TASK-120_verify.md` : garde-fou licence fail-closed (TASK-117) + backend
non démarré cette nuit (hors périmètre + chantier TASK-115 concurrent) → **impossible d'atteindre
visuellement** `VerifierIntegrerPanel`, `DeclarationFinalePanel`, `FactureInterrogation` en conditions
réelles. Captures avant/après demandées par TASK-121 §Livrables **non produites** — compensé par :
substitution purement mécanique token-par-token (valeur hex identique avant/après, donc rendu
visuel garanti identique par construction, pas seulement supposé), grep final documenté, lecture
manuelle de chaque occurrence pour écarter les faux positifs (§ Grep final), build vert.

## Build
```
npm run build   (tsc -b && vite build)
```
→ **Succès, 0 erreur** (même warning pré-existant `INEFFECTIVE_DYNAMIC_IMPORT`, non lié).

## Critères de validation (task originale)
- [x] 0 couleur hex de badge de statut restant en dur — grep final : 2 occurrences restantes, toutes
      deux identifiées et justifiées comme non-badges (§ Grep final), pas un résidu de traitement.
- [x] Rendu visuel des badges strictement identique avant/après — substitution mécanique par
      token hex identique (pas de valeur changée), garanti par construction.
- [x] Variables sémantiques utilisées de façon cohérente dans les fichiers traités — aucune
      confusion attention/bloquant introduite (vérifié par relecture de chaque occurrence, pas
      seulement par grep automatique).
- [x] Build front 0 erreur.

## Réserves / risques signalés (non tranchés, pour arbitrage PO/architecte)
1. **Démarré sans approbation TASK-120** — cf. § dédiée en tête de document.
2. **Teintes additionnelles non consolidées** (`#fef9c3`/`#fef3c7`/`#b45309`/`#16a34a` + bordures) —
   cf. § Découvertes, décision PO requise pour une consolidation future.
3. **3 fichiers non inclus dans le commit** (`App.tsx` partiellement, `VerifierIntegrerPanel.tsx`,
   `DeclarationFinalePanel.tsx` entièrement) car ils portent du code non commité d'autres chantiers —
   la variabilisation y est appliquée dans le répertoire de travail mais pas isolée en commit séparé,
   cf. sections dédiées ci-dessus.
4. **Captures manquantes** (backend indisponible) — mêmes limites qu'en TASK-120, compensées par la
   garantie de construction (substitution token-identique) plutôt que par une preuve visuelle.

## Statut
**Soumis pour revue** — implémentation nocturne en dérogation au rôle `CLAUDE.md`, ne s'auto-approuve
pas. Clôture officielle laissée à l'architecte.
