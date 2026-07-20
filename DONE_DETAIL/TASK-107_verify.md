# Verification of TASK-107 — Drill « Répartition par source » → facture/règlement (option 3)

Arbitrage PO préalable (17/07/2026, requis par le blocage posé dans la fiche après TASK-108) :
**option 3 retenue** — renvoi vers le drill filtré existant (TASK-016, `DomainGrid`), front-only,
sans nouveau back. Options 1 (ventilation descendante enrichie) et 2 (écart = différence
d'ensembles) écartées par le PO.

## 1. Modifications de code

- [RecapSourceTable.tsx](file:///D:/_vibe/GRF/declaration-tva-web/src/RecapSourceTable.tsx) :
  ajout d'une prop optionnelle `onRowClick?: (source: string, label: string) => void` ; chaque
  ligne devient cliquable (curseur, survol) quand la prop est fournie, sans changement de rendu
  sinon (non-régression TASK-103/087).
- [VerifierIntegrerPanel.tsx](file:///D:/_vibe/GRF/declaration-tva-web/src/VerifierIntegrerPanel.tsx) :
  - `handleDrillSource(source, label)` réutilise l'état `drillFiltre` déjà câblé pour le drill
    anomalie (TASK-016/088) : `setDrillFiltre({ domaine: selectedTab, filtre: { source: [source] }, label, kind: 'source' })`.
  - `drillFiltre` gagne un champ `kind?: 'anomalie' | 'source'` pour distinguer le libellé du
    bandeau (« Drill anomalie : » vs « Drill source : ») — aucune autre différence de
    comportement (même `DomainGrid` en lecture seule, même bouton de retour).
  - `ChecklistCard` / `RecapSourceTable` reçoivent et transmettent `onDrillSource`.
- **Aucune modification** du calcul de l'écart, de la valorisation, ni du back — le filtre
  `source` existe déjà côté API (`BuildLigneFilterWhere`, colonne `DM_LGTVA.Source`), c'est le
  même champ que celui groupé par `/checkup` pour produire `recapSource` : aucune transformation,
  aucune divergence possible entre le libellé cliqué et les lignes retrouvées.
- Traçabilité honnête : le compteur `Total résultats : N` de `DomainGrid` reste toujours affiché,
  y compris à 0 — jamais de grille muette si une source n'a exceptionnellement aucune ligne
  rattachable.

## 2. Build / Lint

- `npx tsc --noEmit` → 0 erreur.
- `npm run build` (tsc -b && vite build) → 0 erreur.
- `npm run lint` (oxlint) → mêmes avertissements préexistants (non liés à ce changement) ; aucun
  nouvel avertissement sur `RecapSourceTable.tsx` / `VerifierIntegrerPanel.tsx`.

## 3. Preuve réelle multi-pièces (`GR_EMA_DISTRIBUTION`, `TVA1-2026-06`)

Test Playwright [tests/task107.spec.ts](file:///D:/_vibe/GRF/declaration-tva-web/tests/task107.spec.ts),
API réelle (`Declaration.API`, aucun mock front), base réinitialisée (`reset.ps1`) puis 10
règlements réels marqués éligibles (`make_eligible.ps1`) :

1. Connexion réelle, création de la déclaration `TVA1-2026-06`.
2. Sélection de 5 règlements réels → 3 lignes valorisées (Décaissement), 1 628,29 MAD de TVA.
3. Étape ② Vérifier & Intégrer : seul `equilibre.isValid` est forcé à `false` (interception
   réseau) pour rendre le contrôle visible en écart — **`recapSource` n'est jamais modifié**,
   il reste la valeur réelle renvoyée par le back.
4. Clic sur la ligne source « Decaissement » de `RecapSourceTable` (capture
   `VERIFY/task107-step3-avant-drill.png`).
5. Le bandeau **« Drill source : Source : Decaissement »** s'affiche ; `DomainGrid` s'ouvre
   filtrée (lecture seule) et retrouve **3 pièces réelles** (capture
   `VERIFY/task107-step3-drill-source.png`) :

   | N° Facture | Tiers | Montant TTC | Source |
   |---|---|---|---|
   | FC2502204 | TEMPO FOODS | 8 826,55 MAD | Decaissement |
   | FC2600789 | MARJANE | 540,14 MAD | Decaissement |
   | FC2600895 | MARJANE | 403,10 MAD | Decaissement |

6. Retour au contrôle (« ← Retour au contrôle ») : le bouton « Confirmer intégration » reste
   présent et actif, aucune clôture n'a eu lieu — le drill est resté strictement en lecture, sans
   figer la déclaration.

## 4. Critères de validation (fiche TASK-107)

- ✅ Depuis « Répartition par source » en écart, on atteint la/les pièce(s) FC…/RC… responsables,
  à la demande (preuve §3 : 3 factures réelles retrouvées depuis la source Décaissement).
- ✅ Aucune régression TASK-103/087 : `RecapSourceTable` inchangée quand `onRowClick` n'est pas
  fourni (aucun autre appelant du composant ne passe cette prop).
- ✅ Calcul de l'écart et valorisation strictement inchangés (aucune modification back, aucun
  recalcul front touché).
- ✅ Lecture seule : pas de figeage, retour possible vers le contrôle.
