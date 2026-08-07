import { useState, useEffect, useCallback, useMemo } from 'react';
import { Loader2, Landmark, X, CheckCircle2, Circle, AlertTriangle } from 'lucide-react';
import type { ColDef } from 'ag-grid-community';
import { ApbsGrid } from './grid/ApbsGrid';
import { CustomListFilter } from './grid/CustomListFilter';
import { formatMoney, formatDate } from './utils';
import api from './api';

// ─── Interrogation « Rapprochement bancaire » (TASK-037 / TASK-204 AG Grid) ────

type ListFilterValue = string | string[];

type Col = {
  key: string;
  label: string;
  align?: 'left' | 'right' | 'center';
  sortKey?: 'date' | 'montant' | 'reste';
  filterType?: 'list' | 'text' | 'number' | 'date';
  width?: string;
};

const COLUMNS: Col[] = [
  { key: 'numeroReglement', label: 'N° Règlement', filterType: 'list', width: '140px' },
  { key: 'date', label: 'Date', sortKey: 'date', width: '100px' },
  { key: 'mode', label: 'Mode', filterType: 'list', width: '110px' },
  { key: 'domaine', label: 'Domaine', filterType: 'list', width: '130px' },
  { key: 'tiers', label: 'Tiers', filterType: 'text' },
  { key: 'montant', label: 'Montant', align: 'right', sortKey: 'montant', filterType: 'number', width: '130px' },
  { key: 'rapprocheBanque', label: 'Rappr. banque', align: 'center', filterType: 'list', width: '130px' },
  { key: 'point', label: 'Point', align: 'center', filterType: 'list', width: '80px' },
  { key: 'dateRapprochement', label: 'Date rappro', filterType: 'date', width: '110px' },
  { key: 'numeroExtrait', label: 'N° extrait', filterType: 'list', width: '110px' },
  { key: 'echeance', label: 'Échéance', filterType: 'date', width: '100px' },
  { key: 'banqueCode', label: 'Code banque', filterType: 'list', width: '110px' },
  { key: 'nbFacturesAffectees', label: 'Factures', align: 'center', filterType: 'number', width: '90px' },
  { key: 'resteAAffecter', label: 'Reste à affecter', align: 'right', sortKey: 'reste', filterType: 'number', width: '140px' },
  { key: 'montantTva', label: 'TVA', align: 'right', filterType: 'number', width: '130px' },
  { key: 'origine', label: 'Origine', filterType: 'list', width: '120px' },
  { key: 'declare', label: 'Déclaré', align: 'center', filterType: 'list', width: '150px' },
];

const isResteNonNul = (v: number) => Math.abs(v ?? 0) > 0.005;
const todayIso = () => new Date().toISOString().slice(0, 10);
const yearStartIso = () => `${new Date().getFullYear()}-01-01`;

function OuiNonBadge({ value, trueColor = { bg: 'var(--status-ok-bg)', text: 'var(--status-ok-text)' } }: { value: boolean, trueColor?: { bg: string, text: string } }) {
  const c = value ? trueColor : { bg: '#f3f4f6', text: '#6b7280' };
  return (
    <span style={{ background: c.bg, color: c.text, padding: '2px 8px', borderRadius: '99px', fontSize: '0.7rem', fontWeight: 600, display: 'inline-flex', alignItems: 'center', gap: '0.2rem' }}>
      {value ? <CheckCircle2 size={11} /> : <Circle size={11} />}
      {value ? 'Oui' : 'Non'}
    </span>
  );
}

function DeclareBadge({ numero, declare }: { numero?: string | null; declare: boolean }) {
  if (numero) {
    return (
      <span title={numero} style={{ background: '#e0e7ff', color: '#4338ca', padding: '2px 8px', borderRadius: '99px', fontSize: '0.7rem', fontWeight: 600, display: 'inline-flex', alignItems: 'center', gap: '0.25rem', maxWidth: '100%', overflow: 'hidden' }}>
        <CheckCircle2 size={11} style={{ flexShrink: 0 }} />
        <span style={{ overflow: 'hidden', textOverflow: 'ellipsis' }}>{numero}</span>
      </span>
    );
  }
  return <OuiNonBadge value={declare} trueColor={{ bg: '#e0e7ff', text: '#4338ca' }} />;
}

function OrigineChip({ origine }: { origine: string }) {
  const map: Record<string, { bg: string, text: string }> = {
    Sage: { bg: '#e0e7ff', text: '#4338ca' },
    FGR: { bg: '#fef3c7', text: '#b45309' },
    Mixte: { bg: '#fae8ff', text: '#a21caf' },
    SansAffectation: { bg: 'var(--status-blocking-bg)', text: 'var(--status-blocking-text)' },
    SoldeInitial: { bg: '#f1f5f9', text: '#475569' },
  };
  const c = map[origine] || { bg: '#f3f4f6', text: '#374151' };
  return <span style={{ background: c.bg, color: c.text, padding: '1px 6px', borderRadius: '4px', fontSize: '0.7rem', fontWeight: 500 }}>{origine || '—'}</span>;
}

function DomaineChip({ domaine }: { domaine: string }) {
  const isEnc = domaine === 'Encaissement';
  return (
    <span style={{ background: isEnc ? '#e0f2fe' : '#f3e8ff', color: isEnc ? '#0369a1' : '#6b21a8', padding: '1px 6px', borderRadius: '4px', fontSize: '0.7rem', fontWeight: 600 }}>
      {domaine || '—'}
    </span>
  );
}

export function RapprochementInterrogation({
  societeId,
  showToast,
}: {
  societeId: number;
  showToast: (m: string, t?: 'success' | 'error' | 'warning') => void;
}) {
  const [debut, setDebut] = useState(yearStartIso);
  const [fin, setFin] = useState(todayIso);
  const [data, setData] = useState<any[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);

  const [page, setPage] = useState(1);
  const [filters, setFilters] = useState<Record<string, ListFilterValue>>({});
  const [sortConfig, _setSortConfig] = useState<{ key: 'date' | 'montant' | 'reste', desc: boolean }>({ key: 'date', desc: true });
  const [modeOptions, setModeOptions] = useState<{ label: string, value: string }[]>([]);
  const [domaineOptions, setDomaineOptions] = useState<{ label: string, value: string }[]>([]);
  const [numeroOptions, setNumeroOptions] = useState<string[]>([]);
  const [extraitOptions, setExtraitOptions] = useState<string[]>([]);
  const [banqueOptions, setBanqueOptions] = useState<string[]>([]);
  const [origineOptions, setOrigineOptions] = useState<{ label: string, value: string }[]>([]);
  const [detailRow, setDetailRow] = useState<any | null>(null);

  const size = 100;

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const res = await api.get('/rapprochement/distincts', { params: { debut, fin, soId: societeId } });
        if (cancelled) return;
        const modes = (res.data.modes || []).map((m: any) => ({ label: m.libelle, value: String(m.code) }));
        const domaines = (res.data.domaines || []).map((d: any) => ({ label: d, value: d }));
        const origines = (res.data.origines || []).map((o: any) => ({ label: o.libelle, value: o.libelle }));
        setModeOptions(modes);
        setDomaineOptions(domaines);
        setOrigineOptions(origines);
        setNumeroOptions(res.data.numero || []);
        setExtraitOptions(res.data.extrait || []);
        setBanqueOptions(res.data.banque || []);
      } catch {
        if (!cancelled) {
          setModeOptions([]);
          setDomaineOptions([]);
          setOrigineOptions([]);
          setNumeroOptions([]);
          setExtraitOptions([]);
          setBanqueOptions([]);
        }
      }
    })();
    return () => { cancelled = true; };
  }, [debut, fin, societeId]);

  const fetchPage = useCallback(async () => {
    setLoading(true);
    try {
      const params: any = {
        debut,
        fin,
        soId: societeId,
        page,
        size,
        sort: `${sortConfig.key}_${sortConfig.desc ? 'desc' : 'asc'}`,
      };
      const numero = filters['numeroReglement'];
      if (Array.isArray(numero) && numero.length > 0) params.numero = numero;
      const mode = filters['mode'];
      if (Array.isArray(mode) && mode.length > 0) params.mode = mode;
      const domaine = filters['domaine'];
      if (Array.isArray(domaine) && domaine.length > 0) params.domaine = domaine;
      const tiers = filters['tiers'];
      if (typeof tiers === 'string' && tiers.trim() !== '') params.tiers = tiers.trim();
      const rb = filters['rapprocheBanque'];
      if (Array.isArray(rb) && rb.length > 0) params.rapprocheBanque = rb[0] === 'true';
      const pt = filters['point'];
      if (Array.isArray(pt) && pt.length > 0) params.point = pt[0] === 'true';
      const ext = filters['numeroExtrait'];
      if (Array.isArray(ext) && ext.length > 0) params.numeroExtrait = ext;
      const bq = filters['banqueCode'];
      if (Array.isArray(bq) && bq.length > 0) params.banqueCode = bq;
      const orig = filters['origine'];
      if (Array.isArray(orig) && orig.length > 0) params.origine = orig;
      const decl = filters['declare'];
      if (Array.isArray(decl) && decl.length > 0) params.declare = decl[0] === 'true';

      const setRange = (v: ListFilterValue | undefined, minK: string, maxK: string) => {
        if (typeof v !== 'string') return;
        const [min, max] = v.split('~');
        if ((min || '').trim() !== '') params[minK] = min.trim();
        if ((max || '').trim() !== '') params[maxK] = max.trim();
      };
      setRange(filters['montant'], 'montantMin', 'montantMax');
      setRange(filters['nbFacturesAffectees'], 'nbFacturesMin', 'nbFacturesMax');
      setRange(filters['resteAAffecter'], 'resteMin', 'resteMax');
      setRange(filters['dateRapprochement'], 'dateRappMin', 'dateRappMax');
      setRange(filters['echeance'], 'echeanceMin', 'echeanceMax');

      const res = await api.get('/rapprochement', {
        params,
        paramsSerializer: { indexes: null },
      });
      const items = res.data.items || [];
      setData(items);
      setTotal(res.data.totalCount ?? items.length);
    } catch (e) {
      console.error(e);
      showToast('Erreur lors du chargement des rapprochements', 'error');
      setData([]);
      setTotal(0);
    } finally {
      setLoading(false);
    }
  }, [debut, fin, page, filters, sortConfig, showToast, societeId]);

  useEffect(() => { setPage(1); }, [filters, sortConfig, debut, fin]);
  useEffect(() => { fetchPage(); }, [fetchPage]);

  const renderCell = (key: string, row: any) => {
    const v = row[key];
    switch (key) {
      case 'date': return formatDate(v);
      case 'dateRapprochement':
      case 'echeance':
        return v ? formatDate(v) : <span style={{ color: 'var(--text-secondary)' }}>—</span>;
      case 'numeroExtrait':
      case 'banqueCode':
        return v ? v : <span style={{ color: 'var(--text-secondary)' }}>—</span>;
      case 'montant': return formatMoney(v);
      case 'rapprocheBanque':
        return (
          <span style={{ display: 'inline-flex', alignItems: 'center', gap: '0.25rem' }}>
            <OuiNonBadge value={!!v} />
            {!!v && row.mode === 'Espèce' && (
              <span style={{ fontSize: '0.62rem', color: 'var(--text-secondary)', fontStyle: 'italic' }}>espèce</span>
            )}
          </span>
        );
      case 'point': return <OuiNonBadge value={!!v} />;
      case 'declare': return <DeclareBadge numero={row.numeroDeclaration} declare={!!v} />;
      case 'origine': return <OrigineChip origine={v} />;
      case 'domaine': return <DomaineChip domaine={v} />;
      case 'resteAAffecter':
        return isResteNonNul(v)
          ? <span style={{ color: 'var(--status-warning-text-alt)', fontWeight: 700, display: 'inline-flex', alignItems: 'center', gap: '0.25rem', justifyContent: 'flex-end' }}><AlertTriangle size={12} />{formatMoney(v)}</span>
          : <span style={{ color: 'var(--text-secondary)' }}>{formatMoney(v)}</span>;
      case 'montantTva': {
        const etat = row.etatValorisation;
        if (etat === 'Indisponible') {
          return <span style={{ color: 'var(--status-blocking-text)', fontWeight: 500, fontSize: '0.78rem' }} title="Valorisation indisponible">— (indisponible)</span>;
        }
        if (etat === 'Partielle') {
          return (
            <span style={{ color: 'var(--status-warning-text-alt)', fontWeight: 600, display: 'inline-flex', alignItems: 'center', gap: '0.25rem' }} title="Valorisation partielle">
              <AlertTriangle size={12} />
              {formatMoney(v)}
            </span>
          );
        }
        if (v !== null && v !== undefined && etat === 'Valorisee') {
          return formatMoney(v);
        }
        return <span style={{ color: 'var(--text-secondary)' }}>—</span>;
      }
      case 'nbFacturesAffectees': return v;
      default: return v;
    }
  };

  const columnDefs: ColDef[] = useMemo(() => {
    return COLUMNS.map((col) => {
      const isNumeric = ['montant', 'nbFacturesAffectees', 'resteAAffecter', 'montantTva'].includes(col.key);
      const w = col.width ? parseInt(col.width, 10) : 130;

      let filterComponent: any = undefined;
      let filterParams: any = undefined;

      if (col.filterType === 'list') {
        filterComponent = CustomListFilter;
        if (col.key === 'mode') filterParams = { options: modeOptions };
        else if (col.key === 'domaine') filterParams = { options: domaineOptions };
        else if (col.key === 'origine') filterParams = { options: origineOptions };
        else if (col.key === 'numeroReglement') filterParams = { options: numeroOptions.map(v => ({ label: v, value: v })) };
        else if (col.key === 'numeroExtrait') filterParams = { options: extraitOptions.map(v => ({ label: v, value: v })) };
        else if (col.key === 'banqueCode') filterParams = { options: banqueOptions.map(v => ({ label: v, value: v })) };
        else if (col.key === 'rapprocheBanque' || col.key === 'point' || col.key === 'declare') {
          filterParams = { options: [{ label: 'Oui', value: 'true' }, { label: 'Non', value: 'false' }] };
        }
      } else if (col.filterType === 'text') {
        filterComponent = 'agTextColumnFilter';
      } else if (col.filterType === 'number') {
        filterComponent = 'agNumberColumnFilter';
      } else if (col.filterType === 'date') {
        filterComponent = 'agDateColumnFilter';
      }

      return {
        field: col.key,
        headerName: col.label,
        width: w,
        type: isNumeric ? 'numericColumn' : undefined,
        filter: filterComponent,
        filterParams,
        cellRenderer: (p: any) => p.data ? renderCell(col.key, p.data) : null,
      };
    });
  }, [modeOptions, domaineOptions, origineOptions, numeroOptions, extraitOptions, banqueOptions]);

  const totalPages = Math.ceil(total / size) || 1;
  const activeFilterCount = Object.keys(filters).length;

  return (
    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', height: '100%', overflow: 'hidden' }}>
      <div style={{ padding: '0.75rem 1rem', borderBottom: '1px solid var(--border-color)', background: 'white', display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: '0.75rem' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.6rem' }}>
          <Landmark size={20} style={{ color: 'var(--accent-primary)' }} />
          <div>
            <h2 style={{ margin: 0, fontSize: '1.05rem', fontWeight: 600 }}>Rapprochement bancaire</h2>
            <div style={{ fontSize: '0.72rem', color: 'var(--text-secondary)' }}>Interrogation globale — pivot règlement, lecture seule</div>
          </div>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', fontSize: '0.8rem' }}>
          <label style={{ color: 'var(--text-secondary)' }}>Du</label>
          <input type="date" value={debut} max={fin} onChange={e => setDebut(e.target.value)} className="form-input" style={{ fontSize: '0.8rem', padding: '0.25rem 0.5rem' }} />
          <label style={{ color: 'var(--text-secondary)' }}>Au</label>
          <input type="date" value={fin} min={debut} onChange={e => setFin(e.target.value)} className="form-input" style={{ fontSize: '0.8rem', padding: '0.25rem 0.5rem' }} />
        </div>
      </div>

      <div style={{ padding: '0.4rem 1rem', borderBottom: '1px solid var(--border-color)', background: 'var(--bg-secondary)', display: 'flex', justifyContent: 'space-between', alignItems: 'center', fontSize: '0.8rem' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
          {loading && <Loader2 size={15} className="animate-spin" style={{ color: 'var(--accent-primary)' }} />}
          <span>Règlements : <strong>{total}</strong></span>
        </div>
        {activeFilterCount > 0 && (
          <button className="btn" onClick={() => setFilters({})} style={{ background: 'transparent', border: 'none', color: 'var(--accent-primary)', textDecoration: 'underline', padding: 0, fontSize: '0.8rem', cursor: 'pointer' }}>
            Effacer filtres ({activeFilterCount})
          </button>
        )}
      </div>

      <div style={{ flexGrow: 1, position: 'relative' }}>
        <ApbsGrid
          rowData={data}
          columnDefs={columnDefs}
          onRowClicked={(params) => setDetailRow(params.data)}
          height="100%"
          showColumnSelector={true}
          showExportButton={true}
          exportFileName="rapprochement_interrogation.xlsx"
        />
      </div>

      <div style={{ padding: '0.5rem 1rem', borderTop: '1px solid var(--border-color)', display: 'flex', justifyContent: 'space-between', alignItems: 'center', background: 'white' }}>
        <span style={{ fontSize: '0.8rem', color: 'var(--text-secondary)' }}>Page {page} sur {totalPages}</span>
        <div style={{ display: 'flex', gap: '0.5rem' }}>
          <button onClick={() => setPage(p => Math.max(1, p - 1))} disabled={page === 1} className="btn" style={{ padding: '0.25rem 0.75rem', fontSize: '0.8rem' }}>Précédent</button>
          <button onClick={() => setPage(p => Math.min(totalPages, p + 1))} disabled={page >= totalPages} className="btn" style={{ padding: '0.25rem 0.75rem', fontSize: '0.8rem' }}>Suivant</button>
        </div>
      </div>

      {detailRow && <ReglementDetail row={detailRow} onClose={() => setDetailRow(null)} />}
    </div>
  );
}

function ReglementDetail({ row, onClose }: { row: any, onClose: () => void }) {
  const line = (label: string, value: React.ReactNode) => (
    <div style={{ display: 'flex', justifyContent: 'space-between', padding: '0.6rem 0', borderBottom: '1px solid var(--border-color)', fontSize: '0.85rem' }}>
      <span style={{ color: 'var(--text-secondary)' }}>{label}</span>
      <span style={{ fontWeight: 600, textAlign: 'right' }}>{value}</span>
    </div>
  );

  return (
    <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.4)', zIndex: 1000, display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '1rem' }} onClick={onClose}>
      <div style={{ background: 'white', borderRadius: 'var(--radius-md)', width: '560px', maxWidth: '100%', maxHeight: '90vh', overflow: 'auto', padding: '1.25rem', boxShadow: '0 10px 25px rgba(0,0,0,0.15)' }} onClick={e => e.stopPropagation()}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '1rem', borderBottom: '1px solid var(--border-color)', paddingBottom: '0.75rem' }}>
          <div>
            <div style={{ fontSize: '0.75rem', color: 'var(--text-secondary)', textTransform: 'uppercase', letterSpacing: '0.05em' }}>Détail Règlement</div>
            <h3 style={{ margin: '0.2rem 0 0 0', fontSize: '1.15rem', fontWeight: 700 }}>{row.numeroReglement || '—'}</h3>
          </div>
          <button onClick={onClose} style={{ background: 'transparent', border: 'none', cursor: 'pointer', padding: '0.25rem', color: 'var(--text-secondary)' }}><X size={18} /></button>
        </div>

        <div style={{ marginBottom: '1rem' }}>
          {line('Date règlement', formatDate(row.date))}
          {line('Mode', row.mode || '—')}
          {line('Domaine', <DomaineChip domaine={row.domaine} />)}
          {line('Tiers', `${row.tiersCode || ''}${row.tiersCode && row.tiers ? ' · ' : ''}${row.tiers || '—'}`)}
          {line('Montant', formatMoney(row.montant))}
          {line('Origine', <OrigineChip origine={row.origine} />)}
        </div>

        <div style={{ marginBottom: '1rem' }}>
          {line('Rapprochement banque', <OuiNonBadge value={row.rapprocheBanque} />)}
          {line('Pointage', <OuiNonBadge value={row.point} />)}
          {line('Date rapprochement', row.dateRapprochement ? formatDate(row.dateRapprochement) : '—')}
          {line('N° Extrait', row.numeroExtrait || '—')}
          {line('Échéance', row.echeance ? formatDate(row.echeance) : '—')}
          {line('Code banque', row.banqueCode || '—')}
        </div>

        <div>
          {line('Factures affectées', row.nbFacturesAffectees)}
          {line('Reste à affecter', isResteNonNul(row.resteAAffecter) ? <span style={{ color: 'var(--status-warning-text-alt)', fontWeight: 700 }}>{formatMoney(row.resteAAffecter)}</span> : formatMoney(row.resteAAffecter))}
          {line('Déclaré', <DeclareBadge numero={row.numeroDeclaration} declare={row.declare} />)}
        </div>
      </div>
    </div>
  );
}
