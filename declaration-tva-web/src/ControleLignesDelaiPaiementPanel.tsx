import { useCallback, useEffect, useMemo, useState } from 'react';
import { AlertTriangle, CalendarSearch, Loader2, PencilLine, Settings2 } from 'lucide-react';
import { ColumnSelector } from './ColumnSelector';
import { ExcelFilter } from './ExcelFilter';
import { useColumnPrefs } from './useColumnPrefs';
import { formatDate, formatMoney } from './utils';
import { MiseEnRouteDelaiPaiementModal, RepriseManuelleLigneModal, BandeauErreur } from './MiseEnRouteDelaiPaiementModal';
import { getControleLignesDdp, getParametrageTypeDdp } from './api';
import type { LigneSelectionDdpDto, SelectionDdpDto } from './api';

// ─── TASK-134 écran 4 — Contrôle des lignes hors délai (CDC §5.A-9) ─────────────────────────────
//
// Écran de SIMPLE VISIBILITÉ / REPORTING, strictement séparé du workflow d'intégration : il n'offre
// AUCUN moyen de rattacher une ligne à une déclaration (aucun appel à POST .../lignes n'existe dans
// ce fichier — l'intégration passe exclusivement par la popup de la fiche déclaration). Le seul
// bouton d'écriture ici est la reprise manuelle TASK-128, qui ne touche pas aux déclarations.
//
// FILTRE DE PÉRIODE RAISONNÉ (point corrigé PO du 19/07/2026) : exercice + type (+ trimestre). Les
// bornes exactes sont CALCULÉES côté serveur par DeclarationDelaiPaiementCycleDeVie.CalculerPeriode —
// la même fonction que la création d'une déclaration (TASK-132). Le legacy
// (FrmControleLigneDelaisPaiement.cs:56-60) utilisait deux dates libres sans lien avec le paramétrage
// société : cette anomalie n'est PAS reproduite ici, et aucun champ de date libre n'existe dans cet
// écran. Le type proposé par défaut vient de P_SOCIETE.SO_TypeDecDP (CDC §7.1).

type Col = { key: string; label: string; width?: string; align?: 'left' | 'right' | 'center'; filterType?: 'list' | 'text' };

const COLUMNS: Col[] = [
  { key: 'statut', label: 'Statut', width: '165px', align: 'center', filterType: 'list' },
  { key: 'tiers', label: 'Fournisseur', filterType: 'text' },
  { key: 'facture', label: 'Facture', width: '140px' },
  { key: 'doDate', label: 'Date facture', width: '110px', align: 'center' },
  { key: 'echeanceLegale', label: 'Échéance légale', width: '120px', align: 'center' },
  { key: 'origineDelai', label: 'Origine du délai', width: '135px', align: 'center', filterType: 'list' },
  { key: 'borneReference', label: 'Déjà déclaré au', width: '125px', align: 'center' },
  { key: 'borneActuelle', label: 'Constaté au', width: '115px', align: 'center' },
  { key: 'depassement', label: 'Dépassement (j)', width: '130px', align: 'right' },
  { key: 'montant', label: 'Montant', width: '130px', align: 'right' },
  { key: 'bucket', label: 'Cas', width: '175px', filterType: 'list' },
  { key: 'actions', label: 'Action', width: '150px', align: 'center' },
];

const colStyle = (col: Col): React.CSSProperties =>
  col.width ? { flex: `0 0 ${col.width}`, width: col.width } : { flex: '1 1 0', minWidth: '170px' };
const colJustify = (col: Col) => (col.align === 'right' ? 'flex-end' : col.align === 'center' ? 'center' : 'flex-start');

const EXERCICE_MIN = 2023; // 2023-07-01 = début de la déclaration légale (SeuilsLegauxDelaiPaiement).

const LIBELLES_BUCKET: Record<string, string> = {
  HorsPeriodePartNonAffectee: 'Hors période — part non payée',
  HorsPeriodePartAffectee: 'Hors période — part payée',
  DansPeriodePartAffectee: 'Dans la période — part payée',
  DansPeriodePartNonAffectee: 'Dans la période — part non payée',
};

const LIBELLES_ORIGINE_DELAI: Record<string, string> = {
  ConventionFacture: 'Convention facture',
  Convention: 'Convention',
  Defaut: 'Défaut société',
};

export function ControleLignesDelaiPaiementPanel({ societeId, showToast }: {
  societeId: number,
  showToast: (m: string, t?: 'success' | 'error' | 'warning') => void,
}) {
  const anneeCourante = new Date().getFullYear();
  const exercices = useMemo(
    () => Array.from({ length: anneeCourante + 1 - EXERCICE_MIN + 1 }, (_, i) => EXERCICE_MIN + i).reverse(),
    [anneeCourante],
  );

  const [exercice, setExercice] = useState(anneeCourante);
  const [type, setType] = useState<'annuelle' | 'trimestrielle'>('trimestrielle');
  const [trimestre, setTrimestre] = useState(1);

  const [resultat, setResultat] = useState<SelectionDdpDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [erreur, setErreur] = useState<string | null>(null);
  const [filters, setFilters] = useState<Record<string, string | string[]>>({});
  const [showMiseEnRoute, setShowMiseEnRoute] = useState(false);
  const [repriseCible, setRepriseCible] = useState<LigneSelectionDdpDto | null>(null);

  const { visibleColumns, visibleKeys, toggle, reset } = useColumnPrefs('grf.cols.controleLignesDelaiPaiement', COLUMNS);

  // Type par défaut de la société (proposition, jamais une contrainte).
  useEffect(() => {
    (async () => {
      try {
        const res = await getParametrageTypeDdp(societeId);
        if (res.typeParDefaut === 'Annuelle') setType('annuelle');
        else if (res.typeParDefaut === 'Trimestrielle') setType('trimestrielle');
      } catch {
        // Paramétrage illisible : on garde le trimestriel, sans erreur bloquante.
      }
    })();
  }, [societeId]);

  const charger = useCallback(async () => {
    setLoading(true);
    setErreur(null);
    try {
      const res = await getControleLignesDdp(societeId, exercice, type, type === 'trimestrielle' ? trimestre : null);
      setResultat(res);
    } catch (e: any) {
      console.error(e);
      setErreur(e?.response?.data?.Message || e?.response?.data?.message || 'Erreur lors du chargement des lignes hors délai.');
      setResultat(null);
    } finally {
      setLoading(false);
    }
  }, [societeId, exercice, type, trimestre]);

  useEffect(() => { charger(); }, [charger]);

  // Une SEULE liste affichée (candidates + bloquées), le statut restant explicite colonne par colonne :
  // l'écran de contrôle est un écran de visibilité, il ne doit rien masquer.
  const toutesLignes = useMemo(
    () => resultat ? [...resultat.lignes, ...resultat.lignesRepriseManuelleRequise] : [],
    [resultat],
  );

  const handleFilterChange = (key: string, val: any) => {
    setFilters(prev => {
      const next = { ...prev };
      if (val === '' || (Array.isArray(val) && val.length === 0)) delete next[key];
      else next[key] = val;
      return next;
    });
  };

  const visibleRows = toutesLignes.filter(l => {
    const fTiers = filters['tiers'];
    if (typeof fTiers === 'string' && fTiers.trim() !== '') {
      const needle = fTiers.trim().toLowerCase();
      const cible = `${l.tiersCode ?? ''} ${l.tiersIntitule ?? ''}`.toLowerCase();
      if (!cible.includes(needle)) return false;
    }
    const fStatut = filters['statut'];
    if (Array.isArray(fStatut) && fStatut.length > 0) {
      const v = l.statut === 'RepriseManuelleRequise' ? 'Reprise manuelle requise' : 'Retard calculé';
      if (!fStatut.includes(v)) return false;
    }
    const fBucket = filters['bucket'];
    if (Array.isArray(fBucket) && fBucket.length > 0 && !fBucket.includes(LIBELLES_BUCKET[l.bucket] ?? l.bucket)) return false;
    const fOrigine = filters['origineDelai'];
    if (Array.isArray(fOrigine) && fOrigine.length > 0 && !fOrigine.includes(LIBELLES_ORIGINE_DELAI[l.origineDelai] ?? l.origineDelai)) return false;
    return true;
  });

  const optionsFor = (key: string): { label: string, value: string }[] => {
    if (key === 'statut') return [{ label: 'Retard calculé', value: 'Retard calculé' }, { label: 'Reprise manuelle requise', value: 'Reprise manuelle requise' }];
    if (key === 'bucket') return Object.values(LIBELLES_BUCKET).map(v => ({ label: v, value: v }));
    if (key === 'origineDelai') return Object.values(LIBELLES_ORIGINE_DELAI).map(v => ({ label: v, value: v }));
    return [];
  };

  const renderCell = (col: Col, l: LigneSelectionDdpDto) => {
    const bloquee = l.statut === 'RepriseManuelleRequise';
    switch (col.key) {
      case 'statut':
        // Badge EXPLICITE demandé par la TASK pour les lignes antérieures à la mise en route.
        return bloquee
          ? (
            <span
              data-testid="badge-reprise-manuelle"
              title="Antérieure à la mise en route — retard réel inconnu tant que la reprise manuelle n'est pas saisie"
              style={{ background: 'var(--status-blocking-bg)', color: 'var(--status-blocking-text)', padding: '2px 8px', borderRadius: '99px', fontSize: '0.68rem', fontWeight: 600, whiteSpace: 'nowrap' }}
            >
              Reprise manuelle requise
            </span>
          )
          : (
            <span style={{ background: 'var(--status-ok-bg)', color: 'var(--status-ok-text)', padding: '2px 8px', borderRadius: '99px', fontSize: '0.68rem', fontWeight: 600, whiteSpace: 'nowrap' }}>
              Retard calculé
            </span>
          );
      case 'tiers':
        return <span><strong>{l.tiersCode}</strong> <span style={{ color: 'var(--text-secondary)' }}>{l.tiersIntitule}</span></span>;
      case 'facture':
        return l.doNumero || '—';
      case 'doDate':
        return formatDate(l.doDate);
      case 'echeanceLegale':
        return formatDate(l.echeanceLegale);
      case 'origineDelai':
        return `${LIBELLES_ORIGINE_DELAI[l.origineDelai] ?? l.origineDelai} (${l.nombreJoursDelaiApplique} j)`;
      case 'borneReference':
        return l.borneReference ? formatDate(l.borneReference) : <span style={{ color: 'var(--text-secondary)' }}>—</span>;
      case 'borneActuelle':
        return formatDate(l.borneActuelle);
      case 'depassement':
        // JAMAIS un Depassement calculé automatiquement pour une ligne antérieure à la mise en route.
        return bloquee
          ? <span style={{ color: 'var(--status-blocking-text)', fontStyle: 'italic' }}>inconnu</span>
          : <strong>{l.depassement}</strong>;
      case 'montant':
        return formatMoney(l.montantLigne);
      case 'bucket':
        return <span style={{ fontSize: '0.72rem', color: 'var(--text-secondary)' }}>{LIBELLES_BUCKET[l.bucket] ?? l.bucket}</span>;
      case 'actions':
        // Aucune action d'intégration ici (rôle strictement séparé de la popup de sélection) :
        // uniquement la saisie de reprise manuelle, qui débloque le calcul pour cette échéance.
        return bloquee
          ? (
            <button
              className="btn"
              style={{ display: 'inline-flex', alignItems: 'center', gap: '0.3rem', fontSize: '0.75rem', padding: '0.2rem 0.5rem' }}
              onClick={() => setRepriseCible(l)}
              title="Saisir « déjà déclaré jusqu'au [date] » pour cette facture"
            >
              <PencilLine size={13} /> Saisie manuelle
            </button>
          )
          : <span style={{ color: 'var(--text-secondary)' }}>—</span>;
      default:
        return null;
    }
  };

  return (
    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', height: '100%', overflow: 'hidden' }}>
      {/* En-tête + filtre de période RAISONNÉ (aucune date libre) */}
      <div style={{ padding: '0.75rem 1rem', borderBottom: '1px solid var(--border-color)', background: 'white', display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: '0.75rem' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.6rem' }}>
          <CalendarSearch size={20} style={{ color: 'var(--accent-primary)' }} />
          <div>
            <h2 style={{ margin: 0, fontSize: '1.05rem', fontWeight: 600 }}>Contrôle des lignes hors délai</h2>
            <div style={{ fontSize: '0.72rem', color: 'var(--text-secondary)' }}>
              Délai de Paiement Maroc — CDC §5.A-9 · visibilité seule, aucune intégration depuis cet écran
            </div>
          </div>
        </div>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', flexWrap: 'wrap' }}>
          <label style={{ fontSize: '0.78rem', color: 'var(--text-secondary)' }}>Exercice</label>
          <select className="form-input" style={{ width: '100px' }} value={exercice} onChange={e => setExercice(Number(e.target.value))}>
            {exercices.map(a => <option key={a} value={a}>{a}</option>)}
          </select>
          <label style={{ fontSize: '0.78rem', color: 'var(--text-secondary)' }}>Type</label>
          <select className="form-input" style={{ width: '150px' }} value={type} onChange={e => setType(e.target.value as 'annuelle' | 'trimestrielle')}>
            <option value="trimestrielle">Trimestrielle</option>
            <option value="annuelle">Annuelle</option>
          </select>
          {type === 'trimestrielle' && (
            <select className="form-input" style={{ width: '90px' }} value={trimestre} onChange={e => setTrimestre(Number(e.target.value))} aria-label="Trimestre">
              <option value={1}>T1</option>
              <option value={2}>T2</option>
              <option value={3}>T3</option>
              <option value={4}>T4</option>
            </select>
          )}
          <button className="btn" style={{ display: 'inline-flex', alignItems: 'center', gap: '0.35rem', fontSize: '0.78rem', padding: '0.3rem 0.6rem' }} onClick={() => setShowMiseEnRoute(true)}>
            <Settings2 size={14} /> Date de mise en route
          </button>
          <ColumnSelector columns={COLUMNS} visibleKeys={visibleKeys} onToggle={toggle} onReset={reset} />
        </div>
      </div>

      {/* Bornes CALCULÉES par le serveur, affichées en lecture seule */}
      <div style={{ padding: '0.4rem 1rem', borderBottom: '1px solid var(--border-color)', background: 'var(--bg-secondary)', display: 'flex', justifyContent: 'space-between', alignItems: 'center', fontSize: '0.8rem', gap: '1rem', flexWrap: 'wrap' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '1rem', flexWrap: 'wrap' }}>
          {loading && <Loader2 size={15} className="animate-spin" style={{ color: 'var(--accent-primary)' }} />}
          {resultat && (
            <span>
              Période calculée : <strong data-testid="periode-calculee">{formatDate(resultat.dateDebutPeriode)} → {formatDate(resultat.dateFinPeriode)}</strong>
            </span>
          )}
          <span>Lignes : <strong>{visibleRows.length}</strong> / {toutesLignes.length}</span>
          {resultat && resultat.lignesRepriseManuelleRequise.length > 0 && (
            <span style={{ color: 'var(--status-blocking-text)' }}>
              dont <strong>{resultat.lignesRepriseManuelleRequise.length}</strong> en reprise manuelle requise
            </span>
          )}
          {resultat && <span style={{ color: 'var(--text-secondary)' }}>{resultat.nombreEcheancesExaminees} échéance(s) examinée(s)</span>}
        </div>
        {Object.keys(filters).length > 0 && (
          <button className="btn" onClick={() => setFilters({})} style={{ background: 'transparent', border: 'none', color: 'var(--accent-primary)', textDecoration: 'underline', padding: 0, fontSize: '0.8rem', cursor: 'pointer' }}>
            Effacer filtres ({Object.keys(filters).length})
          </button>
        )}
      </div>

      {erreur && <div style={{ padding: '0.75rem 1rem' }}><BandeauErreur message={erreur} /></div>}

      {resultat && resultat.dateMiseEnRouteSociete == null && (
        <div style={{ margin: '0.6rem 1rem', padding: '0.6rem 0.85rem', background: 'var(--status-blocking-bg)', color: 'var(--status-blocking-text)', borderRadius: '4px', fontSize: '0.82rem', display: 'flex', gap: '0.6rem', alignItems: 'center', flexWrap: 'wrap' }}>
          <AlertTriangle size={16} />
          <span data-testid="alerte-mise-en-route">
            Aucune date de mise en route n'est configurée pour cette société : aucun dépassement n'est
            calculé, toutes les lignes restent en « reprise manuelle requise ».
          </span>
          <button className="btn" style={{ display: 'inline-flex', alignItems: 'center', gap: '0.35rem', fontSize: '0.78rem', padding: '0.25rem 0.55rem' }} onClick={() => setShowMiseEnRoute(true)}>
            <Settings2 size={14} /> Saisir la date
          </button>
        </div>
      )}

      <div style={{ flexGrow: 1, overflow: 'auto', background: 'white' }}>
        <div style={{ minWidth: '1700px', fontSize: '0.8125rem' }}>
          <div style={{ display: 'flex', position: 'sticky', top: 0, background: 'var(--bg-secondary)', zIndex: 10, boxShadow: '0 1px 2px rgba(0,0,0,0.05)', borderBottom: '1px solid var(--border-color)' }}>
            {visibleColumns.map(col => (
              <div key={col.key} style={{ ...colStyle(col), padding: '0.5rem 0.6rem', borderRight: '1px solid var(--border-color)', display: 'flex', alignItems: 'center', justifyContent: 'center', gap: '0.25rem', fontWeight: 600, whiteSpace: 'nowrap' }}>
                {col.label}
                {col.filterType && (
                  <ExcelFilter
                    filterType={col.filterType}
                    options={optionsFor(col.key)}
                    selectedValues={Array.isArray(filters[col.key]) ? filters[col.key] as string[] : []}
                    textValue={typeof filters[col.key] === 'string' ? filters[col.key] as string : ''}
                    onChange={(val) => handleFilterChange(col.key, val)}
                  />
                )}
              </div>
            ))}
          </div>

          {visibleRows.map(l => (
            <div key={`${l.ecId}|${l.afId ?? ''}`} style={{ display: 'flex', borderBottom: '1px solid var(--border-color)' }}>
              {visibleColumns.map(col => (
                <div key={col.key} style={{ ...colStyle(col), padding: '0.35rem 0.6rem', borderRight: '1px solid var(--border-color)', display: 'flex', alignItems: 'center', justifyContent: colJustify(col), whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                  {renderCell(col, l)}
                </div>
              ))}
            </div>
          ))}
        </div>
        {visibleRows.length === 0 && !loading && !erreur && (
          <div style={{ padding: '3rem', textAlign: 'center', color: 'var(--text-secondary)' }}>
            Aucune ligne hors délai pour cette période.
          </div>
        )}
      </div>

      {showMiseEnRoute && (
        <MiseEnRouteDelaiPaiementModal
          societeId={societeId}
          onClose={() => setShowMiseEnRoute(false)}
          onSaved={async () => { setShowMiseEnRoute(false); showToast('Date de mise en route enregistrée', 'success'); await charger(); }}
        />
      )}

      {repriseCible && (
        <RepriseManuelleLigneModal
          societeId={societeId}
          ecId={repriseCible.ecId}
          doNumero={repriseCible.doNumero}
          echeanceLegale={repriseCible.echeanceLegale}
          onClose={() => setRepriseCible(null)}
          onSaved={async () => { setRepriseCible(null); showToast('Reprise manuelle enregistrée', 'success'); await charger(); }}
        />
      )}
    </div>
  );
}
