import { useState, useEffect, useRef, useCallback, useMemo } from 'react';
import { useVirtualizer } from '@tanstack/react-virtual';
import { Loader2, CheckCircle2, AlertTriangle, XCircle } from 'lucide-react';
import { ExcelFilter } from './ExcelFilter';
import { ColumnSelector } from './ColumnSelector';
import { useColumnPrefs } from './useColumnPrefs';
import { formatMoney, formatDate } from './utils';
import api from './api';

// ─── Écran ① Règlements (TASK-054) ─────────────────────────────────────────
//
// Point d'entrée du tunnel règlement-first (TASK-053) : liste les règlements
// décaissés de la PÉRIODE DE LA DÉCLARATION et rend leur sélection actionnable
// vers ② Affectations (TASK-055). Base obligatoire = adaptation de la grille
// dense `RapprochementInterrogation.tsx` (TASK-037) — même endpoint
// GET /api/rapprochement (TASK-036/039/040), intouchable : on ne fait
// qu'ajouter le scope période + la sélection + le total côté front.
//
// Filtre MV_Domaine IN (0,1) déjà appliqué par le back (TASK-039) — jamais
// MV_DECAISSE=1 (trou ~465 factures, mémoire grf-trou-selection-mv-decaisse).

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

type ListFilterValue = string | string[];

type Col = {
  key: string;
  label: string;
  align?: 'left' | 'right' | 'center';
  sortKey?: 'date' | 'montant' | 'reste';
  filterType?: 'list' | 'text' | 'date';
  width?: string;
};

// Clé de sélection stable : la projection TASK-036 n'expose pas MV_Id (id technique
// du mouvement), seulement des champs métier. Composite pour limiter les collisions —
// limitation front assumée, l'endpoint est intouchable dans le périmètre de cette task.
export const reglementKey = (row: ReglementRow) => `${row.numeroReglement}__${row.date}__${row.montant}`;

type Statut = 'eligible' | 'controle' | 'bloque';

// Statut dérivé — 3 valeurs. Principe « jamais masqué » (mémoire grf-valorisation-tracabilite-blocage-om),
// avec UNE exception ciblée et voulue par le PO (TASK-140) : le cas « déjà déclaré » (row.declare) est
// désormais RETIRÉ du jeu de données de l'écran ① Sélection en amont (cf. fetchAll), sa traçabilité
// déplacée vers l'écran Rapprochement global (numéro de déclaration). Les autres cas `bloque` restent
// affichés. La branche `row.declare` ci-dessous demeure (statut/badge cohérents si une ligne déclarée
// devait réapparaître), mais n'est plus atteinte en pratique dans cet écran.
//  - bloque   : déjà déclaré (verrou DT_Id, TASK-028) OU aucune affectation (rien à valoriser) ;
//  - controle : affectation partielle (reste à affecter ≠ 0) — nécessite un contrôle avant sélection ;
//  - eligible : entièrement affecté et non déclaré.
// Remarque périmètre : la conformité IF/ICE (TASK-027) est calculée au niveau FACTURE, absente de
// cette projection règlement-pivot — on ne fabrique pas de valeur ; seuls les champs existants
// (nbFacturesAffectees, resteAAffecter, declare) alimentent la règle.
function statutDe(row: ReglementRow): Statut {
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
    // TASK-141 : rendre visible LE motif unique du statut « À contrôler »
    // (affectation partielle) — la donnée resteAAffecter existe déjà, on la restitue.
    title = `Reste à affecter : ${formatMoney(row.resteAAffecter ?? 0)}`;
  }
  return (
    <span title={title} style={{ background: m.bg, color: m.text, padding: '2px 8px', borderRadius: '99px', fontSize: '0.7rem', fontWeight: 600, display: 'inline-flex', alignItems: 'center', gap: '0.25rem' }}>
      <Icon size={11} />{label}
    </span>
  );
}

// Colonne « Affecté » parlante — dérivée de nbFacturesAffectees/montantAffecte/montant,
// sans fabriquer de donnée absente de la projection (ex. pas de motif IF/ICE, cf. ci-dessus).
function affecteLabel(row: ReglementRow): string {
  if (row.nbFacturesAffectees === 0) return '—';
  if (Math.abs(row.resteAAffecter ?? 0) <= 0.005) {
    return `${row.nbFacturesAffectees} facture${row.nbFacturesAffectees > 1 ? 's' : ''}`;
  }
  const pct = row.montant > 0 ? Math.round((row.montantAffecte / row.montant) * 100) : 0;
  // TASK-141 : l'arrondi entier peut afficher « 100 % » alors qu'un reste non nul subsiste
  // (sinon le statut serait Éligible). On adjoint le reste réel pour lever l'ambiguïté.
  return `partiel ${pct} % (reste ${formatMoney(row.resteAAffecter ?? 0)})`;
}

function DomaineBadge({ domaine }: { domaine: string }) {
  const map: Record<string, { bg: string; text: string }> = {
    Encaissement: { bg: '#e0f2fe', text: '#0369a1' }, // bleu
    Décaissement: { bg: '#f3e8ff', text: '#6b21a8' }, // violet
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

const COLUMNS: Col[] = [
  { key: 'numeroReglement', label: 'N° Règlement', filterType: 'list', width: '140px' },
  { key: 'date', label: 'Date', sortKey: 'date', width: '100px' },
  { key: 'echeance', label: 'Échéance', filterType: 'date', width: '110px' },
  { key: 'mode', label: 'Mode', filterType: 'list', width: '100px' },
  { key: 'domaine', label: 'Domaine', filterType: 'list', width: '120px' },
  { key: 'tiers', label: 'Tiers', filterType: 'list' },
  { key: 'montant', label: 'Montant', align: 'right', sortKey: 'montant', width: '130px' },
  { key: 'affecte', label: 'Affecté', align: 'center', width: '120px' },
  { key: 'rapprocheBanque', label: 'Rappr. banque', align: 'center', filterType: 'list', width: '120px' },
  { key: 'dateRapprochement', label: 'Date rappro', filterType: 'date', width: '120px' },
  { key: 'statut', label: 'Statut', align: 'center', width: '130px', filterType: 'list' },
];

const ETAT_OPTIONS: { label: string; value: string }[] = [
  { label: 'Éligible', value: 'eligible' },
  { label: 'À contrôler', value: 'controle' },
  { label: 'Bloqué', value: 'bloque' },
];

export function periodeBounds(exercice: number, type: number, periode: number): { debut: string; fin: string } {
  // TypePeriode : 0 = Mensuelle (periode = mois 1-12), 1 = Trimestrielle (periode = trimestre 1-4).
  const pad = (n: number) => String(n).padStart(2, '0');
  const formatDateLocal = (d: Date) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
  if (type === 1) {
    const moisDebut = (periode - 1) * 3 + 1;
    const debut = `${exercice}-${pad(moisDebut)}-01`;
    const finMoisIdx = moisDebut + 2; // dernier mois du trimestre (1-12)
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

  // Jeu COMPLET des règlements de la période (filtres serveur mode/rapprocheBanque appliqués),
  // chargé intégralement — pas de pagination serveur ici. Écran ① borné à UNE déclaration
  // (contrairement à l'interrogation globale TASK-037, non bornée) : le volume par mois/trimestre
  // reste maîtrisable, donc on peut filtrer l'État/tiers/numéro (list Excel-like, TASK-067A) et
  // recompter dessus SANS jamais désynchroniser compteur/liste (leçon TASK-040 — un filtre qui ne
  // porterait que sur une page romprait cette cohérence).
  const [allData, setAllData] = useState<ReglementRow[]>([]);
  const [loading, setLoading] = useState(false);

  const [filters, setFilters] = useState<Record<string, ListFilterValue>>({});
  const [sortConfig, setSortConfig] = useState<{ key: 'date' | 'montant' | 'reste'; desc: boolean }>({ key: 'date', desc: true });
  const [modeOptions, setModeOptions] = useState<{ label: string; value: string }[]>([]);

  // Registre de toutes les lignes chargées (across full period fetch), pour recomposer le total
  // vivant même quand une ligne sort du jeu affiché après filtrage.
  const knownRowsRef = useRef<Map<string, ReglementRow>>(new Map());

  // Initialiser knownRowsRef avec les lignes déjà sélectionnées passées par le parent
  useEffect(() => {
    selectedRows.forEach(row => {
      const key = reglementKey(row);
      if (!knownRowsRef.current.has(key)) {
        knownRowsRef.current.set(key, row);
      }
    });
  }, [selectedRows]);

  const parentRef = useRef<HTMLDivElement>(null);

  const { visibleColumns, visibleKeys, toggle: toggleColumn, reset: resetColumns } = useColumnPrefs('grf.cols.reglements', COLUMNS);

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

  // TASK-125 : `cancelled` évite qu'une réponse tardive et périmée (ex. après une rafale de
  // rendus/refetch déclenchée par un showToast non stabilisé, TASK-125) n'écrase un résultat plus
  // frais — même garde que l'effet voisin `distincts` ci-dessus.
  const fetchAll = useCallback(async (cancelled: { current: boolean }) => {
    setLoading(true);
    try {
      const params: any = {
        debut,
        fin,
        sort: `${sortConfig.key}_${sortConfig.desc ? 'desc' : 'asc'}`,
        // Règlement-first : périmètre décaissement + encaissement pour l'écran ①
        // (le back applique de toute façon MV_Domaine IN (0,1), jamais MV_DECAISSE).
        domaine: ['Décaissement', 'Encaissement'],
        declarationId,
      };
      const mode = filters['mode'];
      if (Array.isArray(mode) && mode.length > 0) params.mode = mode[0];
      const rb = filters['rapprocheBanque'];
      if (Array.isArray(rb) && rb.length > 0) params.rapprocheBanque = rb[0];
      // tiers/numeroReglement filtrés côté client (list Excel-like, TASK-067A) — jamais envoyés
      // au serveur, cf. filtrage sur allData ci-dessous.

      // Plages de dates (échéance pièce / date de rapprochement) encodées « min~max »
      // par ExcelFilter → deux query params NULL-safe, mêmes clés que l'interrogation
      // Rapprochement (TASK-063) sur le même endpoint. Filtre serveur : jamais menteur.
      const setDateRange = (v: ListFilterValue | undefined, minKey: string, maxKey: string) => {
        if (typeof v !== 'string') return;
        const [min, max] = v.split('~');
        if ((min || '').trim() !== '') params[minKey] = min.trim();
        if ((max || '').trim() !== '') params[maxKey] = max.trim();
      };
      setDateRange(filters['echeance'], 'echeanceMin', 'echeanceMax');
      setDateRange(filters['dateRapprochement'], 'dateRappMin', 'dateRappMax');

      // Boucle de pagination serveur interne, sans plafond silencieux : on récupère l'intégralité
      // du jeu correspondant aux filtres serveur avant d'appliquer le filtre État côté client.
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
      // TASK-140 : les règlements déjà déclarés (verrou DT_Id, statut « Déjà déclaré (Bloqué) »)
      // sont RETIRÉS du jeu de l'écran ① Sélection — sans valeur pour une NOUVELLE déclaration, ils
      // encombraient la lecture (signalement PO, TVA1-2026-02). Exception CIBLÉE au principe « jamais
      // masqué » (mémoire grf-valorisation-tracabilite-blocage-om) : la traçabilité n'est pas perdue,
      // le numéro de la déclaration verrou est désormais visible dans l'écran Rapprochement global.
      // Filtrage STRICTEMENT front (l'endpoint partagé reste intouché — cf. en-tête, TASK-054). Les
      // AUTRES statuts `bloque` (non affecté, « Autre (impayé) ») restent affichés (non-régression).
      const visibles = items.filter(r => !r.declare);
      setAllData(visibles);
      visibles.forEach(r => knownRowsRef.current.set(reglementKey(r), r));
    } catch (e) {
      if (cancelled.current) return;
      console.error(e);
      showToast('Erreur lors du chargement des règlements', 'error');
      setAllData([]);
    } finally {
      if (!cancelled.current) setLoading(false);
    }
  }, [debut, fin, filters, sortConfig, showToast, societeId, declarationId]);

  useEffect(() => {
    const cancelled = { current: false };
    fetchAll(cancelled);
    return () => { cancelled.current = true; };
  }, [fetchAll]);

  const initializedRef = useRef(false);

  useEffect(() => {
    initializedRef.current = false;
  }, [declarationId]);

  useEffect(() => {
    if (savedSelection === null || allData.length === 0 || loading) return;

    if (!initializedRef.current) {
      // If the parent already has a selection, preserve it instead of loading default or saved selection
      if (selectedKeys.size > 0) {
        initializedRef.current = true;
        return;
      }

      if (savedSelection.length > 0) {
        // Restaurer la sélection sauvegardée
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
      } else {
        // Nouvelle déclaration : coche tout par défaut
        const keys = new Set<string>();
        const rows: ReglementRow[] = [];
        allData.forEach(r => {
          if (statutDe(r) !== 'bloque') {
            const key = reglementKey(r);
            keys.add(key);
            rows.push(r);
          }
        });
        onSelectionChange(keys, rows);
      }
      initializedRef.current = true;
    }
  }, [allData, loading, savedSelection, onSelectionChange, selectedKeys.size]);

  // Filtre État — dérivé, absent du DTO serveur : appliqué ici sur le jeu COMPLET (jamais une
  // page), donc le compteur affiché reste toujours exact vis-à-vis de la liste rendue.
  const etatFilter = filters['statut'];
  const tiersFilter = filters['tiers'];
  const numeroFilter = filters['numeroReglement'];
  const domaineFilter = filters['domaine'];
  const data = useMemo(() => {
    let result = allData;
    if (Array.isArray(etatFilter) && etatFilter.length > 0) result = result.filter(r => etatFilter.includes(statutDe(r)));
    if (Array.isArray(tiersFilter) && tiersFilter.length > 0) result = result.filter(r => tiersFilter.includes(r.tiers));
    if (Array.isArray(numeroFilter) && numeroFilter.length > 0) result = result.filter(r => numeroFilter.includes(r.numeroReglement));
    if (Array.isArray(domaineFilter) && domaineFilter.length > 0) result = result.filter(r => domaineFilter.includes(r.domaine));
    return result;
  }, [allData, etatFilter, tiersFilter, numeroFilter, domaineFilter]);

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

  const filterOptionsFor = (key: string): { label: string; value: string }[] => {
    if (key === 'mode') return modeOptions;
    if (key === 'rapprocheBanque') return [{ label: 'Oui', value: 'true' }, { label: 'Non', value: 'false' }];
    if (key === 'statut') return ETAT_OPTIONS;
    if (key === 'tiers') return [...new Set(allData.map(r => r.tiers))].filter(Boolean).sort().map(v => ({ label: v, value: v }));
    if (key === 'numeroReglement') return [...new Set(allData.map(r => r.numeroReglement))].filter(Boolean).sort().map(v => ({ label: v, value: v }));
    if (key === 'domaine') return [...new Set(allData.map(r => r.domaine))].filter(Boolean).sort().map(v => ({ label: v, value: v }));
    return [];
  };

  const toggleRow = (row: ReglementRow) => {
    if (statutDe(row) === 'bloque') return; // lecture seule stricte : jamais sélectionnable
    const key = reglementKey(row);
    const next = new Set(selectedKeys);
    if (next.has(key)) next.delete(key);
    else next.add(key);
    const rows = [...next].map(k => knownRowsRef.current.get(k)).filter(Boolean) as ReglementRow[];
    onSelectionChange(next, rows);
  };

  const selectableVisible = data.filter(r => statutDe(r) !== 'bloque');
  const allVisibleSelected = selectableVisible.length > 0 && selectableVisible.every(r => selectedKeys.has(reglementKey(r)));
  const toggleAllVisible = () => {
    const next = new Set(selectedKeys);
    if (allVisibleSelected) {
      selectableVisible.forEach(r => next.delete(reglementKey(r)));
    } else {
      selectableVisible.forEach(r => next.add(reglementKey(r)));
    }
    const rows = [...next].map(k => knownRowsRef.current.get(k)).filter(Boolean) as ReglementRow[];
    onSelectionChange(next, rows);
  };

  const renderCell = (col: Col, row: ReglementRow) => {
    switch (col.key) {
      case 'date': return formatDate(row.date);
      case 'echeance': return row.echeance ? formatDate(row.echeance) : <span style={{ color: 'var(--text-secondary)' }}>—</span>;
      case 'domaine': return <DomaineBadge domaine={row.domaine} />;
      case 'dateRapprochement': return row.dateRapprochement ? formatDate(row.dateRapprochement) : <span style={{ color: 'var(--text-secondary)' }}>—</span>;
      case 'montant': return formatMoney(row.montant);
      case 'affecte': return affecteLabel(row);
      case 'rapprocheBanque': return row.rapprocheBanque
        ? <span style={{ color: 'var(--status-ok-text)', fontWeight: 600 }}>Oui</span>
        : <span style={{ color: 'var(--text-secondary)' }}>Non</span>;
      case 'statut': return <StatutBadge row={row} />;
      default: return (row as any)[col.key];
    }
  };

  const activeFilterCount = Object.keys(filters).length;

  const selectedTotal = useMemo(() => {
    return selectedRows.reduce((sum, r) => sum + r.montant, 0);
  }, [selectedRows]);

  const colStyle = (col: Col): React.CSSProperties =>
    col.width ? { flex: `0 0 ${col.width}`, width: col.width } : { flex: '1 1 0', minWidth: '160px' };
  const colJustify = (col: Col) =>
    col.align === 'right' ? 'flex-end' : col.align === 'center' ? 'center' : 'flex-start';

  return (
    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', height: '100%', overflow: 'hidden' }}>
      <div ref={parentRef} style={{ flexGrow: 1, overflow: 'auto', position: 'relative', background: 'white' }}>
        <div style={{ minWidth: '1350px', fontSize: '0.8125rem' }}>
          <div style={{ display: 'flex', position: 'sticky', top: 0, background: 'var(--bg-secondary)', zIndex: 10, boxShadow: '0 1px 2px rgba(0,0,0,0.05)', borderBottom: '1px solid var(--border-color)' }}>
            <div style={{ flex: '0 0 40px', width: '40px', padding: '0.5rem 0.75rem', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
              <input type="checkbox" checked={allVisibleSelected} onChange={toggleAllVisible} title="Sélectionner toutes les lignes visibles (éligibles/à contrôler)" />
            </div>
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

          <div style={{ height: `${rowVirtualizer.getTotalSize()}px`, position: 'relative' }}>
            {rowVirtualizer.getVirtualItems().map(virtualRow => {
              const row = data[virtualRow.index];
              if (!row) return null;
              const key = reglementKey(row);
              const bloque = statutDe(row) === 'bloque';
              const checked = selectedKeys.has(key);
              return (
                <div
                  key={`${key}-${virtualRow.index}`}
                  style={{
                    position: 'absolute', top: 0, left: 0, width: '100%',
                    transform: `translateY(${virtualRow.start}px)`,
                    height: `${virtualRow.size}px`,
                    display: 'flex',
                    borderBottom: '1px solid var(--border-color)',
                    background: checked ? '#eff6ff' : 'white',
                    cursor: bloque ? 'default' : 'pointer',
                  }}
                  onClick={() => toggleRow(row)}
                >
                  <div style={{ flex: '0 0 40px', width: '40px', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                    <input type="checkbox" checked={checked} disabled={bloque} onChange={() => toggleRow(row)} onClick={e => e.stopPropagation()} />
                  </div>
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
            Aucun règlement décaissé pour cette période / ces filtres.
          </div>
        )}
      </div>

      <div style={{ padding: '0.5rem 1rem', borderTop: '1px solid var(--border-color)', display: 'flex', justifyContent: 'space-between', alignItems: 'center', background: 'white', gap: '0.75rem', flexWrap: 'wrap', fontSize: '0.8rem' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
          {loading && <Loader2 size={15} className="animate-spin" style={{ color: 'var(--accent-primary)' }} />}
          <span>Règlements : <strong>{data.length}</strong>{activeFilterCount > 0 && data.length !== allData.length && <> sur {allData.length}</>}</span>
          <span>Sélectionnés : <strong>{selectedKeys.size}</strong></span>
          {activeFilterCount > 0 && (
            <button className="btn" onClick={() => setFilters({})} style={{ background: 'transparent', border: 'none', color: 'var(--accent-primary)', textDecoration: 'underline', padding: 0, fontSize: '0.8rem', cursor: 'pointer' }}>
              Effacer filtres ({activeFilterCount})
            </button>
          )}
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
          <span style={{ fontSize: '0.85rem', fontWeight: 600 }}>Total sélectionné : {formatMoney(selectedTotal)}</span>
          <ColumnSelector columns={COLUMNS} visibleKeys={visibleKeys} onToggle={toggleColumn} onReset={resetColumns} />
        </div>
      </div>
    </div>
  );
}
