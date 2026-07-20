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

test('Test TASK-096: Total sélectionné persiste après retour de Détail des lignes', async ({ page }) => {
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

  const totalBefore = await totalSelector.innerText();
  console.log('Total sélectionné avant drill:', totalBefore);
  
  // Make sure it is not 0,00 MAD
  expect(totalBefore).not.toContain('0,00 MAD');

  // Take screenshot of step 1 selection showing the non-zero total
  await page.screenshot({ path: '../VERIFY/task096-01-avant.png' });

  // Open the drill (Détail des lignes)
  await page.click('button:has-text("Détail des lignes")');
  await page.waitForTimeout(1000);

  // Return to selection
  await page.click('button:has-text("Retour à la sélection")');
  await page.waitForTimeout(1000);

  // Read selected total in the grid footer after returning
  await expect(totalSelector).toBeVisible();
  const totalAfter = await totalSelector.innerText();
  console.log('Total sélectionné après retour du drill:', totalAfter);

  // Verify that the total after returning matches the total before and is not 0,00 MAD
  expect(totalAfter).toBe(totalBefore);
  expect(totalAfter).not.toContain('0,00 MAD');

  // Take screenshot of step 1 selection after returning showing the exact same non-zero total
  await page.screenshot({ path: '../VERIFY/task096-02-apres-retour.png' });

  console.log('Finished TASK-096 test!');
});
