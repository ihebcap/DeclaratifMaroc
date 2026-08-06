import { useState, useEffect, useMemo } from 'react';
import { Loader2, GitMerge, AlertTriangle } from 'lucide-react';
import type { ColDef } from 'ag-grid-community';
import { ApbsGrid } from './grid/ApbsGrid';
import { CustomListFilter } from './grid/CustomListFilter';
import { formatMoney } from './utils';
import api from './api';
import type { ReglementRow } from './ReglementsSelection';

// ─── Écran ② Affectations (TASK-055 / TASK-204 AG Grid) ───────────────────────

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
  statutLigne: number;
  motif: string;
  statutConformite: string;
  origine: string;
  ecId: number;
  incoherenceValidee: boolean;
};

const NON_VALORISE = new Set([2, 3, 4]);

type PageFetchResult = { items: LigneAffectation[]; alertes: AlertRevalidationRaw[] };
type AlertRevalidationRaw = { type: string; message: string; code: string; refLigne: string };

function getApiDomaine(dom: string): string {
  if (dom === 'Encaissement') return 'Encaissement';
  return 'Decaissement';
}

async function fetchLignesPourReglements(declarationId: string, domaine: string, numeros: string[]): Promise<PageFetchResult> {
  const size = 500;
  let page = 1;
  let items: LigneAffectation[] = [];
  let alertes: AlertRevalidationRaw[] = [];
  for (;;) {
    const res = await api.get(`/declarations/${declarationId}/lignes`, {
      params: {
        domaine,
        page,
        size,
        filter: JSON.stringify({ numeroRapprochement: numeros }),
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

type SelectionFetchResult = {
  dataByReglement: Record<string, LigneAffectation[]>;
  alertes: AlertRevalidationRaw[];
  echecs: string[];
};

async function fetchLignesSelection(declarationId: string, selectedRows: ReglementRow[]): Promise<SelectionFetchResult> {
  const byDomaine: Record<string, string[]> = {};
  selectedRows.forEach(r => {
    const apiDom = getApiDomaine(r.domaine);
    (byDomaine[apiDom] ||= []).push(r.numeroReglement);
  });

  const echecs: string[] = [];
  const results = await Promise.allSettled(
    Object.entries(byDomaine).map(async ([dom, numeros]) => {
      const res = await fetchLignesPourReglements(declarationId, dom, numeros);
      return { dom, numeros, res };
    })
  );

  const grouped: Record<string, LigneAffectation[]> = {};
  const alertes: AlertRevalidationRaw[] = [];

  results.forEach((r, idx) => {
    const dom = Object.keys(byDomaine)[idx];
    const nums = byDomaine[dom] || [];
    if (r.status === 'fulfilled') {
      const { items, alertes: alt } = r.value.res;
      alertes.push(...alt);
      items.forEach(l => {
        const key = l.numeroRapprochement || l.id;
        (grouped[key] ||= []).push(l);
      });
    } else {
      echecs.push(...nums);
    }
  });

  return { dataByReglement: grouped, alertes, echecs };
}

async function fetchAllLignesDeclaration(declarationId: string): Promise<PageFetchResult> {
  const domaines = ['Decaissement', 'Encaissement'];
  const res = await Promise.all(
    domaines.map(d =>
      fetchLignesPourReglements(declarationId, d, [])
    )
  );
  return {
    items: res.flatMap(r => r.items),
    alertes: res.flatMap(r => r.alertes),
  };
}

function echecsMessage(echecs: string[]): string {
  if (echecs.length === 0) return '';
  if (echecs.length <= 3) return `Échec du chargement pour ${echecs.join(', ')}`;
  return `Échec du chargement pour ${echecs.length} règlements (dont ${echecs.slice(0, 3).join(', ')})`;
}

type GridRow = {
  key: string;
  numeroReglement: string;
  factureNumero: string;
  tiers: string;
  origine: string;
  statutConformite: string;
  incoherenceValidee: boolean;
  tauxTVA: number | null;
  paye: number;
  ttc: number | null;
  prorata: number;
  baseHt: number | null;
  baseTva: number | null;
  tva: number;
  nonValorise: boolean;
  motif?: string;
  ecId: number;
};

function buildGridRows(
  dataByReglement: Record<string, LigneAffectation[]>,
  reglementInfo: Record<string, { tiers?: string }>,
): GridRow[] {
  const rows: GridRow[] = [];
  Object.entries(dataByReglement).forEach(([numReg, lignes]) => {
    const regTiers = reglementInfo[numReg]?.tiers;
    lignes.forEach((l, idx) => {
      const nonVal = NON_VALORISE.has(l.statutLigne);
      const ratio = l.prorata > 0 ? l.prorata / 100 : 1;
      const ttcOrig = nonVal || l.prorata <= 0 ? null : l.montantAffecte / ratio;
      const baseTvaOrig = nonVal || l.prorata <= 0 ? null : l.montantTVA / ratio;

      rows.push({
        key: `${numReg}-${l.id}-${idx}`,
        numeroReglement: numReg,
        factureNumero: l.factureNumero || '—',
        tiers: l.tiers || regTiers || '—',
        origine: l.origine || 'Sage',
        statutConformite: l.statutConformite || 'Conforme',
        incoherenceValidee: !!l.incoherenceValidee,
        tauxTVA: nonVal ? null : l.tauxTVA,
        paye: l.montantAffecte,
        ttc: ttcOrig,
        prorata: l.prorata,
        baseHt: nonVal ? null : l.montantHT,
        baseTva: baseTvaOrig,
        tva: nonVal ? 0 : l.montantTVA,
        nonValorise: nonVal,
        motif: l.motif,
        ecId: l.ecId || 0,
      });
    });
  });
  return rows;
}

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
  { key: 'incoherenceValidee', label: 'Incoh.', align: 'center', filterType: 'list', width: '110px' },
  { key: 'tauxTVA', label: 'Taux', align: 'right', filterType: 'list', width: '90px' },
  { key: 'paye', label: 'Payé', align: 'right', filterType: 'number', width: '120px' },
  { key: 'ttc', label: 'TTC', align: 'right', filterType: 'number', width: '120px' },
  { key: 'prorata', label: 'Prorata', align: 'right', filterType: 'number', width: '100px' },
  { key: 'baseHt', label: 'Base HT', align: 'right', filterType: 'number', width: '120px' },
  { key: 'baseTva', label: 'TVA facture (avant prorata)', align: 'right', filterType: 'number', width: '160px' },
  { key: 'tva', label: 'TVA déclarée', align: 'right', filterType: 'number', width: '130px' },
];

export function AffectationsDrill({
  declarationId,
  selectedRows,
  readOnly = false,
  showToast,
}: {
  declarationId: string;
  selectedRows: ReglementRow[];
  readOnly?: boolean;
  showToast: (m: string, t?: 'success' | 'error' | 'warning') => void;
}) {
  const [dataByReglement, setDataByReglement] = useState<Record<string, LigneAffectation[]>>({});
  const [alertesIncoherence, setAlertesIncoherence] = useState<AlertRevalidationRaw[]>([]);
  const [loading, setLoading] = useState(false);
  const [echecsChargement, setEchecsChargement] = useState<string[]>([]);
  const [erreurChargement, setErreurChargement] = useState<string | null>(null);
  const [showOnlyIncoherent, setShowOnlyIncoherent] = useState(false);

  const reload = async () => {
    setLoading(true);
    setErreurChargement(null);
    setEchecsChargement([]);
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
        const { dataByReglement: grouped, alertes, echecs } = await fetchLignesSelection(declarationId, selectedRows);
        setDataByReglement(grouped);
        setAlertesIncoherence(alertes.filter(a => a.code === 'LIGNE_FIGEE_A_REVERIFIER'));
        setEchecsChargement(echecs);
        if (echecs.length > 0) showToast(echecsMessage(echecs), 'error');
      }
    } catch (e) {
      console.error(e);
      setErreurChargement('Erreur lors du chargement des affectations');
      showToast('Erreur lors du chargement des affectations', 'error');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      setErreurChargement(null);
      setEchecsChargement([]);
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
          const { dataByReglement: grouped, alertes, echecs } = await fetchLignesSelection(declarationId, selectedRows);
          if (cancelled) return;
          setDataByReglement(grouped);
          setAlertesIncoherence(alertes.filter(a => a.code === 'LIGNE_FIGEE_A_REVERIFIER'));
          setEchecsChargement(echecs);
        }
      } catch (e) {
        console.error(e);
        if (!cancelled) setErreurChargement('Erreur lors du chargement des affectations');
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, [declarationId, selectedRows, readOnly]);

  const totauxTva = useMemo(() => {
    let collectee = 0;
    let deductible = 0;
    Object.values(dataByReglement).flat().forEach(l => {
      if (NON_VALORISE.has(l.statutLigne)) return;
      if (l.source === 'Encaissement') collectee += l.montantTVA;
      else deductible += l.montantTVA;
    });
    return { collectee, deductible, solde: collectee - deductible };
  }, [dataByReglement]);

  const reglementInfo = useMemo(
    () => (readOnly ? {} : Object.fromEntries(selectedRows.map(r => [r.numeroReglement, { tiers: r.tiers }]))),
    [selectedRows, readOnly],
  );
  const allRows = useMemo(() => buildGridRows(dataByReglement, reglementInfo), [dataByReglement, reglementInfo]);

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

  const renderCell = (col: GCol, r: GridRow) => {
    if (r.nonValorise && !['numeroReglement', 'factureNumero', 'tiers', 'origine'].includes(col.key)) {
      return (
        <span style={{ color: 'var(--status-warning-text-alt)', fontStyle: 'italic' }}>
          Non valorisé ({r.motif || '—'})
        </span>
      );
    }

    switch (col.key) {
      case 'statutConformite':
        return (
          <span style={{ fontSize: '0.68rem', padding: '1px 6px', borderRadius: '99px', background: r.statutConformite === 'Conforme' ? 'var(--status-ok-bg)' : 'var(--status-blocking-bg)', color: r.statutConformite === 'Conforme' ? 'var(--status-ok-text)' : 'var(--status-blocking-text)' }}>
            {r.statutConformite}
          </span>
        );
      case 'incoherenceValidee':
        return r.incoherenceValidee ? (
          <span
            style={{ fontSize: '0.68rem', padding: '1px 6px', borderRadius: '99px', background: '#fef3c7', color: 'var(--status-warning-text)', fontWeight: 600 }}
            title="Incohérence Sage validée en connaissance de cause — décision tracée, ligne et totaux inchangés"
          >
            Validée
          </span>
        ) : '—';
      case 'tauxTVA': return r.tauxTVA == null ? '—' : `${r.tauxTVA}%`;
      case 'paye': return formatMoney(r.paye);
      case 'ttc': return r.ttc == null ? '—' : formatMoney(r.ttc);
      case 'prorata': return `${r.prorata.toFixed(2)}%`;
      case 'baseHt': return r.baseHt == null ? '—' : formatMoney(r.baseHt);
      case 'baseTva': return r.baseTva == null ? '—' : formatMoney(r.baseTva);
      case 'tva': return <strong>{formatMoney(r.tva)}</strong>;
      default: return (r as unknown as Record<string, any>)[col.key];
    }
  };

  const columnDefs: ColDef[] = useMemo(() => {
    return GRID_COLUMNS.map((col) => {
      const isNumeric = ['paye', 'ttc', 'prorata', 'baseHt', 'baseTva', 'tva'].includes(col.key);
      const w = col.width ? parseInt(col.width, 10) : 130;

      let filterComponent: any = undefined;
      if (col.filterType === 'list') {
        filterComponent = CustomListFilter;
      } else if (col.filterType === 'number') {
        filterComponent = 'agNumberColumnFilter';
      }

      return {
        field: col.key,
        headerName: col.label,
        width: w,
        type: isNumeric ? 'numericColumn' : undefined,
        filter: filterComponent,
        cellRenderer: (p: any) => p.data ? renderCell(col, p.data) : null,
      };
    });
  }, []);

  const chargementIncomplet = echecsChargement.length > 0 || erreurChargement !== null;

  if (!readOnly && selectedRows.length === 0) {
    return (
      <div style={{ flex: 1, display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', textAlign: 'center', padding: '2rem', color: 'var(--text-secondary)', height: '100%' }}>
        <GitMerge size={48} style={{ opacity: 0.25, marginBottom: '1rem' }} />
        <p style={{ margin: 0, fontSize: '0.9rem' }}>Aucun règlement sélectionné — retournez à l'étape ① Règlements.</p>
      </div>
    );
  }

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
      <div style={{ padding: '0.75rem 1rem', borderBottom: '1px solid var(--border-color)', background: 'white', display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: '0.75rem' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.6rem' }}>
          <GitMerge size={20} style={{ color: 'var(--accent-primary)' }} />
          <div>
            <h2 style={{ margin: 0, fontSize: '1.05rem', fontWeight: 600 }}>Détail des affectations règlement → facture</h2>
            <div style={{ fontSize: '0.72rem', color: 'var(--text-secondary)' }}>
              {readOnly ? Object.keys(dataByReglement).length : selectedRows.length} règlement{(readOnly ? Object.keys(dataByReglement).length : selectedRows.length) > 1 ? 's' : ''} · payé ÷ TTC = % puis TVA × % = TVA déclarée
            </div>
          </div>
          {loading && <Loader2 size={15} className="animate-spin" style={{ color: 'var(--accent-primary)' }} />}
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
          <span style={{ fontSize: '0.85rem', fontWeight: 600, color: chargementIncomplet ? '#b91c1c' : undefined, display: 'flex', gap: '0.9rem', alignItems: 'baseline' }}>
            <span>TVA collectée (ventes) : {formatMoney(totauxTva.collectee)}</span>
            <span>TVA déductible (achats) : {formatMoney(totauxTva.deductible)}</span>
            <span title="TVA due = TVA collectée − TVA déductible (hors crédit reporté et hors régularisations)">
              Solde : {formatMoney(totauxTva.solde)}
            </span>
            {chargementIncomplet && <span>— TOTAUX INCOMPLETS</span>}
          </span>
        </div>
      </div>

      {chargementIncomplet && (
        <div style={{
          display: 'flex', alignItems: 'center', gap: '0.5rem', padding: '0.6rem 1rem',
          borderBottom: '1px solid #fecaca', background: 'var(--status-blocking-bg)', color: '#7f1d1d',
          fontSize: '0.85rem', fontWeight: 600,
        }}>
          <AlertTriangle size={16} style={{ flexShrink: 0 }} />
          <span>
            {erreurChargement
              ? `${erreurChargement} — les montants affichés ci-dessous sont incomplets, ne les utilisez pas pour déclarer.`
              : `${echecsMessage(echecsChargement)} — les montants affichés ci-dessous sont incomplets, ne les utilisez pas pour déclarer.`}
          </span>
          <button
            onClick={reload}
            style={{ marginLeft: 'auto', background: 'white', border: '1px solid #fecaca', color: '#7f1d1d', borderRadius: 4, padding: '0.25rem 0.6rem', fontSize: '0.78rem', fontWeight: 600, cursor: 'pointer' }}
          >
            Réessayer le chargement
          </button>
        </div>
      )}

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

      <div style={{ flexGrow: 1, position: 'relative' }}>
        <ApbsGrid
          rowData={rowsApresIncoherence}
          columnDefs={columnDefs}
          height="100%"
          showColumnSelector={true}
          showExportButton={true}
          exportFileName="affectations_drill.xlsx"
        />
      </div>
    </div>
  );
}
