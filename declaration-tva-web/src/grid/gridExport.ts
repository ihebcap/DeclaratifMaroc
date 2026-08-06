import * as XLSX from 'xlsx';
import type { GridApi } from 'ag-grid-community';

export function exportGridToExcel(gridApi: GridApi, fileName: string = 'export.xlsx') {
  if (!gridApi) return;

  const colState = gridApi.getColumnState();
  const columns: { field: string; headerName: string }[] = [];

  colState.forEach((cs) => {
    if (!cs.hide) {
      const colDef = gridApi.getColumnDef(cs.colId);
      if (colDef && colDef.field) {
        columns.push({
          field: colDef.field,
          headerName: colDef.headerName || cs.colId,
        });
      }
    }
  });

  const rows: Record<string, any>[] = [];
  gridApi.forEachNodeAfterFilterAndSort((node) => {
    if (node.data) {
      const rowObj: Record<string, any> = {};
      columns.forEach((col) => {
        const val = node.data[col.field];
        rowObj[col.headerName] = val !== undefined && val !== null ? val : '';
      });
      rows.push(rowObj);
    }
  });

  const worksheet = XLSX.utils.json_to_sheet(rows);
  const workbook = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(workbook, worksheet, 'Données');
  const safeName = fileName.endsWith('.xlsx') ? fileName : `${fileName}.xlsx`;
  XLSX.writeFile(workbook, safeName);
}
