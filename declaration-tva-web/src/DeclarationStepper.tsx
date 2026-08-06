import { useState, useEffect } from 'react';
import {
    ChevronLeft, Loader2, Landmark, Lock,
    ShieldCheck, CheckCircle2, Circle, ArrowRight, FileText, ShieldAlert,
} from 'lucide-react';
import api from './api';
import { DeclarationFinalePanel } from './DeclarationFinalePanel';
import { ReglementsSelection, periodeBounds } from './ReglementsSelection';
import type { ReglementRow } from './ReglementsSelection';
import { AffectationsDrill } from './AffectationsDrill';
import { VerifierIntegrerPanel } from './VerifierIntegrerPanel';
import { FacturesADeclarerPanel } from './FacturesADeclarerPanel';
import { formatMoney, formatDate } from './utils';

export type DomaineTVA = 'Décaissement' | 'Encaissement' | 'Dépense' | 'Frais bancaire';

type StepId = 'reglements' | 'factures' | 'verifier' | 'confirmer' | 'declaration';

const STEPS: { id: StepId; label: string; icon: typeof Landmark }[] = [
    { id: 'reglements', label: 'Sélection', icon: Landmark },
    { id: 'factures', label: 'Factures à déclarer', icon: FileText },
    { id: 'verifier', label: 'Vérifier', icon: ShieldAlert },
    { id: 'confirmer', label: 'Confirmer', icon: CheckCircle2 },
    { id: 'declaration', label: 'Déclaration', icon: ShieldCheck },
];

const STATUT_LABELS: Record<number, string> = { 0: 'En cours', 1: 'Clôturée', 2: 'Générée', 3: 'Déposée' };

function isIntegree(statut: number | undefined) {
    return statut !== undefined && statut !== 0;
}

function isUnlocked(stepId: StepId, integree: boolean) {
    if (stepId === 'declaration') return integree;
    return true;
}

export function DeclarationStepper({ declarationId, showToast, onBack }: { declarationId: string, showToast: (m: string, t?: any) => void, onBack: () => void }) {
    const [info, setInfo] = useState<any>(null);
    const [loading, setLoading] = useState(true);
    const [activeStep, setActiveStep] = useState<StepId>('reglements');
    const [selectedKeys, setSelectedKeys] = useState<Set<string>>(new Set());
    const [selectedRows, setSelectedRows] = useState<ReglementRow[]>([]);
    const [showDrill, setShowDrill] = useState(false);
    const [savedSelection, setSavedSelection] = useState<string[] | null>(null);

    const [facturesFilters, setFacturesFilters] = useState<Record<string, any> | undefined>(undefined);
    const [facturesDomaine, setFacturesDomaine] = useState<DomaineTVA>('Décaissement');

    const fetchInfo = async () => {
        try {
            const res = await api.get(`/declarations/${declarationId}`);
            setInfo(res.data);
            if (isIntegree(res.data.statut)) {
                setActiveStep('declaration');
            }

            const selRes = await api.get(`/declarations/${declarationId}/selection`);
            setSavedSelection(selRes.data);
        } catch (e) {
            console.error(e);
        } finally {
            setLoading(false);
        }
    };

    const persisterSelection = async () => {
        const selectedNumbers = selectedRows.map(r => r.numeroReglement);
        await api.post(`/declarations/${declarationId}/selection`, selectedNumbers);
        setSavedSelection(selectedNumbers);
    };

    const handlePasserAuCalcul = async () => {
        if (integree) {
            goTo('factures');
            return;
        }
        setLoading(true);
        try {
            await persisterSelection();
            goTo('factures');
        } catch (e: any) {
            console.error(e);
            showToast(e?.response?.data?.message || 'Erreur lors de la sauvegarde de la sélection', 'error');
        } finally {
            setLoading(false);
        }
    };

    const handleOuvrirDrill = async () => {
        if (integree) {
            setShowDrill(true);
            return;
        }
        setLoading(true);
        try {
            await persisterSelection();
            setShowDrill(true);
        } catch (e: any) {
            console.error(e);
            showToast(e?.response?.data?.message || 'Erreur lors de la sauvegarde de la sélection', 'error');
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        fetchInfo();
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [declarationId]);

    if (loading) return <div style={{ display: 'flex', justifyContent: 'center', padding: '3rem' }}><Loader2 size={32} className="animate-spin text-primary" /></div>;
    if (!info) return <div>Erreur de chargement</div>;

    const integree = isIntegree(info.statut);
    const readOnlyStep = integree && (activeStep === 'reglements' || activeStep === 'factures' || activeStep === 'verifier' || activeStep === 'confirmer');
    const hasSelection = selectedKeys.size > 0;

    const goTo = (id: StepId) => {
        if (!isUnlocked(id, integree)) return;
        setActiveStep(id);
    };

    const nextStep = (id: StepId, label: string) => (
        <BottomBar>
            <span style={{ color: 'var(--text-secondary)', fontSize: '0.8125rem' }}>Étape {STEPS.findIndex(s => s.id === activeStep) + 1}/{STEPS.length}</span>
            <button onClick={() => goTo(id)} disabled={!isUnlocked(id, integree)} className="btn btn-primary" style={ctaStyle}>
                {label} <ArrowRight size={16} />
            </button>
        </BottomBar>
    );

    const reglementsBottomBar = (
        <BottomBar>
            <span style={{ color: 'var(--text-secondary)', fontSize: '0.8125rem' }}>
                {hasSelection
                    ? <>{selectedKeys.size} règlement{selectedKeys.size > 1 ? 's' : ''} sélectionné{selectedKeys.size > 1 ? 's' : ''} · Total {formatMoney(selectedRows.reduce((s, r) => s + r.montant, 0))}</>
                    : 'Sélectionnez au moins un règlement pour continuer'}
            </span>
            <div style={{ display: 'flex', gap: '0.75rem' }}>
                <button
                    onClick={handleOuvrirDrill}
                    disabled={(!hasSelection && !integree) || loading}
                    className="btn"
                    style={{
                        display: 'flex', alignItems: 'center', gap: '0.5rem',
                        background: 'white', color: 'var(--text-primary)', border: '1px solid var(--border-color)',
                        padding: '0.5rem 1rem', borderRadius: '4px', cursor: (!hasSelection && !integree) ? 'not-allowed' : 'pointer', fontWeight: 600, fontSize: '0.875rem',
                    }}
                >
                    Détail des lignes
                </button>
                <button
                    onClick={handlePasserAuCalcul}
                    disabled={(!hasSelection && !integree) || loading}
                    className="btn btn-primary"
                    style={ctaStyle}
                >
                    Passer aux factures <ArrowRight size={16} />
                </button>
            </div>
        </BottomBar>
    );

    const titleSlot = (
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', padding: '0 0.85rem', borderRight: '1px solid var(--border-color)', flexShrink: 0 }}>
            <button onClick={onBack} className="btn" title="Retour aux déclarations" style={{ background: 'transparent', border: 'none', padding: 0, cursor: 'pointer', display: 'flex', alignItems: 'center', gap: '0.15rem', color: 'var(--text-secondary)', fontSize: '0.75rem' }}><ChevronLeft size={16} /> Retour</button>
            <div style={{ fontSize: '0.95rem', fontWeight: 600 }}>{info.numero}</div>
        </div>
    );

    const { debut, fin } = periodeBounds(info.exercice, info.type, info.periode);
    const trailingSlot = (
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', padding: '0 1rem', flexShrink: 0 }}>
            <span style={{ fontSize: '0.72rem', color: 'var(--text-secondary)', whiteSpace: 'nowrap' }}>
                Période {formatDate(debut)} → {formatDate(fin)}
            </span>
            {integree ? (
                <span style={{ fontSize: '0.72rem', fontWeight: 600, padding: '0.25rem 0.6rem', borderRadius: 'var(--radius-full)', background: 'var(--status-ok-bg)', color: 'var(--status-ok-text)', whiteSpace: 'nowrap' }}>
                    {STATUT_LABELS[info.statut] ?? info.statut} — lecture seule
                </span>
            ) : (
                <span style={{ fontSize: '0.72rem', fontWeight: 600, padding: '0.25rem 0.6rem', borderRadius: 'var(--radius-full)', background: 'var(--bg-secondary)', color: 'var(--text-secondary)', whiteSpace: 'nowrap' }}>
                    {STATUT_LABELS[info.statut] ?? info.statut}
                </span>
            )}
        </div>
    );

    if (showDrill) {
        return (
            <div style={{ display: 'flex', flexDirection: 'column', height: '100%', background: 'var(--bg-primary)' }}>
                {integree && (
                    <div style={{ padding: '0.5rem 1.5rem', background: '#f0fdf4', borderBottom: '1px solid #bbf7d0', color: 'var(--status-ok-text)', fontSize: '0.8125rem', fontWeight: 500, flexShrink: 0 }}>
                        Déclaration intégrée — drill en lecture (lecture seule).
                    </div>
                )}
                <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', padding: '0.75rem 1rem', borderBottom: '1px solid var(--border-color)', background: 'white', flexShrink: 0 }}>
                    <button onClick={() => setShowDrill(false)} className="btn" style={{ display: 'flex', alignItems: 'center', gap: '0.25rem', padding: '0.4rem 0.8rem', fontSize: '0.8125rem', cursor: 'pointer', border: '1px solid var(--border-color)', borderRadius: '4px', background: 'white' }}>
                        <ChevronLeft size={16} /> Retour à la sélection
                    </button>
                    <span style={{ fontSize: '0.9rem', color: 'var(--text-secondary)', marginLeft: '1rem' }}>
                        Détail des affectations — {info.numero}
                    </span>
                </div>
                <div style={{ flex: 1, overflow: 'hidden', display: 'flex', flexDirection: 'column' }}>
                    <AffectationsDrill
                        declarationId={declarationId}
                        selectedRows={selectedRows}
                        readOnly={readOnlyStep}
                        showToast={showToast}
                    />
                </div>
            </div>
        );
    }

    return (
        <div style={{ display: 'flex', flexDirection: 'column', height: '100%', background: 'var(--bg-primary)' }}>
            <StepBar activeStep={activeStep} integree={integree} onSelect={goTo} leading={titleSlot} trailing={trailingSlot} />

            <div style={{ flex: 1, overflow: 'hidden', display: 'flex', flexDirection: 'column' }}>
                {readOnlyStep && (
                    <div style={{ padding: '0.5rem 1.5rem', background: '#f0fdf4', borderBottom: '1px solid #bbf7d0', color: 'var(--status-ok-text)', fontSize: '0.8125rem', fontWeight: 500, flexShrink: 0 }}>
                        Déclaration intégrée — mode lecture seule.
                    </div>
                )}

                <div style={{ flex: 1, overflow: 'hidden', minHeight: 0, display: 'flex', flexDirection: 'column' }}>
                    {activeStep === 'reglements' && (
                        <ReglementsSelection
                            societeId={info.societeId}
                            exercice={info.exercice}
                            type={info.type}
                            periode={info.periode}
                            selectedKeys={selectedKeys}
                            selectedRows={selectedRows}
                            onSelectionChange={(keys, rows) => { setSelectedKeys(keys); setSelectedRows(rows); }}
                            showToast={showToast}
                            savedSelection={savedSelection}
                            declarationId={declarationId}
                        />
                    )}
                    {activeStep === 'factures' && (
                        <FacturesADeclarerPanel
                            declarationId={declarationId}
                            readOnly={readOnlyStep}
                            showToast={showToast}
                            initialFilters={facturesFilters}
                            initialDomaine={facturesDomaine}
                        />
                    )}
                    {activeStep === 'verifier' && (
                        <VerifierIntegrerPanel
                            mode="verifier"
                            declarationId={declarationId}
                            selectedRows={selectedRows}
                            readOnly={readOnlyStep}
                            integree={integree}
                            showToast={showToast}
                            onVoirLignes={(domaine, filter) => {
                                setFacturesDomaine(domaine);
                                setFacturesFilters(filter);
                                goTo('factures');
                            }}
                        />
                    )}
                    {activeStep === 'confirmer' && (
                        <VerifierIntegrerPanel
                            mode="confirmer"
                            declarationId={declarationId}
                            selectedRows={selectedRows}
                            readOnly={readOnlyStep}
                            integree={integree}
                            showToast={showToast}
                            onIntegrationSuccess={fetchInfo}
                            onGoToVerifier={() => goTo('verifier')}
                        />
                    )}
                    {activeStep === 'declaration' && (
                        <DeclarationFinalePanel
                            declarationId={declarationId}
                            statut={info.statut}
                            showToast={showToast}
                            onBack={onBack}
                        />
                    )}
                </div>

                {activeStep === 'reglements' && reglementsBottomBar}
                {activeStep === 'factures' && nextStep('verifier', 'Continuer vers la vérification')}
                {activeStep === 'verifier' && nextStep('confirmer', 'Continuer vers la confirmation')}
                {activeStep === 'confirmer' && integree && nextStep('declaration', 'Continuer vers la déclaration')}
            </div>
        </div>
    );
}

function StepBar({ activeStep, integree, onSelect, leading, trailing }: { activeStep: StepId; integree: boolean; onSelect: (id: StepId) => void; leading?: React.ReactNode; trailing?: React.ReactNode }) {
    return (
        <div style={{ display: 'flex', alignItems: 'stretch', background: 'white', borderBottom: '1px solid var(--border-color)', flexShrink: 0 }}>
            {leading}
            <div style={{ display: 'flex', flex: 1, overflowX: 'auto' }}>
            {STEPS.map((step, i) => {
                const unlocked = isUnlocked(step.id, integree);
                const isActive = activeStep === step.id;
                const isDone = integree && step.id !== 'declaration';
                return (
                    <button
                        key={step.id}
                        onClick={() => onSelect(step.id)}
                        disabled={!unlocked}
                        title={unlocked ? step.label : `${step.label} — nécessite l'intégration`}
                        style={{
                            display: 'flex', alignItems: 'center', gap: '0.35rem',
                            padding: '0.6rem 0.7rem',
                            background: isActive ? 'var(--bg-secondary)' : 'transparent',
                            border: 'none',
                            borderBottom: isActive ? '2px solid var(--accent-primary)' : '2px solid transparent',
                            color: !unlocked ? '#9ca3af' : isActive ? 'var(--accent-primary)' : 'var(--text-primary)',
                            fontWeight: isActive ? 600 : 500,
                            fontSize: '0.8125rem',
                            cursor: unlocked ? 'pointer' : 'not-allowed',
                            whiteSpace: 'nowrap',
                            flexShrink: 0,
                        }}
                    >
                        {isDone ? <CheckCircle2 size={15} style={{ color: '#16a34a' }} /> : !unlocked ? <Lock size={13} /> : <Circle size={13} style={{ opacity: isActive ? 1 : 0.4 }} />}
                        <span>{i + 1}. {step.label}</span>
                    </button>
                );
            })}
            </div>
            {trailing}
        </div>
    );
}

function BottomBar({ children }: { children: React.ReactNode }) {
    return (
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '0.75rem 1.5rem', background: 'white', borderTop: '1px solid var(--border-color)', flexShrink: 0 }}>
            {children}
        </div>
    );
}

const ctaStyle: React.CSSProperties = {
    display: 'flex', alignItems: 'center', gap: '0.5rem',
    background: 'var(--accent-primary)', color: 'white', border: 'none',
    padding: '0.5rem 1rem', borderRadius: '4px', cursor: 'pointer', fontWeight: 600, fontSize: '0.875rem',
};
