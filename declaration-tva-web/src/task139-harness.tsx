import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import './index.css';
import { VerifierIntegrerPanel } from './VerifierIntegrerPanel';

// TASK-139 — Harnais de preuve visuelle (hors build de prod : servi uniquement par vite dev via
// task139.html pour le test Playwright). Rend le VRAI composant VerifierIntegrerPanel dans Chromium ;
// les réponses /api (/lignes et /checkup) sont mockées au niveau réseau par le test — pas besoin du
// backend .NET ni de la base prod. Objectif : prouver que lorsque l'écart global est expliqué par
// une ligne incohérente située dans l'AUTRE onglet, le message renvoie explicitement vers le bon
// domaine au lieu du trompeur « aucune ligne incohérente identifiée ».

function Harness() {
    return (
        <div style={{ height: '100vh', background: '#f3f4f6' }}>
            <VerifierIntegrerPanel
                declarationId="TVA1-2026-02"
                selectedRows={[]}
                readOnly={true}
                integree={false}
                showToast={(m) => console.log('toast:', m)}
                onIntegrationSuccess={() => {}}
            />
        </div>
    );
}

createRoot(document.getElementById('root')!).render(
    <StrictMode>
        <Harness />
    </StrictMode>,
);
