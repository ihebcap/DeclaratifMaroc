import { useState, useEffect } from 'react';
import {
    Lock, CheckCircle2, XCircle, AlertTriangle, Loader2,
    ShieldCheck, FileStack, Calculator,
} from 'lucide-react';
import api from './api';
import { formatMoney } from './utils';

// ─── Écran ④ Intégration (TASK-057) ─────────────────────────────────────────
//
// Confirmation avant figeage de la déclaration (TASK-028 : pose du tampon
// DT_Id sur RT_AFFECTATION, triggers immuabilité). Le back est intouchable.
// Ce composant ne fait qu'appeler les endpoints existants :
//   - GET  /declarations/{id}/checkup  → check-list de contrôles
//   - POST /declarations/{id}/cloture  → figeage (TASK-012/017/028)
//
// Règles anti-régression :
//  - Source unique des montants = props transmises depuis ③ (aucun recalcul).
//  - Aucune écriture front hors l'appel POST /cloture.
//  - Un contrôle bloquant rouge → bouton désactivé (jamais bypass).
//  - Post-intégration : lecture seule pilotée par DeclarationStepper (statut).

// ─── Types ────────────────────────────────────────────────────────────────────

type AlertType = 'bloquant' | 'avertissement' | 'Error' | 'Warning' | 'Info';

interface Alerte {
    type: AlertType;
    message: string;
    ligneId?: string;
    domaine?: string;
}

interface Reconciliation {
    candidates: number;
    integrees: number;
    exclues: number;
    reportees: number;
    ecartees: number;
    proposees: number;
}

interface CheckupResult {
    // Bug corrigé (13/07/2026) : l'API expose le contrôle isValid/ecart réellement calculé
    // (TTC vs HT+TVA, tolérance 0,01) sous la clé `equilibre` (DeclarationsController.cs:194) —
    // `controleEquilibre` est l'objet C# brut (TotalMontantAffecte/TotalDeclareTtc/ResiduExplique),
    // sans `isValid` ni `ecart`. Le front lisait la mauvaise clé → badge BLOQUANT permanent.
    equilibre?: { isValid: boolean; ecart?: number };
    alertes: Alerte[];
    reconciliation: Reconciliation;
}

// Normalise les types retournés selon l'implémentation back (mock ou réel)
function isBloquant(a: Alerte): boolean {
    return a.type === 'bloquant' || a.type === 'Error';
}
function isAvertissement(a: Alerte): boolean {
    return a.type === 'avertissement' || a.type === 'Warning';
}

// ─── Composant principal ─────────────────────────────────────────────────────

export interface IntegrationPanelProps {
    declarationId: string;
    /** Nb lignes valorisées depuis ③ — source unique, pas de recalcul. */
    nbLignes: number;
    /** Total TVA à intégrer depuis ③ — source unique, pas de recalcul. */
    totalTVA: number;
    /** Nb règlements sélectionnés depuis ① */
    nbReglements: number;
    /** Déclaration déjà intégrée (statut ≠ 0) */
    integree: boolean;
    showToast: (m: string, t?: 'success' | 'error' | 'warning') => void;
    /** Appelé après une intégration réussie pour recharger l'état de la déclaration */
    onIntegrationSuccess: () => void;
}

export function IntegrationPanel({
    declarationId,
    nbLignes,
    totalTVA,
    nbReglements,
    integree,
    showToast,
    onIntegrationSuccess,
}: IntegrationPanelProps) {
    const [checkup, setCheckup] = useState<CheckupResult | null>(null);
    const [loadingCheckup, setLoadingCheckup] = useState(true);
    const [submitting, setSubmitting] = useState(false);
    const [confirmed, setConfirmed] = useState(false);

    useEffect(() => {
        let cancelled = false;
        (async () => {
            setLoadingCheckup(true);
            try {
                const res = await api.get(`/declarations/${declarationId}/checkup`);
                if (!cancelled) setCheckup(res.data as CheckupResult);
            } catch (e) {
                console.error(e);
                if (!cancelled) showToast('Erreur lors du chargement du checkup', 'error');
            } finally {
                if (!cancelled) setLoadingCheckup(false);
            }
        })();
        return () => { cancelled = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [declarationId]);

    const bloquants = checkup?.alertes.filter(isBloquant) ?? [];
    const avertissements = checkup?.alertes.filter(isAvertissement) ?? [];
    const hasBloquant = bloquants.length > 0;
    const canConfirm = !integree && !confirmed && !hasBloquant && !loadingCheckup && !submitting;

    // Contrôles structurés pour la check-list
    const controls = buildControls(checkup, nbLignes, nbReglements);

    const handleConfirm = async () => {
        if (!canConfirm) return;
        setSubmitting(true);
        try {
            await api.post(`/declarations/${declarationId}/cloture`);
            setConfirmed(true);
            showToast('Déclaration intégrée avec succès — tampon DT_Id posé', 'success');
            onIntegrationSuccess();
        } catch (err: any) {
            const msg = err?.response?.data?.message
                || err?.response?.data?.Message
                || 'Erreur lors de l\'intégration';
            showToast(msg, 'error');
        } finally {
            setSubmitting(false);
        }
    };

    const isReadOnly = integree || confirmed;

    return (
        <div style={{ flex: 1, display: 'flex', flexDirection: 'column', height: '100%', overflow: 'hidden', background: 'var(--bg-secondary)' }}>
            {/* En-tête */}
            <div style={{
                padding: '0.65rem 1rem',
                borderBottom: '1px solid var(--border-color)',
                background: 'white',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                flexWrap: 'wrap',
                gap: '0.5rem',
                flexShrink: 0,
            }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '0.6rem' }}>
                    <Lock size={20} style={{ color: isReadOnly ? '#15803d' : 'var(--accent-primary)' }} />
                    <div>
                        <h2 style={{ margin: 0, fontSize: '1.05rem', fontWeight: 600 }}>④ Intégration</h2>
                        <div style={{ fontSize: '0.72rem', color: 'var(--text-secondary)' }}>
                            {isReadOnly
                                ? 'Déclaration intégrée — tampon DT_Id posé · lignes exclues des prochaines recherches'
                                : 'Confirmation avant figeage · vérifiez les contrôles puis confirmez'}
                        </div>
                    </div>
                </div>
                {isReadOnly && (
                    <span style={{
                        display: 'inline-flex', alignItems: 'center', gap: '0.3rem',
                        fontSize: '0.75rem', fontWeight: 700,
                        padding: '0.25rem 0.65rem', borderRadius: 'var(--radius-full)',
                        background: '#dcfce7', color: '#15803d',
                        border: '1px solid #bbf7d0',
                    }}>
                        <CheckCircle2 size={13} /> Intégrée
                    </span>
                )}
                {loadingCheckup && !isReadOnly && (
                    <Loader2 size={15} className="animate-spin" style={{ color: 'var(--accent-primary)' }} />
                )}
            </div>

            {/* Corps scrollable */}
            <div style={{ flex: 1, overflow: 'auto', padding: '0.75rem', display: 'flex', flexDirection: 'column', gap: '0.65rem' }}>

                {/* Récap dense — chiffres repris de ③, sans recalcul */}
                <RecapCard
                    nbLignes={nbLignes}
                    totalTVA={totalTVA}
                    nbReglements={nbReglements}
                    reconciliation={checkup?.reconciliation ?? null}
                    isReadOnly={isReadOnly}
                />

                {/* Check-list de contrôles */}
                <ChecklistCard
                    controls={controls}
                    loading={loadingCheckup}
                    bloquants={bloquants}
                    avertissements={avertissements}
                />

                {/* État post-intégration */}
                {isReadOnly && (
                    <div style={{
                        background: '#f0fdf4',
                        border: '1px solid #bbf7d0',
                        borderRadius: '8px',
                        padding: '0.75rem 1rem',
                        display: 'flex',
                        alignItems: 'center',
                        gap: '0.6rem',
                        fontSize: '0.82rem',
                        color: '#15803d',
                        fontWeight: 500,
                    }}>
                        <ShieldCheck size={16} />
                        Déclaration intégrée — lignes rattachées et exclues des prochaines sélections. Lecture seule.
                    </div>
                )}
            </div>

            {/* Pied — CTA intégration ou statut lecture seule */}
            <div style={{
                flexShrink: 0,
                borderTop: '2px solid var(--border-color)',
                background: 'white',
                padding: '0.75rem 1rem',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                gap: '1rem',
            }}>
                {/* Info bloquants à gauche */}
                <div style={{ fontSize: '0.8rem', color: 'var(--text-secondary)' }}>
                    {isReadOnly ? (
                        <span style={{ color: '#15803d', fontWeight: 600 }}>
                            ✓ Intégration confirmée
                        </span>
                    ) : hasBloquant ? (
                        <span style={{ display: 'inline-flex', alignItems: 'center', gap: '0.35rem', color: '#b91c1c', fontWeight: 600 }}>
                            <XCircle size={14} />
                            {bloquants.length} contrôle{bloquants.length > 1 ? 's' : ''} bloquant{bloquants.length > 1 ? 's' : ''} — intégration impossible
                        </span>
                    ) : !loadingCheckup ? (
                        <span style={{ display: 'inline-flex', alignItems: 'center', gap: '0.35rem', color: '#15803d' }}>
                            <CheckCircle2 size={14} />
                            Tous les contrôles sont passés
                        </span>
                    ) : null}
                </div>

                {/* Bouton CTA */}
                {!isReadOnly && (
                    <button
                        id="btn-confirmer-integration"
                        onClick={handleConfirm}
                        disabled={!canConfirm}
                        style={{
                            display: 'inline-flex', alignItems: 'center', gap: '0.5rem',
                            padding: '0.55rem 1.2rem',
                            borderRadius: '5px',
                            border: 'none',
                            fontWeight: 700,
                            fontSize: '0.875rem',
                            cursor: canConfirm ? 'pointer' : 'not-allowed',
                            background: canConfirm
                                ? 'var(--accent-primary)'
                                : 'var(--bg-tertiary)',
                            color: canConfirm ? 'white' : 'var(--text-secondary)',
                            transition: 'background 0.15s, opacity 0.15s',
                            opacity: canConfirm ? 1 : 0.65,
                        }}
                        title={hasBloquant ? 'Des contrôles bloquants empêchent l\'intégration' : 'Confirmer et figer la déclaration'}
                    >
                        {submitting ? (
                            <><Loader2 size={14} className="animate-spin" /> Intégration en cours…</>
                        ) : (
                            <><Lock size={14} /> Confirmer intégration</>
                        )}
                    </button>
                )}
            </div>
        </div>
    );
}

// ─── Récap dense ─────────────────────────────────────────────────────────────

function RecapCard({
    nbLignes, totalTVA, nbReglements, reconciliation, isReadOnly,
}: {
    nbLignes: number;
    totalTVA: number;
    nbReglements: number;
    reconciliation: Reconciliation | null;
    isReadOnly: boolean;
}) {
    return (
        <div style={{
            background: 'white',
            border: '1px solid var(--border-color)',
            borderRadius: '8px',
            overflow: 'hidden',
        }}>
            <div style={{
                padding: '0.45rem 0.9rem',
                background: 'var(--bg-secondary)',
                borderBottom: '1px solid var(--border-color)',
                fontSize: '0.75rem', fontWeight: 600, color: 'var(--text-secondary)',
                display: 'flex', alignItems: 'center', gap: '0.4rem',
            }}>
                <FileStack size={13} /> Récapitulatif de l'intégration
            </div>
            <div style={{
                display: 'grid',
                gridTemplateColumns: 'repeat(auto-fit, minmax(160px, 1fr))',
                gap: 0,
            }}>
                <RecapCell
                    label="Règlements sélectionnés"
                    value={nbReglements}
                    unit="règlement(s)"
                />
                <RecapCell
                    label="Lignes valorisées"
                    value={nbLignes}
                    unit="ligne(s)"
                    bordered
                />
                <RecapCell
                    label="Total TVA à intégrer"
                    value={formatMoney(totalTVA)}
                    accent
                    bordered
                />
                {reconciliation && (
                    <RecapCell
                        label="Lignes intégrées (back)"
                        value={reconciliation.integrees}
                        unit="ligne(s)"
                        accent={isReadOnly}
                        bordered
                    />
                )}
            </div>
        </div>
    );
}

function RecapCell({
    label, value, unit, accent, bordered,
}: {
    label: string;
    value: number | string;
    unit?: string;
    accent?: boolean;
    bordered?: boolean;
}) {
    return (
        <div style={{
            padding: '0.6rem 1rem',
            borderLeft: bordered ? '1px solid var(--border-color)' : undefined,
        }}>
            <div style={{ fontSize: '0.72rem', color: 'var(--text-secondary)', marginBottom: '0.15rem' }}>{label}</div>
            <div style={{
                fontSize: typeof value === 'string' ? '1.1rem' : '1.3rem',
                fontWeight: 800,
                fontVariantNumeric: 'tabular-nums',
                color: accent ? 'var(--accent-primary)' : 'var(--text-primary)',
                letterSpacing: '-0.01em',
                lineHeight: 1.1,
            }}>
                {value}
                {unit && <span style={{ fontSize: '0.72rem', fontWeight: 500, color: 'var(--text-secondary)', marginLeft: '0.3rem' }}>{unit}</span>}
            </div>
        </div>
    );
}

// ─── Check-list ───────────────────────────────────────────────────────────────

interface Control {
    id: string;
    label: string;
    description: string;
    status: 'ok' | 'error' | 'warning' | 'pending';
}

function buildControls(checkup: CheckupResult | null, nbLignes: number, nbReglements: number): Control[] {
    if (!checkup) {
        return [
            { id: 'reglements', label: 'Règlements sélectionnés', description: 'Vérification de la sélection', status: 'pending' },
            { id: 'lignes', label: 'Lignes valorisées', description: 'Valorisation par le back', status: 'pending' },
            { id: 'equilibre', label: 'Équilibre comptable', description: 'Contrôle d\'équilibre des montants', status: 'pending' },
            { id: 'bloquants', label: 'Absence d\'anomalies bloquantes', description: 'Aucune alerte bloquante', status: 'pending' },
        ];
    }

    const bloquants = checkup.alertes.filter(isBloquant);
    const equilibre = checkup.equilibre;

    return [
        {
            id: 'reglements',
            label: 'Règlements sélectionnés',
            description: `${nbReglements} règlement${nbReglements > 1 ? 's' : ''} en entrée`,
            status: nbReglements > 0 ? 'ok' : 'error',
        },
        {
            id: 'lignes',
            label: 'Lignes valorisées présentes',
            description: nbLignes > 0
                ? `${nbLignes} ligne${nbLignes > 1 ? 's' : ''} valorisée${nbLignes > 1 ? 's' : ''} prêtes à intégrer`
                : 'Aucune ligne valorisée — retournez à l\'étape ③',
            status: nbLignes > 0 ? 'ok' : 'error',
        },
        {
            id: 'affectations',
            label: 'Affectations valides',
            description: checkup.reconciliation.integrees > 0 || checkup.reconciliation.proposees > 0
                ? `${checkup.reconciliation.integrees + checkup.reconciliation.proposees} affectation(s) en attente ou intégrée(s)`
                : 'Aucune affectation trouvée',
            status: checkup.reconciliation.integrees > 0 || checkup.reconciliation.proposees > 0 ? 'ok' : 'warning',
        },
        {
            id: 'equilibre',
            label: 'Équilibre comptable',
            description: equilibre
                ? (equilibre.isValid ? 'Équilibre validé — aucun écart' : `Écart détecté : ${formatMoney(equilibre.ecart ?? 0)}`)
                : 'Non vérifié',
            status: equilibre ? (equilibre.isValid ? 'ok' : 'error') : 'warning',
        },
        {
            id: 'bloquants',
            label: 'Absence d\'anomalies bloquantes',
            description: bloquants.length === 0
                ? 'Aucune anomalie bloquante détectée'
                : `${bloquants.length} anomalie${bloquants.length > 1 ? 's' : ''} bloquante${bloquants.length > 1 ? 's' : ''} — intégration impossible`,
            status: bloquants.length === 0 ? 'ok' : 'error',
        },
    ];
}

function ChecklistCard({
    controls, loading, bloquants, avertissements,
}: {
    controls: Control[];
    loading: boolean;
    bloquants: Alerte[];
    avertissements: Alerte[];
}) {
    return (
        <div style={{
            background: 'white',
            border: '1px solid var(--border-color)',
            borderRadius: '8px',
            overflow: 'hidden',
        }}>
            <div style={{
                padding: '0.45rem 0.9rem',
                background: 'var(--bg-secondary)',
                borderBottom: '1px solid var(--border-color)',
                fontSize: '0.75rem', fontWeight: 600, color: 'var(--text-secondary)',
                display: 'flex', alignItems: 'center', gap: '0.4rem',
            }}>
                <Calculator size={13} /> Contrôles pré-intégration
                {loading && <Loader2 size={12} className="animate-spin" style={{ marginLeft: 4 }} />}
            </div>

            <div>
                {controls.map((c, i) => (
                    <div
                        key={c.id}
                        style={{
                            display: 'flex',
                            alignItems: 'flex-start',
                            gap: '0.65rem',
                            padding: '0.55rem 1rem',
                            borderTop: i > 0 ? '1px solid var(--border-color)' : undefined,
                            background: c.status === 'error' ? 'rgba(239,68,68,0.03)' : 'white',
                        }}
                    >
                        <ControlIcon status={c.status} />
                        <div style={{ flex: 1, minWidth: 0 }}>
                            <div style={{
                                fontSize: '0.82rem',
                                fontWeight: 600,
                                color: c.status === 'error' ? '#b91c1c' : c.status === 'warning' ? '#b45309' : 'var(--text-primary)',
                            }}>
                                {c.label}
                            </div>
                            <div style={{ fontSize: '0.75rem', color: 'var(--text-secondary)', marginTop: '0.1rem' }}>
                                {c.description}
                            </div>
                        </div>
                        <ControlBadge status={c.status} />
                    </div>
                ))}
            </div>

            {/* Détail des alertes bloquantes */}
            {bloquants.length > 0 && (
                <div style={{
                    borderTop: '2px solid #fee2e2',
                    background: '#fff5f5',
                    padding: '0.55rem 1rem',
                }}>
                    <div style={{ fontSize: '0.74rem', fontWeight: 700, color: '#b91c1c', marginBottom: '0.3rem', display: 'flex', alignItems: 'center', gap: '0.3rem' }}>
                        <XCircle size={12} /> Anomalies bloquantes
                    </div>
                    {bloquants.map((a, i) => (
                        <div key={i} style={{ fontSize: '0.74rem', color: '#7f1d1d', marginTop: '0.2rem', paddingLeft: '1rem' }}>
                            • {a.message}
                        </div>
                    ))}
                </div>
            )}

            {/* Avertissements non bloquants */}
            {avertissements.length > 0 && (
                <div style={{
                    borderTop: '1px solid #fde68a',
                    background: '#fffbeb',
                    padding: '0.55rem 1rem',
                }}>
                    <div style={{ fontSize: '0.74rem', fontWeight: 700, color: '#92400e', marginBottom: '0.3rem', display: 'flex', alignItems: 'center', gap: '0.3rem' }}>
                        <AlertTriangle size={12} /> Avertissements (non bloquants)
                    </div>
                    {avertissements.map((a, i) => (
                        <div key={i} style={{ fontSize: '0.74rem', color: '#78350f', marginTop: '0.2rem', paddingLeft: '1rem' }}>
                            • {a.message}
                        </div>
                    ))}
                </div>
            )}
        </div>
    );
}

function ControlIcon({ status }: { status: Control['status'] }) {
    const size = 16;
    if (status === 'ok') return <CheckCircle2 size={size} style={{ color: '#15803d', flexShrink: 0, marginTop: 1 }} />;
    if (status === 'error') return <XCircle size={size} style={{ color: '#b91c1c', flexShrink: 0, marginTop: 1 }} />;
    if (status === 'warning') return <AlertTriangle size={size} style={{ color: '#b45309', flexShrink: 0, marginTop: 1 }} />;
    return <Loader2 size={size} className="animate-spin" style={{ color: 'var(--text-secondary)', flexShrink: 0, marginTop: 1 }} />;
}

function ControlBadge({ status }: { status: Control['status'] }) {
    const map = {
        ok:      { label: 'OK',        bg: '#dcfce7', color: '#15803d' },
        error:   { label: 'BLOQUANT',  bg: '#fee2e2', color: '#b91c1c' },
        warning: { label: 'ATTENTION', bg: '#fef9c3', color: '#92400e' },
        pending: { label: '…',         bg: 'var(--bg-tertiary)', color: 'var(--text-secondary)' },
    } as const;
    const m = map[status];
    return (
        <span style={{
            flexShrink: 0,
            padding: '1px 7px',
            borderRadius: 'var(--radius-full)',
            fontSize: '0.68rem',
            fontWeight: 700,
            background: m.bg,
            color: m.color,
            letterSpacing: '0.04em',
        }}>
            {m.label}
        </span>
    );
}
