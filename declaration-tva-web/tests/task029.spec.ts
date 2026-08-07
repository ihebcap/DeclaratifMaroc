import { test, expect } from '@playwright/test';

// TASK-029 : preuve e2e que depuis le dashboard connecté, cliquer « Guide fonctionnel » ouvre un
// nouvel onglet sur le guide (URL + titre). Nécessite un backend + DB réels pour le login (liste
// des sociétés). Non exécutable dans un environnement sans accès SQL Server réel (cf. limitation
// documentée dans VERIFY/TASK-029_verify.md, identique à TASK-060/200/202) — structure alignée sur
// tests/task183.spec.ts pour exécution ultérieure sur un poste avec accès DB.

test('TASK-029 : ouverture du guide fonctionnel dans un nouvel onglet depuis le dashboard', async ({ page, context }) => {
  await page.goto('/');
  await page.fill('input[type="text"]', 'Admin');
  await page.fill('input[type="password"]', 'Admin');
  const select = page.getByRole('combobox');
  await select.waitFor({ state: 'attached' });
  await select.locator('option').nth(1).waitFor({ state: 'attached' });
  await select.selectOption({ index: 1 });
  await page.click('button:has-text("Se connecter")');
  await page.waitForSelector('.animate-spin', { state: 'detached' });

  const [guidePage] = await Promise.all([
    context.waitForEvent('page'),
    page.click('button[title="Guide fonctionnel TVA"]'),
  ]);
  await guidePage.waitForLoadState();

  expect(guidePage.url()).toContain('guide-fonctionnel-tva.html');
  await expect(guidePage.locator('text=Déclaration de TVA déductible').first()).toBeVisible();

  await page.screenshot({ path: '../VERIFY/task029-sidebar-guide.png' });
  await guidePage.screenshot({ path: '../VERIFY/task029-guide-ouvert.png' });
});
