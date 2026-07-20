import { test, expect } from '@playwright/test';
import { execSync } from 'child_process';

// ─── TASK-109 — « Détail des lignes » ouvert en tout premier ne doit pas figer
// zéro ligne ──
//
// Signalement PO (TVA1-2026-01, 145 règlements) : ouvrir le drill ② depuis ①
// AVANT tout clic sur « Passer au calcul » figeait zéro ligne côté serveur, car
// ConstruireLignesFigeesAsync filtre sur la sélection PERSISTÉE (jamais mise à
// jour par « Détail des lignes » avant ce correctif) — la grille affichait alors
// « Aucune affectation ne correspond aux filtres » malgré une sélection réelle
// non vide.

test.beforeAll(async () => {
  try {
    execSync('powershell -File ../reset.ps1');
    execSync('powershell -File ../make_eligible.ps1');
  } catch (e) {
    console.error('Failed to reset DB / make eligible:', e);
  }
});

test('Test TASK-109: Détail des lignes ouvert en tout premier reflète la sélection réelle', async ({ page }) => {
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

  // Nouvelle déclaration (jamais visitée — aucun appel /lignes préalable pour ce domaine)
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

  // Décoche tout, puis sélectionne des lignes réellement éligibles
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
  await page.screenshot({ path: '../VERIFY/task109-01-avant-drill.png' });

  // Ouvre « Détail des lignes » DIRECTEMENT — jamais cliqué « Passer au calcul »
  // avant, donc jamais eu de sélection persistée côté serveur avant ce correctif.
  await page.click('button:has-text("Détail des lignes")');
  await page.waitForSelector('.animate-spin', { state: 'detached' });
  await page.waitForTimeout(1000);

  // La grille ne doit PAS afficher un message d'absence de données — ni l'ancien
  // message trompeur "filtres" (aucun filtre actif ici), ni le nouveau message
  // honnête réservé au cas métier réel.
  await expect(page.locator('text=Aucune affectation ne correspond aux filtres.')).toHaveCount(0);
  await expect(page.locator('text=Aucune affectation trouvée pour les règlements sélectionnés.')).toHaveCount(0);

  // Au moins une ligne réellement affichée dans la grille du drill.
  const ligneCount = page.locator('text=/^\\d+ ligne/');
  await expect(ligneCount).toBeVisible();
  const ligneCountText = await ligneCount.innerText();
  console.log('Lignes affichées dans le drill (premier appel):', ligneCountText);
  expect(ligneCountText).not.toMatch(/^0 ligne/);

  await page.screenshot({ path: '../VERIFY/task109-02-drill-premier-appel.png' });

  console.log('Finished TASK-109 test!');
});
