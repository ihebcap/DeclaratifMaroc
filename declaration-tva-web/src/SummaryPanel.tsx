import { AlertTriangle, Info, AlertCircle } from 'lucide-react';
import type { DeclarationModele } from './mockData';
import { formatMoney } from './utils';

export function SummaryPanel({ data }: { data: DeclarationModele }) {
  return (
    <div style={{ display: 'flex', gap: '1.5rem', marginBottom: '1rem', flexDirection: 'column' }}>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))', gap: '1rem' }}>
        
        {/* Recap Source */}
        <div style={{ background: 'white', padding: '1rem', borderRadius: '8px', boxShadow: '0 1px 3px rgba(0,0,0,0.1)' }}>
          <h3 style={{ fontSize: '1rem', fontWeight: 600, marginBottom: '1rem', color: 'var(--text-primary)' }}>Récapitulatif par Source</h3>
          <table className="table" style={{ width: '100%', fontSize: '0.875rem' }}>
            <thead>
              <tr>
                <th style={{ textAlign: 'left', padding: '0.5rem', borderBottom: '1px solid var(--border-color)' }}>Source</th>
                <th style={{ textAlign: 'right', padding: '0.5rem', borderBottom: '1px solid var(--border-color)' }}>HT</th>
                <th style={{ textAlign: 'right', padding: '0.5rem', borderBottom: '1px solid var(--border-color)' }}>TVA</th>
                <th style={{ textAlign: 'right', padding: '0.5rem', borderBottom: '1px solid var(--border-color)' }}>TTC</th>
              </tr>
            </thead>
            <tbody>
              {data.recapSource.map((r, i) => (
                <tr key={i}>
                  <td style={{ padding: '0.5rem', borderBottom: '1px solid var(--border-color)' }}>{r.source}</td>
                  <td style={{ textAlign: 'right', padding: '0.5rem', borderBottom: '1px solid var(--border-color)' }}>{formatMoney(r.ht)}</td>
                  <td style={{ textAlign: 'right', padding: '0.5rem', borderBottom: '1px solid var(--border-color)' }}>{formatMoney(r.tva)}</td>
                  <td style={{ textAlign: 'right', padding: '0.5rem', borderBottom: '1px solid var(--border-color)' }}>{formatMoney(r.ttc)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {/* Recap Taux */}
        <div style={{ background: 'white', padding: '1rem', borderRadius: '8px', boxShadow: '0 1px 3px rgba(0,0,0,0.1)' }}>
          <h3 style={{ fontSize: '1rem', fontWeight: 600, marginBottom: '1rem', color: 'var(--text-primary)' }}>Récapitulatif par Taux</h3>
          <table className="table" style={{ width: '100%', fontSize: '0.875rem' }}>
            <thead>
              <tr>
                <th style={{ textAlign: 'left', padding: '0.5rem', borderBottom: '1px solid var(--border-color)' }}>Taux</th>
                <th style={{ textAlign: 'right', padding: '0.5rem', borderBottom: '1px solid var(--border-color)' }}>HT</th>
                <th style={{ textAlign: 'right', padding: '0.5rem', borderBottom: '1px solid var(--border-color)' }}>TVA</th>
                <th style={{ textAlign: 'right', padding: '0.5rem', borderBottom: '1px solid var(--border-color)' }}>TTC</th>
              </tr>
            </thead>
            <tbody>
              {data.recapTaux.map((r, i) => (
                <tr key={i}>
                  <td style={{ padding: '0.5rem', borderBottom: '1px solid var(--border-color)' }}>{r.taux}%</td>
                  <td style={{ textAlign: 'right', padding: '0.5rem', borderBottom: '1px solid var(--border-color)' }}>{formatMoney(r.ht)}</td>
                  <td style={{ textAlign: 'right', padding: '0.5rem', borderBottom: '1px solid var(--border-color)' }}>{formatMoney(r.tva)}</td>
                  <td style={{ textAlign: 'right', padding: '0.5rem', borderBottom: '1px solid var(--border-color)' }}>{formatMoney(r.ttc)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* Alertes */}
      <div style={{ background: 'white', padding: '1rem', borderRadius: '8px', boxShadow: '0 1px 3px rgba(0,0,0,0.1)' }}>
        <h3 style={{ fontSize: '1rem', fontWeight: 600, marginBottom: '1rem', color: 'var(--text-primary)', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
          <AlertTriangle size={18} style={{ color: 'var(--warning-color, #f59e0b)' }} /> Alertes
        </h3>
        {data.alertes.length === 0 ? (
          <p style={{ fontSize: '0.875rem', color: 'var(--text-secondary)' }}>Aucune alerte détectée.</p>
        ) : (
          <ul style={{ listStyle: 'none', padding: 0, margin: 0, display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
            {data.alertes.map((a, i) => (
              <li key={i} style={{ 
                display: 'flex', alignItems: 'flex-start', gap: '0.5rem', 
                fontSize: '0.875rem', padding: '0.5rem', borderRadius: '4px',
                backgroundColor: a.type === 'bloquant' ? 'rgba(239, 68, 68, 0.1)' : 'rgba(245, 158, 11, 0.1)',
                color: a.type === 'bloquant' ? 'var(--danger-color, #ef4444)' : '#d97706'
              }}>
                {a.type === 'bloquant' ? <AlertCircle size={16} style={{ marginTop: '2px', flexShrink: 0 }} /> : <Info size={16} style={{ marginTop: '2px', flexShrink: 0 }} />}
                <div>
                  <strong>{a.type === 'bloquant' ? 'Bloquant' : 'Avertissement'}:</strong> {a.message}
                  {a.ligneId && <span style={{ marginLeft: '0.5rem', fontSize: '0.75rem', opacity: 0.8 }}>(Ligne #{a.ligneId})</span>}
                </div>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}
