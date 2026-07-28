# TASK-136 — Menu : nouvelle entrée "Délai de paiement"

## Contexte
Câblage final du périmètre Délai de Paiement Maroc dans la navigation (`App.tsx`, shell menu groupé
INTERROGATION / DÉCLARATION / À VENIR, TASK-035). Décision PO (19/07/2026) : **entrée de menu autonome**,
sur le même modèle que "Déclaration TVA" (pas un regroupement sous une entrée existante).

## Objectif
```
Groupe DÉCLARATION :
  - Déclaration TVA          (existant, inchangé)
  - Délai de paiement         (NOUVEAU — cette tâche)
      ├─ Déclarations (liste + fiche, TASK-134)
      ├─ Sélection/Contrôle des lignes hors délai (TASK-134)
      └─ Conventions par tiers (TASK-130)
  - Relevé de déductions      (existant, placeholder ⚪)

Groupe INTERROGATION : inchangé — Rapprochement bancaire et Factures restent où ils sont (l'extension
de l'écran Factures, TASK-135, n'est qu'un ajout de colonnes, pas un déplacement d'écran).
```

## Périmètre STRICT
- **Inclus** : nouvelle entrée de menu + sous-navigation interne au domaine Délai de paiement, routage
  vers les écrans livrés en TASK-130/134.
- **Exclu** : toute modification des écrans Rapprochement/Factures/Déclaration TVA existants au-delà du
  strict nécessaire de navigation (aucun changement de leur contenu, cf. TASK-135 traitée séparément).

## Étapes
1. Ajouter l'entrée "Délai de paiement" dans `App.tsx` (groupe DÉCLARATION), avec sous-navigation interne
   (Déclarations / Conventions), cohérente avec le style existant (icônes `lucide-react`, densité).
2. Router vers les écrans TASK-130 (conventions) et TASK-134 (déclarations DDP).
3. Vérifier qu'aucun écran existant (Rapprochement, Factures, Déclaration TVA) n'est déplacé ni modifié
   par cette tâche.
4. Build tsc+vite / oxlint 0 erreur ; test e2e Playwright de navigation (chaque sous-entrée atteint le
   bon écran).

## Livrables
- Entrée de menu + routage.
- Test e2e de navigation.

## Critères de validation
- Les 3 sous-écrans (Déclarations DDP, Sélection/Contrôle, Conventions) sont atteignables depuis la
  nouvelle entrée de menu.
- Aucune régression sur la navigation existante (Rapprochement, Factures, Déclaration TVA inchangés).
- Build 0 erreur, e2e vert.

## Risques / dépendances
- Dépend de TASK-130 (front conventions) et TASK-134 (front DDP) pour avoir des écrans réels à router —
  peut être développé avec des routes/placeholders en amont si le planning l'exige, à l'image de ce qui
  a été fait pour TASK-035 (placeholder honnête tant que l'écran cible n'est pas livré).
