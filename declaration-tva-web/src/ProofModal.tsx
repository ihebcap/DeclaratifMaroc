import { useState } from 'react';
import { X } from 'lucide-react';
import { DomainGrid } from './DomainGrid';

export function ProofModal({ declarationId, numeroRapprochement, onClose }: { declarationId: string, numeroRapprochement: string, onClose: () => void }) {
    // We render 3 tabs or sections for the 3 proofs.
    const [activeProof, setActiveProof] = useState<'Rapprochement' | 'Affectation' | 'Conformite'>('Rapprochement');

    return (
        <div style={{
            position: 'fixed', top: 0, left: 0, width: '100%', height: '100%',
            background: 'rgba(0,0,0,0.5)', zIndex: 1000,
            display: 'flex', justifyContent: 'flex-end'
        }}>
            <div style={{
                width: '800px', height: '100%', background: 'white',
                boxShadow: '-4px 0 15px rgba(0,0,0,0.1)',
                display: 'flex', flexDirection: 'column'
            }}>
                <div style={{ padding: '1.5rem', borderBottom: '1px solid var(--border-color)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                    <div>
                        <h3 style={{ margin: 0, fontSize: '1.25rem', fontWeight: 600 }}>Preuves du règlement</h3>
                        <div style={{ fontSize: '0.875rem', color: 'var(--text-secondary)' }}>Règlement N° {numeroRapprochement}</div>
                    </div>
                    <button onClick={onClose} style={{ background: 'transparent', border: 'none', cursor: 'pointer' }}><X size={24}/></button>
                </div>

                <div style={{ display: 'flex', padding: '0 1.5rem', borderBottom: '1px solid var(--border-color)', gap: '1.5rem', background: 'var(--bg-secondary)' }}>
                    {[
                        { id: 'Rapprochement', label: 'Rapprochement' },
                        { id: 'Affectation', label: 'Affectation' },
                        { id: 'Conformite', label: 'Conformité IF/ICE' }
                    ].map(t => (
                        <button
                            key={t.id}
                            onClick={() => setActiveProof(t.id as any)}
                            style={{
                                padding: '1rem 0',
                                background: 'transparent',
                                border: 'none',
                                borderBottom: activeProof === t.id ? '2px solid var(--accent-primary)' : '2px solid transparent',
                                color: activeProof === t.id ? 'var(--accent-primary)' : 'var(--text-secondary)',
                                fontWeight: activeProof === t.id ? 600 : 400,
                                cursor: 'pointer',
                                fontSize: '0.9rem'
                            }}
                        >
                            {t.label}
                        </button>
                    ))}
                </div>

                <div style={{ flex: 1, padding: '1.5rem', overflow: 'hidden' }}>
                    <DomainGrid 
                        declarationId={declarationId} 
                        domaine={activeProof as any}
                        onActionDone={() => {}} 
                        showToast={(m) => alert(m)} 
                        initialFilters={{ numeroRapprochement: [numeroRapprochement] }}
                        readonly={true}
                        columns={
                            activeProof === 'Rapprochement' ? [
                                { key: 'numeroRapprochement', label: 'N° Rapprochement', filterType: 'list' as const },
                                { key: 'source', label: 'Domaine', filterType: 'list' as const },
                                { key: 'statutLigne', label: 'Statut (Éligible/Reporté)', filterType: 'list' as const }
                            ] : activeProof === 'Affectation' ? [
                                { key: 'numeroRapprochement', label: 'N° Rapprochement', filterType: 'list' as const },
                                { key: 'factureNumero', label: 'N° Facture', filterType: 'text' as const },
                                { key: 'montantHT', label: 'Montant Affecté HT', filterType: 'number' as const },
                                { key: 'tauxTVA', label: 'Taux TVA', filterType: 'list' as const },
                                { key: 'montantTTC', label: 'Montant Affecté TTC', filterType: 'number' as const },
                                { key: 'source', label: 'Nature (Déductible/Collectée)', filterType: 'list' as const }
                            ] : [
                                { key: 'tiers', label: 'Fournisseur', filterType: 'text' as const },
                                { key: 'tiersIdentifiantFiscal', label: 'IF', filterType: 'text' as const },
                                { key: 'tiersICE', label: 'ICE', filterType: 'text' as const },
                                { key: 'statutConformite', label: 'Conformité (Vert/Rouge)', filterType: 'list' as const }
                            ]
                        }
                    />
                </div>
            </div>
        </div>
    );
}
