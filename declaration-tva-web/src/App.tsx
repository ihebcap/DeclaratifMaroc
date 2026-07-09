import { useState } from 'react';
import { LogOut, LayoutDashboard, ArrowLeft } from 'lucide-react';
import './index.css';
import './App.css';
import { Auth } from './Auth';
import { DeclarationList } from './DeclarationList';
import { CreateDeclarationModal } from './CreateDeclarationModal';
import { DeclarationStepper } from './DeclarationStepper';

export interface User {
  login: string;
  nom: string;
  societeId: string;
  societeName: string;
  token: string;
}

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
  const [currentDeclarationId, setCurrentDeclarationId] = useState<string | null>(null);
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);

  return (
    <div className="app-container animate-fade-in">
      <aside className={`app-sidebar ${isSidebarOpen ? '' : 'collapsed'}`}>
        <div className="sidebar-header" style={isSidebarOpen ? {cursor: 'pointer', flexDirection: 'column', alignItems: 'stretch', justifyContent: 'flex-start'} : {cursor: 'pointer'}} onClick={() => setIsSidebarOpen(!isSidebarOpen)}>
          {isSidebarOpen ? (
            <>
              <div style={{display: 'flex', alignItems: 'center', justifyContent: 'space-between'}}>
                <div className="flex items-center gap-2">
                  <LayoutDashboard size={24} style={{color: 'var(--accent-primary)'}} />
                  <span>TVA</span>
                </div>
              </div>
              <div style={{fontSize: '0.75rem', fontWeight: 500, color: 'var(--text-secondary)', paddingLeft: 'calc(24px + 0.5rem)', marginTop: '0.25rem', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis'}}>
                {user.societeName}
              </div>
            </>
          ) : (
            <LayoutDashboard size={24} style={{color: 'var(--accent-primary)'}} />
          )}
        </div>

        <div className="sidebar-menu">
          <div className={`sidebar-item ${!currentDeclarationId ? 'active' : ''}`} title="Mes Déclarations" onClick={() => setCurrentDeclarationId(null)}>
            <LayoutDashboard size={18} />
            <span className="sidebar-text">Mes Déclarations</span>
          </div>
          {currentDeclarationId && (
            <div className="sidebar-item active" title="Déclaration en cours">
              <span className="sidebar-text" style={{ paddingLeft: '1.5rem', fontSize: '0.875rem', color: 'var(--accent-primary)' }}>Déclaration en cours</span>
            </div>
          )}
        </div>
        
        <div className="sidebar-footer">
          {isSidebarOpen ? (
            <>
              <div style={{fontWeight: 600, fontSize: '0.875rem', color: 'var(--text-primary)'}}>
                {user.nom}
              </div>
              <button onClick={onLogout} className="btn" style={{backgroundColor: 'var(--bg-tertiary)', color: 'var(--text-primary)', border: '1px solid var(--border-color)', padding: '0.5rem', display: 'flex', alignItems: 'center', justifyContent: 'center', gap: '0.5rem'}}>
                <LogOut size={16} />
                <span>Déconnexion</span>
              </button>
            </>
          ) : (
            <button onClick={onLogout} className="btn" style={{backgroundColor: 'transparent', color: 'var(--text-primary)', padding: '0.5rem', display: 'flex', alignItems: 'center', justifyContent: 'center'}} title="Déconnexion">
              <LogOut size={16} />
            </button>
          )}
        </div>
      </aside>

      <main className="main-content" style={{ display: 'flex', flexDirection: 'column' }}>
        {currentDeclarationId ? (
          <div style={{ flex: 1, display: 'flex', flexDirection: 'column', height: '100%', overflow: 'hidden' }}>
            <div style={{ padding: '1rem', borderBottom: '1px solid var(--border-color)', background: 'white' }}>
              <button 
                onClick={() => setCurrentDeclarationId(null)}
                className="btn"
                style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', background: 'transparent', border: 'none', color: 'var(--text-secondary)', cursor: 'pointer', fontSize: '0.875rem' }}
              >
                <ArrowLeft size={16} /> Retour aux déclarations
              </button>
            </div>
            <DeclarationStepper declarationId={currentDeclarationId} showToast={showToast} onBack={() => setCurrentDeclarationId(null)} />
          </div>
        ) : (
          <DeclarationList 
            societeId={user.societeId} 
            onOpenDeclaration={(id) => setCurrentDeclarationId(id)} 
            onCreateNew={() => setIsCreateModalOpen(true)}
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

export default App;
