import { useState } from 'react';
import {
  LogOut, LayoutDashboard, Landmark, FileText, FileCheck,
  Receipt, Scissors, Send, BarChart3, Lock, Construction,
} from 'lucide-react';
import './index.css';
import './App.css';
import { Auth } from './Auth';
import { DeclarationList } from './DeclarationList';
import { CreateDeclarationModal } from './CreateDeclarationModal';
import { DeclarationStepper } from './DeclarationStepper';
import { RapprochementInterrogation } from './RapprochementInterrogation';
import { FactureInterrogation } from './FactureInterrogation';

export interface User {
  login: string;
  nom: string;
  societeId: number;
  societeName: string;
  token: string;
  isAdmin: boolean;
}

// Sections navigables (le shell ne fait que router — aucun calcul/API ici).
type SectionKey = 'rapprochement' | 'factures' | 'declaration' | 'releve';

type MenuStatus = 'live' | 'soon' | 'todo';

interface MenuEntry {
  key: SectionKey | string;
  label: string;
  icon: typeof LayoutDashboard;
  status: MenuStatus; // live = livré, soon = placeholder honnête, todo = grisé "à venir"
}

interface MenuGroup {
  title: string;
  entries: MenuEntry[];
}

const MENU_GROUPS: MenuGroup[] = [
  {
    title: 'INTERROGATION',
    entries: [
      { key: 'rapprochement', label: 'Rapprochement bancaire', icon: Landmark, status: 'live' },
      { key: 'factures', label: 'Factures', icon: FileText, status: 'live' },
    ],
  },
  {
    title: 'DÉCLARATION',
    entries: [
      { key: 'declaration', label: 'Déclaration TVA', icon: FileCheck, status: 'live' },
      { key: 'releve', label: 'Relevé de déductions', icon: Receipt, status: 'soon' },
    ],
  },
  {
    title: 'À VENIR',
    entries: [
      { key: 'ras', label: 'Retenue à la source (RAS)', icon: Scissors, status: 'todo' },
      { key: 'simpl', label: 'Télédéclaration (SIMPL)', icon: Send, status: 'todo' },
      { key: 'dashboard', label: 'Tableau de bord', icon: BarChart3, status: 'todo' },
    ],
  },
];

function App() {
  const [user, setUser] = useState<User | null>(() => {
    const saved = sessionStorage.getItem('tva_user');
    if (!saved) return null;
    return JSON.parse(saved) as User;
  });
  
  const [toast, setToast] = useState<{message: string, type: 'success'|'error'|'warning'} | null>(null);

  const showToast = (message: string, type: 'success'|'error'|'warning' = 'success') => {
    setToast({ message, type });
    setTimeout(() => setToast(null), 3000);
  };

  const handleLogin = (u: User) => {
    sessionStorage.setItem('tva_user', JSON.stringify(u));
    setUser(u);
  };

  const handleLogout = () => {
    sessionStorage.removeItem('tva_user');
    setUser(null);
  };

  if (!user) {
    return <Auth onLogin={handleLogin} />;
  }

  return (
    <>
      <Dashboard user={user} onLogout={handleLogout} showToast={showToast} />
      {toast && (
        <div style={{
          position: 'fixed', bottom: '2rem', right: '2rem', zIndex: 9999,
          backgroundColor: toast.type === 'error' ? 'var(--danger-color, #ef4444)' : toast.type === 'warning' ? '#f59e0b' : 'var(--success-color, #22c55e)',
          color: 'white', padding: '1rem 1.5rem', borderRadius: '8px',
          boxShadow: '0 10px 15px -3px rgba(0, 0, 0, 0.1)',
          display: 'flex', alignItems: 'center', gap: '0.75rem',
          fontWeight: 500, fontSize: '0.875rem', animation: 'fade-in 0.3s ease-out'
        }}>
          {toast.message}
        </div>
      )}
    </>
  );
}

function Dashboard({ user, onLogout, showToast }: { user: User; onLogout: () => void; showToast: (msg: string, type?: 'success'|'error'|'warning') => void }) {
  const [isSidebarOpen, setIsSidebarOpen] = useState(true);
  const [activeSection, setActiveSection] = useState<SectionKey>('declaration');
  const [currentDeclarationId, setCurrentDeclarationId] = useState<string | null>(null);
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);

  const handleSelect = (entry: MenuEntry) => {
    if (entry.status === 'todo') return; // "à venir" : non cliquable, aucune route morte
    setActiveSection(entry.key as SectionKey);
  };

  return (
    <div className="app-container animate-fade-in">
      <aside className={`app-sidebar ${isSidebarOpen ? '' : 'collapsed'}`}>
        <div className="sidebar-header" style={isSidebarOpen ? {cursor: 'pointer', flexDirection: 'column', alignItems: 'stretch', justifyContent: 'flex-start'} : {cursor: 'pointer'}} onClick={() => setIsSidebarOpen(!isSidebarOpen)}>
          {isSidebarOpen ? (
            <>
              <div style={{display: 'flex', alignItems: 'center', justifyContent: 'space-between'}}>
                <div className="flex items-center gap-2">
                  <LayoutDashboard size={22} style={{color: 'var(--accent-primary)'}} />
                  <img src="/icon-dm.svg" alt="DM" width={18} height={18} style={{borderRadius: '4px', imageRendering: 'pixelated'}} />
                  <span>TVA</span>
                </div>
              </div>
              <div style={{fontSize: '0.7rem', fontWeight: 500, color: 'var(--text-secondary)', paddingLeft: 'calc(22px + 0.5rem)', marginTop: '0.15rem', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis'}}>
                {user.societeName}
              </div>
            </>
          ) : (
            <LayoutDashboard size={22} style={{color: 'var(--accent-primary)'}} />
          )}
        </div>

        <div className="sidebar-menu">
          {MENU_GROUPS
            // On masque la partie « à venir » (entrées todo) tant qu'elle n'est pas livrée.
            // La structure reste intacte : retirer ce filtre suffit à la réafficher.
            .map(group => ({ ...group, entries: group.entries.filter(e => e.status !== 'todo') }))
            .filter(group => group.entries.length > 0)
            .map(group => (
            <div key={group.title} className="sidebar-group">
              {isSidebarOpen && <div className="sidebar-group-label">{group.title}</div>}
              {group.entries.map(entry => {
                const Icon = entry.icon;
                const isActive = entry.status !== 'todo' && activeSection === entry.key;
                const disabled = entry.status === 'todo';
                return (
                  <div
                    key={entry.key}
                    className={`sidebar-item ${isActive ? 'active' : ''} ${disabled ? 'disabled' : ''}`}
                    title={disabled ? `${entry.label} — à venir` : entry.label}
                    onClick={() => handleSelect(entry)}
                    aria-disabled={disabled}
                  >
                    <Icon size={17} />
                    <span className="sidebar-text">{entry.label}</span>
                    {isSidebarOpen && entry.status === 'soon' && <span className="sidebar-tag">bientôt</span>}
                    {isSidebarOpen && disabled && <Lock size={13} className="sidebar-lock" />}
                  </div>
                );
              })}
            </div>
          ))}
        </div>

        <div className="sidebar-footer">
          {isSidebarOpen ? (
            <>
              <div style={{fontWeight: 600, fontSize: '0.8125rem', color: 'var(--text-primary)', marginBottom: '0.5rem'}}>
                {user.nom}
              </div>
              <button onClick={onLogout} className="btn" style={{backgroundColor: 'var(--bg-tertiary)', color: 'var(--text-primary)', border: '1px solid var(--border-color)', padding: '0.375rem', display: 'flex', alignItems: 'center', justifyContent: 'center', gap: '0.5rem', width: '100%'}}>
                <LogOut size={15} />
                <span>Déconnexion</span>
              </button>
            </>
          ) : (
            <button onClick={onLogout} className="btn" style={{backgroundColor: 'transparent', color: 'var(--text-primary)', padding: '0.5rem', display: 'flex', alignItems: 'center', justifyContent: 'center'}} title="Déconnexion">
              <LogOut size={15} />
            </button>
          )}
        </div>
      </aside>

      <main className="main-content">
        {activeSection === 'declaration' ? (
          currentDeclarationId ? (
            <DeclarationStepper declarationId={currentDeclarationId} showToast={showToast} onBack={() => setCurrentDeclarationId(null)} />
          ) : (
            <DeclarationList
              societeId={user.societeId}
              isAdmin={user.isAdmin}
              onOpenDeclaration={(id) => setCurrentDeclarationId(id)}
              onCreateNew={() => setIsCreateModalOpen(true)}
              showToast={showToast}
            />
          )
        ) : activeSection === 'rapprochement' ? (
          <RapprochementInterrogation societeId={user.societeId} showToast={showToast} />
        ) : activeSection === 'factures' ? (
          <FactureInterrogation societeId={user.societeId} showToast={showToast} />
        ) : (
          <Placeholder
            icon={Receipt}
            title="Relevé de déductions"
            description="Sortie / annexe officielle du relevé de déductions. Écran en cours de cadrage — aucune donnée factice affichée."
          />
        )}
      </main>

      {isCreateModalOpen && (
        <CreateDeclarationModal
          societeId={user.societeId}
          societeName={user.societeName}
          onClose={() => setIsCreateModalOpen(false)}
          onSuccess={(id) => {
            setIsCreateModalOpen(false);
            setCurrentDeclarationId(id);
            showToast('Déclaration créée avec succès');
          }}
        />
      )}
    </div>
  );
}

// Placeholder honnête : aucune donnée factice, indique clairement l'état "en cours".
function Placeholder({ icon: Icon, title, description }: { icon: typeof LayoutDashboard; title: string; description: string }) {
  return (
    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', textAlign: 'center', padding: '2rem', color: 'var(--text-secondary)' }}>
      <div style={{ position: 'relative', marginBottom: '1rem' }}>
        <Icon size={56} style={{ opacity: 0.25 }} />
        <Construction size={22} style={{ position: 'absolute', bottom: -4, right: -8, color: 'var(--warning)' }} />
      </div>
      <h2 style={{ margin: '0 0 0.5rem 0', fontSize: '1.25rem', fontWeight: 600, color: 'var(--text-primary)' }}>{title}</h2>
      <p style={{ margin: 0, maxWidth: 460, fontSize: '0.875rem', lineHeight: 1.5 }}>{description}</p>
      <span style={{ marginTop: '1.25rem', padding: '0.2rem 0.6rem', borderRadius: 'var(--radius-full)', fontSize: '0.7rem', fontWeight: 600, background: 'var(--bg-tertiary)', border: '1px solid var(--border-color)', color: 'var(--text-secondary)', textTransform: 'uppercase', letterSpacing: '0.05em' }}>
        Écran en cours
      </span>
    </div>
  );
}

export default App;
