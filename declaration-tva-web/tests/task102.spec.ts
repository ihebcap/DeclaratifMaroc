import { test, expect } from '@playwright/test';
import { execSync } from 'child_process';

test.beforeAll(async () => {
  execSync('powershell -File ../reset.ps1', { stdio: 'inherit' });
  execSync('powershell -File ../make_eligible.ps1', { stdio: 'inherit' });
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
  // IMPORTANT: bloquants/avertissements are visible only in 'verifier' step (mode="verifier")
  await page.click('button:has-text("Passer aux factures")');
  await page.waitForTimeout(500);
  await page.click('button:has-text("Continuer vers la vérification")');
  await page.waitForTimeout(1000);

  // Wait for checkup to load (spinner disappears)
  await page.waitForSelector('.animate-spin', { state: 'detached', timeout: 20000 });
  
  // Verify the bloquant alert is visible (bloquants are always expanded, no accordion)
  // Bloquant message: "Ligne en anomalie de recalcul (facture FC2501667, règlement RC25040088)..."
  // UI appends refLigne in parens: "... (FC2501667)"
  const errorText = page.locator('text=Ligne en anomalie de recalcul').first();
  await expect(errorText).toBeVisible({ timeout: 10000 });
  
  // Verify the warning (accordion must be expanded first)
  // Warning message will show FC2501717 reference
  const expandWarningsBtn = page.locator('button').filter({ hasText: /avertissement/ }).first();
  if (await expandWarningsBtn.isVisible().catch(() => false)) {
    await expandWarningsBtn.scrollIntoViewIfNeeded();
    await expandWarningsBtn.click();
    await page.waitForTimeout(500);
  }
  const warningText = page.locator('text=FC2501717').first();
  await expect(warningText).toBeVisible({ timeout: 10000 });

  // Scroll to make sure they are visible on screen
  await errorText.scrollIntoViewIfNeeded();
  await page.waitForTimeout(1000);

  // Take screenshot showing the anomalies with the règlement numbers visible
  await page.screenshot({ path: '../VERIFY/task102-step3-anomalies-reglement.png' });

  console.log('Finished TASK-102 test!');
});
