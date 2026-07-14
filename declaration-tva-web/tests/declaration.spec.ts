import { test, expect } from '@playwright/test';
import { execSync } from 'child_process';

test.beforeAll(async () => {
  try {
    execSync('powershell -File ../reset.ps1');
  } catch (e) {
    console.error("Failed to reset DB:", e);
  }
});

test('Parcours complet: Création, Règlements, Affectations, Calcul, Intégration, Contrôle, Synthèse', async ({ page }) => {
  // Listen for console logs
  page.on('console', msg => console.log('BROWSER:', msg.text()));

  // Login
  await page.goto('/');
  await page.fill('input[type="text"]', 'admin');
  await page.fill('input[type="password"]', 'admin');
  
  const select = page.getByRole('combobox');
  await select.waitFor({ state: 'attached' });
  await select.locator('option').nth(1).waitFor({ state: 'attached' });
  await select.selectOption({ index: 1 });
  await page.click('button:has-text("Se connecter")');

  // Dashboard - Create Declaration for 2026-06
  await page.click('button:has-text("Créer une déclaration")');
  await page.fill('input[type="number"]', '2026');
  
  // Select type Mensuel (default)
  await page.selectOption('select', { label: 'Mensuel' });
  
  // Select month Juin (06)
  const selects = await page.locator('select').all();
  if (selects.length > 1) {
      await selects[1].selectOption({ label: 'Juin (06)' });
  }
  await page.locator('button:text-is("Créer")').click();

  // Wait for transition to Stepper page
  await page.waitForTimeout(2000);
  
  // Step 1: Règlements
  // Wait for loading spinner to disappear (data loaded)
  await page.waitForSelector('.animate-spin', { state: 'detached' });
  
  // Check the select-all checkbox
  await page.locator('input[type="checkbox"]').first().click();
  await page.waitForTimeout(500);
  
  // Take screenshot overview
  await page.screenshot({ path: '../VERIFY/05-workstation-overview.png' });
  
  // Click CTA to step 2
  await page.click('button:has-text("Analyser TVA")');
  await page.waitForTimeout(1000);

  // Step 2: Affectations
  await page.screenshot({ path: '../VERIFY/06-factures-face-ne-declare-pas.png' });
  await page.click('button:has-text("Passer au calcul")');
  await page.waitForTimeout(1000);

  // Step 3: Calcul
  // Wait for loading spinner to disappear in Calcul step
  await page.waitForSelector('.animate-spin', { state: 'detached' });
  await page.screenshot({ path: '../VERIFY/09-actions-masse.png' });
  await page.click('button:has-text("Passer à l\'intégration")');
  await page.waitForTimeout(1000);

  // Step 4: Intégration
  // Wait for checkup to load (loading spinner disappears)
  await page.waitForSelector('.animate-spin', { state: 'detached' });
  await page.waitForSelector('#btn-confirmer-integration');
  await page.screenshot({ path: '../VERIFY/07-cloture-verrouillee.png' });
  
  // Confirm integration
  await page.click('#btn-confirmer-integration');
  await page.waitForTimeout(2000);
  
  // Continuer vers le contrôle
  await page.click('button:has-text("Continuer vers le contrôle")');
  await page.waitForTimeout(1000);

  // Step 5: Contrôle
  await page.screenshot({ path: '../VERIFY/08-drill-preuves-modal.png' });
  await page.click('button:has-text("Passer à la synthèse")');
  await page.waitForTimeout(1000);

  // Step 6: Synthèse
  await expect(page.locator('button:has-text("Générer les fichiers")')).toBeVisible();
  await page.screenshot({ path: '../VERIFY/10-synthese-overview.png' });
});
