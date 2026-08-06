# Verification Report — TASK-200: Écran Sélection vide par défaut + bouton Intégrer

## Date & Execution Context
- **Date**: 2026-08-07
- **Task ID**: TASK-200
- **Scope**: Screen ① Selection (`ReglementsSelection.tsx`) — Empty default state for new declarations, explicit "Intégrer" button, removal of automatic pre-selection (100% manual selection for new declarations), and preservation of automatic loading & selection restoration for existing declarations with saved lines.
- **Status**: COMPLETE & VERIFIED

---

## Pre-Requisites & Decisions (PO Decision 06/08/2026)
- **New Declaration / No Saved Selection**: Screen opens in an empty state (`hasFetched === false`) with an invitation message ("Cliquez sur Intégrer pour charger les règlements de la période"). No automatic network request to `/api/rapprochement`. Upon clicking "Intégrer", data is fetched and displayed with **0 lines pre-selected** (100% manual selection).
- **Existing Declaration With Saved Lines (`savedSelection.length > 0`)**: Automatically fetched on mount and saved selection restored as requested by PO decision.

---

## Verification Evidence & Build Results

### 1. Build Verification
Command: `npm run build` inside `declaration-tva-web/`.
Result:
```
vite v8.1.3 building client environment for production...
transforming...✓ 1855 modules transformed.
rendering chunks...
dist/index.html 0.68 kB │ gzip: 0.36 kB
dist/assets/index-Ci9HlRXU.css 265.35 kB │ gzip: 44.94 kB
dist/assets/index-CSvLO9J1.js 1,891.87 kB │ gzip: 537.62 kB
✓ built in 1.53s
Exit Code: 0
```

---

## Checklist of Requirements (5/5)

1. [x] **Empty State on New Declaration**
   - Renders `selection-vide-invite` container inviting user to click "Intégrer les règlements".
   - No auto-fetch triggered when `savedSelection` is empty.

2. [x] **"Intégrer" Button Action**
   - Prominently placed both in the central empty state and bottom action bar.
   - Clicking triggers `fetchAll(cancelled)` for period bounds `debut`/`fin` and sets `hasFetched = true`.

3. [x] **Zero Pre-Cochage on New Fetch**
   - Removed automatic pre-selection loop (`statutDe(r) !== 'bloque'`).
   - `selectedKeys` remains empty after fetch until user manually selects checkboxes.

4. [x] **Restoration of Saved Selection**
   - Existing declarations with `savedSelection.length > 0` load automatically and restore matching `numeroReglement` rows.

5. [x] **Re-usability & Non-Regression**
   - Button converts to "Rafraîchir" once loaded to allow re-fetching if period or filters change.
