import { useState, useEffect, useCallback, useRef, useMemo, type CSSProperties } from 'react';
import { Loader2, X, AlertTriangle, Paperclip, Plus, Search, Trash2, CalendarClock } from 'lucide-react';
import type { ColDef } from 'ag-grid-community';
import { ApbsGrid } from './grid/ApbsGrid';
import { CustomListFilter } from './grid/CustomListFilter';
import { formatDate } from './utils';

const btnStyle: CSSProperties = {
  display: 'inline-flex', alignItems: 'center', gap: '0.35rem', fontSize: '0.78rem', padding: '0.3rem 0.6rem',
};
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

// Conventions : uniquement les fournisseurs (achat). Le domaine « vente » (client) existe côté
// back (SearchTiersAsync/GetFacturesNonPayeesAsync le supportent déjà) mais n'a pas d'usage métier
// ici (demande PO) — l'écran ne propose donc plus la bascule Achat/Vente, `domaine` est figé.
const DOMAINE: DomaineConvention = 'achat';

export function ConventionsDelaiPaiementPanel({ societeId, showToast }: {
  societeId: number,
  showToast: (m: string, t?: 'success' | 'error' | 'warning') => void,
}) {
  const [rows, setRows] = useState<ConventionDelaiPaiementDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [showCreate, setShowCreate] = useState(false);
  const [terminerTarget, setTerminerTarget] = useState<ConventionDelaiPaiementDto | null>(null);
  const [deleteBusyId, setDeleteBusyId] = useState<number | null>(null);
  const [downloadBusyId, setDownloadBusyId] = useState<number | null>(null);

  const fetchRows = useCallback(async () => {
    setLoading(true);
    try {
      const items = await getConventionsDelaiPaiement(societeId, DOMAINE);
      setRows(items);
    } catch (e) {
      console.error(e);
      showToast('Erreur lors du chargement des conventions', 'error');
      setRows([]);
    } finally {
      setLoading(false);
    }
  }, [societeId, showToast]);

  useEffect(() => { fetchRows(); }, [fetchRows]);

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

  const columnDefs: ColDef[] = useMemo(() => [
    { field: 'tiersCode', headerName: 'Code fournisseur', width: 150, filter: 'agTextColumnFilter' },
    { field: 'tiersIntitule', headerName: 'Intitulé fournisseur', filter: 'agTextColumnFilter' },
    { field: 'type', headerName: 'Type', width: 110, filter: CustomListFilter, cellRenderer: (p: any) => p.data ? <TypeBadge type={p.data.type} /> : null },
    { field: 'numero', headerName: 'N° convention', width: 150 },
    { field: 'periodeOuFacture', headerName: 'Dates / N° facture', width: 230, valueGetter: (p) => p.data ? (p.data.type === 'Facture' ? (p.data.numeroFacture || '—') : `${formatDate(p.data.dateDebut)} → ${formatDate(p.data.dateFin)}`) : '' },
    { field: 'delai', headerName: 'Délai (j)', width: 90, type: 'numericColumn', valueGetter: (p) => p.data?.delaiJours ?? 0 },
    { field: 'valide', headerName: 'Statut', width: 110, filter: CustomListFilter, cellRenderer: (p: any) => p.data ? <ValideBadge valide={p.data.estValide} /> : null },
    {
      field: 'piece',
      headerName: 'Pièce jointe',
      width: 120,
      cellRenderer: (p: any) => {
        const row = p.data;
        if (!row || !row.aPieceJointe) return <span style={{ color: 'var(--text-secondary)' }}>—</span>;
        return (
          <button
            onClick={() => handleDownload(row)}
            disabled={downloadBusyId === row.cpId}
            className="btn"
            style={btnStyle}
            title="Télécharger la pièce jointe"
          >
            {downloadBusyId === row.cpId ? <Loader2 size={13} className="animate-spin" /> : <Paperclip size={13} />}
            <span>Télécharger</span>
          </button>
        );
      }
    },
    {
      headerName: 'Actions',
      width: 170,
      pinned: 'right',
      suppressHeaderMenuButton: true,
      cellRenderer: (p: any) => {
        const row = p.data;
        if (!row) return null;
        return (
          <div style={{ display: 'flex', gap: '0.35rem', alignItems: 'center', height: '100%' }} onClick={(e) => e.stopPropagation()}>
            {row.estValide && row.type === 'Convention' && (
              <button className="btn" style={btnStyle} onClick={() => setTerminerTarget(row)}>
                Terminer
              </button>
            )}
            <button
              className="btn"
              style={{ ...btnStyle, color: 'var(--danger-color, #ef4444)' }}
              onClick={() => handleDelete(row)}
              disabled={deleteBusyId === row.cpId}
              title="Supprimer la convention"
            >
              {deleteBusyId === row.cpId ? <Loader2 size={13} className="animate-spin" /> : <Trash2 size={13} />}
            </button>
          </div>
        );
      }
    }
  ], [downloadBusyId, deleteBusyId, handleDownload, handleDelete]);

  return (
    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', height: '100%', overflow: 'hidden' }}>
      {/* En-tête écran + bascule Achat/Vente */}
      <div style={{ padding: '0.75rem 1rem', borderBottom: '1px solid var(--border-color)', background: 'white', display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: '0.75rem' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.6rem' }}>
          <CalendarClock size={20} style={{ color: 'var(--accent-primary)' }} />
          <div>
            <h2 style={{ margin: 0, fontSize: '1.05rem', fontWeight: 600 }}>Conventions de délai de paiement fournisseurs</h2>
            <div style={{ fontSize: '0.72rem', color: 'var(--text-secondary)' }}>Délai de Paiement Maroc — par fournisseur</div>
          </div>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
          <button
            className="btn btn-primary"
            onClick={() => setShowCreate(true)}
            style={{ display: 'inline-flex', alignItems: 'center', gap: '0.35rem', fontSize: '0.8rem', padding: '0.3rem 0.75rem' }}
          >
            <Plus size={14} /> Nouvelle convention
          </button>
        </div>
      </div>

      {/* Barre d'info */}
      <div style={{ padding: '0.4rem 1rem', borderBottom: '1px solid var(--border-color)', background: 'var(--bg-secondary)', display: 'flex', justifyContent: 'space-between', alignItems: 'center', fontSize: '0.8rem' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
          {loading && <Loader2 size={15} className="animate-spin" style={{ color: 'var(--accent-primary)' }} />}
          <span>Conventions : <strong>{rows.length}</strong></span>
        </div>
      </div>

      <div style={{ flexGrow: 1, position: 'relative' }}>
        <ApbsGrid
          rowData={rows}
          columnDefs={columnDefs}
          height="100%"
          showColumnSelector={true}
          showExportButton={true}
          exportFileName="conventions_ddp.xlsx"
        />
      </div>

      {showCreate && (
        <CreerConventionModal
          societeId={societeId}
          domaine={DOMAINE}
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

  // Factures non payées du tiers sélectionné (type Facture uniquement). Combobox avec recherche
  // sur le n° de facture — même UX que la sélection du fournisseur ci-dessus, mais filtrée
  // localement (toutes les factures non payées du tiers sont déjà chargées, pas de volume
  // comparable à une recherche tiers globale).
  const [factures, setFactures] = useState<FactureNonPayeeDto[]>([]);
  const [facturesLoading, setFacturesLoading] = useState(false);
  const [factureSelectionnee, setFactureSelectionnee] = useState<FactureNonPayeeDto | null>(null);
  const [factureRecherche, setFactureRecherche] = useState('');

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
    if (type !== 'Facture' || !tiersSelectionne) { setFactures([]); setFactureSelectionnee(null); setFactureRecherche(''); return; }
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
          <h3 style={{ margin: 0, fontSize: '1.15rem', fontWeight: 600 }}>Nouvelle convention fournisseur</h3>
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
            <label style={labelStyle}>Fournisseur</label>
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
                factureSelectionnee ? (
                  <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', padding: '0.4rem 0.6rem', background: 'var(--bg-secondary)', borderRadius: '4px', fontSize: '0.85rem' }}>
                    <strong>{factureSelectionnee.doNumero}</strong>
                    <span style={{ color: 'var(--text-secondary)' }}>{formatDate(factureSelectionnee.doDate)} — solde {factureSelectionnee.solde.toFixed(2)}</span>
                    <button type="button" onClick={() => { setFactureSelectionnee(null); setFactureRecherche(''); }} style={{ marginLeft: 'auto', background: 'transparent', border: 'none', cursor: 'pointer', color: 'var(--accent-primary)', fontSize: '0.78rem' }}>Changer</button>
                  </div>
                ) : (
                  <div style={{ position: 'relative' }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '0.4rem' }}>
                      <Search size={14} style={{ color: 'var(--text-secondary)' }} />
                      <input
                        type="text"
                        placeholder="N° de facture…"
                        value={factureRecherche}
                        onChange={e => setFactureRecherche(e.target.value)}
                        className="form-input"
                        style={inputStyle}
                      />
                    </div>
                    <div style={{ border: '1px solid var(--border-color)', borderRadius: '4px', marginTop: '0.3rem', maxHeight: '160px', overflowY: 'auto' }}>
                      {factures
                        .filter(f => f.doNumero.toLowerCase().includes(factureRecherche.trim().toLowerCase()))
                        .map(f => (
                          <div
                            key={f.ecId}
                            onClick={() => { setFactureSelectionnee(f); setFactureRecherche(''); }}
                            style={{ padding: '0.4rem 0.6rem', fontSize: '0.82rem', cursor: 'pointer', borderBottom: '1px solid var(--border-color)' }}
                            onMouseEnter={e => (e.currentTarget.style.background = 'var(--bg-secondary)')}
                            onMouseLeave={e => (e.currentTarget.style.background = 'white')}
                          >
                            <strong>{f.doNumero}</strong> — {formatDate(f.doDate)} — solde {f.solde.toFixed(2)}
                          </div>
                        ))}
                      {factures.filter(f => f.doNumero.toLowerCase().includes(factureRecherche.trim().toLowerCase())).length === 0 && (
                        <div style={{ padding: '0.4rem 0.6rem', fontSize: '0.8rem', color: 'var(--text-secondary)' }}>Aucune facture ne correspond.</div>
                      )}
                    </div>
                  </div>
                )
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
