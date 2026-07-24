import React, { useState, useEffect, useRef, useCallback } from 'react';
import { ExcelFilter } from './ExcelFilter';
import { ColumnSelector } from './ColumnSelector';
import { useColumnPrefs } from './useColumnPrefs';
import { formatMoney } from './utils';
import api from './api';
import { useVirtualizer } from '@tanstack/react-virtual';
import { Loader2, CheckSquare, XSquare, Clock } from 'lucide-react';
import type { DomaineTVA } from './DeclarationStepper';

export function DomainGrid({ 
    declarationId, 
    domaine, 
    onActionDone, 
    showToast,
    initialFilters,
    readonly = false,
    onRowClick,
    columns,
    colsStorageKey,
    codeActiviteOptions
}: {
    declarationId: string,
    domaine?: DomaineTVA,
    onActionDone: () => void,
    showToast: (m: string, t?: any) => void,
    initialFilters?: Record<string, any>,
    readonly?: boolean,
    onRowClick?: (row: any) => void,
    columns?: { key: string, label: string, filterType: 'list' | 'text' | 'number' | 'date', width?: string, derived?: boolean, editable?: boolean }[],
    // TASK-142 : clé de persistance des colonnes visibles. Un jeu de colonnes non-standard (ex. drill
    // incohérence avec Montant TVA + Écart) DOIT utiliser sa propre clé, sinon il hérite des préférences
    // enregistrées pour la grille par défaut (qui ne connaît pas ces colonnes) → elles seraient masquées.
    colsStorageKey?: string,
    // TASK-161 : options de la liste déroulante pour toute colonne `editable` de clé 'codeActivite'
    // (référentiel P_DECTVAACTIVITE). Non fourni = colonne affichée en lecture seule même si
    // `editable` est posé (garde-fou : jamais un select vide silencieux).
    codeActiviteOptions?: { value: string, label: string }[]
}) {
    const [data, setData] = useState<any[]>([]);
    const [total, setTotal] = useState(0);
    const [distincts, setDistincts] = useState<Record<string, string[]>>({});
    const [loading, setLoading] = useState(false);
    
    // Server state
    const [page, setPage] = useState(1);
    const [filters, setFilters] = useState<Record<string, string | string[]>>({});
    const [sortConfig, setSortConfig] = useState<{ key: string, desc: boolean } | null>(null);
    
    // Selection state
    const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
    const [selectAllFilters, setSelectAllFilters] = useState(false);

    const size = 100; // items per page
    const parentRef = useRef<HTMLDivElement>(null);

    // TASK-142 : `derived` = colonne purement calculée en rendu à partir de champs déjà chargés
    // (ex. « Écart » = montantHT+montantTVA−montantTTC). Aucune clé correspondante côté API → on ne
    // doit ni la trier ni la filtrer (sinon on enverrait une clé inconnue au back, cf. garde-fou).
    type ColumnDef = { key: string, label: string, filterType: 'list' | 'text' | 'number' | 'date', width?: string, derived?: boolean, editable?: boolean };
    // TASK-110 : largeur bornée par colonne (pattern AffectationsDrill.tsx GridCell/colStyle) —
    // évite qu'un motif d'écartement long étire toute la ligne ; défaut 200px sinon spécifié.
    const DEFAULT_COL_WIDTH = '200px';
    const colMaxWidth = (col: ColumnDef) => col.width || DEFAULT_COL_WIDTH;
    // TASK-138 : source UNIQUE de largeur de colonne, partagée par l'en-tête ET par chaque ligne
    // (pattern colStyle de AffectationsDrill.tsx). Toutes les colonnes de DomainGrid ont une largeur
    // fixe (colMaxWidth défaut 200px) → flex non extensible « 0 0 width » : l'en-tête et le corps ne
    // peuvent plus diverger, contrairement au calcul de layout d'un <table> + <tr position:absolute>
    // (cause des deux échecs TASK-113 v1/v2).
    const colStyle = (col: ColumnDef): React.CSSProperties => ({
        flex: `0 0 ${colMaxWidth(col)}`,
        width: colMaxWidth(col),
    });
    const CHECKBOX_W = '40px';
    const isNumericCol = (key: string) => ['montantHT', 'montantTVA', 'montantTTC', 'tauxTVA', 'ecart'].includes(key);
    // TASK-142 : écart d'équilibre par ligne, dérivé en pur affichage (aucun appel/recalcul serveur).
    const ligneEcart = (row: any) => Number(row?.montantHT || 0) + Number(row?.montantTVA || 0) - Number(row?.montantTTC || 0);
    const defaultColumns: ColumnDef[] = [
        { key: 'factureNumero', label: 'N° Facture', filterType: 'text' },
        // TASK-034 : colonne « Désignation » retirée — aucune source dans LigneCandidate
        // (décision PO par défaut : ne rien inventer, pas de colonne vide muette).
        { key: 'tiers', label: 'Tiers', filterType: 'text' },
        { key: 'origine', label: 'Origine', filterType: 'list' }, // TASK-038 : origine EC_Type (Sage/OM · FGR · Solde initial)
        { key: 'montantHT', label: 'Montant HT', filterType: 'number' },
        { key: 'tauxTVA', label: 'Taux TVA', filterType: 'list' },
        { key: 'montantTTC', label: 'Montant TTC', filterType: 'number' },
        { key: 'source', label: 'Source', filterType: 'list' },
        { key: 'statutLigne', label: 'Statut', filterType: 'list' },
        { key: 'motif', label: 'Motif Écartement', filterType: 'text', width: '280px' }
    ];
    const gridColumns: ColumnDef[] = columns || defaultColumns;
    const { visibleColumns, visibleKeys, toggle: toggleColumn, reset: resetColumns } = useColumnPrefs(colsStorageKey || 'grf.cols.domain', gridColumns);

    const fetchPage = useCallback(async () => {
        setLoading(true);
        try {
            // TASK-067B : le back (BuildLigneFilterWhere) applique désormais numeroRapprochement/
            // source/tauxTVA/origine en MULTI-SÉLECTION RÉELLE (IN), fin de la troncature à la 1re
            // valeur cochée (l'ancien `v[0]` ignorait silencieusement les autres cases — filtre
            // menteur, cf. TASK-063 pour le même bug côté Rapprochement).
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
    }, [declarationId, domaine, page, filters, sortConfig]);

    useEffect(() => {
        // Reset page to 1 when filters or sort change, but not on initial mount
        setPage(1);
    }, [filters, sortConfig, domaine]);

    useEffect(() => {
        fetchPage();
        setSelectedIds(new Set()); // Clear selection on data change
        setSelectAllFilters(false);
    }, [fetchPage]);

    useEffect(() => {
        if (initialFilters) setFilters(initialFilters);
    }, [initialFilters, domaine]);

    const handleFilterChange = (key: string, val: any) => {
        setFilters(prev => {
            const next = { ...prev };
            if (val === '' || (Array.isArray(val) && val.length === 0)) {
                delete next[key];
            } else {
                next[key] = val;
            }
            return next;
        });
    };

    const handleSort = (key: string) => {
        setSortConfig(prev => {
            if (prev?.key === key) {
                if (prev.desc) return null;
                return { key, desc: true };
            }
            return { key, desc: false };
        });
    };

    const handleToggleAll = (e: React.ChangeEvent<HTMLInputElement>) => {
        if (e.target.checked) {
            setSelectedIds(new Set(data.map(d => d.id)));
        } else {
            setSelectedIds(new Set());
            setSelectAllFilters(false);
        }
    };

    const handleToggleOne = (id: string) => {
        setSelectedIds(prev => {
            const next = new Set(prev);
            if (next.has(id)) next.delete(id);
            else next.add(id);
            return next;
        });
    };

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

    // TASK-173 : affectation en masse du code activité — même sélection (IDs ou domaine+filtre)
    // que doBulkAction ci-dessus, réutilise le mécanisme :bulk existant (TASK-012) côté back.
    // N'est proposée que si `codeActiviteOptions` est fourni par l'écran appelant (drill « Codes
    // activité » de VerifierIntegrerPanel) — jamais sur les 5 autres écrans partageant DomainGrid.
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

    // TASK-161 : surcharge manuelle du code activité d'une ligne (colonne `editable`, jamais en
    // lecture seule) — PATCH ciblé par ligne (Id = DM_LGTVA.Id), jamais par EC_Id (une même
    // facture peut porter deux lignes de taux différents avec deux activités différentes, cas
    // confirmé PO). Mise à jour optimiste de la ligne locale après succès, pas de refetch complet.
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

    // Virtualization setup
    const rowVirtualizer = useVirtualizer({
        count: data.length,
        getScrollElement: () => parentRef.current,
        estimateSize: () => 40,
        overscan: 5,
    });

    const renderCell = (key: string, val: any) => {
        // TASK-142 : montantTVA (2ᵉ opérande de l'égalité isolée par le drill) + ecart (dérivé) manquaient
        // à la liste des colonnes monétaires → ils s'affichaient en brut. Ajoutés ici.
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

    const totalPages = Math.ceil(total / size);

    // TASK-138 : largeur totale de la grille = somme des largeurs fixes (+ colonne case à cocher),
    // bornée à 1000px minimum (reprise du minWidth de l'ancien <table>). Sert de minWidth au wrapper
    // interne afin que l'en-tête et les lignes virtualisées (width:100% du wrapper) partagent
    // exactement la même largeur — condition de l'alignement strict.
    const gridMinWidth = Math.max(
        1000,
        (readonly ? 0 : parseInt(CHECKBOX_W, 10)) +
            visibleColumns.reduce((sum: number, col: ColumnDef) => sum + parseInt(colMaxWidth(col), 10), 0)
    );

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
                                    <button onClick={() => doBulkAction('Intégrée')} className="btn" style={{ background: 'var(--status-ok-bg)', color: 'var(--status-ok-text)', border: '1px solid #bbf7d0', padding: '0.25rem 0.75rem', display: 'flex', alignItems: 'center', gap: '0.25rem', fontSize: '0.75rem' }}><CheckSquare size={14}/> Intégrer</button>
                                    <button onClick={() => doBulkAction('Exclue')} className="btn" style={{ background: 'var(--status-blocking-bg)', color: 'var(--status-blocking-text)', border: '1px solid #fecaca', padding: '0.25rem 0.75rem', display: 'flex', alignItems: 'center', gap: '0.25rem', fontSize: '0.75rem' }}><XSquare size={14}/> Exclure</button>
                                    <button onClick={() => doBulkAction('Reportée')} className="btn" style={{ background: '#fef3c7', color: 'var(--status-warning-text-alt)', border: '1px solid var(--status-warning-border)', padding: '0.25rem 0.75rem', display: 'flex', alignItems: 'center', gap: '0.25rem', fontSize: '0.75rem' }}><Clock size={14}/> Reporter</button>
                                    <button onClick={() => doBulkAction('Proposée')} className="btn" style={{ background: 'white', color: 'var(--text-secondary)', border: '1px solid var(--border-color)', padding: '0.25rem 0.75rem', display: 'flex', alignItems: 'center', gap: '0.25rem', fontSize: '0.75rem' }}>Réinitialiser</button>
                                </div>
                            )}
                            {/* TASK-173 : affectation en masse du code activité — visible uniquement quand
                                l'écran appelant fournit codeActiviteOptions (drill « Codes activité »). */}
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
                    <ColumnSelector columns={gridColumns} visibleKeys={visibleKeys} onToggle={toggleColumn} onReset={resetColumns} />
                </div>
            </div>

            {/* Select All Banner */}
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
            {/* TASK-138 : rendu flexbox <div> (pattern AffectationsDrill.tsx) en remplacement du
                <table> + <colgroup> + <tr position:absolute> virtualisés — les deux itérations
                TASK-113 (v1 tableLayout:fixed+colgroup, v2 + width explicite) n'ont pas corrigé le
                désalignement en test réel. Ici l'en-tête et les lignes utilisent la MÊME fonction de
                largeur (colStyle), et le wrapper interne impose une largeur commune (gridMinWidth) :
                l'alignement est garanti par construction, sans dépendre du calcul de layout d'un
                tableau. Virtualisation @tanstack/react-virtual conservée (lignes = <div>
                position:absolute). Rôles ARIA posés pour compenser l'abandon du <table> sémantique. */}
            <div ref={parentRef} style={{ flexGrow: 1, overflow: 'auto', position: 'relative' }}>
                <div role="table" style={{ minWidth: `${gridMinWidth}px`, fontSize: '0.8125rem' }}>
                    {/* En-tête collant */}
                    <div role="row" style={{ display: 'flex', position: 'sticky', top: 0, background: 'var(--bg-secondary)', zIndex: 10, boxShadow: '0 1px 2px rgba(0,0,0,0.05)', borderBottom: '1px solid var(--border-color)' }}>
                        {!readonly && (
                            <div role="columnheader" style={{ flex: `0 0 ${CHECKBOX_W}`, width: CHECKBOX_W, padding: '0.5rem 1rem', borderRight: '1px solid var(--border-color)', display: 'flex', alignItems: 'center' }}>
                                <input type="checkbox" checked={selectAllFilters || (selectedIds.size > 0 && selectedIds.size === data.length)} onChange={handleToggleAll} />
                            </div>
                        )}
                        {visibleColumns.map((col: ColumnDef) => (
                            <div
                                key={col.key}
                                role="columnheader"
                                onClick={col.derived ? undefined : () => handleSort(col.key)}
                                style={{ ...colStyle(col), padding: '0.5rem 1rem', borderRight: '1px solid var(--border-color)', cursor: col.derived ? 'default' : 'pointer', userSelect: 'none', display: 'flex', alignItems: 'center', justifyContent: isNumericCol(col.key) ? 'flex-end' : 'flex-start', gap: '0.25rem' }}
                            >
                                <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', fontWeight: 600 }}>{col.label}</span>
                                {/* TASK-142 : colonne dérivée (Écart) non triable/non filtrable — aucune clé
                                    envoyée au back (garde-fou : ne pas transmettre de clé inexistante côté API). */}
                                {!col.derived && sortConfig?.key === col.key && (
                                    <span style={{ fontSize: '0.7rem', color: 'var(--text-secondary)' }}>{sortConfig?.desc ? '▼' : '▲'}</span>
                                )}
                                {!col.derived && (
                                    <span onClick={(e) => e.stopPropagation()} style={{ display: 'flex', alignItems: 'center' }}>
                                        <ExcelFilter
                                            filterType={col.filterType}
                                            options={(distincts[col.key] || []).map(v => ({label: v, value: v}))}
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
                    <div role="rowgroup" style={{ height: `${rowVirtualizer.getTotalSize()}px`, position: 'relative' }}>
                        {rowVirtualizer.getVirtualItems().map((virtualRow) => {
                            const row = data[virtualRow.index];
                            if (!row) return null;
                            const isSelected = selectedIds.has(row.id);

                            return (
                                <div
                                    key={row.id}
                                    role="row"
                                    onClick={() => onRowClick && onRowClick(row)}
                                    style={{
                                        display: 'flex',
                                        position: 'absolute', top: 0, left: 0, width: '100%',
                                        transform: `translateY(${virtualRow.start}px)`,
                                        height: `${virtualRow.size}px`,
                                        borderBottom: '1px solid var(--border-color)',
                                        backgroundColor: isSelected ? 'var(--bg-secondary)' : 'white',
                                        cursor: onRowClick ? 'pointer' : 'default'
                                    }}
                                >
                                    {!readonly && (
                                        <div role="cell" style={{ flex: `0 0 ${CHECKBOX_W}`, width: CHECKBOX_W, padding: '0.5rem 1rem', borderRight: '1px solid var(--border-color)', display: 'flex', alignItems: 'center' }} onClick={(e) => e.stopPropagation()}>
                                            <input type="checkbox" checked={isSelected || selectAllFilters} onChange={() => handleToggleOne(row.id)} disabled={selectAllFilters} />
                                        </div>
                                    )}
                                    {visibleColumns.map((col: ColumnDef) => {
                                        // TASK-142 : valeur de la colonne « Écart » calculée en rendu ; toutes les
                                        // autres colonnes lisent la donnée API telle quelle (row[col.key]).
                                        const cellVal = col.key === 'ecart' ? ligneEcart(row) : row[col.key];
                                        // Mise en évidence de la ligne fautive (HT+TVA ≠ TTC), tolérance 0,005
                                        // pour absorber les arrondis d'affichage.
                                        const ecartAnormal = col.key === 'ecart' && Math.abs(cellVal) > 0.005;
                                        // TASK-161 : cellule éditable UNIQUEMENT si la colonne le demande
                                        // explicitement (`editable`), la grille n'est pas en lecture seule, et
                                        // des options ont été fournies — jamais un select vide silencieux.
                                        const editableCodeActivite = col.key === 'codeActivite' && col.editable && !readonly && codeActiviteOptions;
                                        return (
                                        <div
                                            key={col.key}
                                            role="cell"
                                            style={{ ...colStyle(col), padding: '0.5rem 1rem', borderRight: '1px solid var(--border-color)', display: 'flex', alignItems: 'center', justifyContent: isNumericCol(col.key) ? 'flex-end' : 'flex-start', overflow: 'hidden', background: ecartAnormal ? 'var(--status-blocking-bg)' : undefined, color: ecartAnormal ? 'var(--status-blocking-text)' : undefined, fontWeight: ecartAnormal ? 700 : undefined }}
                                            title={typeof row[col.key] === 'string' ? row[col.key] : undefined}
                                            onClick={editableCodeActivite ? (e) => e.stopPropagation() : undefined}
                                        >
                                            {editableCodeActivite ? (
                                                <select
                                                    value={cellVal || ''}
                                                    onChange={(e) => handleCodeActiviteChange(row, e.target.value)}
                                                    style={{ width: '100%', fontSize: '0.8125rem', padding: '0.15rem' }}
                                                >
                                                    <option value="">(sans activité)</option>
                                                    {codeActiviteOptions!.map(o => (
                                                        <option key={o.value} value={o.value}>{o.label}</option>
                                                    ))}
                                                </select>
                                            ) : (
                                                <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                                                    {renderCell(col.key, cellVal)}
                                                </span>
                                            )}
                                        </div>
                                        );
                                    })}
                                </div>
                            );
                        })}
                    </div>
                </div>
                {data.length === 0 && !loading && (
                    <div style={{ padding: '3rem', textAlign: 'center', color: 'var(--text-secondary)' }}>
                        Aucune ligne trouvée.
                    </div>
                )}
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
        </div>
    );
}
