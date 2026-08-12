import { test, expect } from '@playwright/test';

// TASK-068 — sélecteur de colonnes persistant. Écran testé : Rapprochement bancaire
// (lecture seule, aucune écriture DB) — masquer une colonne, vérifier la disparition
// entête+corps, recharger la page, vérifier la persistance localStorage.

test('Sélecteur de colonnes — masquage + persistance (Rapprochement bancaire)', async ({ page }) => {
  test.setTimeout(60000);
  await page.goto('/');
  await page.fill('input[type="text"]', 'Admin');
  await page.fill('input[type="password"]', 'Admin');

  const select = page.getByRole('combobox');
  await select.waitFor({ state: 'attached' });
  await select.locator('option').nth(1).waitFor({ state: 'attached' });
  await select.selectOption({ index: 1 });
  await page.click('button:has-text("Se connecter")');

  await page.click('.sidebar-item:has-text("Rapprochement bancaire")');
  await page.waitForSelector('.animate-spin', { state: 'detached' });

  const header = page.locator('.ag-header, div[style*="sticky"]').first();
  await expect(header.getByText('Tiers', { exact: true })).toBeVisible();

  // Ouvre le sélecteur de colonnes et masque « Tiers ».
  await page.click('button:has-text("Colonnes")');
  await page.locator('label:has-text("Tiers") input[type="checkbox"]').uncheck();
  await page.mouse.click(10, 10);

  await expect(header.getByText('Tiers', { exact: true })).not.toBeVisible();

  // Recharge la page (le routage interne n'est pas persisté, seule la préférence
  // de colonnes doit l'être) : on retombe sur le tableau de bord, on renavigue.
  await page.reload();
  await page.waitForSelector('.animate-spin', { state: 'detached' });
  await page.click('.sidebar-item:has-text("Rapprochement bancaire")');
  await page.waitForSelector('.animate-spin', { state: 'detached' });
  const headerAfterReload = page.locator('.ag-header, div[style*="sticky"]').first();
  await expect(headerAfterReload.getByText('Tiers', { exact: true })).not.toBeVisible();

  // Vérifie la clé localStorage dédiée à cet écran.
  const stored = await page.evaluate(() => localStorage.getItem('grf.cols.rapprochement'));
  expect(stored).toBeTruthy();
  expect(JSON.parse(stored!)).not.toContain('tiers');

  // Réaffiche toutes les colonnes (repli sûr / reset).
  await page.click('button:has-text("Colonnes")');
  await page.click('button:has-text("Tout afficher")');
  await expect(headerAfterReload.getByText('Tiers', { exact: true })).toBeVisible();
});
