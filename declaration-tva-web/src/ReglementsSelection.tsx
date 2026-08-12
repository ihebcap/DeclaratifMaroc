import { useState, useEffect, useRef, useCallback, useMemo } from 'react';
import { Loader2, CheckCircle2, AlertTriangle, XCircle, Play, RefreshCw } from 'lucide-react';
import type { ColDef, GridApi, GridReadyEvent } from 'ag-grid-community';
import { ApbsGrid } from './grid/ApbsGrid';
import { CustomListFilter } from './grid/CustomListFilter';
import { formatMoney, formatDate } from './utils';
import api from './api';

// ─── Écran ① Règlements (TASK-054 / TASK-204 AG Grid) ─────────────────────────

export type ReglementRow = {
  numeroReglement: string;
  date: string;
  mode: string;
  domaine: string;
  tiers: string;
  tiersCode: string;
  montant: number;
  rapprocheBanque: boolean;
  dateRapprochement: string | null;
  echeance: string | null;
  nbFacturesAffectees: number;
  montantAffecte: number;
  resteAAffecter: number;
  origine: string;
  declare: boolean;
};

export const reglementKey = (row: ReglementRow) => `${row.numeroReglement}__${row.date}__${row.montant}`;

type Statut = 'eligible' | 'controle' | 'bloque';

function statutDe(row?: ReglementRow): Statut {
  if (!row) return 'bloque';
  if (row.declare) return 'bloque';
  if (row.nbFacturesAffectees === 0) return 'bloque';
  if (row.origine && row.origine.startsWith('Autre (')) return 'bloque';
  if (Math.abs(row.resteAAffecter ?? 0) > 0.005) return 'controle';
  return 'eligible';
}

const STATUT_META: Record<Statut, { label: string; bg: string; text: string; icon: typeof CheckCircle2 }> = {
  eligible: { label: 'Éligible', bg: 'var(--status-ok-bg)', text: 'var(--status-ok-text)', icon: CheckCircle2 },
  controle: { label: 'À contrôler', bg: '#fef3c7', text: 'var(--status-warning-text-alt)', icon: AlertTriangle },
  bloque: { label: 'Bloqué', bg: 'var(--status-blocking-bg)', text: 'var(--status-blocking-text)', icon: XCircle },
};

function StatutBadge({ row }: { row: ReglementRow }) {
  const statut = statutDe(row);
  const m = STATUT_META[statut];
  const Icon = m.icon;
  let label = m.label;
  let title: string | undefined;
  if (statut === 'bloque') {
    if (row.origine === 'Autre (1)') {
      label = 'Impayé (Bloqué)';
    } else if (row.origine && row.origine.startsWith('Autre (')) {
      label = 'Hors périmètre (Bloqué)';
    } else if (row.nbFacturesAffectees === 0) {
      label = 'Non affecté (Bloqué)';
    } else if (row.declare) {
      label = 'Déjà déclaré (Bloqué)';
    }
  } else if (statut === 'controle') {
    title = `Reste à affecter : ${formatMoney(row.resteAAffecter ?? 0)}`;
  }
  return (
    <span title={title} style={{ background: m.bg, color: m.text, padding: '2px 8px', borderRadius: '99px', fontSize: '0.7rem', fontWeight: 600, display: 'inline-flex', alignItems: 'center', gap: '0.25rem' }}>
      <Icon size={11} />{label}
    </span>
  );
}

function affecteLabel(row?: ReglementRow): string {
  if (!row || row.nbFacturesAffectees === 0) return '—';
  if (Math.abs(row.resteAAffecter ?? 0) <= 0.005) {
    return `${row.nbFacturesAffectees} facture${row.nbFacturesAffectees > 1 ? 's' : ''}`;
  }
  const pct = row.montant > 0 ? Math.round((row.montantAffecte / row.montant) * 100) : 0;
  return `partiel ${pct} % (reste ${formatMoney(row.resteAAffecter ?? 0)})`;
}

function DomaineBadge({ domaine }: { domaine: string }) {
  const map: Record<string, { bg: string; text: string }> = {
    Encaissement: { bg: '#e0f2fe', text: '#0369a1' },
    Décaissement: { bg: '#f3e8ff', text: '#6b21a8' },
  };
  const c = map[domaine] || { bg: '#f3f4f6', text: '#374151' };
  return (
    <span style={{
      background: c.bg,
      color: c.text,
      padding: '2px 8px',
      borderRadius: '4px',
      fontSize: '0.75rem',
      fontWeight: 600
    }}>
      {domaine || '—'}
    </span>
  );
}

export function periodeBounds(exercice: number, type: number, periode: number): { debut: string; fin: string } {
  const pad = (n: number) => String(n).padStart(2, '0');
  const formatDateLocal = (d: Date) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
  if (type === 1) {
    const moisDebut = (periode - 1) * 3 + 1;
    const debut = `${exercice}-${pad(moisDebut)}-01`;
    const finMoisIdx = moisDebut + 2;
    const finDate = new Date(exercice, finMoisIdx, 0);
    return { debut, fin: formatDateLocal(finDate) };
  }
  const debut = `${exercice}-${pad(periode)}-01`;
  const finDate = new Date(exercice, periode, 0);
  return { debut, fin: formatDateLocal(finDate) };
}

export function ReglementsSelection({
  societeId,
  exercice,
  type,
  periode,
  selectedKeys,
  selectedRows,
  onSelectionChange,
  showToast,
  savedSelection = null,
  declarationId,
}: {
  societeId: number;
  exercice: number;
  type: number;
  periode: number;
  selectedKeys: Set<string>;
  selectedRows: ReglementRow[];
  onSelectionChange: (keys: Set<string>, rows: ReglementRow[]) => void;
  showToast: (m: string, t?: 'success' | 'error' | 'warning') => void;
  savedSelection?: string[] | null;
  declarationId: string;
}) {
  const { debut, fin } = useMemo(() => periodeBounds(exercice, type, periode), [exercice, type, periode]);

  const [allData, setAllData] = useState<ReglementRow[]>([]);
  const [loading, setLoading] = useState(false);
  const [hasFetched, setHasFetched] = useState(false);
  const [modeOptions, setModeOptions] = useState<{ label: string; value: string }[]>([]);
  const [gridApi, setGridApi] = useState<GridApi | null>(null);
  const [displayedCount, setDisplayedCount] = useState(0);

  const knownRowsRef = useRef<Map<string, ReglementRow>>(new Map());

  useEffect(() => {
    selectedRows.forEach(row => {
      const key = reglementKey(row);
      if (!knownRowsRef.current.has(key)) {
        knownRowsRef.current.set(key, row);
      }
    });
  }, [selectedRows]);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const res = await api.get('/rapprochement/distincts', { params: { debut, fin, soId: societeId } });
        if (cancelled) return;
        const modes = (res.data.modes || []).map((m: any) => ({ label: m.libelle, value: String(m.code) }));
        setModeOptions(modes);
      } catch {
        if (!cancelled) setModeOptions([]);
      }
    })();
    return () => { cancelled = true; };
  }, [debut, fin, societeId]);

  const fetchAll = useCallback(async (cancelled: { current: boolean }) => {
    setLoading(true);
    try {
      const params: any = {
        debut,
        fin,
        sort: 'date_desc',
        domaine: ['Décaissement', 'Encaissement'],
        declarationId,
      };

      const chunkSize = 500;
      let items: ReglementRow[] = [];
      let serverPage = 1;
      let totalCount = 0;
      for (;;) {
        const res = await api.get('/rapprochement', {
          params: { ...params, soId: societeId, page: serverPage, size: chunkSize },
          paramsSerializer: { indexes: null },
        });
        const chunk: ReglementRow[] = res.data.items || [];
        totalCount = res.data.totalCount ?? chunk.length;
        items = items.concat(chunk);
        if (chunk.length === 0 || items.length >= totalCount) break;
        serverPage += 1;
      }
      if (cancelled.current) return;
      const visibles = items.filter(r => !r.declare);
      setAllData(visibles);
      setDisplayedCount(visibles.length);
      visibles.forEach(r => knownRowsRef.current.set(reglementKey(r), r));
    } catch (e) {
      if (cancelled.current) return;
      console.error(e);
      showToast('Erreur lors du chargement des règlements', 'error');
      setAllData([]);
      setDisplayedCount(0);
    } finally {
      if (!cancelled.current) setLoading(false);
    }
  }, [debut, fin, showToast, societeId, declarationId]);

  const handleIntegrerClick = useCallback(() => {
    const cancelled = { current: false };
    fetchAll(cancelled);
    setHasFetched(true);
  }, [fetchAll]);

  // TASK-200 PO Decision:
  // Existing declaration with saved selection -> load automatically and restore selection.
  // New declaration or empty saved selection -> do NOT load automatically, wait for user to click "Intégrer".
  useEffect(() => {
    if (savedSelection && savedSelection.length > 0) {
      const cancelled = { current: false };
      fetchAll(cancelled);
      setHasFetched(true);
      return () => { cancelled.current = true; };
    } else {
      setHasFetched(false);
      setAllData([]);
    }
  }, [declarationId, savedSelection, fetchAll]);

  const initializedRef = useRef(false);

  useEffect(() => {
    initializedRef.current = false;
  }, [declarationId]);

  useEffect(() => {
    if (savedSelection === null || allData.length === 0 || loading) return;

    if (!initializedRef.current) {
      if (selectedKeys.size > 0) {
        initializedRef.current = true;
        return;
      }

      if (savedSelection && savedSelection.length > 0) {
        const keys = new Set<string>();
        const rows: ReglementRow[] = [];
        allData.forEach(r => {
          if (savedSelection.includes(r.numeroReglement)) {
            const key = reglementKey(r);
            keys.add(key);
            rows.push(r);
          }
        });
        onSelectionChange(keys, rows);
      }
      // TASK-200: For new declarations (savedSelection.length === 0), do NOT pre-select any rows!
      // Leave selectedKeys empty for 100% manual selection.
      initializedRef.current = true;
    }
  }, [allData, loading, savedSelection, onSelectionChange, selectedKeys.size]);

  const handleSelectionChanged = useCallback(() => {
    if (!gridApi) return;
    const selectedNodes = gridApi.getSelectedNodes();
    const keys = new Set<string>();
    const rows: ReglementRow[] = [];
    selectedNodes.forEach((node) => {
      if (node.data) {
        const key = reglementKey(node.data);
        keys.add(key);
        rows.push(node.data);
      }
    });
    onSelectionChange(keys, rows);
  }, [gridApi, onSelectionChange]);

  const handleModelUpdated = useCallback(() => {
    if (!gridApi) return;
    setDisplayedCount(gridApi.getDisplayedRowCount());
  }, [gridApi]);

  const onGridReady = useCallback((params: GridReadyEvent) => {
    setGridApi(params.api);
  }, []);

  useEffect(() => {
    if (!gridApi) return;
    gridApi.forEachNode((node) => {
      if (node.data) {
        const key = reglementKey(node.data);
        const shouldSelect = selectedKeys.has(key);
        if (node.isSelected() !== shouldSelect) {
          node.setSelected(shouldSelect);
        }
      }
    });
  }, [gridApi, selectedKeys, allData]);

  const selectedTotal = useMemo(() => {
    return selectedRows.reduce((sum, r) => sum + r.montant, 0);
  }, [selectedRows]);

  // La case à cocher est rendue par `rowSelection.checkboxes` (API v36, cf. ApbsGrid ci-dessous) +
  // `isRowSelectable` — une colonne dédiée `checkboxSelection` (ancienne API) en plus produisait
  // DEUX cases à cocher côte à côte (cf. même correctif dans DomainGrid.tsx).
  const columnDefs: ColDef<ReglementRow>[] = useMemo(() => [
    {
      field: 'numeroReglement',
      headerName: 'N° Règlement',
      width: 140,
      filter: CustomListFilter,
    },
    {
      field: 'date',
      headerName: 'Date règlement',
      width: 120,
      filter: 'agDateColumnFilter',
      valueFormatter: (p) => p.value ? formatDate(p.value) : '',
    },
    {
      field: 'echeance',
      headerName: 'Échéance',
      width: 120,
      filter: 'agDateColumnFilter',
      valueFormatter: (p) => (p.value ? formatDate(p.value) : '—'),
    },
    {
      field: 'mode',
      headerName: 'Mode',
      width: 110,
      filter: CustomListFilter,
      filterParams: { options: modeOptions },
    },
    {
      field: 'domaine',
      headerName: 'Domaine',
      width: 120,
      filter: CustomListFilter,
      cellRenderer: (p: any) => <DomaineBadge domaine={p.value} />,
    },
    {
      field: 'tiersCode',
      headerName: 'Code tiers',
      width: 110,
      filter: CustomListFilter,
      cellRenderer: (p: any) => p.value || <span style={{ color: 'var(--text-secondary)' }}>—</span>,
    },
    {
      field: 'tiers',
      headerName: 'Intitulé tiers',
      filter: CustomListFilter,
    },
    {
      field: 'montant',
      headerName: 'Montant',
      width: 130,
      type: 'numericColumn',
      filter: 'agNumberColumnFilter',
      valueFormatter: (p) => formatMoney(p.value),
    },
    {
      colId: 'affecte',
      headerName: 'Affecté',
      width: 140,
      filter: CustomListFilter,
      valueGetter: (p) => affecteLabel(p.data),
    },
    {
      field: 'rapprocheBanque',
      headerName: 'Rappr. banque',
      width: 120,
      filter: CustomListFilter,
      cellRenderer: (p: any) => (p.value ? (
        <span style={{ color: 'var(--status-ok-text)', fontWeight: 600 }}>Oui</span>
      ) : (
        <span style={{ color: 'var(--text-secondary)' }}>Non</span>
      )),
    },
    {
      field: 'dateRapprochement',
      headerName: 'Date rapprochement',
      width: 150,
      filter: 'agDateColumnFilter',
      valueFormatter: (p) => (p.value ? formatDate(p.value) : '—'),
    },
    {
      colId: 'statut',
      headerName: 'Statut',
      width: 140,
      filter: CustomListFilter,
      valueGetter: (p) => statutDe(p.data),
      cellRenderer: (p: any) => <StatutBadge row={p.data} />,
    },
  ], [modeOptions]);

  return (
    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', height: '100%', overflow: 'hidden' }}>
      <div style={{ flexGrow: 1, position: 'relative', background: 'white' }}>
        {!hasFetched && (!savedSelection || savedSelection.length === 0) ? (
          <div
            data-testid="selection-vide-invite"
            style={{
              height: '100%', display: 'flex', flexDirection: 'column',
              alignItems: 'center', justifyContent: 'center',
              padding: '3rem 1.5rem', textAlign: 'center',
            }}
          >
            <div style={{ width: '56px', height: '56px', borderRadius: '50%', background: '#eef2ff', display: 'flex', alignItems: 'center', justifyContent: 'center', marginBottom: '1rem', color: 'var(--accent-primary)' }}>
              <RefreshCw size={26} />
            </div>
            <h3 style={{ margin: '0 0 0.5rem 0', fontSize: '1.1rem', fontWeight: 600, color: 'var(--text-primary)' }}>
              Liste vide par défaut
            </h3>
            <p style={{ margin: '0 0 1.5rem 0', fontSize: '0.875rem', color: 'var(--text-secondary)', maxWidth: '480px', lineHeight: 1.5 }}>
              Cliquez sur le bouton <strong>« Intégrer »</strong> pour charger et examiner les règlements de la période.
            </p>
            <button
              onClick={handleIntegrerClick}
              disabled={loading}
              className="btn btn-primary"
              style={{
                display: 'inline-flex', alignItems: 'center', gap: '0.5rem',
                padding: '0.55rem 1.25rem', fontSize: '0.875rem', fontWeight: 600,
                borderRadius: 'var(--radius-md)', background: 'var(--accent-primary)', color: 'white', border: 'none', cursor: loading ? 'not-allowed' : 'pointer'
              }}
            >
              {loading ? <Loader2 size={16} className="animate-spin" /> : <Play size={16} />}
              Intégrer les règlements
            </button>
          </div>
        ) : (
          <ApbsGrid<ReglementRow>
            rowData={allData}
            columnDefs={columnDefs}
            rowSelection={{ mode: 'multiRow', checkboxes: true, headerCheckbox: true }}
            getRowId={(params) => reglementKey(params.data)}
            isRowSelectable={(node) => statutDe(node.data) !== 'bloque'}
            onSelectionChanged={handleSelectionChanged}
            onModelUpdated={handleModelUpdated}
            onGridReady={onGridReady}
            height="100%"
            showColumnSelector={true}
            showExportButton={false}
          />
        )}
      </div>

      <div style={{ padding: '0.5rem 1rem', borderTop: '1px solid var(--border-color)', display: 'flex', justifyContent: 'space-between', alignItems: 'center', background: 'white', gap: '0.75rem', flexWrap: 'wrap', fontSize: '0.8rem' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
          <button
            onClick={handleIntegrerClick}
            disabled={loading}
            className="btn btn-primary"
            style={{
              display: 'inline-flex', alignItems: 'center', gap: '0.4rem',
              padding: '0.4rem 0.85rem', fontSize: '0.8125rem', fontWeight: 600,
              borderRadius: 'var(--radius-md)', background: 'var(--accent-primary)', color: 'white', border: 'none', cursor: loading ? 'not-allowed' : 'pointer', opacity: loading ? 0.7 : 1,
            }}
            title="Intégrer / Rafraîchir les règlements de la période"
          >
            {loading ? <Loader2 size={14} className="animate-spin" /> : <Play size={14} />}
            {hasFetched ? 'Rafraîchir' : 'Intégrer'}
          </button>
          {loading && <Loader2 size={15} className="animate-spin" style={{ color: 'var(--accent-primary)' }} />}
          {hasFetched && (
            <>
              <span>Règlements : <strong>{displayedCount}</strong>{displayedCount !== allData.length && <> sur {allData.length}</>}</span>
              <span>Sélectionnés : <strong>{selectedKeys.size}</strong></span>
            </>
          )}
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
          <span style={{ fontSize: '0.85rem', fontWeight: 600 }}>Total sélectionné : {formatMoney(selectedTotal)}</span>
        </div>
      </div>
    </div>
  );
}
