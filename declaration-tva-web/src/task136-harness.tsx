import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import './index.css';
import './App.css';
import { Dashboard, type User } from './App';

// TASK-136 — Harnais de preuve visuelle e2e (hors build de prod : servi uniquement par vite dev
// via task136.html pour le test Playwright, même pattern que task130/138/139-harness). Rend le
// VRAI composant Dashboard (App.tsx) avec un utilisateur fictif, contournant le flux
// licence/connexion (flux déjà couvert par declaration.spec.ts, hors périmètre de cette TASK).
// Objectif unique : prouver que la nouvelle entrée de menu « Délai de paiement » route bien vers
// les 3 écrans TASK-130/134 (ConventionsDelaiPaiementPanel, DeclarationsDelaiPaiementPanel,
// ControleLignesDelaiPaiementPanel), et que la navigation existante (Rapprochement, Factures,
// Déclaration TVA) n'est pas régressée — sans dupliquer App.tsx.

const fakeUser: User = {
  login: 'test',
  nom: 'Utilisateur Test',
  societeId: 1,
  societeName: 'Société Test',
  token: 'fake-token',
  isAdmin: true,
};

function Harness() {
  return (
    <Dashboard
      user={fakeUser}
      onLogout={() => console.log('logout')}
      showToast={(m) => console.log('toast:', m)}
    />
  );
}

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <Harness />
  </StrictMode>,
);
