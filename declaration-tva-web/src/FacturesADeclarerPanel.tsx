import { useState, useEffect } from 'react';
import { DomainGrid } from './DomainGrid';
import type { DomaineTVA } from './DeclarationStepper';
import api from './api';
import { FileText, ArrowLeftRight } from 'lucide-react';

export function FacturesADeclarerPanel({
    declarationId,
    readOnly = false,
    showToast,
    initialFilters,
    initialDomaine = 'Décaissement',
    onActionDone,
}: {
    declarationId: string;
    readOnly?: boolean;
    showToast: (m: string, t?: any) => void;
    initialFilters?: Record<string, any>;
    initialDomaine?: DomaineTVA;
    onActionDone?: () => void;
}) {
    const [domaine, setDomaine] = useState<DomaineTVA>(initialDomaine);
    const [codeActiviteOptions, setCodeActiviteOptions] = useState<{ value: string, label: string }[]>([]);

    useEffect(() => {
        if (initialDomaine) {
            setDomaine(initialDomaine);
        }
    }, [initialDomaine]);

    useEffect(() => {
        let cancelled = false;
        (async () => {
            try {
                const res = await api.get('/codes-activite', { params: { domaine } });
                if (cancelled) return;
                const options = (res.data || []).map((r: any) => ({
                    value: r.code,
                    label: `${r.code} — ${r.libelle}`
                }));
                setCodeActiviteOptions(options);
            } catch (e) {
                console.error('Erreur lors du chargement du référentiel des codes activité', e);
            }
        })();
        return () => { cancelled = true; };
    }, [domaine]);

    return (
        <div style={{ display: 'flex', flexDirection: 'column', height: '100%', overflow: 'hidden', background: 'var(--bg-primary)' }}>
            {/* Header Onglets Achats / Ventes */}
            <div style={{
                display: 'flex', alignItems: 'center', justifyContent: 'space-between',
                padding: '0.6rem 1rem', background: 'white', borderBottom: '1px solid var(--border-color)',
                flexShrink: 0
            }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '0.35rem', fontSize: '0.875rem', fontWeight: 600, color: 'var(--text-primary)' }}>
                        <FileText size={18} className="text-primary" />
                        <span>Factures à déclarer</span>
                    </div>
                    <div style={{ display: 'flex', background: 'var(--bg-secondary)', padding: '2px', borderRadius: '6px', border: '1px solid var(--border-color)' }}>
                        <button
                            onClick={() => setDomaine('Décaissement')}
                            style={{
                                padding: '0.35rem 0.85rem',
                                fontSize: '0.8125rem',
                                fontWeight: domaine === 'Décaissement' ? 600 : 500,
                                border: 'none',
                                borderRadius: '4px',
                                background: domaine === 'Décaissement' ? 'white' : 'transparent',
                                color: domaine === 'Décaissement' ? 'var(--accent-primary)' : 'var(--text-secondary)',
                                boxShadow: domaine === 'Décaissement' ? '0 1px 2px rgba(0,0,0,0.05)' : 'none',
                                cursor: 'pointer',
                                transition: 'all 0.15s'
                            }}
                        >
                            TVA Déductible (Achats)
                        </button>
                        <button
                            onClick={() => setDomaine('Encaissement')}
                            style={{
                                padding: '0.35rem 0.85rem',
                                fontSize: '0.8125rem',
                                fontWeight: domaine === 'Encaissement' ? 600 : 500,
                                border: 'none',
                                borderRadius: '4px',
                                background: domaine === 'Encaissement' ? 'white' : 'transparent',
                                color: domaine === 'Encaissement' ? 'var(--accent-primary)' : 'var(--text-secondary)',
                                boxShadow: domaine === 'Encaissement' ? '0 1px 2px rgba(0,0,0,0.05)' : 'none',
                                cursor: 'pointer',
                                transition: 'all 0.15s'
                            }}
                        >
                            TVA Collective (Ventes)
                        </button>
                    </div>
                </div>

                <div style={{ fontSize: '0.78rem', color: 'var(--text-secondary)', display: 'flex', alignItems: 'center', gap: '0.4rem' }}>
                    <ArrowLeftRight size={14} />
                    <span>Lignes candidates sélectionnées pour cette déclaration</span>
                </div>
            </div>

            {/* Corps Grille AG Grid */}
            <div style={{ flex: 1, overflow: 'hidden', padding: '0.75rem' }}>
                <DomainGrid
                    declarationId={declarationId}
                    domaine={domaine}
                    readonly={readOnly}
                    showToast={showToast}
                    initialFilters={initialFilters}
                    codeActiviteOptions={codeActiviteOptions}
                    showResynchroniserAction={!readOnly}
                    onActionDone={() => {
                        if (onActionDone) onActionDone();
                    }}
                />
            </div>
        </div>
    );
}
