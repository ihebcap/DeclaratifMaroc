import { useState, useEffect } from 'react';
import { ChevronLeft, Loader2 } from 'lucide-react';
import api from './api';
import { WorkstationPanel } from './WorkstationPanel';
import { GenerationPanel } from './GenerationPanel';

export type DomaineTVA = 'Décaissement' | 'Encaissement' | 'Dépense' | 'Frais bancaire';
type Step = 'Workstation' | 'Génération';

export function DeclarationStepper({ declarationId, showToast, onBack }: { declarationId: string, showToast: (m: string, t?: any) => void, onBack: () => void }) {
    const [info, setInfo] = useState<any>(null);
    const [loading, setLoading] = useState(true);
    const [activeStep, setActiveStep] = useState<Step>('Workstation');

    const fetchInfo = async () => {
        try {
            const res = await api.get(`/declarations/${declarationId}`);
            setInfo(res.data);
            if (res.data.statut === 'Clôturée' || res.data.statut === 'Générée') {
                setActiveStep('Génération');
            }
        } catch (e) {
            console.error(e);
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        fetchInfo();
    }, [declarationId]);

    if (loading) return <div style={{ display: 'flex', justifyContent: 'center', padding: '3rem' }}><Loader2 size={32} className="animate-spin text-primary" /></div>;
    if (!info) return <div>Erreur de chargement</div>;

    return (
        <div style={{ display: 'flex', flexDirection: 'column', height: '100%', background: 'var(--bg-primary)' }}>
            <div style={{ padding: '1rem 1.5rem', background: 'white', borderBottom: '1px solid var(--border-color)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
                    <div>
                        <button onClick={onBack} className="btn" style={{ background: 'transparent', border: 'none', padding: 0, cursor: 'pointer', display: 'flex', alignItems: 'center', color: 'var(--text-secondary)' }}><ChevronLeft size={20}/></button>
                    </div>
                    <div>
                        <h2 style={{ margin: 0, fontSize: '1.25rem', fontWeight: 600 }}>{info.numero}</h2>
                        <span style={{ fontSize: '0.875rem', color: 'var(--text-secondary)' }}>Statut: <strong>{info.statut}</strong></span>
                    </div>
                </div>
            </div>

            <div style={{ flex: 1, overflow: 'hidden', padding: '0' }}>
                {activeStep === 'Workstation' ? (
                    <WorkstationPanel 
                        declarationId={declarationId}
                        onClotured={() => {
                            showToast('Déclaration clôturée avec succès');
                            fetchInfo();
                        }} 
                    />
                ) : (
                    <GenerationPanel declarationId={declarationId} onBack={onBack} />
                )}
            </div>
        </div>
    );
}
