import { useState, useEffect } from 'react';
import { FileText, Plus, ArrowRight } from 'lucide-react';
import api from './api';

export function DeclarationList({ 
    societeId, 
    onOpenDeclaration, 
    onCreateNew 
}: { 
    societeId: string, 
    onOpenDeclaration: (id: string) => void,
    onCreateNew: () => void 
}) {
    const [declarations, setDeclarations] = useState<any[]>([]);
    const [loading, setLoading] = useState(true);

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

    return (
        <div style={{ padding: '2rem', display: 'flex', flexDirection: 'column', gap: '1.5rem', maxWidth: '1000px', margin: '0 auto' }}>
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
                        declarations.map(dec => (
                            <div key={dec.id} style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', background: 'white', padding: '1.25rem 1.5rem', borderRadius: '8px', border: '1px solid var(--border-color)', boxShadow: '0 1px 2px rgba(0,0,0,0.05)' }}>
                                <div>
                                    <h3 style={{ margin: '0 0 0.25rem 0', fontSize: '1.125rem', fontWeight: 600, color: 'var(--text-primary)' }}>{dec.numero}</h3>
                                    <div style={{ display: 'flex', gap: '1rem', fontSize: '0.875rem', color: 'var(--text-secondary)' }}>
                                        <span>Exercice: <strong>{dec.exercice}</strong></span>
                                        <span>Période: <strong>{dec.periode}</strong></span>
                                        <span>Type: <strong>{dec.type}</strong></span>
                                    </div>
                                </div>
                                <div style={{ display: 'flex', alignItems: 'center', gap: '1.5rem' }}>
                                    <span style={{ 
                                        padding: '0.25rem 0.75rem', 
                                        borderRadius: '99px', 
                                        fontSize: '0.875rem', 
                                        fontWeight: 500,
                                        backgroundColor: dec.statut === 'EnCours' ? '#e0e7ff' : dec.statut === 'Clôturée' ? '#fef3c7' : '#dcfce7',
                                        color: dec.statut === 'EnCours' ? '#4338ca' : dec.statut === 'Clôturée' ? '#b45309' : '#15803d'
                                    }}>
                                        {dec.statut}
                                    </span>
                                    <button 
                                        onClick={() => onOpenDeclaration(dec.id)}
                                        style={{ background: 'transparent', border: '1px solid var(--border-color)', padding: '0.5rem', borderRadius: '4px', cursor: 'pointer', display: 'flex', alignItems: 'center', justifyContent: 'center', color: 'var(--text-secondary)' }}
                                        title="Ouvrir"
                                    >
                                        <ArrowRight size={18} />
                                    </button>
                                </div>
                            </div>
                        ))
                    )}
                </div>
            )}
        </div>
    );
}
