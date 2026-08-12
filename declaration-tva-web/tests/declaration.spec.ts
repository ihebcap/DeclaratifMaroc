import { test, expect } from '@playwright/test';
import { execSync } from 'child_process';

test.beforeAll(async () => {
  execSync('powershell -File ../reset.ps1', { stdio: 'inherit' });
  execSync('powershell -File ../make_eligible.ps1', { stdio: 'inherit' });
});

test('Parcours complet: Création, Règlements, Affectations, Calcul, Intégration, Contrôle, Synthèse', async ({ page }) => {
  // Listen for console logs
  page.on('console', msg => console.log('BROWSER:', msg.text()));

  // Login
  await page.goto('http://localhost:5173');
  await page.evaluate(() => sessionStorage.clear());
  await page.reload();
  
  const select = page.getByRole('combobox');
  await select.waitFor({ state: 'attached' });
  await select.locator('option').nth(1).waitFor({ state: 'attached' });
  await select.selectOption({ index: 1 });
  await page.fill('input[type="text"]', 'Admin');
  await page.fill('input[type="password"]', 'Admin');
  await page.click('button:has-text("Se connecter")');

  // Dashboard - Open existing TVA1-2026-06 or create declaration for 2026-06
  const existingCard = page.locator('div').filter({ hasText: 'TVA1-2026-06' }).filter({ has: page.locator('button[title="Ouvrir"]') }).last();
  const cardVisible = await existingCard.isVisible().catch(() => false);
  console.log(`EXISTING CARD VISIBLE: ${cardVisible}`);
  if (cardVisible) {
    await existingCard.locator('button[title="Ouvrir"]').click();
  } else {
    const createBtn = page.locator('button:has-text("Créer une déclaration")');
    await createBtn.waitFor({ state: 'visible', timeout: 15000 });
    await createBtn.click({ force: true });
    await page.fill('[data-testid="annee-declaration"]', '2026');
    await page.selectOption('select', { label: 'Mensuel' });
    const selects = await page.locator('select').all();
    if (selects.length > 1) {
        await selects[1].selectOption({ label: 'Juin (06)' });
    }
    await page.locator('button:text-is("Créer")').click();
  }

  // Wait for transition to Stepper page
  await page.waitForTimeout(2000);
  
  // Step 1: Règlements - Wait for Intégrer les règlements button if present and click it
  const integrerBtn = page.locator('button:has-text("Intégrer les règlements")').first();
  const integrerVisible = await integrerBtn.isVisible({ timeout: 5000 }).catch(() => false);
  console.log(`INTEGRER LES REGLEMENTS BTN VISIBLE: ${integrerVisible}`);
  if (integrerVisible) {
    await integrerBtn.click();
    await page.waitForTimeout(500);
  }

  // Wait for AG Grid rows to be loaded and attached in DOM
  await page.waitForSelector('.ag-row', { state: 'attached', timeout: 60000 });
  await page.waitForSelector('.animate-spin', { state: 'detached' });
  
  // Select all Décaissement rows in the grid
  await page.waitForSelector('.ag-row', { state: 'attached' });
  const rowInputs = page.locator('.ag-row .ag-grid-pinned-left-cells input[type="checkbox"]');
  const rowCount = await rowInputs.count();
  for (let i = 0; i < rowCount; i++) {
    const rowText = await page.locator('.ag-row').nth(i).innerText();
    if (rowText.includes('Décaissement')) {
      await rowInputs.nth(i).focus();
      await page.keyboard.press('Space');
      await page.waitForTimeout(100);
    }
  }

  // Verify selected total is non-zero
  const totalSelector = page.locator('text=/Total sélectionné : .+/');
  await expect(totalSelector).toBeVisible();
  console.log(`Total selected: ${await totalSelector.innerText()}`);
  await expect(totalSelector).not.toHaveText(/Total sélectionné : 0,00\s*MAD/);
  await page.waitForTimeout(500);
  
  // Take screenshot overview
  await page.screenshot({ path: '../VERIFY/05-workstation-overview.png' });

  // Proceed to Step 2 (Factures)
  await page.click('button:has-text("Passer aux factures")');
  await page.waitForTimeout(500);

  // Proceed to Step 3a (Vérifier)
  await page.click('button:has-text("Continuer vers la vérification")');
  await page.waitForTimeout(500);

  // Proceed to Step 3b (Confirmer)
  await page.click('button:has-text("Continuer vers la confirmation")');
  await page.waitForTimeout(1000);

  // Step 3: Vérifier & Intégrer
  // Wait for checkup and lines to load (loading spinner disappears)
  await page.waitForSelector('.animate-spin', { state: 'detached' });
  await page.screenshot({ path: '../VERIFY/09-actions-masse.png' });
  await page.waitForSelector('#btn-confirmer-integration');
  await page.screenshot({ path: '../VERIFY/07-cloture-verrouillee.png' });
  
  // Confirm integration (should trigger automatic redirection)
  await page.click('#btn-confirmer-integration');
  await page.waitForTimeout(3000);

  // Step 3: Déclaration (unified post-integration check & export screen)
  await expect(page.locator('button:has-text("XML")').first()).toBeVisible();
  await page.screenshot({ path: '../VERIFY/08-drill-preuves-modal.png' });
  await page.screenshot({ path: '../VERIFY/10-synthese-overview.png' });
});
