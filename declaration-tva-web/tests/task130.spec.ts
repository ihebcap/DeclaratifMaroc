import { test, expect, type Page, type Route } from '@playwright/test';

// ─── TASK-130 — Convention de délai de paiement par tiers (front) ─────────────────────────────
//
// Parcours e2e du VRAI composant ConventionsDelaiPaiementPanel dans Chromium (harnais
// task130.html, même pattern que task138/task139) : /api/conventions-delai-paiement* mocké au
// niveau réseau via un dispatcher unique (évite toute ambiguïté d'ordre/priorité entre plusieurs
// `page.route` qui se chevauchent sur le même préfixe) — pas besoin du backend .NET ni de la base
// réelle. Couvre les 4 scénarios du critère de validation de la TASK : création Convention,
// création Facture, rejet chevauchement (message explicite serveur, pas générique), clôture
// anticipée (+ borne UI rejetée sans appel réseau).

const TIERS_F001 = { tiersNo: 100, tiersCode: 'F001', tiersIntitule: 'Fournisseur Test' };

// Les labels de ce formulaire sont de simples <label> siblings (pas de for/id, pattern déjà en
// place dans tout le reste du dépôt front — cf. absence totale de getByLabel dans les autres specs
// e2e du projet) : on cible le champ via le sélecteur CSS "sibling adjacent" plutôt que getByLabel
// (qui exige une association programmatique absente ici).
function fieldByLabel(page: Page, label: string) {
  return page.locator(`label:has-text("${label}") + input`);
}

type Dispatcher = (route: Route, url: URL) => Promise<boolean>; // true = géré

async function installDispatcher(page: Page, handlers: Dispatcher[]) {
  await page.route('**/api/conventions-delai-paiement**', async (route: Route) => {
    const url = new URL(route.request().url());
    for (const handler of handlers) {
      if (await handler(route, url)) return;
    }
    await route.fulfill({ status: 404, json: { Message: 'Route non mockée : ' + url.pathname } });
  });
}

function listHandler(items: object[]): Dispatcher {
  return async (route, url) => {
    if (route.request().method() !== 'GET' || !url.pathname.endsWith('/conventions-delai-paiement')) return false;
    await route.fulfill({ json: items });
    return true;
  };
}

function tiersHandler(items: object[]): Dispatcher {
  return async (route, url) => {
    if (route.request().method() !== 'GET' || !url.pathname.endsWith('/tiers')) return false;
    await route.fulfill({ json: items.map((t: any) => ({ tiersNo: t.tiersNo, tiersCode: t.tiersCode, tiersIntitule: t.tiersIntitule })) });
    return true;
  };
}

function facturesNonPayeesHandler(items: object[]): Dispatcher {
  return async (route, url) => {
    if (route.request().method() !== 'GET' || !url.pathname.endsWith('/factures-non-payees')) return false;
    await route.fulfill({ json: items });
    return true;
  };
}

function createHandler(onCreate: (body: any) => { status: number, json: object }): Dispatcher {
  return async (route, url) => {
    if (route.request().method() !== 'POST' || !url.pathname.endsWith('/conventions-delai-paiement')) return false;
    const body = route.request().postDataJSON();
    const { status, json } = onCreate(body);
    await route.fulfill({ status, json });
    return true;
  };
}

function terminerHandler(onTerminer: () => void): Dispatcher {
  return async (route, url) => {
    if (route.request().method() !== 'PUT' || !url.pathname.endsWith('/terminer')) return false;
    onTerminer();
    await route.fulfill({ status: 204, body: '' });
    return true;
  };
}

test('TASK-130 A — création Convention : parcours complet, la liste se rafraîchit', async ({ page }) => {
  // État piloté par la CRÉATION (pas par un compteur d'appels GET) : React 19 StrictMode (dev,
  // utilisé par le harnais) double-invoque les effets de montage, donc le 1er GET de la liste
  // peut survenir 2 fois avant toute interaction — un compteur d'appels serait fragile ici.
  let created = false;
  const ROW_APRES_CREATION = {
    cpId: 501, tiersNo: 100, tiersCode: 'F001', date: '2026-07-28', numero: 'CONV-001',
    dateDebut: '2026-01-01', dateFin: '2026-06-30', nombreJoursDelaisPaiement: 60,
    domaine: 'Achat', type: 'Convention', factureNo: null, factureNumero: null,
    hasFile: false, valide: true,
  };
  let createdBody: any = null;

  await installDispatcher(page, [
    async (route, url) => {
      if (route.request().method() !== 'GET' || !url.pathname.endsWith('/conventions-delai-paiement')) return false;
      await route.fulfill({ json: created ? [ROW_APRES_CREATION] : [] });
      return true;
    },
    tiersHandler([TIERS_F001]),
    createHandler((body) => { createdBody = body; created = true; return { status: 200, json: { cpId: 501 } }; }),
  ]);

  await page.goto('/task130.html');
  await expect(page.getByText('Aucune convention fournisseur pour ces filtres.')).toBeVisible();

  await page.getByRole('button', { name: /Nouvelle convention/ }).click();
  await expect(page.getByRole('heading', { name: /Nouvelle convention/ })).toBeVisible();

  // Type Convention déjà sélectionné par défaut.
  await page.getByPlaceholder('Code ou nom du tiers…').fill('F0');
  await page.getByText('F001 — Fournisseur Test').click();

  await fieldByLabel(page, 'N° convention').fill('CONV-001');
  await fieldByLabel(page, 'Date début').fill('2026-01-01');
  await fieldByLabel(page, 'Date fin').fill('2026-06-30');
  await fieldByLabel(page, 'Délai de paiement').fill('60');

  await page.getByRole('button', { name: 'Créer' }).click();

  // Modale fermée, et la liste (rechargée) montre la nouvelle ligne.
  await expect(page.getByRole('heading', { name: /Nouvelle convention/ })).toHaveCount(0);
  await expect(page.getByText('CONV-001')).toBeVisible();

  expect(createdBody).toMatchObject({
    tiersNo: 100, numero: 'CONV-001', type: 'Convention', domaine: 'achat',
    dateDebut: '2026-01-01', dateFin: '2026-06-30', nombreJoursDelaisPaiement: 60,
  });

  await page.screenshot({ path: '../VERIFY/task130-A-creation-convention.png', fullPage: true });
});

test('TASK-130 B — création Facture : sélection tiers puis facture non payée', async ({ page }) => {
  let createdBody: any = null;
  await installDispatcher(page, [
    listHandler([]),
    tiersHandler([TIERS_F001]),
    facturesNonPayeesHandler([
      { ecId: 777, doNumero: 'FF260099', doDate: '2026-05-10', montant: 1200, solde: 1200 },
    ]),
    createHandler((body) => { createdBody = body; return { status: 200, json: { cpId: 502 } }; }),
  ]);

  await page.goto('/task130.html');
  await page.getByRole('button', { name: /Nouvelle convention/ }).click();
  await page.getByRole('button', { name: 'Facture (dérogation ponctuelle)' }).click();

  await page.getByPlaceholder('Code ou nom du tiers…').fill('F0');
  await page.getByText('F001 — Fournisseur Test').click();

  await expect(page.locator('select.form-input')).toBeVisible();
  await page.locator('select.form-input').selectOption('777');

  await fieldByLabel(page, 'N° convention').fill('FACT-001');
  await fieldByLabel(page, 'Délai de paiement').fill('30');

  await page.getByRole('button', { name: 'Créer' }).click();

  await expect(page.getByRole('heading', { name: /Nouvelle convention/ })).toHaveCount(0);

  expect(createdBody).toMatchObject({
    tiersNo: 100, numero: 'FACT-001', type: 'Facture', domaine: 'achat',
    factureNo: 777, nombreJoursDelaisPaiement: 30,
  });

  await page.screenshot({ path: '../VERIFY/task130-B-creation-facture.png', fullPage: true });
});

test('TASK-130 C — rejet chevauchement : message serveur explicite affiché tel quel (pas générique)', async ({ page }) => {
  const MESSAGE_CONFLIT = 'Il existe une convention pour la même période (convention [CONV-000], 01/01/2026 - 31/03/2026).';
  await installDispatcher(page, [
    listHandler([]),
    tiersHandler([TIERS_F001]),
    createHandler(() => ({ status: 409, json: { Message: MESSAGE_CONFLIT } })),
  ]);

  await page.goto('/task130.html');
  await page.getByRole('button', { name: /Nouvelle convention/ }).click();

  await page.getByPlaceholder('Code ou nom du tiers…').fill('F0');
  await page.getByText('F001 — Fournisseur Test').click();
  await fieldByLabel(page, 'N° convention').fill('CONV-002');
  await fieldByLabel(page, 'Date début').fill('2026-02-01');
  await fieldByLabel(page, 'Date fin').fill('2026-02-28');
  await fieldByLabel(page, 'Délai de paiement').fill('45');

  await page.getByRole('button', { name: 'Créer' }).click();

  // Message serveur affiché TEL QUEL, citant la convention en conflit (numéro + période) —
  // jamais un message générique ("Erreur lors de la création..."). Modale reste ouverte.
  await expect(page.getByText(MESSAGE_CONFLIT)).toBeVisible();
  await expect(page.getByText('Erreur lors de la création de la convention.')).toHaveCount(0);
  await expect(page.getByRole('heading', { name: /Nouvelle convention/ })).toBeVisible();

  await page.screenshot({ path: '../VERIFY/task130-C-rejet-chevauchement.png', fullPage: true });
});

test('TASK-130 D — clôture anticipée : succès dans les bornes, rejet UI hors bornes sans appel réseau', async ({ page }) => {
  const ROW = {
    cpId: 900, tiersNo: 100, tiersCode: 'F001', date: '2026-01-01', numero: 'CONV-900',
    dateDebut: '2026-01-01', dateFin: '2026-12-31', nombreJoursDelaisPaiement: 60,
    domaine: 'Achat', type: 'Convention', factureNo: null, factureNumero: null,
    hasFile: false, valide: true,
  };
  let terminerCalls = 0;
  await installDispatcher(page, [
    listHandler([ROW]),
    terminerHandler(() => { terminerCalls++; }),
  ]);

  await page.goto('/task130.html');
  await expect(page.getByText('CONV-900')).toBeVisible();

  await page.getByRole('button', { name: 'Terminer' }).click();
  await expect(page.getByRole('heading', { name: /Clôture anticipée/ })).toBeVisible();

  // D1 — hors borne haute (> DateFin actuelle) : rejet CÔTÉ UI, aucun appel réseau. Le champ porte
  // `max` = DateFin actuelle (borne native HTML5, Étape 4 de la TASK) : Chromium bloque la
  // soumission du <form> AVANT même que le onSubmit React ne s'exécute (comportement natif du
  // navigateur, pas un bug) — on vérifie donc la contrainte native explicitement (checkValidity)
  // plutôt que de supposer quel mécanisme (natif ou JS applicatif, cf. ValiderTerminer) a bloqué.
  await page.locator('input[type="date"]').fill('2027-01-01');
  const bloqueParContrainteNative = await page.locator('input[type="date"]').evaluate((el: HTMLInputElement) => !el.checkValidity());
  expect(bloqueParContrainteNative).toBe(true);
  await page.getByRole('button', { name: 'Confirmer' }).click();
  // Modale toujours ouverte, aucun appel réseau : la borne a bien empêché la soumission.
  await expect(page.getByRole('heading', { name: /Clôture anticipée/ })).toBeVisible();
  expect(terminerCalls).toBe(0);

  // D2 — dans les bornes : succès, modale se ferme.
  await page.locator('input[type="date"]').fill('2026-06-30');
  await page.getByRole('button', { name: 'Confirmer' }).click();
  await expect(page.getByRole('heading', { name: /Clôture anticipée/ })).toHaveCount(0);
  expect(terminerCalls).toBe(1);

  await page.screenshot({ path: '../VERIFY/task130-D-cloture-anticipee.png', fullPage: true });
});
