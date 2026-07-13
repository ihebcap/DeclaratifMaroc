# TASK-056 — Écran ③ Calcul TVA (valorisation avant intégration)

> Étape ③ du tunnel (TASK-053). Front (lecture seule). Densité **0 espace perdu**. Vue de contrôle du calcul **avant** de figer.

## Contexte
Avant l'intégration, afficher la **valorisation agrégée** de la sélection : par facture et par taux, avec totaux (`reflexion dectva.md` §4). C'est la vue qui « valorise » la lecture Sage, le cache, la proratisation et le multi-taux. Les composants `ControlGrid.tsx` et `SummaryPanel.tsx` existent et fournissent le patron d'agrégation dense.

## Périmètre STRICT
- **Inclus** : tableau dense `Facture / Taux / HT déclaré / TVA déclarée` ; totaux par taux ; total TVA global ; état de valorisation explicite par ligne (valorisé / non valorisé + motif).
- **Exclu** : le drill facture (② = TASK-055) ; l'action d'intégration (④ = TASK-057) ; tout calcul côté front (les montants viennent du back valorisé).

## Objectif
```
Entrée : sélection valorisée de la déclaration (par facture, par taux)
Traitement : agréger/afficher HT & TVA déclarés, ventiler par taux
Sortie : vue lecture seule de contrôle, totaux par taux + global, avant intégration
```

## Étapes
1. Câbler `ControlGrid`/`SummaryPanel` sur la valorisation existante de la déclaration.
2. Ventilation par taux (20/14/10/…) avec sous-totaux, densité maximale.
3. État de valorisation par ligne : jamais un `0` muet — « non valorisé » + motif (cohérent TASK-055, mémoire `grf-valorisation-tracabilite-blocage-om`).
4. Total TVA global mis en évidence (bandeau/pied), aligné avec ce que ④ intégrera.

## Livrables
- Écran ③ (adaptation `ControlGrid`/`SummaryPanel`).
- `VERIFY/TASK-056_verify.md` : preuve réelle (totaux par taux = somme des lignes, total global cohérent avec ②), lignes non valorisées tracées, build + lint verts.

## Critères de validation
- Σ(TVA par taux) = total TVA global = total agrégé des affectations (②).
- Aucun front-calcul : les montants proviennent du back valorisé (source unique).
- Lignes non valorisées visibles avec motif (transparence).
- Lecture seule stricte ; densité 0 espace perdu.

## Risques / dépendances
- Dépend de TASK-055 (valorisation par affectation) et TASK-053.
- Réutilise `ControlGrid`/`SummaryPanel` + valorisation TASK-022/023/024.
- Cohérence des arrondis avec ② (DGI/`AwayFromZero`).
- 🚫 **Anti-régression** : `ControlGrid.tsx`/`SummaryPanel.tsx` et la valorisation back sont **intouchables** — cette task ne fait qu'**agréger/afficher** en lecture seule. Aucun front-calcul (source unique = back valorisé) ; les montants doivent être identiques à ceux de ②.
