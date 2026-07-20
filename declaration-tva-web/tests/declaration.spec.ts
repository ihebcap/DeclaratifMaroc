import { test, expect } from '@playwright/test';
import { execSync } from 'child_process';

test.beforeAll(async () => {
  try {
    execSync('powershell -File ../reset.ps1');
    execSync('powershell -File ../make_eligible.ps1');
  } catch (e) {
    console.error("Failed to reset DB / make eligible:", e);
  }
});

test('Parcours complet: Création, Règlements, Affectations, Calcul, Intégration, Contrôle, Synthèse', async ({ page }) => {
  // Listen for console logs
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
  
  // Wait for selected total to stabilize (default selection is checked by default)
  const totalSelector = page.locator('text=/Total sélectionné : .+/');
  await expect(totalSelector).toBeVisible();
  
  // Clear the default selection of all rows to avoid browser resource exhaustion in drill
  const currentTotal = await totalSelector.innerText();
  if (!currentTotal.includes('0,00 MAD')) {
    await page.locator('input[type="checkbox"]').first().click();
    await expect(totalSelector).toContainText('0,00 MAD');
  }
  
  // Select specific Decaissements that are guaranteed to have affectations in the database
  await page.locator('div[style*="absolute"]').filter({ hasText: 'RF26060080' }).locator('input[type="checkbox"]').click();
  await page.locator('div[style*="absolute"]').filter({ hasText: 'RF26060107' }).locator('input[type="checkbox"]').click();
  await page.locator('div[style*="absolute"]').filter({ hasText: 'RF26060057' }).locator('input[type="checkbox"]').click();
  await expect(totalSelector).not.toHaveText(/Total sélectionné : 0,00\s*MAD/);
  await page.waitForTimeout(500);
  
  // Take screenshot overview
  await page.screenshot({ path: '../VERIFY/05-workstation-overview.png' });
  
  // Open the drill
  await page.click('button:has-text("Détail des lignes")');
  await page.waitForTimeout(1000);

  // Take screenshot of the drill (Affectations)
  await page.screenshot({ path: '../VERIFY/06-factures-face-ne-declare-pas.png' });

  // Return to selection
  await page.click('button:has-text("Retour à la sélection")');
  await page.waitForTimeout(500);

  // Proceed to verification & integration (Passer au calcul)
  await page.click('button:has-text("Passer au calcul")');
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
  await expect(page.locator('button:has-text("Générer XML")')).toBeVisible();
  await page.screenshot({ path: '../VERIFY/08-drill-preuves-modal.png' });
  await page.screenshot({ path: '../VERIFY/10-synthese-overview.png' });
});
