import React, { useState, useEffect } from 'react';
import { Loader2 } from 'lucide-react';
import type { User } from './App';

interface Societe {
  soId: number;
  raisonSociale: string;
}

export function Auth({ onLogin }: { onLogin: (user: User) => void }) {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [societes, setSocietes] = useState<Societe[]>([]);
  const [selectedSocieteId, setSelectedSocieteId] = useState<number | ''>('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const api = (await import('./api')).default;
        const res = await api.get('/societes');
        if (cancelled) return;
        setSocietes(res.data || []);
        if (res.data && res.data.length > 0) {
          setSelectedSocieteId(res.data[0].soId);
        }
      } catch (err) {
        if (!cancelled) {
          console.error('Erreur lors du chargement des sociétés:', err);
          setError('Serveur injoignable — vérifiez que l\'API est démarrée');
        }
      }
    })();
    return () => { cancelled = true; };
  }, []);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (selectedSocieteId === '') {
      setError('Veuillez sélectionner une société');
      return;
    }
    setLoading(true);
    setError('');

    try {
        const api = (await import('./api')).default;
        const res = await api.post('/auth/login', { username, password });
        const selected = societes.find(s => s.soId === selectedSocieteId);
        if (!selected) {
          setError('Société sélectionnée invalide');
          setLoading(false);
          return;
        }
        onLogin({
          login: res.data.username || username,
          nom: res.data.nom || 'Admin User',
          societeId: selected.soId,
          societeName: selected.raisonSociale,
          token: res.data.token || res.data.Token,
          isAdmin: Boolean(res.data.isAdmin ?? res.data.IsAdmin)
        });
    } catch (err: any) {
        if (err?.response) {
          setError(err.response.data?.Message || err.response.data?.message || 'Identifiants incorrects');
        } else {
          setError('Serveur injoignable — vérifiez que l\'API est démarrée');
        }
    } finally {
        setLoading(false);
    }
  };

  return (
    <div className="auth-container">
      <div className="auth-card">
        <h1 className="auth-title">Déclaratif Maroc</h1>
        <p className="auth-subtitle">Accès sécurisé à l'espace de gestion</p>
        
        {error && (
          <div className="mb-4 text-sm text-danger text-center animate-fade-in" style={{ color: 'var(--danger-color, #ef4444)', marginBottom: '1rem' }}>
            {error}
          </div>
        )}
        
        <form onSubmit={handleSubmit}>
          <div className="form-group" style={{ marginBottom: '1rem' }}>
            <label className="form-label" style={{ display: 'block', marginBottom: '0.5rem', fontSize: '0.875rem', fontWeight: 500 }}>Nom d'utilisateur</label>
            <input 
              type="text" 
              className="form-input" 
              style={{ width: '100%', padding: '0.5rem', borderRadius: '4px', border: '1px solid var(--border-color)' }}
              value={username}
              onChange={e => setUsername(e.target.value)}
              required 
            />
          </div>
          <div className="form-group" style={{ marginBottom: '1rem' }}>
            <label className="form-label" style={{ display: 'block', marginBottom: '0.5rem', fontSize: '0.875rem', fontWeight: 500 }}>Mot de passe</label>
            <input 
              type="password" 
              className="form-input"
              style={{ width: '100%', padding: '0.5rem', borderRadius: '4px', border: '1px solid var(--border-color)' }} 
              value={password}
              onChange={e => setPassword(e.target.value)}
              required 
            />
          </div>
          <div className="form-group" style={{ marginBottom: '1.5rem' }}>
            <label className="form-label" style={{ display: 'block', marginBottom: '0.5rem', fontSize: '0.875rem', fontWeight: 500 }}>Société</label>
            <select
              className="form-input"
              style={{ width: '100%', padding: '0.5rem', borderRadius: '4px', border: '1px solid var(--border-color)', backgroundColor: 'white', color: 'var(--text-primary)' }}
              value={selectedSocieteId}
              onChange={e => setSelectedSocieteId(e.target.value === '' ? '' : Number(e.target.value))}
              required
            >
              <option value="">-- Choisir une société --</option>
              {societes.map(s => (
                <option key={s.soId} value={s.soId}>{s.raisonSociale}</option>
              ))}
            </select>
          </div>
          <button type="submit" className="btn btn-primary mt-4" disabled={loading || selectedSocieteId === ''} style={{ width: '100%', padding: '0.75rem', backgroundColor: 'var(--accent-primary)', color: 'white', border: 'none', borderRadius: '4px', cursor: 'pointer', display: 'flex', justifyContent: 'center' }}>
            {loading ? <Loader2 className="animate-spin" size={20} /> : 'Se Connecter'}
          </button>
        </form>
      </div>
    </div>
  );
}
