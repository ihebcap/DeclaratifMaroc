import { test, expect, type Page } from '@playwright/test';

// ─── TASK-138 — DomainGrid : migration <table>+<tr position:absolute> → flexbox <div> ──────
//
// Preuve visuelle RÉELLE exigée par l'Étape 5 de la task (deux corrections TASK-113 validées par
// build + revue statique mais infirmées en test réel PO). Le défaut est purement CSS/layout :
// l'en-tête et les lignes divergeaient en largeur. On rend donc le VRAI composant DomainGrid dans
// Chromium (harnais task138.html), avec les réponses /api mockées au niveau réseau — pas besoin du
// backend .NET ni de la base prod. On MESURE ensuite les bords gauche/droite de chaque cellule
// d'en-tête et de la 1re ligne : l'alignement strict est prouvé par les coordonnées réelles du
// rendu, pas par une lecture de code.

const MOTIF_LONG =
  'Incohérence Sage : Σ(HT+TVA+Parafiscale)=3960,00 ≠ TTC=3762,00 (écart -198,00)';

// Cas exact du signalement PO (FC2501717 / FC2501667), 9 colonnes par défaut renseignées.
const ITEMS = [
  { id: '1', factureNumero: 'FC2501717', tiers: 'FOURNISSEUR ALPHA SARL', origine: 'Sage / OM', montantHT: 3135, tauxTVA: 20, montantTTC: 3762, source: 'Décaissement', statutLigne: 1, motif: '' },
  { id: '2', factureNumero: 'FC2501667', tiers: 'BETA DISTRIBUTION SA', origine: 'FGR', montantHT: 3300, tauxTVA: 20, montantTTC: 3960, source: 'Décaissement', statutLigne: 4, motif: MOTIF_LONG },
  { id: '3', factureNumero: 'FC2501702', tiers: 'GAMMA SERVICES', origine: 'Solde initial', montantHT: 1200, tauxTVA: 14, montantTTC: 1368, source: 'Décaissement', statutLigne: 0, motif: '' },
  { id: '4', factureNumero: 'FC2501688', tiers: 'DELTA IMPORT EXPORT SARL AU', origine: 'Sage / OM', montantHT: 875.5, tauxTVA: 10, montantTTC: 963.05, source: 'Décaissement', statutLigne: 2, motif: 'Hors champ TVA' },
  { id: '5', factureNumero: 'FC2501690', tiers: 'EPSILON NEGOCE', origine: 'Sage / OM', montantHT: 5400, tauxTVA: 20, montantTTC: 6480, source: 'Décaissement', statutLigne: 3, motif: '' },
];

const DISTINCTS: Record<string, string[]> = {
  origine: ['Sage / OM', 'FGR', 'Solde initial'],
  tauxTVA: ['10', '14', '20'],
  montantTTC: [],
  source: ['Décaissement'],
  statutLigne: ['Proposée', 'Intégrée', 'Exclue', 'Reportée', 'Écartée'],
};

async function mockLignes(page: Page) {
  await page.route('**/api/declarations/*/lignes*', async (route) => {
    await route.fulfill({
      json: { items: ITEMS, totalCount: ITEMS.length, distincts: DISTINCTS },
    });
  });
}

// Vérifie que, colonne par colonne, le bord gauche et le bord droit de la cellule d'en-tête
// coïncident (à 1px près) avec ceux de la cellule de la 1re ligne. C'est la preuve mesurée de
// l'alignement, indépendante de toute lecture de code.
async function assertAlignment(page: Page) {
  const headers = page.locator('.ag-header-cell:visible, [role="columnheader"]');
  const firstRow = page.locator('.ag-center-cols-container .ag-row, .ag-row').first();
  const cells = firstRow.locator('.ag-cell:visible, [role="cell"]');

  const nH = await headers.count();
  const nC = await cells.count();
  expect(nH, 'même nombre de colonnes en-tête / corps').toBeGreaterThan(0);
  expect(nH, 'même nombre de colonnes en-tête / corps').toBe(nC);

  for (let i = 0; i < nH; i++) {
    const h = await headers.nth(i).boundingBox();
    const c = await cells.nth(i).boundingBox();
    expect(h, `header ${i} visible`).not.toBeNull();
    expect(c, `cell ${i} visible`).not.toBeNull();
    expect(Math.abs(h!.x - c!.x), `col ${i} bord gauche aligné`).toBeLessThanOrEqual(1);
    expect(Math.abs((h!.x + h!.width) - (c!.x + c!.width)), `col ${i} bord droit aligné`).toBeLessThanOrEqual(1);
  }
}

test('TASK-138 ① mode non-readonly (Affectations, case à cocher) — alignement en-tête/lignes', async ({ page }) => {
  await mockLignes(page);
  await page.goto('/task138.html?readonly=0');
  await expect(page.getByText('FC2501717')).toBeVisible();
  await expect(page.getByText('FC2501667')).toBeVisible();
  await assertAlignment(page);
  await page.screenshot({ path: '../VERIFY/task138-apres-A-non-readonly.png', fullPage: true });
});

test('TASK-138 ② mode readonly (drill TVA1-2026-01, cas PO) — alignement en-tête/lignes', async ({ page }) => {
  await mockLignes(page);
  await page.goto('/task138.html?readonly=1');
  await expect(page.getByText('FC2501717')).toBeVisible();
  await expect(page.getByText('FC2501667')).toBeVisible();
  await assertAlignment(page);

  // Non-régression TASK-110 : motif long tronqué (title complet), cellule bornée (pas de débordement).
  const motifCell = page.locator('.ag-cell, [role="cell"]').filter({ hasText: 'Incohérence Sage' }).first();
  await expect(motifCell).toBeVisible();
  const box = await motifCell.boundingBox();
  expect(box!.width, 'colonne Motif bornée (~280px)').toBeLessThan(400);

  await page.screenshot({ path: '../VERIFY/task138-apres-B-readonly-drill.png', fullPage: true });
});
