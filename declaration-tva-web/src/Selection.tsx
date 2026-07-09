import { useState } from 'react';
import { Loader2 } from 'lucide-react';
import { mockDeclarationData, type DeclarationModele } from './mockData';

export function Selection({ onLoaded }: { onLoaded: (data: DeclarationModele) => void }) {
  const [societe, setSociete] = useState('1');
  const [periodeType, setPeriodeType] = useState('mensuel');
  const [mois, setMois] = useState('10');
  const [annee, setAnnee] = useState('2023');
  const [loading, setLoading] = useState(false);

  const handleLoad = () => {
    setLoading(true);
    // Simulate API call for POST /declarations/preview
    setTimeout(() => {
      onLoaded(mockDeclarationData);
      setLoading(false);
    }, 600);
  };

  return (
    <div style={{ padding: '2rem', maxWidth: '800px', margin: '0 auto' }}>
      <h2 style={{ marginBottom: '1.5rem', fontSize: '1.5rem', fontWeight: 600, color: 'var(--text-primary)' }}>
        Sélection de la Déclaration
      </h2>
      
      <div style={{ background: 'white', padding: '2rem', borderRadius: '8px', boxShadow: '0 1px 3px rgba(0,0,0,0.1)' }}>
        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '1.5rem' }}>
          
          <div>
            <label style={{ display: 'block', marginBottom: '0.5rem', fontSize: '0.875rem', fontWeight: 500 }}>Société</label>
            <select className="form-input" value={societe} onChange={e => setSociete(e.target.value)} style={{ width: '100%', padding: '0.5rem' }}>
              <option value="1">GRF Société Mock</option>
            </select>
          </div>

          <div>
            <label style={{ display: 'block', marginBottom: '0.5rem', fontSize: '0.875rem', fontWeight: 500 }}>Périodicité</label>
            <select className="form-input" value={periodeType} onChange={e => setPeriodeType(e.target.value)} style={{ width: '100%', padding: '0.5rem' }}>
              <option value="mensuel">Mensuelle</option>
              <option value="trimestriel">Trimestrielle</option>
            </select>
          </div>

          <div>
            <label style={{ display: 'block', marginBottom: '0.5rem', fontSize: '0.875rem', fontWeight: 500 }}>Année</label>
            <select className="form-input" value={annee} onChange={e => setAnnee(e.target.value)} style={{ width: '100%', padding: '0.5rem' }}>
              <option value="2022">2022</option>
              <option value="2023">2023</option>
              <option value="2024">2024</option>
            </select>
          </div>

          <div>
            <label style={{ display: 'block', marginBottom: '0.5rem', fontSize: '0.875rem', fontWeight: 500 }}>
              {periodeType === 'mensuel' ? 'Mois' : 'Trimestre'}
            </label>
            {periodeType === 'mensuel' ? (
              <select className="form-input" value={mois} onChange={e => setMois(e.target.value)} style={{ width: '100%', padding: '0.5rem' }}>
                {Array.from({length: 12}, (_, i) => i + 1).map(m => (
                  <option key={m} value={m}>{new Date(2000, m - 1).toLocaleString('fr-FR', {month: 'long'})}</option>
                ))}
              </select>
            ) : (
              <select className="form-input" value={mois} onChange={e => setMois(e.target.value)} style={{ width: '100%', padding: '0.5rem' }}>
                <option value="1">T1</option>
                <option value="2">T2</option>
                <option value="3">T3</option>
                <option value="4">T4</option>
              </select>
            )}
          </div>

        </div>

        <div style={{ marginTop: '2rem', display: 'flex', justifyContent: 'flex-end' }}>
          <button 
            className="btn" 
            onClick={handleLoad} 
            disabled={loading}
            style={{ backgroundColor: 'var(--accent-primary)', color: 'white', padding: '0.5rem 1.5rem', borderRadius: '4px', border: 'none', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: '0.5rem' }}
          >
            {loading ? <Loader2 className="animate-spin" size={16} /> : null}
            Charger / Recalculer
          </button>
        </div>
      </div>
    </div>
  );
}
