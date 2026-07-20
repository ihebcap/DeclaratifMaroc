# TASK-092 — Étape ② Affectations → drill à la demande + barre finale 3 étapes (tunnel 3 étapes, 3/3)

> **Origine** : décision PO 14/07/2026 — cible tunnel 3 étapes (cf. TASK-090/091). Inventaire
> décisionnel (architecte) : ② est un drill de **lecture** (payé÷TTC → prorata → TVA déclarée),
> traversable sans aucune décision — ses seules actions mutantes (« Valider l'incohérence » /
> « Corriger / Resynchroniser », `AffectationsDrill.tsx` l.376-390, `POST
> …/lignes/valider-incoherence` et `…/lignes/resynchroniser`) n'apparaissent que si une
> incohérence Sage post-figeage est détectée (TASK-077/078) et ne conditionnent jamais la
> progression (« Passer au calcul » n'est pas bloqué). Le contenu reste indispensable, mais comme
> **détail à la demande**, pas comme étape imposée.

## Contexte

- `DeclarationStepper.tsx` : `isUnlocked('affectations')` = `integree || hasSelection` (l.47-51) ;
  en mode intégré (`readOnly`), le drill charge l'intégralité des lignes (`AffectationsDrill.tsx`
  l.68/96) — c'est par ce mode que le workflow incohérence TASK-077/078 reste jouable après
  figeage.
- Après TASK-090/091, le tunnel transitoire est : Sélection · Affectations · Vérifier & Intégrer ·
  Déclaration (4 étapes). Cette task le porte à la cible finale 3 étapes.

## Périmètre STRICT

- **Inclus** :
  1. Retirer « Affectations » de la barre d'étapes. `AffectationsDrill.tsx` **conservé tel quel**
     (aucune modification interne) : seul son montage change — drill plein écran ouvert depuis
     ① Sélection via un bouton « Détail des lignes » dans la barre du bas (actif dès qu'une
     sélection existe ; toujours actif en mode intégré, où le drill charge tout), avec retour à ①.
  2. Préserver le workflow incohérence (TASK-077/078) : badge « N ligne(s) incohérente(s) » et
     boutons valider/resynchroniser accessibles via ce drill, **y compris post-figeage** (mode
     `readOnly` actuel inchangé).
  3. Barre finale **3 étapes** : « ① Sélection · ② Vérifier & Intégrer · ③ Déclaration » —
     renumérotation des libellés/commentaires ; redirection auto de `fetchInfo` : statut ≠ 0 →
     **« Déclaration »** (écran post-figeage : constat + exports), et non plus l'écran
     d'engagement (dont le bouton est désactivé une fois intégrée — rediriger dessus n'a plus
     d'utilité).
  4. Mode figé : bandeau « Déclaration intégrée — étape figée » (`readOnlyStep`) adapté aux
     étapes restantes (Sélection figée, drill en lecture).
- **Exclu** :
  - Toute modification interne d'`AffectationsDrill.tsx` (logique incohérence, filtres, colonnes,
    chargement : intacts).
  - Tout changement back.
  - Mise à jour du guide fonctionnel (`DOCS/GUIDE_PROCESS_DECLARATION_TVA.html`) qui décrit le
    tunnel par étapes : à traiter avec TASK-029 (signalé, hors périmètre ici).

## Objectif

```
Entrée  : tunnel transitoire 4 étapes (après TASK-090/091), ② = drill de lecture imposé
Traitement : ② devient un drill à la demande depuis ① ; barre réduite à 3 étapes ; redirection
             auto vers l'écran post-figeage
Sortie  : tunnel final 3 étapes conforme à la décision PO — un geste métier par étape
          (choisir · vérifier+s'engager · constater+produire)
```

## Livrables

- `DeclarationStepper.tsx` modifié (barre 3 étapes, montage du drill, redirection) ; ajustement du
  point d'entrée du drill (barre du bas de ① ou équivalent).
- `VERIFY/TASK-092_verify.md` : captures barre 3 étapes, drill ouvert/refermé depuis ①, badge et
  actions incohérence visibles sur cas réel post-figeage (cas `FC2501717`/`EC_Id=21473` si
  rejouable), build tsc+vite 0 erreur, e2e adaptés.

## Critères de validation

- Barre d'étapes = **3 entrées exactement** : « Sélection / Vérifier & Intégrer / Déclaration ».
- Drill Affectations accessible depuis ① **avant et après** intégration ; workflow
  valider/resynchroniser rejouable à l'identique (aucune régression TASK-077/078).
- Ouverture d'une déclaration déjà intégrée → arrivée directe sur « Déclaration ».
- Aucune fonctionnalité de l'ancien ② perdue (filtres, sélecteur de colonnes, incohérences).

## Risques / dépendances

- À exécuter **après** TASK-090 et TASK-091 (dernière des 3 fusions).
- **Découvrabilité des incohérences** : l'étape n'étant plus imposée, une incohérence post-figeage
  pourrait passer inaperçue si l'utilisateur n'ouvre pas le drill. Cette task garantit l'accès,
  pas le signal proactif ; si le PO veut un indicateur sur ① ou dans le checkup (nécessite
  d'exposer l'info côté back ou de charger les lignes depuis ①), ouvrir une task dédiée sur
  demande PO — signalé, hors périmètre ici.
