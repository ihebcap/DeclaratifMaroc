import { test, expect } from '@playwright/test';
import { execSync } from 'child_process';

// ─── TASK-111 — Drill « Détail des lignes » (②) : explosion de requêtes HTTP
// parallèles sur grosse sélection ──
//
// Signalement (dump console navigateur, `DESKTOP-5BFKKEP`) : une sélection de 150+
// règlements ouvrait AUTANT de requêtes GET /lignes en parallèle (une par règlement,
// Promise.all non borné) → ERR_INSUFFICIENT_RESOURCES (plafond ~6 connexions HTTP/1.1
// par origine). Correctif : le filtre `numeroRapprochement` accepte déjà une liste
// côté back (TASK-067B) — on regroupe par domaine et on émet UN appel par domaine
// (≤2 requêtes au total), jamais un par règlement.
//
// Reproduction MOCKÉE (autorisée par la task, cf. Livrables) : 150+ règlements réels
// ne sont pas disponibles dans le jeu de données de test ; on intercepte /rapprochement
// pour injecter une sélection synthétique de taille comparable au cas réel.

test.beforeAll(async () => {
  try {
    execSync('powershell -File ../reset.ps1');
  } catch (e) {
    console.error('Failed to reset DB:', e);
  }
});

const NB_DECAISSEMENT = 150;
const NB_ENCAISSEMENT = 10;

function buildReglements() {
  const rows: Record<string, unknown>[] = [];
  for (let i = 0; i < NB_DECAISSEMENT; i++) {
    rows.push({
      numeroReglement: `RF-DEC-${String(i).padStart(4, '0')}`,
      date: '2026-06-05', mode: 'Chèque', domaine: 'Décaissement',
      tiers: `Fournisseur ${i}`, tiersCode: `F${i}`, montant: 1000,
      rapprocheBanque: true, dateRapprochement: '2026-06-06', echeance: '2026-06-10',
      nbFacturesAffectees: 1, montantAffecte: 1000, resteAAffecter: 0,
      origine: '', declare: false,
    });
  }
  for (let i = 0; i < NB_ENCAISSEMENT; i++) {
    rows.push({
      numeroReglement: `RC-ENC-${String(i).padStart(4, '0')}`,
      date: '2026-06-05', mode: 'Virement', domaine: 'Encaissement',
      tiers: `Client ${i}`, tiersCode: `C${i}`, montant: 2000,
      rapprocheBanque: true, dateRapprochement: '2026-06-06', echeance: '2026-06-10',
      nbFacturesAffectees: 1, montantAffecte: 2000, resteAAffecter: 0,
      origine: '', declare: false,
    });
  }
  return rows;
}

function ligneMock(numeroRapprochement: string, domaine: string) {
  return {
    id: `${numeroRapprochement}-L1`,
    factureNumero: `FC-${numeroRapprochement}`,
    numeroRapprochement,
    tiers: 'Tiers Test', tiersIdentifiantFiscal: '12345678', tiersICE: '001234567000012',
    montantHT: 1000, tauxTVA: 20, montantTVA: 200, montantTTC: 1200,
    prorata: 100, montantAffecte: 1200, source: 'Test',
    statutLigne: 1, motif: '', statutConformite: 'Conforme', origine: domaine,
    ecId: 0, incoherenceValidee: false,
  };
}

async function login(page: import('@playwright/test').Page) {
  await page.goto('/');
  await page.fill('input[type="text"]', 'Admin');
  await page.fill('input[type="password"]', 'Admin');
  const select = page.getByRole('combobox');
  await select.waitFor({ state: 'attached' });
  await select.locator('option').nth(1).waitFor({ state: 'attached' });
  await select.selectOption({ index: 1 });
  await page.click('button:has-text("Se connecter")');
}

// Chaque test crée une déclaration sur une PÉRIODE DISTINCTE — reset.ps1 ne supprime pas les
// déclarations déjà créées (uniquement DM_LGTVA/DM_ENTTVA/RT_AFFECTATION.DT_Id), donc réutiliser
// le même mois entre deux tests déclenche un 409 Conflict (déclaration déjà existante).
async function creerDeclaration2026(page: import('@playwright/test').Page, moisLabel: string) {
  await page.click('button:has-text("Créer une déclaration")');
  await page.fill('input[type="number"]', '2026');
  await page.selectOption('select', { label: 'Mensuel' });
  const selects = await page.locator('select').all();
  if (selects.length > 1) {
    await selects[1].selectOption({ label: moisLabel });
  }
  await page.locator('button:text-is("Créer")').click();
  await page.waitForTimeout(2000);
}

test('TASK-111 : grosse sélection (160 règlements) ne déclenche plus une requête HTTP par règlement', async ({ page }) => {
  page.on('console', msg => console.log('BROWSER:', msg.text()));

  const reglements = buildReglements();

  await page.route('**/rapprochement?*', async (route) => {
    await route.fulfill({ json: { items: reglements, totalCount: reglements.length } });
  });

  const lignesRequests: string[] = [];
  await page.route('**/declarations/*/lignes?*', async (route) => {
    const url = new URL(route.request().url());
    lignesRequests.push(url.toString());
    const domaine = url.searchParams.get('domaine') || '';
    const filter = JSON.parse(url.searchParams.get('filter') || '{}');
    const numeros: string[] = Array.isArray(filter.numeroRapprochement) ? filter.numeroRapprochement : [];
    const items = numeros.map(n => ligneMock(n, domaine));
    await route.fulfill({ json: { items, totalCount: items.length, alertes: [] } });
  });

  await login(page);
  await creerDeclaration2026(page, 'Août (08)');

  const totalSelector = page.locator('text=/Total sélectionné : .+/');
  await expect(totalSelector).toBeVisible({ timeout: 15000 });
  await expect(totalSelector).not.toHaveText(/Total sélectionné : 0,00\s*MAD/);

  const selectionCountText = await page.locator('text=/\\d+ règlement.* sélectionné/').innerText();
  console.log('Sélection initiale :', selectionCountText);
  expect(selectionCountText).toContain(`${reglements.length}`);

  await page.click('button:has-text("Détail des lignes")');
  await page.waitForSelector('.animate-spin', { state: 'detached', timeout: 20000 });
  await page.waitForTimeout(500);

  console.log('Requêtes /lignes émises :', lignesRequests.length, lignesRequests);
  // Avant TASK-111 : une requête PAR règlement (160, ERR_INSUFFICIENT_RESOURCES).
  // Après : une requête PAR DOMAINE (2 domaines ici) — le seul multiplicateur restant
  // est le double-mount volontaire de React.StrictMode en dev (main.tsx), qui rejoue le
  // même effet deux fois (2 domaines x 2 = 4) ; en production StrictMode n'existe pas et
  // le nombre réel serait 2. Dans tous les cas, INDÉPENDANT de N (160), jamais N requêtes.
  expect(lignesRequests.length).toBeGreaterThan(0);
  expect(lignesRequests.length).toBeLessThanOrEqual(4);
  expect(lignesRequests.length).toBeLessThan(reglements.length);

  await expect(page.locator(`text=${reglements.length} règlement`)).toBeVisible();
  const ligneCount = page.locator('text=/^\\d+ ligne/');
  await expect(ligneCount).toBeVisible();
  const ligneCountText = await ligneCount.innerText();
  console.log('Lignes affichées dans le drill :', ligneCountText);
  expect(ligneCountText).toContain(`${reglements.length}`);

  // Aucun échec, donc aucun message d'erreur générique ni d'échec partiel.
  await expect(page.locator('text=Erreur lors du chargement des affectations')).toHaveCount(0);
  await expect(page.locator('text=/Échec du chargement/')).toHaveCount(0);

  await page.screenshot({ path: '../VERIFY/task111-grosse-selection.png' });
  console.log('Finished TASK-111 test (grosse sélection)!');
});

test('TASK-111 : échec réseau isolé sur un domaine identifie nommément les règlements affectés', async ({ page }) => {
  page.on('console', msg => console.log('BROWSER:', msg.text()));

  const reglements = buildReglements();

  await page.route('**/rapprochement?*', async (route) => {
    await route.fulfill({ json: { items: reglements, totalCount: reglements.length } });
  });

  await page.route('**/declarations/*/lignes?*', async (route) => {
    const url = new URL(route.request().url());
    const domaine = url.searchParams.get('domaine') || '';
    // Le domaine Décaissement échoue (réseau/serveur) — Encaissement doit rester intact.
    if (domaine === 'Decaissement') {
      await route.fulfill({ status: 500, json: { message: 'Erreur simulée (test TASK-111)' } });
      return;
    }
    const filter = JSON.parse(url.searchParams.get('filter') || '{}');
    const numeros: string[] = Array.isArray(filter.numeroRapprochement) ? filter.numeroRapprochement : [];
    const items = numeros.map(n => ligneMock(n, domaine));
    await route.fulfill({ json: { items, totalCount: items.length, alertes: [] } });
  });

  await login(page);
  await creerDeclaration2026(page, 'Septembre (09)');

  const totalSelector = page.locator('text=/Total sélectionné : .+/');
  await expect(totalSelector).toBeVisible({ timeout: 15000 });
  await expect(totalSelector).not.toHaveText(/Total sélectionné : 0,00\s*MAD/);

  await page.click('button:has-text("Détail des lignes")');
  await page.waitForSelector('.animate-spin', { state: 'detached', timeout: 20000 });

  // Message honnête : nomme le nombre de règlements en échec ET au moins l'un d'eux —
  // jamais l'ancien « Erreur lors du chargement des affectations » générique qui masquait
  // lequel des N règlements avait échoué.
  const echecToast = page.locator('text=/Échec du chargement pour \\d+ règlement/');
  await expect(echecToast).toBeVisible({ timeout: 3000 });
  const echecText = await echecToast.innerText();
  console.log('Message d\'échec isolé :', echecText);
  expect(echecText).toContain(`${NB_DECAISSEMENT}`);
  expect(echecText).toContain('RF-DEC-0000');

  // Le domaine Encaissement (non affecté par l'échec) reste chargé — la panne d'un
  // domaine n'efface pas les données de l'autre (pas de perte silencieuse).
  const ligneCount = page.locator('text=/^\\d+ ligne/');
  await expect(ligneCount).toBeVisible();
  const ligneCountText = await ligneCount.innerText();
  console.log('Lignes affichées malgré l\'échec Décaissement :', ligneCountText);
  expect(ligneCountText).toContain(`${NB_ENCAISSEMENT}`);

  await page.screenshot({ path: '../VERIFY/task111-echec-isole.png' });
  console.log('Finished TASK-111 test (échec isolé)!');
});
