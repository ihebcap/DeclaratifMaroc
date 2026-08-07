import { test, expect } from '@playwright/test';

// TASK-060 : preuve e2e que la répartition des erreurs de valorisation par code est visible à
// l'écran (modale RapportValorisationModal) sans ouvrir les DevTools ni consulter les logs
// serveur. Nécessite un backend + DB réels avec des règlements en anomalie sur la période testée
// (juin 2026, soId=1, cf. TASK-060). Non exécutable dans un environnement sans accès SQL Server
// réel (cf. limitation documentée dans VERIFY/TASK-060_verify.md) — structure alignée sur
// tests/task183.spec.ts pour exécution ultérieure sur un poste avec accès DB.

test('TASK-060 : répartition des erreurs de valorisation visible à l\'écran, sans DevTools', async ({ page }) => {
  await page.goto('/');
  await page.fill('input[type="text"]', 'Admin');
  await page.fill('input[type="password"]', 'Admin');
  const select = page.getByRole('combobox');
  await select.waitFor({ state: 'attached' });
  await select.locator('option').nth(1).waitFor({ state: 'attached' });
  await select.selectOption({ index: 1 });
  await page.click('button:has-text("Se connecter")');

  // Écran Factures — période de référence juin 2026
  await page.click('text=Factures');
  await page.fill('input[type="date"] >> nth=0', '2026-06-01');
  await page.fill('input[type="date"] >> nth=1', '2026-06-30');
  await page.waitForSelector('.animate-spin', { state: 'detached' });

  // Déclenche le rafraîchissement de valorisation
  await page.click('button:has-text("Rafraîchir valorisation")');
  await page.waitForSelector('.animate-spin', { state: 'detached', timeout: 60000 });

  // La modale de détail doit s'ouvrir automatiquement si nbErreurs > 0, sans action DevTools
  const modal = page.locator('text=Détail des erreurs de valorisation');
  await expect(modal).toBeVisible();

  // Répartition par code visible dans le tableau
  const rows = page.locator('table tr').filter({ has: page.locator('td') });
  const rowCount = await rows.count();
  expect(rowCount).toBeGreaterThan(0);

  // Somme des "Nombre" par ligne == nbErreurs affiché dans l'en-tête (livrable de preuve #2)
  const header = await page.locator('text=/\\d+ anomalie\\(s\\) détectée\\(s\\)/').innerText();
  const nbErreursAffiche = Number(header.match(/(\d+) anomalie/)?.[1] ?? -1);
  let somme = 0;
  for (let i = 0; i < rowCount; i++) {
    const nombreCell = await rows.nth(i).locator('td').nth(2).innerText();
    somme += Number(nombreCell.trim());
  }
  expect(somme).toBe(nbErreursAffiche);

  await page.screenshot({ path: '../VERIFY/task060-repartition-erreurs.png' });
});
