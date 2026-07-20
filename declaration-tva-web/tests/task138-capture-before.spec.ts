import { test, type Page } from '@playwright/test';

// TASK-138 — capture « avant » (ancien <table>), sans assertion de rôles ARIA (absents de l'ancien
// rendu). Sert de comparatif visuel joint à la VERIFY. Réutilise le même harnais + mêmes données.

const MOTIF_LONG =
  'Incohérence Sage : Σ(HT+TVA+Parafiscale)=3960,00 ≠ TTC=3762,00 (écart -198,00)';
const ITEMS = [
  { id: '1', factureNumero: 'FC2501717', tiers: 'FOURNISSEUR ALPHA SARL', origine: 'Sage / OM', montantHT: 3135, tauxTVA: 20, montantTTC: 3762, source: 'Décaissement', statutLigne: 1, motif: '' },
  { id: '2', factureNumero: 'FC2501667', tiers: 'BETA DISTRIBUTION SA', origine: 'FGR', montantHT: 3300, tauxTVA: 20, montantTTC: 3960, source: 'Décaissement', statutLigne: 4, motif: MOTIF_LONG },
  { id: '3', factureNumero: 'FC2501702', tiers: 'GAMMA SERVICES', origine: 'Solde initial', montantHT: 1200, tauxTVA: 14, montantTTC: 1368, source: 'Décaissement', statutLigne: 0, motif: '' },
  { id: '4', factureNumero: 'FC2501688', tiers: 'DELTA IMPORT EXPORT SARL AU', origine: 'Sage / OM', montantHT: 875.5, tauxTVA: 10, montantTTC: 963.05, source: 'Décaissement', statutLigne: 2, motif: 'Hors champ TVA' },
  { id: '5', factureNumero: 'FC2501690', tiers: 'EPSILON NEGOCE', origine: 'Sage / OM', montantHT: 5400, tauxTVA: 20, montantTTC: 6480, source: 'Décaissement', statutLigne: 3, motif: '' },
];
const DISTINCTS: Record<string, string[]> = {
  origine: ['Sage / OM', 'FGR', 'Solde initial'], tauxTVA: ['10', '14', '20'],
  source: ['Décaissement'], statutLigne: ['Proposée', 'Intégrée', 'Exclue', 'Reportée', 'Écartée'],
};
async function mockLignes(page: Page) {
  await page.route('**/api/declarations/*/lignes*', (route) =>
    route.fulfill({ json: { items: ITEMS, totalCount: ITEMS.length, distincts: DISTINCTS } }));
}

test('avant A non-readonly', async ({ page }) => {
  await mockLignes(page);
  await page.goto('/task138.html?readonly=0');
  await page.getByText('FC2501717').waitFor();
  await page.screenshot({ path: '../VERIFY/task138-avant-A-non-readonly.png', fullPage: true });
});
test('avant B readonly', async ({ page }) => {
  await mockLignes(page);
  await page.goto('/task138.html?readonly=1');
  await page.getByText('FC2501717').waitFor();
  await page.screenshot({ path: '../VERIFY/task138-avant-B-readonly-drill.png', fullPage: true });
});
