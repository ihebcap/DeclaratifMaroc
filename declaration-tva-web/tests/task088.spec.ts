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

test('Test TASK-088: Detail des avertissements Ligne exclue with refLigne and drill-down', async ({ page }) => {
  // Listen for console logs
  page.on('console', msg => console.log('BROWSER:', msg.text()));

  // Intercept /checkup API call to inject a warning with refLigne and filtre
  await page.route('**/declarations/*/checkup', async (route) => {
    const response = await route.fetch();
    const json = await response.json();
    console.log("ORIGINAL CHECKUP JSON:", JSON.stringify(json));
    
    // Inject warnings
    json.alertes = [
      {
        type: "avertissement",
        message: "Ligne exclue : Échéance hors périmètre (ni facture ni solde)",
        code: "LIGNE_EXCLUE",
        refLigne: "FC2501717",
        domaine: "Décaissement",
        filtre: { numeroRapprochement: "REG-MOCK-088" }
      },
      {
        type: "avertissement",
        message: "Autre avertissement sans drill",
        code: "AUTRE",
        refLigne: null,
        domaine: null,
        filtre: null
      }
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
  
  // Check that the formatted warning is visible
  const warningText = page.getByText('Ligne exclue (FC2501717) : Échéance hors périmètre (ni facture ni solde)');
  await expect(warningText).toBeVisible();

  // Scroll to warnings
  await warningText.scrollIntoViewIfNeeded();
  await page.waitForTimeout(1000);

  // Take screenshot showing the formatted warning with refLigne and drill button
  await page.screenshot({ path: '../VERIFY/task088-step3-warning.png' });

  // Click on "Voir lignes" drill button next to the warning
  const drillBtn = page.locator('button:has-text("Voir lignes")').first();
  await expect(drillBtn).toBeVisible();
  await drillBtn.click();
  await page.waitForTimeout(1000);

  // Take screenshot of the drill-down view
  await page.screenshot({ path: '../VERIFY/task088-step3-drill.png' });

  // Go back
  const backBtn = page.locator('button:has-text("Retour au contrôle")');
  await expect(backBtn).toBeVisible();
  await backBtn.click();
  await page.waitForTimeout(500);

  // Click Confirmer intégration
  console.log('Clicking Confirm Integration...');
  await page.click('#btn-confirmer-integration');
  await page.waitForTimeout(3000);
  console.log('Finished!');
});
