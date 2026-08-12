import { test, expect } from '@playwright/test';
import { execSync } from 'child_process';

test.beforeAll(async () => {
  execSync('powershell -File ../reset.ps1', { stdio: 'inherit' });
  execSync('powershell -File ../make_eligible.ps1', { stdio: 'inherit' });
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
  if (await existingCard.isVisible().catch(() => false)) {
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
  await page.waitForTimeout(2000);
  
  // Step 1: Règlements
  const integrerInviteBtn = page.getByRole('button', { name: /Intégrer/i }).first();
  if (await integrerInviteBtn.isVisible().catch(() => false)) {
    await integrerInviteBtn.click();
  }

  // Wait for loading spinner to disappear and AG Grid rows to be attached
  await page.waitForSelector('.ag-row', { state: 'attached', timeout: 15000 });
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
  
  const totalSelector = page.locator('text=/Total sélectionné : .+/');
  await expect(totalSelector).toBeVisible();
  await expect(totalSelector).not.toHaveText(/Total sélectionné : 0,00\s*MAD/);
  await page.waitForTimeout(500);

  // Navigate to step 3 (Vérifier): reglements → factures → verifier
  // NOTE: assertions happen in 'verifier' step (mode="verifier"), NOT in 'confirmer'
  await page.click('button:has-text("Passer aux factures")');
  await page.waitForTimeout(500);
  await page.click('button:has-text("Continuer vers la vérification")');
  await page.waitForTimeout(1000);
  
  // Wait for controls to load (spinner disappears)
  await page.waitForSelector('.animate-spin', { state: 'detached', timeout: 20000 });
  
  // Assert the 'Cohérence des totaux déclarés' control is visible in verifier step
  const equilibreRow = page.getByText('Cohérence des totaux déclarés').first();
  await equilibreRow.waitFor({ state: 'attached', timeout: 15000 });
  await equilibreRow.scrollIntoViewIfNeeded();
  await page.waitForTimeout(1000);
  
  // Take screenshot showing the equilibrium control in error state and its detailed table
  await page.screenshot({ path: '../VERIFY/task087-step3-ecart.png' });

  // 2. Clear route interception so we can proceed to integration
  await page.unroute('**/declarations/*/checkup');

  // Now navigate to confirmer step to do the actual integration
  await page.click('button:has-text("Continuer vers la confirmation")');
  await page.waitForTimeout(1000);
  await page.waitForSelector('.animate-spin', { state: 'detached', timeout: 20000 });

  // Click Confirmer intégration
  console.log('Clicking Confirm Integration...');
  await page.click('#btn-confirmer-integration');
  console.log('Waiting for integration to complete...');
  await page.waitForTimeout(3000);

  // After integration (step 5 / declaration screen or confirmed state)
  // Toggle the Avertissements section if present
  const avertBtn = page.locator('button:has-text("Avertissements")').first();
  if (await avertBtn.isVisible().catch(() => false)) {
    await avertBtn.click();
    await page.waitForTimeout(500);
  }

  // Scroll to Répartition par source
  console.log('Locating Répartition par source...');
  const sectionSource = page.locator('button:has-text("Répartition par source")').first();
  if (await sectionSource.isVisible().catch(() => false)) {
    await sectionSource.scrollIntoViewIfNeeded();
    await page.waitForTimeout(500);
  }
  
  // Take screenshot of post-integration screen showing RecapSourceTable
  console.log('Taking second screenshot...');
  await page.screenshot({ path: '../VERIFY/task087-step5-no-regression.png' });
  console.log('Finished!');
});
