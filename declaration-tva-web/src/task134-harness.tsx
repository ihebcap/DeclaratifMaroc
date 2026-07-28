import { StrictMode, useState } from 'react';
import { createRoot } from 'react-dom/client';
import './index.css';
import { DeclarationsDelaiPaiementPanel } from './DeclarationsDelaiPaiementPanel';
import { ControleLignesDelaiPaiementPanel } from './ControleLignesDelaiPaiementPanel';

// TASK-134 — Harnais de preuve visuelle (hors build de prod : servi uniquement par vite dev via
// task134.html pour les tests Playwright, même pattern que task130/task138/task139). Rend les VRAIS
// composants dans Chromium ; les réponses /api sont mockées au niveau réseau par le test — pas besoin
// du backend .NET ni de la base réelle.
//
// La bascule ci-dessous n'est PAS un branchement au menu (TASK-136, explicitement hors périmètre) :
// c'est une route directe temporaire de test, confinée à ce fichier de harnais, jamais importée par
// App.tsx.

function Harness() {
    const [onglet, setOnglet] = useState<'declarations' | 'controle'>('declarations');

    return (
        <div style={{ height: '100vh', display: 'flex', flexDirection: 'column', background: '#f3f4f6' }}>
            <div style={{ display: 'flex', gap: '0.5rem', padding: '0.5rem 1rem', background: 'white', borderBottom: '1px solid var(--border-color)' }}>
                <button
                    onClick={() => setOnglet('declarations')}
                    style={{ padding: '0.3rem 0.75rem', fontSize: '0.8rem', cursor: 'pointer', border: '1px solid var(--border-color)', borderRadius: '4px', background: onglet === 'declarations' ? 'var(--accent-primary)' : 'white', color: onglet === 'declarations' ? 'white' : 'var(--text-primary)' }}
                >
                    Déclarations DDP
                </button>
                <button
                    onClick={() => setOnglet('controle')}
                    style={{ padding: '0.3rem 0.75rem', fontSize: '0.8rem', cursor: 'pointer', border: '1px solid var(--border-color)', borderRadius: '4px', background: onglet === 'controle' ? 'var(--accent-primary)' : 'white', color: onglet === 'controle' ? 'white' : 'var(--text-primary)' }}
                >
                    Contrôle lignes hors délai
                </button>
            </div>
            <div style={{ flex: 1, minHeight: 0, display: 'flex' }}>
                {onglet === 'declarations'
                    ? <DeclarationsDelaiPaiementPanel societeId={1} showToast={(m) => console.log('toast:', m)} />
                    : <ControleLignesDelaiPaiementPanel societeId={1} showToast={(m) => console.log('toast:', m)} />}
            </div>
        </div>
    );
}

createRoot(document.getElementById('root')!).render(
    <StrictMode>
        <Harness />
    </StrictMode>,
);
