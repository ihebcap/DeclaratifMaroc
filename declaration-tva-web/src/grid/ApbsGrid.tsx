import { useState, useCallback } from 'react';
import { AgGridReact, type AgGridReactProps } from 'ag-grid-react';
import type { ColDef, GridApi, GridReadyEvent } from 'ag-grid-community';
import './agGridSetup';
import { exportGridToExcel } from './gridExport';
import { Settings } from 'lucide-react';

export interface ApbsGridProps<TData = any> extends AgGridReactProps<TData> {
  height?: string | number;
  showColumnSelector?: boolean;
  showExportButton?: boolean;
  exportFileName?: string;
  onGridReadyCustom?: (api: GridApi) => void;
}

export function ApbsGrid<TData = any>({
  height = '500px',
  showColumnSelector = true,
  showExportButton = false,
  exportFileName = 'export.xlsx',
  columnDefs,
  defaultColDef,
  onGridReady,
  onGridReadyCustom,
  className,
  ...restProps
}: ApbsGridProps<TData>) {
  const [gridApi, setGridApi] = useState<GridApi | null>(null);
  const [showColMenu, setShowColMenu] = useState(false);
  const [columnsState, setColumnsState] = useState<{ id: string; name: string; hide: boolean }[]>([]);

  const handleGridReady = useCallback(
    (params: GridReadyEvent<TData>) => {
      setGridApi(params.api);
      if (onGridReady) onGridReady(params);
      if (onGridReadyCustom) onGridReadyCustom(params.api);

      const state = params.api.getColumnState();
      const list = state.map((cs) => {
        const def = params.api.getColumnDef(cs.colId);
        return {
          id: cs.colId,
          name: (def?.headerName as string) || cs.colId,
          hide: !!cs.hide,
        };
      });
      setColumnsState(list);
    },
    [onGridReady, onGridReadyCustom]
  );

  const toggleColumnHide = (colId: string) => {
    if (!gridApi) return;
    const current = columnsState.find((c) => c.id === colId);
    const nextHide = !current?.hide;
    gridApi.setColumnsVisible([colId], !nextHide);
    setColumnsState((prev) =>
      prev.map((c) => (c.id === colId ? { ...c, hide: nextHide } : c))
    );
  };

  const handleExport = () => {
    if (gridApi) {
      exportGridToExcel(gridApi, exportFileName);
    }
  };

  const mergedDefaultColDef: ColDef = {
    sortable: true,
    resizable: true,
    filter: 'agTextColumnFilter',
    wrapHeaderText: true,
    autoHeaderHeight: true,
    ...defaultColDef,
  };

  return (
    <div style={{ display: 'flex', flexDirection: 'column', width: '100%' }}>
      {(showColumnSelector || showExportButton) && (
        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '8px', marginBottom: '6px', position: 'relative' }}>
          {showExportButton && (
            <button
              type="button"
              onClick={handleExport}
              className="px-3 py-1 bg-green-600 hover:bg-green-700 text-white text-xs font-medium rounded shadow-sm transition-colors"
            >
              Export Excel
            </button>
          )}
          {showColumnSelector && (
            <div style={{ position: 'relative' }}>
              <button
                type="button"
                onClick={() => setShowColMenu(!showColMenu)}
                className="px-2.5 py-1 bg-gray-100 dark:bg-gray-800 hover:bg-gray-200 dark:hover:bg-gray-700 text-gray-700 dark:text-gray-200 text-xs font-medium rounded border border-gray-300 dark:border-gray-600 flex items-center gap-1.5 transition-colors"
                title="Choisir les colonnes affichées"
              >
                <Settings className="w-3.5 h-3.5" />
                <span>Colonnes</span>
              </button>
              {showColMenu && (
                <div
                  style={{
                    position: 'absolute',
                    right: 0,
                    top: '100%',
                    marginTop: '4px',
                    zIndex: 50,
                    backgroundColor: '#ffffff',
                    border: '1px solid #ccc',
                    borderRadius: '4px',
                    boxShadow: '0 4px 12px rgba(0,0,0,0.15)',
                    padding: '8px',
                    minWidth: '180px',
                    maxHeight: '250px',
                    overflowY: 'auto',
                  }}
                >
                  <div style={{ fontWeight: 600, fontSize: '12px', marginBottom: '6px', color: '#333' }}>
                    Colonnes affichées
                  </div>
                  {columnsState.map((col) => (
                    <label
                      key={col.id}
                      style={{
                        display: 'flex',
                        alignItems: 'center',
                        gap: '6px',
                        fontSize: '12px',
                        padding: '2px 0',
                        cursor: 'pointer',
                        color: '#444',
                      }}
                    >
                      <input
                        type="checkbox"
                        checked={!col.hide}
                        onChange={() => toggleColumnHide(col.id)}
                      />
                      <span>{col.name}</span>
                    </label>
                  ))}
                </div>
              )}
            </div>
          )}
        </div>
      )}
      <div className={`ag-theme-alpine ${className || ''}`} style={{ height, width: '100%' }}>
        <AgGridReact<TData>
          columnDefs={columnDefs}
          defaultColDef={mergedDefaultColDef}
          onGridReady={handleGridReady}
          {...restProps}
        />
      </div>
    </div>
  );
}
