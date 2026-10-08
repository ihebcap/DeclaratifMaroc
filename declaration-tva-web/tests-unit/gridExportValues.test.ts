// TASK-223 : tests unitaires purs (sans DOM, sans ag-grid, sans xlsx)
// de formatCellValueForExcel et filterAndDeduplicateColumns.
// Exécution : node --test tests-unit/**/*.test.ts
import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
  formatCellValueForExcel,
  computeExcelDateSerial,
  filterAndDeduplicateColumns,
  isValidDate,
  isLeapYear,
  type ExcelDateCell,
  type ColumnExportInfo,
} from '../src/grid/gridExportValues.ts';

test('calcul du numéro de série Excel : exactitude et absence de partie décimale', () => {
  const serie1 = computeExcelDateSerial(2025, 4, 23);
  assert.equal(serie1, 45770, '23/04/2025 doit donner la série 45770');
  assert.equal(Number.isInteger(serie1), true, 'Le numéro de série doit être un entier');
  assert.equal(serie1 % 1, 0, 'Aucune partie décimale');

  const serie2 = computeExcelDateSerial(2025, 12, 31);
  assert.equal(serie2, 46022, '31/12/2025 doit donner la série 46022');
  assert.equal(Number.isInteger(serie2), true, 'Le numéro de série doit être un entier');
});

test('validation des dates limites et bissextiles', () => {
  assert.equal(isLeapYear(2024), true);
  assert.equal(isLeapYear(2025), false);
  assert.equal(isLeapYear(1900), false);
  assert.equal(isLeapYear(2000), true);

  assert.equal(isValidDate(2025, 4, 23), true);
  assert.equal(isValidDate(2025, 2, 28), true);
  assert.equal(isValidDate(2024, 2, 29), true);
  assert.equal(isValidDate(2025, 2, 29), false, '29/02/2025 impossible');
  assert.equal(isValidDate(2025, 2, 31), false, '31/02/2025 impossible');
  assert.equal(isValidDate(1850, 1, 1), false, 'Année 1850 hors 1900-2999');
  assert.equal(isValidDate(3000, 1, 1), false, 'Année 3000 hors 1900-2999');
});

test('conversion des chaînes ISO en cellule date Excel jj/mm/aaaa', () => {
  // ISO standard
  const res1 = formatCellValueForExcel('2025-04-23T00:00:00') as ExcelDateCell;
  assert.equal(typeof res1, 'object');
  assert.equal(res1.t, 'n');
  assert.equal(res1.v, 45770);
  assert.equal(res1.z, 'dd/mm/yyyy');

  // ISO avec heure non nulle : l'heure doit être ignorée et le jour conservé sans décalage
  const res2 = formatCellValueForExcel('2025-12-31T23:30:00') as ExcelDateCell;
  assert.equal(typeof res2, 'object');
  assert.equal(res2.t, 'n');
  assert.equal(res2.v, 46022, '31/12/2025 doit rester 31/12/2025, jamais 01/01/2026');
  assert.equal(res2.z, 'dd/mm/yyyy');

  // ISO avec suffixe Z ou décalage horaire (10 premiers caractères lus)
  const resZ = formatCellValueForExcel('2025-04-23T00:00:00Z') as ExcelDateCell;
  assert.equal(resZ.v, 45770);
  const resOffset = formatCellValueForExcel('2025-04-23T00:00:00+01:00') as ExcelDateCell;
  assert.equal(resOffset.v, 45770);

  // Date courte YYYY-MM-DD
  const resShort = formatCellValueForExcel('2025-04-23') as ExcelDateCell;
  assert.equal(resShort.v, 45770);
});

test('conversion des chaînes jj/mm/aaaa en cellule date Excel', () => {
  const res = formatCellValueForExcel('23/04/2025') as ExcelDateCell;
  assert.equal(typeof res, 'object');
  assert.equal(res.t, 'n');
  assert.equal(res.v, 45770);
  assert.equal(res.z, 'dd/mm/yyyy');
});

test('dates invalides ou hors limites restent du texte inchangé', () => {
  assert.equal(formatCellValueForExcel('31/02/2025'), '31/02/2025');
  assert.equal(formatCellValueForExcel('1850-01-01'), '1850-01-01');
});

test('textes non date ou contenant une plage de dates restent inchangés', () => {
  assert.equal(formatCellValueForExcel('F001 · ACME'), 'F001 · ACME');
  assert.equal(formatCellValueForExcel('FA2025-123'), 'FA2025-123');
  assert.equal(formatCellValueForExcel('01/01/2025 → 31/03/2025'), '01/01/2025 → 31/03/2025');
  assert.equal(formatCellValueForExcel('2025-04'), '2025-04');
  assert.equal(formatCellValueForExcel('ABC-2025-01-01'), 'ABC-2025-01-01');
});

test('nombres, booléens et vides', () => {
  assert.equal(formatCellValueForExcel(273), 273);
  assert.equal(formatCellValueForExcel(0), 0);
  assert.equal(formatCellValueForExcel(NaN), '');
  assert.equal(formatCellValueForExcel(true), true);
  assert.equal(formatCellValueForExcel(false), false);
  assert.equal(formatCellValueForExcel(null), '');
  assert.equal(formatCellValueForExcel(undefined), '');
  assert.equal(formatCellValueForExcel(''), '');
});

test('objets arbitraires deviennent cellule vide (jamais [object Object])', () => {
  assert.equal(formatCellValueForExcel({}), '');
  assert.equal(formatCellValueForExcel({ tiersCode: 'F001' }), '');
  assert.equal(formatCellValueForExcel([1, 2, 3]), '');
});

test('objet Date natif', () => {
  const d = new Date(2025, 3, 23); // 23 avril 2025 en local
  const res = formatCellValueForExcel(d) as ExcelDateCell;
  assert.equal(res.t, 'n');
  assert.equal(res.v, 45770);
  assert.equal(res.z, 'dd/mm/yyyy');

  const invalidDate = new Date('invalid');
  assert.equal(formatCellValueForExcel(invalidDate), '');
});

test('sélection des colonnes : exclut les masquées et les colonnes d actions sans valeur', () => {
  const columns: ColumnExportInfo[] = [
    { colId: 'actions', headerName: 'Actions', hide: false },
    { colId: 'select', hide: false },
    { colId: 'hiddenField', headerName: 'Caché', field: 'secret', hide: true },
    { colId: 'tiers', headerName: 'Fournisseur', valueGetter: () => 'val' },
    { colId: 'facture', headerName: 'Facture', field: 'doNumero' },
  ];

  const result = filterAndDeduplicateColumns(columns);
  assert.equal(result.length, 2);
  assert.equal(result[0].colId, 'tiers');
  assert.equal(result[0].headerName, 'Fournisseur');
  assert.equal(result[1].colId, 'facture');
  assert.equal(result[1].headerName, 'Facture');
});

test('dédoublonnage des en-têtes identiques avec suffixe (2), (3)', () => {
  const columns: ColumnExportInfo[] = [
    { colId: 'statut1', headerName: 'Statut', field: 's1' },
    { colId: 'statut2', headerName: 'Statut', field: 's2' },
    { colId: 'statut3', headerName: 'Statut', field: 's3' },
    { colId: 'autre', headerName: 'Autre', field: 'a' },
  ];

  const result = filterAndDeduplicateColumns(columns);
  assert.equal(result.length, 4);
  assert.equal(result[0].headerName, 'Statut');
  assert.equal(result[1].headerName, 'Statut (2)');
  assert.equal(result[2].headerName, 'Statut (3)');
  assert.equal(result[3].headerName, 'Autre');
});

test('repli sur colId si headerName est absent ou vide', () => {
  const columns: ColumnExportInfo[] = [
    { colId: 'customId', field: 'val' },
  ];
  const result = filterAndDeduplicateColumns(columns);
  assert.equal(result.length, 1);
  assert.equal(result[0].headerName, 'customId');
});

test('simulation d export Contrôle DDP avec lecture XLSX réelle', async () => {
  const XLSX = await import('xlsx');

  // Définition exacte des colonnes de Contrôle DDP
  const rawColumns: (ColumnExportInfo & { getter: (row: any) => any })[] = [
    { colId: 'statut', headerName: 'Statut', field: 'statut', valueGetter: true, getter: (r) => r ? 'Retard calculé' : '' },
    { colId: 'tiers', headerName: 'Fournisseur', field: 'tiers', valueGetter: true, getter: (r) => `${r.tiersCode} · ${r.tiersIntitule}` },
    { colId: 'facture', headerName: 'Facture', field: 'facture', valueGetter: true, getter: (r) => r.doNumero },
    { colId: 'doDate', headerName: 'Date facture', field: 'doDate', valueGetter: true, getter: (r) => r.doDate },
    { colId: 'echeanceLegale', headerName: 'Échéance légale', field: 'echeanceLegale', valueGetter: true, getter: (r) => r.echeanceLegale },
    { colId: 'borneReference', headerName: 'Dernière déclaration', field: 'borneReference', valueGetter: true, getter: (r) => r.borneReference || '' },
    { colId: 'borneActuelle', headerName: 'Constaté le', field: 'borneActuelle', valueGetter: true, getter: (r) => r.borneActuelle },
    { colId: 'depassement', headerName: 'Dépassement (j)', field: 'depassement', valueGetter: true, getter: (r) => r.depassement },
    { colId: 'montant', headerName: 'Montant', field: 'montant', valueGetter: true, getter: (r) => '1 234,00 MAD' },
  ];

  const exportCols = filterAndDeduplicateColumns(rawColumns);
  const data = [
    {
      tiersCode: 'F001',
      tiersIntitule: 'ACME Corp',
      doNumero: 'FA2025-123',
      doDate: '23/04/2025', // format renvoyé par formatDate dans valueGetter
      echeanceLegale: '2025-04-23T00:00:00', // ISO
      borneReference: null, // vide
      borneActuelle: '2025-12-31T23:30:00', // ISO avec heure
      depassement: 273,
    },
  ];

  const aoa: any[][] = [];
  aoa.push(exportCols.map(c => c.headerName));
  data.forEach((row) => {
    aoa.push(exportCols.map((col) => {
      const colDef = rawColumns.find(c => c.colId === col.colId)!;
      const val = colDef.getter(row);
      return formatCellValueForExcel(val);
    }));
  });

  const ws = XLSX.utils.aoa_to_sheet(aoa);
  const wb = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(wb, ws, 'Données');

  const buf = XLSX.write(wb, { type: 'buffer', bookType: 'xlsx' });
  const readWb = XLSX.read(buf, { type: 'buffer', cellNF: true });
  const readWs = readWb.Sheets['Données'];

  // Ligne 1 : en-têtes
  assert.equal(readWs['A1'].v, 'Statut');
  assert.equal(readWs['B1'].v, 'Fournisseur');
  assert.equal(readWs['C1'].v, 'Facture');
  assert.equal(readWs['D1'].v, 'Date facture');
  assert.equal(readWs['E1'].v, 'Échéance légale');
  assert.equal(readWs['F1'].v, 'Dernière déclaration');
  assert.equal(readWs['G1'].v, 'Constaté le');
  assert.equal(readWs['H1'].v, 'Dépassement (j)');
  assert.equal(readWs['I1'].v, 'Montant');

  // Ligne 2 : valeurs
  assert.equal(readWs['A2'].v, 'Retard calculé');
  assert.equal(readWs['B2'].v, 'F001 · ACME Corp');
  assert.equal(readWs['C2'].v, 'FA2025-123');

  // Dates = vraies cellules dates (t: 'n', format dd/mm/yyyy, série sans heure)
  assert.equal(readWs['D2'].t, 'n');
  assert.equal(readWs['D2'].v, 45770);
  assert.equal(readWs['D2'].z, 'dd/mm/yyyy');
  assert.equal(readWs['D2'].w, '23/04/2025');

  assert.equal(readWs['E2'].t, 'n');
  assert.equal(readWs['E2'].v, 45770);
  assert.equal(readWs['E2'].z, 'dd/mm/yyyy');
  assert.equal(readWs['E2'].w, '23/04/2025');

  // borneReference vide
  assert.equal(readWs['F2']?.v ?? '', '');

  // borneActuelle (31/12/2025)
  assert.equal(readWs['G2'].t, 'n');
  assert.equal(readWs['G2'].v, 46022);
  assert.equal(readWs['G2'].z, 'dd/mm/yyyy');
  assert.equal(readWs['G2'].w, '31/12/2025');

  // depassement numérique
  assert.equal(readWs['H2'].t, 'n');
  assert.equal(readWs['H2'].v, 273);

  // montant texte formaté
  assert.equal(readWs['I2'].t, 's');
  assert.equal(readWs['I2'].v, '1 234,00 MAD');
});

test('non-régression : grille à colonnes simples (Déclarations TVA)', async () => {
  const XLSX = await import('xlsx');
  const cols: ColumnExportInfo[] = [
    { colId: 'numero', headerName: 'N° Déclaration', field: 'numero' },
    { colId: 'periode', headerName: 'Période', field: 'periode' },
    { colId: 'dateCreation', headerName: 'Date création', field: 'dateCreation' },
  ];
  const exportCols = filterAndDeduplicateColumns(cols);
  const rows = [
    { numero: 'DEC-2025-01', periode: '2025-01', dateCreation: '2025-02-15T10:00:00' },
  ];
  const aoa: any[][] = [exportCols.map(c => c.headerName)];
  rows.forEach(r => {
    aoa.push([
      formatCellValueForExcel(r.numero),
      formatCellValueForExcel(r.periode),
      formatCellValueForExcel(r.dateCreation),
    ]);
  });
  const ws = XLSX.utils.aoa_to_sheet(aoa);
  const wb = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(wb, ws, 'Données');
  const buf = XLSX.write(wb, { type: 'buffer', bookType: 'xlsx' });
  const readWs = XLSX.read(buf, { type: 'buffer', cellNF: true }).Sheets['Données'];

  assert.equal(readWs['A2'].v, 'DEC-2025-01');
  assert.equal(readWs['B2'].v, '2025-01'); // texte non date préservé
  assert.equal(readWs['C2'].t, 'n'); // date ISO transformée en vraie date Excel
  assert.equal(readWs['C2'].z, 'dd/mm/yyyy');
});

test('non-régression : grille mixte avec booléen et plage (Conventions DDP)', async () => {
  const XLSX = await import('xlsx');
  const cols: ColumnExportInfo[] = [
    { colId: 'type', headerName: 'Type', field: 'type' },
    { colId: 'periode', headerName: 'Dates / N° facture', valueGetter: true },
    { colId: 'valide', headerName: 'Validée', field: 'valide' },
  ];
  const exportCols = filterAndDeduplicateColumns(cols);
  const aoa: any[][] = [
    exportCols.map(c => c.headerName),
    [
      formatCellValueForExcel('Période'),
      formatCellValueForExcel('01/01/2025 → 31/03/2025'),
      formatCellValueForExcel(true),
    ],
  ];
  const ws = XLSX.utils.aoa_to_sheet(aoa);
  const wb = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(wb, ws, 'Données');
  const buf = XLSX.write(wb, { type: 'buffer', bookType: 'xlsx' });
  const readWs = XLSX.read(buf, { type: 'buffer', cellNF: true }).Sheets['Données'];

  assert.equal(readWs['A2'].v, 'Période');
  assert.equal(readWs['B2'].v, '01/01/2025 → 31/03/2025'); // plage conservée en texte
  assert.equal(readWs['C2'].v, true); // booléen conservé
  assert.equal(readWs['C2'].t, 'b');
});

