import { useState, useEffect, useCallback, useMemo } from 'react';
import type { ColDef, GridApi, GridReadyEvent } from 'ag-grid-community';
import { ApbsGrid } from './grid/ApbsGrid';
import { CustomListFilter } from './grid/CustomListFilter';
import { formatMoney } from './utils';
import api from './api';
import { Loader2, CheckSquare, RefreshCw, Calculator, X } from 'lucide-react';
import type { DomaineTVA } from './DeclarationStepper';
import { relireDepuisSage, resynchroniserLignesBulk, enregistrerSaisieSoldeInitial } from './api';

const estSoldeInitialASaisir = (motif: unknown) => typeof motif === 'string' && motif.startsWith('Solde initial :');

function SaisieSoldeInitialModal({ row, declarationId, onClose, onSaved, showToast }: {
    row: any,
    declarationId: string,
    onClose: () => void,
    onSaved: () => void,
    showToast: (m: string, t?: 'success' | 'error' | 'warning') => void,
}) {
    const [taux, setTaux] = useState<number | ''>('');
    const [montantTva, setMontantTva] = useState<number | ''>('');
    const [submitting, setSubmitting] = useState(false);

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        if (montantTva === '' || Number(montantTva) < 0) { showToast('Le montant de TVA est obligatoire (≥ 0).', 'error'); return; }
        setSubmitting(true);
        try {
            const { resolue } = await enregistrerSaisieSoldeInitial(declarationId, row.ecId, Number(taux || 0), Number(montantTva));
            showToast(resolue ? 'Solde initial intégré à la déclaration.' : 'Saisie enregistrée, mais la ligne reste en anomalie.', resolue ? 'success' : 'warning');
            onSaved();
        } catch (e: any) {
            showToast(e?.response?.data?.Message || e?.response?.data?.message || 'Échec de l\'enregistrement de la saisie.', 'error');
        } finally {
            setSubmitting(false);
        }
    };

    return (
        <div style={{ position: 'fixed', inset: 0, zIndex: 9999, display: 'flex', alignItems: 'center', justifyContent: 'center', backgroundColor: 'rgba(0,0,0,0.5)' }}>
            <div style={{ background: 'white', borderRadius: '8px', width: '100%', maxWidth: '440px', boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1), 0 10px 10px -5px rgba(0,0,0,0.04)' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '1.25rem 1.5rem', borderBottom: '1px solid var(--border-color)' }}>
                    <h3 style={{ margin: 0, fontSize: '1.1rem', fontWeight: 600 }}>Solde initial — saisie TVA</h3>
                    <button onClick={onClose} style={{ background: 'transparent', border: 'none', cursor: 'pointer', color: 'var(--text-secondary)' }}><X size={20} /></button>
                </div>
                <form onSubmit={handleSubmit} style={{ padding: '1.5rem', display: 'flex', flexDirection: 'column', gap: '1rem' }}>
                    <p style={{ margin: 0, fontSize: '0.82rem', color: 'var(--text-secondary)' }}>
                        Facture <strong>{row.factureNumero || row.ecId}</strong> — montant du solde (TTC) : <strong>{formatMoney(row.ttc ?? row.montantTTC ?? 0)}</strong>.
                        Ce solde initial n'a aucun détail de TVA côté Sage : saisissez le taux et le montant de TVA pour l'intégrer à la déclaration (le HT sera déduit : TTC − TVA).
                    </p>
                    <div style={{ display: 'flex', gap: '1rem' }}>
                        <div style={{ display: 'flex', flexDirection: 'column', gap: '0.4rem', flex: 1 }}>
                            <label style={{ fontSize: '0.8rem', fontWeight: 500 }}>Taux de TVA (%)</label>
                            <input type="number" step="0.01" min={0} className="form-input" value={taux} onChange={e => setTaux(e.target.value === '' ? '' : Number(e.target.value))} />
                        </div>
                        <div style={{ display: 'flex', flexDirection: 'column', gap: '0.4rem', flex: 1 }}>
                            <label style={{ fontSize: '0.8rem', fontWeight: 500 }}>Montant de TVA</label>
                            <input type="number" step="0.01" min={0} className="form-input" value={montantTva} onChange={e => setMontantTva(e.target.value === '' ? '' : Number(e.target.value))} required />
                        </div>
                    </div>
                    <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '1rem', marginTop: '0.5rem' }}>
                        <button type="button" onClick={onClose} disabled={submitting} className="btn" style={{ background: 'transparent', border: '1px solid var(--border-color)', padding: '0.5rem 1rem', borderRadius: '4px' }}>Annuler</button>
                        <button type="submit" disabled={submitting} className="btn btn-primary" style={{ background: 'var(--accent-primary)', color: 'white', border: 'none', padding: '0.5rem 1.5rem', borderRadius: '4px', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                            {submitting && <Loader2 size={16} className="animate-spin" />}
                            Enregistrer
                        </button>
                    </div>
                </form>
            </div>
        </div>
    );
}

export function DomainGrid({ 
    declarationId, 
    domaine, 
    onActionDone, 
    showToast,
    initialFilters,
    readonly = false,
    onRowClick,
    columns,
    colsStorageKey: _colsStorageKey,
    codeActiviteOptions,
    showResynchroniserAction
}: {
    declarationId: string,
    domaine?: DomaineTVA,
    onActionDone: () => void,
    showToast: (m: string, t?: any) => void,
    initialFilters?: Record<string, any>,
    readonly?: boolean,
    onRowClick?: (row: any) => void,
    columns?: { key: string, label: string, filterType: 'list' | 'text' | 'number' | 'date', width?: string, derived?: boolean, editable?: boolean }[],
    colsStorageKey?: string,
    codeActiviteOptions?: { value: string, label: string }[],
    showResynchroniserAction?: boolean
}) {
    const [data, setData] = useState<any[]>([]);
    const [total, setTotal] = useState(0);
    const [distincts, setDistincts] = useState<Record<string, string[]>>({});
    const [loading, setLoading] = useState(false);
    const [gridApi, setGridApi] = useState<GridApi | null>(null);

    const [page, setPage] = useState(1);
    const [filters, setFilters] = useState<Record<string, string | string[]>>({});
    const [sortConfig, _setSortConfig] = useState<{ key: string, desc: boolean } | null>(null);

    const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
    const [selectAllFilters, setSelectAllFilters] = useState(false);

    const size = 100;
    const ligneEcart = (row: any) => Number(row?.montantHT || 0) + Number(row?.montantTVA || 0) - Number(row?.montantTTC || 0);

    type ColumnDef = { key: string, label: string, filterType: 'list' | 'text' | 'number' | 'date', width?: string, derived?: boolean, editable?: boolean };

    const defaultColumns: ColumnDef[] = [
        { key: 'factureNumero', label: 'N° Facture', filterType: 'text' },
        { key: 'tiers', label: 'Tiers', filterType: 'text' },
        { key: 'origine', label: 'Origine', filterType: 'list' },
        { key: 'montantHT', label: 'Montant HT', filterType: 'number' },
        { key: 'tauxTVA', label: 'Taux TVA', filterType: 'list' },
        { key: 'montantTTC', label: 'Montant TTC', filterType: 'number' },
        { key: 'source', label: 'Source', filterType: 'list' },
        { key: 'statutLigne', label: 'Statut', filterType: 'list' },
        { key: 'motif', label: 'Motif Écartement', filterType: 'text', width: '280px' }
    ];
    const gridColumns: ColumnDef[] = columns || defaultColumns;

    const fetchPage = useCallback(async () => {
        setLoading(true);
        try {
            const backendFilters: any = {};
            for (const [k, v] of Object.entries(filters)) {
                const outK = k === 'statutLigne' ? 'etat' : k;
                backendFilters[outK] = v;
            }

            const res = await api.get(`/declarations/${declarationId}/lignes`, {
                params: {
                    domaine: domaine || 'Decaissement',
                    page,
                    size,
                    sort: sortConfig ? `${sortConfig.key}:${sortConfig.desc ? 'desc' : 'asc'}` : undefined,
                    filter: Object.keys(backendFilters).length > 0 ? JSON.stringify(backendFilters) : undefined
                }
            });
            const newItems = res.data.items || res.data.data || (Array.isArray(res.data) ? res.data : []);
            setData(newItems);
            setTotal(res.data.totalCount ?? res.data.total ?? newItems.length);
            setDistincts(res.data.distincts || {});
        } catch (e) {
            console.error(e);
            showToast('Erreur lors du chargement des données', 'error');
        } finally {
            setLoading(false);
        }
    }, [declarationId, domaine, page, filters, sortConfig, showToast]);

    useEffect(() => {
        setPage(1);
    }, [filters, sortConfig, domaine]);

    useEffect(() => {
        fetchPage();
        setSelectedIds(new Set());
        setSelectAllFilters(false);
    }, [fetchPage]);

    useEffect(() => {
        if (initialFilters) setFilters(initialFilters);
    }, [initialFilters, domaine]);

    const doBulkAction = async (statut: 'Intégrée' | 'Exclue' | 'Reportée' | 'Proposée') => {
        if (selectedIds.size === 0 && !selectAllFilters) return;
        try {
            const etatIndex = ['Proposée', 'Intégrée', 'Exclue', 'Reportée', 'Écartée'].indexOf(statut);
            await api.post(`/declarations/${declarationId}/lignes:bulk`, {
                ligneIds: selectAllFilters ? undefined : Array.from(selectedIds),
                filter: selectAllFilters ? JSON.stringify(filters) : undefined,
                domaine: selectAllFilters ? (domaine || 'Decaissement') : undefined,
                etat: etatIndex
            });
            showToast(`${selectAllFilters ? total : selectedIds.size} ligne(s) marquée(s) comme ${statut}`);
            setSelectedIds(new Set());
            setSelectAllFilters(false);
            fetchPage();
            onActionDone();
        } catch (e) {
            showToast('Erreur lors de l\'action', 'error');
        }
    };

    const [codeActiviteMasse, setCodeActiviteMasse] = useState('');
    const [affectationEnCours, setAffectationEnCours] = useState(false);
    const doBulkCodeActivite = async () => {
        if ((selectedIds.size === 0 && !selectAllFilters) || !codeActiviteMasse) return;
        setAffectationEnCours(true);
        try {
            await api.post(`/declarations/${declarationId}/lignes/code-activite:bulk`, {
                ligneIds: selectAllFilters ? undefined : Array.from(selectedIds),
                filter: selectAllFilters ? JSON.stringify(filters) : undefined,
                domaine: selectAllFilters ? (domaine || 'Decaissement') : undefined,
                codeActivite: codeActiviteMasse
            });
            showToast(`Code activité affecté à ${selectAllFilters ? total : selectedIds.size} ligne(s).`);
            setSelectedIds(new Set());
            setSelectAllFilters(false);
            setCodeActiviteMasse('');
            fetchPage();
            onActionDone();
        } catch (e: any) {
            const message = e?.response?.data?.Message || e?.response?.data?.message || 'Erreur lors de l\'affectation en masse du code activité';
            showToast(message, 'error');
        } finally {
            setAffectationEnCours(false);
        }
    };

    const SEUIL_CONFIRMATION_RESYNC = 20;
    const [resynchroMasseEnCours, setResynchroMasseEnCours] = useState(false);
    const doBulkResynchroniser = async () => {
        if (selectedIds.size === 0 && !selectAllFilters) return;
        const nb = selectAllFilters ? total : selectedIds.size;
        if (nb >= SEUIL_CONFIRMATION_RESYNC) {
            const ok = window.confirm(
                `Resynchroniser ${nb} ligne(s) depuis Sage ?\n\n` +
                `Chaque pièce est relue une par une (séquentiel) — cette opération peut prendre ` +
                `plusieurs dizaines de secondes sur un volume important.`
            );
            if (!ok) return;
        }
        setResynchroMasseEnCours(true);
        try {
            const r = await resynchroniserLignesBulk(declarationId, {
                ligneIds: selectAllFilters ? undefined : Array.from(selectedIds),
                filter: selectAllFilters ? JSON.stringify(filters) : undefined,
                domaine: selectAllFilters ? (domaine || 'Decaissement') : undefined,
            });
            let msg = `${r.traitees} pièce(s) resynchronisée(s) : ${r.resolues} résolue(s), ` +
                `${r.toujoursEnAnomalie.length} toujours en anomalie.`;
            if (r.nonTrouvees > 0) msg += ` ${r.nonTrouvees} ignorée(s) (introuvable).`;
            const niveau = r.interrompu
                ? 'warning'
                : (r.toujoursEnAnomalie.length > 0 ? 'warning' : 'success');
            if (r.interrompu) {
                msg += ` Interrompu : ${r.messageInterruption || 'un autre traitement OM a pris le verrou.'} ` +
                    `Les lignes déjà traitées sont conservées — relancez pour le reste.`;
            }
            showToast(msg, niveau);
            setSelectedIds(new Set());
            setSelectAllFilters(false);
            fetchPage();
            onActionDone();
        } catch (e: any) {
            const message = e?.response?.data?.Message || e?.response?.data?.message || 'Échec de la resynchronisation en masse.';
            showToast(message, 'error');
        } finally {
            setResynchroMasseEnCours(false);
        }
    };

    const handleCodeActiviteChange = async (row: any, nouveauCode: string) => {
        try {
            await api.patch(`/declarations/${declarationId}/lignes/${row.id}/code-activite`, { codeActivite: nouveauCode });
            setData(prev => prev.map(r => r.id === row.id ? { ...r, codeActivite: nouveauCode, codeActiviteModifieManuellement: true } : r));
            showToast('Code activité mis à jour.');
        } catch (e: any) {
            const message = e?.response?.data?.Message || 'Erreur lors de la mise à jour du code activité';
            showToast(message, 'error');
        }
    };

    const [resynchronisant, setResynchronisant] = useState<Set<string>>(new Set());
    const [soldeInitialTarget, setSoldeInitialTarget] = useState<any | null>(null);
    const handleResynchroniser = async (row: any) => {
        if (!row.ecId || row.ecId <= 0) return;
        setResynchronisant(prev => new Set(prev).add(row.id));
        try {
            const { resolue } = await relireDepuisSage(declarationId, row.ecId);
            if (resolue) {
                showToast(`Ligne ${row.factureNumero || row.ecId} resynchronisée depuis Sage.`);
                fetchPage();
                onActionDone();
            } else {
                showToast(`Relecture Sage effectuée pour ${row.factureNumero || row.ecId} — la ligne reste en anomalie (motif inchangé).`, 'warning');
            }
        } catch (e: any) {
            const message = e?.response?.data?.Message || e?.response?.data?.message || 'Échec de la relecture Sage.';
            showToast(message, 'error');
        } finally {
            setResynchronisant(prev => { const next = new Set(prev); next.delete(row.id); return next; });
        }
    };

    const handleSelectionChanged = useCallback(() => {
        if (!gridApi) return;
        const selectedNodes = gridApi.getSelectedNodes();
        const ids = new Set<string>();
        selectedNodes.forEach((n) => {
            if (n.data?.id) ids.add(n.data.id);
        });
        setSelectedIds(ids);
    }, [gridApi]);

    const onGridReady = useCallback((params: GridReadyEvent) => {
        setGridApi(params.api);
    }, []);

    const renderCell = (key: string, val: any) => {
        if (['ht', 'tva', 'ttc', 'montantHT', 'montantTVA', 'montantTTC', 'ecart'].includes(key)) return formatMoney(val);
        if (key === 'taux' || key === 'tauxTVA') return `${val}%`;
        if (key === 'etat' || key === 'statutLigne') {
            let label = val;
            if (typeof val === 'number') {
                const etatMap = ['Proposée', 'Intégrée', 'Exclue', 'Reportée', 'Écartée'];
                label = etatMap[val] || val;
            }
            const colors: any = {
                'Proposée': { bg: '#f3f4f6', text: '#4b5563' },
                'Intégrée': { bg: 'var(--status-ok-bg)', text: 'var(--status-ok-text)' },
                'Exclue': { bg: 'var(--status-blocking-bg)', text: 'var(--status-blocking-text)' },
                'Reportée': { bg: '#fef3c7', text: 'var(--status-warning-text-alt)' },
                'Écartée': { bg: '#e5e7eb', text: '#374151', border: '1px solid #9ca3af' },
            };
            const c = colors[label] || colors['Proposée'];
            return <span style={{ background: c.bg, color: c.text, border: c.border || 'none', padding: '2px 8px', borderRadius: '99px', fontSize: '0.75rem', fontWeight: 600 }}>{label}</span>;
        }
        return val;
    };

    const columnDefs: ColDef[] = useMemo(() => {
        const defs: ColDef[] = [];
        if (!readonly) {
            defs.push({
                headerCheckboxSelection: true,
                checkboxSelection: true,
                width: 50,
                pinned: 'left',
                suppressHeaderMenuButton: true,
                resizable: false,
            });
        }

        gridColumns.forEach((col) => {
            const isNumeric = ['montantHT', 'montantTVA', 'montantTTC', 'tauxTVA', 'ecart'].includes(col.key);
            const w = col.width ? parseInt(col.width, 10) : 180;
            const isEditableCodeAct = col.key === 'codeActivite' && col.editable && !readonly && codeActiviteOptions;

            const colDef: ColDef = {
                field: col.key,
                headerName: col.label,
                width: w,
                type: isNumeric ? 'numericColumn' : undefined,
                filter: col.derived ? false : CustomListFilter,
                filterParams: {
                    options: (distincts[col.key] || []).map(v => ({ label: v, value: v }))
                },
                cellRenderer: (p: any) => {
                    const row = p.data;
                    if (!row) return null;
                    const cellVal = col.key === 'ecart' ? ligneEcart(row) : row[col.key];
                    const ecartAnormal = col.key === 'ecart' && Math.abs(cellVal) > 0.005;

                    if (isEditableCodeAct) {
                        return (
                            <select
                                value={cellVal || ''}
                                onChange={(e) => handleCodeActiviteChange(row, e.target.value)}
                                style={{ width: '100%', fontSize: '0.8125rem', padding: '0.15rem' }}
                                onClick={(e) => e.stopPropagation()}
                            >
                                <option value="">(sans activité)</option>
                                {codeActiviteOptions!.map(o => (
                                    <option key={o.value} value={o.value}>{o.label}</option>
                                ))}
                            </select>
                        );
                    }

                    return (
                        <div
                            style={{
                                background: ecartAnormal ? 'var(--status-blocking-bg)' : undefined,
                                color: ecartAnormal ? 'var(--status-blocking-text)' : undefined,
                                fontWeight: ecartAnormal ? 700 : undefined,
                                overflow: 'hidden',
                                textOverflow: 'ellipsis',
                                whiteSpace: 'nowrap',
                                width: '100%',
                            }}
                            title={typeof cellVal === 'string' ? cellVal : undefined}
                        >
                            {renderCell(col.key, cellVal)}
                        </div>
                    );
                }
            };
            defs.push(colDef);
        });

        if (showResynchroniserAction) {
            defs.push({
                headerName: 'Actions',
                width: 250,
                pinned: 'right',
                suppressHeaderMenuButton: true,
                cellRenderer: (p: any) => {
                    const row = p.data;
                    if (!row) return null;
                    return (
                        <div style={{ display: 'flex', alignItems: 'center', gap: '0.4rem', height: '100%' }} onClick={(e) => e.stopPropagation()}>
                            {row.ecId > 0 && estSoldeInitialASaisir(row.motif) && (
                                <button
                                    onClick={() => setSoldeInitialTarget(row)}
                                    title="Saisir le taux et le montant de TVA pour intégrer ce solde initial à la déclaration"
                                    style={{
                                        display: 'inline-flex', alignItems: 'center', gap: '0.3rem',
                                        padding: '2px 9px', borderRadius: '6px', fontSize: '0.72rem', fontWeight: 600,
                                        cursor: 'pointer', background: 'var(--accent-primary)', border: '1px solid var(--accent-primary)',
                                        color: 'white', whiteSpace: 'nowrap',
                                    }}
                                >
                                    <Calculator size={12} />
                                    Saisir TVA
                                </button>
                            )}
                            {row.ecId > 0 && (
                                <button
                                    onClick={() => handleResynchroniser(row)}
                                    disabled={resynchronisant.has(row.id)}
                                    title="Relire cette ligne depuis Sage (ex. après correction d'un montant sur Sage)"
                                    style={{
                                        display: 'inline-flex', alignItems: 'center', gap: '0.3rem',
                                        padding: '2px 9px', borderRadius: '6px', fontSize: '0.72rem', fontWeight: 600,
                                        cursor: resynchronisant.has(row.id) ? 'default' : 'pointer',
                                        background: resynchronisant.has(row.id) ? 'var(--bg-secondary)' : 'white',
                                        border: '1px solid var(--border-color)', color: 'var(--text-primary)', whiteSpace: 'nowrap',
                                    }}
                                >
                                    <RefreshCw size={12} className={resynchronisant.has(row.id) ? 'animate-spin' : undefined} />
                                    Resynchroniser
                                </button>
                            )}
                        </div>
                    );
                }
            });
        }

        return defs;
    }, [gridColumns, readonly, codeActiviteOptions, distincts, showResynchroniserAction, resynchronisant]);

    const totalPages = Math.ceil(total / size);
    const nbSansCodeActivitePage = data.filter(r => !((r as unknown as Record<string, string>).codeActivite ?? '').trim()).length;

    return (
        <div style={{ background: 'white', borderRadius: '8px', border: '1px solid var(--border-color)', display: 'flex', flexDirection: 'column', height: '100%', overflow: 'hidden' }}>

            {/* Toolbar */}
            <div style={{ padding: '0.75rem 1rem', borderBottom: '1px solid var(--border-color)', display: 'flex', justifyContent: 'space-between', alignItems: 'center', background: 'var(--bg-secondary)' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
                    {!readonly && (
                        <>
                            <span style={{ fontSize: '0.875rem', fontWeight: 600 }}>
                                Sélectionnées : {selectAllFilters ? total : selectedIds.size}
                            </span>
                            {(selectedIds.size > 0 || selectAllFilters) && (
                                <div style={{ display: 'flex', gap: '0.5rem' }}>
                                    <button title="Ces lignes seront COMPTÉES dans la TVA de cette déclaration." onClick={() => doBulkAction('Intégrée')} className="btn" style={{ background: 'var(--status-ok-bg)', color: 'var(--status-ok-text)', border: '1px solid #bbf7d0', padding: '0.25rem 0.75rem', display: 'flex', alignItems: 'center', gap: '0.25rem', fontSize: '0.75rem' }}><CheckSquare size={14}/> Intégrer</button>
                                    <button title="Annule la décision manuelle : ces lignes repassent à l'état proposé par l'application." onClick={() => doBulkAction('Proposée')} className="btn" style={{ background: 'white', color: 'var(--text-secondary)', border: '1px solid var(--border-color)', padding: '0.25rem 0.75rem', display: 'flex', alignItems: 'center', gap: '0.25rem', fontSize: '0.75rem' }}>Réinitialiser</button>
                                    {showResynchroniserAction && (
                                        <button onClick={doBulkResynchroniser} disabled={resynchroMasseEnCours} className="btn" style={{ background: 'white', color: 'var(--accent-primary)', border: '1px solid var(--accent-primary)', padding: '0.25rem 0.75rem', display: 'flex', alignItems: 'center', gap: '0.25rem', fontSize: '0.75rem' }}><RefreshCw size={14} className={resynchroMasseEnCours ? 'animate-spin' : ''}/> {resynchroMasseEnCours ? 'Resynchronisation…' : 'Resynchroniser la sélection'}</button>
                                    )}
                                </div>
                            )}
                            {codeActiviteOptions && (selectedIds.size > 0 || selectAllFilters) && (
                                <div style={{ display: 'flex', gap: '0.4rem', alignItems: 'center', borderLeft: '1px solid var(--border-color)', paddingLeft: '0.75rem' }}>
                                    <select
                                        value={codeActiviteMasse}
                                        onChange={(e) => setCodeActiviteMasse(e.target.value)}
                                        style={{ fontSize: '0.75rem', padding: '0.2rem' }}
                                    >
                                        <option value="">Affecter un code activité…</option>
                                        {codeActiviteOptions.map(o => (
                                            <option key={o.value} value={o.value}>{o.label}</option>
                                        ))}
                                    </select>
                                    <button
                                        onClick={doBulkCodeActivite}
                                        disabled={!codeActiviteMasse || affectationEnCours}
                                        className="btn"
                                        style={{ background: 'white', color: 'var(--text-primary)', border: '1px solid var(--border-color)', padding: '0.25rem 0.75rem', fontSize: '0.75rem' }}
                                    >
                                        {affectationEnCours ? 'Affectation…' : 'Affecter'}
                                    </button>
                                </div>
                            )}
                        </>
                    )}
                </div>

                <div style={{ display: 'flex', alignItems: 'center', gap: '1rem', fontSize: '0.875rem' }}>
                    {loading && <Loader2 size={16} className="animate-spin text-primary" />}
                    <span>Total résultats : <strong>{total}</strong></span>
                    {Object.keys(filters).length > 0 && (
                        <button className="btn" onClick={() => setFilters({})} style={{ background: 'transparent', border: 'none', color: 'var(--accent-primary)', textDecoration: 'underline', padding: 0 }}>
                            Effacer filtres
                        </button>
                    )}
                </div>
            </div>

            {codeActiviteOptions && !readonly && nbSansCodeActivitePage > 0 && (
                <div style={{ padding: '0.5rem 1rem', background: '#fffbeb', color: 'var(--status-warning-text-alt, #92400e)', borderBottom: '1px solid var(--status-warning-border, #fde68a)', fontSize: '0.8rem', fontWeight: 600 }}>
                    ⚠ {nbSansCodeActivitePage} ligne(s) sans code activité sur cette page — le relevé de déductions
                    exige une désignation pour chaque ligne. Sélectionnez les lignes puis utilisez l'affectation groupée.
                </div>
            )}

            {selectedIds.size === data.length && data.length > 0 && total > data.length && !selectAllFilters && (
                <div style={{ padding: '0.5rem', background: '#eff6ff', color: '#1d4ed8', textAlign: 'center', borderBottom: '1px solid #bfdbfe', fontSize: '0.875rem' }}>
                    Toutes les <strong>{data.length}</strong> lignes de cette page sont sélectionnées. 
                    <button onClick={() => setSelectAllFilters(true)} style={{ marginLeft: '0.5rem', background: 'transparent', border: 'none', color: '#1d4ed8', fontWeight: 600, textDecoration: 'underline', cursor: 'pointer' }}>
                        Sélectionner les {total} lignes correspondantes au filtre
                    </button>
                </div>
            )}
            {selectAllFilters && (
                <div style={{ padding: '0.5rem', background: '#eff6ff', color: '#1d4ed8', textAlign: 'center', borderBottom: '1px solid #bfdbfe', fontSize: '0.875rem', fontWeight: 600 }}>
                    Toutes les {total} lignes sont sélectionnées.
                    <button onClick={() => { setSelectAllFilters(false); setSelectedIds(new Set()); }} style={{ marginLeft: '0.5rem', background: 'transparent', border: 'none', color: '#1d4ed8', fontWeight: 600, textDecoration: 'underline', cursor: 'pointer' }}>
                        Annuler la sélection
                    </button>
                </div>
            )}

            {/* Grid Container */}
            <div style={{ flexGrow: 1, position: 'relative' }}>
                <ApbsGrid
                    rowData={data}
                    columnDefs={columnDefs}
                    getRowId={(params) => params.data.id}
                    onSelectionChanged={handleSelectionChanged}
                    onRowClicked={(params) => onRowClick && onRowClick(params.data)}
                    onGridReady={onGridReady}
                    height="100%"
                    showColumnSelector={true}
                    showExportButton={true}
                    exportFileName="domain_export.xlsx"
                />
            </div>

            {/* Pagination */}
            <div style={{ padding: '0.5rem 1rem', borderTop: '1px solid var(--border-color)', display: 'flex', justifyContent: 'space-between', alignItems: 'center', background: 'white' }}>
                <span style={{ fontSize: '0.875rem', color: 'var(--text-secondary)' }}>
                    Page {page} sur {totalPages || 1}
                </span>
                <div style={{ display: 'flex', gap: '0.5rem' }}>
                    <button 
                        onClick={() => setPage(p => Math.max(1, p - 1))} 
                        disabled={page === 1}
                        className="btn" style={{ padding: '0.25rem 0.75rem', fontSize: '0.875rem' }}
                    >
                        Précédent
                    </button>
                    <button 
                        onClick={() => setPage(p => Math.min(totalPages, p + 1))} 
                        disabled={page >= totalPages}
                        className="btn" style={{ padding: '0.25rem 0.75rem', fontSize: '0.875rem' }}
                    >
                        Suivant
                    </button>
                </div>
            </div>

            {soldeInitialTarget && (
                <SaisieSoldeInitialModal
                    row={soldeInitialTarget}
                    declarationId={declarationId}
                    onClose={() => setSoldeInitialTarget(null)}
                    onSaved={() => { setSoldeInitialTarget(null); fetchPage(); onActionDone(); }}
                    showToast={showToast}
                />
            )}
        </div>
    );
}
