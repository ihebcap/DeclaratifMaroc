import { useState, useCallback, useRef, type ReactNode } from 'react';
import { AgGridReact, type AgGridReactProps } from 'ag-grid-react';
import type { ColDef, FirstDataRenderedEvent, GridApi, GridReadyEvent, GridSizeChangedEvent } from 'ag-grid-community';
import './agGridSetup';
import { exportGridToExcel } from './gridExport';
import { Settings } from 'lucide-react';

// Comble l'espace vide à droite (ex: grilles à peu de colonnes, comme la liste des déclarations)
// en élargissant uniquement la DERNIÈRE colonne non épinglée — jamais toutes les colonnes
// proportionnellement (`sizeColumnsToFit`), qui déforme de façon imprévisible les colonnes déjà
// dimensionnées à leur contenu (`autoSizeAllColumns`). Ne fait rien si les colonnes dépassent déjà
// la largeur visible (grilles larges, scroll horizontal prévu, ex: Rapprochement bancaire).
function fitColumnsIfRoomToSpare(api: GridApi, availableWidth: number) {
  if (availableWidth <= 0) return;
  const columns = api.getAllDisplayedColumns().filter((c) => !c.getPinned());
  if (columns.length === 0) return;
  const totalColumnsWidth = api.getAllDisplayedColumns().reduce((sum, c) => sum + c.getActualWidth(), 0);
  const slack = availableWidth - totalColumnsWidth;
  if (slack <= 0) return;
  const lastColumn = columns[columns.length - 1];
  api.setColumnWidths([{ key: lastColumn.getColId(), newWidth: lastColumn.getActualWidth() + slack }]);
}

export interface ApbsGridProps<TData = any> extends AgGridReactProps<TData> {
  height?: string | number;
  showColumnSelector?: boolean;
  showExportButton?: boolean;
  exportFileName?: string;
  storageKey?: string;
  onGridReadyCustom?: (api: GridApi) => void;
  /** Contenu (filtres, compteur...) affiché à gauche de la barre d'outils, sur la même ligne que Export/Colonnes. */
  toolbarLeft?: ReactNode;
}

export function ApbsGrid<TData = any>({
  height = '500px',
  showColumnSelector = true,
  showExportButton = false,
  exportFileName = 'export.xlsx',
  storageKey,
  columnDefs,
  defaultColDef,
  onGridReady,
  onGridReadyCustom,
  className,
  toolbarLeft,
  rowHeight,
  ...restProps
}: ApbsGridProps<TData>) {
  const [gridApi, setGridApi] = useState<GridApi | null>(null);
  const [showColMenu, setShowColMenu] = useState(false);
  const [columnsState, setColumnsState] = useState<{ id: string; name: string; hide: boolean }[]>([]);
  const hasAutoSized = useRef(false);
  const gridWrapperRef = useRef<HTMLDivElement>(null);

  // Persiste à la fois la visibilité ET l'ordre des colonnes — un simple tableau de colIds
  // visibles (ancien format) ne suffit pas : `applyColumnState` a besoin de l'ordre COMPLET
  // (colonnes masquées incluses) pour restaurer un glisser-déposer, sinon le drag utilisateur
  // ne survit pas au rechargement de la page.
  const savePrefs = useCallback(
    (api: GridApi) => {
      if (!storageKey) return;
      const currentState = api.getColumnState();
      const order = currentState.map((c) => c.colId);
      const hidden = currentState.filter((c) => c.hide).map((c) => c.colId);
      localStorage.setItem(storageKey, JSON.stringify({ order, hidden }));
    },
    [storageKey]
  );

  const handleGridReady = useCallback(
    (params: GridReadyEvent<TData>) => {
      setGridApi(params.api);
      if (onGridReady) onGridReady(params);
      if (onGridReadyCustom) onGridReadyCustom(params.api);

      if (storageKey) {
        const saved = localStorage.getItem(storageKey);
        if (saved) {
          try {
            const parsed = JSON.parse(saved);
            // Compat avec l'ancien format (tableau de colIds visibles, pas d'ordre sauvegardé).
            const order: string[] | null = Array.isArray(parsed) ? null : parsed.order ?? null;
            const hidden: string[] = Array.isArray(parsed) ? [] : parsed.hidden ?? [];
            const visibleIds: string[] | null = Array.isArray(parsed) ? parsed : null;

            const currentState = params.api.getColumnState();
            const byId = new Map(currentState.map((c) => [c.colId, c]));
            const orderedState = order
              ? [...order.map((id) => byId.get(id)).filter((c): c is (typeof currentState)[number] => !!c),
                 ...currentState.filter((c) => !order.includes(c.colId))]
              : currentState;
            const updatedState = orderedState.map((c) => ({
              ...c,
              hide: order ? hidden.includes(c.colId) : !(visibleIds ?? []).includes(c.colId),
            }));
            params.api.applyColumnState({ state: updatedState, applyOrder: true });
          } catch (e) {
            console.warn('Invalid column prefs in localStorage for', storageKey);
          }
        }
      }

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
    [onGridReady, onGridReadyCustom, storageKey]
  );

  // Auto-ajuste les colonnes à leur contenu (en-tête inclus) une seule fois, dès que des lignes
  // sont réellement rendues — évite par ex. un en-tête « Point » coupé en deux lignes (« Poin »/
  // « t ») faute de largeur suffisante. Une seule passe : au-delà, l'utilisateur peut redimensionner
  // lui-même sans que la grille ne lui reprenne la main à chaque rafraîchissement de données.
  // Mesure la largeur directement dans le DOM (fiable) plutôt que via l'API AG Grid, qui peut
  // renvoyer une plage encore non stabilisée à ce stade du cycle de rendu.
  const handleFirstDataRendered = useCallback((params: FirstDataRenderedEvent<TData>) => {
    if (hasAutoSized.current) return;
    hasAutoSized.current = true;
    params.api.autoSizeAllColumns();
    fitColumnsIfRoomToSpare(params.api, gridWrapperRef.current?.clientWidth ?? 0);
  }, []);

  const handleGridSizeChanged = useCallback((params: GridSizeChangedEvent<TData>) => {
    fitColumnsIfRoomToSpare(params.api, params.clientWidth);
  }, []);

  // Persiste le glisser-déposer d'en-tête (réordonnancement manuel des colonnes) — `finished`
  // évite d'écrire à chaque pixel de déplacement, seulement au relâchement.
  const handleColumnMoved = useCallback(
    (params: { finished: boolean; api: GridApi }) => {
      if (!params.finished) return;
      savePrefs(params.api);
    },
    [savePrefs]
  );

  const toggleColumnHide = (colId: string) => {
    if (!gridApi) return;
    const current = columnsState.find((c) => c.id === colId);
    const nextHide = !current?.hide;
    gridApi.setColumnsVisible([colId], !nextHide);
    setColumnsState((prev) =>
      prev.map((c) => (c.id === colId ? { ...c, hide: nextHide } : c))
    );
    savePrefs(gridApi);
  };

  const handleShowAllColumns = () => {
    if (!gridApi) return;
    const allIds = columnsState.map((c) => c.id);
    gridApi.setColumnsVisible(allIds, true);
    setColumnsState((prev) => prev.map((c) => ({ ...c, hide: false })));
    if (storageKey) {
      localStorage.setItem(storageKey, JSON.stringify(allIds));
    }
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

  const hasToolbar = !!toolbarLeft || showColumnSelector || showExportButton;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', width: '100%', height }}>
      {hasToolbar && (
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: '0.5rem', marginBottom: '4px', flexWrap: 'nowrap' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', fontSize: '0.8rem', minWidth: 0, flex: 1, overflowX: 'auto', flexWrap: 'nowrap' }}>
            {toolbarLeft}
          </div>
          <div style={{ display: 'flex', gap: '0.5rem', position: 'relative', flexShrink: 0 }}>
            {showExportButton && (
              <button type="button" onClick={handleExport} className="btn btn-primary" style={{ fontSize: '0.78rem', padding: '0.3rem 0.6rem' }}>
                Export Excel
              </button>
            )}
            {showColumnSelector && (
              <div style={{ position: 'relative' }}>
                <button
                  type="button"
                  onClick={() => setShowColMenu(!showColMenu)}
                  className="btn"
                  style={{ fontSize: '0.78rem', padding: '0.3rem 0.6rem', background: 'transparent', border: '1px solid var(--border-color)', color: 'var(--text-primary)', gap: '0.35rem' }}
                  title="Choisir les colonnes affichées"
                >
                  <Settings size={13} />
                  <span>Colonnes</span>
                </button>
                {showColMenu && (
                  <>
                    <div
                      style={{ position: 'fixed', inset: 0, zIndex: 40 }}
                      onClick={() => setShowColMenu(false)}
                    />
                    <div
                      style={{
                        position: 'absolute',
                        right: 0,
                        top: '100%',
                        marginTop: '4px',
                        zIndex: 50,
                        backgroundColor: 'var(--bg-secondary)',
                        border: '1px solid var(--border-color)',
                        borderRadius: 'var(--radius-md)',
                        boxShadow: 'var(--shadow-lg)',
                        padding: '8px',
                        minWidth: '180px',
                        maxHeight: '250px',
                        overflowY: 'auto',
                      }}
                    >
                      <div
                        style={{
                          display: 'flex',
                          justifyContent: 'space-between',
                          alignItems: 'center',
                          marginBottom: '6px',
                        }}
                      >
                        <span style={{ fontWeight: 600, fontSize: '12px', color: 'var(--text-primary)' }}>
                          Colonnes affichées
                        </span>
                        <button
                          type="button"
                          onClick={handleShowAllColumns}
                          style={{
                            background: 'none',
                            border: 'none',
                            color: 'var(--accent-primary)',
                            fontSize: '11px',
                            cursor: 'pointer',
                            padding: 0,
                            textDecoration: 'underline',
                          }}
                        >
                          Tout afficher
                        </button>
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
                            color: 'var(--text-primary)',
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
                  </>
                )}
              </div>
            )}
          </div>
        </div>
      )}
      <div ref={gridWrapperRef} className={`ag-theme-alpine ${className || ''}`} style={{ flexGrow: 1, minHeight: 0, width: '100%' }}>
        <AgGridReact<TData>
          theme="legacy"
          columnDefs={columnDefs}
          defaultColDef={mergedDefaultColDef}
          rowHeight={rowHeight ?? 28}
          onGridReady={handleGridReady}
          onGridSizeChanged={handleGridSizeChanged}
          onFirstDataRendered={handleFirstDataRendered}
          onColumnMoved={handleColumnMoved}
          {...restProps}
        />
      </div>
    </div>
  );
}
