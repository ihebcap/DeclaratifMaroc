import { useState, useEffect, useCallback, useRef } from 'react';
import { Loader2, X, AlertTriangle, Paperclip, Plus, Search, Trash2, CalendarClock } from 'lucide-react';
import { ExcelFilter } from './ExcelFilter';
import { ColumnSelector } from './ColumnSelector';
import { useColumnPrefs } from './useColumnPrefs';
import { formatDate } from './utils';
import api, {
  getConventionsDelaiPaiement, searchTiersConvention, getFacturesNonPayees,
  creerConventionDelaiPaiement, terminerConventionDelaiPaiement, supprimerConventionDelaiPaiement,
  urlFichierConventionDelaiPaiement,
} from './api';
import type {
  ConventionDelaiPaiementDto, DomaineConvention, TypeConvention, TiersRechercheDto,
  FactureNonPayeeDto,
} from './api';

// ─── TASK-130 (Délai de Paiement Maroc — Convention par tiers, FRONT) ──────────────────────────
//
// Écran de la sous-fonctionnalité Convention (CDC §3.2, §6) : liste (CRUD Convention/Facture) +
// action « Terminer » (clôture anticipée). Consomme le contrôleur TASK-130
// (ConventionsDelaiPaiementController), lui-même passe-plat vers IConventionDelaiPaiementService
// (TASK-129 — tout le métier réel, plafond/chevauchement/unicité/bornes clôture, vit là-bas et
// n'est PAS dupliqué ici). Densité « 0 espace perdu », pas de panneau latéral, composants de
// grille déjà disponibles (ExcelFilter/ColumnSelector/useColumnPrefs, TASK-068).
//
// Hors périmètre strict de cette TASK (documenté, pas un oubli) : branchement au menu (TASK-136),
// résolution du délai (déjà côté back), écran de sélection DDP (TASK-134).

// Reproduit Declaration.Core.ConventionDelaiPaiementValidator.PlafondJours — UNIQUEMENT pour un
// retour immédiat à l'écran (pas d'aller-retour serveur pour ce contrôle simple, Étape 2 de la
// TASK). La vérité métier reste le contrôle serveur (TASK-129) : toute divergence future entre
// cette constante et le back resterait couverte, le serveur rejetterait quand même en 409/400.
const PLAFOND_JOURS = 180;

type Col = { key: string; label: string; width?: string; align?: 'left' | 'right' | 'center'; filterType?: 'list' | 'text' };

const COLUMNS: Col[] = [
  { key: 'tiers', label: 'Tiers', filterType: 'text' },
  { key: 'type', label: 'Type', filterType: 'list', width: '110px', align: 'center' },
  { key: 'numero', label: 'N° convention', width: '150px' },
  { key: 'periodeOuFacture', label: 'Dates / N° facture', width: '230px' },
  { key: 'delai', label: 'Délai (j)', align: 'right', width: '90px' },
  { key: 'valide', label: 'Statut', align: 'center', width: '110px', filterType: 'list' },
  { key: 'piece', label: 'Pièce jointe', align: 'center', width: '120px' },
  { key: 'actions', label: 'Actions', width: '170px' },
];

const colStyle = (col: Col): React.CSSProperties =>
  col.width ? { flex: `0 0 ${col.width}`, width: col.width } : { flex: '1 1 0', minWidth: '160px' };
const colJustify = (col: Col) => (col.align === 'right' ? 'flex-end' : col.align === 'center' ? 'center' : 'flex-start');

function TypeBadge({ type }: { type: TypeConvention }) {
  const isConvention = type === 'Convention';
  return (
    <span style={{
      background: isConvention ? '#e0e7ff' : '#fef3c7', color: isConvention ? '#4338ca' : '#b45309',
      padding: '2px 8px', borderRadius: '4px', fontSize: '0.7rem', fontWeight: 600,
    }}>
      {isConvention ? 'Convention' : 'Facture'}
    </span>
  );
}

function ValideBadge({ valide }: { valide: boolean }) {
  return (
    <span style={{
      background: valide ? 'var(--status-ok-bg)' : 'var(--status-blocking-bg)',
      color: valide ? 'var(--status-ok-text)' : 'var(--status-blocking-text)',
      padding: '2px 8px', borderRadius: '99px', fontSize: '0.7rem', fontWeight: 600,
    }}>
      {valide ? 'Valide' : 'Expirée'}
    </span>
  );
}

export function ConventionsDelaiPaiementPanel({ societeId, showToast }: {
  societeId: number,
  showToast: (m: string, t?: 'success' | 'error' | 'warning') => void,
}) {
  const [domaine, setDomaine] = useState<DomaineConvention>('achat');
  const [rows, setRows] = useState<ConventionDelaiPaiementDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [filters, setFilters] = useState<Record<string, string | string[]>>({});
  const [sortConfig, setSortConfig] = useState<{ key: 'numero' | 'delai' | 'tiers', desc: boolean }>({ key: 'numero', desc: false });

  const [showCreate, setShowCreate] = useState(false);
  const [terminerTarget, setTerminerTarget] = useState<ConventionDelaiPaiementDto | null>(null);
  const [deleteBusyId, setDeleteBusyId] = useState<number | null>(null);
  const [downloadBusyId, setDownloadBusyId] = useState<number | null>(null);

  const { visibleColumns, visibleKeys, toggle, reset } = useColumnPrefs('grf.cols.conventionsDelaiPaiement', COLUMNS);

  const fetchRows = useCallback(async () => {
    setLoading(true);
    try {
      const items = await getConventionsDelaiPaiement(societeId, domaine);
      setRows(items);
    } catch (e) {
      console.error(e);
      showToast('Erreur lors du chargement des conventions', 'error');
      setRows([]);
    } finally {
      setLoading(false);
    }
  }, [societeId, domaine, showToast]);

  useEffect(() => { fetchRows(); }, [fetchRows]);

  const handleFilterChange = (key: string, val: any) => {
    setFilters(prev => {
      const next = { ...prev };
      if (val === '' || (Array.isArray(val) && val.length === 0)) delete next[key];
      else next[key] = val;
      return next;
    });
  };

  const filterOptionsFor = (key: string): { label: string, value: string }[] => {
    if (key === 'type') return [{ label: 'Convention', value: 'Convention' }, { label: 'Facture', value: 'Facture' }];
    if (key === 'valide') return [{ label: 'Valide', value: 'Valide' }, { label: 'Expirée', value: 'Expiree' }];
    return [];
  };

  // Liste courte (pas de pagination côté back — l'écran de liste conventions ne porte pas de volume
  // comparable aux grilles de règlements/factures), filtre/tri intégralement CLIENT.
  const visibleRows = rows.filter(r => {
    const tiersFilter = filters['tiers'];
    if (typeof tiersFilter === 'string' && tiersFilter.trim() !== '') {
      const needle = tiersFilter.trim().toLowerCase();
      if (!r.tiersCode.toLowerCase().includes(needle)) return false;
    }
    const typeFilter = filters['type'];
    if (Array.isArray(typeFilter) && typeFilter.length > 0 && !typeFilter.includes(r.type)) return false;
    const valideFilter = filters['valide'];
    if (Array.isArray(valideFilter) && valideFilter.length > 0) {
      const v = r.valide ? 'Valide' : 'Expiree';
      if (!valideFilter.includes(v)) return false;
    }
    return true;
  }).sort((a, b) => {
    let cmp = 0;
    if (sortConfig.key === 'numero') cmp = a.numero.localeCompare(b.numero);
    else if (sortConfig.key === 'delai') cmp = a.nombreJoursDelaisPaiement - b.nombreJoursDelaisPaiement;
    else if (sortConfig.key === 'tiers') cmp = a.tiersCode.localeCompare(b.tiersCode);
    return sortConfig.desc ? -cmp : cmp;
  });

  const handleSort = (key: 'numero' | 'delai' | 'tiers') => {
    setSortConfig(prev => prev.key === key ? { key, desc: !prev.desc } : { key, desc: false });
  };

  const handleDownload = async (row: ConventionDelaiPaiementDto) => {
    setDownloadBusyId(row.cpId);
    try {
      // Téléchargement authentifié via blob (JWT porté par l'intercepteur axios, cf. api.ts) —
      // un <a href> brut ne porterait pas l'en-tête Authorization (pattern DeclarationFinalePanel.tsx).
      const res = await api.get(urlFichierConventionDelaiPaiement(row.cpId), { responseType: 'blob' });
      const blobUrl = URL.createObjectURL(res.data);
      const a = document.createElement('a');
      a.href = blobUrl;
      a.download = `convention-${row.numero || row.cpId}.pdf`;
      document.body.appendChild(a);
      a.click();
      a.remove();
      URL.revokeObjectURL(blobUrl);
    } catch (e) {
      console.error(e);
      showToast('Impossible de télécharger la pièce jointe', 'error');
    } finally {
      setDownloadBusyId(null);
    }
  };

  const handleDelete = async (row: ConventionDelaiPaiementDto) => {
    if (!window.confirm(`Supprimer la convention [${row.numero}] ? Cette action est irréversible (aucune garde côté serveur, cf. TASK-129).`)) return;
    setDeleteBusyId(row.cpId);
    try {
      await supprimerConventionDelaiPaiement(row.cpId);
      showToast('Convention supprimée', 'success');
      await fetchRows();
    } catch (e: any) {
      console.error(e);
      showToast(e?.response?.data?.Message || 'Erreur lors de la suppression', 'error');
    } finally {
      setDeleteBusyId(null);
    }
  };

  const renderCell = (col: Col, row: ConventionDelaiPaiementDto) => {
    switch (col.key) {
      case 'tiers':
        return <span><strong style={{ fontWeight: 600 }}>{row.tiersCode}</strong></span>;
      case 'type':
        return <TypeBadge type={row.type} />;
      case 'numero':
        return row.numero;
      case 'periodeOuFacture':
        return row.type === 'Facture'
          ? <span>Facture <strong>{row.factureNumero || row.factureNo}</strong></span>
          : <span>{formatDate(row.dateDebut || '')} → {formatDate(row.dateFin || '')}</span>;
      case 'delai':
        return row.nombreJoursDelaisPaiement;
      case 'valide':
        return <ValideBadge valide={row.valide} />;
      case 'piece':
        return row.hasFile
          ? (
            <button
              className="btn"
              onClick={() => handleDownload(row)}
              disabled={downloadBusyId === row.cpId}
              title="Télécharger la pièce jointe"
              style={{ display: 'inline-flex', alignItems: 'center', gap: '0.3rem', fontSize: '0.75rem', padding: '0.2rem 0.5rem' }}
            >
              {downloadBusyId === row.cpId ? <Loader2 size={13} className="animate-spin" /> : <Paperclip size={13} />}
              PDF
            </button>
          )
          : <span style={{ color: 'var(--text-secondary)' }}>—</span>;
      case 'actions':
        return (
          <div style={{ display: 'flex', gap: '0.4rem' }}>
            {row.type === 'Convention' && row.valide && (
              <button
                className="btn"
                onClick={() => setTerminerTarget(row)}
                title="Clôture anticipée"
                style={{ display: 'inline-flex', alignItems: 'center', gap: '0.3rem', fontSize: '0.75rem', padding: '0.2rem 0.5rem' }}
              >
                <CalendarClock size={13} /> Terminer
              </button>
            )}
            <button
              className="btn"
              onClick={() => handleDelete(row)}
              disabled={deleteBusyId === row.cpId}
              title="Supprimer"
              style={{ display: 'inline-flex', alignItems: 'center', gap: '0.3rem', fontSize: '0.75rem', padding: '0.2rem 0.5rem', color: 'var(--status-blocking-text)' }}
            >
              {deleteBusyId === row.cpId ? <Loader2 size={13} className="animate-spin" /> : <Trash2 size={13} />}
            </button>
          </div>
        );
      default:
        return null;
    }
  };

  return (
    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', height: '100%', overflow: 'hidden' }}>
      {/* En-tête écran + bascule Achat/Vente */}
      <div style={{ padding: '0.75rem 1rem', borderBottom: '1px solid var(--border-color)', background: 'white', display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: '0.75rem' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.6rem' }}>
          <CalendarClock size={20} style={{ color: 'var(--accent-primary)' }} />
          <div>
            <h2 style={{ margin: 0, fontSize: '1.05rem', fontWeight: 600 }}>Conventions de délai de paiement</h2>
            <div style={{ fontSize: '0.72rem', color: 'var(--text-secondary)' }}>Délai de Paiement Maroc — CDC §3.2/§6, par tiers</div>
          </div>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
          <div style={{ display: 'inline-flex', border: '1px solid var(--border-color)', borderRadius: '4px', overflow: 'hidden' }}>
            <button
              onClick={() => setDomaine('achat')}
              style={{ padding: '0.3rem 0.75rem', fontSize: '0.8rem', border: 'none', cursor: 'pointer', background: domaine === 'achat' ? 'var(--accent-primary)' : 'white', color: domaine === 'achat' ? 'white' : 'var(--text-primary)' }}
            >
              Achat (fournisseurs)
            </button>
            <button
              onClick={() => setDomaine('vente')}
              style={{ padding: '0.3rem 0.75rem', fontSize: '0.8rem', border: 'none', cursor: 'pointer', background: domaine === 'vente' ? 'var(--accent-primary)' : 'white', color: domaine === 'vente' ? 'white' : 'var(--text-primary)' }}
            >
              Vente (clients)
            </button>
          </div>
          <button
            className="btn btn-primary"
            onClick={() => setShowCreate(true)}
            style={{ display: 'inline-flex', alignItems: 'center', gap: '0.35rem', fontSize: '0.8rem', padding: '0.3rem 0.75rem' }}
          >
            <Plus size={14} /> Nouvelle convention
          </button>
          <ColumnSelector columns={COLUMNS} visibleKeys={visibleKeys} onToggle={toggle} onReset={reset} />
        </div>
      </div>

      {/* Barre d'info */}
      <div style={{ padding: '0.4rem 1rem', borderBottom: '1px solid var(--border-color)', background: 'var(--bg-secondary)', display: 'flex', justifyContent: 'space-between', alignItems: 'center', fontSize: '0.8rem' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
          {loading && <Loader2 size={15} className="animate-spin" style={{ color: 'var(--accent-primary)' }} />}
          <span>Conventions : <strong>{visibleRows.length}</strong> / {rows.length}</span>
        </div>
        {Object.keys(filters).length > 0 && (
          <button className="btn" onClick={() => setFilters({})} style={{ background: 'transparent', border: 'none', color: 'var(--accent-primary)', textDecoration: 'underline', padding: 0, fontSize: '0.8rem', cursor: 'pointer' }}>
            Effacer filtres ({Object.keys(filters).length})
          </button>
        )}
      </div>

      {/* Grille (flexbox : entête + lignes partagent les mêmes largeurs, pattern TASK-138) */}
      <div style={{ flexGrow: 1, overflow: 'auto', position: 'relative', background: 'white' }}>
        <div style={{ minWidth: '1200px', fontSize: '0.8125rem' }}>
          <div style={{ display: 'flex', position: 'sticky', top: 0, background: 'var(--bg-secondary)', zIndex: 10, boxShadow: '0 1px 2px rgba(0,0,0,0.05)', borderBottom: '1px solid var(--border-color)' }}>
            {visibleColumns.map(col => {
              const sortKey = col.key === 'numero' || col.key === 'delai' || col.key === 'tiers' ? col.key as 'numero' | 'delai' | 'tiers' : undefined;
              return (
                <div
                  key={col.key}
                  onClick={() => sortKey && handleSort(sortKey)}
                  style={{ ...colStyle(col), padding: '0.5rem 0.75rem', borderRight: '1px solid var(--border-color)', cursor: sortKey ? 'pointer' : 'default', userSelect: 'none', display: 'flex', alignItems: 'center', justifyContent: 'center', gap: '0.25rem', fontWeight: 600, whiteSpace: 'nowrap' }}
                >
                  {col.label}
                  {sortKey && sortConfig.key === sortKey && (
                    <span style={{ fontSize: '0.7rem', color: 'var(--text-secondary)' }}>{sortConfig.desc ? '▼' : '▲'}</span>
                  )}
                  {col.filterType && (
                    <span onClick={e => e.stopPropagation()}>
                      <ExcelFilter
                        filterType={col.filterType}
                        options={filterOptionsFor(col.key)}
                        selectedValues={Array.isArray(filters[col.key]) ? filters[col.key] as string[] : []}
                        textValue={typeof filters[col.key] === 'string' ? filters[col.key] as string : ''}
                        onChange={(val) => handleFilterChange(col.key, val)}
                      />
                    </span>
                  )}
                </div>
              );
            })}
          </div>

          {visibleRows.map(row => (
            <div
              key={row.cpId}
              style={{ display: 'flex', borderBottom: '1px solid var(--border-color)', background: 'white' }}
              onMouseEnter={e => (e.currentTarget.style.background = 'var(--bg-secondary)')}
              onMouseLeave={e => (e.currentTarget.style.background = 'white')}
            >
              {visibleColumns.map(col => (
                <div key={col.key} style={{ ...colStyle(col), padding: '0.4rem 0.75rem', borderRight: '1px solid var(--border-color)', display: 'flex', alignItems: 'center', justifyContent: colJustify(col), whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                  {renderCell(col, row)}
                </div>
              ))}
            </div>
          ))}
        </div>
        {visibleRows.length === 0 && !loading && (
          <div style={{ padding: '3rem', textAlign: 'center', color: 'var(--text-secondary)' }}>
            Aucune convention {domaine === 'achat' ? 'fournisseur' : 'client'} pour ces filtres.
          </div>
        )}
      </div>

      {showCreate && (
        <CreerConventionModal
          societeId={societeId}
          domaine={domaine}
          onClose={() => setShowCreate(false)}
          onSuccess={async () => { setShowCreate(false); showToast('Convention créée', 'success'); await fetchRows(); }}
        />
      )}

      {terminerTarget && (
        <TerminerConventionModal
          convention={terminerTarget}
          onClose={() => setTerminerTarget(null)}
          onSuccess={async () => { setTerminerTarget(null); showToast('Convention clôturée', 'success'); await fetchRows(); }}
        />
      )}
    </div>
  );
}

// ─── Formulaire de création (Étape 2/3 de TASK-130) ────────────────────────────────────────────
//
// Bascule des champs visibles selon le type choisi (Convention → dates ; Facture → sélection
// facture non payée du tiers). Plafond 180j affiché en validation immédiate (pas d'attente
// serveur). Message d'erreur EXPLICITE en cas de chevauchement détecté par le back (Étape 3) —
// on affiche tel quel le message serveur (TASK-129 cite déjà la convention en conflit, numéro +
// période), jamais un message générique.
function CreerConventionModal({ societeId, domaine, onClose, onSuccess }: {
  societeId: number,
  domaine: DomaineConvention,
  onClose: () => void,
  onSuccess: () => void,
}) {
  const [type, setType] = useState<TypeConvention>('Convention');
  const [numero, setNumero] = useState('');
  const [date, setDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [dateDebut, setDateDebut] = useState('');
  const [dateFin, setDateFin] = useState('');
  const [delai, setDelai] = useState<number | ''>('');

  // Recherche tiers (débouncée) — Étape 2 : sans elle, le formulaire ne peut désigner aucun tiers
  // (endpoint additif TASK-130, cf. commentaire de classe du contrôleur).
  const [tiersRecherche, setTiersRecherche] = useState('');
  const [tiersOptions, setTiersOptions] = useState<TiersRechercheDto[]>([]);
  const [tiersLoading, setTiersLoading] = useState(false);
  const [tiersSelectionne, setTiersSelectionne] = useState<TiersRechercheDto | null>(null);

  // Factures non payées du tiers sélectionné (type Facture uniquement).
  const [factures, setFactures] = useState<FactureNonPayeeDto[]>([]);
  const [facturesLoading, setFacturesLoading] = useState(false);
  const [factureSelectionnee, setFactureSelectionnee] = useState<FactureNonPayeeDto | null>(null);

  const [fichierNom, setFichierNom] = useState<string | null>(null);
  const [fichierBase64, setFichierBase64] = useState<string | null>(null);

  const [erreur, setErreur] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    if (debounceRef.current) clearTimeout(debounceRef.current);
    if (tiersSelectionne) { setTiersOptions([]); return; }
    if (tiersRecherche.trim().length < 1) { setTiersOptions([]); return; }
    debounceRef.current = setTimeout(async () => {
      setTiersLoading(true);
      try {
        const res = await searchTiersConvention(societeId, domaine, tiersRecherche.trim());
        setTiersOptions(res);
      } catch {
        setTiersOptions([]);
      } finally {
        setTiersLoading(false);
      }
    }, 300);
    return () => { if (debounceRef.current) clearTimeout(debounceRef.current); };
  }, [tiersRecherche, tiersSelectionne, societeId, domaine]);

  useEffect(() => {
    if (type !== 'Facture' || !tiersSelectionne) { setFactures([]); setFactureSelectionnee(null); return; }
    (async () => {
      setFacturesLoading(true);
      try {
        const res = await getFacturesNonPayees(societeId, tiersSelectionne.ctNo, domaine);
        setFactures(res);
      } catch {
        setFactures([]);
      } finally {
        setFacturesLoading(false);
      }
    })();
  }, [type, tiersSelectionne, societeId, domaine]);

  const handleFichierChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) { setFichierNom(null); setFichierBase64(null); return; }
    const reader = new FileReader();
    reader.onload = () => {
      const result = reader.result as string;
      // "data:application/pdf;base64,XXXX" → on ne garde que la partie base64.
      const base64 = result.split(',')[1] || '';
      setFichierNom(file.name);
      setFichierBase64(base64);
    };
    reader.readAsDataURL(file);
  };

  // Validation immédiate CÔTÉ FRONT (Étape 2 : plafond 180j sans aller-retour serveur). Miroir de
  // Declaration.Core.ConventionDelaiPaiementValidator — la vérité reste le contrôle serveur.
  const validerLocalement = (): string | null => {
    if (!tiersSelectionne) return 'Le tiers est obligatoire.';
    if (!numero.trim()) return 'Le numéro de la convention est obligatoire.';
    if (delai === '' || Number(delai) <= 0) return 'Le délai de paiement doit être strictement positif.';
    if (Number(delai) > PLAFOND_JOURS) return `Le délai de paiement ne doit pas dépasser ${PLAFOND_JOURS} jours.`;
    if (type === 'Convention') {
      if (!dateDebut) return 'La date début est obligatoire pour une convention.';
      if (!dateFin) return 'La date fin est obligatoire pour une convention.';
      if (dateFin < dateDebut) return 'La date fin doit être supérieure ou égale à la date début.';
    } else {
      if (!factureSelectionnee) return 'Le numéro de facture est obligatoire pour une convention de type Facture.';
    }
    return null;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErreur(null);
    const erreurLocale = validerLocalement();
    if (erreurLocale) { setErreur(erreurLocale); return; }

    setSubmitting(true);
    try {
      await creerConventionDelaiPaiement({
        soId: societeId,
        tiersNo: tiersSelectionne!.ctNo,
        tiersCode: tiersSelectionne!.ctCode,
        date,
        numero: numero.trim(),
        dateDebut: type === 'Convention' ? dateDebut : null,
        dateFin: type === 'Convention' ? dateFin : null,
        nombreJoursDelaisPaiement: Number(delai),
        domaine,
        type,
        factureNo: type === 'Facture' ? factureSelectionnee!.ecId : null,
        fileName: fichierNom,
        fileBase64: fichierBase64,
      });
      onSuccess();
    } catch (err: any) {
      console.error(err);
      // Étape 3 : message serveur affiché TEL QUEL — TASK-129 cite déjà la convention en conflit
      // (numéro + période) pour un chevauchement, jamais un message générique ici.
      const msg = err?.response?.data?.Message || err?.response?.data?.message || 'Erreur lors de la création de la convention.';
      setErreur(msg);
    } finally {
      setSubmitting(false);
    }
  };

  const inputStyle: React.CSSProperties = {};
  const labelStyle: React.CSSProperties = { fontSize: '0.8rem', fontWeight: 500, color: 'var(--text-primary)' };

  return (
    <div style={{ position: 'fixed', inset: 0, zIndex: 9999, display: 'flex', alignItems: 'center', justifyContent: 'center', backgroundColor: 'rgba(0,0,0,0.5)' }}>
      <div style={{ background: 'white', borderRadius: '8px', width: '100%', maxWidth: '520px', maxHeight: '90vh', overflowY: 'auto', boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1), 0 10px 10px -5px rgba(0,0,0,0.04)' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '1.25rem 1.5rem', borderBottom: '1px solid var(--border-color)' }}>
          <h3 style={{ margin: 0, fontSize: '1.15rem', fontWeight: 600 }}>Nouvelle convention {domaine === 'achat' ? '(fournisseur)' : '(client)'}</h3>
          <button onClick={onClose} style={{ background: 'transparent', border: 'none', cursor: 'pointer', color: 'var(--text-secondary)' }}><X size={20} /></button>
        </div>

        <form onSubmit={handleSubmit} style={{ padding: '1.5rem', display: 'flex', flexDirection: 'column', gap: '1rem' }}>
          {erreur && (
            <div style={{ padding: '0.75rem 1rem', background: 'var(--status-blocking-bg)', color: 'var(--status-blocking-text)', borderRadius: '4px', fontSize: '0.85rem', display: 'flex', gap: '0.5rem' }}>
              <AlertTriangle size={16} style={{ flexShrink: 0, marginTop: '1px' }} />
              <span>{erreur}</span>
            </div>
          )}

          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.4rem' }}>
            <label style={labelStyle}>Type</label>
            <div style={{ display: 'inline-flex', border: '1px solid var(--border-color)', borderRadius: '4px', overflow: 'hidden', width: 'fit-content' }}>
              <button type="button" onClick={() => setType('Convention')} style={{ padding: '0.35rem 0.9rem', fontSize: '0.85rem', border: 'none', cursor: 'pointer', background: type === 'Convention' ? 'var(--accent-primary)' : 'white', color: type === 'Convention' ? 'white' : 'var(--text-primary)' }}>Convention</button>
              <button type="button" onClick={() => setType('Facture')} style={{ padding: '0.35rem 0.9rem', fontSize: '0.85rem', border: 'none', cursor: 'pointer', background: type === 'Facture' ? 'var(--accent-primary)' : 'white', color: type === 'Facture' ? 'white' : 'var(--text-primary)' }}>Facture (dérogation ponctuelle)</button>
            </div>
          </div>

          {/* Sélection tiers — obligatoire quel que soit le type */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.4rem' }}>
            <label style={labelStyle}>Tiers {domaine === 'achat' ? '(fournisseur)' : '(client)'}</label>
            {tiersSelectionne ? (
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', padding: '0.4rem 0.6rem', background: 'var(--bg-secondary)', borderRadius: '4px', fontSize: '0.85rem' }}>
                <strong>{tiersSelectionne.ctCode}</strong>
                <span style={{ color: 'var(--text-secondary)' }}>{tiersSelectionne.ctIntitule}</span>
                <button type="button" onClick={() => { setTiersSelectionne(null); setTiersRecherche(''); }} style={{ marginLeft: 'auto', background: 'transparent', border: 'none', cursor: 'pointer', color: 'var(--accent-primary)', fontSize: '0.78rem' }}>Changer</button>
              </div>
            ) : (
              <div style={{ position: 'relative' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '0.4rem' }}>
                  <Search size={14} style={{ color: 'var(--text-secondary)' }} />
                  <input
                    type="text"
                    placeholder="Code ou nom du tiers…"
                    value={tiersRecherche}
                    onChange={e => setTiersRecherche(e.target.value)}
                    className="form-input"
                    style={inputStyle}
                  />
                  {tiersLoading && <Loader2 size={14} className="animate-spin" style={{ color: 'var(--accent-primary)' }} />}
                </div>
                {tiersOptions.length > 0 && (
                  <div style={{ border: '1px solid var(--border-color)', borderRadius: '4px', marginTop: '0.3rem', maxHeight: '160px', overflowY: 'auto' }}>
                    {tiersOptions.map(t => (
                      <div
                        key={t.ctNo}
                        onClick={() => { setTiersSelectionne(t); setTiersOptions([]); }}
                        style={{ padding: '0.4rem 0.6rem', fontSize: '0.82rem', cursor: 'pointer', borderBottom: '1px solid var(--border-color)' }}
                        onMouseEnter={e => (e.currentTarget.style.background = 'var(--bg-secondary)')}
                        onMouseLeave={e => (e.currentTarget.style.background = 'white')}
                      >
                        <strong>{t.ctCode}</strong> — {t.ctIntitule}
                      </div>
                    ))}
                  </div>
                )}
              </div>
            )}
          </div>

          <div style={{ display: 'flex', gap: '1rem' }}>
            <div style={{ display: 'flex', flexDirection: 'column', gap: '0.4rem', flex: 1 }}>
              <label style={labelStyle}>N° convention</label>
              <input type="text" value={numero} onChange={e => setNumero(e.target.value)} className="form-input" required />
            </div>
            <div style={{ display: 'flex', flexDirection: 'column', gap: '0.4rem', flex: 1 }}>
              <label style={labelStyle}>Date de saisie</label>
              <input type="date" value={date} onChange={e => setDate(e.target.value)} className="form-input" required />
            </div>
          </div>

          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.4rem' }}>
            <label style={labelStyle}>Délai de paiement (jours, plafond {PLAFOND_JOURS})</label>
            <input
              type="number"
              min={1}
              max={PLAFOND_JOURS}
              value={delai}
              onChange={e => setDelai(e.target.value === '' ? '' : Number(e.target.value))}
              className="form-input"
              required
            />
            {delai !== '' && Number(delai) > PLAFOND_JOURS && (
              <span style={{ fontSize: '0.75rem', color: 'var(--status-blocking-text)' }}>Dépasse le plafond de {PLAFOND_JOURS} jours.</span>
            )}
          </div>

          {type === 'Convention' ? (
            <div style={{ display: 'flex', gap: '1rem' }}>
              <div style={{ display: 'flex', flexDirection: 'column', gap: '0.4rem', flex: 1 }}>
                <label style={labelStyle}>Date début</label>
                <input type="date" value={dateDebut} onChange={e => setDateDebut(e.target.value)} className="form-input" required />
              </div>
              <div style={{ display: 'flex', flexDirection: 'column', gap: '0.4rem', flex: 1 }}>
                <label style={labelStyle}>Date fin</label>
                <input type="date" value={dateFin} min={dateDebut || undefined} onChange={e => setDateFin(e.target.value)} className="form-input" required />
              </div>
            </div>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: '0.4rem' }}>
              <label style={labelStyle}>Facture non payée du tiers</label>
              {!tiersSelectionne && <span style={{ fontSize: '0.78rem', color: 'var(--text-secondary)', fontStyle: 'italic' }}>Sélectionnez d'abord un tiers.</span>}
              {tiersSelectionne && facturesLoading && <Loader2 size={16} className="animate-spin" style={{ color: 'var(--accent-primary)' }} />}
              {tiersSelectionne && !facturesLoading && factures.length === 0 && (
                <span style={{ fontSize: '0.78rem', color: 'var(--status-warning-text-alt)' }}>Aucune facture non payée pour ce tiers.</span>
              )}
              {tiersSelectionne && factures.length > 0 && (
                <select
                  className="form-input"
                  value={factureSelectionnee?.ecId ?? ''}
                  onChange={e => setFactureSelectionnee(factures.find(f => f.ecId === Number(e.target.value)) || null)}
                  required
                >
                  <option value="" disabled>— Choisir une facture —</option>
                  {factures.map(f => (
                    <option key={f.ecId} value={f.ecId}>
                      {f.doNumero} — {formatDate(f.doDate)} — solde {f.solde.toFixed(2)}
                    </option>
                  ))}
                </select>
              )}
            </div>
          )}

          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.4rem' }}>
            <label style={labelStyle}>Pièce jointe (PDF, optionnel)</label>
            <input type="file" accept="application/pdf" onChange={handleFichierChange} style={{ fontSize: '0.82rem' }} />
            {fichierNom && <span style={{ fontSize: '0.75rem', color: 'var(--text-secondary)' }}><Paperclip size={12} style={{ verticalAlign: 'middle' }} /> {fichierNom}</span>}
          </div>

          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '1rem', marginTop: '0.5rem' }}>
            <button type="button" onClick={onClose} className="btn" style={{ background: 'transparent', border: '1px solid var(--border-color)', padding: '0.5rem 1rem', borderRadius: '4px' }}>
              Annuler
            </button>
            <button type="submit" disabled={submitting} className="btn btn-primary" style={{ background: 'var(--accent-primary)', color: 'white', border: 'none', padding: '0.5rem 1.5rem', borderRadius: '4px', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
              {submitting && <Loader2 size={16} className="animate-spin" />}
              Créer
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

// ─── Action « Terminer » (clôture anticipée, Étape 4 de TASK-130) ─────────────────────────────
//
// Modale de saisie de la nouvelle date de fin, bornée [DateDebut, DateFin actuelle] côté UI
// (attributs min/max natifs + revalidation JS identique) EN PLUS du contrôle back (TASK-129,
// ConventionDelaiPaiementValidator.ValiderTerminer) — la borne UI n'est qu'un confort de saisie,
// jamais la seule protection.
function TerminerConventionModal({ convention, onClose, onSuccess }: {
  convention: ConventionDelaiPaiementDto,
  onClose: () => void,
  onSuccess: () => void,
}) {
  const [nouvelleDateFin, setNouvelleDateFin] = useState(convention.dateFin ? convention.dateFin.slice(0, 10) : '');
  const [erreur, setErreur] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const dateDebut = convention.dateDebut ? convention.dateDebut.slice(0, 10) : undefined;
  const ancienneDateFin = convention.dateFin ? convention.dateFin.slice(0, 10) : undefined;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErreur(null);

    if (!nouvelleDateFin) { setErreur('La nouvelle date de fin est obligatoire.'); return; }
    if (dateDebut && nouvelleDateFin < dateDebut) { setErreur('La date fin doit être supérieure ou égale à la date début.'); return; }
    if (ancienneDateFin && nouvelleDateFin > ancienneDateFin) { setErreur('La date fin doit être inférieure ou égale à la date fin actuelle.'); return; }

    setSubmitting(true);
    try {
      await terminerConventionDelaiPaiement(convention.cpId, nouvelleDateFin);
      onSuccess();
    } catch (err: any) {
      console.error(err);
      const msg = err?.response?.data?.Message || err?.response?.data?.message || 'Erreur lors de la clôture anticipée.';
      setErreur(msg);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div style={{ position: 'fixed', inset: 0, zIndex: 9999, display: 'flex', alignItems: 'center', justifyContent: 'center', backgroundColor: 'rgba(0,0,0,0.5)' }}>
      <div style={{ background: 'white', borderRadius: '8px', width: '100%', maxWidth: '420px', boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1), 0 10px 10px -5px rgba(0,0,0,0.04)' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', padding: '1.25rem 1.5rem', borderBottom: '1px solid var(--border-color)' }}>
          <h3 style={{ margin: 0, fontSize: '1.1rem', fontWeight: 600 }}>Clôture anticipée — {convention.numero}</h3>
          <button onClick={onClose} style={{ background: 'transparent', border: 'none', cursor: 'pointer', color: 'var(--text-secondary)' }}><X size={20} /></button>
        </div>
        <form onSubmit={handleSubmit} style={{ padding: '1.5rem', display: 'flex', flexDirection: 'column', gap: '1rem' }}>
          {erreur && (
            <div style={{ padding: '0.75rem 1rem', background: 'var(--status-blocking-bg)', color: 'var(--status-blocking-text)', borderRadius: '4px', fontSize: '0.85rem', display: 'flex', gap: '0.5rem' }}>
              <AlertTriangle size={16} style={{ flexShrink: 0, marginTop: '1px' }} />
              <span>{erreur}</span>
            </div>
          )}
          <div style={{ fontSize: '0.82rem', color: 'var(--text-secondary)' }}>
            Période actuelle : {formatDate(convention.dateDebut || '')} → {formatDate(convention.dateFin || '')}
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.4rem' }}>
            <label style={{ fontSize: '0.8rem', fontWeight: 500 }}>Nouvelle date de fin</label>
            <input
              type="date"
              value={nouvelleDateFin}
              min={dateDebut}
              max={ancienneDateFin}
              onChange={e => setNouvelleDateFin(e.target.value)}
              className="form-input"
              required
            />
          </div>
          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '1rem', marginTop: '0.5rem' }}>
            <button type="button" onClick={onClose} className="btn" style={{ background: 'transparent', border: '1px solid var(--border-color)', padding: '0.5rem 1rem', borderRadius: '4px' }}>
              Annuler
            </button>
            <button type="submit" disabled={submitting} className="btn btn-primary" style={{ background: 'var(--accent-primary)', color: 'white', border: 'none', padding: '0.5rem 1.5rem', borderRadius: '4px', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
              {submitting && <Loader2 size={16} className="animate-spin" />}
              Confirmer
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
