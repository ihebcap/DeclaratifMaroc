import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  AlertTriangle, ArrowLeft, CalendarClock, CheckCircle2, ChevronLeft, Circle, Download, FileCog, FileX2,
  Landmark, Loader2, Lock, LockOpen, Plus, Send, Settings2, ShieldCheck, Trash2,
} from 'lucide-react';
import type { ColDef } from 'ag-grid-community';
import { ApbsGrid } from './grid/ApbsGrid';
import { CustomListFilter } from './grid/CustomListFilter';
import { formatDate, formatMoney } from './utils';
import { MiseEnRouteDelaiPaiementModal, ModalShell, BandeauErreur, BoutonsModale } from './MiseEnRouteDelaiPaiementModal';
import api, {
  annulerGenerationDeclarationDdp, cloturerDeclarationDdp, creerDeclarationDdp,
  decloturerDeclarationDdp, deposerDeclarationDdp, genererFichierDeclarationDdp,
  getControleIfIceDdp, getDeclarationDdp, getDeclarationsDdp, getLignesDeclarationDdp,
  getParametrageTypeDdp, getSelectionDeclarationDdp, integrerLignesDeclarationDdp,
  modifierLibelleDeclarationDdp, supprimerDeclarationDdp, supprimerLigneDeclarationDdp,
  urlFichierDeclarationDdp,
} from './api';
import type {
  CleLigneDdp, ControleIfIceDdpDto, DeclarationDdpDto, FournisseurFautifDdpDto,
  LigneIntegreeDdpDto, LigneSelectionDdpDto, ResultatIntegrationDdpDto, SelectionDdpDto,
  TypeDeclarationDdp,
} from './api';

// ─── TASK-134 — DDP : liste des déclarations, fiche, popup de sélection des lignes ──────────────
//
// Écrans 1, 2 et 3 de la TASK. Consomme DeclarationsDelaiPaiementController (livré par TASK-134,
// aucun endpoint n'existait pour ce domaine), lui-même passe-plat vers TASK-131 (sélection),
// TASK-132 (cycle de vie + contrôle IF/ICE) et TASK-133 (génération XML/ZIP).
//
// PRINCIPES RESPECTÉS ICI (aucune logique métier côté UI, ARCHITECTURE §5) :
//  - l'état des boutons vient du serveur (`declaration.actions`), calculé depuis les gardes PURES de
//    TASK-132 — jamais déduit d'une combinaison de statuts réinventée ici ;
//  - les périodes ne sont JAMAIS saisies : la popup de sélection affiche les bornes de la déclaration
//    parente en lecture seule (demande PO du 19/07/2026) ;
//  - les messages d'erreur métier sont affichés TELS QUELS (tournures legacy conservées côté back) ;
//  - le contrôle IF/ICE bloquant n'est PAS rejoué côté front : aucune règle de longueur 8/15 n'existe
//    dans ce fichier (point ouvert PO/fiscaliste, cf. VERIFY TASK-132 §8 n°9) — on affiche le verdict
//    et les fournisseurs fautifs renvoyés par le back.
//
// Hors périmètre (documenté, pas un oubli) : branchement au menu (TASK-136), écran conventions
// (TASK-130, déjà livré), mesure du délai fournisseur (TASK-135, déjà livré).



/** Libellé de période lisible — dérivé du type/trimestre renvoyés par le serveur, jamais recalculé depuis des dates. */
function libellePeriode(d: { exercice: number, type: TypeDeclarationDdp, trimestre: number | null }): string {
  return d.type === 'Annuelle' ? `Annuelle ${d.exercice}` : `T${d.trimestre ?? '?'} ${d.exercice}`;
}

function Badge({ texte, ton }: { texte: string, ton: 'ok' | 'warn' | 'block' | 'neutre' }) {
  const styles: Record<string, React.CSSProperties> = {
    ok: { background: 'var(--status-ok-bg)', color: 'var(--status-ok-text)' },
    warn: { background: '#fef3c7', color: '#b45309' },
    block: { background: 'var(--status-blocking-bg)', color: 'var(--status-blocking-text)' },
    neutre: { background: 'var(--bg-secondary)', color: 'var(--text-secondary)' },
  };
  return (
    <span style={{ ...styles[ton], padding: '2px 8px', borderRadius: '99px', fontSize: '0.7rem', fontWeight: 600, whiteSpace: 'nowrap' }}>
      {texte}
    </span>
  );
}

function StatutBadge({ d }: { d: DeclarationDdpDto }) {
  if (d.estDeposee) return <Badge texte="Déposée" ton="ok" />;
  if (d.statut === 'Cloture') return <Badge texte="Clôturée" ton="warn" />;
  return <Badge texte="En cours" ton="neutre" />;
}

const btnStyle: React.CSSProperties = {
  display: 'inline-flex', alignItems: 'center', gap: '0.35rem', fontSize: '0.78rem', padding: '0.3rem 0.6rem',
};

function messageErreur(e: any, defaut: string): string {
  return e?.response?.data?.Message || e?.response?.data?.message || defaut;
}

// ═══ Écran 1 — Liste des déclarations ══════════════════════════════════════════════════════════

export function DeclarationsDelaiPaiementPanel({ societeId, showToast }: {
  societeId: number,
  showToast: (m: string, t?: 'success' | 'error' | 'warning') => void,
}) {
  const [ddpOuvert, setDdpOuvert] = useState<number | null>(null);

  if (ddpOuvert !== null) {
    return (
      <FicheDeclarationDelaiPaiement
        societeId={societeId}
        ddpId={ddpOuvert}
        showToast={showToast}
        onRetour={() => setDdpOuvert(null)}
      />
    );
  }

  return <ListeDeclarationsDelaiPaiement societeId={societeId} showToast={showToast} onOuvrir={setDdpOuvert} />;
}

function ListeDeclarationsDelaiPaiement({ societeId, showToast, onOuvrir }: {
  societeId: number,
  showToast: (m: string, t?: 'success' | 'error' | 'warning') => void,
  onOuvrir: (ddpId: number) => void,
}) {
  const [rows, setRows] = useState<DeclarationDdpDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [filters, setFilters] = useState<Record<string, string | string[]>>({});
  const [showCreate, setShowCreate] = useState(false);
  const [showMiseEnRoute, setShowMiseEnRoute] = useState(false);
  const [_deleteBusyId, setDeleteBusyId] = useState<number | null>(null);

  const fetchRows = useCallback(async () => {
    setLoading(true);
    try {
      setRows(await getDeclarationsDdp(societeId));
    } catch (e) {
      console.error(e);
      showToast('Erreur lors du chargement des déclarations délai de paiement', 'error');
      setRows([]);
    } finally {
      setLoading(false);
    }
  }, [societeId, showToast]);

  useEffect(() => { fetchRows(); }, [fetchRows]);



  const optionsPeriode = useMemo(
    () => Array.from(new Set(rows.map(libellePeriode))).sort().map(v => ({ label: v, value: v })),
    [rows],
  );

  const visibleRows = rows.filter(r => {
    const fPeriode = filters['periode'];
    if (Array.isArray(fPeriode) && fPeriode.length > 0 && !fPeriode.includes(libellePeriode(r))) return false;
    const fStatut = filters['statut'];
    if (Array.isArray(fStatut) && fStatut.length > 0) {
      const v = r.estDeposee ? 'Déposée' : r.statut === 'Cloture' ? 'Clôturée' : 'En cours';
      if (!fStatut.includes(v)) return false;
    }
    return true;
  });

  const handleDelete = async (row: DeclarationDdpDto) => {
    if (!window.confirm(`Supprimer la déclaration [${row.numero}] ?`)) return;
    setDeleteBusyId(row.ddpId);
    try {
      await supprimerDeclarationDdp(row.ddpId);
      showToast('Déclaration supprimée', 'success');
      await fetchRows();
    } catch (e: any) {
      console.error(e);
      showToast(messageErreur(e, 'Erreur lors de la suppression'), 'error');
    } finally {
      setDeleteBusyId(null);
    }
  };


  const columnDefsList: ColDef[] = useMemo(() => [
    {
      field: 'numero',
      headerName: 'N° déclaration',
      width: 150,
      cellRenderer: (p: any) => p.data ? (
        <button
          onClick={() => onOuvrir(p.data.ddpId)}
          style={{ background: 'transparent', border: 'none', color: 'var(--accent-primary)', fontWeight: 600, textDecoration: 'underline', cursor: 'pointer', padding: 0, fontSize: '0.8125rem' }}
        >
          {p.data.numero}
        </button>
      ) : null,
    },
    {
      field: 'periodeText',
      headerName: 'Période',
      width: 210,
      filter: CustomListFilter,
      filterParams: { options: optionsPeriode },
      valueGetter: (p) => p.data ? libellePeriode(p.data) : '',
    },
    {
      field: 'dates',
      headerName: 'Dates',
      width: 210,
      valueGetter: (p) => p.data ? `${formatDate(p.data.dateDebut)} → ${formatDate(p.data.dateFin)}` : '',
    },
    {
      field: 'statutText',
      headerName: 'Statut',
      width: 150,
      filter: CustomListFilter,
      filterParams: { options: [{ label: 'En cours', value: 'En cours' }, { label: 'Clôturée', value: 'Clôturée' }, { label: 'Déposée', value: 'Déposée' }] },
      cellRenderer: (p: any) => p.data ? <StatutBadge d={p.data} /> : null,
    },
    {
      field: 'nbLignes',
      headerName: 'Lignes',
      width: 90,
      type: 'numericColumn',
      valueGetter: (p) => p.data?.nbLignes ?? 0,
    },
    {
      field: 'fichier',
      headerName: 'Fichier',
      width: 110,
      cellRenderer: (p: any) => p.data?.fichierGenere ? <Badge texte="Généré" ton="ok" /> : <Badge texte="Non généré" ton="neutre" />,
    },
    {
      field: 'depot',
      headerName: 'Dépôt',
      width: 110,
      cellRenderer: (p: any) => p.data?.estDeposee ? <Badge texte="Déposée" ton="ok" /> : <Badge texte="Non déposée" ton="neutre" />,
    },
    {
      field: 'libelle',
      headerName: 'Libellé',
      valueGetter: (p) => p.data?.libelle || '—',
    },
    {
      headerName: 'Actions',
      width: 130,
      pinned: 'right',
      suppressHeaderMenuButton: true,
      cellRenderer: (p: any) => {
        const row = p.data;
        if (!row) return null;
        return (
          <div style={{ display: 'flex', gap: '0.35rem', alignItems: 'center', height: '100%' }} onClick={(e) => e.stopPropagation()}>
            <button className="btn btn-primary" style={btnStyle} onClick={() => onOuvrir(row.ddpId)}>
              Ouvrir
            </button>
            {row.actions.peutSupprimer && (
              <button
                className="btn"
                style={{ ...btnStyle, color: 'var(--danger-color, #ef4444)' }}
                onClick={() => handleDelete(row)}
                title="Supprimer la déclaration"
              >
                <Trash2 size={13} />
              </button>
            )}
          </div>
        );
      }
    }
  ], [onOuvrir, optionsPeriode, handleDelete]);

  return (
    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', height: '100%', overflow: 'hidden' }}>
      <div style={{ padding: '0.75rem 1rem', borderBottom: '1px solid var(--border-color)', background: 'white', display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: '0.75rem' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.6rem' }}>
          <CalendarClock size={20} style={{ color: 'var(--accent-primary)' }} />
          <div>
            <h2 style={{ margin: 0, fontSize: '1.05rem', fontWeight: 600 }}>Déclarations délai de paiement</h2>
            <div style={{ fontSize: '0.72rem', color: 'var(--text-secondary)' }}>Délai de Paiement Maroc</div>
          </div>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
          {/* TASK-128 : sans cette date, aucune ligne n'est intégrable (cf. MiseEnRouteDelaiPaiementModal). */}
          <button className="btn" style={btnStyle} onClick={() => setShowMiseEnRoute(true)} title="Paramétrer la date de mise en route">
            <Settings2 size={14} /> Date de mise en route
          </button>
          <button className="btn btn-primary" style={{ ...btnStyle, padding: '0.3rem 0.75rem' }} onClick={() => setShowCreate(true)}>
            <Plus size={14} /> Nouvelle déclaration
          </button>
        </div>
      </div>

      <div style={{ flexGrow: 1, minHeight: 0, position: 'relative', padding: '0.4rem 1rem' }}>
        <ApbsGrid
          rowData={rows}
          columnDefs={columnDefsList}
          onRowClicked={(p) => onOuvrir(p.data.ddpId)}
          height="100%"
          showColumnSelector={true}
          showExportButton={true}
          exportFileName="declarations_ddp.xlsx"
          toolbarLeft={
            <>
              {loading && <Loader2 size={15} className="animate-spin" style={{ color: 'var(--accent-primary)' }} />}
              <span>Déclarations : <strong>{visibleRows.length}</strong> / {rows.length}</span>
              {Object.keys(filters).length > 0 && (
                <button className="btn" onClick={() => setFilters({})} style={{ background: 'transparent', border: 'none', color: 'var(--accent-primary)', textDecoration: 'underline', padding: 0, fontSize: '0.8rem', cursor: 'pointer' }}>
                  Effacer filtres ({Object.keys(filters).length})
                </button>
              )}
            </>
          }
        />
      </div>

      {showCreate && (
        <CreerDeclarationModal
          societeId={societeId}
          onClose={() => setShowCreate(false)}
          onSuccess={async () => { setShowCreate(false); showToast('Déclaration créée', 'success'); await fetchRows(); }}
        />
      )}

      {showMiseEnRoute && (
        <MiseEnRouteDelaiPaiementModal
          societeId={societeId}
          onClose={() => setShowMiseEnRoute(false)}
          onSaved={() => { setShowMiseEnRoute(false); showToast('Date de mise en route enregistrée', 'success'); }}
        />
      )}
    </div>
  );
}

// ─── Création (exercice / type / trimestre / libellé) ───────────────────────────────────────────
//
// Ni numéro ni bornes de période dans ce formulaire : le numéro est attribué par le serveur
// (NumerotationDeclarationDelaiPaiement, TASK-132) et les bornes sont CALCULÉES depuis
// exercice + type + trimestre. Le type proposé par défaut vient de P_SOCIETE.SO_TypeDecDP (CDC §7.1).

const EXERCICE_MIN = 2023; // 2023-07-01 = début de la déclaration légale (SeuilsLegauxDelaiPaiement).

function CreerDeclarationModal({ societeId, onClose, onSuccess }: {
  societeId: number,
  onClose: () => void,
  onSuccess: () => void,
}) {
  const anneeCourante = new Date().getFullYear();
  const exercices = useMemo(
    () => Array.from({ length: anneeCourante + 1 - EXERCICE_MIN + 1 }, (_, i) => EXERCICE_MIN + i).reverse(),
    [anneeCourante],
  );

  const [exercice, setExercice] = useState(anneeCourante);
  const [type, setType] = useState<'annuelle' | 'trimestrielle'>('trimestrielle');
  const [trimestre, setTrimestre] = useState(1);
  const [libelle, setLibelle] = useState('');
  const [erreur, setErreur] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  // Type par défaut de la société (SO_TypeDecDP) — proposition, pas une contrainte : l'utilisateur
  // reste libre, et le serveur valide de toute façon.
  useEffect(() => {
    (async () => {
      try {
        const res = await getParametrageTypeDdp(societeId);
        if (res.typeParDefaut === 'Annuelle') setType('annuelle');
        else if (res.typeParDefaut === 'Trimestrielle') setType('trimestrielle');
      } catch {
        // Paramétrage illisible : on garde le trimestriel (cas le plus fréquent au Maroc) et on
        // n'affiche pas d'erreur bloquante — ce n'est qu'une proposition de valeur par défaut.
      }
    })();
  }, [societeId]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErreur(null);
    setSubmitting(true);
    try {
      await creerDeclarationDdp({
        soId: societeId,
        exercice,
        type,
        trimestre: type === 'trimestrielle' ? trimestre : null,
        libelle: libelle.trim() || null,
      });
      onSuccess();
    } catch (err: any) {
      console.error(err);
      // Message serveur TEL QUEL : TASK-132 cite déjà la déclaration en conflit (numéro + période).
      setErreur(messageErreur(err, 'Erreur lors de la création de la déclaration.'));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <ModalShell titre="Nouvelle déclaration délai de paiement" onClose={onClose} maxWidth="520px">
      <form onSubmit={handleSubmit} style={{ padding: '1.5rem', display: 'flex', flexDirection: 'column', gap: '1rem' }}>
        {erreur && <BandeauErreur message={erreur} />}

        <div style={{ fontSize: '0.78rem', color: 'var(--text-secondary)' }}>
          Le numéro et les bornes de période sont attribués/calculés par le serveur — aucune date à saisir.
        </div>

        <div style={{ display: 'flex', gap: '1rem' }}>
          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.4rem', flex: 1 }}>
            <label style={{ fontSize: '0.8rem', fontWeight: 500 }}>Exercice</label>
            <select className="form-input" value={exercice} onChange={e => setExercice(Number(e.target.value))}>
              {exercices.map(a => <option key={a} value={a}>{a}</option>)}
            </select>
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.4rem', flex: 1 }}>
            <label style={{ fontSize: '0.8rem', fontWeight: 500 }}>Type</label>
            <select className="form-input" value={type} onChange={e => setType(e.target.value as 'annuelle' | 'trimestrielle')}>
              <option value="trimestrielle">Trimestrielle</option>
              <option value="annuelle">Annuelle</option>
            </select>
          </div>
        </div>

        {type === 'trimestrielle' && (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '0.4rem' }}>
            <label style={{ fontSize: '0.8rem', fontWeight: 500 }}>Trimestre</label>
            <select className="form-input" value={trimestre} onChange={e => setTrimestre(Number(e.target.value))}>
              <option value={1}>T1 (01/01 → 31/03)</option>
              <option value={2}>T2 (01/04 → 30/06)</option>
              <option value={3}>T3 (01/07 → 30/09)</option>
              <option value={4}>T4 (01/10 → 31/12)</option>
            </select>
          </div>
        )}

        <div style={{ display: 'flex', flexDirection: 'column', gap: '0.4rem' }}>
          <label style={{ fontSize: '0.8rem', fontWeight: 500 }}>Libellé (optionnel)</label>
          <input type="text" className="form-input" value={libelle} onChange={e => setLibelle(e.target.value)} />
        </div>

        <BoutonsModale onClose={onClose} submitting={submitting} libelle="Créer" />
      </form>
    </ModalShell>
  );
}

// ═══ Écran 2 — Fiche déclaration ═══════════════════════════════════════════════════════════════



// ─── HARMONISATION AVEC LE MODULE TVA (demande PO) ─────────────────────────────────────────────
// La fiche déclaration TVA guide le comptable par un stepper « 1. Sélection / 2. Vérifier &
// Intégrer / 3. Déclaration », avec des titres « Étape N — … ». La fiche DDP présentait au
// contraire une barre plate mélangeant intégration, contrôle IF/ICE, génération, dépôt et
// déclôture — impossible de savoir dans quel ordre agir. On reprend ici EXACTEMENT les mêmes
// conventions visuelles et le même vocabulaire d'étapes, adaptés aux 3 actes réels de la DDP.
type EtapeDdp = 'lignes' | 'verifier' | 'declaration';

const ETAPES_DDP: { id: EtapeDdp; label: string; icon: typeof Landmark }[] = [
  { id: 'lignes', label: 'Sélection des lignes', icon: Landmark },
  { id: 'verifier', label: 'Vérifier (IF/ICE)', icon: ShieldCheck },
  { id: 'declaration', label: 'Déclaration', icon: FileCog },
];

/** Étape 3 verrouillée tant qu'aucune ligne n'est intégrée — même gate que le stepper TVA. */
function etapeDdpAccessible(id: EtapeDdp, aDesLignes: boolean) {
  return id === 'declaration' ? aDesLignes : true;
}

function StepBarDdp({ etape, aDesLignes, onSelect, leading, trailing }: {
  etape: EtapeDdp,
  aDesLignes: boolean,
  onSelect: (id: EtapeDdp) => void,
  leading?: React.ReactNode,
  trailing?: React.ReactNode,
}) {
  return (
    <div style={{ display: 'flex', alignItems: 'stretch', background: 'white', borderBottom: '1px solid var(--border-color)', flexShrink: 0 }}>
      {leading}
      <div style={{ display: 'flex', flex: 1, overflowX: 'auto' }}>
        {ETAPES_DDP.map((step, i) => {
          const unlocked = etapeDdpAccessible(step.id, aDesLignes);
          const isActive = etape === step.id;
          const isDone = aDesLignes && step.id === 'lignes' && !isActive;
          return (
            <button
              key={step.id}
              onClick={() => unlocked && onSelect(step.id)}
              disabled={!unlocked}
              title={unlocked ? step.label : `${step.label} — nécessite au moins une ligne intégrée`}
              style={{
                display: 'flex', alignItems: 'center', gap: '0.35rem',
                padding: '0.6rem 0.7rem',
                background: isActive ? 'var(--bg-secondary)' : 'transparent',
                border: 'none',
                borderBottom: isActive ? '2px solid var(--accent-primary)' : '2px solid transparent',
                color: !unlocked ? '#9ca3af' : isActive ? 'var(--accent-primary)' : 'var(--text-primary)',
                fontWeight: isActive ? 600 : 500,
                fontSize: '0.8125rem',
                cursor: unlocked ? 'pointer' : 'not-allowed',
                whiteSpace: 'nowrap',
                flexShrink: 0,
              }}
            >
              {isDone ? <CheckCircle2 size={15} style={{ color: '#16a34a' }} /> : !unlocked ? <Lock size={13} /> : <Circle size={13} style={{ opacity: isActive ? 1 : 0.4 }} />}
              <span>{i + 1}. {step.label}</span>
            </button>
          );
        })}
      </div>
      {trailing}
    </div>
  );
}

/** Titre d'étape — même gabarit que « Étape 2 — Vérifier & Intégrer » côté TVA. */
function TitreEtapeDdp({ numero, titre, sousTitre, droite }: {
  numero: number, titre: string, sousTitre: string, droite?: React.ReactNode,
}) {
  return (
    <div style={{ padding: '0.7rem 1rem', borderBottom: '1px solid var(--border-color)', background: 'white', display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: '1rem', flexWrap: 'wrap' }}>
      <div>
        <h2 style={{ margin: 0, fontSize: '1rem', fontWeight: 600 }}>Étape {numero} — {titre}</h2>
        <div style={{ fontSize: '0.75rem', color: 'var(--text-secondary)' }}>{sousTitre}</div>
      </div>
      {droite}
    </div>
  );
}

/** État de règlement d'une ligne intégrée — sert de filtre et de sous-total lisibles. */
const ETAT_REGLE = 'Réglé';
const ETAT_NON_REGLE = 'Non réglé (solde restant)';
function etatReglement(row: LigneIntegreeDdpDto): string {
  return (row.reglementPiece || row.reglementNumero) ? ETAT_REGLE : ETAT_NON_REGLE;
}

function FicheDeclarationDelaiPaiement({ societeId, ddpId, showToast, onRetour }: {
  societeId: number,
  ddpId: number,
  showToast: (m: string, t?: 'success' | 'error' | 'warning') => void,
  onRetour: () => void,
}) {
  const [declaration, setDeclaration] = useState<DeclarationDdpDto | null>(null);
  const [lignes, setLignes] = useState<LigneIntegreeDdpDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState<string | null>(null);
  const [erreurAction, setErreurAction] = useState<string | null>(null);
  const [controle, setControle] = useState<ControleIfIceDdpDto | null>(null);
  const [fautifs, setFautifs] = useState<FournisseurFautifDdpDto[]>([]);
  const [showSelection, setShowSelection] = useState(false);
  const [showLibelle, setShowLibelle] = useState(false);
  const [showMiseEnRoute, setShowMiseEnRoute] = useState(false);
  const [etape, setEtape] = useState<EtapeDdp>('lignes');
  const [filters, setFilters] = useState<Record<string, string | string[]>>({});
  // Cause RÉELLE d'un échec de chargement : un « introuvable » affiché sur une simple coupure
  // réseau/API envoie le comptable chercher un problème de données qui n'existe pas.
  const [erreurChargement, setErreurChargement] = useState<string | null>(null);

  const recharger = useCallback(async () => {
    setLoading(true);
    try {
      const [d, l] = await Promise.all([getDeclarationDdp(ddpId), getLignesDeclarationDdp(ddpId)]);
      setDeclaration(d);
      setLignes(l);
      setErreurChargement(null);
    } catch (e: any) {
      console.error(e);
      const statut = e?.response?.status;
      setErreurChargement(
        statut === 404
          ? `Déclaration introuvable (elle a peut-être été supprimée depuis l'affichage de la liste).`
          : statut === 403
            ? "Vous n'avez pas accès à la déclaration de cette société."
            : messageErreur(e, statut
              ? `Le serveur a répondu une erreur ${statut} : la déclaration n'a pas pu être chargée. Réessayez, puis prévenez votre support si le problème persiste.`
              : "L'application n'a pas pu joindre le serveur : la déclaration n'a pas pu être chargée. Vérifiez que le service est démarré, puis réessayez."),
      );
      showToast('Erreur lors du chargement de la déclaration', 'error');
    } finally {
      setLoading(false);
    }
  }, [ddpId, showToast]);

  useEffect(() => { recharger(); }, [recharger]);

  /** Enveloppe commune : message serveur affiché TEL QUEL, rechargement systématique de l'état. */
  const executer = async (cle: string, action: () => Promise<void>, succes: string) => {
    setBusy(cle);
    setErreurAction(null);
    setFautifs([]);
    try {
      await action();
      showToast(succes, 'success');
      await recharger();
    } catch (e: any) {
      console.error(e);
      setErreurAction(messageErreur(e, 'Action refusée par le serveur.'));
      // Le back joint la liste structurée des fournisseurs fautifs quand le contrôle IF/ICE bloque.
      const listeFautifs = e?.response?.data?.FournisseursFautifs ?? e?.response?.data?.fournisseursFautifs;
      if (Array.isArray(listeFautifs) && listeFautifs.length > 0) setFautifs(listeFautifs);
      await recharger();
    } finally {
      setBusy(null);
    }
  };

  const handleControlerIfIce = async () => {
    setBusy('controle');
    setErreurAction(null);
    try {
      const res = await getControleIfIceDdp(ddpId);
      setControle(res);
      setFautifs(res.fournisseursFautifs);
    } catch (e: any) {
      setErreurAction(messageErreur(e, 'Contrôle IF/ICE impossible.'));
    } finally {
      setBusy(null);
    }
  };

  const handleTelecharger = async () => {
    setBusy('download');
    try {
      const res = await api.get(urlFichierDeclarationDdp(ddpId), { responseType: 'blob' });
      const blobUrl = URL.createObjectURL(res.data);
      const a = document.createElement('a');
      a.href = blobUrl;
      a.download = `${declaration?.numero || `ddp-${ddpId}`}.zip`;
      document.body.appendChild(a);
      a.click();
      a.remove();
      URL.revokeObjectURL(blobUrl);
    } catch (e: any) {
      setErreurAction(messageErreur(e, 'Téléchargement impossible.'));
    } finally {
      setBusy(null);
    }
  };

  const handleSupprimerLigne = async (ddplId: number) => {
    await executer(`ligne-${ddplId}`, () => supprimerLigneDeclarationDdp(ddpId, ddplId), 'Ligne retirée');
  };

  const columnDefsLignes: ColDef[] = useMemo(() => [
    { field: 'facture', headerName: 'N° Facture', width: 130, filter: 'agTextColumnFilter', valueGetter: (p) => p.data?.doNumero || '' },
    { field: 'doDate', headerName: 'Date facture', width: 110, valueGetter: (p) => p.data ? formatDate(p.data.doDate) : '' },
    { field: 'tiers', headerName: 'Fournisseur', filter: 'agTextColumnFilter', valueGetter: (p) => p.data ? `${p.data.tiersCode || ''} · ${p.data.tiersIntitule || ''}` : '' },
    { field: 'identifiantFiscal', headerName: 'IF', width: 110, valueGetter: (p) => p.data?.tiersIdentifiantFiscal || '—' },
    { field: 'ice', headerName: 'ICE', width: 130, valueGetter: (p) => p.data?.tiersICE || '—' },
    { field: 'montantLigne', headerName: 'Montant', width: 120, type: 'numericColumn', valueGetter: (p) => p.data ? formatMoney(p.data.montantAffecte ?? p.data.soldeEcheance) : '' },
    { field: 'depassement', headerName: 'Dépassement (j)', width: 130, type: 'numericColumn', valueGetter: (p) => p.data?.depassement ?? 0 },
    ...(declaration?.actions?.peutIntegrerLignes ? [{
      headerName: 'Action',
      width: 90,
      pinned: 'right' as const,
      suppressHeaderMenuButton: true,
      cellRenderer: (p: any) => {
        const row = p.data;
        if (!row) return null;
        return (
          <button
            className="btn"
            style={{ ...btnStyle, color: 'var(--danger-color, #ef4444)' }}
            onClick={() => handleSupprimerLigne(row.ddplId)}
            disabled={busy === `ligne-${row.ddplId}`}
            title="Retirer cette ligne de la déclaration"
          >
            {busy === `ligne-${row.ddplId}` ? <Loader2 size={13} className="animate-spin" /> : <Trash2 size={13} />}
          </button>
        );
      }
    }] : []),
  ], [declaration?.actions?.peutIntegrerLignes, busy, handleSupprimerLigne]);

  if (loading && !declaration) {
    return (
      <div style={{ flex: 1, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
        <Loader2 size={22} className="animate-spin" style={{ color: 'var(--accent-primary)' }} />
      </div>
    );
  }
  if (!declaration) {
    return (
      <div style={{ flex: 1, padding: '2rem' }}>
        <button className="btn" style={btnStyle} onClick={onRetour}><ArrowLeft size={14} /> Retour</button>
        <div style={{ marginTop: '1rem', maxWidth: '760px' }}>
          <BandeauErreur message={erreurChargement ?? "La déclaration n'a pas pu être chargée."} />
          <button className="btn" style={{ ...btnStyle, marginTop: '0.75rem' }} onClick={recharger}>Réessayer</button>
        </div>
      </div>
    );
  }

  const a = declaration.actions;

  const lignesVisibles = lignes.filter(row => {
    const fTiers = filters['tiers'];
    if (typeof fTiers === 'string' && fTiers.trim() !== '') {
      const needle = fTiers.trim().toLowerCase();
      if (!`${row.tiersCode ?? ''} ${row.tiersIntitule ?? ''}`.toLowerCase().includes(needle)) return false;
    }
    const fFacture = filters['facture'];
    if (Array.isArray(fFacture) && fFacture.length > 0 && !fFacture.includes(row.doNumero ?? '')) return false;
    const fReglement = filters['reglement'];
    if (Array.isArray(fReglement) && fReglement.length > 0 && !fReglement.includes(etatReglement(row))) return false;
    return true;
  });

  const montantLigne = (row: LigneIntegreeDdpDto) => row.montantAffecte ?? row.soldeEcheance;
  const totalMontant = lignesVisibles.reduce((s, r) => s + (montantLigne(r) ?? 0), 0);
  const nbNonRegle = lignesVisibles.filter(r => etatReglement(r) === ETAT_NON_REGLE).length;
  const depassementMax = lignesVisibles.reduce((m, r) => Math.max(m, r.depassement ?? 0), 0);



  const leadingStep = (
    <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', padding: '0 0.85rem', borderRight: '1px solid var(--border-color)', flexShrink: 0 }}>
      <button onClick={onRetour} className="btn" title="Retour à la liste des déclarations" style={{ background: 'transparent', border: 'none', padding: 0, cursor: 'pointer', display: 'flex', alignItems: 'center', gap: '0.15rem', color: 'var(--text-secondary)', fontSize: '0.75rem' }}>
        <ChevronLeft size={16} /> Retour
      </button>
      <div style={{ fontSize: '0.95rem', fontWeight: 600 }}>{declaration.numero}</div>
    </div>
  );

  const trailingStep = (
    <div style={{ display: 'flex', alignItems: 'center', gap: '0.6rem', padding: '0 1rem', flexShrink: 0 }}>
      <span style={{ fontSize: '0.72rem', color: 'var(--text-secondary)', whiteSpace: 'nowrap' }}>
        {libellePeriode(declaration)} · Période {formatDate(declaration.dateDebut)} → {formatDate(declaration.dateFin)}
      </span>
      <StatutBadge d={declaration} />
      {declaration.fichierGenere && <Badge texte="Fichier généré" ton="ok" />}
    </div>
  );



  return (
    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', height: '100%', overflow: 'hidden' }}>
      {/* Stepper — mêmes conventions visuelles et même vocabulaire que la fiche TVA */}
      <StepBarDdp
        etape={etape}
        aDesLignes={lignes.length > 0}
        onSelect={setEtape}
        leading={leadingStep}
        trailing={trailingStep}
      />

      {etape === 'lignes' && (
        <TitreEtapeDdp
          numero={1}
          titre="Sélection des lignes hors délai"
          sousTitre="Choisissez les lignes à déclarer, vérifiez les montants et le dépassement de chacune"
          droite={
            <div style={{ display: 'flex', gap: '0.4rem', alignItems: 'center', flexWrap: 'wrap' }}>
              {a.peutIntegrerLignes && (
                <button className="btn btn-primary" style={btnStyle} onClick={() => setShowSelection(true)}>
                  <Plus size={14} /> Sélectionner des lignes hors délai
                </button>
              )}
              {a.peutModifierLibelle && (
                <button className="btn" style={btnStyle} onClick={() => setShowLibelle(true)}>Modifier le libellé</button>
              )}
            </div>
          }
        />
      )}

      {etape === 'verifier' && (
        <TitreEtapeDdp
          numero={2}
          titre="Vérifier (IF/ICE)"
          sousTitre="Contrôlez l'identifiant fiscal et l'ICE de chaque fournisseur déclaré avant de générer le fichier"
          droite={
            <button className="btn btn-primary" style={btnStyle} disabled={busy === 'controle'} onClick={handleControlerIfIce}>
              {busy === 'controle' ? <Loader2 size={13} className="animate-spin" /> : <CheckCircle2 size={14} />} Lancer le contrôle IF/ICE
            </button>
          }
        />
      )}

      {etape === 'declaration' && (
        <TitreEtapeDdp
          numero={3}
          titre="Déclaration"
          sousTitre="Clôturez la période, générez le fichier XML/ZIP puis marquez la déclaration comme déposée"
          droite={
            <div style={{ display: 'flex', gap: '0.4rem', flexWrap: 'wrap', alignItems: 'center' }}>
              {a.peutCloturer && (
                <button className="btn btn-primary" style={btnStyle} disabled={busy === 'cloture'} onClick={() => executer('cloture', () => cloturerDeclarationDdp(ddpId), 'Déclaration clôturée')}>
                  {busy === 'cloture' ? <Loader2 size={13} className="animate-spin" /> : <Lock size={14} />} Clôturer
                </button>
              )}
              {a.peutAnnulerCloture && (
                <button className="btn" style={btnStyle} disabled={busy === 'decloture'} onClick={() => executer('decloture', () => decloturerDeclarationDdp(ddpId), 'Déclaration déclôturée')}>
                  {busy === 'decloture' ? <Loader2 size={13} className="animate-spin" /> : <LockOpen size={14} />} Déclôturer
                </button>
              )}
              {a.peutGenererFichier && (
                <button className="btn btn-primary" style={btnStyle} disabled={busy === 'generation'} onClick={() => executer('generation', async () => { await genererFichierDeclarationDdp(ddpId); }, 'Fichier XML/ZIP généré')}>
                  {busy === 'generation' ? <Loader2 size={13} className="animate-spin" /> : <FileCog size={14} />} Générer le fichier
                </button>
              )}
              {a.peutAnnulerGeneration && (
                <button className="btn" style={btnStyle} disabled={busy === 'annulGen'} onClick={() => executer('annulGen', () => annulerGenerationDeclarationDdp(ddpId), 'Génération annulée')}>
                  {busy === 'annulGen' ? <Loader2 size={13} className="animate-spin" /> : <FileX2 size={14} />} Annuler la génération
                </button>
              )}
              {declaration.fichierGenere && (
                <button className="btn" style={btnStyle} disabled={busy === 'download'} onClick={handleTelecharger}>
                  {busy === 'download' ? <Loader2 size={13} className="animate-spin" /> : <Download size={14} />} Télécharger le ZIP
                </button>
              )}
              {a.peutDeposer && (
                <button className="btn" style={btnStyle} disabled={busy === 'depot'} onClick={() => executer('depot', () => deposerDeclarationDdp(ddpId), 'Déclaration marquée déposée')}>
                  {busy === 'depot' ? <Loader2 size={13} className="animate-spin" /> : <Send size={14} />} Marquer déposée
                </button>
              )}
            </div>
          }
        />
      )}

      {/* Totaux — un comptable doit voir CE QU'IL DÉCLARE sans exporter le tableau.
          Rattachés explicitement aux lignes affichées (filtres compris) pour ne jamais laisser
          croire qu'un total filtré est le total de la déclaration. */}
      {etape === 'lignes' && (
      <div style={{ padding: '0.4rem 1rem', borderBottom: '1px solid var(--border-color)', background: 'white', display: 'flex', gap: '1.25rem', flexWrap: 'wrap', alignItems: 'center', fontSize: '0.8rem' }}>
        <span>
          {lignesVisibles.length === lignes.length
            ? <>Total de la déclaration ({lignes.length} ligne(s)) :</>
            : <>Total des <strong>{lignesVisibles.length}</strong> ligne(s) affichée(s) sur {lignes.length} :</>}
          {' '}<strong data-testid="ddp-total-montant">{formatMoney(totalMontant)}</strong>
        </span>
        <span title="Lignes dont aucune part n'a été réglée : c'est le solde restant dû qui est déclaré.">
          dont <strong>{nbNonRegle}</strong> ligne(s) non réglée(s)
        </span>
        <span title="Plus grand dépassement (en jours) parmi les lignes affichées.">
          Dépassement le plus élevé : <strong>{depassementMax} j</strong>
        </span>
        {Object.keys(filters).length > 0 && (
          <button
            className="btn"
            onClick={() => setFilters({})}
            style={{ background: 'transparent', border: 'none', color: 'var(--accent-primary)', textDecoration: 'underline', padding: 0, fontSize: '0.8rem', cursor: 'pointer' }}
          >
            Effacer filtres ({Object.keys(filters).length})
          </button>
        )}
      </div>
      )}

      {/* Refus métier + fournisseurs fautifs IF/ICE */}
      {(erreurAction || fautifs.length > 0 || controle) && (
        <div style={{ padding: '0.75rem 1rem', borderBottom: '1px solid var(--border-color)', background: 'white', display: 'flex', flexDirection: 'column', gap: '0.6rem' }}>
          {erreurAction && <BandeauErreur message={erreurAction} />}

          {controle && controle.estConforme && fautifs.length === 0 && (
            <div style={{ padding: '0.6rem 0.85rem', background: 'var(--status-ok-bg)', color: 'var(--status-ok-text)', borderRadius: '4px', fontSize: '0.82rem', display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
              <CheckCircle2 size={16} />
              Contrôle IF/ICE conforme : {controle.nombreFournisseursExamines} fournisseur(s) examiné(s), {controle.nombreLignesExaminees} ligne(s).
            </div>
          )}

          {fautifs.length > 0 && (
            <div style={{ border: '1px solid var(--border-color)', borderRadius: '4px', overflow: 'hidden' }}>
              <div style={{ padding: '0.45rem 0.75rem', background: 'var(--status-blocking-bg)', color: 'var(--status-blocking-text)', fontWeight: 600, fontSize: '0.8rem', display: 'flex', alignItems: 'center', gap: '0.4rem' }}>
                <AlertTriangle size={15} /> {fautifs.length} fournisseur(s) à corriger dans l'ERP avant génération
              </div>
              <div style={{ maxHeight: '190px', overflowY: 'auto' }}>
                {fautifs.map(f => (
                  <div key={f.tiersCode} style={{ padding: '0.4rem 0.75rem', borderTop: '1px solid var(--border-color)', fontSize: '0.8rem' }}>
                    <div><strong>[{f.tiersCode}]</strong> {f.tiersIntitule} — {f.nombreLignes} ligne(s)</div>
                    {/* Motifs libellés PAR LE BACK (TASK-132) : affichés tels quels, aucune règle de
                        longueur IF/ICE n'est évaluée côté front (point ouvert PO/fiscaliste). */}
                    <div style={{ color: 'var(--status-blocking-text)' }}>{f.motifsLibelles.join(' ; ')}</div>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      )}

      {/* Lignes intégrées */}
      <div style={{ flexGrow: 1, minHeight: 0, position: 'relative', padding: '0.4rem 1rem' }}>
        <ApbsGrid
          rowData={lignes}
          columnDefs={columnDefsLignes}
          height="100%"
          showColumnSelector={true}
          showExportButton={true}
          exportFileName="lignes_ddp.xlsx"
        />
        {lignes.length === 0 && !loading && (
          <div style={{ padding: '2.5rem', textAlign: 'center', color: 'var(--text-secondary)', fontSize: '0.85rem' }}>
            Aucune ligne intégrée. Une déclaration sans ligne ne peut pas être clôturée.
            <div style={{ marginTop: '0.75rem', display: 'flex', gap: '0.5rem', justifyContent: 'center' }}>
              {a.peutIntegrerLignes && (
                <button className="btn btn-primary" style={btnStyle} onClick={() => setShowSelection(true)}>
                  <Plus size={14} /> Sélectionner des lignes hors délai
                </button>
              )}
              <button className="btn" style={btnStyle} onClick={() => setShowMiseEnRoute(true)}>
                <Settings2 size={14} /> Date de mise en route
              </button>
            </div>
          </div>
        )}
      </div>

      {showSelection && (
        <SelectionLignesModal
          societeId={societeId}
          declaration={declaration}
          onClose={() => setShowSelection(false)}
          onIntegre={async (resultat) => {
            setShowSelection(false);
            showToast(`${resultat.nombreIntegrees} ligne(s) intégrée(s)`, resultat.nombreIntegrees > 0 ? 'success' : 'warning');
            await recharger();
          }}
        />
      )}

      {showLibelle && (
        <ModifierLibelleModal
          declaration={declaration}
          onClose={() => setShowLibelle(false)}
          onSuccess={async () => { setShowLibelle(false); showToast('Libellé modifié', 'success'); await recharger(); }}
        />
      )}

      {showMiseEnRoute && (
        <MiseEnRouteDelaiPaiementModal
          societeId={societeId}
          onClose={() => setShowMiseEnRoute(false)}
          onSaved={() => { setShowMiseEnRoute(false); showToast('Date de mise en route enregistrée', 'success'); }}
        />
      )}
    </div>
  );
}

function ModifierLibelleModal({ declaration, onClose, onSuccess }: {
  declaration: DeclarationDdpDto,
  onClose: () => void,
  onSuccess: () => void,
}) {
  const [libelle, setLibelle] = useState(declaration.libelle ?? '');
  const [erreur, setErreur] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErreur(null);
    setSubmitting(true);
    try {
      await modifierLibelleDeclarationDdp(declaration.ddpId, libelle.trim() || null);
      onSuccess();
    } catch (err: any) {
      setErreur(messageErreur(err, 'Erreur lors de la modification du libellé.'));
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <ModalShell titre={`Libellé — ${declaration.numero}`} onClose={onClose} maxWidth="440px">
      <form onSubmit={handleSubmit} style={{ padding: '1.5rem', display: 'flex', flexDirection: 'column', gap: '1rem' }}>
        {erreur && <BandeauErreur message={erreur} />}
        <div style={{ display: 'flex', flexDirection: 'column', gap: '0.4rem' }}>
          <label style={{ fontSize: '0.8rem', fontWeight: 500 }}>Libellé</label>
          <input type="text" className="form-input" value={libelle} onChange={e => setLibelle(e.target.value)} />
        </div>
        <BoutonsModale onClose={onClose} submitting={submitting} libelle="Enregistrer" />
      </form>
    </ModalShell>
  );
}

// ═══ Écran 3 — Popup de sélection des lignes hors délai ════════════════════════════════════════
//
// PÉRIODE NON MODIFIABLE : elle est celle de la déclaration parente (DDP_DateDebut/DDP_DateFin,
// renvoyées par le serveur avec la sélection) — la demande PO du 19/07/2026 interdit toute plage de
// dates libre, contrairement au legacy (FrmControleLigneDelaisPaiement.cs:56-60).
//
// Deux listes strictement séparées :
//  1. lignes candidates → cochables, intégrables (Depassement incrémental déjà calculé par TASK-131) ;
//  2. lignes « antérieures à la mise en route — retard réel inconnu » → NON cochables, badgées, sans
//     aucun chiffre de dépassement. Elles restent visibles (aucune ligne masquée silencieusement) et
//     nécessitent une reprise manuelle depuis l'écran de contrôle.

function SelectionLignesModal({ societeId, declaration, onClose, onIntegre }: {
  societeId: number,
  declaration: DeclarationDdpDto,
  onClose: () => void,
  onIntegre: (resultat: ResultatIntegrationDdpDto) => void,
}) {
  const [selection, setSelection] = useState<SelectionDdpDto | null>(null);
  const [cochees, setCochees] = useState<Set<string>>(new Set());
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [erreur, setErreur] = useState<string | null>(null);
  const [showMiseEnRoute, setShowMiseEnRoute] = useState(false);

  const charger = useCallback(async () => {
    setLoading(true);
    setErreur(null);
    try {
      const res = await getSelectionDeclarationDdp(declaration.ddpId);
      setSelection(res);
      setCochees(new Set());
    } catch (e: any) {
      setErreur(messageErreur(e, 'Erreur lors du chargement des lignes candidates.'));
      setSelection(null);
    } finally {
      setLoading(false);
    }
  }, [declaration.ddpId]);

  useEffect(() => { charger(); }, [charger]);

  const cle = (l: LigneSelectionDdpDto) => `${l.ecId}|${l.afId ?? ''}`;

  const basculer = (l: LigneSelectionDdpDto) => {
    setCochees(prev => {
      const next = new Set(prev);
      const k = cle(l);
      if (next.has(k)) next.delete(k); else next.add(k);
      return next;
    });
  };

  const toutCocher = () => {
    if (!selection) return;
    setCochees(prev => prev.size === selection.lignes.length ? new Set() : new Set(selection.lignes.map(cle)));
  };

  const handleIntegrer = async () => {
    if (!selection || cochees.size === 0) return;
    setSubmitting(true);
    setErreur(null);
    try {
      const cles: CleLigneDdp[] = selection.lignes
        .filter(l => cochees.has(cle(l)))
        .map(l => ({ ecId: l.ecId, afId: l.afId }));
      const resultat = await integrerLignesDeclarationDdp(declaration.ddpId, cles);
      onIntegre(resultat);
    } catch (e: any) {
      setErreur(messageErreur(e, 'Erreur lors de l\'intégration des lignes.'));
    } finally {
      setSubmitting(false);
    }
  };

  const societeNonConfiguree = selection != null && selection.dateMiseEnRouteSociete == null;

  return (
    <ModalShell titre={`Lignes hors délai — ${declaration.numero}`} onClose={onClose} maxWidth="1150px">
      <div style={{ padding: '0.75rem 1.25rem', display: 'flex', flexDirection: 'column', gap: '0.75rem', flex: 1, minHeight: 0 }}>
        {/* Bornes de période : AFFICHÉES, jamais saisissables. */}
        <div style={{ padding: '0.5rem 0.75rem', background: 'var(--bg-secondary)', borderRadius: '4px', fontSize: '0.8rem', display: 'flex', gap: '1.25rem', flexWrap: 'wrap', alignItems: 'center' }}>
          <span>
            Période de la déclaration : <strong data-testid="periode-selection">{formatDate(declaration.dateDebut)} → {formatDate(declaration.dateFin)}</strong>
          </span>
          <span style={{ color: 'var(--text-secondary)' }}>{libellePeriode(declaration)} — non modifiable</span>
          {selection && <span style={{ color: 'var(--text-secondary)' }}>{selection.nombreEcheancesExaminees} échéance(s) examinée(s)</span>}
        </div>

        {erreur && <BandeauErreur message={erreur} />}

        {societeNonConfiguree && (
          <div style={{ padding: '0.6rem 0.85rem', background: 'var(--status-blocking-bg)', color: 'var(--status-blocking-text)', borderRadius: '4px', fontSize: '0.82rem', display: 'flex', gap: '0.6rem', alignItems: 'center', flexWrap: 'wrap' }}>
            <AlertTriangle size={16} />
            <span>
              Aucune date de mise en route n'est configurée pour cette société : aucune ligne n'est
              intégrable et aucun dépassement n'est calculé.
            </span>
            <button className="btn" style={btnStyle} onClick={() => setShowMiseEnRoute(true)}>
              <Settings2 size={14} /> Saisir la date de mise en route
            </button>
          </div>
        )}

        {loading ? (
          <div style={{ padding: '2rem', textAlign: 'center' }}>
            <Loader2 size={20} className="animate-spin" style={{ color: 'var(--accent-primary)' }} />
          </div>
        ) : selection && (
          <>
            <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', fontSize: '0.8rem' }}>
              <button className="btn" style={btnStyle} onClick={toutCocher} disabled={selection.lignes.length === 0}>
                {cochees.size === selection.lignes.length && selection.lignes.length > 0 ? 'Tout décocher' : 'Tout cocher'}
              </button>
              <span>Candidates : <strong>{selection.lignes.length}</strong></span>
              <span>Sélectionnées : <strong data-testid="nb-cochees">{cochees.size}</strong></span>
              {selection.lignesRepriseManuelleRequise.length > 0 && (
                <span style={{ color: 'var(--status-blocking-text)' }}>
                  Non intégrables (reprise manuelle requise) : <strong>{selection.lignesRepriseManuelleRequise.length}</strong>
                </span>
              )}
            </div>

            <div style={{ flex: 1, minHeight: '220px', overflow: 'auto', border: '1px solid var(--border-color)', borderRadius: '4px' }}>
              <LignesSelectionTable
                lignes={selection.lignes}
                cochees={cochees}
                cle={cle}
                onBasculer={basculer}
              />
              {selection.lignes.length === 0 && (
                <div style={{ padding: '2rem', textAlign: 'center', color: 'var(--text-secondary)', fontSize: '0.85rem' }}>
                  Aucune ligne candidate pour cette période.
                </div>
              )}

              {selection.lignesRepriseManuelleRequise.length > 0 && (
                <>
                  <div style={{ padding: '0.45rem 0.75rem', background: 'var(--status-blocking-bg)', color: 'var(--status-blocking-text)', fontWeight: 600, fontSize: '0.78rem', borderTop: '1px solid var(--border-color)' }}>
                    Antérieures à la mise en route — retard réel inconnu (non intégrables)
                  </div>
                  <LignesSelectionTable
                    lignes={selection.lignesRepriseManuelleRequise}
                    cochees={new Set()}
                    cle={cle}
                    onBasculer={undefined}
                  />
                </>
              )}
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '1rem', paddingBottom: '0.5rem' }}>
              <button type="button" onClick={onClose} className="btn" style={{ background: 'transparent', border: '1px solid var(--border-color)', padding: '0.5rem 1rem', borderRadius: '4px' }}>
                Fermer
              </button>
              <button
                type="button"
                onClick={handleIntegrer}
                disabled={submitting || cochees.size === 0}
                className="btn btn-primary"
                style={{ background: 'var(--accent-primary)', color: 'white', border: 'none', padding: '0.5rem 1.5rem', borderRadius: '4px', display: 'flex', alignItems: 'center', gap: '0.5rem', opacity: cochees.size === 0 ? 0.55 : 1 }}
              >
                {submitting && <Loader2 size={16} className="animate-spin" />}
                Intégrer {cochees.size > 0 ? `(${cochees.size})` : ''}
              </button>
            </div>
          </>
        )}
      </div>

      {showMiseEnRoute && (
        <MiseEnRouteDelaiPaiementModal
          societeId={societeId}
          onClose={() => setShowMiseEnRoute(false)}
          onSaved={async () => { setShowMiseEnRoute(false); await charger(); }}
        />
      )}
    </ModalShell>
  );
}

/**
 * Table des lignes de sélection. `onBasculer` absent ⇒ lignes NON sélectionnables (bloc « reprise
 * manuelle requise ») : la case à cocher n'est même pas rendue, l'intégration est structurellement
 * impossible depuis ce bloc.
 */
function LignesSelectionTable({ lignes, cochees, cle, onBasculer }: {
  lignes: LigneSelectionDdpDto[],
  cochees: Set<string>,
  cle: (l: LigneSelectionDdpDto) => string,
  onBasculer?: (l: LigneSelectionDdpDto) => void,
}) {
  if (lignes.length === 0) return null;
  return (
    <div style={{ fontSize: '0.78rem', minWidth: '1050px' }}>
      <div style={{ display: 'flex', position: 'sticky', top: 0, background: 'var(--bg-secondary)', zIndex: 5, borderBottom: '1px solid var(--border-color)', fontWeight: 600 }}>
        <div style={{ flex: '0 0 40px', padding: '0.4rem' }} />
        <div style={{ flex: '1 1 0', minWidth: '180px', padding: '0.4rem 0.6rem' }}>Fournisseur</div>
        <div style={{ flex: '0 0 140px', padding: '0.4rem 0.6rem' }}>Facture</div>
        <div style={{ flex: '0 0 105px', padding: '0.4rem 0.6rem' }}>Date facture</div>
        <div style={{ flex: '0 0 115px', padding: '0.4rem 0.6rem' }}>Échéance légale</div>
        <div style={{ flex: '0 0 130px', padding: '0.4rem 0.6rem' }}>Déjà déclaré au</div>
        <div style={{ flex: '0 0 115px', padding: '0.4rem 0.6rem' }}>Constaté au</div>
        <div style={{ flex: '0 0 125px', padding: '0.4rem 0.6rem', textAlign: 'right' }}>Dépassement (j)</div>
        <div style={{ flex: '0 0 130px', padding: '0.4rem 0.6rem', textAlign: 'right' }}>Montant</div>
      </div>
      {lignes.map(l => {
        const k = cle(l);
        const bloquee = l.statut === 'RepriseManuelleRequise';
        return (
          <div
            key={k}
            data-testid={`ligne-selection-${k}`}
            style={{ display: 'flex', borderBottom: '1px solid var(--border-color)', background: cochees.has(k) ? '#eef2ff' : 'white', cursor: onBasculer ? 'pointer' : 'default' }}
            onClick={onBasculer ? () => onBasculer(l) : undefined}
          >
            <div style={{ flex: '0 0 40px', padding: '0.35rem 0.4rem', display: 'flex', justifyContent: 'center', alignItems: 'center' }}>
              {onBasculer
                ? <input type="checkbox" checked={cochees.has(k)} onChange={() => onBasculer(l)} onClick={e => e.stopPropagation()} aria-label={`Sélectionner ${l.doNumero ?? l.ecId}`} />
                : <span style={{ color: 'var(--text-secondary)' }}>—</span>}
            </div>
            <div style={{ flex: '1 1 0', minWidth: '180px', padding: '0.35rem 0.6rem', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
              <strong>{l.tiersCode}</strong> <span style={{ color: 'var(--text-secondary)' }}>{l.tiersIntitule}</span>
            </div>
            <div style={{ flex: '0 0 140px', padding: '0.35rem 0.6rem' }}>{l.doNumero || '—'}</div>
            <div style={{ flex: '0 0 105px', padding: '0.35rem 0.6rem' }}>{formatDate(l.doDate)}</div>
            <div style={{ flex: '0 0 115px', padding: '0.35rem 0.6rem' }}>{formatDate(l.echeanceLegale)}</div>
            <div style={{ flex: '0 0 130px', padding: '0.35rem 0.6rem' }}>
              {l.borneReference ? formatDate(l.borneReference) : <span style={{ color: 'var(--text-secondary)' }}>—</span>}
            </div>
            <div style={{ flex: '0 0 115px', padding: '0.35rem 0.6rem' }}>{formatDate(l.borneActuelle)}</div>
            <div style={{ flex: '0 0 125px', padding: '0.35rem 0.6rem', textAlign: 'right' }}>
              {/* Jamais un 0 ni un chiffre calculé pour une ligne bloquée : un badge explicite. */}
              {bloquee
                ? <Badge texte="retard inconnu" ton="block" />
                : <strong>{l.depassement}</strong>}
            </div>
            <div style={{ flex: '0 0 130px', padding: '0.35rem 0.6rem', textAlign: 'right' }}>{formatMoney(l.montantLigne)}</div>
          </div>
        );
      })}
    </div>
  );
}
