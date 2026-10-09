import * as XLSX from 'xlsx';
import type { GridApi } from 'ag-grid-community';
import {
  filterAndDeduplicateColumns,
  formatCellValueForExcel,
  computeColumnWidths,
  type ColumnExportInfo,
} from './gridExportValues.ts';

export function exportGridToExcel(gridApi: GridApi, fileName: string = 'export.xlsx') {
  if (!gridApi) return;

  const colState = gridApi.getColumnState();
  const rawColumns: ColumnExportInfo[] = [];

  colState.forEach((cs) => {
    const colDef = gridApi.getColumnDef(cs.colId);
    rawColumns.push({
      colId: cs.colId,
      hide: !!cs.hide,
      headerName: colDef?.headerName,
      field: colDef?.field,
      valueGetter: colDef?.valueGetter,
    });
  });

  const columns = filterAndDeduplicateColumns(rawColumns);

  const aoa: any[][] = [];
  aoa.push(columns.map((col) => col.headerName));

  gridApi.forEachNodeAfterFilterAndSort((node) => {
    if (node.data) {
      const row = columns.map((col) => {
        const val = gridApi.getCellValue({ rowNode: node, colKey: col.colId });
        return formatCellValueForExcel(val);
      });
      aoa.push(row);
    }
  });

  const worksheet = XLSX.utils.aoa_to_sheet(aoa);
  worksheet['!cols'] = computeColumnWidths(aoa);
  const workbook = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(workbook, worksheet, 'Données');
  const safeName = fileName.endsWith('.xlsx') ? fileName : `${fileName}.xlsx`;
  XLSX.writeFile(workbook, safeName);
}
