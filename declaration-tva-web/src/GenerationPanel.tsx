import { useState } from 'react';
import { Download, FileCode2, FileSpreadsheet, FileText, ArrowLeft, Loader2, CheckCircle2 } from 'lucide-react';
import api from './api';

export function GenerationPanel({ declarationId, onBack }: { declarationId: string, onBack: () => void }) {
    const [generating, setGenerating] = useState(false);
    const [generated, setGenerated] = useState(false);
    const [fichiers, setFichiers] = useState<any>(null);

    const handleGenerate = async () => {
        setGenerating(true);
        try {
            const res = await api.post(`/declarations/${declarationId}/generation`);
            setFichiers(res.data.fichiers);
            setGenerated(true);
        } catch (e) {
            console.error(e);
            alert('Erreur lors de la génération');
        } finally {
            setGenerating(false);
        }
    };

    const downloadFile = (url: string, name: string) => {
        // En vrai: window.location.href = url ou create object URL
        alert(`Téléchargement de ${name} depuis l'URL: ${url}`);
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

                            <div style={{ padding: '1rem', border: '1px solid var(--border-color)', borderRadius: '8px', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                                <div style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
                                    <FileText size={24} style={{ color: '#dc2626' }} />
                                    <div>
                                        <div style={{ fontWeight: 600 }}>Rapport d'anomalies</div>
                                        <div style={{ fontSize: '0.875rem', color: 'var(--text-secondary)' }}>PDF des avertissements tolérés</div>
                                    </div>
                                </div>
                                <button onClick={() => downloadFile(fichiers?.rapportAnomalies || '', 'Anomalies.pdf')} className="btn" style={{ background: 'var(--bg-secondary)', border: '1px solid var(--border-color)', padding: '0.5rem 1rem', borderRadius: '4px', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                                    <Download size={16} /> Télécharger
                                </button>
                            </div>
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
