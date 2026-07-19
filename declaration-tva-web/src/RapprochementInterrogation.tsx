import { useState, useEffect, useRef, useCallback } from 'react';
import { useVirtualizer } from '@tanstack/react-virtual';
import { Loader2, Landmark, X, CheckCircle2, Circle, AlertTriangle } from 'lucide-react';
import { ExcelFilter } from './ExcelFilter';
import { ColumnSelector } from './ColumnSelector';
import { useColumnPrefs } from './useColumnPrefs';
import { formatMoney, formatDate } from './utils';
import api from './api';

// ─── Interrogation « Rapprochement bancaire » (TASK-037) ──────────────────────
//
// Écran LECTURE SEULE, pivot RÈGLEMENT, consommant GET /api/rapprochement (TASK-036).
// Aucune écriture, aucun verrou, aucun couplage gocom-web. Densité comptable :
// « 0 espace perdu ». Pilier confiance : le « reste à affecter » ≠ 0 est rendu VISIBLE,
// jamais absorbé en silence.

type ListFilterValue = string | string[];

// Colonnes du pivot règlement. `sortKey` renseigné uniquement pour les colonnes
// triables côté serveur (date_desc | montant_desc | reste_desc, cf. TASK-036).
type Col = {
  key: string;
  label: string;
  align?: 'left' | 'right' | 'center';
  sortKey?: 'date' | 'montant' | 'reste';
  filterType?: 'list' | 'text' | 'number' | 'date';
  width?: string;
};

// TASK-063 : parité de filtre par type de donnée. Chaque colonne porte un filterType adapté :
//   date → plage du~au ; montant/compteur → plage min~max ; énumération → cases + recherche ;
//   texte → LIKE. La colonne `date` (règlement) reste sans filtre : déjà bornée par la période
//   globale obligatoire Du/Au (doublon inutile — décision PO, réversible).
// TASK-067B : n° règlement / n° extrait / code banque passent en 'list' (colonnes identifiantes,
// décision PO cardinalité — cf. GET /rapprochement/distincts). Tiers reste en 'text' (nom libre,
// cardinalité non bornée — décision documentée dans VERIFY/TASK-067B_verify.md).
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
  { key: 'origine', label: 'Origine', filterType: 'list', width: '120px' },
  { key: 'declare', label: 'Déclaré', align: 'center', filterType: 'list', width: '100px' },
];

// Reste à affecter significatif (tolérance centimes) → transparence de l'écart.
const isResteNonNul = (v: number) => Math.abs(v ?? 0) > 0.005;

// Premier jour de l'année courante → aujourd'hui : borne raisonnable et éditable
// (l'endpoint EXIGE une période bornée pour éviter tout scan intégral).
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

function OrigineChip({ origine }: { origine: string }) {
  // Origine EC_Type (preuve TVA) : Sage (OM) / FGR (détail SQL) / Mixte / SansAffectation.
  const map: Record<string, { bg: string, text: string }> = {
    Sage: { bg: '#e0e7ff', text: '#4338ca' },
    FGR: { bg: '#fef3c7', text: '#b45309' },
    Mixte: { bg: '#fae8ff', text: '#a21caf' },
    SansAffectation: { bg: 'var(--status-blocking-bg)', text: 'var(--status-blocking-text)' },
    SoldeInitial: { bg: '#f1f5f9', text: '#475569' },
  };
  const c = map[origine] || { bg: '#f3f4f6', text: '#374151' };
  return <span style={{ background: c.bg, color: c.text, padding: '2px 8px', borderRadius: '4px', fontSize: '0.7rem', fontWeight: 600 }}>{origine}</span>;
}

function DomaineChip({ domaine }: { domaine: string }) {
  // MV_Domaine : Encaissement / Décaissement / Frais bancaire (codes inconnus → « Autre (n) »).
  const map: Record<string, { bg: string, text: string }> = {
    Encaissement: { bg: '#dcfce7', text: '#15803d' },
    Décaissement: { bg: '#dbeafe', text: '#1d4ed8' },
    'Frais bancaire': { bg: '#fef3c7', text: '#b45309' },
  };
  const c = map[domaine] || { bg: '#f3f4f6', text: '#374151' };
  return <span style={{ background: c.bg, color: c.text, padding: '2px 8px', borderRadius: '4px', fontSize: '0.7rem', fontWeight: 600 }}>{domaine || '—'}</span>;
}

export function RapprochementInterrogation({ societeId, showToast }: { societeId: number, showToast: (m: string, t?: 'success' | 'error' | 'warning') => void }) {
  const [data, setData] = useState<any[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);

  // Période bornée (obligatoire endpoint).
  const [debut, setDebut] = useState<string>(yearStartIso());
  const [fin, setFin] = useState<string>(todayIso());

  const [page, setPage] = useState(1);
  const [filters, setFilters] = useState<Record<string, ListFilterValue>>({});
  const [sortConfig, setSortConfig] = useState<{ key: 'date' | 'montant' | 'reste', desc: boolean }>({ key: 'date', desc: true });
  const [modeOptions, setModeOptions] = useState<{ label: string, value: string }[]>([]);
  // TASK-067B — options des colonnes identifiantes, peuplées depuis /rapprochement/distincts
  // (MÊME WHERE/période que la liste principale — invariant TASK-040, aucune valeur infiltrable).
  const [numeroReglementOptions, setNumeroReglementOptions] = useState<string[]>([]);
  const [numeroExtraitOptions, setNumeroExtraitOptions] = useState<string[]>([]);
  const [banqueOptions, setBanqueOptions] = useState<string[]>([]);

  const [detailRow, setDetailRow] = useState<any | null>(null);

  const size = 100;
  const parentRef = useRef<HTMLDivElement>(null);

  const { visibleColumns, visibleKeys, toggle: toggleColumn, reset: resetColumns } = useColumnPrefs('grf.cols.rapprochement', COLUMNS);

  // Valeurs distinctes (modes présents) pour alimenter le filtre liste — dépend de la période.
  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const res = await api.get('/rapprochement/distincts', { params: { debut, fin, soId: societeId } });
        if (cancelled) return;
        const modes = (res.data.modes || []).map((m: any) => ({ label: m.libelle, value: String(m.code) }));
        setModeOptions(modes);
        // TASK-067B : colonnes identifiantes (n° règlement, n° extrait, code banque) — valeurs
        // distinctes exactes de la période, jamais une valeur absente du jeu réel (invariant TASK-040).
        setNumeroReglementOptions(res.data.numeroReglement || []);
        setNumeroExtraitOptions(res.data.numeroExtrait || []);
        setBanqueOptions(res.data.banque || []);
      } catch {
        if (!cancelled) {
          setModeOptions([]);
          setNumeroReglementOptions([]);
          setNumeroExtraitOptions([]);
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
      // TASK-063 — TOUS les filtres côté serveur (même WHERE que le COUNT, invariant TASK-040),
      // chaque filtre adapté au type de sa donnée. Plus aucun filtrage client résiduel.
      //
      // Énumérations & booléens : MULTI-SÉLECTION RÉELLE (fin du bug « filtre menteur » — plus de
      // `mode[0]`/`declare[0]` qui ignorait silencieusement les autres cases cochées). On envoie le
      // TABLEAU complet ; ASP.NET le binde en int[]/bool[]. Cocher Oui+Non renvoie bien les deux.
      const asArray = (v: ListFilterValue | undefined) => (Array.isArray(v) && v.length > 0 ? v : undefined);
      const mode = asArray(filters['mode']);
      if (mode) params.mode = mode;
      const rb = asArray(filters['rapprocheBanque']);
      if (rb) params.rapprocheBanque = rb;
      const dc = asArray(filters['declare']);
      if (dc) params.declare = dc;
      const pt = asArray(filters['point']);
      if (pt) params.point = pt;
      const origine = asArray(filters['origine']);
      if (origine) params.origine = origine;
      const domaine = asArray(filters['domaine']);
      if (domaine) params.domaine = domaine;
      // TASK-067B : n° règlement / n° extrait / code banque en MULTI-SÉLECTION RÉELLE (valeurs
      // distinctes exactes), fin du LIKE scalaire — même mécanisme que origine/domaine ci-dessus.
      const numero = asArray(filters['numeroReglement']);
      if (numero) params.numero = numero;
      const numeroExtrait = asArray(filters['numeroExtrait']);
      if (numeroExtrait) params.numeroExtrait = numeroExtrait;
      const banque = asArray(filters['banqueCode']);
      if (banque) params.banque = banque;

      // Textes (LIKE) — tiers reste en texte libre (cardinalité non bornée).
      const asText = (v: ListFilterValue | undefined) => (typeof v === 'string' && v.trim() !== '' ? v.trim() : undefined);
      const tiers = asText(filters['tiers']);
      if (tiers) params.tiers = tiers;

      // Plages (number/date) encodées « min~max » par ExcelFilter → deux query params NULL-safe.
      const range = (v: ListFilterValue | undefined): [string, string] => {
        if (typeof v !== 'string') return ['', ''];
        const [min, max] = v.split('~');
        return [(min || '').trim(), (max || '').trim()];
      };
      const setRange = (v: ListFilterValue | undefined, minKey: string, maxKey: string) => {
        const [min, max] = range(v);
        if (min !== '') params[minKey] = min;
        if (max !== '') params[maxKey] = max;
      };
      setRange(filters['montant'], 'montantMin', 'montantMax');
      setRange(filters['resteAAffecter'], 'resteMin', 'resteMax');
      setRange(filters['nbFacturesAffectees'], 'nbFacturesMin', 'nbFacturesMax');
      setRange(filters['dateRapprochement'], 'dateRappMin', 'dateRappMax');
      setRange(filters['echeance'], 'echeanceMin', 'echeanceMax');

      const res = await api.get('/rapprochement', {
        params,
        // origine[]/domaine[] : sérialisation en clés répétées (?origine=Sage&origine=FGR) attendue
        // par le binding [FromQuery] string[] côté ASP.NET.
        paramsSerializer: { indexes: null },
      });
      const items = res.data.items || [];
      setData(items);
      setTotal(res.data.totalCount ?? items.length);
    } catch (e) {
      console.error(e);
      showToast('Erreur lors du chargement du rapprochement', 'error');
      setData([]);
      setTotal(0);
    } finally {
      setLoading(false);
    }
  }, [debut, fin, page, filters, sortConfig, showToast, societeId]);

  useEffect(() => { setPage(1); }, [filters, sortConfig, debut, fin]);
  useEffect(() => { fetchPage(); }, [fetchPage]);

  const rowVirtualizer = useVirtualizer({
    count: data.length,
    getScrollElement: () => parentRef.current,
    estimateSize: () => 38,
    overscan: 8,
  });

  const handleSort = (col: Col) => {
    if (!col.sortKey) return;
    setSortConfig(prev => {
      if (prev.key === col.sortKey) return { key: col.sortKey!, desc: !prev.desc };
      return { key: col.sortKey!, desc: true };
    });
  };

  const handleFilterChange = (key: string, val: any) => {
    setFilters(prev => {
      const next = { ...prev };
      if (val === '' || (Array.isArray(val) && val.length === 0)) delete next[key];
      else next[key] = val;
      return next;
    });
  };

  const filterOptionsFor = (key: string): { label: string, value: string }[] => {
    if (key === 'mode') return modeOptions;
    // TASK-067B — colonnes identifiantes : options = valeurs distinctes de la PÉRIODE ENTIÈRE
    // (endpoint /rapprochement/distincts, même WHERE que la liste), jamais de la page courante.
    if (key === 'numeroReglement') return numeroReglementOptions.map(v => ({ label: v, value: v }));
    if (key === 'numeroExtrait') return numeroExtraitOptions.map(v => ({ label: v, value: v }));
    if (key === 'banqueCode') return banqueOptions.map(v => ({ label: v, value: v }));
    // Booléens en cases cochables — multi-sélection réelle (TASK-063) : Oui+Non ⇒ tout.
    if (key === 'rapprocheBanque' || key === 'declare' || key === 'point')
      return [{ label: 'Oui', value: 'true' }, { label: 'Non', value: 'false' }];
    // TASK-040 (étape 4) — DÉCISION : le filtrage est désormais SERVEUR (sur toute la période, pas la
    // page). Peupler les options depuis `data` (page courante) mentirait — une valeur absente de la
    // page serait infiltrable, et le premier filtre appliqué viderait la liste des choix. On expose donc
    // le DOMAINE MÉTIER COMPLET des libellés dérivés (source unique : ReglementRapprochementRow) :
    //  - origine ← LibelleOrigine (EC_Type + Mixte/SansAffectation) ;
    //  - domaine ← LibelleDomaine restreint à MV_Domaine IN (0,1) côté serveur (TASK-039).
    // Ces valeurs correspondent EXACTEMENT aux libellés comparés dans le WHERE serveur.
    if (key === 'origine') {
      return ['Sage', 'FGR', 'Mixte', 'SansAffectation', 'SoldeInitial'].map(o => ({ label: o, value: o }));
    }
    if (key === 'domaine') {
      return ['Encaissement', 'Décaissement'].map(o => ({ label: o, value: o }));
    }
    return [];
  };

  const renderCell = (col: Col, row: any) => {
    const v = row[col.key];
    switch (col.key) {
      case 'date': return formatDate(v);
      case 'dateRapprochement':
      case 'echeance':
        return v ? formatDate(v) : <span style={{ color: 'var(--text-secondary)' }}>—</span>;
      case 'numeroExtrait':
      case 'banqueCode':
        return v ? v : <span style={{ color: 'var(--text-secondary)' }}>—</span>;
      case 'montant': return formatMoney(v);
      case 'rapprocheBanque':
        // Espèce = auto-rapprochée (pas de pointage sur extrait) : marqueur explicite pour ne pas
        // laisser croire à un rapprochement bancaire réel (transparence — TASK-042).
        return (
          <span style={{ display: 'inline-flex', alignItems: 'center', gap: '0.25rem' }}>
            <OuiNonBadge value={!!v} />
            {!!v && row.mode === 'Espèce' && (
              <span style={{ fontSize: '0.62rem', color: 'var(--text-secondary)', fontStyle: 'italic' }}>espèce</span>
            )}
          </span>
        );
      case 'point': return <OuiNonBadge value={!!v} />;
      case 'declare': return <OuiNonBadge value={!!v} trueColor={{ bg: '#e0e7ff', text: '#4338ca' }} />;
      case 'origine': return <OrigineChip origine={v} />;
      case 'domaine': return <DomaineChip domaine={v} />;
      case 'resteAAffecter':
        return isResteNonNul(v)
          ? <span style={{ color: 'var(--status-warning-text-alt)', fontWeight: 700, display: 'inline-flex', alignItems: 'center', gap: '0.25rem', justifyContent: 'flex-end' }}><AlertTriangle size={12} />{formatMoney(v)}</span>
          : <span style={{ color: 'var(--text-secondary)' }}>{formatMoney(v)}</span>;
      case 'nbFacturesAffectees': return v;
      default: return v;
    }
  };

  const totalPages = Math.ceil(total / size) || 1;
  const activeFilterCount = Object.keys(filters).length;

  // Largeur de colonne partagée entête/corps. Colonne sans width fixe (« Tiers »)
  // = flexible. Indispensable : la virtualisation met les lignes en position:absolute,
  // ce qui casse le modèle de colonnes d'un <table>. On aligne donc via flexbox.
  const colStyle = (col: Col): React.CSSProperties =>
    col.width
      ? { flex: `0 0 ${col.width}`, width: col.width }
      : { flex: '1 1 0', minWidth: '160px' };
  const colJustify = (col: Col) =>
    col.align === 'right' ? 'flex-end' : col.align === 'center' ? 'center' : 'flex-start';

  return (
    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', height: '100%', overflow: 'hidden' }}>
      {/* En-tête écran + période */}
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
          <ColumnSelector columns={COLUMNS} visibleKeys={visibleKeys} onToggle={toggleColumn} onReset={resetColumns} />
        </div>
      </div>

      {/* Barre d'info */}
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

      {/* Grille (flexbox : entête + lignes virtualisées partagent les mêmes largeurs) */}
      <div ref={parentRef} style={{ flexGrow: 1, overflow: 'auto', position: 'relative', background: 'white' }}>
        <div style={{ minWidth: '1600px', fontSize: '0.8125rem' }}>
          {/* Entête collant */}
          <div style={{ display: 'flex', position: 'sticky', top: 0, background: 'var(--bg-secondary)', zIndex: 10, boxShadow: '0 1px 2px rgba(0,0,0,0.05)', borderBottom: '1px solid var(--border-color)' }}>
            {visibleColumns.map(col => (
              <div
                key={col.key}
                style={{ ...colStyle(col), padding: '0.5rem 0.75rem', borderRight: '1px solid var(--border-color)', cursor: col.sortKey ? 'pointer' : 'default', userSelect: 'none', display: 'flex', alignItems: 'center', justifyContent: 'center', gap: '0.25rem', fontWeight: 600, whiteSpace: 'nowrap' }}
                onClick={() => handleSort(col)}
              >
                {col.label}
                {col.sortKey && sortConfig.key === col.sortKey && (
                  <span style={{ fontSize: '0.7rem', color: 'var(--text-secondary)' }}>{sortConfig.desc ? '▼' : '▲'}</span>
                )}
                {col.filterType && (
                  <span onClick={e => e.stopPropagation()}>
                    <ExcelFilter
                      filterType={col.filterType}
                      options={filterOptionsFor(col.key)}
                      selectedValues={Array.isArray(filters[col.key]) ? filters[col.key] as string[] : []}
                      textValue={typeof filters[col.key] === 'string' ? filters[col.key] as string : ''}
                      onChange={(val) => handleFilterChange(col.key, val)}
                    />
                  </span>
                )}
              </div>
            ))}
          </div>

          {/* Corps virtualisé */}
          <div style={{ height: `${rowVirtualizer.getTotalSize()}px`, position: 'relative' }}>
            {rowVirtualizer.getVirtualItems().map(virtualRow => {
              const row = data[virtualRow.index];
              if (!row) return null;
              return (
                <div
                  key={`${row.numeroReglement}-${virtualRow.index}`}
                  onClick={() => setDetailRow(row)}
                  style={{
                    position: 'absolute', top: 0, left: 0, width: '100%',
                    transform: `translateY(${virtualRow.start}px)`,
                    height: `${virtualRow.size}px`,
                    display: 'flex',
                    borderBottom: '1px solid var(--border-color)',
                    background: 'white', cursor: 'pointer',
                  }}
                  onMouseEnter={e => (e.currentTarget.style.background = 'var(--bg-secondary)')}
                  onMouseLeave={e => (e.currentTarget.style.background = 'white')}
                >
                  {visibleColumns.map(col => (
                    <div key={col.key} style={{ ...colStyle(col), padding: '0.4rem 0.75rem', borderRight: '1px solid var(--border-color)', display: 'flex', alignItems: 'center', justifyContent: colJustify(col), whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                      {renderCell(col, row)}
                    </div>
                  ))}
                </div>
              );
            })}
          </div>
        </div>
        {data.length === 0 && !loading && (
          <div style={{ padding: '3rem', textAlign: 'center', color: 'var(--text-secondary)' }}>
            Aucun règlement pour ces filtres / cette période.
          </div>
        )}
      </div>

      {/* Pagination */}
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

// Panneau de détail À LA DEMANDE (pas de panneau permanent — tension « 0 espace perdu »).
// Montre la décomposition du règlement telle que fournie par TASK-036 : montant, montant
// affecté (nb factures), reste à affecter explicite, origine EC_Type (preuve TVA OM/FGR),
// rapproché banque, déclaré. Aucune donnée factice : le détail par facture n'existe pas dans
// l'endpoint TASK-036 (agrégat) — la ventilation ligne à ligne relève du poste Déclaration.
function ReglementDetail({ row, onClose }: { row: any, onClose: () => void }) {
  const reste = row.resteAAffecter ?? 0;
  const line = (label: string, value: React.ReactNode) => (
    <div style={{ display: 'flex', justifyContent: 'space-between', padding: '0.6rem 0', borderBottom: '1px solid var(--border-color)', fontSize: '0.85rem' }}>
      <span style={{ color: 'var(--text-secondary)' }}>{label}</span>
      <span style={{ fontWeight: 600, textAlign: 'right' }}>{value}</span>
    </div>
  );

  return (
    <div style={{ position: 'fixed', top: 0, left: 0, width: '100%', height: '100%', background: 'rgba(0,0,0,0.5)', zIndex: 1000, display: 'flex', justifyContent: 'flex-end' }} onClick={onClose}>
      <div style={{ width: '480px', maxWidth: '100%', height: '100%', background: 'white', boxShadow: '-4px 0 15px rgba(0,0,0,0.1)', display: 'flex', flexDirection: 'column' }} onClick={e => e.stopPropagation()}>
        <div style={{ padding: '1.25rem 1.5rem', borderBottom: '1px solid var(--border-color)', display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
          <div>
            <h3 style={{ margin: 0, fontSize: '1.1rem', fontWeight: 600 }}>Règlement {row.numeroReglement}</h3>
            <div style={{ fontSize: '0.8rem', color: 'var(--text-secondary)', marginTop: '0.15rem' }}>{formatDate(row.date)} · {row.mode}</div>
          </div>
          <button onClick={onClose} style={{ background: 'transparent', border: 'none', cursor: 'pointer', color: 'var(--text-secondary)' }}><X size={22} /></button>
        </div>

        <div style={{ flex: 1, overflowY: 'auto', padding: '1.25rem 1.5rem' }}>
          <div style={{ marginBottom: '1.25rem' }}>
            <div style={{ fontSize: '0.7rem', fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.05em', color: 'var(--text-secondary)', marginBottom: '0.4rem' }}>Tiers</div>
            <div style={{ fontSize: '0.9rem', fontWeight: 600 }}>{row.tiers || '—'}</div>
            {row.tiersCode && <div style={{ fontSize: '0.75rem', color: 'var(--text-secondary)' }}>{row.tiersCode}</div>}
          </div>

          <div style={{ fontSize: '0.7rem', fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.05em', color: 'var(--text-secondary)', marginBottom: '0.2rem' }}>Décomposition</div>
          {line('Montant du règlement', formatMoney(row.montant))}
          {line(`Montant affecté (${row.nbFacturesAffectees} facture${row.nbFacturesAffectees > 1 ? 's' : ''})`, formatMoney(row.montantAffecte))}
          {line('Reste à affecter', isResteNonNul(reste)
            ? <span style={{ color: 'var(--status-warning-text-alt)', fontWeight: 700, display: 'inline-flex', alignItems: 'center', gap: '0.25rem' }}><AlertTriangle size={13} />{formatMoney(reste)}</span>
            : <span style={{ color: 'var(--status-ok-text)' }}>{formatMoney(reste)}</span>)}

          {isResteNonNul(reste) && (
            <div style={{ marginTop: '0.75rem', padding: '0.6rem 0.75rem', background: 'var(--status-warning-bg)', border: '1px solid var(--status-warning-border)', borderRadius: '6px', fontSize: '0.78rem', color: 'var(--status-warning-text)', display: 'flex', gap: '0.5rem' }}>
              <AlertTriangle size={15} style={{ flexShrink: 0, marginTop: '1px' }} />
              <span>Écart non nul entre le montant du règlement et la somme des affectations — rendu visible (aucune somme absorbée en silence).</span>
            </div>
          )}

          <div style={{ fontSize: '0.7rem', fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.05em', color: 'var(--text-secondary)', margin: '1.25rem 0 0.2rem' }}>État & preuve TVA</div>
          {line('Rapproché banque', (
            <span style={{ display: 'inline-flex', alignItems: 'center', gap: '0.35rem' }}>
              <OuiNonBadge value={!!row.rapprocheBanque} />
              {!!row.rapprocheBanque && row.mode === 'Espèce' && (
                <span style={{ fontSize: '0.7rem', color: 'var(--text-secondary)', fontStyle: 'italic' }}>espèce (auto)</span>
              )}
            </span>
          ))}
          {line('Pointage (extrait)', <OuiNonBadge value={!!row.point} />)}
          {line('Date rapprochement', row.dateRapprochement ? formatDate(row.dateRapprochement) : '—')}
          {line('N° extrait', row.numeroExtrait || '—')}
          {line('Échéance pièce', row.echeance ? formatDate(row.echeance) : '—')}
          {line('Code banque', row.banqueCode || '—')}
          {line('Origine (preuve TVA)', <OrigineChip origine={row.origine} />)}
          {line('Déclaré (verrou)', <OuiNonBadge value={!!row.declare} trueColor={{ bg: '#e0e7ff', text: '#4338ca' }} />)}

          <div style={{ marginTop: '1rem', fontSize: '0.72rem', color: 'var(--text-secondary)', lineHeight: 1.5 }}>
            L'origine indique la source de la TVA des factures affectées :{' '}
            <strong>Sage</strong> (via OM), <strong>FGR</strong> (détail comptable), <strong>Mixte</strong> ou <strong>SansAffectation</strong>.
            La ventilation ligne à ligne et la valorisation TVA détaillée sont produites dans le poste « Déclaration TVA ».
          </div>
        </div>
      </div>
    </div>
  );
}
