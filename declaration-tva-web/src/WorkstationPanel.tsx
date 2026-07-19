import { useState, useEffect } from 'react';
import { AlertCircle, Lock, Loader2 } from 'lucide-react';
import api from './api';
import { DomainGrid } from './DomainGrid';
import { ProofModal } from './ProofModal';

export function WorkstationPanel({ declarationId, onClotured }: { declarationId: string, onClotured: () => void }) {
    const [checkup, setCheckup] = useState<any>(null);
    const [loading, setLoading] = useState(true);
    const [clotureLoading, setClotureLoading] = useState(false);
    const [clotureError, setClotureError] = useState<string | null>(null);
    const [activeTab, setActiveTab] = useState<'Rapprochement' | 'Affectation' | 'Conformite' | 'Factures'>('Factures');
    const [face, setFace] = useState<'Declare' | 'NeDeclarePas'>('Declare');
    const [proofNumero, setProofNumero] = useState<string | null>(null);

    const fetchCheckup = async () => {
        try {
            await api.get(`/declarations/${declarationId}/lignes?domaine=Decaissement`);
            const res = await api.get(`/declarations/${declarationId}/checkup`);
            setCheckup(res.data);
        } catch (e) {
            console.error(e);
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        fetchCheckup();
    }, [declarationId]);

    if (loading) return <div style={{ display: 'flex', justifyContent: 'center', padding: '3rem' }}><Loader2 size={32} className="animate-spin text-primary" /></div>;
    if (!checkup) return <div>Erreur de chargement du checkup</div>;

    const hasBlocking = checkup.alertes.some((a: any) => a.type === 'bloquant');
    const remainingToDecide = checkup.reconciliation.proposees;
    const canCloture = !hasBlocking && remainingToDecide === 0;

    const handleCloturer = async () => {
        if (!canCloture) return;
        setClotureLoading(true);
        setClotureError(null);
        try {
            await api.post(`/declarations/${declarationId}/cloture`);
            onClotured();
        } catch (e: any) {
            setClotureError(e.response?.data?.message || e.message);
        } finally {
            setClotureLoading(false);
        }
    };

    const rec = checkup.reconciliation;
    const total = rec.candidates;
    const segments = [
        { key: 'Intégrées', count: rec.integrees, color: 'var(--status-ok-text)', bg: 'var(--status-ok-bg)' },
        { key: 'Exclues', count: rec.exclues, color: 'var(--status-blocking-text)', bg: 'var(--status-blocking-bg)' },
        { key: 'Reportées', count: rec.reportees, color: 'var(--status-warning-text-alt)', bg: '#fef3c7' },
        { key: 'Écartées', count: rec.ecartees, color: '#374151', bg: '#f3f4f6' },
        { key: 'Proposées', count: rec.proposees, color: '#4b5563', bg: '#e5e7eb' },
    ];

    const getInitialFiltersForFactures = () => {
        if (face === 'Declare') {
            return { statutLigne: ['Intégrée', 'Proposée'] };
        } else {
            return { statutLigne: ['Reportée', 'Écartée', 'Exclue'] };
        }
    };

    const getColumnsForTab = (tab: string) => {
        if (tab === 'Rapprochement') {
            return [
                { key: 'numeroRapprochement', label: 'N° Rapprochement', filterType: 'list' as const },
                { key: 'source', label: 'Domaine', filterType: 'list' as const },
                { key: 'statutLigne', label: 'Statut (Éligible/Reporté)', filterType: 'list' as const }
            ];
        } else if (tab === 'Affectation') {
            return [
                { key: 'numeroRapprochement', label: 'N° Rapprochement', filterType: 'list' as const },
                { key: 'factureNumero', label: 'N° Facture', filterType: 'text' as const },
                { key: 'montantHT', label: 'Montant Affecté HT', filterType: 'number' as const },
                { key: 'tauxTVA', label: 'Taux TVA', filterType: 'list' as const },
                { key: 'montantTTC', label: 'Montant Affecté TTC', filterType: 'number' as const },
                { key: 'source', label: 'Nature (Déductible/Collectée)', filterType: 'list' as const }
            ];
        } else if (tab === 'Conformite') {
            return [
                { key: 'tiers', label: 'Fournisseur', filterType: 'text' as const },
                { key: 'tiersIdentifiantFiscal', label: 'IF', filterType: 'text' as const },
                { key: 'tiersICE', label: 'ICE', filterType: 'text' as const },
                { key: 'statutConformite', label: 'Conformité (Vert/Rouge)', filterType: 'list' as const }
            ];
        }
        return undefined; // default columns for Factures
    };


    return (
        <div style={{ display: 'flex', flexDirection: 'column', height: '100%', overflow: 'hidden' }}>
            {/* Banner Réconciliation */}
            <div style={{ background: 'white', padding: '0.75rem 1.5rem', borderBottom: '1px solid var(--border-color)', display: 'flex', alignItems: 'center', gap: '2rem', flexShrink: 0 }}>
                <div style={{ fontWeight: 600, color: 'var(--text-secondary)' }}>Réconciliation :</div>
                <div style={{ flex: 1, display: 'flex', height: '12px', borderRadius: '99px', overflow: 'hidden', background: 'var(--bg-secondary)' }}>
                    {segments.map(s => {
                        const pct = total > 0 ? (s.count / total) * 100 : 0;
                        if (pct === 0) return null;
                        return <div key={s.key} style={{ width: `${pct}%`, background: s.color }} title={`${s.key}: ${s.count}`} />
                    })}
                </div>
                <div style={{ display: 'flex', gap: '1rem', fontSize: '0.75rem' }}>
                    {segments.map(s => s.count > 0 && (
                        <div key={s.key} style={{ display: 'flex', alignItems: 'center', gap: '0.25rem' }}>
                            <div style={{ width: '8px', height: '8px', borderRadius: '50%', background: s.color }} />
                            <span>{s.count} {s.key}</span>
                        </div>
                    ))}
                </div>
                <button 
                    onClick={handleCloturer} 
                    disabled={!canCloture || clotureLoading}
                    className="btn"
                    style={{ 
                        display: 'flex', alignItems: 'center', gap: '0.5rem', 
                        background: canCloture ? '#16a34a' : '#e5e7eb', 
                        color: canCloture ? 'white' : '#9ca3af', 
                        padding: '0.5rem 1rem', border: 'none', borderRadius: '4px', 
                        cursor: canCloture ? 'pointer' : 'not-allowed', fontWeight: 600, fontSize: '0.875rem'
                    }}
                >
                    {clotureLoading ? <Loader2 size={16} className="animate-spin" /> : <Lock size={16} />}
                    {remainingToDecide > 0 ? `Clôturer (${remainingToDecide} à faire)` : 'Clôturer la déclaration'}
                </button>
            </div>

            {clotureError && (
                <div style={{ padding: '0.75rem 1.5rem', background: 'var(--status-blocking-bg)', color: 'var(--status-blocking-text)', borderBottom: '1px solid #fecaca', display: 'flex', alignItems: 'center', gap: '0.5rem', flexShrink: 0 }}>
                    <AlertCircle size={18} />
                    {clotureError}
                </div>
            )}

            {/* Navigation Tabs */}
            <div style={{ display: 'flex', padding: '0 1.5rem', background: 'white', borderBottom: '1px solid var(--border-color)', gap: '1.5rem', flexShrink: 0 }}>
                {[
                    { id: 'Factures', label: 'Factures à déclarer' },
                    { id: 'Rapprochement', label: 'Rapprochement' },
                    { id: 'Affectation', label: 'Affectation' },
                    { id: 'Conformite', label: 'Conformité IF/ICE' }
                ].map(t => (
                    <button
                        key={t.id}
                        onClick={() => setActiveTab(t.id as any)}
                        style={{
                            padding: '1rem 0',
                            background: 'transparent',
                            border: 'none',
                            borderBottom: activeTab === t.id ? '2px solid var(--accent-primary)' : '2px solid transparent',
                            color: activeTab === t.id ? 'var(--accent-primary)' : 'var(--text-secondary)',
                            fontWeight: activeTab === t.id ? 600 : 400,
                            cursor: 'pointer',
                            fontSize: '0.9rem'
                        }}
                    >
                        {t.label}
                    </button>
                ))}
            </div>

            {/* Main Content Area */}
            <div style={{ flex: 1, padding: '1.5rem', overflow: 'hidden', display: 'flex', flexDirection: 'column', gap: '1rem' }}>
                {activeTab === 'Factures' && (
                    <>
                        <div style={{ display: 'flex', gap: '1rem' }}>
                            <button
                                onClick={() => setFace('Declare')}
                                style={{
                                    padding: '0.5rem 1rem', borderRadius: '4px', border: '1px solid var(--border-color)',
                                    background: face === 'Declare' ? 'var(--bg-secondary)' : 'white',
                                    fontWeight: face === 'Declare' ? 600 : 400, cursor: 'pointer'
                                }}
                            >
                                Je déclare
                            </button>
                            <button
                                onClick={() => setFace('NeDeclarePas')}
                                style={{
                                    padding: '0.5rem 1rem', borderRadius: '4px', border: '1px solid var(--border-color)',
                                    background: face === 'NeDeclarePas' ? 'var(--bg-secondary)' : 'white',
                                    fontWeight: face === 'NeDeclarePas' ? 600 : 400, cursor: 'pointer'
                                }}
                            >
                                Je ne déclare pas
                            </button>
                        </div>
                        <div style={{ flex: 1, overflow: 'hidden' }}>
                            {/* We use a key to force remount when face changes to properly reset initialFilters */}
                            <DomainGrid 
                                key={face}
                                declarationId={declarationId} 
                                onActionDone={fetchCheckup} 
                                showToast={(m: string) => alert(m)} 
                                initialFilters={getInitialFiltersForFactures()}
                                readonly={false}
                                onRowClick={(row) => row.numeroRapprochement && setProofNumero(row.numeroRapprochement)}
                            />
                        </div>
                    </>
                )}
                
                {activeTab !== 'Factures' && (
                    <div style={{ flex: 1, overflow: 'hidden' }}>
                        <DomainGrid 
                            key={activeTab}
                            declarationId={declarationId} 
                            domaine={activeTab as any}
                            onActionDone={fetchCheckup} 
                            showToast={(m: string) => alert(m)} 
                            readonly={true}
                            columns={getColumnsForTab(activeTab)}
                            onRowClick={(row) => row.numeroRapprochement && setProofNumero(row.numeroRapprochement)}
                        />
                    </div>
                )}
            </div>

            {proofNumero && (
                <ProofModal 
                    declarationId={declarationId} 
                    numeroRapprochement={proofNumero} 
                    onClose={() => setProofNumero(null)} 
                />
            )}
        </div>
    );
}
