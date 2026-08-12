import { useState } from 'react';
import { X, Loader2 } from 'lucide-react';
import api from './api';

export function CreateDeclarationModal({ 
    societeId, 
    societeName, 
    onClose, 
    onSuccess 
}: { 
    societeId: number, 
    societeName: string, 
    onClose: () => void,
    onSuccess: (id: string) => void
}) {
    const [exercice, setExercice] = useState(new Date().getFullYear());
    const [type, setType] = useState<'Mensuel' | 'Trimestriel'>('Mensuel');
    const [periode, setPeriode] = useState('01');
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState<string | null>(null);

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        setError(null);
        setLoading(true);

        try {
            const isMensuel = type === 'Mensuel';
            const res = await api.post('/declarations', {
                societeId,
                exercice,
                type: isMensuel ? 0 : 1, // 0 = Mensuelle, 1 = Trimestrielle
                periode: isMensuel ? Number(periode) : Number(periode.replace('T', ''))
            });
            onSuccess(res.data.id);
        } catch (err: any) {
            setError(err.response?.data?.Message || err.response?.data?.message || err.message);
        } finally {
            setLoading(false);
        }
    };

    // Aperçu ALIGNÉ sur la règle réelle du back (DeclarationWorkflowService : `TVA{societeId}-
    // {exercice}-{periode:D2}{suffixeType}`, suffixe "-T" en trimestriel). L'aperçu affichait
    // auparavant le NOM de société (« TVANEW_EMA DISTRIBUTION-2026-01ance ») alors que la
    // déclaration créée s'appelait « TVA1-2026-01 » : deux numéros différents pour la même pièce.
    const isMensuelApercu = type === 'Mensuel';
    const periodeApercu = String(Number(periode.replace('T', ''))).padStart(2, '0');
    const numeroApercu = `TVA${societeId}-${exercice}-${periodeApercu}${isMensuelApercu ? '' : '-T'}`;

    return (
        <div style={{ position: 'fixed', inset: 0, zIndex: 9999, display: 'flex', alignItems: 'center', justifyContent: 'center', backgroundColor: 'rgba(0,0,0,0.5)', animation: 'fade-in 0.2s ease-out' }}>
            <div style={{ background: 'white', borderRadius: '8px', width: '100%', maxWidth: '450px', boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1), 0 10px 10px -5px rgba(0,0,0,0.04)' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '1.25rem 1.5rem', borderBottom: '1px solid var(--border-color)' }}>
                    <h3 style={{ margin: 0, fontSize: '1.25rem', fontWeight: 600 }}>Nouvelle Déclaration</h3>
                    <button onClick={onClose} style={{ background: 'transparent', border: 'none', cursor: 'pointer', color: 'var(--text-secondary)' }}><X size={20} /></button>
                </div>
                
                <form onSubmit={handleSubmit} style={{ padding: '1.5rem', display: 'flex', flexDirection: 'column', gap: '1.25rem' }}>
                    {error && (
                        <div style={{ padding: '0.75rem 1rem', background: 'var(--status-blocking-bg)', color: 'var(--status-blocking-text)', borderRadius: '4px', fontSize: '0.875rem' }}>
                            {error}
                        </div>
                    )}

                    <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
                        <label style={{ fontSize: '0.875rem', fontWeight: 500, color: 'var(--text-primary)' }}>Société</label>
                        <input type="text" value={societeName} disabled className="form-input" style={{ backgroundColor: 'var(--bg-secondary)', color: 'var(--text-secondary)' }} />
                    </div>

                    <div style={{ display: 'flex', gap: '1rem' }}>
                        <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem', flex: 1 }}>
                            <label style={{ fontSize: '0.875rem', fontWeight: 500, color: 'var(--text-primary)' }}>Exercice</label>
                            <input 
                                id="annee-declaration"
                                data-testid="annee-declaration"
                                type="number" 
                                value={exercice} 
                                onChange={e => setExercice(Number(e.target.value))} 
                                className="form-input" 
                                required 
                            />
                        </div>
                        <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem', flex: 1 }}>
                            <label style={{ fontSize: '0.875rem', fontWeight: 500, color: 'var(--text-primary)' }}>Type</label>
                            <select 
                                value={type} 
                                onChange={e => {
                                    const val = e.target.value as 'Mensuel' | 'Trimestriel';
                                    setType(val);
                                    setPeriode(val === 'Mensuel' ? '01' : 'T1');
                                }} 
                                className="form-input"
                            >
                                <option value="Mensuel">Mensuel</option>
                                <option value="Trimestriel">Trimestriel</option>
                            </select>
                        </div>
                    </div>

                    <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
                        <label style={{ fontSize: '0.875rem', fontWeight: 500, color: 'var(--text-primary)' }}>Période</label>
                        <select 
                            value={periode} 
                            onChange={e => setPeriode(e.target.value)} 
                            className="form-input"
                        >
                            {type === 'Mensuel' ? (
                                <>
                                    <option value="01">Janvier (01)</option>
                                    <option value="02">Février (02)</option>
                                    <option value="03">Mars (03)</option>
                                    <option value="04">Avril (04)</option>
                                    <option value="05">Mai (05)</option>
                                    <option value="06">Juin (06)</option>
                                    <option value="07">Juillet (07)</option>
                                    <option value="08">Août (08)</option>
                                    <option value="09">Septembre (09)</option>
                                    <option value="10">Octobre (10)</option>
                                    <option value="11">Novembre (11)</option>
                                    <option value="12">Décembre (12)</option>
                                </>
                            ) : (
                                <>
                                    <option value="T1">1er Trimestre (T1)</option>
                                    <option value="T2">2ème Trimestre (T2)</option>
                                    <option value="T3">3ème Trimestre (T3)</option>
                                    <option value="T4">4ème Trimestre (T4)</option>
                                </>
                            )}
                        </select>
                    </div>

                    <div style={{ padding: '1rem', background: 'var(--bg-secondary)', borderRadius: '4px', fontSize: '0.875rem', color: 'var(--text-secondary)' }}>
                        Numéro généré : <strong style={{ color: 'var(--text-primary)' }}>{numeroApercu}</strong>
                    </div>

                    <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '1rem', marginTop: '1rem' }}>
                        <button type="button" onClick={onClose} className="btn" style={{ background: 'transparent', border: '1px solid var(--border-color)', padding: '0.5rem 1rem', borderRadius: '4px' }}>
                            Annuler
                        </button>
                        <button type="submit" disabled={loading} className="btn btn-primary" style={{ background: 'var(--accent-primary)', color: 'white', border: 'none', padding: '0.5rem 1.5rem', borderRadius: '4px', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                            {loading && <Loader2 size={16} className="animate-spin" />}
                            Créer
                        </button>
                    </div>
                </form>
            </div>
        </div>
    );
}
