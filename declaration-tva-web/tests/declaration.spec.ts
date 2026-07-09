import { test, expect } from '@playwright/test';
import * as fs from 'fs';

test('Parcours complet: Création, poste de travail, factures 2 faces, drill preuves, clôture', async ({ page }) => {
  // Test uses real backend on localhost:5005 proxied via Vite (/api)

  // Login
  await page.goto('/');
  await page.fill('input[type="text"]', 'admin');
  await page.fill('input[type="password"]', 'admin');
  await page.click('button:has-text("Se connecter")');

  // If there's an existing declaration for 2028, open it instead
  const existingDeclaration = page.locator('h3:has-text("2028")');
  await page.waitForTimeout(1000); // Wait for list to load
  if (await existingDeclaration.count() > 0) {
      // Dashboard - Open existing declaration
      await page.locator('button[title="Ouvrir"]').first().click();
  } else {
      // Dashboard - Create Declaration
      await page.click('button:has-text("Créer une déclaration")');
      await page.fill('input[type="number"]', '2028');
      await page.selectOption('select', { label: 'Mensuel' });
      const selects = await page.locator('select').all();
      if (selects.length > 1) {
          await selects[1].selectOption({ label: 'Janvier (01)' });
      }
      await page.locator('button:text-is("Créer")').click();
  }
  
  await page.waitForTimeout(2000);
  // We should be on the Workstation Panel
  await expect(page.locator('button:has-text("Factures à déclarer")')).toBeVisible();
  
  // Wait for the creation toast to disappear
  await page.waitForTimeout(3500);

  // Take screenshot of the new Workstation
  await page.screenshot({ path: '../VERIFY/05-workstation-overview.png' });

  // In "Factures à déclarer" by default, we have "Je déclare" and "Je ne déclare pas"
  await expect(page.locator('button:has-text("Je déclare")')).toBeVisible();
  await expect(page.locator('button:has-text("Je ne déclare pas")')).toBeVisible();

  // We start on "Je déclare".
  // Switch to "Je ne déclare pas" to ensure we have rows to reset

  // Switch to "Je ne déclare pas"
  await page.click('button:has-text("Je ne déclare pas")');
  // It should now show rows with Exclue, Reportée, Écartée
  await expect(page.locator('tbody tr').first()).toBeVisible();
  await page.screenshot({ path: '../VERIFY/06-factures-face-ne-declare-pas.png' });

  // Select a row and reset it to 'Proposée' to test the lock
  await page.locator('tbody tr').first().locator('input[type="checkbox"]').click();
  await page.screenshot({ path: '../VERIFY/09-actions-masse.png' });
  await page.click('button:has-text("Réinitialiser")');
  await expect(page.locator('text=Sélectionnées : 0')).toBeVisible();

  // Switch back to "Je déclare" to see our 'Proposée' line
  await page.click('button:has-text("Je déclare")');
  await expect(page.locator('tbody tr').first()).toBeVisible();
  await page.waitForTimeout(1500); // Attente refresh checkup

  const btnCloture = page.locator('button', { hasText: 'Clôturer' }).first();
  await expect(btnCloture).toBeDisabled();
  await expect(btnCloture).toHaveText(/à faire/i);
  await btnCloture.screenshot({ path: '../VERIFY/07-cloture-verrouillee.png' });

  // Test Drill "Remonter aux preuves"
  // Switch back to "Je déclare"
  await page.click('button:has-text("Je déclare")');
  await expect(page.locator('tbody tr').first()).toBeVisible();
  // Click on a row (not the checkbox)
  await page.locator('tbody tr').first().locator('td').nth(2).click(); // Click on "Tiers" column for example

  // The ProofModal should appear
  await expect(page.locator('h3:has-text("Preuves du règlement")')).toBeVisible();
  await page.screenshot({ path: '../VERIFY/08-drill-preuves-modal.png' });

  // Verify the 3 proofs are present as tabs
  await expect(page.locator('button:has-text("Rapprochement")').first()).toBeVisible();
  await expect(page.locator('button:has-text("Affectation")').first()).toBeVisible();
  await expect(page.locator('button:has-text("Conformité IF/ICE")').first()).toBeVisible();

  // Close modal
  await page.click('h3:has-text("Preuves du règlement") >> xpath=../../button'); // The X button
});
