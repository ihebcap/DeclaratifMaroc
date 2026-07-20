import { test, expect, type Page } from '@playwright/test';

// ─── TASK-139 — Badge « Écart détecté » global vs détail « ligne incohérente » filtré par onglet ──
//
// Défaut de RESTITUTION pur (front) : le badge d'écart est global (toutes lignes), mais le détail
// « ligne incohérente » sous le badge est filtré par l'onglet actif. Quand la ligne incohérente
// responsable de l'écart est dans l'AUTRE onglet, l'ancien code affichait « aucune ligne incohérente
// identifiée » — faux signal (l'écart EST expliqué, mais ailleurs). On rend le VRAI composant
// VerifierIntegrerPanel dans Chromium (harnais task139.html), /lignes et /checkup mockés au niveau
// réseau. Cas exact du signalement PO : déclaration TVA1-2026-02, écart -49 167,99 MAD, ligne
// incohérente côté Collectée (Encaissement), onglet Décaissement ouvert par défaut.

type RecapIncoherence = {
  domaine: string; incoherente: boolean; ht: number; tva: number; ttc: number; residu: number; nbLignes: number;
};

// Fenêtre haute : le panneau fait 100vh avec scroll interne ; on l'agrandit pour que le message
// sous le badge « Écart » soit entièrement visible sur la capture (preuve PO).
test.use({ viewport: { width: 1280, height: 1400 } });

const RECON = { candidates: 0, integrees: 0, exclues: 0, reportees: 0, proposees: 0, ecartees: 0 };

// Une ligne de valorisation minimale — juste pour franchir la garde « aucune ligne » du panneau
// (readOnly). fetchAllLignes réécrit le domaine par boucle : la même ligne peuple les deux onglets.
const LIGNE = {
  id: '1', factureNumero: 'FC2602001', tiers: 'FOURNISSEUR TEST', tiersIdentifiantFiscal: '',
  tiersICE: '', tauxTVA: 20, montantHT: 1000, montantTVA: 200, montantTTC: 1200, prorata: 100,
  montantAffecte: 1200, statutLigne: 1, motif: '', origine: 'Sage / OM', statutConformite: 'OK',
  domaine: 'Decaissement',
};

// Écart global -49 167,99 expliqué par 1 ligne incohérente côté Encaissement (Collectée).
const CHECKUP_AUTRE_ONGLET = {
  equilibre: { isValid: false, ecart: -49167.99, ecartExplique: true },
  alertes: [],
  reconciliation: RECON,
  recapIncoherence: [
    { domaine: 'Encaissement', incoherente: true, ht: 245839.95, tva: 49167.99, ttc: 295007.94, residu: 0, nbLignes: 1 } as RecapIncoherence,
  ],
};

// Cas TASK-112 (déjà correct) : la ligne incohérente est dans l'onglet AFFICHÉ (Décaissement).
const CHECKUP_MEME_ONGLET = {
  equilibre: { isValid: false, ecart: -49167.99, ecartExplique: true },
  alertes: [],
  reconciliation: RECON,
  recapIncoherence: [
    { domaine: 'Decaissement', incoherente: true, ht: 245839.95, tva: 49167.99, ttc: 295007.94, residu: 0, nbLignes: 1 } as RecapIncoherence,
  ],
};

// Écart réellement inexpliqué par AUCUNE ligne d'aucun domaine (ecartExplique false, rien d'incohérent).
const CHECKUP_NON_EXPLIQUE = {
  equilibre: { isValid: false, ecart: -49167.99, ecartExplique: false },
  alertes: [],
  reconciliation: RECON,
  recapIncoherence: [] as RecapIncoherence[],
};

async function mockApi(page: Page, checkup: object) {
  await page.route('**/api/declarations/*/lignes*', async (route) => {
    await route.fulfill({ json: { items: [LIGNE], totalCount: 1, distincts: {} } });
  });
  await page.route('**/api/declarations/*/checkup', async (route) => {
    await route.fulfill({ json: checkup });
  });
}

const NEW_MSG = /Écart expliqué\s*:/;
const OLD_TROMPEUR = /aucune ligne incohérente identifiée/i;

test('TASK-139 A — ligne incohérente dans l\'AUTRE onglet : renvoi explicite au bon domaine (le fix)', async ({ page }) => {
  await mockApi(page, CHECKUP_AUTRE_ONGLET);
  await page.goto('/task139.html');

  // Onglet Décaissement ouvert par défaut : le nouveau message doit renvoyer vers la TVA Collectée.
  await expect(page.getByText(NEW_MSG)).toBeVisible();
  await expect(page.getByText(/onglet «\s*TVA Collectée \(Ventes\)\s*»/)).toBeVisible();
  // Le faux signal ne doit PLUS apparaître.
  await expect(page.getByText(OLD_TROMPEUR)).toHaveCount(0);

  await page.screenshot({ path: '../VERIFY/task139-apres-A-autre-onglet-renvoi.png', fullPage: true });
});

test('TASK-139 B — bascule sur l\'onglet Collectée : le détail incohérence s\'affiche (pas de faux message)', async ({ page }) => {
  await mockApi(page, CHECKUP_AUTRE_ONGLET);
  await page.goto('/task139.html');

  await page.getByRole('button', { name: /TVA Collectée \(Ventes\)/ }).click();

  // Sur le bon onglet : le tableau RecapSourceTable (colonne « Écart ») est visible, plus de renvoi.
  await expect(page.getByText('Écart', { exact: true })).toBeVisible();
  await expect(page.getByText(NEW_MSG)).toHaveCount(0);
  await expect(page.getByText(OLD_TROMPEUR)).toHaveCount(0);

  await page.screenshot({ path: '../VERIFY/task139-apres-B-onglet-collectee-detail.png', fullPage: true });
});

test('TASK-139 C — non-régression TASK-112 : ligne incohérente dans l\'onglet affiché → tableau détail', async ({ page }) => {
  await mockApi(page, CHECKUP_MEME_ONGLET);
  await page.goto('/task139.html');

  // Onglet Décaissement (défaut) contient déjà l'incohérence → comportement TASK-112 inchangé.
  await expect(page.getByText('Écart', { exact: true })).toBeVisible();
  await expect(page.getByText(NEW_MSG)).toHaveCount(0);
  await expect(page.getByText(OLD_TROMPEUR)).toHaveCount(0);

  await page.screenshot({ path: '../VERIFY/task139-apres-C-meme-onglet-non-regression.png', fullPage: true });
});

test('TASK-139 D — non-régression : écart inexpliqué par aucune ligne → message « non expliqué » conservé', async ({ page }) => {
  await mockApi(page, CHECKUP_NON_EXPLIQUE);
  await page.goto('/task139.html');

  // Aucune ligne incohérente nulle part + ecartExplique=false → on NE bascule PAS vers « autre onglet ».
  await expect(page.getByText(OLD_TROMPEUR)).toBeVisible();
  await expect(page.getByText(NEW_MSG)).toHaveCount(0);

  await page.screenshot({ path: '../VERIFY/task139-apres-D-non-explique-conserve.png', fullPage: true });
});
