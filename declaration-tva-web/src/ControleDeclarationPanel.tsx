import { useState, useEffect } from 'react';
import {
    ShieldCheck, Loader2, CheckCircle2, XCircle,
    AlertTriangle, BarChart2, Percent, ArrowRight,
    ChevronDown, ChevronRight, ExternalLink,
} from 'lucide-react';
import api from './api';
import { formatMoney } from './utils';
import { DomainGrid } from './DomainGrid';
import type { DomaineTVA } from './DeclarationStepper';

// ─── Écran ⑤ Contrôle déclaration (TASK-058) ─────────────────────────────────
//
// Vue de contrôle final post-intégration : répartition par source & par taux,
// liste d'anomalies typées 🔴 bloquantes / 🟠 avertissements, drill → grille.
// Source unique : GET /declarations/{id}/checkup (back TASK-009).
// Aucun recalcul front — lecture seule — densité 0 espace perdu.
// Ne pas ressusciter le hub agrégat abandonné (TASK-018).

// ─── Types ────────────────────────────────────────────────────────────────────

type AlertType = 'bloquant' | 'avertissement';

interface CheckupAlerte {
    type: AlertType;
    message: string;
    ligneId?: string;
    domaine?: string;
    filtre?: Record<string, any>;
}

interface RecapLigne {
    source?: string;
    taux?: number;
    ht: number;
    tva: number;
    ttc: number;
}

interface CheckupData {
    recapSource: (RecapLigne & { source: string })[];
    recapTaux: (RecapLigne & { taux: number })[];
    alertes: CheckupAlerte[];
    reconciliation: {
        candidates: number;
        integrees: number;
        exclues: number;
        reportees: number;
        ecartees: number;
        proposees: number;
    };
    equilibre?: { isValid: boolean; ecart?: number };
}

// ─── Source labels ─────────────────────────────────────────────────────────────

const SOURCE_LABELS: Record<string, string> = {
    ACHAT: 'Décaissements fournisseurs',
    VENTE: 'Encaissements clients',
    CAISSE: 'Espèces',
    BANQUE: 'Frais bancaires',
    Décaissement: 'Décaissements',
    Encaissement: 'Encaissements',
    Dépense: 'Dépenses',
    'Frais bancaire': 'Frais bancaires',
};

function sourceLabel(s: string): string {
    return SOURCE_LABELS[s] ?? s;
}

// ─── Props ────────────────────────────────────────────────────────────────────

export interface ControleDeclarationPanelProps {
    declarationId: string;
    showToast: (m: string, t?: 'success' | 'error' | 'warning') => void;
    /** Appelé par DeclarationStepper pour savoir si on peut passer à ⑥ */
    onHasBloquants?: (has: boolean) => void;
}

// ─── Composant principal ─────────────────────────────────────────────────────

export function ControleDeclarationPanel({
    declarationId,
    showToast,
    onHasBloquants,
}: ControleDeclarationPanelProps) {
    const [data, setData] = useState<CheckupData | null>(null);
    const [loading, setLoading] = useState(true);

    // Drill anomalie → grille filtrée
    const [drillFiltre, setDrillFiltre] = useState<{
        domaine: DomaineTVA;
        filtre: Record<string, any>;
        label: string;
    } | null>(null);

    // Sections repliables
    const [sourceOpen, setSourceOpen] = useState(true);
    const [tauxOpen, setTauxOpen] = useState(true);
    const [bloquantsOpen, setBloquantsOpen] = useState(true);
    const [avertissementsOpen, setAvertissementsOpen] = useState(true);

    useEffect(() => {
        let cancelled = false;
        (async () => {
            setLoading(true);
            try {
                const res = await api.get(`/declarations/${declarationId}/checkup`);
                if (!cancelled) {
                    setData(res.data as CheckupData);
                    const hasBloquant = (res.data as CheckupData).alertes.some(
                        (a) => a.type === 'bloquant',
                    );
                    onHasBloquants?.(hasBloquant);
                }
            } catch (e) {
                console.error(e);
                if (!cancelled) showToast('Erreur lors du chargement du contrôle', 'error');
            } finally {
                if (!cancelled) setLoading(false);
            }
        })();
        return () => { cancelled = true; };
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [declarationId]);

    if (loading) {
        return (
            <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100%' }}>
                <Loader2 size={28} className="animate-spin" style={{ color: 'var(--accent-primary)' }} />
            </div>
        );
    }

    if (!data) return <div style={{ padding: '2rem', color: 'var(--danger)' }}>Erreur de chargement.</div>;

    const bloquants = data.alertes.filter((a) => a.type === 'bloquant');
    const avertissements = data.alertes.filter((a) => a.type === 'avertissement');
    const hasBloquant = bloquants.length > 0;

    const totalSource = data.recapSource.reduce((s, r) => s + r.tva, 0);
    const totalHT = data.recapSource.reduce((s, r) => s + r.ht, 0);
    const totalTTC = data.recapSource.reduce((s, r) => s + r.ttc, 0);

    // Si on est en vue drill, on affiche la DomainGrid filtrée
    if (drillFiltre) {
        return (
            <div style={{ display: 'flex', flexDirection: 'column', height: '100%', overflow: 'hidden' }}>
                {/* Bandeau drill */}
                <div style={{
                    padding: '0.5rem 1rem',
                    background: '#fffbeb',
                    borderBottom: '1px solid #fde68a',
                    display: 'flex',
                    alignItems: 'center',
                    gap: '0.75rem',
                    flexShrink: 0,
                    fontSize: '0.8125rem',
                }}>
                    <button
                        onClick={() => setDrillFiltre(null)}
                        style={{
                            display: 'flex', alignItems: 'center', gap: '0.4rem',
                            background: 'white', border: '1px solid #fde68a',
                            borderRadius: 'var(--radius-md)', padding: '0.25rem 0.6rem',
                            cursor: 'pointer', fontSize: '0.8125rem', fontWeight: 500,
                            color: 'var(--text-primary)',
                        }}
                    >
                        ← Retour au contrôle
                    </button>
                    <span style={{ color: 'var(--text-secondary)' }}>
                        Drill anomalie :
                    </span>
                    <span style={{ fontWeight: 600 }}>{drillFiltre.label}</span>
                </div>
                {/* Grille filtrée en lecture seule (TASK-016) */}
                <div style={{ flex: 1, overflow: 'hidden' }}>
                    <DomainGrid
                        declarationId={declarationId}
                        domaine={drillFiltre.domaine}
                        onActionDone={() => {}}
                        showToast={showToast}
                        initialFilters={drillFiltre.filtre}
                        readonly={true}
                    />
                </div>
            </div>
        );
    }

    return (
        <div style={{
            display: 'flex', flexDirection: 'column', height: '100%',
            overflow: 'auto', background: 'var(--bg-secondary)',
        }}>
            {/* En-tête */}
            <div style={{
                padding: '0.6rem 1rem',
                background: 'white',
                borderBottom: '1px solid var(--border-color)',
                display: 'flex',
                alignItems: 'center',
                gap: '0.6rem',
                flexShrink: 0,
            }}>
                <ShieldCheck size={18} style={{ color: hasBloquant ? 'var(--danger)' : 'var(--success)' }} />
                <div>
                    <h2 style={{ margin: 0, fontSize: '1rem', fontWeight: 600 }}>⑤ Contrôle déclaration</h2>
                    <div style={{ fontSize: '0.72rem', color: 'var(--text-secondary)' }}>
                        Répartition source / taux · anomalies · lecture seule · source = back (TASK-009)
                    </div>
                </div>
                <div style={{ marginLeft: 'auto', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                    {hasBloquant ? (
                        <span style={{
                            display: 'inline-flex', alignItems: 'center', gap: '0.3rem',
                            fontSize: '0.75rem', fontWeight: 700,
                            padding: '0.25rem 0.65rem', borderRadius: 'var(--radius-full)',
                            background: '#fee2e2', color: '#b91c1c',
                            border: '1px solid #fca5a5',
                        }}>
                            <XCircle size={13} /> {bloquants.length} bloquant{bloquants.length > 1 ? 's' : ''}
                        </span>
                    ) : (
                        <span style={{
                            display: 'inline-flex', alignItems: 'center', gap: '0.3rem',
                            fontSize: '0.75rem', fontWeight: 700,
                            padding: '0.25rem 0.65rem', borderRadius: 'var(--radius-full)',
                            background: '#dcfce7', color: '#15803d',
                            border: '1px solid #bbf7d0',
                        }}>
                            <CheckCircle2 size={13} /> Aucun bloquant
                        </span>
                    )}
                    {avertissements.length > 0 && (
                        <span style={{
                            display: 'inline-flex', alignItems: 'center', gap: '0.3rem',
                            fontSize: '0.75rem', fontWeight: 700,
                            padding: '0.25rem 0.65rem', borderRadius: 'var(--radius-full)',
                            background: '#fff7ed', color: '#c2410c',
                            border: '1px solid #fed7aa',
                        }}>
                            <AlertTriangle size={13} /> {avertissements.length} avert.
                        </span>
                    )}
                </div>
            </div>

            {/* Garde-fou bloquant */}
            {hasBloquant && (
                <div style={{
                    padding: '0.5rem 1rem',
                    background: '#fee2e2',
                    borderBottom: '1px solid #fca5a5',
                    color: '#b91c1c',
                    fontSize: '0.8125rem',
                    fontWeight: 600,
                    flexShrink: 0,
                    display: 'flex',
                    alignItems: 'center',
                    gap: '0.5rem',
                }}>
                    <XCircle size={14} />
                    Export / clôture bloqués — corrigez les anomalies 🔴 avant de continuer.
                </div>
            )}

            {/* Corps scrollable dense */}
            <div style={{ flex: 1, padding: '0.65rem', display: 'flex', flexDirection: 'column', gap: '0.6rem' }}>

                {/* ① Répartition par source */}
                <Section
                    icon={<BarChart2 size={14} />}
                    title="Répartition par source"
                    open={sourceOpen}
                    onToggle={() => setSourceOpen(!sourceOpen)}
                    badge={data.recapSource.length}
                    statusIcon={
                        data.equilibre
                            ? data.equilibre.isValid
                                ? <span style={{ fontSize: '0.7rem', color: '#15803d', fontWeight: 700 }}>✔ Équilibre OK</span>
                                : <span style={{ fontSize: '0.7rem', color: '#b91c1c', fontWeight: 700 }}>✘ Déséquilibre {formatMoney(data.equilibre.ecart ?? 0)}</span>
                            : null
                    }
                >
                    <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.8125rem' }}>
                        <thead>
                            <tr style={{ background: 'var(--bg-tertiary)' }}>
                                <Th>Source</Th>
                                <Th right>HT</Th>
                                <Th right>TVA</Th>
                                <Th right>TTC</Th>
                            </tr>
                        </thead>
                        <tbody>
                            {data.recapSource.map((r) => (
                                <tr key={r.source} style={{ borderBottom: '1px solid var(--border-color)' }}>
                                    <Td>{sourceLabel(r.source)}</Td>
                                    <Td right>{formatMoney(r.ht)}</Td>
                                    <Td right>{formatMoney(r.tva)}</Td>
                                    <Td right>{formatMoney(r.ttc)}</Td>
                                </tr>
                            ))}
                        </tbody>
                        <tfoot>
                            <tr style={{ background: 'var(--bg-tertiary)', fontWeight: 700, fontSize: '0.8125rem' }}>
                                <Td>Total</Td>
                                <Td right>{formatMoney(totalHT)}</Td>
                                <Td right>{formatMoney(totalSource)}</Td>
                                <Td right>{formatMoney(totalTTC)}</Td>
                            </tr>
                        </tfoot>
                    </table>
                </Section>

                {/* ② Vue par taux */}
                <Section
                    icon={<Percent size={14} />}
                    title="Vue par taux TVA"
                    open={tauxOpen}
                    onToggle={() => setTauxOpen(!tauxOpen)}
                    badge={data.recapTaux.length}
                >
                    <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.8125rem' }}>
                        <thead>
                            <tr style={{ background: 'var(--bg-tertiary)' }}>
                                <Th>Taux</Th>
                                <Th right>HT</Th>
                                <Th right>TVA</Th>
                                <Th right>TTC</Th>
                            </tr>
                        </thead>
                        <tbody>
                            {data.recapTaux
                                .slice()
                                .sort((a, b) => b.taux - a.taux)
                                .map((r) => (
                                    <tr key={r.taux} style={{ borderBottom: '1px solid var(--border-color)' }}>
                                        <Td>
                                            <span style={{
                                                display: 'inline-block',
                                                background: r.taux === 0 ? '#f3f4f6' : '#eff6ff',
                                                color: r.taux === 0 ? 'var(--text-secondary)' : 'var(--accent-primary)',
                                                fontWeight: 700,
                                                fontSize: '0.75rem',
                                                padding: '0.1rem 0.4rem',
                                                borderRadius: 'var(--radius-sm)',
                                                minWidth: '2.5rem',
                                                textAlign: 'center',
                                            }}>
                                                {r.taux}%
                                            </span>
                                        </Td>
                                        <Td right>{formatMoney(r.ht)}</Td>
                                        <Td right>{formatMoney(r.tva)}</Td>
                                        <Td right>{formatMoney(r.ttc)}</Td>
                                    </tr>
                                ))}
                        </tbody>
                    </table>
                </Section>

                {/* ③ Anomalies 🔴 Bloquantes */}
                <Section
                    icon={<XCircle size={14} style={{ color: '#b91c1c' }} />}
                    title="Anomalies bloquantes 🔴"
                    open={bloquantsOpen}
                    onToggle={() => setBloquantsOpen(!bloquantsOpen)}
                    badge={bloquants.length}
                    badgeStyle={{ background: '#fee2e2', color: '#b91c1c' }}
                    emptyMessage={bloquants.length === 0 ? 'Aucune anomalie bloquante ✔' : undefined}
                    emptyStyle={{ color: '#15803d' }}
                >
                    {bloquants.length > 0 && (
                        <div style={{ display: 'flex', flexDirection: 'column', gap: 0 }}>
                            {bloquants.map((a, i) => (
                                <AnomalieRow
                                    key={i}
                                    alerte={a}
                                    onDrill={(domaine, filtre, label) =>
                                        setDrillFiltre({ domaine, filtre, label })
                                    }
                                    isLast={i === bloquants.length - 1}
                                />
                            ))}
                        </div>
                    )}
                </Section>

                {/* ④ Anomalies 🟠 Avertissements */}
                <Section
                    icon={<AlertTriangle size={14} style={{ color: '#c2410c' }} />}
                    title="Avertissements 🟠"
                    open={avertissementsOpen}
                    onToggle={() => setAvertissementsOpen(!avertissementsOpen)}
                    badge={avertissements.length}
                    badgeStyle={avertissements.length > 0 ? { background: '#fff7ed', color: '#c2410c' } : undefined}
                    emptyMessage={avertissements.length === 0 ? 'Aucun avertissement ✔' : undefined}
                    emptyStyle={{ color: '#15803d' }}
                >
                    {avertissements.length > 0 && (
                        <div style={{ display: 'flex', flexDirection: 'column', gap: 0 }}>
                            {avertissements.map((a, i) => (
                                <AnomalieRow
                                    key={i}
                                    alerte={a}
                                    onDrill={(domaine, filtre, label) =>
                                        setDrillFiltre({ domaine, filtre, label })
                                    }
                                    isLast={i === avertissements.length - 1}
                                />
                            ))}
                        </div>
                    )}
                </Section>

                {/* ⑤ Récap rapprochement (densité) */}
                <Section
                    icon={<ShieldCheck size={14} />}
                    title="Récap lignes"
                    open={false}
                    onToggle={() => {}}
                    badge={data.reconciliation.candidates}
                    collapsedSummary={
                        <span style={{ fontSize: '0.75rem', color: 'var(--text-secondary)', fontStyle: 'italic' }}>
                            {data.reconciliation.integrees} intégrées ·{' '}
                            {data.reconciliation.ecartees} écartées ·{' '}
                            {data.reconciliation.proposees} proposées ·{' '}
                            {data.reconciliation.exclues} exclues
                        </span>
                    }
                >
                    <></>
                </Section>

            </div>
        </div>
    );
}

// ─── Ligne anomalie avec drill ─────────────────────────────────────────────────

function AnomalieRow({
    alerte,
    onDrill,
    isLast,
}: {
    alerte: CheckupAlerte;
    onDrill: (domaine: DomaineTVA, filtre: Record<string, any>, label: string) => void;
    isLast: boolean;
}) {
    const isBloquant = alerte.type === 'bloquant';
    const hasDrill = !!(alerte.filtre && Object.keys(alerte.filtre).length > 0);
    const domaine = (alerte.domaine as DomaineTVA) || 'Décaissement';

    return (
        <div style={{
            display: 'flex',
            alignItems: 'flex-start',
            justifyContent: 'space-between',
            padding: '0.45rem 0.65rem',
            borderBottom: isLast ? 'none' : '1px solid var(--border-color)',
            gap: '0.75rem',
            background: isBloquant ? '#fff8f8' : '#fffdf7',
        }}>
            <div style={{ display: 'flex', alignItems: 'flex-start', gap: '0.5rem', flex: 1, minWidth: 0 }}>
                {isBloquant
                    ? <XCircle size={13} style={{ color: '#b91c1c', flexShrink: 0, marginTop: '0.1rem' }} />
                    : <AlertTriangle size={13} style={{ color: '#c2410c', flexShrink: 0, marginTop: '0.1rem' }} />
                }
                <div style={{ flex: 1, minWidth: 0 }}>
                    <div style={{ fontSize: '0.8125rem', color: 'var(--text-primary)', lineHeight: 1.4 }}>
                        {alerte.message}
                    </div>
                    {alerte.domaine && (
                        <div style={{ fontSize: '0.7rem', color: 'var(--text-secondary)', marginTop: '0.1rem' }}>
                            Domaine : {alerte.domaine}
                        </div>
                    )}
                </div>
            </div>
            {hasDrill && (
                <button
                    title="Voir les lignes concernées"
                    onClick={() => onDrill(domaine, alerte.filtre!, alerte.message)}
                    style={{
                        display: 'inline-flex', alignItems: 'center', gap: '0.25rem',
                        padding: '0.2rem 0.5rem',
                        background: 'white',
                        border: `1px solid ${isBloquant ? '#fca5a5' : '#fed7aa'}`,
                        borderRadius: 'var(--radius-sm)',
                        cursor: 'pointer',
                        fontSize: '0.7rem',
                        fontWeight: 600,
                        color: isBloquant ? '#b91c1c' : '#c2410c',
                        whiteSpace: 'nowrap',
                        flexShrink: 0,
                    }}
                >
                    <ExternalLink size={11} /> Voir lignes
                </button>
            )}
        </div>
    );
}

// ─── Section repliable dense ───────────────────────────────────────────────────

function Section({
    icon, title, open, onToggle, badge, badgeStyle,
    emptyMessage, emptyStyle, statusIcon, collapsedSummary,
    children,
}: {
    icon?: React.ReactNode;
    title: string;
    open: boolean;
    onToggle: () => void;
    badge?: number;
    badgeStyle?: React.CSSProperties;
    emptyMessage?: string;
    emptyStyle?: React.CSSProperties;
    statusIcon?: React.ReactNode;
    collapsedSummary?: React.ReactNode;
    children: React.ReactNode;
}) {
    return (
        <div style={{
            background: 'white',
            border: '1px solid var(--border-color)',
            borderRadius: 'var(--radius-md)',
            overflow: 'hidden',
        }}>
            <button
                onClick={onToggle}
                style={{
                    display: 'flex', alignItems: 'center', gap: '0.5rem',
                    width: '100%', padding: '0.5rem 0.75rem',
                    background: 'var(--bg-tertiary)',
                    border: 'none', borderBottom: open ? '1px solid var(--border-color)' : 'none',
                    cursor: 'pointer', textAlign: 'left',
                }}
            >
                {open
                    ? <ChevronDown size={13} style={{ color: 'var(--text-secondary)', flexShrink: 0 }} />
                    : <ChevronRight size={13} style={{ color: 'var(--text-secondary)', flexShrink: 0 }} />
                }
                {icon}
                <span style={{ fontSize: '0.8125rem', fontWeight: 600, color: 'var(--text-primary)', flex: 1 }}>
                    {title}
                </span>
                {!open && collapsedSummary}
                {badge !== undefined && badge > 0 && (
                    <span style={{
                        fontSize: '0.65rem', fontWeight: 700,
                        padding: '0.1rem 0.4rem',
                        borderRadius: 'var(--radius-full)',
                        background: 'var(--bg-primary)',
                        border: '1px solid var(--border-color)',
                        color: 'var(--text-secondary)',
                        ...badgeStyle,
                    }}>
                        {badge}
                    </span>
                )}
                {statusIcon && <span>{statusIcon}</span>}
                <ArrowRight size={12} style={{ color: 'var(--text-tertiary)', transform: open ? 'rotate(90deg)' : 'none', transition: 'transform 150ms' }} />
            </button>
            {open && (
                emptyMessage
                    ? <div style={{ padding: '0.75rem', fontSize: '0.8125rem', fontStyle: 'italic', ...emptyStyle }}>{emptyMessage}</div>
                    : children
            )}
        </div>
    );
}

// ─── Helpers table dense ───────────────────────────────────────────────────────

function Th({ children, right }: { children: React.ReactNode; right?: boolean }) {
    return (
        <th style={{
            padding: '0.35rem 0.65rem',
            fontSize: '0.7rem', fontWeight: 700,
            textTransform: 'uppercase', letterSpacing: '0.04em',
            color: 'var(--text-secondary)',
            textAlign: right ? 'right' : 'left',
            background: 'var(--bg-tertiary)',
            borderBottom: '1px solid var(--border-color)',
        }}>
            {children}
        </th>
    );
}

function Td({ children, right }: { children: React.ReactNode; right?: boolean }) {
    return (
        <td style={{
            padding: '0.4rem 0.65rem',
            fontSize: '0.8125rem',
            textAlign: right ? 'right' : 'left',
            color: 'var(--text-primary)',
            whiteSpace: 'nowrap',
        }}>
            {children}
        </td>
    );
}
