import React, { useState } from 'react';
import { Loader2 } from 'lucide-react';
import type { User } from './App';

export function Auth({ onLogin }: { onLogin: (user: User) => void }) {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError('');

    try {
        const res = await (await import('./api')).default.post('/auth/login', { username, password });
        onLogin({
          login: res.data.username || username,
          nom: res.data.nom || 'Admin User',
          societeId: res.data.societeId || '001',
          societeName: res.data.societeName || 'GR_EMA_DISTRIBUTION',
          token: res.data.token || res.data.Token
        });
    } catch (err: any) {
        setError('Identifiants incorrects ou erreur réseau');
    } finally {
        setLoading(false);
    }
  };

  return (
    <div className="auth-container">
      <div className="auth-card">
        <h1 className="auth-title">Déclaration TVA</h1>
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
          <div className="form-group" style={{ marginBottom: '1.5rem' }}>
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
          <button type="submit" className="btn btn-primary mt-4" disabled={loading} style={{ width: '100%', padding: '0.75rem', backgroundColor: 'var(--accent-primary)', color: 'white', border: 'none', borderRadius: '4px', cursor: 'pointer', display: 'flex', justifyContent: 'center' }}>
            {loading ? <Loader2 className="animate-spin" size={20} /> : 'Se Connecter'}
          </button>
        </form>
      </div>
    </div>
  );
}
