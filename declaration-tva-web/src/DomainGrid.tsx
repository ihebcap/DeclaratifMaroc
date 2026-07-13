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
    columns
}: { 
    declarationId: string, 
    domaine?: DomaineTVA, 
    onActionDone: () => void,
    showToast: (m: string, t?: any) => void,
    initialFilters?: Record<string, any>,
    readonly?: boolean,
    onRowClick?: (row: any) => void,
    columns?: { key: string, label: string, filterType: 'list' | 'text' | 'number' | 'date' }[]
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

    type ColumnDef = { key: string, label: string, filterType: 'list' | 'text' | 'number' | 'date' };
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
        { key: 'motif', label: 'Motif Écartement', filterType: 'text' }
    ];
    const gridColumns: ColumnDef[] = columns || defaultColumns;
    const { visibleColumns, visibleKeys, toggle: toggleColumn, reset: resetColumns } = useColumnPrefs('grf.cols.domain', gridColumns);

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

    // Virtualization setup
    const rowVirtualizer = useVirtualizer({
        count: data.length,
        getScrollElement: () => parentRef.current,
        estimateSize: () => 40,
        overscan: 5,
    });

    const renderCell = (key: string, val: any) => {
        if (['ht', 'tva', 'ttc', 'montantHT', 'montantTTC'].includes(key)) return formatMoney(val);
        if (key === 'taux' || key === 'tauxTVA') return `${val}%`;
        if (key === 'etat' || key === 'statutLigne') {
            let label = val;
            if (typeof val === 'number') {
                const etatMap = ['Proposée', 'Intégrée', 'Exclue', 'Reportée', 'Écartée'];
                label = etatMap[val] || val;
            }
            const colors: any = {
                'Proposée': { bg: '#f3f4f6', text: '#4b5563' },
                'Intégrée': { bg: '#dcfce7', text: '#15803d' },
                'Exclue': { bg: '#fee2e2', text: '#b91c1c' },
                'Reportée': { bg: '#fef3c7', text: '#b45309' },
                'Écartée': { bg: '#e5e7eb', text: '#374151', border: '1px solid #9ca3af' },
            };
            const c = colors[label] || colors['Proposée'];
            return <span style={{ background: c.bg, color: c.text, border: c.border || 'none', padding: '2px 8px', borderRadius: '99px', fontSize: '0.75rem', fontWeight: 600 }}>{label}</span>;
        }
        return val;
    };

    const totalPages = Math.ceil(total / size);

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
                                    <button onClick={() => doBulkAction('Intégrée')} className="btn" style={{ background: '#dcfce7', color: '#15803d', border: '1px solid #bbf7d0', padding: '0.25rem 0.75rem', display: 'flex', alignItems: 'center', gap: '0.25rem', fontSize: '0.75rem' }}><CheckSquare size={14}/> Intégrer</button>
                                    <button onClick={() => doBulkAction('Exclue')} className="btn" style={{ background: '#fee2e2', color: '#b91c1c', border: '1px solid #fecaca', padding: '0.25rem 0.75rem', display: 'flex', alignItems: 'center', gap: '0.25rem', fontSize: '0.75rem' }}><XSquare size={14}/> Exclure</button>
                                    <button onClick={() => doBulkAction('Reportée')} className="btn" style={{ background: '#fef3c7', color: '#b45309', border: '1px solid #fde68a', padding: '0.25rem 0.75rem', display: 'flex', alignItems: 'center', gap: '0.25rem', fontSize: '0.75rem' }}><Clock size={14}/> Reporter</button>
                                    <button onClick={() => doBulkAction('Proposée')} className="btn" style={{ background: 'white', color: 'var(--text-secondary)', border: '1px solid var(--border-color)', padding: '0.25rem 0.75rem', display: 'flex', alignItems: 'center', gap: '0.25rem', fontSize: '0.75rem' }}>Réinitialiser</button>
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
            <div ref={parentRef} style={{ flexGrow: 1, overflow: 'auto', position: 'relative' }}>
                <table style={{ width: '100%', minWidth: '1000px', borderCollapse: 'collapse', fontSize: '0.8125rem' }}>
                    <thead style={{ position: 'sticky', top: 0, background: 'var(--bg-secondary)', zIndex: 10, boxShadow: '0 1px 2px rgba(0,0,0,0.05)' }}>
                        <tr>
                            {!readonly && (
                                <th style={{ padding: '0.5rem 1rem', width: '40px', borderBottom: '1px solid var(--border-color)', borderRight: '1px solid var(--border-color)' }}>
                                    <input type="checkbox" checked={selectAllFilters || (selectedIds.size > 0 && selectedIds.size === data.length)} onChange={handleToggleAll} />
                                </th>
                            )}
                            {visibleColumns.map((col: ColumnDef) => (
                                <th key={col.key} style={{ padding: '0.5rem 1rem', borderBottom: '1px solid var(--border-color)', borderRight: '1px solid var(--border-color)', textAlign: ['montantHT', 'montantTVA', 'montantTTC', 'tauxTVA'].includes(col.key) ? 'right' : 'left', cursor: 'pointer', userSelect: 'none' }} onClick={() => handleSort(col.key)}>
                                    <div style={{ display: 'flex', alignItems: 'center', justifyContent: ['montantHT', 'montantTVA', 'montantTTC', 'tauxTVA'].includes(col.key) ? 'flex-end' : 'flex-start', gap: '0.25rem' }}>
                                        {col.label}
                                        {sortConfig?.key === col.key && (
                                            <span style={{ fontSize: '0.7rem', color: 'var(--text-secondary)' }}>{sortConfig?.desc ? '▼' : '▲'}</span>
                                        )}
                                        <ExcelFilter 
                                            filterType={col.filterType} 
                                            options={(distincts[col.key] || []).map(v => ({label: v, value: v}))} 
                                            selectedValues={Array.isArray(filters[col.key]) ? filters[col.key] as string[] : []}
                                            textValue={typeof filters[col.key] === 'string' ? filters[col.key] as string : ''}
                                            onChange={(val) => handleFilterChange(col.key, val)} 
                                        />
                                    </div>
                                </th>
                            ))}
                        </tr>
                    </thead>
                    <tbody style={{ height: `${rowVirtualizer.getTotalSize()}px`, position: 'relative' }}>
                        {rowVirtualizer.getVirtualItems().map((virtualRow) => {
                            const row = data[virtualRow.index];
                            if (!row) return null;
                            const isSelected = selectedIds.has(row.id);

                            return (
                                <tr 
                                    key={row.id} 
                                    onClick={() => onRowClick && onRowClick(row)}
                                    style={{ 
                                        position: 'absolute', top: 0, left: 0, width: '100%', 
                                        transform: `translateY(${virtualRow.start}px)`,
                                        height: `${virtualRow.size}px`,
                                        borderBottom: '1px solid var(--border-color)',
                                        backgroundColor: isSelected ? 'var(--bg-secondary)' : 'white',
                                        cursor: onRowClick ? 'pointer' : 'default'
                                    }}
                                >
                                    {!readonly && (
                                        <td style={{ padding: '0.5rem 1rem', width: '40px', borderRight: '1px solid var(--border-color)' }} onClick={(e) => e.stopPropagation()}>
                                            <input type="checkbox" checked={isSelected || selectAllFilters} onChange={() => handleToggleOne(row.id)} disabled={selectAllFilters} />
                                        </td>
                                    )}
                                    {visibleColumns.map((col: ColumnDef) => (
                                        <td key={col.key} style={{ padding: '0.5rem 1rem', borderRight: '1px solid var(--border-color)', textAlign: ['montantHT', 'montantTVA', 'montantTTC', 'tauxTVA'].includes(col.key) ? 'right' : 'left', whiteSpace: 'nowrap' }}>
                                            {renderCell(col.key, row[col.key])}
                                        </td>
                                    ))}
                                </tr>
                            );
                        })}
                    </tbody>
                </table>
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
