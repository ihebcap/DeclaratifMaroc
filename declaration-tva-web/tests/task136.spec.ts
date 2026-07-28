import { test, expect, type Route } from '@playwright/test';

// ─── TASK-136 — Menu : nouvelle entrée « Délai de paiement » ──────────────────────────────────
//
// Test de NAVIGATION uniquement (aucune règle métier ici, déjà couverte par task130.spec.ts /
// task134.spec.ts) : prouve que la nouvelle entrée de menu route bien vers les 3 écrans TASK-130/
// TASK-134, et qu'aucun écran existant (Rapprochement/Factures/Déclaration TVA) n'est régressé.
// Harnais task136.html : rend le VRAI Dashboard (App.tsx, exporté pour ce test), en contournant le
// flux licence/connexion (déjà couvert par declaration.spec.ts, hors périmètre de cette TASK).
// /api/** mocké au niveau réseau (pas besoin du backend .NET ni de la base réelle).

// Dispatcher unique par préfixe (évite toute ambiguïté d'ordre entre plusieurs `page.route` qui
// se chevauchent sur le même préfixe) — même pattern que task130.spec.ts/task134.spec.ts.
async function installMocks(page: import('@playwright/test').Page) {
  // Écran « Déclaration TVA » (section par défaut, doit rester inchangée) : liste vide suffit.
  // Prédicat exact sur le chemin (pas de glob `**`) : évite tout chevauchement avec
  // `/declarations-delai-paiement*`, qui partage le même préfixe littéral.
  await page.route((url) => url.pathname === '/api/declarations', (route: Route) => route.fulfill({ json: [] }));

  // Rapprochement / Factures (INTERROGATION, inchangés) : réponses vides pour éviter tout bruit
  // réseau non mocké, sans exercer leur logique métier (hors périmètre TASK-136).
  await page.route('**/api/rapprochement**', (route: Route) => route.fulfill({ json: [] }));
  await page.route('**/api/factures**', (route: Route) => route.fulfill({ json: [] }));

  // Domaine DDP (TASK-131/132/134) : un seul dispatcher, branché sur le chemin exact.
  await page.route('**/api/declarations-delai-paiement**', (route: Route) => {
    const path = new URL(route.request().url()).pathname;
    if (path.endsWith('/declarations-delai-paiement/parametrage')) {
      return route.fulfill({ json: { typeParDefaut: 'Trimestrielle' } });
    }
    if (path.endsWith('/declarations-delai-paiement/controle')) {
      // Écran de contrôle (TASK-134, écran 4) : forme complète de SelectionDdpDto (0 ligne).
      return route.fulfill({
        json: {
          dateDebutPeriode: '2026-01-01T00:00:00',
          dateFinPeriode: '2026-03-31T23:59:59',
          dateMiseEnRouteSociete: '2023-07-01T00:00:00',
          nombreEcheancesExaminees: 0,
          lignes: [],
          lignesRepriseManuelleRequise: [],
        },
      });
    }
    if (path.endsWith('/declarations-delai-paiement')) {
      return route.fulfill({ json: [] });
    }
    return route.fulfill({ json: [] });
  });

  // Écran « Conventions » (TASK-129/130) : un seul dispatcher, branché sur le chemin exact.
  await page.route('**/api/conventions-delai-paiement**', (route: Route) => {
    return route.fulfill({ json: [] });
  });
}

test('Navigation : la nouvelle entrée « Délai de paiement » atteint les 3 sous-écrans, sans régression des écrans existants', async ({ page }) => {
  await installMocks(page);

  page.on('pageerror', (err) => { throw err; }); // toute exception React fait échouer le test

  await page.goto('/task136.html');

  // Section par défaut inchangée : « Déclaration TVA » (aucune régression de comportement).
  await expect(page.locator('.sidebar-item.active')).toContainText('Déclaration TVA');

  // Régression : Rapprochement bancaire toujours atteignable et affiché normalement.
  await page.click('.sidebar-item:has-text("Rapprochement bancaire")');
  await expect(page.locator('.sidebar-item.active')).toContainText('Rapprochement bancaire');

  // Régression : Factures toujours atteignable.
  await page.click('.sidebar-item:has-text("Factures")');
  await expect(page.locator('.sidebar-item.active')).toContainText('Factures');

  // Retour Déclaration TVA (régression du 3e écran existant).
  await page.click('.sidebar-item:has-text("Déclaration TVA")');
  await expect(page.locator('.sidebar-item.active')).toContainText('Déclaration TVA');

  // Nouvelle entrée « Délai de paiement ».
  const ddpEntry = page.locator('.sidebar-item:has-text("Délai de paiement")');
  await expect(ddpEntry).toBeVisible();
  await ddpEntry.click();
  await expect(page.locator('.sidebar-item.active')).toContainText('Délai de paiement');

  // Sous-écran 1 (par défaut) : Déclarations DDP (TASK-134, écran 1).
  await expect(page.locator('h2:has-text("Déclarations délai de paiement")')).toBeVisible();
  await page.screenshot({ path: '../VERIFY/task136-1-declarations.png' });

  // Sous-écran 2 : Sélection / Contrôle (TASK-134, écran 4).
  await page.click('button:has-text("Sélection / Contrôle")');
  await expect(page.locator('h2:has-text("Contrôle des lignes hors délai")')).toBeVisible();
  await page.screenshot({ path: '../VERIFY/task136-2-controle.png' });

  // Sous-écran 3 : Conventions (TASK-130).
  await page.click('button:has-text("Conventions")');
  await expect(page.locator('h2:has-text("Conventions de délai de paiement")')).toBeVisible();
  await page.screenshot({ path: '../VERIFY/task136-3-conventions.png' });

  // Retour au sous-écran 1 : la sous-navigation est bien bidirectionnelle.
  await page.click('button:has-text("Déclarations")');
  await expect(page.locator('h2:has-text("Déclarations délai de paiement")')).toBeVisible();

  // Régression finale : quitter la section DDP puis y revenir ne casse rien, et la section
  // Déclaration TVA reste, elle, strictement inchangée (aucune sous-navigation ajoutée dessus).
  await page.click('.sidebar-item:has-text("Déclaration TVA")');
  await expect(page.locator('button:has-text("Créer une déclaration")')).toBeVisible();
});
