# TASK-109 — « Détail des lignes » (drill ②) peut apparaître vide alors que des règlements sont sélectionnés

## Origine

Signalement PO 17/07/2026 (capture écran, `TVA1-2026-01`, 145 règlements) : l'en-tête du drill
« Détail des affectations » annonce bien 145 règlements et « payé ÷ TTC = % puis TVA × % = TVA
déclarée », mais la grille affiche **0 ligne**, **Total TVA (tous règlements) : 0,00 MAD** et
« Aucune affectation ne correspond aux filtres. » — sans qu'aucun filtre ne soit actif. Question
PO : « pk parfois je vois pas de données pourtant j'ai des reglements ».

## Contexte

Le compteur d'en-tête (`{selectedRows.length} règlements`, `AffectationsDrill.tsx:519`) est dérivé
de l'état **front** `selectedRows` (les cases cochées à l'écran ① Sélection) — il est donc toujours
exact vis-à-vis de ce que l'utilisateur a coché, **indépendamment** de ce que la grille parvient à
charger. Le contenu de la grille, lui, vient de `GET /declarations/{id}/lignes`
(`DeclarationsController.cs:113-121`, commentaire l.105 : « figeant au premier appel ») :

1. Ce endpoint appelle `ChargerCandidatesSiNecessaireAsync` (`DeclarationWorkflowService.cs:131`) :
   si `GetLignesCountAsync(declarationId, domaine) == 0`, il **construit et persiste** les lignes
   `DM_LGTVA` pour ce domaine via `ConstruireLignesFigeesAsync` (l.184) — sinon il se contente de
   relire/revalider l'existant (chemin rapide, aucune reconstruction).
2. `ConstruireLignesFigeesAsync` filtre les candidats sur la **sélection persistée côté serveur**
   (`GetSelectionReglementsAsync(declarationId)`, l.209-211) — **jamais** sur `selectedRows` (état
   front). Si aucune sélection n'a encore été persistée, le résultat est **volontairement zéro
   candidat** (comportement voulu par TASK-097, l.205-208 : jamais « tous les candidats » par
   défaut).
3. Le **seul** point d'écriture de cette sélection persistée est `POST
   /declarations/{id}/selection`, appelé uniquement par `handlePasserAuCalcul`
   (`DeclarationStepper.tsx:73-90`), c'est-à-dire le bouton **« Passer au calcul »**.
4. Depuis TASK-092, le bouton **« Détail des lignes »** (bas de l'écran ①,
   `DeclarationStepper.tsx:129-139`) ouvre ce même drill dès qu'une sélection existe côté front
   (`disabled={!hasSelection && !integree}`) — **sans jamais forcer** un `POST /selection` au
   préalable, contrairement à « Passer au calcul ».

**Conséquence** : si le tout premier appel `/lignes` pour ce (déclaration, domaine) survient via
« Détail des lignes » — càd avant que « Passer au calcul » ait persisté la sélection réellement
cochée — `ConstruireLignesFigeesAsync` construit et **persiste zéro ligne** pour ce domaine (aucune
insertion, `SaveLignesCandidatesAsync` sur liste vide). La grille reste alors vide, avec le
diagnostic trompeur « Aucune affectation ne correspond aux filtres » (aucun filtre actif, en
réalité rien à afficher).

**Second symptôme, plus insidieux, découlant du même mécanisme** : une fois des lignes figées avec
succès pour un domaine (via un premier appel avec sélection persistée à jour), toute **modification
ultérieure** de la sélection à l'écran ① (cocher/décocher un règlement) n'est **plus jamais**
reflétée dans le drill — `GetLignesCountAsync > 0` fait toujours prendre le chemin rapide (relecture
seule, aucune reconstruction), même si l'utilisateur reclique « Passer au calcul » entre-temps (qui
persiste la nouvelle sélection en base mais ne déclenche aucun refigeage). Non couvert par cette
task (périmètre distinct : refigeage sur changement de sélection après un premier appel) —
**signalé ici pour mémoire**, à trancher séparément avec le PO si confirmé sur cas réel.

## Périmètre STRICT

- **Inclus** :
  1. Le bouton « Détail des lignes » (`DeclarationStepper.tsx:129-139`) doit garantir que la
     sélection **actuellement cochée** est bien celle utilisée pour le premier figeage avant
     d'ouvrir le drill — par exemple en persistant la sélection (même appel que
     `handlePasserAuCalcul`, sans changer d'étape) avant de basculer `showDrill(true)`, si aucune
     lignes n'existent encore pour ce domaine.
  2. Repli honnête si malgré tout la grille est vide après ce garde-fou (ex. règlements réellement
     sans affectation) — message distinct de l'actuel (`Aucune affectation ne correspond aux
     filtres` est trompeur quand `activeFilterCount === 0`) : ne pas réutiliser ce libellé quand
     aucun filtre n'est actif.
- **Exclu** (signalé, hors périmètre) :
  - Le refigeage automatique sur changement de sélection après un premier figeage réussi (second
    symptôme ci-dessus) — dépend d'un arbitrage PO plus large (cf. section TODO.md « CRITIQUE —
    Figeage/clôture non scopés à la sélection réelle », déjà partiellement traitée par TASK-097/099).
  - Toute modification du calcul de valorisation ou du contrôle d'équilibre.

## Objectif

```
Entrée  : Détail des lignes ouvert depuis ① avant tout appel /lignes réussi pour ce domaine
Traitement : garantir que la sélection persistée côté serveur reflète la sélection cochée avant
             le premier figeage (ou distinguer clairement "vide car rien à charger" de "vide car
             filtré")
Sortie  : le drill n'affiche plus 0 ligne pour une sélection réelle non vide ; si réellement 0
          ligne pour cause métier (règlement sans affectation), message honnête distinct du message
          "filtres"
```

## Livrables

- `DeclarationStepper.tsx` : garde-fou sur `setShowDrill(true)` (persister la sélection avant
  ouverture si nécessaire, ou équivalent).
- `AffectationsDrill.tsx` : message de grille vide distinct selon `activeFilterCount === 0` (aucune
  ligne chargée) vs `> 0` (filtré).
- Test(s) reproduisant le scénario : sélection cochée à ①, drill ouvert en tout premier (aucun appel
  `/lignes` préalable réussi), assertion que la grille n'est pas vide.
- `VERIFY/TASK-109_verify.md` : build OK, tests verts, preuve réelle (capture avant/après sur cas
  similaire au signalement PO).

## Critères de validation

- Ouvrir « Détail des lignes » en tout premier depuis ① (sélection non vide, jamais visité
  auparavant) → la grille reflète la sélection réelle, pas 0 ligne par défaut.
- Message « Aucune affectation ne correspond aux filtres » réservé au cas où un filtre est
  effectivement actif.
- Aucune régression sur le mode `readOnly` (relecture post-intégration, TASK-075/092) ni sur le
  workflow incohérence (TASK-077/078/105).

## Risques / dépendances

- Toucher `handlePasserAuCalcul`/persistance de sélection est une zone déjà sensible (TASK-097,
  garde-fou serveur « jamais tous les candidats par défaut ») — ne pas contourner ce garde-fou,
  seulement s'assurer qu'il voit la bonne sélection au bon moment.
- Le second symptôme (non-refigeage sur changement de sélection) partage la même cause racine
  (`GetLignesCountAsync > 0` = chemin rapide définitif) — un futur correctif pourrait vouloir traiter
  les deux ensemble ; à confirmer avec le PO avant de scinder ou fusionner.

## NOTES

Découvert en répondant à une question PO sur un cas réel (`TVA1-2026-01`, 145 règlements, capture
écran 17/07/2026). Analyse de code uniquement (architecte) — non reproduit manuellement en
environnement réel (pas d'accès DB depuis ce rôle) ; à confirmer par test/VERIFY avant clôture.
