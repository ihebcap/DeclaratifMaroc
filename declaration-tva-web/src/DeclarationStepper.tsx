import { useState, useEffect } from 'react';
import {
    ChevronLeft, Loader2, Landmark, GitMerge, Calculator, Lock,
    ShieldCheck, FileOutput, CheckCircle2, Circle, ArrowRight,
    XCircle,
} from 'lucide-react';
import api from './api';
import { SynthesePanel } from './SynthesePanel';
import { ReglementsSelection, periodeBounds } from './ReglementsSelection';
import type { ReglementRow } from './ReglementsSelection';
import { AffectationsDrill } from './AffectationsDrill';
import { CalculTvaPanel } from './CalculTvaPanel';
import { IntegrationPanel } from './IntegrationPanel';
import { ControleDeclarationPanel } from './ControleDeclarationPanel';
import { formatMoney, formatDate } from './utils';

export type DomaineTVA = 'Décaissement' | 'Encaissement' | 'Dépense' | 'Frais bancaire';

// Tunnel règlement-first (TASK-053, cf. "reflexion dectva.md") : le règlement pilote,
// pas la facture. Les écrans dédiés ①②④⑤ arrivent en TASK-054/055/057/058 ; en attendant,
// ils sont affichés en placeholder honnête (aucune donnée factice). ③ et ⑥ réutilisent
// WorkstationPanel/GenerationPanel existants, intouchables.
type StepId = 'reglements' | 'affectations' | 'calcul' | 'integration' | 'controle' | 'synthese';

const STEPS: { id: StepId; label: string; icon: typeof Landmark }[] = [
    { id: 'reglements', label: 'Règlements', icon: Landmark },
    { id: 'affectations', label: 'Affectations', icon: GitMerge },
    { id: 'calcul', label: 'Calcul', icon: Calculator },
    { id: 'integration', label: 'Intégration', icon: Lock },
    { id: 'controle', label: 'Contrôle', icon: ShieldCheck },
    { id: 'synthese', label: 'Synthèse', icon: FileOutput },
];

// StatutDeclaration (back, WorkflowEntities.cs) : 0=EnCours, 1=Cloturee, 2=Generee, 3=Deposee.
// Seul signal réel disponible aujourd'hui côté API. Le tampon DT_Id (TASK-028/057)
// affinera ce gate quand l'écran ④ sera livré.
const STATUT_LABELS: Record<number, string> = { 0: 'En cours', 1: 'Clôturée', 2: 'Générée', 3: 'Déposée' };

function isIntegree(statut: number | undefined) {
    return statut !== undefined && statut !== 0;
}

// ⑤⑥ n'ont de sens qu'une fois l'intégration actée (règle réellement vérifiable).
// ③④ restent groupées tant que 056/057 n'exposent pas de gate plus fin — c'est un
// placeholder honnête, pas une simulation de règle métier. ② requiert désormais une
// sélection réelle en ① (TASK-054 : le règlement pilote, pas de drill sans sélection).
function isUnlocked(stepId: StepId, integree: boolean, hasSelection: boolean) {
    if (stepId === 'controle' || stepId === 'synthese') return integree;
    if (stepId === 'affectations') return integree || hasSelection;
    return true;
}

export function DeclarationStepper({ declarationId, showToast, onBack }: { declarationId: string, showToast: (m: string, t?: any) => void, onBack: () => void }) {
    const [info, setInfo] = useState<any>(null);
    const [loading, setLoading] = useState(true);
    const [activeStep, setActiveStep] = useState<StepId>('reglements');
    const [selectedKeys, setSelectedKeys] = useState<Set<string>>(new Set());
    const [selectedRows, setSelectedRows] = useState<ReglementRow[]>([]);
    // Données de ③ à transmettre à ④ en source unique (pas de recalcul front)
    const [calcTVA, setCalcTVA] = useState<{ totalTVA: number; nbLignes: number }>({ totalTVA: 0, nbLignes: 0 });
    // ⑤ : garde-fou — export bloqué si des anomalies 🔴 existent (TASK-058)
    const [hasBloquants, setHasBloquants] = useState(false);

    const fetchInfo = async () => {
        try {
            const res = await api.get(`/declarations/${declarationId}`);
            setInfo(res.data);
            if (isIntegree(res.data.statut)) {
                setActiveStep(prev => (prev === 'controle' || prev === 'synthese') ? prev : 'integration');
            }
        } catch (e) {
            console.error(e);
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
    const readOnlyStep = integree && (activeStep === 'reglements' || activeStep === 'affectations' || activeStep === 'calcul');
    const hasSelection = selectedKeys.size > 0;

    const goTo = (id: StepId) => {
        if (!isUnlocked(id, integree, hasSelection)) return;
        setActiveStep(id);
    };

    const nextStep = (id: StepId, label: string) => (
        <BottomBar>
            <span style={{ color: 'var(--text-secondary)', fontSize: '0.8125rem' }}>Étape {STEPS.findIndex(s => s.id === activeStep) + 1}/6</span>
            <button onClick={() => goTo(id)} disabled={!isUnlocked(id, integree, hasSelection)} className="btn btn-primary" style={ctaStyle}>
                {label} <ArrowRight size={16} />
            </button>
        </BottomBar>
    );

    // Bandeau bas dédié à ① (TASK-054) : total vivant de la sélection + CTA unique,
    // désactivé tant qu'aucun règlement éligible/à contrôler n'est sélectionné.
    const reglementsBottomBar = (
        <BottomBar>
            <span style={{ color: 'var(--text-secondary)', fontSize: '0.8125rem' }}>
                {hasSelection
                    ? <>{selectedKeys.size} règlement{selectedKeys.size > 1 ? 's' : ''} sélectionné{selectedKeys.size > 1 ? 's' : ''} · Total {formatMoney(selectedRows.reduce((s, r) => s + r.montant, 0))}</>
                    : 'Sélectionnez au moins un règlement pour continuer'}
            </span>
            <button onClick={() => goTo('affectations')} disabled={!hasSelection || readOnlyStep} className="btn btn-primary" style={ctaStyle}>
                Analyser TVA <ArrowRight size={16} />
            </button>
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

    return (
        <div style={{ display: 'flex', flexDirection: 'column', height: '100%', background: 'var(--bg-primary)' }}>
            <StepBar activeStep={activeStep} integree={integree} hasSelection={hasSelection} onSelect={goTo} leading={titleSlot} trailing={trailingSlot} />

            <div style={{ flex: 1, overflow: 'hidden', display: 'flex', flexDirection: 'column' }}>
                {readOnlyStep && (
                    <div style={{ padding: '0.5rem 1.5rem', background: '#f0fdf4', borderBottom: '1px solid #bbf7d0', color: 'var(--status-ok-text)', fontSize: '0.8125rem', fontWeight: 500, flexShrink: 0 }}>
                        Déclaration intégrée — cette étape est figée (lecture seule).
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
                            onSelectionChange={(keys, rows) => { setSelectedKeys(keys); setSelectedRows(rows); }}
                            showToast={showToast}
                        />
                    )}
                    {activeStep === 'affectations' && (
                        <AffectationsDrill
                            declarationId={declarationId}
                            selectedRows={selectedRows}
                            readOnly={readOnlyStep}
                            showToast={showToast}
                        />
                    )}
                    {activeStep === 'calcul' && (
                        <CalculTvaPanel
                            declarationId={declarationId}
                            selectedRows={selectedRows}
                            readOnly={readOnlyStep}
                            showToast={showToast}
                            onCalcSummary={(totalTVA, nbLignes) => setCalcTVA({ totalTVA, nbLignes })}
                        />
                    )}
                    {activeStep === 'integration' && (
                        <IntegrationPanel
                            declarationId={declarationId}
                            nbLignes={calcTVA.nbLignes}
                            totalTVA={calcTVA.totalTVA}
                            nbReglements={selectedRows.length}
                            integree={integree}
                            showToast={showToast}
                            onIntegrationSuccess={fetchInfo}
                        />
                    )}
                    {activeStep === 'controle' && (
                        <ControleDeclarationPanel
                            declarationId={declarationId}
                            showToast={showToast}
                            onHasBloquants={setHasBloquants}
                        />
                    )}
                    {activeStep === 'synthese' && (
                        <SynthesePanel
                            declarationId={declarationId}
                            statut={info.statut}
                            showToast={showToast}
                            onClotured={fetchInfo}
                            onBack={onBack}
                        />
                    )}
                </div>

                {activeStep === 'reglements' && reglementsBottomBar}
                {activeStep === 'affectations' && nextStep('calcul', 'Passer au calcul')}
                {activeStep === 'calcul' && nextStep('integration', '→ Passer à l\'intégration')}
                {activeStep === 'integration' && integree && nextStep('controle', 'Continuer vers le contrôle')}
                {activeStep === 'controle' && (
                    <BottomBar>
                        <span style={{ color: 'var(--text-secondary)', fontSize: '0.8125rem' }}>Étape 5/6</span>
                        {hasBloquants && (
                            <span style={{ display: 'flex', alignItems: 'center', gap: '0.35rem', fontSize: '0.8125rem', color: 'var(--status-blocking-text)', fontWeight: 600 }}>
                                <XCircle size={14} /> Corriger les anomalies 🔴 avant de continuer
                            </span>
                        )}
                        <button
                            onClick={() => goTo('synthese')}
                            disabled={hasBloquants || !isUnlocked('synthese', integree, hasSelection)}
                            className="btn btn-primary"
                            title={hasBloquants ? 'Des anomalies bloquantes empêchent la progression' : undefined}
                            style={ctaStyle}
                        >
                            Passer à la synthèse <ArrowRight size={16} />
                        </button>
                    </BottomBar>
                )}
            </div>
        </div>
    );
}

function StepBar({ activeStep, integree, hasSelection, onSelect, leading, trailing }: { activeStep: StepId; integree: boolean; hasSelection: boolean; onSelect: (id: StepId) => void; leading?: React.ReactNode; trailing?: React.ReactNode }) {
    return (
        <div style={{ display: 'flex', alignItems: 'stretch', background: 'white', borderBottom: '1px solid var(--border-color)', flexShrink: 0 }}>
            {leading}
            <div style={{ display: 'flex', flex: 1, overflowX: 'auto' }}>
            {STEPS.map((step, i) => {
                const unlocked = isUnlocked(step.id, integree, hasSelection);
                const isActive = activeStep === step.id;
                const isDone = integree && (step.id === 'reglements' || step.id === 'affectations' || step.id === 'calcul' || step.id === 'integration');
                return (
                    <button
                        key={step.id}
                        onClick={() => onSelect(step.id)}
                        disabled={!unlocked}
                        title={unlocked ? step.label : (step.id === 'affectations' ? `${step.label} — sélectionnez un règlement en ①` : `${step.label} — nécessite l'intégration`)}
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
