import { test, expect } from '@playwright/test';
import { execSync } from 'child_process';

test.beforeAll(async () => {
  execSync('powershell -File ../reset.ps1', { stdio: 'inherit' });
  execSync('powershell -File ../make_eligible.ps1', { stdio: 'inherit' });
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

  // Navigate to verifier step: reglements → factures → verifier
  // IMPORTANT: assertions happen in 'verifier' step (mode="verifier"), NOT in 'confirmer'
  await page.click('button:has-text("Passer aux factures")');
  await page.waitForTimeout(500);
  await page.click('button:has-text("Continuer vers la vérification")');
  await page.waitForTimeout(1000);

  // Wait for checkup to load (spinner disappears)
  await page.waitForSelector('.animate-spin', { state: 'detached', timeout: 20000 });
  
  // Expand warnings accordion - button text: "N avertissement(s) (non bloquant(s))"
  // Scroll first to make sure button is visible, then click
  const expandWarningsBtn = page.locator('button').filter({ hasText: /avertissement/ }).first();
  await expandWarningsBtn.waitFor({ state: 'attached', timeout: 10000 });
  await expandWarningsBtn.scrollIntoViewIfNeeded();
  await expandWarningsBtn.click();
  await page.waitForTimeout(500);
  
  // Check that the formatted warning is visible (UI renders "Ligne exclue (FC2501717) : ...")
  const warningText = page.locator('text=Ligne exclue (FC2501717)').first();
  await expect(warningText).toBeVisible({ timeout: 10000 });

  // Scroll to warnings
  await warningText.scrollIntoViewIfNeeded();
  await page.waitForTimeout(1000);

  // Take screenshot showing the formatted warning with refLigne and drill button
  await page.screenshot({ path: '../VERIFY/task088-step3-warning.png' });

  // Click on "Voir lignes" drill button next to the warning.
  // NOTE: in verifier mode, onVoirLignes is set → clicking "Voir lignes" navigates to
  // the factures step (not inline drill). We verify the button exists and is clickable.
  const drillBtn = page.locator('button:has-text("Voir lignes")').first();
  await expect(drillBtn).toBeVisible();
  await drillBtn.click();
  await page.waitForTimeout(1000);

  // Take screenshot of the factures drill view
  await page.screenshot({ path: '../VERIFY/task088-step3-drill.png' });

  // Navigate back to verifier step via stepper
  await page.click('button:has-text("Continuer vers la vérification")');
  await page.waitForTimeout(1000);
  await page.waitForSelector('.animate-spin', { state: 'detached', timeout: 20000 });

  // Now navigate to confirmer step for integration
  await page.click('button:has-text("Continuer vers la confirmation")');
  await page.waitForTimeout(1000);
  await page.waitForSelector('.animate-spin', { state: 'detached', timeout: 20000 });

  // Click Confirmer intégration
  console.log('Clicking Confirm Integration...');
  await page.click('#btn-confirmer-integration');
  await page.waitForTimeout(3000);
  console.log('Finished!');
});
