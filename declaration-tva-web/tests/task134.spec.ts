import { test, expect, type Page, type Route } from '@playwright/test';

// ─── TASK-134 — DDP : liste, fiche, popup de sélection, écran de contrôle ───────────────────────
//
// Parcours e2e des VRAIS composants (DeclarationsDelaiPaiementPanel / ControleLignesDelaiPaiementPanel)
// dans Chromium via le harnais task134.html — même pattern que task130/task138/task139. Les réponses
// /api sont mockées AU NIVEAU RÉSEAU par un dispatcher unique et STATEFUL (pas besoin du backend .NET
// ni de la base réelle ; le back a été exercé séparément contre GR_EMA_DISTRIBUTION, cf. VERIFY).
//
// Le mock reproduit fidèlement les CONTRATS du contrôleur TASK-134 : les drapeaux `actions` sont
// recalculés par le mock à partir des mêmes règles que DeclarationDelaiPaiementCycleDeVie (clôture
// interdite sans ligne, génération réservée aux clôturées, dépôt réservé aux fichiers générés), et le
// contrôle IF/ICE renvoie un 409 avec `Message` + `FournisseursFautifs` exactement comme le back.
//
// Couverture : cycle complet création → sélection → intégration → clôture → génération BLOQUÉE par
// IF/ICE manquant (message + fournisseurs fautifs affichés) → correction → génération réussie →
// dépôt ; plus le critère structurant « aucun filtre de date libre » sur les deux écrans, et le
// badge + la saisie manuelle de reprise sur l'écran de contrôle.

type Etat = {
  declaration: any | null;
  lignes: any[];
  ifIceConforme: boolean;
  dateMiseEnRoute: string | null;
  reprises: { ecId: number, date: string }[];
  clotures: number;
  depots: number;
};

const FOURNISSEURS_FAUTIFS = [
  {
    tiersNo: 41, tiersCode: '4411INWI', tiersIntitule: 'Tiers à créer',
    identifiantFiscal: null, ice: null, nombreLignes: 1,
    motifsLibelles: ['identifiant fiscal absent', 'ICE absent'],
  },
  {
    tiersNo: 42, tiersCode: 'FOUBREL', tiersIntitule: 'Tiers à créer',
    identifiantFiscal: null, ice: null, nombreLignes: 1,
    motifsLibelles: ['identifiant fiscal absent', 'ICE absent'],
  },
];

const MESSAGE_BLOQUANT =
  'Génération du fichier impossible : 2 fournisseurs ont une identité fiscale invalide (identifiant '
  + 'fiscal de 8 caractères sans espace, ICE de 15 caractères sans espace). Corrigez la fiche tiers '
  + 'dans l\'ERP puis relancez la génération.';

/** Deux candidates + une ligne « antérieure à la mise en route » (Depassement null, JAMAIS 0). */
function lignesSelection() {
  return {
    dateDebutPeriode: '2026-01-01T00:00:00',
    dateFinPeriode: '2026-03-31T23:59:59',
    dateMiseEnRouteSociete: '2023-07-01T00:00:00',
    nombreEcheancesExaminees: 1408,
    lignes: [
      {
        ecId: 19909, afId: null, bucket: 'HorsPeriodePartNonAffectee', statut: 'Candidate',
        echeanceLegale: '2025-04-16T00:00:00', nombreJoursDelaiApplique: 60, origineDelai: 'Defaut',
        borneActuelle: '2026-03-31T00:00:00', borneReference: '2025-04-16T00:00:00',
        origineBorneReference: 'EcheanceLegale', depassement: 349, montantLigne: 12000,
        doNumero: 'FA250015', doDate: '2025-02-15T00:00:00', doReference: 'REF-15',
        echeanceContractuelle: '2025-03-17T00:00:00', montantEcheance: 12000, soldeEcheance: 12000,
        tiersNo: 41, tiersCode: '4411INWI', tiersIntitule: 'Tiers à créer',
        typeReglement: null, dateReglement: null, dateRapprochement: null, reglementPiece: null,
      },
      {
        ecId: 20155, afId: 8801, bucket: 'DansPeriodePartAffectee', statut: 'Candidate',
        echeanceLegale: '2026-01-20T00:00:00', nombreJoursDelaiApplique: 60, origineDelai: 'Defaut',
        borneActuelle: '2026-02-25T00:00:00', borneReference: '2026-01-20T00:00:00',
        origineBorneReference: 'EcheanceLegale', depassement: 36, montantLigne: 4500,
        doNumero: 'FA260007', doDate: '2025-11-21T00:00:00', doReference: null,
        echeanceContractuelle: '2026-01-15T00:00:00', montantEcheance: 4500, soldeEcheance: 0,
        tiersNo: 42, tiersCode: 'FOUBREL', tiersIntitule: 'Tiers à créer',
        typeReglement: 'Cheque', dateReglement: '2026-02-20T00:00:00',
        dateRapprochement: '2026-02-25T00:00:00', reglementPiece: 'CHQ-778',
      },
    ],
    lignesRepriseManuelleRequise: [
      {
        ecId: 17001, afId: null, bucket: 'HorsPeriodePartNonAffectee', statut: 'RepriseManuelleRequise',
        echeanceLegale: '2023-05-10T00:00:00', nombreJoursDelaiApplique: 60, origineDelai: 'Defaut',
        borneActuelle: '2026-03-31T00:00:00', borneReference: null,
        origineBorneReference: 'Indeterminee', depassement: null, montantLigne: 25000,
        doNumero: 'FA230001', doDate: '2023-03-11T00:00:00', doReference: null,
        echeanceContractuelle: '2023-04-10T00:00:00', montantEcheance: 25000, soldeEcheance: 25000,
        tiersNo: 43, tiersCode: 'FOUANC', tiersIntitule: 'Fournisseur ancien',
        typeReglement: null, dateReglement: null, dateRapprochement: null, reglementPiece: null,
      },
    ],
  };
}

/** Reproduit les gardes TASK-132 : c'est le SERVEUR qui décide de l'état des boutons. */
function actionsDe(d: any, nbLignes: number) {
  const enCours = d.statut === 'EnCours';
  const clot = d.statut === 'Cloture';
  return {
    peutModifierLibelle: !d.estDeposee && enCours,
    peutIntegrerLignes: !d.estDeposee && enCours,
    peutCloturer: !d.estDeposee && enCours && nbLignes > 0,
    peutAnnulerCloture: !d.estDeposee && !d.fichierGenere && clot,
    peutGenererFichier: !d.fichierGenere && !d.estDeposee && clot && nbLignes > 0,
    peutAnnulerGeneration: d.fichierGenere && !d.estDeposee && clot && nbLignes > 0,
    peutDeposer: d.fichierGenere && !d.estDeposee && clot && nbLignes > 0,
    peutSupprimer: !d.estDeposee && enCours && nbLignes === 0,
  };
}

function declarationDto(etat: Etat) {
  const d = etat.declaration!;
  return { ...d, nombreLignes: etat.lignes.length, actions: actionsDe(d, etat.lignes.length) };
}

async function installMock(page: Page, etat: Etat) {
  await page.route('**/api/**', async (route: Route) => {
    const url = new URL(route.request().url());
    const p = url.pathname.replace(/^.*\/api/, '');
    const method = route.request().method();

    // ── TASK-128 : paramétrage « date de mise en route » + reprise manuelle ──
    if (p.startsWith('/delai-paiement/parametrage/')) {
      if (method === 'GET') return route.fulfill({ json: { soId: 1, dateMiseEnRoute: etat.dateMiseEnRoute } });
      if (method === 'PUT') {
        etat.dateMiseEnRoute = route.request().postDataJSON()?.dateMiseEnRoute ?? null;
        return route.fulfill({ status: 204, body: '' });
      }
    }
    if (p === '/delai-paiement/reprise' && method === 'POST') {
      const body = route.request().postDataJSON();
      etat.reprises.push({ ecId: body.ecId, date: body.dateDejaDeclareeJusquau });
      return route.fulfill({ status: 204, body: '' });
    }

    // ── TASK-134 : domaine déclaration DDP ──
    if (p === '/declarations-delai-paiement/parametrage' && method === 'GET') {
      return route.fulfill({ json: { soId: 1, typeParDefautCode: 1, typeParDefaut: 'Annuelle' } });
    }
    if (p === '/declarations-delai-paiement/controle' && method === 'GET') {
      const base = lignesSelection();
      const type = url.searchParams.get('type');
      const trimestre = url.searchParams.get('trimestre');
      // Bornes CALCULÉES côté serveur : le mock les dérive du type/trimestre reçus, jamais de dates
      // transmises par le client (le test vérifie d'ailleurs qu'aucune date n'est envoyée).
      const bornes = type === 'annuelle'
        ? { debut: '2026-01-01T00:00:00', fin: '2026-12-31T23:59:59' }
        : trimestre === '2'
          ? { debut: '2026-04-01T00:00:00', fin: '2026-06-30T23:59:59' }
          : { debut: '2026-01-01T00:00:00', fin: '2026-03-31T23:59:59' };
      // Une reprise saisie fait basculer la ligne bloquée côté serveur (comportement TASK-131).
      const bloquees = etat.reprises.some(r => r.ecId === 17001) ? [] : base.lignesRepriseManuelleRequise;
      return route.fulfill({
        json: {
          ...base,
          dateDebutPeriode: bornes.debut,
          dateFinPeriode: bornes.fin,
          dateMiseEnRouteSociete: etat.dateMiseEnRoute,
          lignesRepriseManuelleRequise: bloquees,
        },
      });
    }
    if (p === '/declarations-delai-paiement' && method === 'GET') {
      return route.fulfill({ json: etat.declaration ? [declarationDto(etat)] : [] });
    }
    if (p === '/declarations-delai-paiement' && method === 'POST') {
      const body = route.request().postDataJSON();
      etat.declaration = {
        ddpId: 1, numero: 'DDP26070001', soId: 1, date: '2026-07-28T00:00:00',
        exercice: body.exercice, type: body.type === 'annuelle' ? 'Annuelle' : 'Trimestrielle',
        trimestre: body.type === 'annuelle' ? null : body.trimestre,
        dateDebut: '2026-01-01T00:00:00', dateFin: '2026-03-31T23:59:59',
        statut: 'EnCours', estDeposee: false, fichierGenere: false, libelle: body.libelle ?? null,
        dateCreation: '2026-07-28T00:00:00', dateModification: '2026-07-28T00:00:00',
      };
      return route.fulfill({ json: { ddpId: 1 } });
    }
    if (p === '/declarations-delai-paiement/1' && method === 'GET') {
      return route.fulfill({ json: declarationDto(etat) });
    }
    if (p === '/declarations-delai-paiement/1/lignes' && method === 'GET') {
      return route.fulfill({ json: etat.lignes });
    }
    if (p === '/declarations-delai-paiement/1/selection' && method === 'GET') {
      const base = lignesSelection();
      const dejaIntegrees = new Set(etat.lignes.map(l => `${l.ecId}|${l.afId ?? ''}`));
      return route.fulfill({
        json: {
          ...base,
          dateMiseEnRouteSociete: etat.dateMiseEnRoute,
          lignes: base.lignes.filter(l => !dejaIntegrees.has(`${l.ecId}|${l.afId ?? ''}`)),
        },
      });
    }
    if (p === '/declarations-delai-paiement/1/lignes' && method === 'POST') {
      const selection: { ecId: number, afId: number | null }[] = route.request().postDataJSON()?.selection ?? [];
      const candidates = lignesSelection().lignes;
      let integrees = 0;
      for (const cle of selection) {
        const src = candidates.find(c => c.ecId === cle.ecId && (c.afId ?? null) === (cle.afId ?? null));
        if (!src) continue;
        etat.lignes.push({
          ddplId: 1000 + etat.lignes.length, ecId: src.ecId, afId: src.afId,
          depassement: src.depassement, echeanceLegale: src.echeanceLegale,
          tiersNo: src.tiersNo, tiersCode: src.tiersCode, tiersIntitule: src.tiersIntitule,
          doNumero: src.doNumero, doDate: src.doDate, doReference: src.doReference,
          echeanceContractuelle: src.echeanceContractuelle, montantEcheance: src.montantEcheance,
          soldeEcheance: src.soldeEcheance, montantAffecte: src.afId ? src.montantLigne : null,
          reglementNumero: null, reglementPiece: src.reglementPiece, reglementDate: src.dateReglement,
          reglementRapproche: src.dateRapprochement != null, reglementDateRapprochement: src.dateRapprochement,
        });
        integrees++;
      }
      return route.fulfill({
        json: {
          ddpId: 1, nombreCandidates: candidates.length, nombreIntegrees: integrees,
          clesDejaIntegrees: [], clesRefuseesRepriseManuelleRequise: [], clesIntrouvablesDansSelection: [],
          nombreRepriseManuelleRequiseDisponibles: 1, dateMiseEnRouteSociete: etat.dateMiseEnRoute,
        },
      });
    }
    if (p === '/declarations-delai-paiement/1/cloture' && method === 'POST') {
      etat.clotures++;
      etat.declaration.statut = 'Cloture';
      return route.fulfill({ status: 204, body: '' });
    }
    if (p === '/declarations-delai-paiement/1/decloture' && method === 'POST') {
      etat.declaration.statut = 'EnCours';
      return route.fulfill({ status: 204, body: '' });
    }
    if (p === '/declarations-delai-paiement/1/controle-identite-fiscale' && method === 'GET') {
      return route.fulfill({
        json: etat.ifIceConforme
          ? { estConforme: true, nombreFournisseursExamines: 2, nombreLignesExaminees: etat.lignes.length, messageBloquant: '', fournisseursFautifs: [] }
          : { estConforme: false, nombreFournisseursExamines: 2, nombreLignesExaminees: etat.lignes.length, messageBloquant: MESSAGE_BLOQUANT, fournisseursFautifs: FOURNISSEURS_FAUTIFS },
      });
    }
    if (p === '/declarations-delai-paiement/1/generation' && method === 'POST') {
      if (!etat.ifIceConforme) {
        // Exactement le contrat du contrôleur : 409 + message serveur + fournisseurs fautifs.
        return route.fulfill({ status: 409, json: { Message: MESSAGE_BLOQUANT, FournisseursFautifs: FOURNISSEURS_FAUTIFS } });
      }
      etat.declaration.fichierGenere = true;
      return route.fulfill({ json: { fichier: '/declarations-delai-paiement/1/fichier' } });
    }
    if (p === '/declarations-delai-paiement/1/generation/annulation' && method === 'POST') {
      etat.declaration.fichierGenere = false;
      return route.fulfill({ status: 204, body: '' });
    }
    if (p === '/declarations-delai-paiement/1/depot' && method === 'POST') {
      etat.depots++;
      etat.declaration.estDeposee = true;
      return route.fulfill({ status: 204, body: '' });
    }

    return route.fulfill({ status: 404, json: { Message: 'Route non mockée : ' + method + ' ' + p } });
  });
}

function etatInitial(overrides: Partial<Etat> = {}): Etat {
  return {
    declaration: null, lignes: [], ifIceConforme: false,
    dateMiseEnRoute: '2023-07-01T00:00:00', reprises: [], clotures: 0, depots: 0,
    ...overrides,
  };
}

test('TASK-134 A — cycle complet : création → sélection → intégration → clôture → génération bloquée IF/ICE → correction → génération → dépôt', async ({ page }) => {
  const etat = etatInitial();
  await installMock(page, etat);

  await page.goto('/task134.html');
  await expect(page.getByText('Aucune déclaration délai de paiement pour cette société.')).toBeVisible();

  // ── 1. Création : exercice + type + trimestre, AUCUNE date saisie, aucun numéro saisi.
  await page.getByRole('button', { name: /Nouvelle déclaration/ }).click();
  await expect(page.getByRole('heading', { name: /Nouvelle déclaration délai de paiement/ })).toBeVisible();
  // Aucun champ de date libre dans le formulaire de création (critère de validation de la TASK).
  await expect(page.locator('.btn:visible ~ * input[type="date"]')).toHaveCount(0);
  const modale = page.locator('form');
  await expect(modale.locator('input[type="date"]')).toHaveCount(0);

  // Le type par défaut vient de SO_TypeDecDP (mock : Annuelle) → on choisit explicitement T1.
  await modale.locator('select').nth(1).selectOption('trimestrielle');
  await modale.locator('select').nth(2).selectOption('1');
  await page.getByRole('button', { name: 'Créer' }).click();

  await expect(page.getByText('DDP26070001')).toBeVisible();
  await expect(page.getByText('T1 2026')).toBeVisible();

  // ── 2. Ouverture de la fiche.
  await page.getByRole('button', { name: 'DDP26070001' }).click();
  await expect(page.getByRole('heading', { name: /Déclaration DDP26070001/ })).toBeVisible();
  await expect(page.getByText(/Aucune ligne intégrée/)).toBeVisible();

  // ── 3. Popup de sélection : la PÉRIODE est celle de la déclaration, affichée et NON saisissable.
  await page.getByRole('button', { name: /Sélectionner des lignes hors délai/ }).first().click();
  await expect(page.getByRole('heading', { name: /Lignes hors délai — DDP26070001/ })).toBeVisible();
  await expect(page.getByTestId('periode-selection')).toHaveText('01/01/2026 → 31/03/2026');
  await expect(page.getByText('non modifiable')).toBeVisible();
  // Critère STRUCTURANT : aucun champ de date libre dans la popup.
  const popup = page.locator('div').filter({ has: page.getByTestId('periode-selection') });
  await expect(popup.locator('input[type="date"]')).toHaveCount(0);

  // La ligne bloquée est visible, badgée « retard inconnu », et N'A PAS de case à cocher.
  await expect(page.getByText('Antérieures à la mise en route — retard réel inconnu (non intégrables)')).toBeVisible();
  await expect(page.getByText('retard inconnu')).toBeVisible();
  await expect(page.getByTestId('ligne-selection-17001|').locator('input[type="checkbox"]')).toHaveCount(0);

  // ── 4. Intégration manuelle multi-sélection (2 candidates).
  await page.getByRole('button', { name: 'Tout cocher' }).click();
  await expect(page.getByTestId('nb-cochees')).toHaveText('2');
  await page.getByRole('button', { name: /^Intégrer/ }).click();

  await expect(page.getByRole('heading', { name: /Lignes hors délai/ })).toHaveCount(0);
  await expect(page.getByText('FA250015')).toBeVisible();
  await expect(page.getByText('FA260007')).toBeVisible();
  await expect(page.getByText('349')).toBeVisible();

  await page.screenshot({ path: '../VERIFY/task134-A1-fiche-lignes-integrees.png', fullPage: true });

  // ── 5. Clôture (autorisée seulement parce qu'il y a des lignes — garde serveur).
  await page.getByRole('button', { name: 'Clôturer' }).click();
  await expect(page.getByText('Clôturée')).toBeVisible();
  expect(etat.clotures).toBe(1);

  // ── 6. Génération BLOQUÉE par IF/ICE : message serveur TEL QUEL + fournisseurs fautifs listés.
  await page.getByRole('button', { name: /Générer le fichier/ }).click();
  await expect(page.getByText(MESSAGE_BLOQUANT)).toBeVisible();
  await expect(page.getByText(/fournisseur\(s\) à corriger dans l'ERP/)).toBeVisible();
  await expect(page.getByText('[4411INWI]')).toBeVisible();
  await expect(page.getByText('[FOUBREL]')).toBeVisible();
  await expect(page.getByText('identifiant fiscal absent ; ICE absent').first()).toBeVisible();
  // Le fichier n'est PAS marqué généré : le bouton de dépôt reste absent.
  await expect(page.getByRole('button', { name: /Marquer déposée/ })).toHaveCount(0);

  await page.screenshot({ path: '../VERIFY/task134-A2-generation-bloquee-ifice.png', fullPage: true });

  // ── 7. Correction des IF/ICE dans l'ERP (simulée côté mock) puis re-contrôle informatif.
  etat.ifIceConforme = true;
  await page.getByRole('button', { name: /Contrôler IF\/ICE/ }).click();
  await expect(page.getByText(/Contrôle IF\/ICE conforme/)).toBeVisible();

  // ── 8. Génération réussie.
  await page.getByRole('button', { name: /Générer le fichier/ }).click();
  await expect(page.getByText('Fichier généré')).toBeVisible();

  // ── 9. Dépôt (flag manuel — aucun appel à une plateforme externe).
  await page.getByRole('button', { name: /Marquer déposée/ }).click();
  await expect(page.getByText('Déposée').first()).toBeVisible();
  expect(etat.depots).toBe(1);
  // Après dépôt, plus aucune action de cycle de vie n'est proposée (gardes serveur).
  await expect(page.getByRole('button', { name: 'Clôturer' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Déclôturer' })).toHaveCount(0);
  await expect(page.getByRole('button', { name: /Sélectionner des lignes hors délai/ })).toHaveCount(0);

  await page.screenshot({ path: '../VERIFY/task134-A3-deposee.png', fullPage: true });
});

test('TASK-134 B — écran de contrôle : période RAISONNÉE (aucune date libre), badge + saisie manuelle de reprise', async ({ page }) => {
  const etat = etatInitial();
  const requetesControle: string[] = [];
  await installMock(page, etat);
  page.on('request', r => {
    if (r.url().includes('/declarations-delai-paiement/controle')) requetesControle.push(r.url());
  });

  await page.goto('/task134.html');
  await page.getByRole('button', { name: 'Contrôle lignes hors délai' }).click();

  await expect(page.getByRole('heading', { name: /Contrôle des lignes hors délai/ })).toBeVisible();

  // Critère STRUCTURANT : AUCUN champ de date libre nulle part sur cet écran (le legacy en avait deux).
  await expect(page.locator('input[type="date"]')).toHaveCount(0);
  await expect(page.locator('input[type="datetime-local"]')).toHaveCount(0);

  // Le type par défaut vient de SO_TypeDecDP (mock : Annuelle) ⇒ bornes annuelles calculées serveur.
  await expect(page.getByTestId('periode-calculee')).toHaveText('01/01/2026 → 31/12/2026');

  // Bascule en trimestriel T2 : les bornes viennent du SERVEUR, pas d'une saisie.
  await page.locator('select').nth(1).selectOption('trimestrielle');
  await page.locator('select[aria-label="Trimestre"]').selectOption('2');
  await expect(page.getByTestId('periode-calculee')).toHaveText('01/04/2026 → 30/06/2026');

  // Aucune requête n'a jamais transporté de borne de date : seulement exercice/type/trimestre.
  expect(requetesControle.length).toBeGreaterThan(0);
  for (const u of requetesControle) {
    expect(u).not.toMatch(/dateDebut|dateFin/i);
    expect(u).toMatch(/exercice=2026/);
  }

  // Badge explicite + action de saisie manuelle sur la ligne antérieure à la mise en route ;
  // le dépassement n'est JAMAIS calculé pour cette ligne.
  await expect(page.getByTestId('badge-reprise-manuelle')).toBeVisible();
  await expect(page.getByText('inconnu')).toBeVisible();
  await expect(page.getByText('FA230001')).toBeVisible();

  // Cet écran ne permet AUCUNE intégration (rôle strictement séparé de la popup de sélection).
  await expect(page.getByRole('button', { name: /Intégrer/ })).toHaveCount(0);
  await expect(page.locator('input[type="checkbox"]')).toHaveCount(0);

  await page.screenshot({ path: '../VERIFY/task134-B1-controle-periode-raisonnee.png', fullPage: true });

  // Saisie de la reprise manuelle → la ligne quitte le statut bloqué (comportement serveur TASK-131).
  await page.getByRole('button', { name: /Saisie manuelle/ }).click();
  await expect(page.getByRole('heading', { name: /Reprise manuelle — facture FA230001/ })).toBeVisible();
  await page.locator('input[type="date"]').fill('2025-12-31');
  await page.getByRole('button', { name: /Enregistrer la reprise/ }).click();

  await expect(page.getByRole('heading', { name: /Reprise manuelle/ })).toHaveCount(0);
  expect(etat.reprises).toEqual([{ ecId: 17001, date: '2025-12-31' }]);
  await expect(page.getByTestId('badge-reprise-manuelle')).toHaveCount(0);

  await page.screenshot({ path: '../VERIFY/task134-B2-reprise-manuelle-saisie.png', fullPage: true });
});

test('TASK-134 C — société sans date de mise en route : alerte explicite + accès direct à la saisie', async ({ page }) => {
  // Cas réel constaté en base (VERIFY TASK-131 §9 n°3) : sans date de mise en route, 0 candidate et
  // toutes les lignes en « reprise manuelle requise ». L'écran doit l'EXPLIQUER, pas apparaître vide.
  const etat = etatInitial({ dateMiseEnRoute: null });
  await installMock(page, etat);

  await page.goto('/task134.html');
  await page.getByRole('button', { name: 'Contrôle lignes hors délai' }).click();

  await expect(page.getByTestId('alerte-mise-en-route')).toBeVisible();
  await page.getByRole('button', { name: /Saisir la date/ }).click();
  await expect(page.getByRole('heading', { name: /Date de mise en route/ })).toBeVisible();
  await expect(page.getByText('non configurée')).toBeVisible();

  await page.locator('input[type="date"]').fill('2023-07-01');
  await page.getByRole('button', { name: 'Enregistrer' }).click();

  await expect(page.getByRole('heading', { name: /Date de mise en route — Délai/ })).toHaveCount(0);
  expect(etat.dateMiseEnRoute).toBe('2023-07-01');
  await expect(page.getByTestId('alerte-mise-en-route')).toHaveCount(0);

  await page.screenshot({ path: '../VERIFY/task134-C-mise-en-route.png', fullPage: true });
});
