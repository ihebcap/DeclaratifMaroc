import { useCallback, useEffect, useState } from 'react';
import {
  LogOut, LayoutDashboard, Landmark, FileText, FileCheck,
  Scissors, Send, BarChart3, Lock, ShieldAlert, AlertTriangle, CalendarClock, ChevronDown, ChevronRight, BookOpen,
} from 'lucide-react';
import './index.css';
import './App.css';
import { Auth } from './Auth';
import { DeclarationList } from './DeclarationList';
import { CreateDeclarationModal } from './CreateDeclarationModal';
import { DeclarationStepper } from './DeclarationStepper';
import { RapprochementInterrogation } from './RapprochementInterrogation';
import { FactureInterrogation } from './FactureInterrogation';
import { DeclarationsDelaiPaiementPanel } from './DeclarationsDelaiPaiementPanel';
import { ControleLignesDelaiPaiementPanel } from './ControleLignesDelaiPaiementPanel';
import { ConventionsDelaiPaiementPanel } from './ConventionsDelaiPaiementPanel';
import { getLicenceStatus, type LicenceStatusDto } from './api';

export interface User {
  login: string;
  nom: string;
  societeId: number;
  societeName: string;
  token: string;
  isAdmin: boolean;
}

// Sections navigables (le shell ne fait que router — aucun calcul/API ici).
type SectionKey = 'rapprochement' | 'factures' | 'declaration' | 'delai-paiement';

// TASK-136 : sous-écrans internes au domaine « Délai de paiement ». Demande PO (revenue sur la
// décision précédente) : un groupe DÉDIÉ dans la sidebar avec ces 3 sous-menus, à la place de la
// barre d'onglets interne au-dessus du contenu (supprimée).
type DdpSousEcran = 'declarations' | 'controle' | 'conventions';

type MenuStatus = 'live' | 'soon' | 'todo';

interface MenuEntry {
  key: SectionKey | string;
  label: string;
  icon: typeof LayoutDashboard;
  status: MenuStatus; // live = livré, soon = placeholder honnête, todo = grisé "à venir"
  ddpSousEcran?: DdpSousEcran; // posé uniquement sur les 3 entrées du groupe « Délai de paiement »
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
    title: 'DÉCLARATION TVA',
    entries: [
      { key: 'declaration', label: 'Déclaration TVA', icon: FileCheck, status: 'live' },
    ],
  },
  {
    // TASK-136 : groupe dédié (demande PO) — 3 sous-menus au même niveau que les autres groupes,
    // remplace la barre d'onglets interne précédemment affichée en haut du contenu.
    title: 'DÉLAI DE PAIEMENT',
    entries: [
      { key: 'delai-paiement', label: 'Déclaration', icon: CalendarClock, status: 'live', ddpSousEcran: 'declarations' },
      { key: 'delai-paiement', label: 'Contrôle', icon: CalendarClock, status: 'live', ddpSousEcran: 'controle' },
      { key: 'delai-paiement', label: 'Conventions', icon: CalendarClock, status: 'live', ddpSousEcran: 'conventions' },
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

  // TASK-117 : statut de licence ApLicence, lu une fois au chargement — le blocage s'applique
  // indépendamment de l'authentification (CDC ApLicence §1.3), donc vérifié avant l'écran de
  // connexion. `null` = vérification en cours ; le point de contrôle back (middleware) reste la
  // seule vraie barrière, ce check front n'est qu'un rendu honnête du message renvoyé par l'API.
  const [licenceStatus, setLicenceStatus] = useState<LicenceStatusDto | null>(null);

  useEffect(() => {
    let cancelled = false;
    getLicenceStatus()
      .then(status => { if (!cancelled) setLicenceStatus(status); })
      .catch((err: any) => {
        // Le middleware de blocage (Program.cs) répond 503 + { message } quand la licence est
        // invalide — axios traite ce statut comme une erreur, d'où ce catch. Serveur totalement
        // injoignable : même traitement, jamais "valide par défaut" (fail-closed, cf. back).
        if (!cancelled) {
          setLicenceStatus({
            estValide: false,
            message: err?.response?.data?.message ?? 'Merci de vérifier la licence',
            alerteProcheExpiration: false,
            joursRestants: null,
            dateExpiration: null,
          });
        }
      });
    return () => { cancelled = true; };
  }, []);

  // TASK-125 : mémoïsée (useCallback) — une identité recréée à chaque rendu d'App propage un
  // nouveau showToast jusqu'à ReglementsSelection.fetchAll (dépendance de son useCallback), qui
  // redéclenche alors le useEffect de fetch. Si ce fetch échoue et affiche un toast, App se
  // re-rend, régénère showToast, et reboucle — tempête de requêtes auto-entretenue confirmée en
  // reproduction réelle (~1000 requêtes /rapprochement en quelques secondes), qui laisse la grille
  // et la sélection bloquées à 0 durablement même après retour à la normale du réseau.
  const showToast = useCallback((message: string, type: 'success'|'error'|'warning' = 'success') => {
    setToast({ message, type });
    setTimeout(() => setToast(null), 3000);
  }, []);

  const handleLogin = (u: User) => {
    sessionStorage.setItem('tva_user', JSON.stringify(u));
    setUser(u);
  };

  const handleLogout = () => {
    sessionStorage.removeItem('tva_user');
    setUser(null);
  };

  // TASK-117 : le blocage de licence est indépendant de l'authentification (CDC ApLicence §1.3) —
  // vérifié avant même l'écran de connexion. Tant que le premier check n'a pas répondu,
  // `licenceStatus` vaut `null` (état "Inconnue" côté back également, fail-closed par défaut).
  if (licenceStatus === null) {
    return <LicenceGate variant="loading" />;
  }
  if (!licenceStatus.estValide) {
    return <LicenceGate variant="blocked" message={licenceStatus.message} />;
  }

  if (!user) {
    return (
      <>
        {licenceStatus.alerteProcheExpiration && (
          <LicenceExpirationBanner joursRestants={licenceStatus.joursRestants} dateExpiration={licenceStatus.dateExpiration} />
        )}
        <Auth onLogin={handleLogin} />
      </>
    );
  }

  return (
    <>
      {licenceStatus.alerteProcheExpiration && (
        <LicenceExpirationBanner joursRestants={licenceStatus.joursRestants} dateExpiration={licenceStatus.dateExpiration} />
      )}
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

// TASK-117 : point de rendu unique du blocage de licence (message fixe renvoyé par l'API,
// "Merci de vérifier la licence") — écran plein, aucune donnée métier affichée derrière.
function LicenceGate({ variant, message }: { variant: 'loading' | 'blocked'; message?: string | null }) {
  return (
    <div style={{
      position: 'fixed', inset: 0, zIndex: 10000, display: 'flex', flexDirection: 'column',
      alignItems: 'center', justifyContent: 'center', textAlign: 'center', padding: '2rem',
      backgroundColor: 'var(--bg-primary, #ffffff)', color: 'var(--text-primary)',
    }}>
      <ShieldAlert size={48} style={{ color: variant === 'blocked' ? 'var(--danger-color, #ef4444)' : 'var(--text-secondary)', marginBottom: '1rem' }} />
      <h1 style={{ margin: '0 0 0.5rem 0', fontSize: '1.25rem', fontWeight: 600 }}>
        {variant === 'loading' ? 'Vérification de la licence…' : (message || 'Merci de vérifier la licence')}
      </h1>
      {variant === 'blocked' && (
        <p style={{ margin: 0, maxWidth: 460, fontSize: '0.875rem', lineHeight: 1.5, color: 'var(--text-secondary)' }}>
          L'accès à l'application est bloqué tant qu'aucune licence valide n'a été vérifiée.
          Contactez votre administrateur.
        </p>
      )}
    </div>
  );
}

// TASK-117 : bannière d'alerte J-30 (CDC ApLicence §1.4) — n'apparaît jamais bloquante,
// affichée uniquement quand licenceStatus.estValide === true.
function LicenceExpirationBanner({ joursRestants, dateExpiration }: { joursRestants: number | null; dateExpiration: string | null }) {
  const dateTexte = dateExpiration ? new Date(dateExpiration).toLocaleDateString('fr-FR') : null;
  return (
    <div style={{
      display: 'flex', alignItems: 'center', gap: '0.5rem', padding: '0.5rem 1rem',
      backgroundColor: '#fef3c7', color: 'var(--status-warning-text)', fontSize: '0.8125rem', fontWeight: 500,
    }}>
      <AlertTriangle size={16} />
      <span>
        Licence proche de l'expiration
        {joursRestants !== null ? ` — ${joursRestants} jour${joursRestants > 1 ? 's' : ''} restant${joursRestants > 1 ? 's' : ''}` : ''}
        {dateTexte ? ` (échéance le ${dateTexte})` : ''}.
      </span>
    </div>
  );
}

// TASK-136 : exportée pour permettre au harnais de test e2e (task136-harness.tsx) de monter le
// VRAI shell de navigation sans rejouer tout le flux licence/connexion (déjà couvert par
// declaration.spec.ts) — même esprit que l'export de composants dédiés par TASK-130/134.
export function Dashboard({ user, onLogout, showToast }: { user: User; onLogout: () => void; showToast: (msg: string, type?: 'success'|'error'|'warning') => void }) {
  const [isSidebarOpen, setIsSidebarOpen] = useState(true);
  const [activeSection, setActiveSection] = useState<SectionKey>('declaration');
  const [currentDeclarationId, setCurrentDeclarationId] = useState<string | null>(null);
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  // TASK-136 : sous-écran actif au sein de la section « Délai de paiement » (sous-navigation
  // interne, cf. DdpSousEcran) — indépendant de activeSection, ne modifie rien au sidebar.
  const [ddpSousEcran, setDdpSousEcran] = useState<DdpSousEcran>('declarations');
  // Groupes de la sidebar pliables (demande PO) — un groupe replié masque ses entrées mais reste
  // cliquable pour se redéplier ; tous les groupes démarrent dépliés (aucune route cachée au premier
  // chargement).
  const [collapsedGroups, setCollapsedGroups] = useState<Set<string>>(new Set());
  const toggleGroup = (title: string) => {
    setCollapsedGroups(prev => {
      const next = new Set(prev);
      if (next.has(title)) next.delete(title);
      else next.add(title);
      return next;
    });
  };

  const handleSelect = (entry: MenuEntry) => {
    if (entry.status === 'todo') return; // "à venir" : non cliquable, aucune route morte
    setActiveSection(entry.key as SectionKey);
    if (entry.ddpSousEcran) setDdpSousEcran(entry.ddpSousEcran);
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
              {isSidebarOpen && (
                <div
                  className="sidebar-group-label"
                  onClick={() => toggleGroup(group.title)}
                  style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', cursor: 'pointer' }}
                  title={collapsedGroups.has(group.title) ? 'Déplier' : 'Replier'}
                >
                  <span>{group.title}</span>
                  {collapsedGroups.has(group.title) ? <ChevronRight size={13} /> : <ChevronDown size={13} />}
                </div>
              )}
              {(!isSidebarOpen || !collapsedGroups.has(group.title)) && group.entries.map(entry => {
                const Icon = entry.icon;
                // Les 3 entrées du groupe « Délai de paiement » partagent la même SectionKey :
                // l'entrée active se distingue par son ddpSousEcran, pas par activeSection seul.
                const isActive = entry.status !== 'todo' && activeSection === entry.key
                  && (entry.ddpSousEcran === undefined || entry.ddpSousEcran === ddpSousEcran);
                const disabled = entry.status === 'todo';
                return (
                  <div
                    key={entry.key + (entry.ddpSousEcran ?? '')}
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
          <button
            onClick={() => {
              const baseUrl = import.meta.env.BASE_URL || '/';
              const targetUrl = baseUrl.endsWith('/') ? `${baseUrl}guide-fonctionnel-tva.html` : `${baseUrl}/guide-fonctionnel-tva.html`;
              window.open(targetUrl, '_blank', 'noopener');
            }}
            className="btn"
            style={{
              backgroundColor: 'var(--bg-secondary)',
              color: 'var(--text-primary)',
              border: '1px solid var(--border-color)',
              padding: '0.45rem 0.6rem',
              display: 'flex',
              alignItems: 'center',
              justifyContent: isSidebarOpen ? 'flex-start' : 'center',
              gap: '0.5rem',
              width: '100%',
              marginBottom: '0.65rem',
              fontSize: '0.8125rem',
              fontWeight: 600,
              cursor: 'pointer',
              borderRadius: '6px',
            }}
            title="Guide fonctionnel TVA"
          >
            <BookOpen size={16} style={{ color: 'var(--accent-primary)', flexShrink: 0 }} />
            {isSidebarOpen && <span>Guide fonctionnel</span>}
          </button>
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
              onSelectDeclaration={(id: string) => setCurrentDeclarationId(id)}
              onCreateNew={() => setIsCreateModalOpen(true)}
              showToast={showToast}
            />
          )
        ) : activeSection === 'rapprochement' ? (
          <RapprochementInterrogation societeId={user.societeId} showToast={showToast} />
        ) : activeSection === 'factures' ? (
          <FactureInterrogation societeId={user.societeId} showToast={showToast} />
        ) : activeSection === 'delai-paiement' ? (
          // TASK-136 : la navigation entre les 3 écrans se fait désormais depuis le groupe dédié de
          // la sidebar (demande PO) — plus de barre d'onglets interne ici, ce bloc ne fait que router.
          <div style={{ flex: 1, minHeight: 0, overflow: 'hidden', display: 'flex' }}>
            {ddpSousEcran === 'declarations' ? (
              <DeclarationsDelaiPaiementPanel societeId={user.societeId} showToast={showToast} />
            ) : ddpSousEcran === 'controle' ? (
              <ControleLignesDelaiPaiementPanel societeId={user.societeId} showToast={showToast} />
            ) : (
              <ConventionsDelaiPaiementPanel societeId={user.societeId} showToast={showToast} />
            )}
          </div>
        ) : null}
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
