# TASK-192 — Motif « Règlement non comptabilisé » visible sans survol (écran Factures)

Status: ✅ Terminé
Date: 05/08/2026
Module: declaration-tva-web
Fichiers modifiés:
- `declaration-tva-web/src/FactureInterrogation.tsx`

---

## 1. Contexte & Constat

Sur l'écran **Factures** (`FactureInterrogation.tsx`), lorsqu'une facture n'est pas valorisée (`row.valorisee = false`) et qu'aucun montant brut Sage n'est disponible (`brutValue == null`), la cellule affichait uniquement un badge générique `(?) non valorisé`. Le motif exact (`row.motifValorisation`, ex. « Règlement non comptabilisé ») était relégué dans l'attribut `title` (survol au survolateur).

Sur des volumes importants de factures (ex. factures MAROC TELECOM `FF260005`, `FF260006` rapprochées en banque mais non comptabilisées dans Sage `MV_Compta=0`), la cause de non-valorisation restait invisible en un coup d'œil, contredisant la règle n°1 du projet (« aucune ligne silencieuse »).

---

## 2. Solution apportée

1. **Helper `formatMotifCourt(motif)`** :
   Convertit le motif long backend en libellé succinct pour l'affichage inline sans déformer la grille :
   - `Règlement non comptabilisé` → `non comptabilisé`
   - `Règlement non rapproché` → `non rapproché`
   - `Rapproché hors de la période` → `hors période`
   - `Rapproché mais non affecté à une facture` / `Facture non affectée` → `non affecté`
   - `Règlement annulé` → `annulé`
   - `Règlement impayé` → `impayé`
   - `Déjà déclaré...` → `déjà déclaré`
   - `Facture introuvable...` → `introuvable Sage`
   - `OM non lue` / `absente du cache` → `OM non lue`
   - `Solde initial...` → `solde initial`
   - `FGR...` → `FGR hors cache`
   - `Incohérence Sage...` → `incohérence Sage`

2. **Affichage dans `CelluleB`** :
   Le badge affiche désormais directement : `(?) non valorisé — <motif court>` (ex. `(?) non valorisé — non comptabilisé`).
   L'attribut `title={motif}` conserve le motif complet pour le survol.

3. **Invariants métier & garde-fous préservés** :
   - Règle métier inchangée : `NonComptabilise` reste un motif bloquant (`MV_Compta` reste la condition dans `SelectionExpliqueeEvaluator.cs:70`).
   - `EstValorisable` inchangé (`SelectionExpliqueeModels.cs`).
   - Montants bruts Sage (`brutValue != null`, TASK-076) : bloc d'affichage préservé sans régression.
   - Re-utilisation stricte de `row.motifValorisation` de l'API (aucun nouvel endpoint).

---

## 3. Validation

- Build TypeScript : `npx tsc --noEmit` OK (0 erreur).
- Build Web Vite : `npm run build` OK.
- Tests unitaire solution : `dotnet test DeclarationTVA.slnx` OK.
