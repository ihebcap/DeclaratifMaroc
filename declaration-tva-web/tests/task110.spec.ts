import { test, expect } from '@playwright/test';
import { execSync } from 'child_process';

// ─── TASK-110 — Grille de drill « Vérifier & Intégrer » : mise en page dégradée ──
//
// Reproduit le cas réel signalé par le PO (17/07/2026) : un motif d'écartement long
// (FC2501667, incohérence Sage) injecté dans la réponse /lignes du drill (TASK-112 : axe
// « lignes incohérentes », remplace l'axe Source tautologique de TASK-107), pour vérifier
// que la colonne « Motif Écartement » ne déborde plus horizontalement.

const MOTIF_LONG = 'Incohérence Sage : Σ(HT+TVA+Parafiscale)=3960,00 ≠ TTC=3762,00 (écart -198,00)';

test.beforeAll(async () => {
  try {
    execSync('powershell -File ../reset.ps1');
    execSync('powershell -File ../make_eligible.ps1');
  } catch (e) {
    console.error('Failed to reset DB / make eligible:', e);
  }
});

test('Test TASK-110: motif long dans la grille de drill source ne deborde plus', async ({ page }) => {
  page.on('console', msg => console.log('BROWSER:', msg.text()));

  // Login
  await page.goto('/');
  await page.fill('input[type="text"]', 'Admin');
  await page.fill('input[type="password"]', 'Admin');

  const select = page.getByRole('combobox');
  await select.waitFor({ state: 'attached' });
  await select.locator('option').nth(1).waitFor({ state: 'attached' });
  await select.selectOption({ index: 1 });
  await page.click('button:has-text("Se connecter")');

  // Create new declaration
  await page.click('button:has-text("Créer une déclaration")');
  await page.fill('input[type="number"]', '2026');
  await page.selectOption('select', { label: 'Mensuel' });

  const selects = await page.locator('select').all();
  if (selects.length > 1) {
    await selects[1].selectOption({ label: 'Juin (06)' });
  }
  await page.locator('button:text-is("Créer")').click();
  await page.waitForTimeout(2000);

  const totalSelector = page.locator('text=/Total sélectionné : .+/');
  await expect(totalSelector).toBeVisible();

  const currentTotal = await totalSelector.innerText();
  if (!currentTotal.includes('0,00 MAD')) {
    await page.locator('input[type="checkbox"]').first().click();
    await expect(totalSelector).toContainText('0,00 MAD');
  }

  const enabledCheckboxes = page.locator('div[style*="absolute"]').filter({ hasText: 'Éligible' }).locator('input[type="checkbox"]');
  const nbEligibles = await enabledCheckboxes.count();
  const nbASelectionner = Math.min(5, nbEligibles);
  for (let i = 0; i < nbASelectionner; i++) {
    await enabledCheckboxes.nth(i).click();
  }
  await expect(totalSelector).not.toHaveText(/Total sélectionné : 0,00\s*MAD/);
  await page.waitForTimeout(500);

  // Force un écart + une ligne incohérente synthétique pour afficher le tableau du drill
  await page.route('**/declarations/*/checkup', async (route) => {
    const response = await route.fetch();
    const json = await response.json();
    if (!json.recapSource || json.recapSource.length === 0) {
      throw new Error('checkup réel sans lignes valorisées — jeu de données insuffisant pour la preuve TASK-110');
    }
    const ecart = json.equilibre?.ecart || 1;
    json.equilibre = { isValid: false, ecart, ecartExplique: true };
    json.recapIncoherence = [
      { domaine: 'Decaissement', incoherente: true, ht: 0, tva: 0, ttc: ecart, residu: ecart, nbLignes: 1 },
    ];
    await route.fulfill({ json });
  });

  // Injecte le motif long réel (cas PO FC2501667) dans la réponse du drill. TASK-112 : le drill
  // filtre désormais sur `incoherente=true` (lignes qui composent réellement l'écart) au lieu de
  // `source` (tautologique, renvoyait toujours >0 ligne) — sur ce jeu de test synthétique, ce
  // sous-ensemble peut être réellement vide ; on retombe alors sur une ligne réelle non filtrée
  // du même domaine comme gabarit, pour garder la preuve de non-débordement indépendante des
  // données de test.
  await page.route('**/declarations/*/lignes*', async (route, request) => {
    const response = await route.fetch();
    const json = await response.json();
    let items = json.items || json.data || (Array.isArray(json) ? json : []);

    if (items.length === 0 && request.url().includes('incoherente')) {
      const sansFiltre = request.url().replace(/([?&])filter=[^&]*&?/, '$1').replace(/[?&]$/, '');
      const gabaritRes = await route.fetch({ url: sansFiltre });
      const gabaritJson = await gabaritRes.json();
      const gabaritItems = gabaritJson.items || gabaritJson.data || (Array.isArray(gabaritJson) ? gabaritJson : []);
      if (gabaritItems.length > 0) {
        json.items = [gabaritItems[0]];
        if ('totalCount' in json) json.totalCount = 1;
        items = json.items;
      }
    }

    if (items.length > 0) {
      items[0].motif = MOTIF_LONG;
      items[0].factureNumero = 'FC2501667';
      items[0].statutLigne = 4; // Écartée
    }
    await route.fulfill({ json });
  });

  await page.click('button:has-text("Passer au calcul")');
  await page.waitForTimeout(2000);
  await page.waitForSelector('.animate-spin', { state: 'detached' });

  await expect(page.locator('text=/Écart détecté/')).toBeVisible();

  const recapIncoherenceTable = page.locator('table').filter({ has: page.locator('th', { hasText: 'Écart' }) });
  const incoherenteRow = recapIncoherenceTable.locator('tbody tr').first();
  await expect(incoherenteRow).toBeVisible();
  await incoherenteRow.click();
  await page.waitForTimeout(1000);

  await expect(page.locator('text=Drill écart :')).toBeVisible();
  await expect(page.locator('text=/Total résultats : \\d+/')).toBeVisible();

  // Preuve visuelle : le motif long est visible dans la grille (tronqué à l'affichage,
  // texte complet accessible via `title`), sans défilement horizontal disproportionné.
  // TASK-138 : DomainGrid rend désormais des <div role="cell"> (flexbox) au lieu de <td>.
  const motifCell = page.getByRole('cell').filter({ hasText: 'Incohérence Sage' }).first();
  await expect(motifCell).toBeVisible();
  await motifCell.scrollIntoViewIfNeeded();
  await page.waitForTimeout(300);
  await page.screenshot({ path: '../VERIFY/task110-drill-motif-long.png', fullPage: true });

  const cellBox = await motifCell.boundingBox();
  console.log('Largeur cellule motif :', cellBox?.width);
  expect(cellBox?.width ?? 0).toBeLessThan(400);

  console.log('Finished!');
});
