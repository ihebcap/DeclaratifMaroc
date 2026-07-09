# TASK-010 — Export Excel (Détail + Récap) — moyen de contrôle avant dépôt

## Contexte
Export Excel de la déclaration. ⚠️ **Décision PO** : le « moyen de contrôle avant dépôt » (§1sexies) est désormais assuré par les **grilles interactives du front** (TASK-013), **pas** par l'Excel. L'Excel devient un **artefact de dépôt/archive** (consultation hors ligne, pièce jointe) — un **rendu** de la `DeclarationModele` (TASK-005/006), à produire **après le front**.

> **Constat legacy (décompilation vérifiée)** : GRFN n'a **pas** d'état Excel dédié — juste un CSV brut (`CsvGenerator.GeneratModel<LigneDeclarationTvaEncaissementImport,…>`) et l'export DevExpress générique d'une grille. C'est le CSV que le client juge insuffisant. → **Rien à reproduire ici** : on construit un vrai classeur 2 feuilles, c'est la valeur ajoutée.

## Périmètre STRICT
- **Uniquement** : sérialiser une `DeclarationModele` en classeur Excel (2 feuilles).
- **Exclu** : tout calcul, toute lecture Sage/GRF, la génération XML (#7). Rendu pur.

## Positionnement / architecture
- Adaptateur **`Declaration.Export.Excel`** (`net10.0`) consommant `Declaration.Core` (le modèle). Aucune dépendance Sage/SQL.
- Lib Excel **sans COM Office** (ex. ClosedXML ou OpenXML SDK) — génération de fichier, pas d'automation.

## Objectif
```
Entrée : DeclarationModele
Sortie : fichier .xlsx à 2 feuilles
```
- **Feuille Détail** : 1 ligne par `LigneDeclarationEnrichie` — n° facture, désignation, tiers (nom, IF, ICE), HT, taux, TVA, TTC, mode, date paiement, date facture, source.
- **Feuille Récap** : totaux **par source**, **par taux**, **par code activité** + le **contrôle d'équilibre/résidu** (TASK-006) + la **liste des alertes** (ICE/IF manquants, ligne à 0, facture introuvable, règlement non affecté, résidu inexpliqué).

## Contraintes techniques
- `net10.0`, pur ; lib Excel en NuGet (pas d'Interop Office).
- Séparateur/format nombres cohérent ; montants `decimal`.
- Nom de fichier paramétrable ; écriture dans un chemin fourni (pas de chemin en dur).

## Étapes
1. Choisir la lib Excel (ClosedXML recommandé pour la simplicité) et l'ajouter au projet.
2. Feuille **Détail** (colonnes ci-dessus, une ligne par ligne enrichie).
3. Feuille **Récap** (récaps + contrôle + alertes, mise en forme lisible : titres, totaux).
4. **Tests** : générer depuis une `DeclarationModele` de fixture (les 2 factures TASK-002) et vérifier structure/valeurs (nb lignes, totaux, présence des alertes).

## Livrables
- `Declaration.Export.Excel` : `ExporterExcel(DeclarationModele, cheminSortie)`.
- Tests de génération (fixtures).
- `VERIFY/TASK-010_verify.md` : un .xlsx d'exemple + description des 2 feuilles.

## Critères de validation
- 2 feuilles conformes (Détail = 1 ligne/affectation ; Récap = totaux + contrôle + alertes).
- Aucune dépendance Sage/SQL/COM Office.
- Valeurs cohérentes avec le modèle (totaux Récap = agrégats du modèle).
- Désignation et prorata présents (dépend de TASK-006).

## Risques / dépendances
- **Non bloqué** (fixtures suffisent) mais **repoussé après le front** (§5 plan) : le contrôle passe par les grilles (TASK-013) ; l'Excel est un artefact secondaire.
- **Prérequis TASK-006** (désignation/prorata portés, contrôle réel) et TASK-005 (modèle).
- Désignation facture pas encore exposée par le worker (DTO) → colonne vide tant que non ajoutée côté OM ; ne pas bloquer l'export pour ça (le noter).
