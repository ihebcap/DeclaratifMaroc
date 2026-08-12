import { useState, useEffect, useCallback, useMemo } from 'react';
import { Loader2, FileText, X, AlertTriangle, HelpCircle, RefreshCw } from 'lucide-react';
import type { ColDef, FilterChangedEvent } from 'ag-grid-community';
import { ApbsGrid } from './grid/ApbsGrid';
import { CustomListFilter } from './grid/CustomListFilter';
import { agFilterModelToLegacy } from './grid/agGridFilterModel';
import { formatMoney, formatDate } from './utils';
import api from './api';
import { agregerErreursValorisation, CODE_METADATA_VALORISATION, type MotifValorisation } from './valorisationErreurs';

// ─── Interrogation « Factures » (TASK-041 / TASK-204 AG Grid) ──────────────────

type ListFilterValue = string | string[];

type Col = {
  key: string;
  label: string;
  align?: 'left' | 'right' | 'center';
  sortKey?: 'date' | 'ttc' | 'solde' | 'numero';
  filterType?: 'list' | 'text';
  width?: string;
  famille?: 'A' | 'B' | 'C' | 'D';
};

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
  { key: 'echeanceLegale', label: 'Échéance légale', width: '120px', famille: 'D' },
  { key: 'ecartJours', label: 'Écart délai (j)', align: 'right', width: '130px', famille: 'D' },
];

const isNonNul = (v: number) => Math.abs(v ?? 0) > 0.005;
const todayIso = () => new Date().toISOString().slice(0, 10);
const yearStartIso = () => `${new Date().getFullYear()}-01-01`;

const anneeEstPlausible = (isoDate: string) => {
  const annee = Number(isoDate.slice(0, 4));
  return Number.isFinite(annee) && annee >= 1900 && annee <= 2100;
};

function StatutBadge({ statut }: { statut: string }) {
  const map: Record<string, { bg: string, text: string, label: string }> = {
    Total: { bg: 'var(--status-ok-bg)', text: 'var(--status-ok-text)', label: 'Total' },
    Partiel: { bg: '#fef3c7', text: 'var(--status-warning-text-alt)', label: 'Partiel' },
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
  return <span style={{ background: c.bg, color: c.text, padding: '1px 6px', borderRadius: '4px', fontSize: '0.7rem', fontWeight: 500 }}>{origine || '—'}</span>;
}

function CelluleB({ value, valorisee, motif, brutValue }: { value: number | null, valorisee: boolean, motif?: string, brutValue?: number | null }) {
  if (!valorisee) {
    return (
      <span
        title={motif ? `Valorisation manquante : ${motif}` : 'Valorisation TVA indisponible pour cette facture'}
        style={{ color: 'var(--text-secondary)', fontStyle: 'italic', cursor: 'help', display: 'inline-flex', alignItems: 'center', gap: '0.2rem' }}
      >
        <AlertTriangle size={11} style={{ color: 'var(--status-warning-text-alt)' }} />
        <span>non valorisé</span>
      </span>
    );
  }
  if (value === null) {
    if (brutValue !== undefined && brutValue !== null) {
      return (
        <span title="Donnée brute Sage disponible mais valorisation globale incomplète" style={{ color: 'var(--text-secondary)' }}>
          {formatMoney(brutValue)}
        </span>
      );
    }
    return <span style={{ color: 'var(--text-secondary)' }}>—</span>;
  }
  return <span>{formatMoney(value)}</span>;
}

function CelluleD({ ecartJours, soldeFacture }: { ecartJours: number | null, soldeFacture: number }) {
  if (ecartJours === null) {
    return (
      <span
        title="Facture soldée mais date de décaissement/rapprochement non encore connue — aucun écart calculable sans fabriquer de donnée"
        style={{ color: 'var(--text-secondary)', fontStyle: 'italic', cursor: 'help', display: 'inline-flex', alignItems: 'center', gap: '0.2rem' }}
      >
        <HelpCircle size={11} />
        <span>n.c.</span>
      </span>
    );
  }
  const estSoldee = Math.abs(soldeFacture ?? 0) <= 0.005;
  const depassement = ecartJours > 0;
  const isOk = ecartJours <= 0;

  const title = estSoldee
    ? (depassement ? `Retard de paiement effectif : ${ecartJours} jour(s) après l'échéance légale` : `Paiement à temps : ${Math.abs(ecartJours)} jour(s) avant l'échéance légale`)
    : (depassement ? `Provisoire — retard en cours : ${ecartJours} jour(s) au-delà de l'échéance légale` : `Provisoire — reste ${Math.abs(ecartJours)} jour(s) avant l'échéance légale`);

  const bg = isOk ? 'var(--status-ok-bg)' : (estSoldee ? 'var(--status-blocking-bg)' : '#fef3c7');
  const text = isOk ? 'var(--status-ok-text)' : (estSoldee ? 'var(--status-blocking-text)' : 'var(--status-warning-text-alt)');
  const suffix = estSoldee ? '' : '*';

  return (
    <span title={title} style={{ background: bg, color: text, padding: '2px 6px', borderRadius: '4px', fontSize: '0.75rem', fontWeight: 600 }}>
      {ecartJours > 0 ? `+${ecartJours}` : ecartJours} j{suffix}
    </span>
  );
}

export function FactureInterrogation({
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

  const [debouncedDebut, setDebouncedDebut] = useState(debut);
  const [debouncedFin, setDebouncedFin] = useState(fin);
  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedDebut(debut);
      setDebouncedFin(fin);
    }, 350);
    return () => clearTimeout(timer);
  }, [debut, fin]);

  const [refreshing, setRefreshing] = useState(false);
  const [page, setPage] = useState(1);
  const [filters, setFilters] = useState<Record<string, ListFilterValue>>({});
  const [sortConfig, _setSortConfig] = useState<{ key: 'date' | 'ttc' | 'solde' | 'numero', desc: boolean }>({ key: 'date', desc: true });
  const [origineOptions, setOrigineOptions] = useState<{ label: string, value: string }[]>([]);
  const [numeroOptions, setNumeroOptions] = useState<string[]>([]);
  const [referenceOptions, setReferenceOptions] = useState<string[]>([]);
  const [detailRow, setDetailRow] = useState<any | null>(null);

  const size = 100;

  useEffect(() => {
    if (debouncedDebut > debouncedFin) return;
    let cancelled = false;
    (async () => {
      try {
        const res = await api.get('/factures/distincts', { params: { debut: debouncedDebut, fin: debouncedFin, soId: societeId } });
        if (cancelled) return;
        const origines = (res.data.origines || []).map((o: any) => ({ label: o.libelle, value: o.libelle }));
        setOrigineOptions(origines);
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
  }, [debouncedDebut, debouncedFin, societeId]);

  const fetchPage = useCallback(async () => {
    if (debouncedDebut > debouncedFin) return;
    setLoading(true);
    try {
      const params: any = {
        debut: debouncedDebut,
        fin: debouncedFin,
        soId: societeId,
        page,
        size,
        sort: `${sortConfig.key}_${sortConfig.desc ? 'desc' : 'asc'}`,
      };
      const numero = filters['factureNumero'];
      if (Array.isArray(numero) && numero.length > 0) params.numero = numero;
      const fournisseur = filters['fournisseur'];
      if (typeof fournisseur === 'string' && fournisseur.trim() !== '') params.fournisseur = fournisseur.trim();
      const reference = filters['reference'];
      if (Array.isArray(reference) && reference.length > 0) params.reference = reference;
      const origine = filters['origine'];
      if (Array.isArray(origine) && origine.length > 0) params.origine = origine;
      const statut = filters['statut'];
      if (Array.isArray(statut) && statut.length > 0) params.statut = statut;

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
  }, [debouncedDebut, debouncedFin, page, filters, sortConfig, showToast, societeId]);

  useEffect(() => { setPage(1); }, [filters, sortConfig, debouncedDebut, debouncedFin]);
  useEffect(() => { fetchPage(); }, [fetchPage]);

  const [valorisationReport, setValorisationReport] = useState<ValorisationReport | null>(null);

  const handleRefreshValorisation = useCallback(async () => {
    setRefreshing(true);
    try {
      const res = await api.post('/factures/rafraichir-valorisation', null, { params: { debut, fin, soId: societeId } });
      const n = res.data?.facturesTraitees ?? 0;
      const nbErr = res.data?.nbErreurs ?? 0;
      const meList: MotifValorisation[] = res.data?.erreurs ?? [];
      if (nbErr > 0) {
        setValorisationReport({ facturesTraitees: n, nbErreurs: nbErr, erreurs: meList });
        showToast(`Valorisation — ${n} traitée(s), ${nbErr} en erreur (cliquez pour voir le détail)`, 'error');
      } else {
        setValorisationReport(null);
        showToast(`Valorisation rafraîchie — ${n} facture(s) traitée(s)`, 'success');
      }
      await fetchPage();
    } catch (e: any) {
      console.error(e);
      const message = e?.response?.status === 409
        ? (e.response.data?.Message || e.response.data?.message || 'Rafraîchissement déjà en cours pour cette société.')
        : 'Échec du rafraîchissement de la valorisation (lecture Sage)';
      showToast(message, 'error');
    } finally {
      setRefreshing(false);
    }
  }, [debut, fin, fetchPage, showToast, societeId]);

  const renderCell = (key: string, row: any) => {
    const v = row[key];
    switch (key) {
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
          ? <span style={{ color: 'var(--status-warning-text-alt)', fontWeight: 700 }}>{formatMoney(v)}</span>
          : <span style={{ color: 'var(--text-secondary)' }}>{formatMoney(v)}</span>;
      case 'soldeFacture':
        return isNonNul(v)
          ? <span style={{ color: 'var(--status-warning-text-alt)', fontWeight: 600 }}>{formatMoney(v)}</span>
          : <span style={{ color: 'var(--status-ok-text)' }}>{formatMoney(v)}</span>;
      case 'statut': return <StatutBadge statut={v} />;
      case 'origine': return <OrigineChip origine={v} />;
      case 'echeanceLegale': return v ? formatDate(v) : <span style={{ color: 'var(--text-secondary)' }}>—</span>;
      case 'ecartJours': return <CelluleD ecartJours={v} soldeFacture={row.soldeFacture ?? 0} />;
      default: return v;
    }
  };

  // Mise à jour directe d'une seule clé de `filters`, sans dépendre du cycle AG Grid
  // filterChangedCallback → onFilterChanged → getFilterModel() (cf. CustomListFilter).
  const setColumnFilter = useCallback((key: string, values: string[]) => {
    setFilters(prev => {
      const next = { ...prev };
      if (values.length > 0) next[key] = values; else delete next[key];
      return next;
    });
  }, []);

  const columnDefs: ColDef[] = useMemo(() => {
    return COLUMNS.map((col) => {
      const isNumeric = ['montantHT', 'montantTVA', 'autreTaxe', 'ecart', 'escompte', 'montantTTC', 'regle', 'declare', 'resteADeclarer', 'soldeFacture', 'ecartJours'].includes(col.key);
      const w = col.width ? parseInt(col.width, 10) : 130;

      let filterComponent: any = undefined;
      let filterParams: any = undefined;

      if (col.filterType === 'list') {
        filterComponent = CustomListFilter;
        if (col.key === 'statut') {
          filterParams = { options: [{ label: 'Total', value: 'Total' }, { label: 'Partiel', value: 'Partiel' }, { label: 'Non déclarable', value: 'NonDeclarable' }] };
        } else if (col.key === 'origine') {
          filterParams = { options: origineOptions };
        } else if (col.key === 'factureNumero') {
          filterParams = { options: numeroOptions.map(v => ({ label: v, value: v })) };
        } else if (col.key === 'reference') {
          filterParams = { options: referenceOptions.map(v => ({ label: v, value: v })) };
        }
        filterParams = { ...filterParams, onSelectionChange: (values: string[]) => setColumnFilter(col.key, values) };
      } else if (col.filterType === 'text') {
        filterComponent = 'agTextColumnFilter';
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
  }, [origineOptions, numeroOptions, referenceOptions, setColumnFilter]);

  // Écran server-side (pagination) : les filtres d'en-tête AG Grid doivent alimenter `filters`
  // (state qui construit la requête, cf. fetchPage) plutôt que de ne filtrer que la page chargée.
  // Garde-fou anti-boucle : agFilterModelToLegacy recrée un objet à chaque appel ; sans
  // comparaison de contenu, un filterChanged réémis sans changement réel relance fetchPage en boucle.
  const handleFilterChanged = useCallback((event: FilterChangedEvent) => {
    const next = agFilterModelToLegacy(event.api.getFilterModel());
    setFilters(prev => (JSON.stringify(prev) === JSON.stringify(next) ? prev : next));
  }, []);

  const totalPages = Math.ceil(total / size) || 1;
  const activeFilterCount = Object.keys(filters).length;

  return (
    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', height: '100%', overflow: 'hidden' }}>
      <div style={{ padding: '0.75rem 1rem', borderBottom: '1px solid var(--border-color)', background: 'white', display: 'flex', alignItems: 'center', gap: '0.6rem' }}>
        <FileText size={20} style={{ color: 'var(--accent-primary)' }} />
        <div>
          <h2 style={{ margin: 0, fontSize: '1.05rem', fontWeight: 600 }}>Factures</h2>
          <div style={{ fontSize: '0.72rem', color: 'var(--text-secondary)' }}>Interrogation — pivot facture, lecture seule (période obligatoire)</div>
        </div>
      </div>

      <div style={{ flexGrow: 1, minHeight: 0, position: 'relative', padding: '0.4rem 1rem' }}>
        <ApbsGrid
          rowData={data}
          columnDefs={columnDefs}
          onRowClicked={(params) => setDetailRow(params.data)}
          onFilterChanged={handleFilterChanged}
          height="100%"
          showColumnSelector={true}
          showExportButton={true}
          exportFileName="factures_interrogation.xlsx"
          toolbarLeft={
            <>
              <label style={{ color: 'var(--text-secondary)' }}>Du</label>
              <input type="date" value={debut} max={fin} onChange={e => anneeEstPlausible(e.target.value) && setDebut(e.target.value)} className="form-input" style={{ fontSize: '0.8rem', padding: '0.25rem 0.5rem' }} />
              <label style={{ color: 'var(--text-secondary)' }}>Au</label>
              <input type="date" value={fin} min={debut} onChange={e => anneeEstPlausible(e.target.value) && setFin(e.target.value)} className="form-input" style={{ fontSize: '0.8rem', padding: '0.25rem 0.5rem' }} />
              <button
                className="btn"
                onClick={handleRefreshValorisation}
                disabled={refreshing}
                title="Lit les OM Sage pour la période et remplit le cache de valorisation TVA (famille B)"
                style={{ display: 'inline-flex', alignItems: 'center', gap: '0.35rem', fontSize: '0.8rem', padding: '0.25rem 0.6rem' }}
              >
                <RefreshCw size={14} className={refreshing ? 'animate-spin' : undefined} />
                {refreshing ? 'Valorisation…' : 'Rafraîchir valorisation'}
              </button>
              {loading && <Loader2 size={15} className="animate-spin" style={{ color: 'var(--accent-primary)' }} />}
              <span>Factures : <strong>{total}</strong></span>
              {activeFilterCount > 0 && (
                <button className="btn" onClick={() => setFilters({})} style={{ background: 'transparent', border: 'none', color: 'var(--accent-primary)', textDecoration: 'underline', padding: 0, fontSize: '0.8rem', cursor: 'pointer' }}>
                  Effacer filtres ({activeFilterCount})
                </button>
              )}
            </>
          }
        />
      </div>

      <div style={{ padding: '0.5rem 1rem', borderTop: '1px solid var(--border-color)', display: 'flex', justifyContent: 'space-between', alignItems: 'center', background: 'white' }}>
        <span style={{ fontSize: '0.8rem', color: 'var(--text-secondary)' }}>Page {page} sur {totalPages}</span>
        <div style={{ display: 'flex', gap: '0.5rem' }}>
          <button onClick={() => setPage(p => Math.max(1, p - 1))} disabled={page === 1} className="btn" style={{ padding: '0.25rem 0.75rem', fontSize: '0.8rem' }}>Précédent</button>
          <button onClick={() => setPage(p => Math.min(totalPages, p + 1))} disabled={page >= totalPages} className="btn" style={{ padding: '0.25rem 0.75rem', fontSize: '0.8rem' }}>Suivant</button>
        </div>
      </div>

      {detailRow && <FactureDetail row={detailRow} onClose={() => setDetailRow(null)} />}
      {valorisationReport && <RapportValorisationModal report={valorisationReport} onClose={() => setValorisationReport(null)} />}
    </div>
  );
}

type ValorisationReport = {
  facturesTraitees: number;
  nbErreurs: number;
  erreurs: MotifValorisation[];
};

function RapportValorisationModal({ report, onClose }: { report: ValorisationReport; onClose: () => void }) {
  const grouped = useMemo(
    () => agregerErreursValorisation(report.erreurs, CODE_METADATA_VALORISATION),
    [report],
  );

  return (
    <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.5)', zIndex: 9999, display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '1rem' }} onClick={onClose}>
      <div style={{ background: 'white', borderRadius: '12px', width: '100%', maxWidth: '720px', maxHeight: '90vh', display: 'flex', flexDirection: 'column', boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1)' }} onClick={e => e.stopPropagation()}>
        <div style={{ padding: '1.25rem 1.5rem', borderBottom: '1px solid var(--border-color)', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
            <AlertTriangle style={{ color: 'var(--status-warning-text-alt, #d97706)' }} size={22} />
            <div>
              <h3 style={{ margin: 0, fontSize: '1.1rem', fontWeight: 600 }}>Détail des erreurs de valorisation</h3>
              <p style={{ margin: 0, fontSize: '0.82rem', color: 'var(--text-secondary)' }}>
                {report.facturesTraitees} facture(s) traitée(s) · {report.nbErreurs} anomalie(s) détectée(s)
              </p>
            </div>
          </div>
          <button onClick={onClose} style={{ background: 'none', border: 'none', cursor: 'pointer', color: 'var(--text-secondary)', padding: '0.25rem' }}>
            <X size={20} />
          </button>
        </div>

        <div style={{ padding: '1.5rem', overflowY: 'auto', flex: 1 }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.85rem' }}>
            <thead>
              <tr style={{ borderBottom: '2px solid var(--border-color)', textAlign: 'left', color: 'var(--text-secondary)' }}>
                <th style={{ padding: '0.5rem' }}>Motif / Code</th>
                <th style={{ padding: '0.5rem' }}>Type</th>
                <th style={{ padding: '0.5rem', textAlign: 'right' }}>Nombre</th>
                <th style={{ padding: '0.5rem' }}>Exemples de pièces</th>
              </tr>
            </thead>
            <tbody>
              {grouped.map((g) => (
                <tr key={g.code} style={{ borderBottom: '1px solid var(--border-color)' }}>
                  <td style={{ padding: '0.75rem 0.5rem', fontWeight: 600 }}>
                    {g.label}
                    <div style={{ fontSize: '0.75rem', color: 'var(--text-secondary)', fontWeight: 400 }}>{g.code}</div>
                  </td>
                  <td style={{ padding: '0.75rem 0.5rem' }}>
                    {g.qualiteDonnees ? (
                      <span style={{ fontSize: '0.75rem', padding: '0.2rem 0.5rem', borderRadius: '4px', background: '#f1f5f9', color: 'var(--text-secondary)' }}>
                        Fiche tiers (Sage)
                      </span>
                    ) : (
                      <span style={{ fontSize: '0.75rem', padding: '0.2rem 0.5rem', borderRadius: '4px', background: 'rgba(239, 68, 68, 0.1)', color: '#ef4444' }}>
                        Anomalie calcul / FGR
                      </span>
                    )}
                  </td>
                  <td style={{ padding: '0.75rem 0.5rem', textAlign: 'right', fontWeight: 700 }}>
                    {g.count}
                  </td>
                  <td style={{ padding: '0.75rem 0.5rem', color: 'var(--text-secondary)', fontSize: '0.8rem' }}>
                    {g.exemples.length > 0 ? g.exemples.join(', ') : '—'}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <div style={{ padding: '1rem 1.5rem', borderTop: '1px solid var(--border-color)', display: 'flex', justifyContent: 'flex-end' }}>
          <button onClick={onClose} className="btn" style={{ minWidth: '100px', padding: '0.4rem 1rem' }}>
            Fermer
          </button>
        </div>
      </div>
    </div>
  );
}

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
    <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.4)', zIndex: 1000, display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '1rem' }} onClick={onClose}>
      <div style={{ background: 'white', borderRadius: 'var(--radius-md)', width: '560px', maxWidth: '100%', maxHeight: '90vh', overflow: 'auto', padding: '1.25rem', boxShadow: '0 10px 25px rgba(0,0,0,0.15)' }} onClick={e => e.stopPropagation()}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '1rem', borderBottom: '1px solid var(--border-color)', paddingBottom: '0.75rem' }}>
          <div>
            <div style={{ fontSize: '0.75rem', color: 'var(--text-secondary)', textTransform: 'uppercase', letterSpacing: '0.05em' }}>Détail Facture</div>
            <h3 style={{ margin: '0.2rem 0 0 0', fontSize: '1.15rem', fontWeight: 700 }}>{row.factureNumero || '—'}</h3>
          </div>
          <button onClick={onClose} style={{ background: 'transparent', border: 'none', cursor: 'pointer', padding: '0.25rem', color: 'var(--text-secondary)' }}><X size={18} /></button>
        </div>

        <div style={{ marginBottom: '1rem' }}>
          <div style={{ fontSize: '0.75rem', fontWeight: 700, color: 'var(--accent-primary)', marginBottom: '0.4rem', textTransform: 'uppercase' }}>A — Identité Facture</div>
          {line('Date', formatDate(row.date))}
          {line('Fournisseur', `${row.fournisseurCode || '—'}${row.fournisseurIntitule ? ` · ${row.fournisseurIntitule}` : ''}`)}
          {line('Référence', row.reference || '—')}
          {line('Montant TTC', formatMoney(row.montantTTC))}
          {line('Solde Facture', isNonNul(solde) ? <span style={{ color: 'var(--status-warning-text-alt)' }}>{formatMoney(solde)}</span> : <span style={{ color: 'var(--status-ok-text)' }}>{formatMoney(solde)}</span>)}
        </div>

        <div style={{ marginBottom: '1rem' }}>
          <div style={{ fontSize: '0.75rem', fontWeight: 700, color: 'var(--accent-primary)', marginBottom: '0.4rem', textTransform: 'uppercase' }}>B — Valorisation TVA (cache TASK-024)</div>
          {line('Origine', <OrigineChip origine={row.origine} />)}
          {line('Total HT', celluleB(row.montantHT))}
          {line('Total TVA', celluleB(row.montantTVA))}
          {line('Autre taxe', celluleB(row.autreTaxe))}
          {line('Écart d\'équilibre', celluleB(row.ecart))}
          {line('Escompte', celluleB(row.escompte))}
        </div>

        <div>
          <div style={{ fontSize: '0.75rem', fontWeight: 700, color: 'var(--accent-primary)', marginBottom: '0.4rem', textTransform: 'uppercase' }}>C — Statut Déclaration (agrégat DT_Id)</div>
          {line('Statut', <StatutBadge statut={row.statut} />)}
          {line('Montant Réglé', formatMoney(row.regle))}
          {line('Montant Déclaré', formatMoney(row.declare))}
          {line('Reste à déclarer', isNonNul(reste) ? <span style={{ color: 'var(--status-warning-text-alt)', fontWeight: 700 }}>{formatMoney(reste)}</span> : formatMoney(reste))}
        </div>
      </div>
    </div>
  );
}
