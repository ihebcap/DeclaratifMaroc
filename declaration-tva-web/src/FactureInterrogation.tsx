import { useState, useEffect, useRef, useCallback } from 'react';
import { useVirtualizer } from '@tanstack/react-virtual';
import { Loader2, FileText, X, AlertTriangle, HelpCircle, RefreshCw } from 'lucide-react';
import { ExcelFilter } from './ExcelFilter';
import { ColumnSelector } from './ColumnSelector';
import { useColumnPrefs } from './useColumnPrefs';
import { formatMoney, formatDate } from './utils';
import api from './api';

// ─── Interrogation « Factures » (TASK-041) ────────────────────────────────────
//
// Deuxième entrée du menu INTERROGATION, pivot FACTURE (RT_ECHEANCE), consommant
// GET /api/factures (lecture seule). Aucune écriture, aucun tampon DT_Id, aucune
// sélection/déclaration (option 2 différée). Densité comptable « 0 espace perdu ».
//
// Trois familles de colonnes de coûts/sources distincts (cf. TASK-041) :
//   A — identité facture (SQL local) : N°, Date, Fournisseur, Réf, TTC, Solde.
//   B — valorisation TVA (cache TASK-024) : HT, TVA, Autre taxe, Écart, Escompte —
//       rendues « non valorisé » + motif quand absentes du cache, JAMAIS un 0 inventé.
//   C — statut déclaration (agrégat DT_Id) : Réglé, Déclaré, Reste à déclarer,
//       statut à 3 valeurs (NonDéclarable | Partiel | Total).

type ListFilterValue = string | string[];

type Col = {
  key: string;
  label: string;
  align?: 'left' | 'right' | 'center';
  sortKey?: 'date' | 'ttc' | 'solde' | 'numero';
  filterType?: 'list' | 'text';
  width?: string;
  famille?: 'A' | 'B' | 'C';
};

// TASK-067B : n° facture / référence passent en 'list' (colonnes identifiantes, décision PO
// cardinalité — cf. GET /factures/distincts). Fournisseur reste en 'text' (nom libre, cardinalité
// non bornée — décision documentée dans VERIFY/TASK-067B_verify.md).
const COLUMNS: Col[] = [
  { key: 'factureNumero', label: 'N° Facture', filterType: 'list', width: '130px', famille: 'A' },
  { key: 'date', label: 'Date', sortKey: 'date', width: '100px', famille: 'A' },
  { key: 'fournisseur', label: 'Fournisseur', filterType: 'text', famille: 'A' },
  { key: 'reference', label: 'Référence', filterType: 'list', width: '130px', famille: 'A' },
  { key: 'montantHT', label: 'Total HT', align: 'right', width: '120px', famille: 'B' },
  { key: 'montantTVA', label: 'Total TVA', align: 'right', width: '120px', famille: 'B' },
  { key: 'autreTaxe', label: 'Autre taxe', align: 'right', width: '110px', famille: 'B' },
  { key: 'ecart', label: 'Écart', align: 'right', width: '100px', famille: 'B' },
  { key: 'escompte', label: 'Escompte', align: 'right', width: '110px', famille: 'B' },
  { key: 'montantTTC', label: 'TTC', align: 'right', sortKey: 'ttc', width: '120px', famille: 'A' },
  { key: 'regle', label: 'Réglé', align: 'right', width: '120px', famille: 'C' },
  { key: 'declare', label: 'Déclaré', align: 'right', width: '120px', famille: 'C' },
  { key: 'resteADeclarer', label: 'Reste à décl.', align: 'right', width: '120px', famille: 'C' },
  { key: 'soldeFacture', label: 'Solde facture', align: 'right', sortKey: 'solde', width: '120px', famille: 'A' },
  { key: 'statut', label: 'Statut décl.', align: 'center', filterType: 'list', width: '130px', famille: 'C' },
  { key: 'origine', label: 'Origine', filterType: 'list', width: '120px', famille: 'B' },
];

const isNonNul = (v: number) => Math.abs(v ?? 0) > 0.005;

const todayIso = () => new Date().toISOString().slice(0, 10);
const yearStartIso = () => `${new Date().getFullYear()}-01-01`;

// Garde-fou saisie : un input[type=date] natif peut émettre une année transitoire incomplète
// pendant la frappe (ex. "0014-03-10") avant que l'utilisateur ait fini de taper — envoyée telle
// quelle au back, elle dépasse la plage SQL Server DATETIME (min 1753) et provoque une
// SqlTypeException non gérée. On ignore toute valeur dont l'année n'est pas plausible.
const anneeEstPlausible = (isoDate: string) => {
  const annee = Number(isoDate.slice(0, 4));
  return Number.isFinite(annee) && annee >= 1900 && annee <= 2100;
};

// Statut à 3 valeurs, dérivé des affectations DT_Id (jamais un booléen).
function StatutBadge({ statut }: { statut: string }) {
  const map: Record<string, { bg: string, text: string, label: string }> = {
    Total: { bg: '#dcfce7', text: '#15803d', label: 'Total' },
    Partiel: { bg: '#fef3c7', text: '#b45309', label: 'Partiel' },
    NonDeclarable: { bg: '#f3f4f6', text: '#6b7280', label: 'Non décl.' },
  };
  const c = map[statut] || { bg: '#f3f4f6', text: '#374151', label: statut || '—' };
  return <span style={{ background: c.bg, color: c.text, padding: '2px 8px', borderRadius: '99px', fontSize: '0.7rem', fontWeight: 600 }}>{c.label}</span>;
}

function OrigineChip({ origine }: { origine: string }) {
  const map: Record<string, { bg: string, text: string }> = {
    Sage: { bg: '#e0e7ff', text: '#4338ca' },
    FGR: { bg: '#fef3c7', text: '#b45309' },
    SoldeInitial: { bg: '#f1f5f9', text: '#475569' },
  };
  const c = map[origine] || { bg: '#f3f4f6', text: '#374151' };
  return <span style={{ background: c.bg, color: c.text, padding: '2px 8px', borderRadius: '4px', fontSize: '0.7rem', fontWeight: 600 }}>{origine || '—'}</span>;
}

// Cellule famille B : valeur du cache TASK-024 OU marqueur « non valorisé » explicite
// (jamais un 0 silencieux — règle n°1 du projet). Le motif est porté en title (survol).
// TASK-076 : quand un montant BRUT Sage est disponible (facture exclue pour incohérence), on
// l'affiche DIRECTEMENT dans la cellule (grisé/italique + icône) — visible sans ouvrir le
// détail, indispensable pour scanner un grand volume de factures (décision PO 13/07/2026).
// Ce montant brut n'est JAMAIS un montant déclarable (colonnes ③/④ restent null/exclues).
function CelluleB({ value, valorisee, motif, brutValue }: { value: number | null, valorisee: boolean, motif: string, brutValue?: number | null }) {
  if (value === null || value === undefined) {
    if (!valorisee) {
      if (brutValue != null) {
        const title = `${motif} — valeur brute Sage affichée (donnée non fiable, jamais déclarable)`;
        return (
          <span title={title} style={{ color: '#6b7280', display: 'inline-flex', alignItems: 'center', gap: '0.25rem', fontSize: '0.78rem', fontStyle: 'italic', cursor: 'help' }}>
            <AlertTriangle size={12} style={{ color: '#b45309', flexShrink: 0 }} />
            {formatMoney(brutValue)}
          </span>
        );
      }
      return (
        <span title={motif} style={{ color: '#b45309', display: 'inline-flex', alignItems: 'center', gap: '0.2rem', fontSize: '0.72rem', fontStyle: 'italic', cursor: 'help' }}>
          <HelpCircle size={12} /> non valorisé
        </span>
      );
    }
    return <span style={{ color: 'var(--text-secondary)' }}>—</span>;
  }
  return <span>{formatMoney(value)}</span>;
}

export function FactureInterrogation({ societeId, showToast }: { societeId: number, showToast: (m: string, t?: 'success' | 'error' | 'warning') => void }) {
  const [data, setData] = useState<any[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);

  // Période bornée (OBLIGATOIRE endpoint : borne la famille B, garde-fou perf).
  const [debut, setDebut] = useState<string>(yearStartIso());
  const [fin, setFin] = useState<string>(todayIso());

  const [refreshing, setRefreshing] = useState(false);

  const [page, setPage] = useState(1);
  const [filters, setFilters] = useState<Record<string, ListFilterValue>>({});
  const [sortConfig, setSortConfig] = useState<{ key: 'date' | 'ttc' | 'solde' | 'numero', desc: boolean }>({ key: 'date', desc: true });
  const [origineOptions, setOrigineOptions] = useState<{ label: string, value: string }[]>([]);
  // TASK-067B — options des colonnes identifiantes, peuplées depuis /factures/distincts (MÊME
  // WHERE/période que la liste principale — invariant TASK-040, aucune valeur infiltrable).
  const [numeroOptions, setNumeroOptions] = useState<string[]>([]);
  const [referenceOptions, setReferenceOptions] = useState<string[]>([]);

  const [detailRow, setDetailRow] = useState<any | null>(null);

  const size = 100;
  const parentRef = useRef<HTMLDivElement>(null);

  const { visibleColumns, visibleKeys, toggle: toggleColumn, reset: resetColumns } = useColumnPrefs('grf.cols.factures', COLUMNS);

  // Valeurs distinctes (origines EC_Type présentes) pour le filtre liste — dépend de la période.
  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const res = await api.get('/factures/distincts', { params: { debut, fin, soId: societeId } });
        if (cancelled) return;
        const origines = (res.data.origines || []).map((o: any) => ({ label: o.libelle, value: o.libelle }));
        setOrigineOptions(origines);
        // TASK-067B : n° facture / référence — valeurs distinctes exactes de la période.
        setNumeroOptions(res.data.numero || []);
        setReferenceOptions(res.data.reference || []);
      } catch {
        if (!cancelled) {
          setOrigineOptions([]);
          setNumeroOptions([]);
          setReferenceOptions([]);
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
      // Filtres serveur (même WHERE que le COUNT — leçon TASK-040 : compteur cohérent).
      // TASK-067B : n° facture / référence en MULTI-SÉLECTION RÉELLE (valeurs distinctes exactes),
      // fin du LIKE scalaire.
      const numero = filters['factureNumero'];
      if (Array.isArray(numero) && numero.length > 0) params.numero = numero;
      const fournisseur = filters['fournisseur'];
      if (typeof fournisseur === 'string' && fournisseur.trim() !== '') params.fournisseur = fournisseur.trim();
      const reference = filters['reference'];
      if (Array.isArray(reference) && reference.length > 0) params.reference = reference;
      const origine = filters['origine'];
      if (Array.isArray(origine) && origine.length > 0) params.origine = origine; // multi-sélection
      const statut = filters['statut'];
      if (Array.isArray(statut) && statut.length > 0) params.statut = statut; // multi-sélection

      const res = await api.get('/factures', {
        params,
        paramsSerializer: { indexes: null },
      });
      const items = res.data.items || [];
      setData(items);
      setTotal(res.data.totalCount ?? items.length);
    } catch (e) {
      console.error(e);
      showToast('Erreur lors du chargement des factures', 'error');
      setData([]);
      setTotal(0);
    } finally {
      setLoading(false);
    }
  }, [debut, fin, page, filters, sortConfig, showToast, societeId]);

  useEffect(() => { setPage(1); }, [filters, sortConfig, debut, fin]);
  useEffect(() => { fetchPage(); }, [fetchPage]);

  // Rafraîchir la valorisation (famille B) : lit les OM Sage pour la période et remplit le cache
  // TASK-024, puis recharge la liste. Opération explicite, synchrone et potentiellement longue.
  const handleRefreshValorisation = useCallback(async () => {
    setRefreshing(true);
    try {
      const res = await api.post('/factures/rafraichir-valorisation', null, { params: { debut, fin, soId: societeId } });
      const n = res.data?.facturesTraitees ?? 0;
      const nbErr = res.data?.nbErreurs ?? 0;
      if (nbErr > 0) {
        // Transparence : les motifs exacts sont dans la réponse (res.data.erreurs) et dans
        // logs/valorisation.log côté serveur. On les remonte aussi en console pour analyse.
        console.warn('Motifs de non-valorisation :', res.data?.erreurs);
        showToast(`Valorisation — ${n} traitée(s), ${nbErr} en erreur (voir console / logs serveur)`, 'error');
      } else {
        showToast(`Valorisation rafraîchie — ${n} facture(s) traitée(s)`, 'success');
      }
      await fetchPage();
    } catch (e) {
      console.error(e);
      showToast('Échec du rafraîchissement de la valorisation (lecture Sage)', 'error');
    } finally {
      setRefreshing(false);
    }
  }, [debut, fin, fetchPage, showToast, societeId]);

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
    // Statut = domaine fixe à 3 valeurs (source serveur : FactureInterrogationRow.LibelleStatut).
    if (key === 'statut') {
      return [
        { label: 'Total', value: 'Total' },
        { label: 'Partiel', value: 'Partiel' },
        { label: 'Non déclarable', value: 'NonDeclarable' },
      ];
    }
    // Origine : peuplée depuis /factures/distincts (libellés dérivés EC_Type, source unique serveur).
    if (key === 'origine') return origineOptions;
    // TASK-067B — colonnes identifiantes : options = valeurs distinctes de la PÉRIODE ENTIÈRE
    // (endpoint /factures/distincts, même WHERE que la liste), jamais de la page courante.
    if (key === 'factureNumero') return numeroOptions.map(v => ({ label: v, value: v }));
    if (key === 'reference') return referenceOptions.map(v => ({ label: v, value: v }));
    return [];
  };

  const renderCell = (col: Col, row: any) => {
    const v = row[col.key];
    switch (col.key) {
      case 'date': return formatDate(v);
      case 'fournisseur':
        return (
          <span style={{ overflow: 'hidden', textOverflow: 'ellipsis' }}>
            <strong style={{ fontWeight: 600 }}>{row.fournisseurCode || '—'}</strong>
            {row.fournisseurIntitule ? <span style={{ color: 'var(--text-secondary)' }}> · {row.fournisseurIntitule}</span> : null}
          </span>
        );
      case 'montantHT': return <CelluleB value={row.montantHT} valorisee={row.valorisee} motif={row.motifValorisation} brutValue={row.htBrut} />;
      case 'montantTVA': return <CelluleB value={row.montantTVA} valorisee={row.valorisee} motif={row.motifValorisation} brutValue={row.tvaBrut} />;
      case 'autreTaxe': return <CelluleB value={row.autreTaxe} valorisee={row.valorisee} motif={row.motifValorisation} brutValue={row.parafiscaleBrut} />;
      case 'ecart': return <CelluleB value={row.ecart} valorisee={row.valorisee} motif={row.motifValorisation} />;
      case 'escompte': return <CelluleB value={row.escompte} valorisee={row.valorisee} motif={row.motifValorisation} />;
      case 'montantTTC': return <span style={{ fontWeight: 600 }}>{formatMoney(v)}</span>;
      case 'regle': return formatMoney(v);
      case 'declare': return <span style={{ color: isNonNul(v) ? '#4338ca' : 'var(--text-secondary)', fontWeight: isNonNul(v) ? 600 : 400 }}>{formatMoney(v)}</span>;
      case 'resteADeclarer':
        return isNonNul(v)
          ? <span style={{ color: '#b45309', fontWeight: 700 }}>{formatMoney(v)}</span>
          : <span style={{ color: 'var(--text-secondary)' }}>{formatMoney(v)}</span>;
      case 'soldeFacture':
        return isNonNul(v)
          ? <span style={{ color: '#b45309', fontWeight: 600 }}>{formatMoney(v)}</span>
          : <span style={{ color: '#15803d' }}>{formatMoney(v)}</span>;
      case 'statut': return <StatutBadge statut={v} />;
      case 'origine': return <OrigineChip origine={v} />;
      default: return v;
    }
  };

  const totalPages = Math.ceil(total / size) || 1;
  const activeFilterCount = Object.keys(filters).length;

  const colStyle = (col: Col): React.CSSProperties =>
    col.width
      ? { flex: `0 0 ${col.width}`, width: col.width }
      : { flex: '1 1 0', minWidth: '180px' };
  const colJustify = (col: Col) =>
    col.align === 'right' ? 'flex-end' : col.align === 'center' ? 'center' : 'flex-start';

  return (
    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', height: '100%', overflow: 'hidden' }}>
      {/* En-tête écran + période */}
      <div style={{ padding: '0.75rem 1rem', borderBottom: '1px solid var(--border-color)', background: 'white', display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: '0.75rem' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.6rem' }}>
          <FileText size={20} style={{ color: 'var(--accent-primary)' }} />
          <div>
            <h2 style={{ margin: 0, fontSize: '1.05rem', fontWeight: 600 }}>Factures</h2>
            <div style={{ fontSize: '0.72rem', color: 'var(--text-secondary)' }}>Interrogation — pivot facture, lecture seule (période obligatoire)</div>
          </div>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', fontSize: '0.8rem' }}>
          <label style={{ color: 'var(--text-secondary)' }}>Du</label>
          <input type="date" value={debut} max={fin} onChange={e => anneeEstPlausible(e.target.value) && setDebut(e.target.value)} className="form-input" style={{ fontSize: '0.8rem', padding: '0.25rem 0.5rem' }} />
          <label style={{ color: 'var(--text-secondary)' }}>Au</label>
          <input type="date" value={fin} min={debut} onChange={e => anneeEstPlausible(e.target.value) && setFin(e.target.value)} className="form-input" style={{ fontSize: '0.8rem', padding: '0.25rem 0.5rem' }} />
          <button
            className="btn"
            onClick={handleRefreshValorisation}
            disabled={refreshing}
            title="Lit les OM Sage pour la période et remplit le cache de valorisation TVA (famille B)"
            style={{ display: 'inline-flex', alignItems: 'center', gap: '0.35rem', fontSize: '0.8rem', padding: '0.25rem 0.6rem', marginLeft: '0.25rem' }}
          >
            <RefreshCw size={14} className={refreshing ? 'animate-spin' : undefined} />
            {refreshing ? 'Valorisation…' : 'Rafraîchir valorisation'}
          </button>
          <ColumnSelector columns={COLUMNS} visibleKeys={visibleKeys} onToggle={toggleColumn} onReset={resetColumns} />
        </div>
      </div>

      {/* Barre d'info */}
      <div style={{ padding: '0.4rem 1rem', borderBottom: '1px solid var(--border-color)', background: 'var(--bg-secondary)', display: 'flex', justifyContent: 'space-between', alignItems: 'center', fontSize: '0.8rem' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
          {loading && <Loader2 size={15} className="animate-spin" style={{ color: 'var(--accent-primary)' }} />}
          <span>Factures : <strong>{total}</strong></span>
        </div>
        {activeFilterCount > 0 && (
          <button className="btn" onClick={() => setFilters({})} style={{ background: 'transparent', border: 'none', color: 'var(--accent-primary)', textDecoration: 'underline', padding: 0, fontSize: '0.8rem', cursor: 'pointer' }}>
            Effacer filtres ({activeFilterCount})
          </button>
        )}
      </div>

      {/* Grille (flexbox : entête + lignes virtualisées partagent les mêmes largeurs) */}
      <div ref={parentRef} style={{ flexGrow: 1, overflow: 'auto', position: 'relative', background: 'white' }}>
        <div style={{ minWidth: '1700px', fontSize: '0.8125rem' }}>
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
                  key={`${row.factureNumero}-${virtualRow.index}`}
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
            Aucune facture pour ces filtres / cette période.
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

      {detailRow && <FactureDetail row={detailRow} onClose={() => setDetailRow(null)} />}
    </div>
  );
}

// Panneau de détail À LA DEMANDE. Montre l'identité facture (A), la valorisation TVA
// (B — cache TASK-024, « non valorisé » + motif si absente) et l'état de déclaration
// (C — statut à 3 valeurs dérivé DT_Id : Réglé / Déclaré / Reste à déclarer). Aucune
// action de déclaration ici (option 2 différée).
function FactureDetail({ row, onClose }: { row: any, onClose: () => void }) {
  const reste = row.resteADeclarer ?? 0;
  const solde = row.soldeFacture ?? 0;
  const line = (label: string, value: React.ReactNode) => (
    <div style={{ display: 'flex', justifyContent: 'space-between', padding: '0.6rem 0', borderBottom: '1px solid var(--border-color)', fontSize: '0.85rem' }}>
      <span style={{ color: 'var(--text-secondary)' }}>{label}</span>
      <span style={{ fontWeight: 600, textAlign: 'right' }}>{value}</span>
    </div>
  );
  const celluleB = (v: number | null) => <CelluleB value={v} valorisee={row.valorisee} motif={row.motifValorisation} />;

  return (
    <div style={{ position: 'fixed', top: 0, left: 0, width: '100%', height: '100%', background: 'rgba(0,0,0,0.5)', zIndex: 1000, display: 'flex', justifyContent: 'flex-end' }} onClick={onClose}>
      <div style={{ width: '480px', maxWidth: '100%', height: '100%', background: 'white', boxShadow: '-4px 0 15px rgba(0,0,0,0.1)', display: 'flex', flexDirection: 'column' }} onClick={e => e.stopPropagation()}>
        <div style={{ padding: '1.25rem 1.5rem', borderBottom: '1px solid var(--border-color)', display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
          <div>
            <h3 style={{ margin: 0, fontSize: '1.1rem', fontWeight: 600 }}>Facture {row.factureNumero}</h3>
            <div style={{ fontSize: '0.8rem', color: 'var(--text-secondary)', marginTop: '0.15rem' }}>{formatDate(row.date)} · <StatutBadge statut={row.statut} /></div>
          </div>
          <button onClick={onClose} style={{ background: 'transparent', border: 'none', cursor: 'pointer', color: 'var(--text-secondary)' }}><X size={22} /></button>
        </div>

        <div style={{ flex: 1, overflowY: 'auto', padding: '1.25rem 1.5rem' }}>
          <div style={{ marginBottom: '1.25rem' }}>
            <div style={{ fontSize: '0.7rem', fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.05em', color: 'var(--text-secondary)', marginBottom: '0.4rem' }}>Fournisseur</div>
            <div style={{ fontSize: '0.9rem', fontWeight: 600 }}>{row.fournisseurIntitule || '—'}</div>
            {row.fournisseurCode && <div style={{ fontSize: '0.75rem', color: 'var(--text-secondary)' }}>{row.fournisseurCode}</div>}
            {row.reference && <div style={{ fontSize: '0.75rem', color: 'var(--text-secondary)', marginTop: '0.2rem' }}>Réf : {row.reference}</div>}
          </div>

          <div style={{ fontSize: '0.7rem', fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.05em', color: 'var(--text-secondary)', marginBottom: '0.2rem' }}>Valorisation TVA (famille B)</div>
          {line('Total HT', celluleB(row.montantHT))}
          {line('Total TVA', celluleB(row.montantTVA))}
          {line('Autre taxe', celluleB(row.autreTaxe))}
          {line('Écart', celluleB(row.ecart))}
          {line('Escompte', celluleB(row.escompte))}
          {!row.valorisee && (
            <div style={{ marginTop: '0.75rem', padding: '0.6rem 0.75rem', background: '#fffbeb', border: '1px solid #fde68a', borderRadius: '6px', fontSize: '0.78rem', color: '#92400e', display: 'flex', gap: '0.5rem' }}>
              <AlertTriangle size={15} style={{ flexShrink: 0, marginTop: '1px' }} />
              <span>Facture non valorisée — {row.motifValorisation || 'motif indisponible'}. Rendue visible (aucune valeur inventée, aucune ligne masquée).</span>
            </div>
          )}

          {/* TASK-076 : montants BRUTS Sage — donnée d'audit non fiable/non déclarable, visible
              uniquement pour investigation manuelle côté ERP. Jamais mêlée aux colonnes de la
              valorisation (Ht/Tva ci-dessus, qui restent « non valorisé » pour cette facture). */}
          {!row.valorisee && (row.htBrut != null || row.tvaBrut != null || row.parafiscaleBrut != null || row.ttcBrut != null) && (
            <div style={{ marginTop: '0.75rem', padding: '0.6rem 0.75rem', background: '#f3f4f6', border: '1px dashed #9ca3af', borderRadius: '6px' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.4rem', marginBottom: '0.4rem' }}>
                <span style={{ background: '#e5e7eb', color: '#4b5563', padding: '2px 8px', borderRadius: '99px', fontSize: '0.68rem', fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.03em' }}>
                  Donnée brute non fiable · non déclarable
                </span>
              </div>
              <div style={{ fontSize: '0.72rem', color: '#6b7280', marginBottom: '0.5rem' }}>
                Montants Sage tels que lus au moment de la détection de l'incohérence — pour comparaison
                manuelle avec Sage uniquement. Jamais réintégrés dans un calcul déclarable.
              </div>
              {line('Total HT (brut Sage)', <span style={{ color: '#6b7280', fontStyle: 'italic' }}>{row.htBrut != null ? formatMoney(row.htBrut) : '—'}</span>)}
              {line('Total TVA (brut Sage)', <span style={{ color: '#6b7280', fontStyle: 'italic' }}>{row.tvaBrut != null ? formatMoney(row.tvaBrut) : '—'}</span>)}
              {line('Total parafiscal (brut Sage)', <span style={{ color: '#6b7280', fontStyle: 'italic' }}>{row.parafiscaleBrut != null ? formatMoney(row.parafiscaleBrut) : '—'}</span>)}
              {line('Total TTC (brut Sage)', <span style={{ color: '#6b7280', fontStyle: 'italic' }}>{row.ttcBrut != null ? formatMoney(row.ttcBrut) : '—'}</span>)}
            </div>
          )}

          <div style={{ fontSize: '0.7rem', fontWeight: 700, textTransform: 'uppercase', letterSpacing: '0.05em', color: 'var(--text-secondary)', margin: '1.25rem 0 0.2rem' }}>Montants & déclaration (familles A/C)</div>
          {line('TTC (fait foi)', formatMoney(row.montantTTC))}
          {line('Réglé (décaissé)', formatMoney(row.regle))}
          {line('Déclaré (DT_Id)', <span style={{ color: isNonNul(row.declare) ? '#4338ca' : undefined }}>{formatMoney(row.declare)}</span>)}
          {line('Reste à déclarer', isNonNul(reste)
            ? <span style={{ color: '#b45309', fontWeight: 700, display: 'inline-flex', alignItems: 'center', gap: '0.25rem' }}><AlertTriangle size={13} />{formatMoney(reste)}</span>
            : <span style={{ color: '#15803d' }}>{formatMoney(reste)}</span>)}
          {line('Solde facture (TTC − Réglé)', isNonNul(solde)
            ? <span style={{ color: '#b45309', fontWeight: 700 }}>{formatMoney(solde)}</span>
            : <span style={{ color: '#15803d' }}>{formatMoney(solde)}</span>)}

          <div style={{ marginTop: '1rem', fontSize: '0.72rem', color: 'var(--text-secondary)', lineHeight: 1.5 }}>
            La TVA fournisseur (Maroc) se déduit au <strong>décaissement</strong> : la base déclarable
            d'une facture est ce qui est <strong>réglé</strong>. Le statut à 3 valeurs (Non déclarable /
            Partiel / Total) est dérivé des affectations marquées <strong>DT_Id</strong> (verrou TASK-028,
            lu jamais modifié). La sélection et la déclaration partielle sont hors de cet écran (option 2).
          </div>
        </div>
      </div>
    </div>
  );
}
