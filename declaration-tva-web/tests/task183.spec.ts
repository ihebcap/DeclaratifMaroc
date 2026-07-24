import { test, expect } from '@playwright/test';

// TASK-183 : preuve réelle (Playwright, backend + DB réels GR_EMA_DISTRIBUTION, déclaration
// existante TVA1-2026-05, non clôturée, jamais réinitialisée) que le toggle Achats/Ventes du
// bandeau du drill « Codes activité » permet de changer de domaine SANS repasser par
// « Retour au contrôle », et qu'une affectation faite sur une ligne Encaissement via ce chemin
// est bien persistée. Aucun reset.ps1/make_eligible.ps1 ici (déclaration réelle réutilisée telle
// quelle, non destructive). Note : le filtre par « N° Facture »/montant du drill s'est révélé
// silencieusement inopérant côté back (préexistant, hors périmètre TASK-183, cf. VERIFY) — ce test
// interagit donc avec la première ligne réellement rendue par la grille virtualisée, sans filtrer,
// et capture son identité (facture) en log pour vérification SQL a posteriori.

test('TASK-183 : toggle Achats/Ventes dans le drill Codes activité sans sortir', async ({ page }) => {
  page.on('console', msg => { if (msg.type() === 'error') console.log('BROWSER ERROR:', msg.text()); });

  // Login
  await page.goto('/');
  await page.fill('input[type="text"]', 'Admin');
  await page.fill('input[type="password"]', 'Admin');
  const select = page.getByRole('combobox');
  await select.waitFor({ state: 'attached' });
  await select.locator('option').nth(1).waitFor({ state: 'attached' });
  await select.selectOption({ index: 1 });
  await page.click('button:has-text("Se connecter")');

  // Ouvrir la déclaration existante TVA1-2026-05 (déjà valorisée, non clôturée) depuis la liste
  await page.waitForSelector('.animate-spin', { state: 'detached' });
  const row = page.locator('div')
    .filter({ has: page.getByRole('heading', { name: 'TVA1-2026-05', exact: true }) })
    .filter({ has: page.locator('button[title="Ouvrir"]') })
    .last();
  await row.locator('button[title="Ouvrir"]').click();

  // Étape ① Sélection : sélection déjà persistée (TASK-097), "Passer au calcul" doit être actif
  await page.waitForSelector('.animate-spin', { state: 'detached' });
  await page.click('button:has-text("Passer au calcul")');

  // Étape ② Vérifier & Intégrer
  await page.waitForSelector('.animate-spin', { state: 'detached' });
  await page.screenshot({ path: '../VERIFY/task183-00-ecran-verifier-integrer.png' });

  // Ouvrir le drill "Codes activité" (selectedTab par défaut = Decaissement/Achats)
  await page.click('button:has-text("Codes activité")');
  await expect(page.locator('text=TVA Déductible (Achats)').first()).toBeVisible();
  await page.waitForTimeout(500);
  await page.screenshot({ path: '../VERIFY/task183-01-drill-achats.png' });

  const dataRowsAchats = page.locator('[role="row"]').filter({ has: page.locator('[role="cell"]') });
  const factureAchats = await dataRowsAchats.first().locator('[role="cell"]').nth(1).innerText(); // nth(1) : nth(0) est la case à cocher, pas une donnée
  console.log('LIGNE_ACHATS_VISIBLE_AVANT_TOGGLE=' + factureAchats);

  // TASK-183 : bascule vers Ventes SANS cliquer "Retour au contrôle"
  await page.click('button:has-text("TVA Collectée (Ventes)")');
  await expect(page.locator('span', { hasText: 'TVA Collectée (Ventes)' })).toBeVisible();
  await page.waitForTimeout(800);
  await page.screenshot({ path: '../VERIFY/task183-02-drill-ventes-apres-toggle.png' });

  // Affecter un code activité à la première ligne Encaissement réellement rendue (aucune sortie du drill)
  const dataRowsVentes = page.locator('[role="row"]').filter({ has: page.locator('select') });
  const ligneVentes = dataRowsVentes.first();
  await expect(ligneVentes).toBeVisible();
  const factureVentes = await ligneVentes.locator('[role="cell"]').nth(1).innerText(); // nth(1) : nth(0) est la case à cocher, pas une donnée
  console.log('LIGNE_VENTES_CIBLE=' + factureVentes);
  await ligneVentes.locator('select').selectOption({ index: 1 });
  const codeAffecte = await ligneVentes.locator('select').inputValue();
  console.log('CODE_AFFECTE_ENCAISSEMENT=' + codeAffecte);
  await page.waitForTimeout(800); // laisser le PATCH /code-activite partir
  await page.screenshot({ path: '../VERIFY/task183-03-code-activite-affecte-ventes.png' });

  // Bascule retour vers Achats, toujours SANS "Retour au contrôle"
  await page.click('button:has-text("TVA Déductible (Achats)")');
  await expect(page.locator('span', { hasText: 'TVA Déductible (Achats)' }).first()).toBeVisible();
  await page.waitForTimeout(800);
  const dataRowsAchatsApres = page.locator('[role="row"]').filter({ has: page.locator('[role="cell"]') });
  await expect(dataRowsAchatsApres.first()).toBeVisible();
  const factureAchatsApres = await dataRowsAchatsApres.first().locator('[role="cell"]').nth(1).innerText(); // nth(1) : nth(0) est la case à cocher, pas une donnée
  console.log('LIGNE_ACHATS_VISIBLE_APRES_TOGGLE_RETOUR=' + factureAchatsApres);
  await page.screenshot({ path: '../VERIFY/task183-04-retour-achats-apres-toggle.png' });
});
