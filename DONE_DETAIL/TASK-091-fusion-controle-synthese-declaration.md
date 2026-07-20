# TASK-091 — Fusion ⑤ Contrôle + ⑥ Synthèse → écran « Déclaration » + retrait du bouton « Clôturer » mort (tunnel 3 étapes, 2/3)

> **Origine** : décision PO 14/07/2026 — cible tunnel 3 étapes (cf. TASK-090). ⑤ et ⑥ consomment
> le même `GET /declarations/{id}/checkup` (`ControleDeclarationPanel.tsx` l.109 /
> `SynthesePanel.tsx` l.62) et affichent les mêmes agrégats (`recapSource`/`recapTaux`/
> `equilibre`/`alertes`). Deux écrans de constat post-figeage pour un seul besoin : « vérifier le
> résultat et produire le justificatif ».

## Contexte

- `ControleDeclarationPanel.tsx` (⑤) : sections répartition par source / vue par taux / anomalies
  typées + drill « Voir lignes » (`DomainGrid` en `readonly`, l.186). Aucune mutation.
- `SynthesePanel.tsx` (⑥) : mêmes agrégats checkup + « Export Excel » / « Générer XML »
  (`POST /declarations/{id}/generation` l.103, téléchargement blob l.126) + bouton « Clôturer ».
- **Bouton « Clôturer » de ⑥ mort par construction — triple verrou vérifié** :
  1. ⑤/⑥ ne sont accessibles que si `statut !== 0` (`isUnlocked`, `DeclarationStepper.tsx`
     l.47-51) ;
  2. `clotureDisabled = hasBloquants || cloturee || cloturing` avec `cloturee = statut !== 0`
     (`SynthesePanel.tsx` l.21-23 et l.95) → toujours désactivé dès que l'écran est atteignable ;
  3. le back refuse toute clôture hors `EnCours` (`DeclarationWorkflowService.cs` l.804-805).
- L'acte d'engagement unique a **déjà lieu en ④** : « Confirmer intégration » → `POST /cloture` →
  statut `Cloturee` (`DeclarationWorkflowService.cs` l.826, tampon DT_Id l.828+).
- Cas réouverture (TASK-073) : `DT_Id → NULL` + statut `EnCours` → la déclaration repasse par le
  tunnel amont et se ré-engage sur « Vérifier & Intégrer ». Aucun parcours existant ne peut
  cliquer « Clôturer » en ⑥.

## Périmètre STRICT

- **Inclus** :
  1. Nouveau fichier `DeclarationFinalePanel.tsx` assemblant, **sans réécriture de logique** :
     les sections contrôle de ⑤ (répartition par source, vue par taux, anomalies + drill
     « Voir lignes » `readonly`) et les boutons « Export Excel » / « Générer XML » de ⑥
     (`POST /generation` + téléchargement blob inchangés) — **un seul** `GET /checkup`.
  2. Retrait du bouton « Clôturer » et de son câblage front (`cloturer()`, `onClotured`,
     état `cloturing`) — le reste est un déplacement pur de JSX.
  3. `DeclarationStepper.tsx` : fusion des deux étapes ; le gate interne ⑤→⑥
     (`hasBloquants`/`onHasBloquants`) disparaît avec le passage qu'il gardait ; la garde
     `exportDisabled = hasBloquants || generating` est **conservée** sur l'écran fusionné.
  4. Suppression de `ControleDeclarationPanel.tsx` et `SynthesePanel.tsx` après déplacement.
- **Exclu** :
  - Endpoint `POST /cloture` et sa garde back : inchangés (toujours utilisés par
    « Vérifier & Intégrer », TASK-090).
  - Extraction du composant partagé `RecapSourceTable` : TASK-087 (le JSX « Répartition par
    source » est déplacé **tel quel** ici, refactor pur — l'extraction se fait après).
  - Statuts `2=Generee`/`3=Deposee` et comportement de `/generation` : inchangés.

## Objectif

```
Entrée  : écrans ⑤ et ⑥ existants, même source /checkup, bouton Clôturer inatteignable
Traitement : fusion en un écran « Déclaration » (contrôle post-figeage + exports), retrait du
             bouton mort et de son câblage
Sortie  : tunnel transitoire à 4 étapes ; un seul écran post-figeage, un seul appel /checkup,
          aucune perte d'information ni de fonction (hors bouton jamais fonctionnel)
```

## Livrables

- `DeclarationFinalePanel.tsx` (nouveau), `DeclarationStepper.tsx` modifié,
  `ControleDeclarationPanel.tsx` + `SynthesePanel.tsx` supprimés.
- `VERIFY/TASK-091_verify.md` : captures avant/après prouvant qu'aucune section de ⑤ ni export de
  ⑥ n'est perdu ; export Excel **et** XML re-testés sur cas réel ; onglet réseau montrant un seul
  appel `/checkup` ; build tsc+vite 0 erreur, e2e adaptés.

## Critères de validation

- Toutes les sections de ⑤ (source, taux, anomalies, drill) et les exports de ⑥ présents sur
  l'écran fusionné ; **un seul** `GET /checkup` au chargement.
- Bouton « Clôturer » absent ; `git grep "cloturer\|onClotured"` côté front → 0 référence
  résiduelle (hors « Confirmer intégration » de TASK-090).
- Exports toujours désactivés tant qu'il reste une anomalie bloquante.
- Drill « Voir lignes » fonctionnel, toujours en `readonly`.
- `git grep "ControleDeclarationPanel\|SynthesePanel"` → 0 référence résiduelle.

## Risques / dépendances

- À exécuter **après** TASK-090 (ordre 090 → 091 → 092, à la suite).
- TASK-087 dépend de cet écran (extraction `RecapSourceTable` depuis le JSX déplacé).
- Le retrait du bouton « Clôturer » ne préjuge pas d'un futur jalon « Déposée » (statut 3,
  roadmap R2 Télé-déclaration) : le bouton n'a jamais été fonctionnel, aucune capacité perdue —
  signalé pour mémoire.
