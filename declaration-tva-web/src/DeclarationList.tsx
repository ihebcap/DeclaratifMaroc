import { useState, useEffect } from 'react';
import { FileText, Plus, ArrowRight, Trash2, X, Loader2 } from 'lucide-react';
import api from './api';

// Le backend sérialise l'enum StatutDeclaration en NOMBRE (System.Text.Json, aucun
// JsonStringEnumConverter configuré côté API) — pas en texte. Même convention que
// DeclarationStepper.tsx (STATUT_LABELS: Record<number, string>). Une comparaison
// dec.statut === 'EnCours' est donc toujours fausse (chaîne vs nombre).
const STATUT_EN_COURS = 0;
const STATUT_CLOTUREE = 1;
const STATUT_LABELS: Record<number, string> = { 0: 'En cours', 1: 'Clôturée', 2: 'Générée', 3: 'Déposée' };

function DeleteConfirmModal({ numero, onClose, onConfirm, loading }: {
    numero: string,
    onClose: () => void,
    onConfirm: () => void,
    loading: boolean,
}) {
    return (
        <div style={{ position: 'fixed', inset: 0, zIndex: 9999, display: 'flex', alignItems: 'center', justifyContent: 'center', backgroundColor: 'rgba(0,0,0,0.5)', animation: 'fade-in 0.2s ease-out' }}>
            <div style={{ background: 'white', borderRadius: '8px', width: '100%', maxWidth: '420px', boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1), 0 10px 10px -5px rgba(0,0,0,0.04)' }}>
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

export function DeclarationList({
    societeId,
    isAdmin,
    onOpenDeclaration,
    onCreateNew,
    showToast,
}: {
    societeId: number,
    isAdmin: boolean,
    onOpenDeclaration: (id: string) => void,
    onCreateNew: () => void,
    showToast?: (message: string, type?: 'success' | 'error' | 'warning') => void,
}) {
    const [declarations, setDeclarations] = useState<any[]>([]);
    const [loading, setLoading] = useState(true);
    const [toDelete, setToDelete] = useState<{ id: string, numero: string } | null>(null);
    const [deleting, setDeleting] = useState(false);

    const fetchDeclarations = async () => {
        try {
            setLoading(true);
            const res = await api.get('/declarations', { params: { societeId } });
            setDeclarations(res.data);
        } catch (e) {
            console.error(e);
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
            setToDelete(null);
            showToast?.('Déclaration supprimée', 'success');
            await fetchDeclarations();
        } catch (e: any) {
            showToast?.(e.response?.data?.Message || e.response?.data?.message || 'Échec de la suppression', 'error');
        } finally {
            setDeleting(false);
        }
    };

    return (
        <div style={{ flex: 1, overflowY: 'auto', width: '100%', padding: '1.5rem', display: 'flex', flexDirection: 'column', gap: '1.25rem', maxWidth: '1000px', margin: '0 auto' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <h2 style={{ margin: 0, fontSize: '1.5rem', fontWeight: 600, color: 'var(--text-primary)' }}>Déclarations TVA</h2>
                <button
                    onClick={onCreateNew}
                    className="btn btn-primary"
                    style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', background: 'var(--accent-primary)', color: 'white', padding: '0.5rem 1rem', border: 'none', borderRadius: '4px', cursor: 'pointer', fontWeight: 500 }}
                >
                    <Plus size={18} />
                    Créer une déclaration
                </button>
            </div>

            {loading ? (
                <div style={{ textAlign: 'center', padding: '3rem', color: 'var(--text-secondary)' }}>Chargement des déclarations...</div>
            ) : (
                <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
                    {declarations.length === 0 ? (
                        <div style={{ background: 'white', padding: '3rem', borderRadius: '8px', border: '1px dashed var(--border-color)', textAlign: 'center', color: 'var(--text-secondary)' }}>
                            <FileText size={48} style={{ opacity: 0.2, margin: '0 auto 1rem auto' }} />
                            <p style={{ margin: 0 }}>Aucune déclaration existante pour cette société.</p>
                        </div>
                    ) : (
                        declarations.map(dec => {
                            const peutSupprimer = isAdmin && dec.statut === STATUT_EN_COURS;
                            return (
                                <div key={dec.id} style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', background: 'white', padding: '1.25rem 1.5rem', borderRadius: '8px', border: '1px solid var(--border-color)', boxShadow: '0 1px 2px rgba(0,0,0,0.05)' }}>
                                    <div>
                                        <h3 style={{ margin: '0 0 0.25rem 0', fontSize: '1.125rem', fontWeight: 600, color: 'var(--text-primary)' }}>{dec.numero}</h3>
                                        <div style={{ display: 'flex', gap: '1rem', fontSize: '0.875rem', color: 'var(--text-secondary)' }}>
                                            <span>Exercice: <strong>{dec.exercice}</strong></span>
                                            <span>Période: <strong>{dec.periode}</strong></span>
                                            <span>Type: <strong>{dec.type}</strong></span>
                                            <span>Lignes: <strong>{dec.nbLignes ?? 0}</strong></span>
                                            <span>Montant TVA: <strong>{Number(dec.montantTva ?? 0).toLocaleString('fr-FR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</strong></span>
                                        </div>
                                    </div>
                                    <div style={{ display: 'flex', alignItems: 'center', gap: '1.5rem' }}>
                                        <span style={{
                                            padding: '0.25rem 0.75rem',
                                            borderRadius: '99px',
                                            fontSize: '0.875rem',
                                            fontWeight: 500,
                                            backgroundColor: dec.statut === STATUT_EN_COURS ? '#e0e7ff' : dec.statut === STATUT_CLOTUREE ? '#fef3c7' : 'var(--status-ok-bg)',
                                            color: dec.statut === STATUT_EN_COURS ? '#4338ca' : dec.statut === STATUT_CLOTUREE ? '#b45309' : 'var(--status-ok-text)'
                                        }}>
                                            {STATUT_LABELS[dec.statut] ?? dec.statut}
                                        </span>
                                        {peutSupprimer && (
                                            <button
                                                onClick={() => setToDelete({ id: dec.id, numero: dec.numero })}
                                                style={{ background: 'transparent', border: '1px solid var(--border-color)', padding: '0.5rem', borderRadius: '4px', cursor: 'pointer', display: 'flex', alignItems: 'center', justifyContent: 'center', color: 'var(--danger-color, #ef4444)' }}
                                                title="Supprimer"
                                            >
                                                <Trash2 size={18} />
                                            </button>
                                        )}
                                        <button
                                            onClick={() => onOpenDeclaration(dec.id)}
                                            style={{ background: 'transparent', border: '1px solid var(--border-color)', padding: '0.5rem', borderRadius: '4px', cursor: 'pointer', display: 'flex', alignItems: 'center', justifyContent: 'center', color: 'var(--text-secondary)' }}
                                            title="Ouvrir"
                                        >
                                            <ArrowRight size={18} />
                                        </button>
                                    </div>
                                </div>
                            );
                        })
                    )}
                </div>
            )}

            {toDelete && (
                <DeleteConfirmModal
                    numero={toDelete.numero}
                    loading={deleting}
                    onClose={() => setToDelete(null)}
                    onConfirm={handleConfirmDelete}
                />
            )}
        </div>
    );
}
