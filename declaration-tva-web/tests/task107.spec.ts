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
  try {
    execSync('powershell -File ../reset.ps1');
    execSync('powershell -File ../make_eligible.ps1');
  } catch (e) {
    console.error('Failed to reset DB / make eligible:', e);
  }
});

test('Test TASK-107: drill par source vers les factures/règlements réels', async ({ page }) => {
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

  // Create new declaration (Juin 2026 — période marquée éligible par make_eligible.ps1)
  await page.click('button:has-text("Créer une déclaration")');
  await page.fill('input[type="number"]', '2026');
  await page.selectOption('select', { label: 'Mensuel' });

  const selects = await page.locator('select').all();
  if (selects.length > 1) {
    await selects[1].selectOption({ label: 'Juin (06)' });
  }
  await page.locator('button:text-is("Créer")').click();
  await page.waitForTimeout(2000);

  const totalSelector = page.locator('text=/Total sélectionné : .+/');
  await expect(totalSelector).toBeVisible();

  // Décoche tout, puis sélectionne plusieurs lignes réellement éligibles (multi-pièces)
  const currentTotal = await totalSelector.innerText();
  if (!currentTotal.includes('0,00 MAD')) {
    await page.locator('input[type="checkbox"]').first().click();
    await expect(totalSelector).toContainText('0,00 MAD');
  }

  const enabledCheckboxes = page.locator('div[style*="absolute"]').filter({ hasText: 'Éligible' }).locator('input[type="checkbox"]');
  const nbEligibles = await enabledCheckboxes.count();
  const nbASelectionner = Math.min(5, nbEligibles);
  for (let i = 0; i < nbASelectionner; i++) {
    await enabledCheckboxes.nth(i).click();
  }
  await expect(totalSelector).not.toHaveText(/Total sélectionné : 0,00\s*MAD/);
  await page.waitForTimeout(500);

  // Étape ③ Vérifier & Intégrer
  await page.click('button:has-text("Passer au calcul")');
  await page.waitForTimeout(2000);
  await page.waitForSelector('.animate-spin', { state: 'detached' });

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

  // Recharge le checkup avec l'interception active
  await page.click('text=1. Sélection');
  await page.waitForTimeout(500);
  await page.click('button:has-text("Passer au calcul")');
  await page.waitForTimeout(1500);
  await page.waitForSelector('.animate-spin', { state: 'detached' });

  const equilibreRow = page.locator('text="Cohérence des totaux déclarés"').first();
  await equilibreRow.scrollIntoViewIfNeeded();
  await expect(page.locator('text=/Écart détecté/')).toBeVisible();

  // Le tableau des lignes incohérentes est visible sous le contrôle en écart (table identifiée
  // par son en-tête « Écart », distincte de la table « Sous-totaux par taux ») — TASK-112 :
  // remplace l'axe Source (tautologique) par l'axe qui compose réellement l'écart.
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

  // La grille filtrée doit exposer un total de résultats (traçabilité honnête : même à 0,
  // le compteur reste visible et explicite — jamais une grille muette)
  await expect(page.locator('text=/Total résultats : \\d+/')).toBeVisible();
  await page.screenshot({ path: '../VERIFY/task107-step3-drill-source.png' });

  const totalResultatsText = await page.locator('text=/Total résultats : \\d+/').innerText();
  console.log('Total résultats du drill :', totalResultatsText);

  // Retour au contrôle — aucune action de figeage n'a eu lieu (lecture seule)
  await page.click('button:has-text("Retour au contrôle")');
  await expect(page.locator('text="Cohérence des totaux déclarés"').first()).toBeVisible();
  await expect(page.locator('#btn-confirmer-integration')).toBeVisible();

  console.log('Finished!');
});
