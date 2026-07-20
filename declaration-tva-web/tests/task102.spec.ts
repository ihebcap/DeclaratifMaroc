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

test('Test TASK-102: Anomalies de facture avec numero reglement', async ({ page }) => {
  // Listen for console logs
  page.on('console', msg => console.log('BROWSER:', msg.text()));

  // Intercept /checkup API call to inject warnings/errors containing the règlement reference
  await page.route('**/declarations/*/checkup', async (route) => {
    const response = await route.fetch();
    const json = await response.json();
    
    // Inject the simulated alerts for TASK-102
    json.alertes = [
      {
        type: "bloquant",
        message: "Ligne en anomalie de recalcul (facture FC2501667, règlement RC25040088) : Sage...",
        code: "FACTURE_NON_VENTILEE",
        refLigne: "FC2501667",
        domaine: "Décaissement",
        filtre: { numeroRapprochement: "RC25040088" }
      },
      {
        type: "avertissement",
        message: "Facture FC2501717, règlement RF26040040 exclue de la valorisation (EC_Id=21473) : incohérence Sage HT/TVA/TTC détectée — vérification manuelle requise avant clôture. Ligne non valorisée, aucune valeur déclarée pour cette pièce.",
        code: "LIGNE_FIGEE_A_REVERIFIER",
        refLigne: "FC2501717",
        domaine: "Décaissement",
        filtre: { numeroRapprochement: "RF26040040" }
      }
    ];
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
  
  // Wait for selected total to stabilize
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
  
  // Verify that our mocked alerts are displayed on the UI
  const errorText = page.getByText('Ligne en anomalie de recalcul (facture FC2501667, règlement RC25040088) : Sage...');
  const warningText = page.getByText('Facture FC2501717, règlement RF26040040 exclue de la valorisation');
  
  await expect(errorText).toBeVisible();
  await expect(warningText).toBeVisible();

  // Scroll to make sure they are visible on screen
  await errorText.scrollIntoViewIfNeeded();
  await page.waitForTimeout(1000);

  // Take screenshot showing the anomalies with the règlement numbers visible
  await page.screenshot({ path: '../VERIFY/task102-step3-anomalies-reglement.png' });

  console.log('Finished TASK-102 test!');
});
