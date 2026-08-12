import { useState, useEffect } from 'react';
import { DomainGrid } from './DomainGrid';
import type { DomaineTVA } from './DeclarationStepper';
import api from './api';

// Code activité rendu éditable en ligne (select, cf. DomainGrid) — TASK-202 §2 : sur cet écran,
// l'affectation ne doit pas dépendre d'un aller-retour par le drill de l'étape « Vérifier ».
const FACTURES_COLUMNS: { key: string, label: string, filterType: 'list' | 'text' | 'number' | 'date', width?: string, derived?: boolean, editable?: boolean }[] = [
    { key: 'factureNumero', label: 'N° Facture', filterType: 'text' },
    { key: 'tiers', label: 'Tiers', filterType: 'text' },
    { key: 'origine', label: 'Origine', filterType: 'list' },
    { key: 'montantHT', label: 'Montant HT', filterType: 'number' },
    { key: 'tauxTVA', label: 'Taux TVA', filterType: 'list' },
    { key: 'montantTTC', label: 'Montant TTC', filterType: 'number' },
    { key: 'codeActivite', label: 'Code activité', filterType: 'text', width: '220px', editable: true },
    { key: 'source', label: 'Source', filterType: 'list' },
    { key: 'statutLigne', label: 'Statut', filterType: 'list' },
    { key: 'motif', label: 'Motif Écartement', filterType: 'text', width: '280px' },
];

export function FacturesADeclarerPanel({
    declarationId,
    readOnly = false,
    showToast,
    initialFilters,
    initialDomaine = 'Decaissement',
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

    const onglets = (
        <div style={{ display: 'flex', background: 'var(--bg-secondary)', padding: '2px', borderRadius: '6px', border: '1px solid var(--border-color)' }} title="Lignes candidates sélectionnées pour cette déclaration">
            <button
                onClick={() => setDomaine('Decaissement')}
                style={{
                    padding: '0.2rem 0.6rem',
                    fontSize: '0.75rem',
                    fontWeight: domaine === 'Decaissement' ? 600 : 500,
                    border: 'none',
                    borderRadius: '4px',
                    background: domaine === 'Decaissement' ? 'white' : 'transparent',
                    color: domaine === 'Decaissement' ? 'var(--accent-primary)' : 'var(--text-secondary)',
                    boxShadow: domaine === 'Decaissement' ? '0 1px 2px rgba(0,0,0,0.05)' : 'none',
                    cursor: 'pointer',
                    whiteSpace: 'nowrap',
                    transition: 'all 0.15s'
                }}
            >
                Achats
            </button>
            <button
                onClick={() => setDomaine('Encaissement')}
                style={{
                    padding: '0.2rem 0.6rem',
                    fontSize: '0.75rem',
                    fontWeight: domaine === 'Encaissement' ? 600 : 500,
                    border: 'none',
                    borderRadius: '4px',
                    background: domaine === 'Encaissement' ? 'white' : 'transparent',
                    color: domaine === 'Encaissement' ? 'var(--accent-primary)' : 'var(--text-secondary)',
                    boxShadow: domaine === 'Encaissement' ? '0 1px 2px rgba(0,0,0,0.05)' : 'none',
                    cursor: 'pointer',
                    whiteSpace: 'nowrap',
                    transition: 'all 0.15s'
                }}
            >
                Ventes
            </button>
        </div>
    );

    return (
        <div style={{ display: 'flex', flexDirection: 'column', height: '100%', overflow: 'hidden', background: 'var(--bg-primary)' }}>
            <DomainGrid
                declarationId={declarationId}
                domaine={domaine}
                readonly={readOnly}
                showToast={showToast}
                initialFilters={initialFilters}
                columns={FACTURES_COLUMNS}
                colsStorageKey="grf.cols.domain.factures"
                codeActiviteOptions={codeActiviteOptions}
                showResynchroniserAction={!readOnly}
                toolbarPrefix={onglets}
                onActionDone={() => {
                    if (onActionDone) onActionDone();
                }}
            />
        </div>
    );
}
