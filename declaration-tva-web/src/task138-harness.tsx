import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import './index.css';
import { DomainGrid } from './DomainGrid';

// TASK-138 — Harnais de preuve visuelle (hors build de prod : servi uniquement par vite dev via
// task138.html pour le test Playwright). Rend le VRAI composant DomainGrid dans Chromium ; les
// réponses /api sont mockées au niveau réseau par le test. Objectif : prouver l'alignement strict
// en-tête/lignes (défaut CSS pur des tentatives TASK-113), reproduit fidèlement dans un vrai
// navigateur sans dépendre du backend .NET ni de la base prod.

const params = new URLSearchParams(window.location.search);
const readonly = params.get('readonly') === '1';

function Harness() {
    return (
        <div style={{ height: '100vh', padding: '16px', boxSizing: 'border-box', background: '#f3f4f6' }}>
            <DomainGrid
                declarationId="TVA1-2026-01"
                domaine={'Decaissement' as any}
                onActionDone={() => {}}
                showToast={(m) => console.log('toast:', m)}
                readonly={readonly}
                onRowClick={readonly ? () => {} : undefined}
            />
        </div>
    );
}

createRoot(document.getElementById('root')!).render(
    <StrictMode>
        <Harness />
    </StrictMode>,
);
