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

test('Test TASK-087: Cohérence des totaux déclarés control and RecapSourceTable sharing', async ({ page }) => {
  // Listen for console logs
  page.on('console', msg => console.log('BROWSER:', msg.text()));

  // 1. Intercept /checkup API call to inject an equilibrium discrepancy
  await page.route('**/declarations/*/checkup', async (route) => {
    const response = await route.fetch();
    const json = await response.json();
    console.log("ORIGINAL CHECKUP JSON:", JSON.stringify(json));
    // Inject equilibrium error and mock recapSource for testing
    json.equilibre = {
      isValid: false,
      ecart: 368517.56
    };
    json.recapSource = [
      { source: "Règlements sélectionnés", ht: 1842587.80, tva: 368517.56, ttc: 2211105.36, nbLignes: 16 },
      { source: "Déclaration TVA (Générateur)", ht: 0.00, tva: 0.00, ttc: 0.00, nbLignes: 0 },
      { source: "Écart", ht: 1842587.80, tva: 368517.56, ttc: 2211105.36, nbLignes: 16 }
    ];
    console.log("MOCKED CHECKUP JSON:", JSON.stringify(json));
    await route.fulfill({ json });
  });

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
  
  // Wait for selected total to stabilize (default selection is checked by default)
  const totalSelector = page.locator('text=/Total sélectionné : .+/');
  await expect(totalSelector).toBeVisible();
  
  // Clear the default selection of all rows to avoid browser resource exhaustion in drill
  const currentTotal = await totalSelector.innerText();
  if (!currentTotal.includes('0,00 MAD')) {
    await page.locator('input[type="checkbox"]').first().click();
    await expect(totalSelector).toContainText('0,00 MAD');
  }
  
  // Select only the first 2 enabled rows
  const enabledCheckboxes = page.locator('div[style*="absolute"]').filter({ hasText: 'Éligible' }).locator('input[type="checkbox"]');
  await enabledCheckboxes.nth(0).click();
  await enabledCheckboxes.nth(1).click();
  await expect(totalSelector).not.toHaveText(/Total sélectionné : 0,00\s*MAD/);
  await page.waitForTimeout(500);

  // Go to step 3 (Vérifier & Intégrer)
  await page.click('button:has-text("Passer au calcul")');
  await page.waitForTimeout(2000);
  
  // Step 3
  await page.waitForSelector('.animate-spin', { state: 'detached' });
  
  // Scroll the Cohérence des totaux déclarés control row into view
  const equilibreRow = page.locator('text="Cohérence des totaux déclarés"').first();
  await equilibreRow.scrollIntoViewIfNeeded();
  await page.waitForTimeout(1000);
  
  // Take screenshot showing the equilibrium control in error state and its detailed table
  await page.screenshot({ path: '../VERIFY/task087-step3-ecart.png' });

  // 2. Clear route interception so we can proceed to integration
  await page.unroute('**/declarations/*/checkup');

  // Reload the checkup by going back to Selection, and clicking Passer au calcul again
  await page.click('text=1. Sélection');
  await page.waitForTimeout(1000);
  await page.click('button:has-text("Passer au calcul")');
  await page.waitForTimeout(2000);
  await page.waitForSelector('.animate-spin', { state: 'detached' });

  // Now click Confirmer intégration
  console.log('Clicking Confirm Integration...');
  await page.click('#btn-confirmer-integration');
  console.log('Waiting for integration to complete...');
  await page.waitForTimeout(3000);

  // Step 5 (unified post-integration screen)
  // Toggle the Avertissements section closed to make RecapSourceTable visible
  console.log('Closing Avertissements section...');
  await page.locator('button:has-text("Avertissements")').first().click();
  await page.waitForTimeout(500);

  // Scroll to Répartition par source
  console.log('Locating Répartition par source...');
  const sectionSource = page.locator('button:has-text("Répartition par source")').first();
  await sectionSource.scrollIntoViewIfNeeded();
  await page.waitForTimeout(500);
  
  // Take screenshot of step 5 showing RecapSourceTable
  console.log('Taking second screenshot...');
  await page.screenshot({ path: '../VERIFY/task087-step5-no-regression.png' });
  console.log('Finished!');
});
