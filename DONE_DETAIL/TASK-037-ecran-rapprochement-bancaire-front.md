# TASK-037 — Écran « Rapprochement bancaire » (front, interrogation règlement-pivot lecture seule)

## Contexte
Entrée de menu « Rapprochement bancaire » (TASK-035, shell) = **interrogation globale, lecture seule, pivot règlement**. Cette tâche livre l'**écran** qui consomme l'endpoint de **TASK-036** (`GET /rapprochement`). Objectif produit : donner au comptable une lecture **dense et sans angle mort** du rapprochement (aucune ligne silencieuse) — c'est le **cœur de valeur** du module (priorité PO : rapprochement + TVA avant tout).

## Périmètre STRICT
- **Inclus** : un composant d'écran (`declaration-tva-web`) affichant les règlements en grille dense (pivot règlement), filtrable, avec drill-down vers les factures affectées / la preuve TVA. Lecture seule.
- **Exclu** : aucune action de masse, aucun `Je déclare`/verrou (c'est le rôle de l'écran Déclaration). Aucun appel à `gocom-web`/GRC_WEB. Aucune modification back (fournie par TASK-036). Ne pas *faire* le rapprochement (interrogation seulement).

## Objectif
```
Entrée  : l'utilisateur clique « Rapprochement bancaire » (shell TASK-035)
Sortie  : grille dense des règlements avec, par ligne : N° règlement, date, mode, tiers, montant,
          RAPPROCHÉ BANQUE (badge O/N), factures affectées (nb) + reste à affecter, origine, déclaré (badge).
          Drill : clic règlement → détail des factures affectées + preuve TVA (OM/FGR).
Style   : dense, pleine hauteur/largeur, « 0 espace perdu » (réf. visuelle GOCOM).
```

## Étapes
1. Nouveau composant (ex. `RapprochementInterrogation.tsx`) consommant `GET /rapprochement` via `api.ts` (mêmes conventions que `DomainGrid` : page/size/sort/filter/distincts, virtualisation).
2. Colonnes (pivot règlement) : `numeroReglement`, `date`, `mode`, `tiers`, `montant` (droite), **`rapprocheBanque`** (badge vert/gris O/N), `nbFacturesAffectees`, **`resteAAffecter`** (droite, mis en évidence si ≠ 0), `origine`, **`declare`** (badge). Filtres `ExcelFilter` (list/text/number/date) avec `selectedValues || []` (éviter le bug TASK-029).
3. **Reste à affecter ≠ 0 visible** (couleur/alerte discrète) = transparence : aucune somme absorbée en silence.
4. Drill-down : clic ligne → détail des factures affectées à ce règlement + preuve TVA. Réutiliser `ProofModal` si applicable, sinon un panneau/modale de détail **à la demande** (pas de panneau latéral permanent — tension « 0 espace perdu »).
5. Câblage dans le shell TASK-035 (remplace le placeholder de l'entrée « Rapprochement bancaire »).
6. Empty-state honnête (« aucun règlement pour ces filtres »), jamais de données factices.

## Livrables
- Écran « Rapprochement bancaire » branché sur l'endpoint réel.
- `VERIFY/TASK-037_verify.md` : captures montrant la grille règlement-pivot avec rapproché banque O/N, reste à affecter mis en évidence, drill-down vers factures/preuve TVA ; build `tsc + vite` + `oxlint` OK ; navigation sans erreur console.

## Critères de validation
- Grille lecture seule, pivot règlement, dense, pleine hauteur/largeur.
- `rapprocheBanque` / `declare` / `origine` affichés fidèlement (données TASK-036).
- **Reste à affecter non nul rendu visible** (pilier confiance).
- Drill-down vers les factures affectées + preuve TVA fonctionnel.
- Filtres/tri/pagination opérants ; empty-state honnête ; aucune donnée factice.
- Aucune action d'écriture, aucun couplage `gocom-web`.

## Risques / dépendances
- **Dépend de TASK-036** (endpoint) : chemin critique **036 → 037**.
- **Dépend de TASK-035** (shell 2 entrées) pour le point d'entrée, et de **TASK-034** (discipline DTO d'affichage).
- Réutiliser au maximum les patrons de `DomainGrid` (virtualisation, filtres) pour ne pas diverger — mais **ne pas** réutiliser la partie « actions de masse » (lecture seule ici).
- Garder la densité fonctionnelle (lisibilité comptable), pas cosmétique.
