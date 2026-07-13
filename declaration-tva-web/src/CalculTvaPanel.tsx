import { useState, useEffect, useMemo } from 'react';
import { Calculator, AlertTriangle, Loader2, Info } from 'lucide-react';
import { formatMoney } from './utils';
import api from './api';
import type { ReglementRow } from './ReglementsSelection';

// ─── Écran ③ Calcul TVA (TASK-056) ─────────────────────────────────────────
//
// Vue de contrôle lecture seule AVANT intégration. Agrège la valorisation
// retournée par le back (GET /declarations/{id}/lignes) par facture et par
// taux — jamais un recalcul front.
//
// Principes (anti-régression) :
//  - Source unique = back valorisé (montantHT, montantTVA issus du back).
//  - Aucun front-calcul (pas de TVA = HT × taux ici).
//  - Lignes non valorisées (statutLigne IN {2,3,4}) : affichées avec motif
//    explicite — jamais un 0 muet (mémoire grf-valorisation-tracabilite-blocage-om).
//  - ControlGrid.tsx / SummaryPanel.tsx : intouchables.
//  - Arrondis DGI AwayFromZero : portés par le back, affichés tels quels.

type LigneValorisation = {
  id: string;
  factureNumero: string;
  tiers: string;
  tiersIdentifiantFiscal: string;
  tiersICE: string;
  tauxTVA: number;
  montantHT: number;
  montantTVA: number;
  montantTTC: number;
  prorata: number;
  montantAffecte: number;
  statutLigne: number; // 0 Proposée, 1 Intégrée, 2 Exclue, 3 Reportée, 4 Écartée
  motif: string;
  origine: string;
  statutConformite: string;
  numeroRapprochement?: string;
};

// Statuts non valorisés (cohérent avec AffectationsDrill.tsx TASK-055)
const NON_VALORISE = new Set([2, 3, 4]);

const STATUT_LIGNE_LABELS: Record<number, string> = {
  0: 'Proposée',
  1: 'Intégrée',
  2: 'Exclue',
  3: 'Reportée',
  4: 'Écartée',
};

type RowAggr = {
  factureNumero: string;
  tiers: string;
  tiersIdentifiantFiscal: string;
  tiersICE: string;
  tauxTVA: number;
  montantHT: number;    // du back, jamais recalculé
  montantTVA: number;   // du back, jamais recalculé
  nonValorise: boolean;
  motif: string;
  statutLigne: number;
  statutConformite: string;
  prorata: number;
};

// Agrégation par (facture, taux) — source unique = back.
// Si plusieurs lignes back ont la même (facture, taux) on somme HT et TVA.
function agregParFactureTaux(lignes: LigneValorisation[]): RowAggr[] {
  const map = new Map<string, RowAggr>();
  for (const l of lignes) {
    const key = `${l.factureNumero}__${l.tauxTVA}`;
    const existing = map.get(key);
    if (existing) {
      if (!existing.nonValorise) {
        existing.montantHT += l.montantHT;
        existing.montantTVA += l.montantTVA;
      }
    } else {
      const nonValorise = NON_VALORISE.has(l.statutLigne);
      map.set(key, {
        factureNumero: l.factureNumero,
        tiers: l.tiers,
        tiersIdentifiantFiscal: l.tiersIdentifiantFiscal,
        tiersICE: l.tiersICE,
        tauxTVA: l.tauxTVA,
        montantHT: nonValorise ? 0 : l.montantHT,
        montantTVA: nonValorise ? 0 : l.montantTVA,
        nonValorise,
        motif: l.motif,
        statutLigne: l.statutLigne,
        statutConformite: l.statutConformite,
        prorata: l.prorata,
      });
    }
  }
  return [...map.values()].sort((a, b) => {
    const cmp = a.factureNumero.localeCompare(b.factureNumero);
    if (cmp !== 0) return cmp;
    return a.tauxTVA - b.tauxTVA;
  });
}

// Sous-totaux par taux
type SousTotalTaux = {
  taux: number;
  totalHT: number;
  totalTVA: number;
  nbLignes: number;
};

function sousTotauxParTaux(rows: RowAggr[]): SousTotalTaux[] {
  const map = new Map<number, SousTotalTaux>();
  for (const r of rows) {
    if (r.nonValorise) continue;
    const existing = map.get(r.tauxTVA);
    if (existing) {
      existing.totalHT += r.montantHT;
      existing.totalTVA += r.montantTVA;
      existing.nbLignes += 1;
    } else {
      map.set(r.tauxTVA, { taux: r.tauxTVA, totalHT: r.montantHT, totalTVA: r.montantTVA, nbLignes: 1 });
    }
  }
  return [...map.values()].sort((a, b) => b.taux - a.taux);
}

async function fetchAllLignes(
  declarationId: string
): Promise<LigneValorisation[]> {
  const size = 500;
  let page = 1;
  let items: LigneValorisation[] = [];
  for (;;) {
    const res = await api.get(`/declarations/${declarationId}/lignes`, {
      params: {
        domaine: 'Decaissement',
        page,
        size,
      },
    });
    const chunk: LigneValorisation[] = res.data.items || [];
    items = items.concat(chunk);
    const total = res.data.totalCount ?? chunk.length;
    console.log(`fetchAllLignes: page ${page}, chunk: ${chunk.length}, total accumulated: ${items.length}/${total}`);
    if (chunk.length === 0 || items.length >= total) break;
    page += 1;
  }
  return items;
}

// ─── Composant principal ────────────────────────────────────────────────────

export function CalculTvaPanel({
  declarationId,
  selectedRows,
  readOnly = false,
  showToast,
  onCalcSummary,
}: {
  declarationId: string;
  selectedRows: ReglementRow[];
  /** Relecture après intégration (TASK-075) : ignore selectedRows, charge tout par declarationId */
  readOnly?: boolean;
  showToast: (m: string, t?: 'success' | 'error' | 'warning') => void;
  /** Callback optionnel pour remonter totalTVA + nbLignes vers le stepper (source unique ③→④) */
  onCalcSummary?: (totalTVA: number, nbLignes: number) => void;
}) {
  const [dataByReglement, setDataByReglement] = useState<Record<string, LigneValorisation[]>>({});
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      if (!readOnly && selectedRows.length === 0) {
        setLoading(false);
        return;
      }
      setLoading(true);
      try {
        console.log(`CalculTvaPanel: starting fetch (readOnly=${readOnly}, ${selectedRows.length} selected rows)`);
        const allLignes = await fetchAllLignes(declarationId);
        if (cancelled) return;

        console.log(`CalculTvaPanel: fetched ${allLignes.length} total lines from API`);
        if (allLignes.length > 0) {
          console.log("CalculTvaPanel: first line example:", JSON.stringify(allLignes[0]));
        }

        // Relecture (TASK-075) : la déclaration est figée, on prend toutes les
        // lignes de la déclaration sans filtrer par une sélection de session
        // (jamais réhydratée après intégration).
        const mappedData = readOnly
          ? { '__declaration__': allLignes }
          : Object.fromEntries(selectedRows.map(r => {
              const matchingLignes = allLignes.filter(l => l.numeroRapprochement === r.numeroReglement);
              if (matchingLignes.length > 0) {
                console.log(`CalculTvaPanel: found match for ${r.numeroReglement} -> ${matchingLignes.length} lines`);
              }
              return [r.numeroReglement, matchingLignes] as const;
            }));

        setDataByReglement(mappedData);

        // Calculate summary directly to prevent state propagation race conditions
        const flatLignes = Object.values(mappedData).flat();
        const aggregatedRows = agregParFactureTaux(flatLignes);
        const st = sousTotauxParTaux(aggregatedRows);
        const computedTotalTVA = st.reduce((s, x) => s + x.totalTVA, 0);
        const computedNbLignes = aggregatedRows.filter(r => !r.nonValorise).length;
        
        console.log(`CalculTvaPanel: computedTotalTVA=${computedTotalTVA}, computedNbLignes=${computedNbLignes}`);
        if (onCalcSummary) onCalcSummary(computedTotalTVA, computedNbLignes);
      } catch (e) {
        console.error("CalculTvaPanel error:", e);
        showToast('Erreur lors du chargement de la valorisation', 'error');
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, [declarationId, selectedRows, readOnly, showToast]);

  // Toutes les lignes de tous les règlements sélectionnés
  const allLignes = useMemo(() =>
    Object.values(dataByReglement).flat(),
    [dataByReglement]
  );

  // Lignes agrégées (facture × taux)
  const rows = useMemo(() => agregParFactureTaux(allLignes), [allLignes]);

  // Sous-totaux par taux (valorisées uniquement)
  const sousTotaux = useMemo(() => sousTotauxParTaux(rows), [rows]);

  // Total TVA global = somme des sous-totaux par taux
  const totalTVA = useMemo(() => sousTotaux.reduce((s, st) => s + st.totalTVA, 0), [sousTotaux]);
  const totalHT = useMemo(() => sousTotaux.reduce((s, st) => s + st.totalHT, 0), [sousTotaux]);

  // Lignes non valorisées (pour le compteur d'alerte)
  const nbNonValorise = useMemo(() => rows.filter(r => r.nonValorise).length, [rows]);

  // Garde « rien sélectionné » (parcours normal ①→②→③, TASK-054) — ne doit pas
  // s'appliquer en relecture, où l'absence de sélection en session est normale.
  if (!readOnly && selectedRows.length === 0) {
    return (
      <div style={{ flex: 1, display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', textAlign: 'center', padding: '2rem', color: 'var(--text-secondary)', height: '100%' }}>
        <Calculator size={48} style={{ opacity: 0.25, marginBottom: '1rem' }} />
        <p style={{ margin: 0, fontSize: '0.9rem' }}>Aucun règlement sélectionné — retournez à l'étape ① Règlements.</p>
      </div>
    );
  }

  // Garde « rien chargé » distincte (TASK-075, périmètre B) : en relecture, un
  // vide réel serait anormal pour une déclaration intégrée — message différent.
  if (readOnly && !loading && allLignes.length === 0) {
    return (
      <div style={{ flex: 1, display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', textAlign: 'center', padding: '2rem', color: 'var(--text-secondary)', height: '100%' }}>
        <Calculator size={48} style={{ opacity: 0.25, marginBottom: '1rem' }} />
        <p style={{ margin: 0, fontSize: '0.9rem' }}>Aucune ligne de valorisation trouvée pour cette déclaration.</p>
      </div>
    );
  }

  return (
    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', height: '100%', overflow: 'hidden', background: 'var(--bg-secondary)' }}>
      {/* En-tête */}
      <div style={{
        padding: '0.65rem 1rem',
        borderBottom: '1px solid var(--border-color)',
        background: 'white',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        flexWrap: 'wrap',
        gap: '0.5rem',
        flexShrink: 0,
      }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.6rem' }}>
          <Calculator size={20} style={{ color: 'var(--accent-primary)' }} />
          <div>
            <h2 style={{ margin: 0, fontSize: '1.05rem', fontWeight: 600 }}>③ Calcul TVA</h2>
            <div style={{ fontSize: '0.72rem', color: 'var(--text-secondary)' }}>
              Valorisation agrégée avant intégration · lecture seule · source unique = back valorisé
            </div>
          </div>
          {loading && <Loader2 size={15} className="animate-spin" style={{ color: 'var(--accent-primary)' }} />}
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
          {nbNonValorise > 0 && (
            <span style={{ display: 'flex', alignItems: 'center', gap: '0.35rem', fontSize: '0.78rem', color: '#b45309', fontWeight: 600 }}>
              <AlertTriangle size={14} />
              {nbNonValorise} ligne{nbNonValorise > 1 ? 's' : ''} non valorisée{nbNonValorise > 1 ? 's' : ''}
            </span>
          )}
          <span style={{ fontSize: '0.8rem', color: 'var(--text-secondary)' }}>
            {readOnly
              ? 'Déclaration intégrée — toutes les lignes'
              : <>{selectedRows.length} règlement{selectedRows.length > 1 ? 's' : ''} sélectionné{selectedRows.length > 1 ? 's' : ''}</>}
          </span>
        </div>
      </div>

      <div style={{ flex: 1, overflow: 'auto', display: 'flex', flexDirection: 'column', gap: '0' }}>
        {/* Sous-totaux par taux */}
        {sousTotaux.length > 0 && (
          <div style={{ margin: '0 0.75rem 0.75rem', background: 'white', border: '1px solid var(--border-color)', borderRadius: '8px', overflow: 'hidden' }}>
            <div style={{ padding: '0.5rem 0.9rem', background: 'var(--bg-secondary)', borderBottom: '1px solid var(--border-color)', fontSize: '0.78rem', fontWeight: 600, color: 'var(--text-secondary)', display: 'flex', alignItems: 'center', gap: '0.4rem' }}>
              <Info size={13} />
              Sous-totaux par taux TVA
            </div>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.8125rem' }}>
              <thead>
                <tr style={{ background: 'var(--bg-secondary)' }}>
                  <th style={thStyle('left')}>Taux</th>
                  <th style={thStyle('right')}>Nb lignes</th>
                  <th style={thStyle('right')}>Total HT</th>
                  <th style={thStyle('right')}>Total TVA</th>
                </tr>
              </thead>
              <tbody>
                {sousTotaux.map(st => (
                  <tr key={st.taux} style={{ borderTop: '1px solid var(--border-color)' }}>
                    <td style={tdStyle('left')}>
                      <TauxBadge taux={st.taux} />
                    </td>
                    <td style={{ ...tdStyle('right'), color: 'var(--text-secondary)' }}>{st.nbLignes}</td>
                    <td style={{ ...tdStyle('right'), fontVariantNumeric: 'tabular-nums' }}>{formatMoney(st.totalHT)}</td>
                    <td style={{ ...tdStyle('right'), fontWeight: 700, fontVariantNumeric: 'tabular-nums' }}>{formatMoney(st.totalTVA)}</td>
                  </tr>
                ))}
                {/* Ligne totaux */}
                <tr style={{ borderTop: '2px solid var(--border-color)', background: 'var(--bg-secondary)' }}>
                  <td style={{ ...tdStyle('left'), fontWeight: 700 }} colSpan={2}>Σ Total</td>
                  <td style={{ ...tdStyle('right'), fontWeight: 700, fontVariantNumeric: 'tabular-nums' }}>{formatMoney(totalHT)}</td>
                  <td style={{ ...tdStyle('right'), fontWeight: 700, fontVariantNumeric: 'tabular-nums' }}>{formatMoney(totalTVA)}</td>
                </tr>
              </tbody>
            </table>
          </div>
        )}

        {/* Lignes non valorisées — détail motif */}
        {nbNonValorise > 0 && (
          <div style={{ margin: '0 0.75rem 0.75rem', background: 'white', border: '1px solid #fde68a', borderRadius: '8px', overflow: 'hidden' }}>
            <div style={{ padding: '0.5rem 0.9rem', background: '#fffbeb', borderBottom: '1px solid #fde68a', fontSize: '0.78rem', fontWeight: 600, color: '#92400e', display: 'flex', alignItems: 'center', gap: '0.4rem' }}>
              <AlertTriangle size={13} />
              {nbNonValorise} ligne{nbNonValorise > 1 ? 's' : ''} non valorisée{nbNonValorise > 1 ? 's' : ''} — transparence de traçabilité
            </div>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.8rem' }}>
              <thead>
                <tr style={{ background: '#fffbeb' }}>
                  <th style={thStyle('left')}>Facture</th>
                  <th style={thStyle('left')}>Tiers</th>
                  <th style={thStyle('left')}>Statut ligne</th>
                  <th style={thStyle('left')}>Motif</th>
                </tr>
              </thead>
              <tbody>
                {rows.filter(r => r.nonValorise).map((r, i) => (
                  <tr key={`nv-${i}`} style={{ borderTop: '1px solid #fde68a' }}>
                    <td style={{ ...tdStyle('left'), fontFamily: 'monospace', fontSize: '0.77rem', fontWeight: 500 }}>{r.factureNumero}</td>
                    <td style={{ ...tdStyle('left'), fontSize: '0.77rem' }}>{r.tiers}</td>
                    <td style={{ ...tdStyle('left') }}>
                      <span style={{ display: 'inline-block', padding: '1px 7px', borderRadius: '99px', fontSize: '0.72rem', fontWeight: 600, background: '#fee2e2', color: '#b91c1c' }}>
                        {STATUT_LIGNE_LABELS[r.statutLigne] ?? r.statutLigne}
                      </span>
                    </td>
                    <td style={{ ...tdStyle('left'), color: '#92400e', fontSize: '0.77rem' }}>
                      {r.motif || '—'}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Bandeau pied — Total TVA global mis en évidence */}
      <div style={{
        flexShrink: 0,
        borderTop: '2px solid var(--border-color)',
        background: 'white',
        padding: '0.75rem 1rem',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: '1rem',
      }}>
        <div style={{ fontSize: '0.8125rem', color: 'var(--text-secondary)' }}>
          {rows.length} ligne{rows.length > 1 ? 's' : ''} agrégée{rows.length > 1 ? 's' : ''}
          {nbNonValorise > 0 && (
            <span style={{ marginLeft: '0.75rem', color: '#b45309' }}>
              · {nbNonValorise} non valorisée{nbNonValorise > 1 ? 's' : ''} (exclues du total)
            </span>
          )}
        </div>
        <div style={{ display: 'flex', alignItems: 'baseline', gap: '0.6rem' }}>
          <span style={{ fontSize: '0.875rem', color: 'var(--text-secondary)', fontWeight: 500 }}>
            Total TVA à intégrer
          </span>
          <span style={{
            fontSize: '1.35rem',
            fontWeight: 800,
            color: 'var(--accent-primary)',
            fontVariantNumeric: 'tabular-nums',
            letterSpacing: '-0.01em',
          }}>
            {formatMoney(totalTVA)}
          </span>
        </div>
      </div>
    </div>
  );
}

// ─── Helpers de style ────────────────────────────────────────────────────────

function thStyle(align: 'left' | 'right' | 'center' = 'left'): React.CSSProperties {
  return {
    padding: '0.45rem 0.75rem',
    textAlign: align,
    borderBottom: '2px solid var(--border-color)',
    fontWeight: 600,
    fontSize: '0.75rem',
    color: 'var(--text-secondary)',
    whiteSpace: 'nowrap',
    userSelect: 'none',
  };
}

function tdStyle(align: 'left' | 'right' | 'center' = 'left'): React.CSSProperties {
  return {
    padding: '0.42rem 0.75rem',
    textAlign: align,
    whiteSpace: 'nowrap',
    verticalAlign: 'middle',
  };
}

// Badge taux : couleur par taux (20%=bleu, 14%=violet, 10%=vert, 0%=gris)
function TauxBadge({ taux }: { taux: number }) {
  const colors: Record<number, { bg: string; color: string }> = {
    20: { bg: '#dbeafe', color: '#1d4ed8' },
    14: { bg: '#ede9fe', color: '#6d28d9' },
    10: { bg: '#dcfce7', color: '#15803d' },
    7:  { bg: '#fef9c3', color: '#854d0e' },
    0:  { bg: '#f3f4f6', color: '#6b7280' },
  };
  const c = colors[taux] ?? { bg: '#f3f4f6', color: '#374151' };
  return (
    <span style={{
      display: 'inline-block',
      padding: '1px 8px',
      borderRadius: '99px',
      fontSize: '0.75rem',
      fontWeight: 700,
      background: c.bg,
      color: c.color,
    }}>
      {taux}%
    </span>
  );
}
