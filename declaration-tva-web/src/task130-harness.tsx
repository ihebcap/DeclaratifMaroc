import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import './index.css';
import { ConventionsDelaiPaiementPanel } from './ConventionsDelaiPaiementPanel';

// TASK-130 — Harnais de preuve visuelle (hors build de prod : servi uniquement par vite dev via
// task130.html pour le test Playwright, même pattern que task138/task139). Rend le VRAI composant
// ConventionsDelaiPaiementPanel dans Chromium ; les réponses /api sont mockées au niveau réseau
// par le test — pas besoin du backend .NET ni de la base réelle. Objectif : prouver le parcours
// complet CRUD Convention/Facture + rejet chevauchement (message explicite) + clôture anticipée
// sans dépendre du branchement au menu (TASK-136, hors périmètre strict de cette TASK).

function Harness() {
    return (
        <div style={{ height: '100vh', background: '#f3f4f6' }}>
            <ConventionsDelaiPaiementPanel
                societeId={1}
                showToast={(m) => console.log('toast:', m)}
            />
        </div>
    );
}

createRoot(document.getElementById('root')!).render(
    <StrictMode>
        <Harness />
    </StrictMode>,
);
