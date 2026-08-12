import { useEffect, useMemo, useState, type CSSProperties } from 'react';
import { FileText, Plus, Loader2, RotateCcw, Trash2, X } from 'lucide-react';
import type { ColDef } from 'ag-grid-community';
import { ApbsGrid } from './grid/ApbsGrid';
import { CustomListFilter } from './grid/CustomListFilter';
import api from './api';

const STATUT_EN_COURS = 0;
const STATUT_CLOTUREE = 1;
const STATUT_LABELS: Record<number, string> = { 0: 'En cours', 1: 'Clôturée', 2: 'Générée', 3: 'Déposée' };

const TYPE_LABELS: Record<number, string> = { 0: 'Mensuelle', 1: 'Trimestrielle' };
const MOIS_LABELS = ['Janvier', 'Février', 'Mars', 'Avril', 'Mai', 'Juin', 'Juillet', 'Août', 'Septembre', 'Octobre', 'Novembre', 'Décembre'];

function libellePeriode(periode: number, type: number, exercice: number): string {
    if (type === 1) return `${periode}er trimestre ${exercice}`.replace('1er', periode === 1 ? '1er' : `${periode}e`);
    const nom = MOIS_LABELS[periode - 1];
    return nom ? `${nom} ${exercice}` : `${periode}/${exercice}`;
}



function Badge({ texte, ton }: { texte: string, ton: 'ok' | 'warn' | 'block' | 'neutre' }) {
    const styles: Record<string, CSSProperties> = {
        ok: { background: 'var(--status-ok-bg)', color: 'var(--status-ok-text)' },
        warn: { background: '#fef3c7', color: 'var(--status-warning-text-alt)' },
        block: { background: 'var(--status-blocking-bg)', color: 'var(--status-blocking-text)' },
        neutre: { background: '#e0e7ff', color: '#4338ca' },
    };
    return (
        <span style={{ ...styles[ton], padding: '2px 8px', borderRadius: '99px', fontSize: '0.7rem', fontWeight: 600, whiteSpace: 'nowrap' }}>
            {texte}
        </span>
    );
}

function StatutBadge({ statut }: { statut: number }) {
    if (statut === STATUT_CLOTUREE) return <Badge texte="Clôturée" ton="warn" />;
    if (statut === 2 || statut === 3) return <Badge texte={STATUT_LABELS[statut]} ton="ok" />;
    return <Badge texte={STATUT_LABELS[statut] ?? String(statut)} ton="neutre" />;
}

const btnStyle: CSSProperties = {
    display: 'inline-flex', alignItems: 'center', gap: '0.35rem', fontSize: '0.78rem', padding: '0.3rem 0.6rem',
};

function DeleteConfirmModal({ numero, onClose, onConfirm, loading }: {
    numero: string,
    onClose: () => void,
    onConfirm: () => void,
    loading: boolean,
}) {
    return (
        <div style={{ position: 'fixed', inset: 0, zIndex: 9999, display: 'flex', alignItems: 'center', justifyContent: 'center', backgroundColor: 'rgba(0,0,0,0.5)' }}>
            <div style={{ background: 'white', borderRadius: '8px', width: '100%', maxWidth: '420px', boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1)' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '1.25rem 1.5rem', borderBottom: '1px solid var(--border-color)' }}>
                    <h3 style={{ margin: 0, fontSize: '1.125rem', fontWeight: 600 }}>Supprimer la déclaration</h3>
                    <button onClick={onClose} style={{ background: 'transparent', border: 'none', cursor: 'pointer', color: 'var(--text-secondary)' }}><X size={20} /></button>
                </div>
                <div style={{ padding: '1.5rem', display: 'flex', flexDirection: 'column', gap: '1rem' }}>
                    <p style={{ margin: 0, fontSize: '0.9375rem', color: 'var(--text-primary)' }}>
                        Confirmer la suppression définitive de <strong>{numero}</strong> ?
                    </p>
                    <p style={{ margin: 0, fontSize: '0.8125rem', color: 'var(--text-secondary)' }}>
                        L'entête et toutes ses lignes seront supprimées. Les règlements/factures sous-jacents redeviennent immédiatement sélectionnables. Action irréversible.
                    </p>
                    <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '1rem', marginTop: '0.5rem' }}>
                        <button type="button" onClick={onClose} disabled={loading} className="btn" style={{ background: 'transparent', border: '1px solid var(--border-color)', padding: '0.5rem 1rem', borderRadius: '4px' }}>
                            Annuler
                        </button>
                        <button type="button" onClick={onConfirm} disabled={loading} className="btn" style={{ background: 'var(--danger-color, #ef4444)', color: 'white', border: 'none', padding: '0.5rem 1.5rem', borderRadius: '4px', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                            {loading && <Loader2 size={16} className="animate-spin" />}
                            Supprimer
                        </button>
                    </div>
                </div>
            </div>
        </div>
    );
}

function ReopenConfirmModal({ numero, onClose, onConfirm, loading }: {
    numero: string,
    onClose: () => void,
    onConfirm: () => void,
    loading: boolean,
}) {
    return (
        <div style={{ position: 'fixed', inset: 0, zIndex: 9999, display: 'flex', alignItems: 'center', justifyContent: 'center', backgroundColor: 'rgba(0,0,0,0.5)' }}>
            <div style={{ background: 'white', borderRadius: '8px', width: '100%', maxWidth: '420px', boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1)' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '1.25rem 1.5rem', borderBottom: '1px solid var(--border-color)' }}>
                    <h3 style={{ margin: 0, fontSize: '1.125rem', fontWeight: 600 }}>Réouvrir la déclaration</h3>
                    <button onClick={onClose} style={{ background: 'transparent', border: 'none', cursor: 'pointer', color: 'var(--text-secondary)' }}><X size={20} /></button>
                </div>
                <div style={{ padding: '1.5rem', display: 'flex', flexDirection: 'column', gap: '1rem' }}>
                    <p style={{ margin: 0, fontSize: '0.9375rem', color: 'var(--text-primary)' }}>
                        Confirmer la réouverture de <strong>{numero}</strong> ?
                    </p>
                    <p style={{ margin: 0, fontSize: '0.8125rem', color: 'var(--text-secondary)' }}>
                        La déclaration repassera à l'état En cours. Vous pourrez modifier la sélection des règlements et régénérer le fichier de dépôt.
                    </p>
                    <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '1rem', marginTop: '0.5rem' }}>
                        <button type="button" onClick={onClose} disabled={loading} className="btn" style={{ background: 'transparent', border: '1px solid var(--border-color)', padding: '0.5rem 1rem', borderRadius: '4px' }}>
                            Annuler
                        </button>
                        <button type="button" onClick={onConfirm} disabled={loading} className="btn btn-primary" style={{ background: 'var(--accent-primary)', color: 'white', border: 'none', padding: '0.5rem 1.5rem', borderRadius: '4px', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                            {loading && <Loader2 size={16} className="animate-spin" />}
                            Réouvrir
                        </button>
                    </div>
                </div>
            </div>
        </div>
    );
}

export function DeclarationList({ societeId, onSelectDeclaration, onCreateNew, showToast }: {
    societeId: number,
    onSelectDeclaration: (id: string) => void,
    onCreateNew: () => void,
    showToast: (msg: string, type?: 'success' | 'error' | 'warning') => void
}) {
    const [declarations, setDeclarations] = useState<any[]>([]);
    const [loading, setLoading] = useState(false);
    const [toDelete, setToDelete] = useState<{ id: string, numero: string } | null>(null);
    const [deleting, setDeleting] = useState(false);
    const [toReopen, setToReopen] = useState<{ id: string, numero: string } | null>(null);
    const [reopening, setReopening] = useState(false);

    const fetchDeclarations = async () => {
        setLoading(true);
        try {
            const res = await api.get('/declarations', { params: { societeId } });
            const list = res.data.items || res.data || [];
            setDeclarations(list);
        } catch (e) {
            console.error(e);
            showToast('Erreur lors du chargement des déclarations', 'error');
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        fetchDeclarations();
    }, [societeId]);

    const handleConfirmDelete = async () => {
        if (!toDelete) return;
        setDeleting(true);
        try {
            await api.delete(`/declarations/${toDelete.id}`);
            showToast(`Déclaration ${toDelete.numero} supprimée.`, 'success');
            setToDelete(null);
            fetchDeclarations();
        } catch (e: any) {
            console.error(e);
            showToast(e?.response?.data?.Message || 'Échec de la suppression.', 'error');
        } finally {
            setDeleting(false);
        }
    };

    const handleConfirmReopen = async () => {
        if (!toReopen) return;
        setReopening(true);
        try {
            await api.post(`/declarations/${toReopen.id}/reouvrir`);
            showToast(`Déclaration ${toReopen.numero} réouverte.`, 'success');
            setToReopen(null);
            fetchDeclarations();
        } catch (e: any) {
            console.error(e);
            showToast(e?.response?.data?.Message || 'Échec de la réouverture.', 'error');
        } finally {
            setReopening(false);
        }
    };

    const periodesOptions = useMemo(() => {
        const set = new Set<string>();
        declarations.forEach(d => {
          const txt = libellePeriode(d.periode, d.type, d.exercice);
          set.add(txt);
        });
        return Array.from(set).map(v => ({ label: v, value: v }));
    }, [declarations]);

    const typesOptions = useMemo(() => {
        const set = new Set<string>();
        declarations.forEach(d => {
          const txt = TYPE_LABELS[d.type] ?? String(d.type);
          set.add(txt);
        });
        return Array.from(set).map(v => ({ label: v, value: v }));
    }, [declarations]);

    const statutsOptions = useMemo(() => {
        const set = new Set<string>();
        declarations.forEach(d => {
          const txt = STATUT_LABELS[d.statut] ?? String(d.statut);
          set.add(txt);
        });
        return Array.from(set).map(v => ({ label: v, value: v }));
    }, [declarations]);

    const columnDefs: ColDef[] = useMemo(() => {
        return [
            {
                field: 'numero',
                headerName: 'N° déclaration',
                width: 150,
                cellRenderer: (p: any) => {
                    const dec = p.data;
                    if (!dec) return null;
                    return (
                        <button
                            onClick={() => onSelectDeclaration(dec.id)}
                            style={{ background: 'transparent', border: 'none', color: 'var(--accent-primary)', fontWeight: 600, textDecoration: 'underline', cursor: 'pointer', padding: 0, fontSize: '0.8125rem' }}
                        >
                            {dec.numero}
                        </button>
                    );
                }
            },
            {
                field: 'periodeText',
                headerName: 'Période',
                width: 170,
                filter: CustomListFilter,
                filterParams: { options: periodesOptions },
                valueGetter: (p) => p.data ? libellePeriode(p.data.periode, p.data.type, p.data.exercice) : '',
            },
            {
                field: 'typeText',
                headerName: 'Déclaration',
                filter: CustomListFilter,
                filterParams: { options: typesOptions },
                valueGetter: (p) => p.data ? (TYPE_LABELS[p.data.type] ?? String(p.data.type)) : '',
            },
            {
                field: 'lignesCount',
                headerName: 'Nombre de factures',
                width: 150,
                type: 'numericColumn',
                valueGetter: (p) => p.data?.lignesCount ?? p.data?.nbFactures ?? 0,
            },
            {
                field: 'statutText',
                headerName: 'Statut',
                width: 140,
                filter: CustomListFilter,
                filterParams: { options: statutsOptions },
                cellRenderer: (p: any) => p.data ? <StatutBadge statut={p.data.statut} /> : null,
            },
            {
                headerName: 'Actions',
                width: 150,
                pinned: 'right',
                suppressHeaderMenuButton: true,
                cellRenderer: (p: any) => {
                    const dec = p.data;
                    if (!dec) return null;
                    return (
                        <div style={{ display: 'flex', gap: '0.35rem', alignItems: 'center', height: '100%' }} onClick={(e) => e.stopPropagation()}>
                            <button
                                className="btn btn-primary"
                                style={btnStyle}
                                onClick={() => onSelectDeclaration(dec.id)}
                                title="Ouvrir"
                            >
                                Ouvrir
                            </button>
                            {dec.statut === STATUT_EN_COURS && (
                                <button
                                    className="btn"
                                    style={{ ...btnStyle, color: 'var(--danger-color, #ef4444)' }}
                                    onClick={() => setToDelete({ id: dec.id, numero: dec.numero })}
                                    title="Supprimer"
                                >
                                    <Trash2 size={13} />
                                </button>
                            )}
                            {dec.statut === STATUT_CLOTUREE && (
                                <button
                                    className="btn"
                                    style={{ ...btnStyle, color: 'var(--accent-primary)' }}
                                    onClick={() => setToReopen({ id: dec.id, numero: dec.numero })}
                                    title="Réouvrir"
                                >
                                    <RotateCcw size={13} />
                                </button>
                            )}
                        </div>
                    );
                }
            }
        ];
    }, [onSelectDeclaration, periodesOptions, typesOptions, statutsOptions]);

    return (
        <div style={{ flex: 1, display: 'flex', flexDirection: 'column', height: '100%', overflow: 'hidden' }}>
            <div style={{ padding: '0.75rem 1rem', borderBottom: '1px solid var(--border-color)', background: 'white', display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: '0.75rem' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '0.6rem' }}>
                    <FileText size={20} style={{ color: 'var(--accent-primary)' }} />
                    <div>
                        <h2 style={{ margin: 0, fontSize: '1.05rem', fontWeight: 600 }}>Déclarations TVA</h2>
                        <div style={{ fontSize: '0.72rem', color: 'var(--text-secondary)' }}>Déclaration TVA Maroc — modèle SIMPL-TVA</div>
                    </div>
                </div>
                <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                    <button className="btn btn-primary" style={{ ...btnStyle, padding: '0.3rem 0.75rem' }} onClick={onCreateNew}>
                        <Plus size={14} /> Créer une déclaration
                    </button>
                </div>
            </div>

            <div style={{ flexGrow: 1, minHeight: 0, position: 'relative', padding: '0.4rem 1rem' }}>
                <ApbsGrid
                    rowData={declarations}
                    columnDefs={columnDefs}
                    height="100%"
                    showColumnSelector={true}
                    showExportButton={true}
                    exportFileName="declarations_tva.xlsx"
                    toolbarLeft={
                        <>
                            {loading && <Loader2 size={15} className="animate-spin" style={{ color: 'var(--accent-primary)' }} />}
                            <span>Déclarations : <strong>{declarations.length}</strong></span>
                        </>
                    }
                />
            </div>

            {toDelete && (
                <DeleteConfirmModal
                    numero={toDelete.numero}
                    loading={deleting}
                    onClose={() => setToDelete(null)}
                    onConfirm={handleConfirmDelete}
                />
            )}

            {toReopen && (
                <ReopenConfirmModal
                    numero={toReopen.numero}
                    loading={reopening}
                    onClose={() => setToReopen(null)}
                    onConfirm={handleConfirmReopen}
                />
            )}
        </div>
    );
}
