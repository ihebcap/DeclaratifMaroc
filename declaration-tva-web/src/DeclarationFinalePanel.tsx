import { useState, useEffect, useMemo } from 'react';
import {
    ShieldCheck, Loader2, CheckCircle2, XCircle, Lock,
    AlertTriangle, BarChart2, Percent, ArrowRight,
    ChevronDown, ChevronRight, ExternalLink,
    FileSpreadsheet, FileCode2, ArrowLeft,
} from 'lucide-react';
import api from './api';
import { formatMoney } from './utils';
import { DomainGrid } from './DomainGrid';
import type { DomaineTVA } from './DeclarationStepper';
import { RecapSourceTable } from './RecapSourceTable';

// ─── Écran ④ Déclaration (Fusion de ⑤ et ⑥) ─────────────────────────────────
//
// Vue finale fusionnée post-intégration : statistiques globales, répartition
// par source & par taux, anomalies typées, drill « Voir lignes » readonly, et
// exports (Excel & XML Simpl-TVA).
// Source unique : GET /declarations/{id}/checkup (back TASK-009).
// Aucun recalcul front — lecture seule.

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
    codeTaxe?: string;
    ht: number;
    tva: number;
    ttc: number;
    nbLignes?: number;
    domaine?: string;
}

interface RecapActiviteLigne {
    codeActivite: string;
    domaine: string;
    ht: number;
    tva: number;
    ttc: number;
}

interface CheckupData {
    recapSource: (RecapLigne & { source: string })[];
    recapTaux: (RecapLigne & { taux: number })[];
    recapActivite: RecapActiviteLigne[];
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

interface Fichiers {
    xmlDecaissement?: string;
    excelCheckup?: string;
    rapportAnomalies?: string;
    [k: string]: string | undefined;
}



// ─── Props ────────────────────────────────────────────────────────────────────

export interface DeclarationFinalePanelProps {
    declarationId: string;
    statut: number;
    showToast: (m: string, t?: 'success' | 'error' | 'warning') => void;
    onBack: () => void;
}

// ─── Composant principal ─────────────────────────────────────────────────────

export function DeclarationFinalePanel({
    declarationId,
    statut,
    showToast,
    onBack,
}: DeclarationFinalePanelProps) {
    const [data, setData] = useState<CheckupData | null>(null);
    const [loading, setLoading] = useState(true);
    const [generating, setGenerating] = useState(false);
    const [fichiers, setFichiers] = useState<Fichiers | null>(null);

    // Drill anomalie → grille filtrée
    const [drillFiltre, setDrillFiltre] = useState<{
        domaine: DomaineTVA;
        filtre: Record<string, any>;
        label: string;
    } | null>(null);

    // Sections repliables
    const [sourceOpen, setSourceOpen] = useState(true);
    const [tauxOpen, setTauxOpen] = useState(true);
    const [activiteOpen, setActiviteOpen] = useState(true);
    const [bloquantsOpen, setBloquantsOpen] = useState(true);
    const [avertissementsOpen, setAvertissementsOpen] = useState(true);
    const [selectedTab, setSelectedTab] = useState<'Decaissement' | 'Encaissement'>('Decaissement');

    useEffect(() => {
        let cancelled = false;
        (async () => {
            setLoading(true);
            try {
                const res = await api.get(`/declarations/${declarationId}/checkup`);
                if (!cancelled) {
                    setData(res.data as CheckupData);
                }
            } catch (e) {
                console.error(e);
                if (!cancelled) showToast('Erreur lors du chargement de la déclaration', 'error');
            } finally {
                if (!cancelled) setLoading(false);
            }
        })();
        return () => { cancelled = true; };
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [declarationId]);

    const sourceBelongsToDomain = (source: string, domain: 'Decaissement' | 'Encaissement'): boolean => {
        if (domain === 'Encaissement') {
            return source === 'Encaissement' || source === 'VENTE';
        } else {
            return source === 'Decaissement' || source === 'Espece' || source === 'Depense' || source === 'ACHAT' || source === 'CAISSE' || source === 'Dépense';
        }
    };

    const selectedBloquants = useMemo(() => {
        return (data?.alertes ?? []).filter((a: any) => a.type === 'bloquant' && (a.domaine ? (a.domaine === 'Encaissement' ? 'Encaissement' : 'Decaissement') : 'Decaissement') === selectedTab);
    }, [data?.alertes, selectedTab]);

    const selectedAvertissements = useMemo(() => {
        return (data?.alertes ?? []).filter((a: any) => a.type === 'avertissement' && (a.domaine ? (a.domaine === 'Encaissement' ? 'Encaissement' : 'Decaissement') : 'Decaissement') === selectedTab);
    }, [data?.alertes, selectedTab]);

    const filteredRecapSource = useMemo(() => {
        return (data?.recapSource ?? []).filter(r => sourceBelongsToDomain(r.source, selectedTab));
    }, [data?.recapSource, selectedTab]);

    const filteredRecapTaux = useMemo(() => {
        return (data?.recapTaux ?? []).filter(r => !r.domaine || r.domaine === selectedTab);
    }, [data?.recapTaux, selectedTab]);

    // TASK-174 : récap collecté/déductible par code activité, indépendant de l'onglet
    // (les deux domaines affichés côte à côte, cf. demande PO).
    const recapActiviteEncaissement = useMemo(() => {
        return (data?.recapActivite ?? [])
            .filter(r => r.domaine === 'Encaissement')
            .map(r => ({ source: r.codeActivite, ht: r.ht, tva: r.tva, ttc: r.ttc }));
    }, [data?.recapActivite]);

    const recapActiviteDecaissement = useMemo(() => {
        return (data?.recapActivite ?? [])
            .filter(r => r.domaine === 'Decaissement')
            .map(r => ({ source: r.codeActivite, ht: r.ht, tva: r.tva, ttc: r.ttc }));
    }, [data?.recapActivite]);

    const displayNbLignes = useMemo(() => {
        return filteredRecapSource.reduce((s: number, r: RecapLigne) => s + (r.nbLignes ?? 0), 0);
    }, [filteredRecapSource]);

    // Totaux TOUS DOMAINES (indépendants de l'onglet) pour le résultat de la période.
    const tvaCollecteeTotale = useMemo(
        () => (data?.recapSource ?? []).filter(r => sourceBelongsToDomain(r.source, 'Encaissement')).reduce((s, r) => s + r.tva, 0),
        [data?.recapSource],
    );
    const tvaDeductibleTotale = useMemo(
        () => (data?.recapSource ?? []).filter(r => sourceBelongsToDomain(r.source, 'Decaissement')).reduce((s, r) => s + r.tva, 0),
        [data?.recapSource],
    );
    const tvaDueTotale = tvaCollecteeTotale - tvaDeductibleTotale;

    const totalSource = useMemo(() => filteredRecapSource.reduce((s: number, r: RecapLigne) => s + r.tva, 0), [filteredRecapSource]);
    const totalHT = useMemo(() => filteredRecapSource.reduce((s: number, r: RecapLigne) => s + r.ht, 0), [filteredRecapSource]);
    const totalTTC = useMemo(() => filteredRecapSource.reduce((s: number, r: RecapLigne) => s + r.ttc, 0), [filteredRecapSource]);

    if (loading) {
        return (
            <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100%' }}>
                <Loader2 size={28} className="animate-spin" style={{ color: 'var(--accent-primary)' }} />
            </div>
        );
    }

    if (!data) return <div style={{ padding: '2rem', color: 'var(--danger)' }}>Erreur de chargement.</div>;

    const bloquants = data.alertes.filter((a: any) => a.type === 'bloquant');
    const hasBloquant = bloquants.length > 0;
    const cloturee = statut !== 0;
    const exportDisabled = hasBloquant || generating;

    // Déclenche la génération (idempotente) puis télécharge le fichier demandé.
    const genererEtTelecharger = async (type: 'excel' | 'xml') => {
        setGenerating(true);
        try {
            let f = fichiers;
            if (!f) {
                try {
                    const res = await api.post(`/declarations/${declarationId}/generation`);
                    f = res.data.fichiers as Fichiers;
                } catch (err: any) {
                    // 409 « Le fichier … existe déjà » : la génération A DÉJÀ eu lieu (typiquement
                    // dans une session précédente). Refuser le téléchargement dans ce cas était une
                    // impasse — le comptable ne pouvait plus jamais récupérer son fichier de dépôt.
                    // Les URL de téléchargement sont déterministes côté API, on les reconstruit.
                    if (err?.response?.status === 409) {
                        f = {
                            xmlDecaissement: `/declarations/${declarationId}/fichiers/xml`,
                            excelCheckup: `/declarations/${declarationId}/fichiers/excel`,
                        };
                    } else {
                        throw err;
                    }
                }
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

    // Si on est en vue drill, on affiche la DomainGrid filtrée
    if (drillFiltre) {
        return (
            <div style={{ display: 'flex', flexDirection: 'column', height: '100%', overflow: 'hidden' }}>
                {/* Bandeau drill */}
                <div style={{
                    padding: '0.5rem 1rem',
                    background: 'var(--status-warning-bg)',
                    borderBottom: '1px solid var(--status-warning-border)',
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
                            background: 'white', border: '1px solid var(--status-warning-border)',
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
                {/* Grille filtrée en lecture seule */}
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
            overflow: 'hidden', background: 'var(--bg-secondary)',
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
                    <h2 style={{ margin: 0, fontSize: '1rem', fontWeight: 600 }}>Étape 3 — Déclaration</h2>
                    <div style={{ fontSize: '0.72rem', color: 'var(--text-secondary)' }}>
                        Contrôle de cohérence · anomalies · export Excel et XML Simpl-TVA
                    </div>
                </div>
                <div style={{ marginLeft: 'auto', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                    {cloturee && (
                        <span style={{
                            display: 'inline-flex', alignItems: 'center', gap: '0.3rem',
                            fontSize: '0.75rem', fontWeight: 700, padding: '0.25rem 0.65rem',
                            borderRadius: 'var(--radius-full)', background: 'var(--status-ok-bg)', color: 'var(--status-ok-text)',
                            border: '1px solid #bbf7d0',
                        }}>
                            <Lock size={13} /> Verrou posé
                        </span>
                    )}
                    {selectedBloquants.length > 0 ? (
                        <span style={{
                            display: 'inline-flex', alignItems: 'center', gap: '0.3rem',
                            fontSize: '0.75rem', fontWeight: 700,
                            padding: '0.25rem 0.65rem', borderRadius: 'var(--radius-full)',
                            background: 'var(--status-blocking-bg)', color: 'var(--status-blocking-text)',
                            border: '1px solid #fca5a5',
                        }}>
                            <XCircle size={13} /> {selectedBloquants.length} bloquant{selectedBloquants.length > 1 ? 's' : ''}
                        </span>
                    ) : (
                        <span style={{
                            display: 'inline-flex', alignItems: 'center', gap: '0.3rem',
                            fontSize: '0.75rem', fontWeight: 700,
                            padding: '0.25rem 0.65rem', borderRadius: 'var(--radius-full)',
                            background: 'var(--status-ok-bg)', color: 'var(--status-ok-text)',
                            border: '1px solid #bbf7d0',
                        }}>
                            <CheckCircle2 size={13} /> Aucun bloquant
                        </span>
                    )}
                    {selectedAvertissements.length > 0 && (
                        <span style={{
                            display: 'inline-flex', alignItems: 'center', gap: '0.3rem',
                            fontSize: '0.75rem', fontWeight: 700,
                            padding: '0.25rem 0.65rem', borderRadius: 'var(--radius-full)',
                            background: '#fff7ed', color: '#c2410c',
                            border: '1px solid #fed7aa',
                        }}>
                            <AlertTriangle size={13} /> {selectedAvertissements.length} avert.
                        </span>
                    )}
                </div>
            </div>

            {/* Tabs for separating domains (TVA Déductible vs TVA Collectée) */}
            <div style={{
                display: 'flex',
                gap: '0.5rem',
                padding: '0.5rem 1rem 0 1rem',
                borderBottom: '1px solid var(--border-color)',
                background: 'white',
                flexShrink: 0
            }}>
                <button
                    onClick={() => setSelectedTab('Decaissement')}
                    style={{
                        padding: '0.6rem 1.2rem',
                        fontSize: '0.875rem',
                        fontWeight: 600,
                        border: 'none',
                        borderBottom: selectedTab === 'Decaissement' ? '3px solid var(--accent-primary)' : '3px solid transparent',
                        background: 'none',
                        color: selectedTab === 'Decaissement' ? 'var(--accent-primary)' : 'var(--text-secondary)',
                        cursor: 'pointer',
                        transition: 'all 0.2s',
                    }}
                >
                    TVA Déductible (Achats)
                </button>
                <button
                    onClick={() => setSelectedTab('Encaissement')}
                    style={{
                        padding: '0.6rem 1.2rem',
                        fontSize: '0.875rem',
                        fontWeight: 600,
                        border: 'none',
                        borderBottom: selectedTab === 'Encaissement' ? '3px solid var(--accent-primary)' : '3px solid transparent',
                        background: 'none',
                        color: selectedTab === 'Encaissement' ? 'var(--accent-primary)' : 'var(--text-secondary)',
                        cursor: 'pointer',
                        transition: 'all 0.2s',
                    }}
                >
                    TVA Collectée (Ventes)
                </button>
            </div>

            {/* Garde-fou bloquant */}
            {hasBloquant && (
                <div style={{
                    padding: '0.5rem 1rem',
                    background: 'var(--status-blocking-bg)',
                    borderBottom: '1px solid #fca5a5',
                    color: 'var(--status-blocking-text)',
                    fontSize: '0.8125rem',
                    fontWeight: 600,
                    flexShrink: 0,
                    display: 'flex',
                    alignItems: 'center',
                    gap: '0.5rem',
                }}>
                    <XCircle size={14} />
                    Export bloqué — corrigez les anomalies 🔴 avant de finaliser.
                </div>
            )}

            {/* Corps scrollable dense */}
            <div style={{ flex: 1, overflow: 'auto', padding: '0.65rem', display: 'flex', flexDirection: 'column', gap: '0.6rem' }}>

                {/* Résultat de la déclaration — le seul chiffre que le comptable cherche vraiment sur
                    cet écran, et qui n'était affiché NULLE PART : TVA due = collectée − déductible.
                    Il devait le calculer de tête en additionnant deux onglets. Toujours affiché
                    « tous domaines », indépendamment de l'onglet actif. */}
                <div style={{
                    display: 'flex', alignItems: 'baseline', gap: '1.5rem', flexWrap: 'wrap',
                    background: 'white', border: '1px solid var(--border-color)',
                    borderRadius: 'var(--radius-md)', padding: '0.6rem 0.9rem',
                }}>
                    <span style={{ fontSize: '0.68rem', textTransform: 'uppercase', letterSpacing: '0.04em', fontWeight: 700, color: 'var(--text-secondary)' }}>
                        Résultat de la période
                    </span>
                    <span style={{ fontSize: '0.85rem' }}>TVA collectée <strong>{formatMoney(tvaCollecteeTotale)}</strong></span>
                    <span style={{ fontSize: '0.85rem' }}>− TVA déductible <strong>{formatMoney(tvaDeductibleTotale)}</strong></span>
                    <span style={{ fontSize: '1rem', fontWeight: 800, color: tvaDueTotale >= 0 ? 'var(--accent-primary)' : 'var(--status-ok-text)' }}>
                        = {tvaDueTotale >= 0 ? 'TVA due' : 'Crédit de TVA'} {formatMoney(Math.abs(tvaDueTotale))}
                    </span>
                    <span style={{ fontSize: '0.7rem', color: 'var(--text-secondary)' }}>
                        (hors crédit de TVA reporté de la période précédente et hors régularisations)
                    </span>
                </div>

                {/* Bandeau synthèse dense */}
                <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
                    <Stat label={`Lignes intégrées (${selectedTab === 'Decaissement' ? 'achats' : 'ventes'})`} value={String(displayNbLignes)} />
                    <Stat label={`TVA ${selectedTab === 'Decaissement' ? 'déductible' : 'collectée'}`} value={formatMoney(totalSource)} accent />
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

                {/* ① Répartition par source */}
                <Section
                    icon={<BarChart2 size={14} />}
                    title="Répartition par source"
                    open={sourceOpen}
                    onToggle={() => setSourceOpen(!sourceOpen)}
                    badge={filteredRecapSource.length}
                    statusIcon={
                        data.equilibre
                            ? data.equilibre.isValid
                                ? <span style={{ fontSize: '0.7rem', color: 'var(--status-ok-text)', fontWeight: 700 }}>✔ Équilibre OK</span>
                                : <span style={{ fontSize: '0.7rem', color: 'var(--status-blocking-text)', fontWeight: 700 }}>✘ Déséquilibre {formatMoney(data.equilibre.ecart ?? 0)}</span>
                            : null
                    }
                >
                    <RecapSourceTable recapSource={filteredRecapSource} />
                </Section>

                {/* ② Vue par taux */}
                <Section
                    icon={<Percent size={14} />}
                    title="Vue par taux TVA"
                    open={tauxOpen}
                    onToggle={() => setTauxOpen(!tauxOpen)}
                    badge={filteredRecapTaux.length}
                >
                    <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.8125rem' }}>
                        <thead>
                            <tr style={{ background: 'var(--bg-tertiary)' }}>
                                <Th>Taux</Th>
                                <Th>Code taxe</Th>
                                <Th right>HT</Th>
                                <Th right>TVA</Th>
                                <Th right>TTC</Th>
                            </tr>
                        </thead>
                        <tbody>
                            {filteredRecapTaux
                                .slice()
                                .sort((a: any, b: any) => (b.taux ?? 0) - (a.taux ?? 0) || (a.codeTaxe ?? '').localeCompare(b.codeTaxe ?? ''))
                                .map((r: any) => (
                                    <tr key={`${r.taux}_${r.codeTaxe ?? ''}_${r.domaine}`} style={{ borderBottom: '1px solid var(--border-color)' }}>
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
                                        <Td>{r.codeTaxe || '—'}</Td>
                                        <Td right>{formatMoney(r.ht)}</Td>
                                        <Td right>{formatMoney(r.tva)}</Td>
                                        <Td right>{formatMoney(r.ttc)}</Td>
                                    </tr>
                                ))}
                        </tbody>
                    </table>
                </Section>

                {/* ③ Récap collecté/déductible par code activité */}
                <Section
                    icon={<BarChart2 size={14} />}
                    title="Récap par code activité"
                    open={activiteOpen}
                    onToggle={() => setActiviteOpen(!activiteOpen)}
                    badge={recapActiviteEncaissement.length + recapActiviteDecaissement.length}
                >
                    <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
                        <div>
                            <div style={{
                                padding: '0.4rem 0.65rem', fontSize: '0.75rem', fontWeight: 700,
                                color: 'var(--text-secondary)', textTransform: 'uppercase', letterSpacing: '0.04em',
                            }}>
                                Collecté (Encaissement)
                            </div>
                            <RecapSourceTable recapSource={recapActiviteEncaissement} columnLabel="Code activité" />
                        </div>
                        <div>
                            <div style={{
                                padding: '0.4rem 0.65rem', fontSize: '0.75rem', fontWeight: 700,
                                color: 'var(--text-secondary)', textTransform: 'uppercase', letterSpacing: '0.04em',
                            }}>
                                Déductible (Décaissement)
                            </div>
                            <RecapSourceTable recapSource={recapActiviteDecaissement} columnLabel="Code activité" />
                        </div>
                    </div>
                </Section>

                {/* ④ Anomalies 🔴 Bloquantes */}
                <Section
                    // Pastille rouge + icône rouge affichées même quand il n'y a AUCUNE anomalie :
                    // le comptable croyait à un problème alors que la section disait « Aucune ✔ ».
                    icon={selectedBloquants.length > 0
                        ? <XCircle size={14} style={{ color: 'var(--status-blocking-text)' }} />
                        : <CheckCircle2 size={14} style={{ color: 'var(--status-ok-text)' }} />}
                    title={selectedBloquants.length > 0 ? 'Anomalies bloquantes 🔴' : 'Anomalies bloquantes'}
                    open={bloquantsOpen}
                    onToggle={() => setBloquantsOpen(!bloquantsOpen)}
                    badge={selectedBloquants.length}
                    badgeStyle={{ background: 'var(--status-blocking-bg)', color: 'var(--status-blocking-text)' }}
                    emptyMessage={selectedBloquants.length === 0 ? 'Aucune anomalie bloquante ✔' : undefined}
                    emptyStyle={{ color: 'var(--status-ok-text)' }}
                >
                    {selectedBloquants.length > 0 && (
                        <div style={{ display: 'flex', flexDirection: 'column', gap: 0 }}>
                            {selectedBloquants.map((a: any, i: number) => (
                                <AnomalieRow
                                    key={i}
                                    alerte={a}
                                    onDrill={(domaine, filtre, label) =>
                                        setDrillFiltre({ domaine, filtre, label })
                                    }
                                    isLast={i === selectedBloquants.length - 1}
                                />
                            ))}
                        </div>
                    )}
                </Section>

                {/* ⑤ Anomalies 🟠 Avertissements */}
                <Section
                    icon={<AlertTriangle size={14} style={{ color: '#c2410c' }} />}
                    title={selectedAvertissements.length > 0 ? 'Avertissements 🟠' : 'Avertissements'}
                    open={avertissementsOpen}
                    onToggle={() => setAvertissementsOpen(!avertissementsOpen)}
                    badge={selectedAvertissements.length}
                    badgeStyle={selectedAvertissements.length > 0 ? { background: '#fff7ed', color: '#c2410c' } : undefined}
                    emptyMessage={selectedAvertissements.length === 0 ? 'Aucun avertissement ✔' : undefined}
                    emptyStyle={{ color: 'var(--status-ok-text)' }}
                >
                    {selectedAvertissements.length > 0 && (
                        <div style={{ display: 'flex', flexDirection: 'column', gap: 0 }}>
                            {selectedAvertissements.map((a: any, i: number) => (
                                <AnomalieRow
                                    key={i}
                                    alerte={a}
                                    onDrill={(domaine, filtre, label) =>
                                        setDrillFiltre({ domaine, filtre, label })
                                    }
                                    isLast={i === selectedAvertissements.length - 1}
                                />
                            ))}
                        </div>
                    )}
                </Section>

                {/* ⑥ Récap rapprochement (densité) */}
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

            {/* Barre d'actions (Export Excel / XML) */}
            <div style={{
                display: 'flex', alignItems: 'center', gap: '0.6rem', flexWrap: 'wrap',
                padding: '0.65rem 1rem', background: 'white', borderTop: '1px solid var(--border-color)', flexShrink: 0,
            }}>
                <button onClick={onBack} style={backBtnStyle}>
                    <ArrowLeft size={15} /> Retour aux déclarations
                </button>

                <div style={{ marginLeft: 'auto', display: 'flex', alignItems: 'center', gap: '0.6rem', flexWrap: 'wrap' }}>
                    <button
                        onClick={() => genererEtTelecharger('excel')}
                        disabled={exportDisabled}
                        title={hasBloquant ? 'Corrigez les anomalies 🔴 avant l’export' : 'Export Excel de contrôle'}
                        style={secondaryBtnStyle(exportDisabled)}
                    >
                        {generating ? <Loader2 size={15} className="animate-spin" /> : <FileSpreadsheet size={15} />} Export Excel
                    </button>

                    {selectedTab === 'Decaissement' && (
                        <button
                            onClick={() => genererEtTelecharger('xml')}
                            disabled={exportDisabled}
                            title={hasBloquant ? 'Corrigez les anomalies 🔴 avant l’export' : 'Générer le XML Simpl-TVA'}
                            style={secondaryBtnStyle(exportDisabled)}
                        >
                            {generating ? <Loader2 size={15} className="animate-spin" /> : <FileCode2 size={15} />} Générer le fichier SIMPL-TVA (XML)
                        </button>
                    )}
                </div>
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
                    ? <XCircle size={13} style={{ color: 'var(--status-blocking-text)', flexShrink: 0, marginTop: '0.1rem' }} />
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
                        color: isBloquant ? 'var(--status-blocking-text)' : '#c2410c',
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
            // Le conteneur parent est un `display:flex; flex-direction:column`. Sans flexShrink:0,
            // chaque section était COMPRIMÉE pour tenir dans la hauteur visible : les lignes de
            // « Vue par taux TVA » / « Répartition par source » étaient coupées en plein milieu et
            // le conteneur ne défilait pas. Sur l'écran final de la déclaration, cela masquait les
            // montants que le comptable doit lire avant de déposer.
            flexShrink: 0,
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

// ─── Stat dense ──────────────────────────────────────────────────────────────

function Stat({ label, value, accent, ok }: { label: string; value: string; accent?: boolean; ok?: boolean }) {
    const color = ok === false ? 'var(--status-blocking-text)' : ok === true ? 'var(--status-ok-text)' : accent ? 'var(--accent-primary)' : 'var(--text-primary)';
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
