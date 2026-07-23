import { useState } from 'react';
import { Download, FileCode2, FileSpreadsheet, ArrowLeft, Loader2, CheckCircle2 } from 'lucide-react';
import api from './api';

// TASK-155 : ce composant n'est référencé par aucune route (App.tsx:296 monte DeclarationStepper
// → DeclarationFinalePanel.tsx pour l'étape ④/⑥, jamais GenerationPanel.tsx) — cartographie
// corrigée en VERIFY. Alignement fait par hygiène (mêmes clés fichiers.xmlDecaissement/
// excelCheckup que le contrôleur, téléchargement réel, erreur réelle), mais aucun test manuel
// n'a de sens sur un composant non monté.
export function GenerationPanel({ declarationId, onBack }: { declarationId: string, onBack: () => void }) {
    const [generating, setGenerating] = useState(false);
    const [generated, setGenerated] = useState(false);
    const [fichiers, setFichiers] = useState<any>(null);
    const [erreur, setErreur] = useState<string | null>(null);

    const handleGenerate = async () => {
        setGenerating(true);
        setErreur(null);
        try {
            const res = await api.post(`/declarations/${declarationId}/generation`);
            setFichiers(res.data.fichiers);
            setGenerated(true);
        } catch (e: any) {
            console.error(e);
            const msg = e?.response?.data?.Message || e?.response?.data?.message || 'Erreur lors de la génération';
            setErreur(msg);
        } finally {
            setGenerating(false);
        }
    };

    const downloadFile = async (url: string, name: string) => {
        if (!url) return;
        try {
            const res = await api.get(url, { responseType: 'blob' });
            const blobUrl = URL.createObjectURL(res.data);
            const a = document.createElement('a');
            a.href = blobUrl;
            a.download = name;
            document.body.appendChild(a);
            a.click();
            a.remove();
            URL.revokeObjectURL(blobUrl);
        } catch (e: any) {
            console.error(e);
            const msg = e?.response?.data?.Message || e?.response?.data?.message || 'Erreur lors du téléchargement';
            setErreur(msg);
        }
    };

    return (
        <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', height: '100%', padding: '2rem' }}>
            <div style={{ maxWidth: '600px', width: '100%', background: 'white', borderRadius: '8px', border: '1px solid var(--border-color)', overflow: 'hidden', boxShadow: '0 4px 6px -1px rgba(0,0,0,0.1)' }}>
                <div style={{ padding: '2rem', textAlign: 'center', borderBottom: '1px solid var(--border-color)' }}>
                    <div style={{ display: 'inline-flex', alignItems: 'center', justifyContent: 'center', width: '64px', height: '64px', borderRadius: '50%', background: generated ? 'var(--status-ok-bg)' : 'var(--bg-secondary)', color: generated ? '#16a34a' : 'var(--text-secondary)', marginBottom: '1.5rem' }}>
                        {generated ? <CheckCircle2 size={32} /> : <FileCode2 size={32} />}
                    </div>
                    <h2 style={{ margin: '0 0 0.5rem 0', fontSize: '1.5rem', fontWeight: 600 }}>Génération des Fichiers</h2>
                    <p style={{ margin: 0, color: 'var(--text-secondary)' }}>
                        La déclaration est clôturée. Vous pouvez maintenant générer les fichiers de dépôt.
                    </p>
                </div>
                
                <div style={{ padding: '2rem', display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
                    {erreur && (
                        <div style={{ padding: '0.75rem 1rem', borderRadius: '8px', background: 'var(--status-blocking-bg, #fef2f2)', color: 'var(--status-blocking-text, #b91c1c)', fontSize: '0.875rem' }}>
                            {erreur}
                        </div>
                    )}
                    {!generated ? (
                        <button
                            onClick={handleGenerate}
                            disabled={generating}
                            className="btn btn-primary"
                            style={{ width: '100%', padding: '1rem', display: 'flex', justifyContent: 'center', alignItems: 'center', gap: '0.75rem', fontSize: '1.125rem', fontWeight: 600, background: 'var(--accent-primary)', color: 'white', border: 'none', borderRadius: '8px', cursor: 'pointer' }}
                        >
                            {generating ? <Loader2 className="animate-spin" size={24} /> : <FileCode2 size={24} />}
                            Lancer la génération
                        </button>
                    ) : (
                        <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
                            <div style={{ padding: '1rem', border: '1px solid var(--border-color)', borderRadius: '8px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                                <div style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
                                    <FileCode2 size={24} style={{ color: '#0284c7' }} />
                                    <div>
                                        <div style={{ fontWeight: 600 }}>Fichiers XML (DGI)</div>
                                        <div style={{ fontSize: '0.875rem', color: 'var(--text-secondary)' }}>ZIP contenant un XML par domaine</div>
                                    </div>
                                </div>
                                <button onClick={() => downloadFile(fichiers?.xmlDecaissement || '', 'XML_DGI.zip')} className="btn" style={{ background: 'var(--bg-secondary)', border: '1px solid var(--border-color)', padding: '0.5rem 1rem', borderRadius: '4px', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                                    <Download size={16} /> Télécharger
                                </button>
                            </div>
                            
                            <div style={{ padding: '1rem', border: '1px solid var(--border-color)', borderRadius: '8px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                                <div style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
                                    <FileSpreadsheet size={24} style={{ color: '#16a34a' }} />
                                    <div>
                                        <div style={{ fontWeight: 600 }}>Excel Checkup</div>
                                        <div style={{ fontSize: '0.875rem', color: 'var(--text-secondary)' }}>Récapitulatifs détaillés</div>
                                    </div>
                                </div>
                                <button onClick={() => downloadFile(fichiers?.excelCheckup || '', 'Checkup.xlsx')} className="btn" style={{ background: 'var(--bg-secondary)', border: '1px solid var(--border-color)', padding: '0.5rem 1rem', borderRadius: '4px', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                                    <Download size={16} /> Télécharger
                                </button>
                            </div>
                            {/* TASK-155 : pas de "Rapport d'anomalies" PDF — aucun backend ne l'a
                                jamais produit (ni TASK-010/011 ni TASK-155), fantôme UI signalé au
                                PO en VERIFY plutôt qu'implémenté silencieusement ici. */}
                        </div>
                    )}

                    <div style={{ textAlign: 'center', marginTop: '1rem' }}>
                        <button onClick={onBack} className="btn" style={{ background: 'transparent', border: 'none', color: 'var(--text-secondary)', display: 'inline-flex', alignItems: 'center', gap: '0.5rem', cursor: 'pointer' }}>
                            <ArrowLeft size={16} /> Retour aux déclarations
                        </button>
                    </div>
                </div>
            </div>
        </div>
    );
}
