import { useState, useEffect, useMemo, Fragment, type CSSProperties } from 'react';
import { Loader2, GitMerge, AlertTriangle } from 'lucide-react';
import { ExcelFilter } from './ExcelFilter';
import { ColumnSelector } from './ColumnSelector';
import { useColumnPrefs } from './useColumnPrefs';
import { formatMoney } from './utils';
import api from './api';
import type { ReglementRow } from './ReglementsSelection';

// ─── Écran ② Affectations (TASK-055) ───────────────────────────────────────
//
// Drill règlement→factures + prorata multi-taux. Unité métier = l'affectation
// règlement→facture, pas la facture seule (reflexion dectva.md §3). Consomme
// tel quel GET /declarations/{id}/lignes (TASK-034/038/055) — aucun recalcul
// front, aucun nouveau chemin de valorisation (anti-régression de la task).
//
// `prorata`/`montantAffecte` sont des sorties DIRECTES de Declaration.Core
// (Ventilateur.Ventiler), exposées sans transformation. Les deux seuls calculs
// faits ici sont des INVERSIONS algébriques pures pour la traçabilité écran
// (retrouver l'opérande source à partir du résultat déjà produit par le back) :
//   ttcFactureOrigine = montantAffecte / (prorata / 100)
//   tvaSageBrute      = montantTVA     / (prorata / 100)
// Aucun de ces deux nombres ne remplace ni ne recalcule montantHT/montantTVA/
// montantTTC affichés — ceux-ci restent strictement les valeurs du back.
//
// Rendu = grille dense unique filtrable par colonne (ExcelFilter), même patron
// que l'interrogation « Rapprochement bancaire » (TASK-037). Filtres CLIENT :
// le jeu est déjà chargé intégralement en mémoire (une déclaration bornée), donc
// compteur et total restent toujours exacts vis-à-vis des lignes rendues.

type LigneAffectation = {
  id: string;
  factureNumero: string;
  numeroRapprochement: string;
  tiers: string;
  tiersIdentifiantFiscal: string;
  tiersICE: string;
  montantHT: number;
  tauxTVA: number;
  montantTVA: number;
  montantTTC: number;
  prorata: number;
  montantAffecte: number;
  source: string;
  statutLigne: number; // 0 Proposée, 1 Intégrée, 2 Exclue, 3 Reportée, 4 Écartée
  motif: string;
  statutConformite: string;
  origine: string;
  // TASK-078 : clé de la pièce Sage + état de traçabilité de la décision PO sur une
  // incohérence déjà signalée (TASK-077). ecId=0 = clé inconnue (ligne figée avant TASK-077).
  ecId: number;
  incoherenceValidee: boolean;
};

// Jamais un TVA=0 silencieux (mémoire grf-valorisation-tracabilite-blocage-om) :
// ces 3 états portent un motif explicite, affiché à la place d'un prorata muet.
const NON_VALORISE = new Set([2, 3, 4]);

type PageFetchResult = { items: LigneAffectation[]; alertes: AlertRevalidationRaw[] };
type AlertRevalidationRaw = { type: string; message: string; code: string; refLigne: string };

async function fetchLignesReglement(declarationId: string, numeroRapprochement: string): Promise<PageFetchResult> {
  const size = 200;
  let page = 1;
  let items: LigneAffectation[] = [];
  let alertes: AlertRevalidationRaw[] = [];
  for (;;) {
    const res = await api.get(`/declarations/${declarationId}/lignes`, {
      params: {
        domaine: 'Decaissement',
        page,
        size,
        filter: JSON.stringify({ numeroRapprochement }),
      },
    });
    const chunk: LigneAffectation[] = res.data.items || [];
    items = items.concat(chunk);
    alertes = alertes.concat(res.data.alertes || []);
    const total = res.data.totalCount ?? chunk.length;
    if (chunk.length === 0 || items.length >= total) break;
    page += 1;
  }
  return { items, alertes };
}

// Relecture après intégration (TASK-075) : aucune sélection de règlements en
// session (le state local n'est jamais réhydraté au rechargement), donc on
// charge TOUTES les lignes de la déclaration figée par declarationId — même
// endpoint que le parcours normal, juste sans filtre de règlement.
async function fetchAllLignesDeclaration(declarationId: string): Promise<PageFetchResult> {
  const size = 500;
  let page = 1;
  let items: LigneAffectation[] = [];
  let alertes: AlertRevalidationRaw[] = [];
  for (;;) {
    const res = await api.get(`/declarations/${declarationId}/lignes`, {
      params: { domaine: 'Decaissement', page, size },
    });
    const chunk: LigneAffectation[] = res.data.items || [];
    items = items.concat(chunk);
    alertes = alertes.concat(res.data.alertes || []);
    const total = res.data.totalCount ?? chunk.length;
    if (chunk.length === 0 || items.length >= total) break;
    page += 1;
  }
  return { items, alertes };
}

type FactureGroup = {
  factureNumero: string;
  tiers: string;
  tiersIdentifiantFiscal: string;
  tiersICE: string;
  origine: string;
  statutConformite: string;
  nonValorise: boolean;
  motif: string;
  montantAffecte: number;
  prorata: number;
  lignesTaux: LigneAffectation[];
  totalTva: number;
  ecId: number;
  incoherenceValidee: boolean;
};

function groupByFacture(lignes: LigneAffectation[]): FactureGroup[] {
  const map = new Map<string, LigneAffectation[]>();
  lignes.forEach(l => {
    const arr = map.get(l.factureNumero) || [];
    arr.push(l);
    map.set(l.factureNumero, arr);
  });
  return [...map.entries()]
    .map(([factureNumero, ls]) => {
      const first = ls[0];
      const nonValorise = NON_VALORISE.has(first.statutLigne);
      return {
        factureNumero,
        tiers: first.tiers,
        tiersIdentifiantFiscal: first.tiersIdentifiantFiscal,
        tiersICE: first.tiersICE,
        origine: first.origine,
        statutConformite: first.statutConformite,
        nonValorise,
        motif: first.motif,
        montantAffecte: first.montantAffecte,
        prorata: first.prorata,
        lignesTaux: nonValorise ? [] : ls,
        totalTva: nonValorise ? 0 : ls.reduce((s, l) => s + l.montantTVA, 0),
        ecId: first.ecId,
        incoherenceValidee: first.incoherenceValidee,
      };
    })
    .sort((a, b) => a.factureNumero.localeCompare(b.factureNumero));
}

// Inversion pure — cf. commentaire d'en-tête. `null` si prorata absent/nul
// (ligne non valorisée) : jamais une division par zéro déguisée en donnée.
function inverse(resultat: number, prorata: number): number | null {
  if (!prorata) return null;
  return resultat / (prorata / 100);
}

// Aplatit dataByReglement en 1 ligne par taux. Aucune donnée nouvelle : mêmes
// valeurs back que le drill groupé, mêmes inversions pures. Les factures non
// valorisées restent une ligne à motif visible (jamais absorbée en silence).
type GridRow = {
  key: string;
  numeroReglement: string;
  numeroRapprochement: string;
  reglementTiers: string;
  factureNumero: string;
  tiers: string;
  origine: string;
  statutConformite: string;
  nonValorise: boolean;
  motif: string;
  tauxTVA: number | null;
  prorata: number;
  paye: number;
  ttc: number | null;
  baseTva: number | null;
  tva: number;
  ecId: number;
  incoherenceValidee: boolean;
};

function buildGridRows(
  dataByReglement: Record<string, LigneAffectation[]>,
  reglementInfo: Record<string, { tiers: string }>,
): GridRow[] {
  const rows: GridRow[] = [];
  Object.entries(dataByReglement).forEach(([numeroReglement, lignes]) => {
    const reglementTiers = reglementInfo[numeroReglement]?.tiers ?? '';
    groupByFacture(lignes).forEach(f => {
      const numeroRapprochement = f.lignesTaux[0]?.numeroRapprochement ?? numeroReglement;
      if (f.nonValorise) {
        rows.push({
          key: `${numeroReglement}|${f.factureNumero}`,
          numeroReglement, numeroRapprochement, reglementTiers,
          factureNumero: f.factureNumero, tiers: f.tiers, origine: f.origine,
          statutConformite: f.statutConformite, nonValorise: true, motif: f.motif,
          tauxTVA: null, prorata: f.prorata, paye: f.montantAffecte, ttc: null,
          baseTva: null, tva: 0, ecId: f.ecId, incoherenceValidee: f.incoherenceValidee,
        });
        return;
      }
      const ttc = inverse(f.montantAffecte, f.prorata);
      f.lignesTaux.forEach(l => {
        rows.push({
          key: l.id,
          numeroReglement, numeroRapprochement, reglementTiers,
          factureNumero: f.factureNumero, tiers: f.tiers, origine: f.origine,
          statutConformite: f.statutConformite, nonValorise: false, motif: '',
          tauxTVA: l.tauxTVA, prorata: f.prorata, paye: f.montantAffecte, ttc,
          baseTva: inverse(l.montantTVA, l.prorata), tva: l.montantTVA,
          ecId: f.ecId, incoherenceValidee: f.incoherenceValidee,
        });
      });
    });
  });
  return rows;
}

// ─── Grille filtrable (patron « Rapprochement bancaire », TASK-037) ─────────
type ListFilterValue = string | string[];

type GCol = {
  key: string;
  label: string;
  align?: 'left' | 'right' | 'center';
  filterType?: 'list' | 'text' | 'number';
  width?: string;
};

const GRID_COLUMNS: GCol[] = [
  { key: 'numeroReglement', label: 'Règlement', filterType: 'list', width: '130px' },
  { key: 'factureNumero', label: 'Facture', filterType: 'list', width: '120px' },
  { key: 'tiers', label: 'Tiers', filterType: 'list' },
  { key: 'origine', label: 'Orig.', filterType: 'list', width: '90px' },
  { key: 'statutConformite', label: 'Conf.', align: 'center', filterType: 'list', width: '110px' },
  { key: 'tauxTVA', label: 'Taux', align: 'right', filterType: 'list', width: '90px' },
  { key: 'paye', label: 'Payé', align: 'right', filterType: 'number', width: '120px' },
  { key: 'ttc', label: 'TTC', align: 'right', filterType: 'number', width: '120px' },
  { key: 'prorata', label: 'Prorata', align: 'right', filterType: 'number', width: '100px' },
  { key: 'baseTva', label: 'Base TVA', align: 'right', filterType: 'number', width: '120px' },
  { key: 'tva', label: 'TVA déclarée', align: 'right', filterType: 'number', width: '130px' },
];

const COL_BY_KEY = new Map(GRID_COLUMNS.map(c => [c.key, c] as const));

function rawCell(row: GridRow, key: string): string | number | null {
  switch (key) {
    case 'tauxTVA': return row.tauxTVA;
    case 'paye': return row.paye;
    case 'ttc': return row.ttc;
    case 'prorata': return row.prorata;
    case 'baseTva': return row.baseTva;
    case 'tva': return row.tva;
    default: return (row as unknown as Record<string, string>)[key] ?? '';
  }
}

function rowMatches(row: GridRow, filters: Record<string, ListFilterValue>): boolean {
  for (const [key, val] of Object.entries(filters)) {
    if (val == null || (Array.isArray(val) && val.length === 0) || val === '') continue;
    const col = COL_BY_KEY.get(key);
    const cell = rawCell(row, key);
    if (col?.filterType === 'list' && Array.isArray(val)) {
      const s = cell == null ? '' : String(cell);
      if (!val.includes(s)) return false;
    } else if (col?.filterType === 'number' && typeof val === 'string') {
      const [min, max] = val.split('~');
      const n = typeof cell === 'number' ? cell : null;
      if (n == null) return false;
      if (min.trim() !== '' && n < parseFloat(min)) return false;
      if (max.trim() !== '' && n > parseFloat(max)) return false;
    } else if (typeof val === 'string') {
      const s = cell == null ? '' : String(cell);
      if (!s.toLowerCase().includes(val.toLowerCase())) return false;
    }
  }
  return true;
}

export function AffectationsDrill({
  declarationId,
  selectedRows,
  readOnly = false,
  showToast,
}: {
  declarationId: string;
  selectedRows: ReglementRow[];
  /** Relecture après intégration (TASK-075) : ignore selectedRows, charge tout par declarationId */
  readOnly?: boolean;
  showToast: (m: string, t?: 'success' | 'error' | 'warning') => void;
}) {
  const [dataByReglement, setDataByReglement] = useState<Record<string, LigneAffectation[]>>({});
  const [alertesIncoherence, setAlertesIncoherence] = useState<AlertRevalidationRaw[]>([]);
  const [loading, setLoading] = useState(false);
  const [filters, setFilters] = useState<Record<string, ListFilterValue>>({});
  // TASK-078 : bascule le badge « N ligne(s) incohérente(s) » vers un filtre affichant
  // UNIQUEMENT ces lignes — le comptable n'a rien à chercher, juste à cliquer le badge.
  const [showOnlyIncoherent, setShowOnlyIncoherent] = useState(false);
  // Retour immédiat après « Resynchroniser » (avant le prochain rechargement complet).
  const [resyncEnCours, setResyncEnCours] = useState<Set<number>>(new Set());

  const { visibleColumns, visibleKeys, toggle: toggleColumn, reset: resetColumns } = useColumnPrefs('grf.cols.affectations', GRID_COLUMNS);
  const isColVisible = (key: string) => visibleKeys.has(key);

  const reload = async () => {
    setLoading(true);
    try {
      if (readOnly) {
        const { items: allLignes, alertes } = await fetchAllLignesDeclaration(declarationId);
        const grouped: Record<string, LigneAffectation[]> = {};
        allLignes.forEach(l => {
          const key = l.numeroRapprochement || l.id;
          (grouped[key] ||= []).push(l);
        });
        setDataByReglement(grouped);
        setAlertesIncoherence(alertes.filter(a => a.code === 'LIGNE_FIGEE_A_REVERIFIER'));
      } else {
        const entries = await Promise.all(
          selectedRows.map(async r => [r.numeroReglement, await fetchLignesReglement(declarationId, r.numeroReglement)] as const)
        );
        setDataByReglement(Object.fromEntries(entries.map(([k, v]) => [k, v.items])));
        setAlertesIncoherence(
          entries.flatMap(([, v]) => v.alertes).filter(a => a.code === 'LIGNE_FIGEE_A_REVERIFIER')
        );
      }
    } catch (e) {
      console.error(e);
      showToast('Erreur lors du chargement des affectations', 'error');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      try {
        if (readOnly) {
          const { items: allLignes, alertes } = await fetchAllLignesDeclaration(declarationId);
          if (cancelled) return;
          const grouped: Record<string, LigneAffectation[]> = {};
          allLignes.forEach(l => {
            const key = l.numeroRapprochement || l.id;
            (grouped[key] ||= []).push(l);
          });
          setDataByReglement(grouped);
          setAlertesIncoherence(alertes.filter(a => a.code === 'LIGNE_FIGEE_A_REVERIFIER'));
        } else {
          const entries = await Promise.all(
            selectedRows.map(async r => [r.numeroReglement, await fetchLignesReglement(declarationId, r.numeroReglement)] as const)
          );
          if (cancelled) return;
          setDataByReglement(Object.fromEntries(entries.map(([k, v]) => [k, v.items])));
          setAlertesIncoherence(
            entries.flatMap(([, v]) => v.alertes).filter(a => a.code === 'LIGNE_FIGEE_A_REVERIFIER')
          );
        }
      } catch (e) {
        console.error(e);
        showToast('Erreur lors du chargement des affectations', 'error');
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [declarationId, selectedRows, readOnly]);

  const handleValider = async (ecId: number) => {
    try {
      await api.post(`/declarations/${declarationId}/lignes/valider-incoherence`, { ecId });
      showToast('Incohérence validée — tracée, ligne et totaux inchangés', 'success');
      await reload();
    } catch (e) {
      console.error(e);
      showToast('Échec de la validation', 'error');
    }
  };

  const handleResynchroniser = async (ecId: number) => {
    setResyncEnCours(prev => new Set(prev).add(ecId));
    try {
      const res = await api.post(`/declarations/${declarationId}/lignes/resynchroniser`, { ecId });
      showToast(
        res.data?.resolue
          ? 'Resynchronisé — la pièce est désormais cohérente'
          : 'Resynchronisé — toujours incohérente après relecture Sage',
        res.data?.resolue ? 'success' : 'warning'
      );
      await reload();
    } catch (e) {
      console.error(e);
      showToast('Échec de la resynchronisation (relecture Sage)', 'error');
    } finally {
      setResyncEnCours(prev => { const next = new Set(prev); next.delete(ecId); return next; });
    }
  };

  const grandTotalTva = useMemo(() => {
    return Object.values(dataByReglement)
      .flat()
      .reduce((s, l) => s + (NON_VALORISE.has(l.statutLigne) ? 0 : l.montantTVA), 0);
  }, [dataByReglement]);

  // En relecture (readOnly), le tiers du règlement n'est pas dans les lignes
  // back et n'est affiché dans aucune colonne de la grille (cf. GRID_COLUMNS) :
  // un objet vide suffit, aucune perte d'information visible.
  const reglementInfo = useMemo(
    () => (readOnly ? {} : Object.fromEntries(selectedRows.map(r => [r.numeroReglement, { tiers: r.tiers }]))),
    [selectedRows, readOnly],
  );
  const allRows = useMemo(() => buildGridRows(dataByReglement, reglementInfo), [dataByReglement, reglementInfo]);

  // TASK-078 : factures signalées incohérentes (TASK-077, alerte non encore validée) — dérivé
  // du contrat d'alerte existant (refLigne = n° facture), aucune règle de détection dupliquée.
  const facturesIncoherentes = useMemo(
    () => new Set(alertesIncoherence.map(a => a.refLigne)),
    [alertesIncoherence],
  );
  const ecIdsIncoherents = useMemo(
    () => new Set(allRows.filter(r => facturesIncoherentes.has(r.factureNumero) && r.ecId > 0).map(r => r.ecId)),
    [allRows, facturesIncoherentes],
  );
  const nbIncoherentes = ecIdsIncoherents.size;

  const rowsApresIncoherence = useMemo(
    () => (showOnlyIncoherent ? allRows.filter(r => facturesIncoherentes.has(r.factureNumero)) : allRows),
    [allRows, facturesIncoherentes, showOnlyIncoherent],
  );
  const filteredRows = useMemo(() => rowsApresIncoherence.filter(r => rowMatches(r, filters)), [rowsApresIncoherence, filters]);
  const filteredTotalTva = useMemo(() => filteredRows.reduce((s, r) => s + r.tva, 0), [filteredRows]);
  const activeFilterCount = Object.keys(filters).length;

  // Options des filtres liste — dérivées des lignes chargées (jeu complet, pas une page).
  const distinct = (values: (string | number | null)[]) =>
    [...new Set(values.map(v => (v == null ? '' : String(v))))].sort();
  const filterOptionsFor = (key: string): { label: string; value: string }[] => {
    if (key === 'numeroReglement') return distinct(allRows.map(r => r.numeroReglement)).filter(Boolean).map(v => ({ label: v, value: v }));
    if (key === 'factureNumero') return distinct(allRows.map(r => r.factureNumero)).filter(Boolean).map(v => ({ label: v, value: v }));
    if (key === 'tiers') return distinct(allRows.map(r => r.tiers)).filter(Boolean).map(v => ({ label: v, value: v }));
    if (key === 'origine') return distinct(allRows.map(r => r.origine)).filter(Boolean).map(v => ({ label: v, value: v }));
    if (key === 'statutConformite') return distinct(allRows.map(r => r.statutConformite)).filter(Boolean).map(v => ({ label: v, value: v }));
    if (key === 'tauxTVA') {
      return [...new Set(allRows.map(r => r.tauxTVA).filter((t): t is number => t != null))]
        .sort((a, b) => a - b)
        .map(t => ({ label: `${t} %`, value: String(t) }));
    }
    return [];
  };

  const handleFilterChange = (key: string, val: ListFilterValue) => {
    setFilters(prev => {
      const next = { ...prev };
      if (val === '' || (Array.isArray(val) && val.length === 0)) delete next[key];
      else next[key] = val;
      return next;
    });
  };

  const colStyle = (col: GCol): CSSProperties =>
    col.width ? { flex: `0 0 ${col.width}`, width: col.width } : { flex: '1 1 0', minWidth: '160px' };
  const colJustify = (col: GCol) =>
    col.align === 'right' ? 'flex-end' : col.align === 'center' ? 'center' : 'flex-start';

  // Garde « rien sélectionné » (parcours normal ①→②→③, TASK-054) — ne doit pas
  // s'appliquer en relecture, où l'absence de sélection en session est normale.
  if (!readOnly && selectedRows.length === 0) {
    return (
      <div style={{ flex: 1, display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', textAlign: 'center', padding: '2rem', color: 'var(--text-secondary)', height: '100%' }}>
        <GitMerge size={48} style={{ opacity: 0.25, marginBottom: '1rem' }} />
        <p style={{ margin: 0, fontSize: '0.9rem' }}>Aucun règlement sélectionné — retournez à l'étape ① Règlements.</p>
      </div>
    );
  }

  // Garde « rien chargé » distincte (TASK-075, périmètre B) : en relecture, un
  // vide réel serait anormal pour une déclaration intégrée — message différent,
  // jamais confondu avec « aucune sélection en session ».
  if (readOnly && !loading && Object.keys(dataByReglement).length === 0) {
    return (
      <div style={{ flex: 1, display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', textAlign: 'center', padding: '2rem', color: 'var(--text-secondary)', height: '100%' }}>
        <GitMerge size={48} style={{ opacity: 0.25, marginBottom: '1rem' }} />
        <p style={{ margin: 0, fontSize: '0.9rem' }}>Aucune affectation trouvée pour cette déclaration.</p>
      </div>
    );
  }

  return (
    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', height: '100%', overflow: 'hidden' }}>
      {/* En-tête écran */}
      <div style={{ padding: '0.75rem 1rem', borderBottom: '1px solid var(--border-color)', background: 'white', display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: '0.75rem' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.6rem' }}>
          <GitMerge size={20} style={{ color: 'var(--accent-primary)' }} />
          <div>
            <h2 style={{ margin: 0, fontSize: '1.05rem', fontWeight: 600 }}>② Affectations</h2>
            <div style={{ fontSize: '0.72rem', color: 'var(--text-secondary)' }}>
              {readOnly ? Object.keys(dataByReglement).length : selectedRows.length} règlement{(readOnly ? Object.keys(dataByReglement).length : selectedRows.length) > 1 ? 's' : ''} · payé ÷ TTC = % puis TVA × % = TVA déclarée
            </div>
          </div>
          {loading && <Loader2 size={15} className="animate-spin" style={{ color: 'var(--accent-primary)' }} />}
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
          <span style={{ fontSize: '0.85rem', fontWeight: 600 }}>Total TVA (tous règlements) : {formatMoney(grandTotalTva)}</span>
          <ColumnSelector columns={GRID_COLUMNS} visibleKeys={visibleKeys} onToggle={toggleColumn} onReset={resetColumns} />
        </div>
      </div>

      {/* TASK-078 : bandeau incohérence — toujours visible, sans action de recherche du
          comptable, dès qu'une pièce signalée (TASK-077) n'a pas encore été validée. */}
      {nbIncoherentes > 0 && (
        <button
          onClick={() => setShowOnlyIncoherent(v => !v)}
          style={{
            display: 'flex', alignItems: 'center', gap: '0.5rem', width: '100%',
            padding: '0.6rem 1rem', border: 'none', borderBottom: '1px solid #fecaca',
            background: showOnlyIncoherent ? '#fecaca' : 'var(--status-blocking-bg)', color: '#7f1d1d',
            fontSize: '0.85rem', fontWeight: 700, cursor: 'pointer', textAlign: 'left',
          }}
        >
          <AlertTriangle size={16} style={{ flexShrink: 0 }} />
          {nbIncoherentes} ligne{nbIncoherentes > 1 ? 's' : ''} incohérente{nbIncoherentes > 1 ? 's' : ''} détectée{nbIncoherentes > 1 ? 's' : ''}
          — {showOnlyIncoherent ? 'clic pour tout revoir' : 'clic pour ne voir que ces lignes'}
        </button>
      )}

      {/* Barre d'info + reset filtres */}
      <div style={{ padding: '0.4rem 1rem', borderBottom: '1px solid var(--border-color)', background: 'var(--bg-secondary)', display: 'flex', justifyContent: 'space-between', alignItems: 'center', fontSize: '0.8rem' }}>
        <span>
          <strong>{filteredRows.length}</strong> ligne{filteredRows.length > 1 ? 's' : ''}
          {activeFilterCount > 0 && allRows.length !== filteredRows.length && <> sur {allRows.length}</>}
          {activeFilterCount > 0 && <> · TVA filtrée <strong>{formatMoney(filteredTotalTva)}</strong></>}
        </span>
        {activeFilterCount > 0 && (
          <button onClick={() => setFilters({})} style={{ background: 'transparent', border: 'none', color: 'var(--accent-primary)', textDecoration: 'underline', padding: 0, fontSize: '0.8rem', cursor: 'pointer' }}>
            Effacer filtres ({activeFilterCount})
          </button>
        )}
      </div>

      {/* Grille (flexbox : entête + lignes partagent les mêmes largeurs) */}
      <div style={{ flexGrow: 1, overflow: 'auto', position: 'relative', background: 'white' }}>
        <div style={{ minWidth: '1300px', fontSize: '0.8125rem' }}>
          {/* Entête collant */}
          <div style={{ display: 'flex', position: 'sticky', top: 0, background: 'var(--bg-secondary)', zIndex: 10, boxShadow: '0 1px 2px rgba(0,0,0,0.05)', borderBottom: '1px solid var(--border-color)' }}>
            {visibleColumns.map(col => (
              <div
                key={col.key}
                style={{ ...colStyle(col), padding: '0.5rem 0.75rem', borderRight: '1px solid var(--border-color)', userSelect: 'none', display: 'flex', alignItems: 'center', justifyContent: 'center', gap: '0.25rem', fontWeight: 600, whiteSpace: 'nowrap' }}
              >
                {col.label}
                {col.filterType && (
                  <ExcelFilter
                    filterType={col.filterType}
                    options={filterOptionsFor(col.key)}
                    selectedValues={Array.isArray(filters[col.key]) ? filters[col.key] as string[] : []}
                    textValue={typeof filters[col.key] === 'string' ? filters[col.key] as string : ''}
                    onChange={(val) => handleFilterChange(col.key, val)}
                  />
                )}
              </div>
            ))}
          </div>

          {/* Corps */}
          {filteredRows.map((r, i) => {
            const estIncoherente = r.ecId > 0 && facturesIncoherentes.has(r.factureNumero);
            const next = filteredRows[i + 1];
            const derniereLigneDuGroupe = !next || next.numeroReglement !== r.numeroReglement || next.factureNumero !== r.factureNumero;
            return (
              <Fragment key={r.key}>
                <div
                  style={{ display: 'flex', borderBottom: '1px solid var(--border-color)', background: estIncoherente ? '#fff1f2' : 'white' }}
                  onMouseEnter={e => (e.currentTarget.style.background = estIncoherente ? 'var(--status-blocking-bg)' : 'var(--bg-secondary)')}
                  onMouseLeave={e => (e.currentTarget.style.background = estIncoherente ? '#fff1f2' : 'white')}
                >
                  {r.nonValorise ? (
                    <>
                      {isColVisible('numeroReglement') && <GridCell col={COL_BY_KEY.get('numeroReglement')!}>{r.numeroReglement}</GridCell>}
                      {isColVisible('factureNumero') && <GridCell col={COL_BY_KEY.get('factureNumero')!}>{r.factureNumero}</GridCell>}
                      {isColVisible('tiers') && <GridCell col={COL_BY_KEY.get('tiers')!}>{r.tiers}</GridCell>}
                      {isColVisible('origine') && <GridCell col={COL_BY_KEY.get('origine')!}>{r.origine}</GridCell>}
                      <div style={{ flex: '1 1 auto', padding: '0.4rem 0.75rem', display: 'flex', alignItems: 'center', gap: '0.4rem', color: '#b45309', fontWeight: 600, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                        <AlertTriangle size={13} style={{ flexShrink: 0 }} />
                        Non valorisé — {r.motif || 'motif non renseigné'}
                      </div>
                    </>
                  ) : (
                    visibleColumns.map(col => (
                      <div key={col.key} style={{ ...colStyle(col), padding: '0.4rem 0.75rem', borderRight: '1px solid var(--border-color)', display: 'flex', alignItems: 'center', justifyContent: colJustify(col), whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                        {renderGridCell(col, r)}
                      </div>
                    ))
                  )}
                </div>
                {estIncoherente && derniereLigneDuGroupe && (
                  <div style={{ display: 'flex', alignItems: 'center', gap: '0.6rem', padding: '0.4rem 0.75rem 0.6rem', background: '#fff1f2', borderBottom: '2px solid #fecaca' }}>
                    <AlertTriangle size={13} style={{ color: 'var(--status-blocking-text)', flexShrink: 0 }} />
                    <span style={{ fontSize: '0.72rem', color: '#7f1d1d', flex: 1 }}>
                      Incohérence Sage détectée après figeage sur cette facture — décision requise avant clôture.
                    </span>
                    <button
                      onClick={() => handleValider(r.ecId)}
                      style={{ fontSize: '0.72rem', fontWeight: 600, padding: '0.25rem 0.6rem', borderRadius: '4px', border: '1px solid #d1d5db', background: 'white', color: 'var(--text-primary)', cursor: 'pointer' }}
                      title="Accepte l'incohérence en connaissance de cause — décision tracée (qui/quand), ligne et totaux inchangés"
                    >
                      Valider l'incohérence
                    </button>
                    <button
                      onClick={() => handleResynchroniser(r.ecId)}
                      disabled={resyncEnCours.has(r.ecId)}
                      style={{ fontSize: '0.72rem', fontWeight: 600, padding: '0.25rem 0.6rem', borderRadius: '4px', border: 'none', background: 'var(--status-blocking-text)', color: 'white', cursor: resyncEnCours.has(r.ecId) ? 'not-allowed' : 'pointer', opacity: resyncEnCours.has(r.ecId) ? 0.6 : 1 }}
                      title="Relit la pièce Sage maintenant (après correction côté ERP) et réévalue l'incohérence"
                    >
                      {resyncEnCours.has(r.ecId) ? 'Resynchronisation…' : 'Corriger / Resynchroniser'}
                    </button>
                  </div>
                )}
              </Fragment>
            );
          })}

          {/* Total */}
          {filteredRows.length > 0 && (
            <div style={{ display: 'flex', borderTop: '2px solid var(--border-color)', background: 'var(--bg-secondary)', fontWeight: 700 }}>
              <div style={{ flex: '1 1 auto', padding: '0.5rem 0.75rem', textAlign: 'right', fontSize: '0.8rem' }}>Total TVA déclarée</div>
              <div style={{ ...colStyle(visibleColumns[visibleColumns.length - 1]), padding: '0.5rem 0.75rem', display: 'flex', alignItems: 'center', justifyContent: 'flex-end', fontSize: '0.8rem' }}>
                {formatMoney(filteredTotalTva)}
              </div>
            </div>
          )}
        </div>

        {filteredRows.length === 0 && !loading && (
          <div style={{ padding: '3rem', textAlign: 'center', color: 'var(--text-secondary)' }}>
            Aucune affectation ne correspond aux filtres.
          </div>
        )}
      </div>
    </div>
  );
}

function GridCell({ col, children }: { col: GCol; children: React.ReactNode }) {
  const justify = col.align === 'right' ? 'flex-end' : col.align === 'center' ? 'center' : 'flex-start';
  const style: CSSProperties = col.width
    ? { flex: `0 0 ${col.width}`, width: col.width }
    : { flex: '1 1 0', minWidth: '160px' };
  return (
    <div style={{ ...style, padding: '0.4rem 0.75rem', borderRight: '1px solid var(--border-color)', display: 'flex', alignItems: 'center', justifyContent: justify, whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
      {children}
    </div>
  );
}

function renderGridCell(col: GCol, r: GridRow): React.ReactNode {
  switch (col.key) {
    case 'statutConformite':
      return (
        <span style={{ fontSize: '0.68rem', padding: '1px 6px', borderRadius: '99px', background: r.statutConformite === 'Conforme' ? 'var(--status-ok-bg)' : 'var(--status-blocking-bg)', color: r.statutConformite === 'Conforme' ? 'var(--status-ok-text)' : 'var(--status-blocking-text)' }}>
          {r.statutConformite}
        </span>
      );
    case 'tauxTVA': return r.tauxTVA == null ? '—' : `${r.tauxTVA}%`;
    case 'paye': return formatMoney(r.paye);
    case 'ttc': return r.ttc == null ? '—' : formatMoney(r.ttc);
    case 'prorata': return `${r.prorata.toFixed(2)}%`;
    case 'baseTva': return r.baseTva == null ? '—' : formatMoney(r.baseTva);
    case 'tva': return <strong>{formatMoney(r.tva)}</strong>;
    default: return (r as unknown as Record<string, string>)[col.key];
  }
}
