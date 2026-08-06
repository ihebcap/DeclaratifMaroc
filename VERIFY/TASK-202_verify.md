# Verification Report — TASK-202: Refonte navigation TVA en 4 écrans à responsabilité unique

## Date & Execution Context
- **Date**: 2026-08-07
- **Task ID**: TASK-202 (remplaces TASK-178)
- **Scope**: Restructure TVA declaration flow into 4 distinct single-responsibility stepper steps:
  1. **① Sélection** (`ReglementsSelection.tsx`) — Payment selection.
  2. **② Factures à déclarer** (`FacturesADeclarerPanel.tsx` / `DomainGrid.tsx`) — Persistent view of candidate invoices with AG Grid columns, code activite options, line actions (resync, solde initial TVA), and bulk actions (Intégrer, Réinitialiser).
  3. **③ Vérifier** (`VerifierIntegrerPanel.tsx` mode `verifier`) — Consultative controls & diagnostic screen (`ChecklistCard`, `RecapSourceTable`, unvalorized lines + Diagnostiquer, blocking anomalies + "Voir lignes" redirecting to step ② with pre-applied filter). **No confirmation button here.**
  4. **④ Confirmer** (`VerifierIntegrerPanel.tsx` mode `confirmer`) — Final summary & integration screen (4 `RecapCard` stat cards, "Sous-totaux par taux TVA" table without duplicate total row, Excel Control Export button, and **"Confirmer intégration"** button enabled only if step ③ has 0 blocking anomalies).
- **Status**: COMPLETE & VERIFIED

---

## Decisions Taken & Documented

1. **Stepper 4-Step Architecture**:
   - Stepper (`DeclarationStepper.tsx`) now exposes 4 steps before final submission: `1. Sélection`, `2. Factures à déclarer`, `3. Vérifier`, `4. Confirmer`, followed by `5. Déclaration`.

2. **Binary Line Status (PO Decision #4)**:
   - Invoices are either `Proposée` (candidate, payment selected in step ①) or `Intégrée` (integrated upon confirmation).
   - Removed redundant bulk action buttons "Exclure" and "Reporter" from `DomainGrid.tsx` (a user excludes/defers an invoice by omitting its payment in step ①). Bulk actions now consist of "Intégrer" and "Réinitialiser" (sets status back to Proposée).

3. **Single Source of Truth per Metric (No Duplication)**:
   - Stat cards (`RecapCard`) and sub-totals table are exclusive to step ④ ("Confirmer").
   - Duplicate `Σ Total` row in sub-totals table removed (since "Total TVA à intégrer" is already displayed in `RecapCard`).
   - Diagnostic and anomaly details are exclusive to step ③ ("Vérifier").
   - Step ④ relays global status and provides link "Voir le détail (étape ③)" if step ③ reports blocking anomalies.

4. **Targeted Sage Re-read in `DiagnosticModal.tsx`**:
   - Kept "Relire depuis Sage" button inside `DiagnosticModal.tsx` for targeted single-line diagnosis alongside the bulk/row actions in step ②.

---

## Verification Evidence & Build Results

### 1. Frontend Build Verification
Command: `npm run build` inside `declaration-tva-web/`.
Result:
```
vite v8.1.3 building client environment for production...
transforming...✓ 1856 modules transformed.
rendering chunks...
dist/index.html 0.68 kB │ gzip: 0.36 kB
dist/assets/index-Ci9HlRXU.css 265.35 kB │ gzip: 44.94 kB
dist/assets/index-FaRpDd9x.js 1,894.10 kB │ gzip: 537.98 kB
✓ built in 1.56s
Exit Code: 0
```

### 2. Backend Build Verification
Command: `dotnet build DeclarationTVA.slnx`
Result:
```
La génération a réussi.
0 Erreur(s)
24 Avertissement(s)
```

---

## Checklist of Requirements (6/6)

1. [x] **Stepper 4 Steps Integration**:
   - `DeclarationStepper.tsx` updated with 4 steps: ① Sélection, ② Factures à déclarer, ③ Vérifier, ④ Confirmer (+ ⑤ Déclaration).

2. [x] **Persistent Step ② ("Factures à déclarer")**:
   - Created `FacturesADeclarerPanel.tsx` wrapping `DomainGrid` with Achats/Ventes domain tabs, `codeActiviteOptions`, and `showResynchroniserAction`.

3. [x] **Purely Consultative Step ③ ("Vérifier")**:
   - `VerifierIntegrerPanel` mode `verifier` contains verdict banner, 3-item checklist, discrepancy table, unvalorized lines, blocking anomalies, and warnings. No confirmation button or stat cards.

4. [x] **Summary & Confirmation Step ④ ("Confirmer")**:
   - `VerifierIntegrerPanel` mode `confirmer` contains 4 `RecapCard` stat cards, sub-totals table without duplicate total row, Excel export button, and "Confirmer intégration" button (disabled with link to step ③ if blocked).

5. [x] **Navigation "Voir lignes" -> Step ②**:
   - Clicking "Voir lignes" on an anomaly in step ③ invokes `onVoirLignes(domaine, filter)` which switches to step ② with pre-applied filters.

6. [x] **Binary Line Status**:
   - Redundant bulk actions "Exclure" and "Reporter" removed from `DomainGrid.tsx`.
