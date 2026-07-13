import { useState, useEffect } from 'react';
import {
    FileOutput, Loader2, CheckCircle2, XCircle, Lock,
    FileSpreadsheet, FileCode2, ArrowLeft,
} from 'lucide-react';
import api from './api';
import { formatMoney } from './utils';

// ─── Écran ⑥ Synthèse & Export (TASK-059) ────────────────────────────────────
//
// Dernière étape du tunnel (TASK-053). Assemble des briques déjà livrées :
//   • synthèse dense issue du contrôle ⑤ — source unique GET /checkup (back TASK-009),
//     aucun recalcul front ;
//   • Export Excel (TASK-010) / Générer XML Simpl-TVA (TASK-011) via POST /generation ;
//   • Clôture gardée (TASK-012) via POST /cloture — bloquée tant qu'il reste des 🔴
//     ou que le contrôle n'est pas terminé.
// Aucun état factice : les erreurs back remontent honnêtement en toast.
// Densité 0 espace perdu (même vocabulaire visuel que l'écran ⑤).

// StatutDeclaration (back, WorkflowEntities.cs) : 0=EnCours, 1=Cloturee, 2=Generee, 3=Deposee.
function isCloturee(statut: number) {
    return statut !== 0;
}

interface CheckupData {
    recapSource: { source: string; ht: number; tva: number; ttc: number }[];
    recapTaux: { taux: number; ht: number; tva: number; ttc: number }[];
    alertes: { type: 'bloquant' | 'avertissement'; message: string }[];
    reconciliation: { integrees: number };
    equilibre?: { isValid: boolean; ecart?: number };
}

interface Fichiers {
    xmlDecaissement?: string;
    excelCheckup?: string;
    rapportAnomalies?: string;
    [k: string]: string | undefined;
}

export interface SynthesePanelProps {
    declarationId: string;
    /** Statut courant de la déclaration (parent info.statut). */
    statut: number;
    showToast: (m: string, t?: 'success' | 'error' | 'warning') => void;
    /** Rafraîchit la déclaration parente après clôture (verrou posé). */
    onClotured: () => void;
    onBack: () => void;
}

export function SynthesePanel({ declarationId, statut, showToast, onClotured, onBack }: SynthesePanelProps) {
    const [data, setData] = useState<CheckupData | null>(null);
    const [loading, setLoading] = useState(true);
    const [generating, setGenerating] = useState(false);
    const [fichiers, setFichiers] = useState<Fichiers | null>(null);
    const [cloturing, setCloturing] = useState(false);

    useEffect(() => {
        let cancelled = false;
        (async () => {
            setLoading(true);
            try {
                const res = await api.get(`/declarations/${declarationId}/checkup`);
                if (!cancelled) setData(res.data as CheckupData);
            } catch (e) {
                console.error(e);
                if (!cancelled) showToast('Erreur lors du chargement de la synthèse', 'error');
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

    const nbLignes = data.reconciliation.integrees;
    const totalTVA = data.recapSource.reduce((s, r) => s + r.tva, 0);
    const totalHT = data.recapSource.reduce((s, r) => s + r.ht, 0);
    const totalTTC = data.recapSource.reduce((s, r) => s + r.ttc, 0);
    const hasBloquants = data.alertes.some((a) => a.type === 'bloquant');
    const controleTermine = !hasBloquants;
    const cloturee = isCloturee(statut);

    // Garde clôture / export : bloqués tant que le contrôle n'est pas terminé (🔴).
    const exportDisabled = hasBloquants || generating;
    const clotureDisabled = hasBloquants || cloturee || cloturing;

    // Déclenche la génération (idempotente) puis télécharge le fichier demandé.
    const genererEtTelecharger = async (type: 'excel' | 'xml') => {
        setGenerating(true);
        try {
            let f = fichiers;
            if (!f) {
                const res = await api.post(`/declarations/${declarationId}/generation`);
                f = res.data.fichiers as Fichiers;
                setFichiers(f);
            }
            const url = type === 'excel' ? f?.excelCheckup : f?.xmlDecaissement;
            if (!url) {
                showToast('Fichier indisponible dans la réponse de génération', 'error');
                return;
            }
            await telecharger(url, type === 'excel' ? 'Checkup.xlsx' : 'XML_Simpl-TVA.zip');
            showToast(type === 'excel' ? 'Export Excel généré' : 'XML Simpl-TVA généré', 'success');
        } catch (e: any) {
            console.error(e);
            const msg = e?.response?.data?.Message || e?.response?.data?.message
                || (e?.response?.status === 501 ? 'Génération non disponible (endpoint back non câblé)' : 'Erreur lors de la génération');
            showToast(msg, 'error');
        } finally {
            setGenerating(false);
        }
    };

    // Téléchargement réel via blob (aucun leurre : si l'URL échoue, l'erreur remonte).
    const telecharger = async (url: string, filename: string) => {
        const res = await api.get(url, { responseType: 'blob' });
        const blobUrl = URL.createObjectURL(res.data);
        const a = document.createElement('a');
        a.href = blobUrl;
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        a.remove();
        URL.revokeObjectURL(blobUrl);
    };

    const cloturer = async () => {
        setCloturing(true);
        try {
            await api.post(`/declarations/${declarationId}/cloture`);
            showToast('Déclaration clôturée — verrou posé', 'success');
            onClotured();
        } catch (e: any) {
            console.error(e);
            const msg = e?.response?.data?.Message || e?.response?.data?.message || 'Erreur lors de la clôture';
            showToast(msg, 'error');
        } finally {
            setCloturing(false);
        }
    };

    return (
        <div style={{ display: 'flex', flexDirection: 'column', height: '100%', overflow: 'hidden', background: 'var(--bg-secondary)' }}>

            {/* En-tête */}
            <div style={{
                padding: '0.6rem 1rem', background: 'white',
                borderBottom: '1px solid var(--border-color)',
                display: 'flex', alignItems: 'center', gap: '0.6rem', flexShrink: 0,
            }}>
                <FileOutput size={18} style={{ color: 'var(--accent-primary)' }} />
                <div>
                    <h2 style={{ margin: 0, fontSize: '1rem', fontWeight: 600 }}>⑥ Synthèse &amp; Export</h2>
                    <div style={{ fontSize: '0.72rem', color: 'var(--text-secondary)' }}>
                        Récap déclaration · Excel (TASK-010) · XML Simpl-TVA (TASK-011) · clôture gardée (TASK-012)
                    </div>
                </div>
                <div style={{ marginLeft: 'auto' }}>
                    {cloturee ? (
                        <span style={{
                            display: 'inline-flex', alignItems: 'center', gap: '0.3rem',
                            fontSize: '0.75rem', fontWeight: 700, padding: '0.25rem 0.65rem',
                            borderRadius: 'var(--radius-full)', background: '#dcfce7', color: '#15803d',
                            border: '1px solid #bbf7d0',
                        }}>
                            <Lock size={13} /> Verrou posé
                        </span>
                    ) : controleTermine ? (
                        <span style={{
                            display: 'inline-flex', alignItems: 'center', gap: '0.3rem',
                            fontSize: '0.75rem', fontWeight: 700, padding: '0.25rem 0.65rem',
                            borderRadius: 'var(--radius-full)', background: '#dcfce7', color: '#15803d',
                            border: '1px solid #bbf7d0',
                        }}>
                            <CheckCircle2 size={13} /> Contrôle terminé
                        </span>
                    ) : (
                        <span style={{
                            display: 'inline-flex', alignItems: 'center', gap: '0.3rem',
                            fontSize: '0.75rem', fontWeight: 700, padding: '0.25rem 0.65rem',
                            borderRadius: 'var(--radius-full)', background: '#fee2e2', color: '#b91c1c',
                            border: '1px solid #fca5a5',
                        }}>
                            <XCircle size={13} /> Contrôle non terminé
                        </span>
                    )}
                </div>
            </div>

            {/* Garde-fou bloquant */}
            {hasBloquants && (
                <div style={{
                    padding: '0.5rem 1rem', background: '#fee2e2', borderBottom: '1px solid #fca5a5',
                    color: '#b91c1c', fontSize: '0.8125rem', fontWeight: 600, flexShrink: 0,
                    display: 'flex', alignItems: 'center', gap: '0.5rem',
                }}>
                    <XCircle size={14} />
                    Export et clôture bloqués — corrigez les anomalies 🔴 au contrôle ⑤ avant de finaliser.
                </div>
            )}

            {/* Corps */}
            <div style={{ flex: 1, overflow: 'auto', padding: '0.65rem', display: 'flex', flexDirection: 'column', gap: '0.65rem' }}>

                {/* Bandeau synthèse dense */}
                <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
                    <Stat label="Lignes intégrées" value={String(nbLignes)} />
                    <Stat label="TVA totale" value={formatMoney(totalTVA)} accent />
                    <Stat label="Total HT" value={formatMoney(totalHT)} />
                    <Stat label="Total TTC" value={formatMoney(totalTTC)} />
                    {data.equilibre && (
                        <Stat
                            label="Écart équilibre"
                            value={formatMoney(data.equilibre.ecart ?? 0)}
                            ok={data.equilibre.isValid}
                        />
                    )}
                </div>

                {/* Récap par taux (dense, lecture seule) */}
                <div style={{ background: 'white', border: '1px solid var(--border-color)', borderRadius: 'var(--radius-md)', overflow: 'hidden' }}>
                    <div style={{ padding: '0.45rem 0.75rem', background: 'var(--bg-tertiary)', borderBottom: '1px solid var(--border-color)', fontSize: '0.8125rem', fontWeight: 600 }}>
                        Répartition par taux de TVA
                    </div>
                    <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.8125rem' }}>
                        <thead>
                            <tr>
                                <Th>Taux</Th><Th right>HT</Th><Th right>TVA</Th><Th right>TTC</Th>
                            </tr>
                        </thead>
                        <tbody>
                            {data.recapTaux.slice().sort((a, b) => b.taux - a.taux).map((r) => (
                                <tr key={r.taux} style={{ borderBottom: '1px solid var(--border-color)' }}>
                                    <Td>{r.taux}%</Td>
                                    <Td right>{formatMoney(r.ht)}</Td>
                                    <Td right>{formatMoney(r.tva)}</Td>
                                    <Td right>{formatMoney(r.ttc)}</Td>
                                </tr>
                            ))}
                        </tbody>
                        <tfoot>
                            <tr style={{ background: 'var(--bg-tertiary)', fontWeight: 700 }}>
                                <Td>Total</Td>
                                <Td right>{formatMoney(totalHT)}</Td>
                                <Td right>{formatMoney(totalTVA)}</Td>
                                <Td right>{formatMoney(totalTTC)}</Td>
                            </tr>
                        </tfoot>
                    </table>
                </div>
            </div>

            {/* Barre d'actions (Export Excel / XML / Clôturer) */}
            <div style={{
                display: 'flex', alignItems: 'center', gap: '0.6rem', flexWrap: 'wrap',
                padding: '0.65rem 1rem', background: 'white', borderTop: '1px solid var(--border-color)', flexShrink: 0,
            }}>
                <button onClick={onBack} style={backBtnStyle}>
                    <ArrowLeft size={15} /> Retour
                </button>

                <div style={{ marginLeft: 'auto', display: 'flex', alignItems: 'center', gap: '0.6rem', flexWrap: 'wrap' }}>
                    <button
                        onClick={() => genererEtTelecharger('excel')}
                        disabled={exportDisabled}
                        title={hasBloquants ? 'Corrigez les anomalies 🔴 avant l’export' : 'Export Excel de contrôle (TASK-010)'}
                        style={secondaryBtnStyle(exportDisabled)}
                    >
                        {generating ? <Loader2 size={15} className="animate-spin" /> : <FileSpreadsheet size={15} />} Export Excel
                    </button>

                    <button
                        onClick={() => genererEtTelecharger('xml')}
                        disabled={exportDisabled}
                        title={hasBloquants ? 'Corrigez les anomalies 🔴 avant l’export' : 'Générer le XML Simpl-TVA (TASK-011)'}
                        style={secondaryBtnStyle(exportDisabled)}
                    >
                        {generating ? <Loader2 size={15} className="animate-spin" /> : <FileCode2 size={15} />} Générer XML
                    </button>

                    <button
                        onClick={cloturer}
                        disabled={clotureDisabled}
                        title={cloturee ? 'Déclaration déjà clôturée' : hasBloquants ? 'Corrigez les anomalies 🔴 avant de clôturer' : 'Clôturer et poser le verrou (TASK-012)'}
                        style={primaryBtnStyle(clotureDisabled)}
                    >
                        {cloturing ? <Loader2 size={15} className="animate-spin" /> : cloturee ? <CheckCircle2 size={15} /> : <Lock size={15} />}
                        {cloturee ? 'Déclaration clôturée' : 'Clôturer'}
                    </button>
                </div>
            </div>
        </div>
    );
}

// ─── Sous-composants denses ─────────────────────────────────────────────────────

function Stat({ label, value, accent, ok }: { label: string; value: string; accent?: boolean; ok?: boolean }) {
    const color = ok === false ? '#b91c1c' : ok === true ? '#15803d' : accent ? 'var(--accent-primary)' : 'var(--text-primary)';
    return (
        <div style={{
            flex: '1 1 8rem', minWidth: '8rem',
            background: 'white', border: '1px solid var(--border-color)',
            borderRadius: 'var(--radius-md)', padding: '0.5rem 0.75rem',
        }}>
            <div style={{ fontSize: '0.68rem', color: 'var(--text-secondary)', textTransform: 'uppercase', letterSpacing: '0.04em', fontWeight: 600 }}>{label}</div>
            <div style={{ fontSize: '1.05rem', fontWeight: 700, color, marginTop: '0.15rem' }}>{value}</div>
        </div>
    );
}

function Th({ children, right }: { children: React.ReactNode; right?: boolean }) {
    return (
        <th style={{
            padding: '0.35rem 0.65rem', fontSize: '0.7rem', fontWeight: 700,
            textTransform: 'uppercase', letterSpacing: '0.04em', color: 'var(--text-secondary)',
            textAlign: right ? 'right' : 'left', background: 'var(--bg-tertiary)',
            borderBottom: '1px solid var(--border-color)',
        }}>{children}</th>
    );
}

function Td({ children, right }: { children: React.ReactNode; right?: boolean }) {
    return (
        <td style={{
            padding: '0.4rem 0.65rem', fontSize: '0.8125rem',
            textAlign: right ? 'right' : 'left', color: 'var(--text-primary)', whiteSpace: 'nowrap',
        }}>{children}</td>
    );
}

const backBtnStyle: React.CSSProperties = {
    display: 'inline-flex', alignItems: 'center', gap: '0.4rem',
    background: 'transparent', border: '1px solid var(--border-color)',
    borderRadius: 'var(--radius-md)', padding: '0.45rem 0.8rem',
    cursor: 'pointer', fontSize: '0.8125rem', fontWeight: 500, color: 'var(--text-secondary)',
};

function secondaryBtnStyle(disabled: boolean): React.CSSProperties {
    return {
        display: 'inline-flex', alignItems: 'center', gap: '0.4rem',
        background: 'white', border: '1px solid var(--border-color)',
        borderRadius: 'var(--radius-md)', padding: '0.45rem 0.85rem',
        cursor: disabled ? 'not-allowed' : 'pointer', opacity: disabled ? 0.5 : 1,
        fontSize: '0.8125rem', fontWeight: 600, color: 'var(--text-primary)',
    };
}

function primaryBtnStyle(disabled: boolean): React.CSSProperties {
    return {
        display: 'inline-flex', alignItems: 'center', gap: '0.45rem',
        background: disabled ? '#9ca3af' : 'var(--accent-primary)', color: 'white', border: 'none',
        borderRadius: 'var(--radius-md)', padding: '0.5rem 1.1rem',
        cursor: disabled ? 'not-allowed' : 'pointer', opacity: disabled ? 0.7 : 1,
        fontSize: '0.875rem', fontWeight: 700,
    };
}
