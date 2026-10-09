// TASK-223 : fonctions pures de transformation de valeurs et de sélection/dédoublonnage
// de colonnes pour l'export Excel des grilles ApbsGrid.
// CE FICHIER EST PUR : aucun import à l'exécution de ag-grid, xlsx ou React.
// Seuls des `import type` sont autorisés pour compatibilité totale avec `node --test`.

export interface ExcelDateCell {
  t: 'n';
  v: number;
  z: string;
}

export interface ColumnExportInfo {
  colId: string;
  headerName?: string | null;
  field?: string | null;
  valueGetter?: unknown;
  hide?: boolean | null;
}

export interface ResolvedExportColumn {
  colId: string;
  headerName: string;
}

export const ISO_DATE_REGEX = /^\d{4}-\d{2}-\d{2}([T ]\d{2}:\d{2}(:\d{2}(\.\d+)?)?(Z|[+-]\d{2}:?\d{2})?)?$/;
export const JJ_MM_AAAA_REGEX = /^\d{2}\/\d{2}\/\d{4}$/;

export function isLeapYear(year: number): boolean {
  return (year % 4 === 0 && year % 100 !== 0) || year % 400 === 0;
}

export function isValidDate(year: number, month: number, day: number): boolean {
  if (year < 1900 || year > 2999) return false;
  if (month < 1 || month > 12) return false;
  if (day < 1 || day > 31) return false;
  const daysInMonths = [31, isLeapYear(year) ? 29 : 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];
  return day <= daysInMonths[month - 1];
}

/**
 * Calcule le numéro de série d'une date pour Excel :
 * Date.UTC(a, m-1, j) / 86400000 + 25569.
 * Sans composante heure (entier strict), insensible au fuseau local.
 */
export function computeExcelDateSerial(year: number, month: number, day: number): number {
  return Math.round(Date.UTC(year, month - 1, day) / 86400000 + 25569);
}

export function makeExcelDateCell(year: number, month: number, day: number): ExcelDateCell {
  return {
    t: 'n',
    v: computeExcelDateSerial(year, month, day),
    z: 'dd/mm/yyyy',
  };
}

/**
 * Convertit une valeur de cellule brute/affichée en valeur adaptée à Excel.
 * - Dates (Date, ISO, jj/mm/aaaa) -> cellule date Excel { t: 'n', v: série, z: 'dd/mm/yyyy' }
 * - Nombres -> nombre (NaN -> '')
 * - Booléens -> booléen
 * - Texte -> texte inchangé
 * - null / undefined / objet inconnu -> ''
 */
export function formatCellValueForExcel(val: unknown): string | number | boolean | ExcelDateCell {
  if (val === null || val === undefined) {
    return '';
  }

  if (typeof val === 'number') {
    return Number.isNaN(val) ? '' : val;
  }

  if (typeof val === 'boolean') {
    return val;
  }

  if (val instanceof Date) {
    if (isNaN(val.getTime())) {
      return '';
    }
    const a = val.getFullYear();
    const m = val.getMonth() + 1;
    const j = val.getDate();
    if (isValidDate(a, m, j)) {
      return makeExcelDateCell(a, m, j);
    }
    return '';
  }

  if (typeof val === 'string') {
    if (ISO_DATE_REGEX.test(val)) {
      const a = parseInt(val.slice(0, 4), 10);
      const m = parseInt(val.slice(5, 7), 10);
      const j = parseInt(val.slice(8, 10), 10);
      if (isValidDate(a, m, j)) {
        return makeExcelDateCell(a, m, j);
      }
      return val;
    }

    if (JJ_MM_AAAA_REGEX.test(val)) {
      const j = parseInt(val.slice(0, 2), 10);
      const m = parseInt(val.slice(3, 5), 10);
      const a = parseInt(val.slice(6, 10), 10);
      if (isValidDate(a, m, j)) {
        return makeExcelDateCell(a, m, j);
      }
      return val;
    }

    return val;
  }

  // Tout autre type (objet non Date, tableau, fonction, etc.) -> cellule vide
  return '';
}

export const convertCellValueForExcel = formatCellValueForExcel;

/**
 * Filtre les colonnes masquées ou sans valeur (ni field ni valueGetter),
 * et déduplique les en-têtes identiques en suffixant le 2e par ' (2)', etc.
 */
export function filterAndDeduplicateColumns(columns: ColumnExportInfo[]): ResolvedExportColumn[] {
  const result: ResolvedExportColumn[] = [];
  const headerCounts = new Map<string, number>();

  for (const col of columns) {
    if (col.hide) continue;

    const hasField = typeof col.field === 'string' && col.field.trim().length > 0;
    const hasValueGetter = Boolean(col.valueGetter);
    if (!hasField && !hasValueGetter) continue;

    const rawHeader = (col.headerName && col.headerName.trim().length > 0)
      ? col.headerName
      : col.colId;

    const count = (headerCounts.get(rawHeader) || 0) + 1;
    headerCounts.set(rawHeader, count);

    const headerName = count === 1 ? rawHeader : `${rawHeader} (${count})`;
    result.push({
      colId: col.colId,
      headerName,
    });
  }

  return result;
}

export interface ColumnWidth {
  wch: number;
}

export function isExcelDateCell(val: unknown): val is ExcelDateCell {
  return (
    typeof val === 'object' &&
    val !== null &&
    (val as any).t === 'n' &&
    typeof (val as any).v === 'number' &&
    (val as any).z === 'dd/mm/yyyy'
  );
}

/**
 * Calcule la largeur de chaque colonne pour l'export Excel (worksheet['!cols']).
 * - Basée sur la longueur maximale de l'en-tête et du contenu de la colonne
 * - Minimum 12 pour une colonne contenant des dates (évite l'affichage ###### dans Excel)
 * - Plafond strict à 60 caractères
 */
export function computeColumnWidths(aoa: any[][]): ColumnWidth[] {
  if (!aoa || aoa.length === 0) return [];

  const numCols = aoa[0]?.length || 0;
  const result: ColumnWidth[] = [];

  for (let c = 0; c < numCols; c++) {
    let maxLen = 0;
    let isDate = false;

    // Vérifie si l'en-tête suggère une colonne de date
    const headerStr = aoa[0]?.[c] != null ? String(aoa[0][c]) : '';
    if (headerStr && /date/i.test(headerStr)) {
      isDate = true;
    }

    for (let r = 0; r < aoa.length; r++) {
      const cell = aoa[r]?.[c];
      if (cell === null || cell === undefined || cell === '') continue;

      if (isExcelDateCell(cell)) {
        isDate = true;
        maxLen = Math.max(maxLen, 12);
      } else if (typeof cell === 'string') {
        if (r > 0 && (ISO_DATE_REGEX.test(cell) || JJ_MM_AAAA_REGEX.test(cell))) {
          isDate = true;
          maxLen = Math.max(maxLen, 12);
        } else {
          maxLen = Math.max(maxLen, cell.length);
        }
      } else if (typeof cell === 'number') {
        maxLen = Math.max(maxLen, String(cell).length);
      } else if (typeof cell === 'boolean') {
        maxLen = Math.max(maxLen, cell ? 4 : 5);
      } else {
        maxLen = Math.max(maxLen, String(cell).length);
      }
    }

    if (isDate) {
      maxLen = Math.max(maxLen, 12);
    }

    const wch = Math.min(Math.max(maxLen, isDate ? 12 : 0), 60);
    result.push({ wch });
  }

  return result;
}

