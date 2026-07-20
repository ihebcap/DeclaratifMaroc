# TASK-090 — Fusion ③ Calcul + ④ Intégration → écran « Vérifier & Intégrer » (tunnel 3 étapes, 1/3)

> **Origine** : décision PO 14/07/2026 — cible tunnel **3 étapes** (« Sélection · Vérifier &
> Intégrer · Déclaration »), retenue parmi 4 options après inventaire décisionnel des 6 écrans
> (architecte). Constat : ③ Calcul est 100 % passif (aucun bouton, aucun filtre —
> `CalculTvaPanel.tsx`) et ④ porte le seul acte d'engagement du tunnel (`POST /cloture`). Deux
> écrans pour un seul geste métier « vérifier puis s'engager ». Première des 3 fusions
> (090 : ③+④ ; 091 : ⑤+⑥ ; 092 : ② en drill).

## Contexte

- `CalculTvaPanel.tsx` : sous-totaux par taux agrégés front depuis `GET /declarations/{id}/lignes`
  (pagination chunk 500), lecture seule stricte ; remonte `totalTVA`/`nbLignes` au stepper via
  `onCalcSummary` (`DeclarationStepper.tsx` l.181).
- `IntegrationPanel.tsx` : `GET /declarations/{id}/checkup` (l.97) → check-list de contrôles +
  bouton « Confirmer intégration » (`#btn-confirmer-integration`, l.253) → `POST /cloture`
  (l.122), gates `canConfirm` (l.113) et `hasBloquant` (l.110-112).
- Le pipe d'état ③→④ (`calcTVA`, `useState` stepper l.60) est la cause racine du « récap vide »
  documentée par TASK-086 : il ne se remplit que si l'utilisateur traverse physiquement ③ dans la
  session. La fusion supprime ce pipe → **TASK-086 est absorbée ici**.

## Périmètre STRICT

- **Inclus** :
  1. Nouveau fichier `VerifierIntegrerPanel.tsx` assemblant, **sans réécriture de logique** :
     le bloc « Sous-totaux par taux TVA » de `CalculTvaPanel.tsx` (avec la colonne TTC, TASK-085
     étant livrée avant dans l'ordre prévu), puis le `RecapCard` + la check-list `/checkup` +
     bouton « Confirmer intégration » d'`IntegrationPanel.tsx` (id `#btn-confirmer-integration`
     conservé pour les e2e).
  2. `RecapCard` alimenté par la réponse `/checkup` (`recapSource`/`recapTaux` — interface
     `CheckupResult` étendue), plus par les props volatiles de ③ (**absorption TASK-086**) :
     robuste au F5 et à l'ouverture directe d'une déclaration avancée.
  3. `DeclarationStepper.tsx` : suppression de l'étape « Calcul », du state
     `calcTVA`/`onCalcSummary` ; bouton bas de ① (« Analyser TVA ») recâblé vers la nouvelle
     étape ; adaptation d'`isUnlocked` et de la redirection auto de `fetchInfo`
     (statut ≠ 0 → cette étape, comportement actuel conservé au cran près — la cible finale
     « Déclaration » est traitée en TASK-092) ; barre transitoire à 5 étapes.
  4. Suppression de `CalculTvaPanel.tsx` et `IntegrationPanel.tsx` après déplacement (aucun code
     mort résiduel).
- **Exclu** :
  - Tout changement back (`/lignes`, `/checkup`, `/cloture` inchangés).
  - Tout changement de contenu fonctionnel : renommage du contrôle + détail d'écart = TASK-087 ;
    enrichissement des avertissements = TASK-088 (toutes deux retargetées sur ce nouvel écran).
  - Étapes ①②⑤⑥ (fusion ⑤+⑥ = TASK-091 ; ② en drill = TASK-092).

## Objectif

```
Entrée  : décision PO tunnel 3 étapes ; écrans ③ (passif) et ④ (engagement) existants
Traitement : fusion en un écran « Vérifier & Intégrer » (calcul par taux + contrôles +
             engagement), suppression du pipe d'état ③→④
Sortie  : tunnel transitoire à 5 étapes, un seul écran entre Affectations et Contrôle ;
          récap toujours rempli (F5 et ouverture directe inclus)
```

## Livrables

- `VerifierIntegrerPanel.tsx` (nouveau), `DeclarationStepper.tsx` modifié,
  `CalculTvaPanel.tsx` + `IntegrationPanel.tsx` supprimés.
- `VERIFY/TASK-090_verify.md` : captures (navigation normale + F5 direct sur l'écran), totaux par
  taux identiques à l'ancien ③ sur le même cas réel (cas PO 68 règlements / 245 lignes), build
  tsc+vite 0 erreur, e2e Playwright vert (liste des specs adaptées).

## Critères de validation

- Un seul écran entre « Affectations » et « Contrôle » ; barre d'étapes cohérente (5 entrées
  transitoires).
- Sous-totaux par taux **identiques** à l'ancien écran ③ (mêmes montants, même cas réel).
- « Confirmer intégration » : gates conservés (désactivé si bloquant / checkup en cours / déjà
  intégrée) ; `POST /cloture` inchangé.
- `RecapCard` rempli en navigation normale **et** après F5/ouverture directe (critère repris de
  TASK-086).
- Contrôle et Synthèse restent verrouillées avant intégration (`isUnlocked` inchangé pour elles).
- `git grep "CalculTvaPanel\|IntegrationPanel"` → 0 référence résiduelle.

## Risques / dépendances

- À exécuter **après** TASK-085 (colonne TTC — la table déplacée l'emporte finie) et TASK-089
  (code mort purgé avant de déplacer du JSX).
- e2e Playwright : les scénarios traversant ③ doivent être adaptés (étape disparue) — lister les
  specs touchées au VERIFY.
- TASK-086 absorbée (fichier marqué, ligne TODO reclassée) ; TASK-087/088 retargetées sur
  `VerifierIntegrerPanel.tsx`.
- État transitoire : entre 090 et 092 le tunnel a 5 puis 4 étapes — livrer 090 → 091 → 092 à la
  suite, sans autre task front intercalée.
