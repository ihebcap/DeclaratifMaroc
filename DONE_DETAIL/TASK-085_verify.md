# Verification of TASK-085 — Colonne TTC dans le tableau des sous-totaux (Calcul TVA)

This document verifies the implementation of adding the `Total TTC` column in the sub-totals by VAT rate table inside the **③ Calcul TVA** panel.

## 1. Code Modifications
The changes were performed in [CalculTvaPanel.tsx](file:///D:/_vibe/GRF/declaration-tva-web/src/CalculTvaPanel.tsx) at three distinct locations:
- **Header**: Added a new column header `Total TTC` at the end of the table header row.
- **Taux Rows**: Added a table cell displaying the sum of `totalHT` and `totalTVA` formatted via `formatMoney` (`formatMoney(st.totalHT + st.totalTVA)`) with the proper weights and CSS styles.
- **Σ Total Row**: Added a table cell displaying the global sum of `totalHT` and `totalTVA` (`formatMoney(totalHT + totalTVA)`).

## 2. Test Execution
All E2E Playwright tests were updated to support the new case-sensitive credentials (`Admin` / `Admin`) introduced by the recent real authentication implementation. 
The tests passed successfully:
- `column-selector.spec.ts` -> **PASSED**
- `declaration.spec.ts` -> **PASSED**

## 3. Mathematical Verification (June 2026)
Below is the breakdown of the sub-totals by VAT rate and the overall totals verified on the June 2026 declaration created during the test run (Declaration ID: `2fba1f55-b017-45a0-8e50-7889f10ad1d5`):

| Taux | Total HT (MAD) | Total TVA (MAD) | Total TTC (HT + TVA) (MAD) | Verification |
| --- | --- | --- | --- | --- |
| **20%** | 10,579.18 | 2,115.84 | 12,695.02 | `10579.18 + 2115.84 = 12695.02` (Exact) |
| **10%** | 1,180.05 | 118.01 | 1,298.06 | `1180.05 + 118.01 = 1298.06` (Exact) |
| **0%** | 188.60 | 0.00 | 188.60 | `188.60 + 0.00 = 188.60` (Exact) |
| **Σ Total** | **11,947.83** | **2,233.85** | **14,181.68** | `11947.83 + 2233.85 = 14181.68` (Exact) |

The visual verification screenshot was captured at `VERIFY/10-synthese-overview.png`.
