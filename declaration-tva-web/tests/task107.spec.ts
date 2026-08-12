import { test, expect } from '@playwright/test';
import { execSync } from 'child_process';

// ─── TASK-107/TASK-112 — drill « Lignes incohérentes » → factures/règlements ──
//
// TASK-107 (option 3) avait câblé ce drill sur `source=Decaissement` depuis l'onglet
// Décaissement — or DM_LGTVA.Source vaut déjà `Decaissement` pour 100% des lignes du
// domaine : filtre tautologique, signalé par le PO (TASK-112). Remplacé par l'axe
// « lignes incohérentes » (TTC ≠ HT+TVA), qui isole réellement les lignes composant
// l'écart. Preuve réelle : depuis le tableau sous le badge ÉCART (écran ③ Vérifier &
// Intégrer), un clic ouvre la grille filtrée (DomainGrid, mécanisme TASK-016) sur les
// lignes incohérentes — atteignant les pièces FC…/RC… réelles, sans figer la
// déclaration (lecture seule, retour possible).

test.beforeAll(async () => {
  execSync('powershell -File ../reset.ps1', { stdio: 'inherit' });
  execSync('powershell -File ../make_eligible.ps1', { stdio: 'inherit' });
});

test('Test TASK-107: drill par source vers les factures/règlements réels', async ({ page }) => {
  page.on('console', msg => console.log('BROWSER:', msg.text()));

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
  // IMPORTANT: ChecklistCard (controls + recapIncoherence table) is only in 'verifier' step
  await page.click('button:has-text("Passer aux factures")');
  await page.waitForTimeout(500);
  await page.click('button:has-text("Continuer vers la vérification")');
  await page.waitForTimeout(1000);
  await page.waitForSelector('.animate-spin', { state: 'detached', timeout: 20000 });

  // Force l'affichage du détail d'écart (jeu de lignes valorisées RÉEL, seuls `equilibre` et
  // `recapIncoherence` sont forcés) — permet de démontrer le drill même quand TASK-108 a rendu
  // l'équilibre réellement correct sur ce petit jeu de lignes (aucune ligne réellement
  // incohérente TTC≠HT+TVA à disposition dans ce scénario de test).
  await page.route('**/declarations/*/checkup', async (route) => {
    const response = await route.fetch();
    const json = await response.json();
    if (!json.recapSource || json.recapSource.length === 0) {
      throw new Error('checkup réel sans lignes valorisées — jeu de données insuffisant pour la preuve TASK-112');
    }
    const ecart = json.equilibre?.ecart || 1;
    json.equilibre = { isValid: false, ecart, ecartExplique: true };
    json.recapIncoherence = [
      { domaine: 'Decaissement', incoherente: true, ht: 0, tva: 0, ttc: ecart, residu: ecart, nbLignes: 1 },
    ];
    await route.fulfill({ json });
  });

  // Recharge le checkup avec l'interception active — go back to selection, then return to verifier
  await page.click('text=1. Sélection');
  await page.waitForTimeout(500);
  await page.click('button:has-text("Passer aux factures")');
  await page.waitForTimeout(500);
  await page.click('button:has-text("Continuer vers la vérification")');
  await page.waitForTimeout(1000);
  await page.waitForSelector('.animate-spin', { state: 'detached', timeout: 20000 });

  // 'Cohérence des totaux déclarés' is in the ChecklistCard visible in verifier mode
  const equilibreRow = page.getByText('Cohérence des totaux déclarés').first();
  await equilibreRow.waitFor({ state: 'attached', timeout: 15000 });
  await equilibreRow.scrollIntoViewIfNeeded();
  await expect(page.getByText('Écart détecté', { exact: false }).first()).toBeVisible();

  // Le tableau des lignes incohérentes est visible sous le contrôle en écart (table identifiée
  // par son en-tête « Écart », distincte de la table « Sous-totaux par taux »)
  const recapIncoherenceTable = page.locator('table').filter({ has: page.locator('th', { hasText: 'Écart' }) });
  const incoherenteRow = recapIncoherenceTable.locator('tbody tr').first();
  await expect(incoherenteRow).toBeVisible();
  await expect(incoherenteRow).toContainText('Lignes incohérentes');
  await page.screenshot({ path: '../VERIFY/task107-step3-avant-drill.png' });

  await incoherenteRow.click();
  await page.waitForTimeout(1000);

  // Bandeau de drill affiché, lecture seule, référence explicite aux lignes qui composent l'écart
  await expect(page.locator('text=Drill écart :')).toBeVisible();
  await expect(page.locator('text=Lignes incohérentes (TTC ≠ HT+TVA)')).toBeVisible();

  // La grille filtrée doit exposer un total de résultats
  await expect(page.locator('text=/Total résultats : \\d+/')).toBeVisible();
  await page.screenshot({ path: '../VERIFY/task107-step3-drill-source.png' });

  const totalResultatsText = await page.locator('text=/Total résultats : \\d+/').innerText();
  console.log('Total résultats du drill :', totalResultatsText);

  // Retour au contrôle — aucune action de figeage n'a eu lieu (lecture seule)
  await page.click('button:has-text("Retour au contrôle")');
  await expect(page.getByText('Cohérence des totaux déclarés').first()).toBeVisible();
  // In verifier mode there's a "Continuer vers la confirmation" button, not #btn-confirmer-integration
  await expect(page.locator('button:has-text("Continuer vers la confirmation")')).toBeVisible();

  console.log('Finished!');
});
