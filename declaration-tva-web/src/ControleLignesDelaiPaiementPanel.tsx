import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { AlertTriangle, CalendarSearch, Loader2, PencilLine, Settings2 } from 'lucide-react';
import type { ColDef } from 'ag-grid-community';
import { ApbsGrid } from './grid/ApbsGrid';
import { CustomListFilter } from './grid/CustomListFilter';
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

const LIBELLES_MODE: Record<string, string> = {
  Espece: 'Espèce',
  Cheque: 'Chèque',
  Traite: 'Traite',
  Virement: 'Virement',
  Autre: 'Autre',
};



const EXERCICE_MIN = 2023; // 2023-07-01 = début de la déclaration légale (SeuilsLegauxDelaiPaiement).

const CAS_PAYE_HORS_DELAI = 'Payé hors délai';
const CAS_PAYE_NON_RAPPROCHE = 'Payé non rapproché';
const CAS_NON_PAYE = 'Non payé';


/**
 * Libellé du "Cas" (colonne, ex-"bucket") — demande PO : distinguer un règlement affecté mais
 * PAS ENCORE rapproché en banque (chèque/traite/virement) du même montant une fois rapproché.
 * Numériquement les deux sont déjà traités pareil par le calculateur (retard qui court jusqu'à
 * la fin de période tant que non rapproché, cf. BorneActuelleDansPeriode) — cette distinction est
 * uniquement un raffinement d'affichage, aucun changement de calcul.
 */
function libelleCas(l: LigneSelectionDdpDto): string {
  const estPartAffectee = l.bucket === 'DansPeriodePartAffectee' || l.bucket === 'HorsPeriodePartAffectee';
  if (!estPartAffectee) return CAS_NON_PAYE;
  const estPiece = l.typeReglement === 'Cheque' || l.typeReglement === 'Traite' || l.typeReglement === 'Virement';
  const estRapprochee = !!l.dateRapprochement;
  return (estPiece && !estRapprochee) ? CAS_PAYE_NON_RAPPROCHE : CAS_PAYE_HORS_DELAI;
}

const LIBELLES_ORIGINE_DELAI: Record<string, string> = {
  ConventionFacture: 'Convention facture',
  Convention: 'Convention',
  Defaut: 'Défaut société',
};

/**
 * AUDIT UX — « jamais un zéro silencieux ». Un tableau vide a TROIS causes très différentes pour un
 * comptable, qui doivent être nommées explicitement au lieu du message unique « Aucune ligne hors
 * délai pour cette période » (qui se lisait à tort « aucun retard chez nos fournisseurs ») :
 *   1. des filtres de colonne masquent les lignes chargées ;
 *   2. le retard de ces échéances a DÉJÀ été déclaré (calcul incrémental anti-double-déclaration) —
 *      c'est le cas le plus fréquent et le plus trompeur ;
 *   3. il n'y a réellement aucun retard sur la période.
 */


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

  // Garde anti-course : le type par défaut de la société (effet séparé ci-dessus) arrive de
  // façon asynchrone et peut déclencher un 2ᵉ appel pendant que le 1ᵉʳ (état initial
  // trimestriel) est encore en vol. Sans cette garde, si l'ancienne réponse arrive APRÈS la
  // nouvelle, elle écrase silencieusement le bon résultat avec une période obsolète (bug
  // constaté : sélecteur affichant "Annuelle" mais données/période encore du 1ᵉʳ trimestre).
  const requeteEnCoursId = useRef(0);

  const charger = useCallback(async () => {
    const idRequete = ++requeteEnCoursId.current;
    setLoading(true);
    setErreur(null);
    try {
      const res = await getControleLignesDdp(societeId, exercice, type, type === 'trimestrielle' ? trimestre : null);
      if (idRequete !== requeteEnCoursId.current) return; // réponse obsolète, une requête plus récente est en vol
      setResultat(res);
    } catch (e: any) {
      if (idRequete !== requeteEnCoursId.current) return;
      console.error(e);
      setErreur(e?.response?.data?.Message || e?.response?.data?.message || 'Erreur lors du chargement des lignes hors délai.');
      setResultat(null);
    } finally {
      if (idRequete === requeteEnCoursId.current) setLoading(false);
    }
  }, [societeId, exercice, type, trimestre]);

  useEffect(() => { charger(); }, [charger]);

  const toutesLignes = useMemo(
    () => (resultat ? [...resultat.lignes, ...resultat.lignesRepriseManuelleRequise] : []),
    [resultat],
  );

  const columnDefs: ColDef[] = useMemo(() => [
    { field: 'statut', headerName: 'Statut', width: 165, filter: CustomListFilter, cellRenderer: (p: any) => p.data ? (p.data.estRepriseManuelleRequise ? <span style={{ color: '#b45309', fontWeight: 600 }}>Reprise manuelle requise</span> : <span style={{ color: 'var(--status-ok-text)', fontWeight: 600 }}>Retard calculé</span>) : null },
    { field: 'tiers', headerName: 'Fournisseur', filter: 'agTextColumnFilter', valueGetter: (p) => p.data ? `${p.data.tiersCode || ''} · ${p.data.tiersIntitule || ''}` : '' },
    { field: 'facture', headerName: 'Facture', width: 140, filter: CustomListFilter, valueGetter: (p) => p.data?.doNumero || '' },
    { field: 'doDate', headerName: 'Date facture', width: 110, valueGetter: (p) => p.data ? formatDate(p.data.doDate) : '' },
    { field: 'echeanceLegale', headerName: 'Échéance légale', width: 120, valueGetter: (p) => p.data ? formatDate(p.data.echeanceLegale) : '' },
    { field: 'origineDelai', headerName: 'Origine du délai', width: 165, filter: CustomListFilter, valueGetter: (p) => p.data ? `${LIBELLES_ORIGINE_DELAI[p.data.origineDelai] || p.data.origineDelai} (${p.data.nombreJoursDelaiApplique} j)` : '' },
    { field: 'borneReference', headerName: 'Dernière déclaration', width: 150, headerTooltip: "Date jusqu'à laquelle le retard de cette échéance a déjà été signalé dans une déclaration DDP précédente. Vide si l'échéance n'a jamais été déclarée.", valueGetter: (p) => p.data ? formatDate(p.data.borneReference) : '' },
    { field: 'borneActuelle', headerName: 'Constaté le', width: 125, headerTooltip: "Date jusqu'à laquelle le retard est compté pour cette période : la date de paiement si l'échéance est réglée, sinon la fin de la période en cours tant qu'elle reste impayée.", valueGetter: (p) => p.data ? formatDate(p.data.borneActuelle) : '' },
    { field: 'depassement', headerName: 'Dépassement (j)', width: 130, type: 'numericColumn', headerTooltip: 'Nombre de jours de retard NOUVEAUX depuis la dernière déclaration — pas le retard total depuis l\'échéance légale, pour éviter de compter deux fois le même retard.', valueGetter: (p) => p.data?.depassement ?? 0 },
    { field: 'montant', headerName: 'Montant', width: 130, type: 'numericColumn', valueGetter: (p) => p.data ? formatMoney(p.data.montantLigne) : '' },
    { field: 'reglementNumero', headerName: 'N° Règlement', width: 120, valueGetter: (p) => p.data?.reglementNumero || '—' },
    { field: 'mode', headerName: 'Mode', width: 100, valueGetter: (p) => p.data?.typeReglement ? (LIBELLES_MODE[p.data.typeReglement] || p.data.typeReglement) : '—' },
    { field: 'bucket', headerName: 'Cas', width: 175, filter: CustomListFilter, valueGetter: (p) => p.data ? libelleCas(p.data) : '' },
    {
      headerName: 'Action',
      width: 150,
      pinned: 'right',
      suppressHeaderMenuButton: true,
      cellRenderer: (p: any) => {
        const l = p.data;
        if (!l || !l.estRepriseManuelleRequise) return null;
        return (
          <button
            className="btn btn-primary"
            style={{ display: 'inline-flex', alignItems: 'center', gap: '0.35rem', fontSize: '0.78rem', padding: '0.3rem 0.6rem' }}
            onClick={() => setRepriseCible(l)}
          >
            <PencilLine size={13} /> Reprise manuelle
          </button>
        );
      }
    }
  ], []);

  return (
    <div style={{ flex: 1, display: 'flex', flexDirection: 'column', height: '100%', overflow: 'hidden' }}>
      {/* En-tête + filtre de période RAISONNÉ (aucune date libre) */}
      <div style={{ padding: '0.75rem 1rem', borderBottom: '1px solid var(--border-color)', background: 'white', display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: '0.75rem' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '0.6rem' }}>
          <CalendarSearch size={20} style={{ color: 'var(--accent-primary)' }} />
          <div>
            <h2 style={{ margin: 0, fontSize: '1.05rem', fontWeight: 600 }}>Contrôle des lignes hors délai</h2>
            <div style={{ fontSize: '0.72rem', color: 'var(--text-secondary)' }}>
              Délai de Paiement Maroc — visibilité seule, aucune intégration depuis cet écran
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
        </div>
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

      <div style={{ flexGrow: 1, minHeight: 0, position: 'relative', padding: '0.4rem 1rem' }}>
        <ApbsGrid
          rowData={toutesLignes}
          columnDefs={columnDefs}
          height="100%"
          showColumnSelector={true}
          showExportButton={true}
          exportFileName="controle_lignes_ddp.xlsx"
          toolbarLeft={
            <>
              {loading && <Loader2 size={15} className="animate-spin" style={{ color: 'var(--accent-primary)' }} />}
              {resultat && (
                <span>
                  Période calculée : <strong data-testid="periode-calculee">{formatDate(resultat.dateDebutPeriode)} → {formatDate(resultat.dateFinPeriode)}</strong>
                </span>
              )}
              <span>Lignes : <strong>{toutesLignes.length}</strong></span>
              {resultat && resultat.lignesRepriseManuelleRequise.length > 0 && (
                <span style={{ color: 'var(--status-blocking-text)' }}>
                  dont <strong>{resultat.lignesRepriseManuelleRequise.length}</strong> en reprise manuelle requise
                </span>
              )}
              {resultat && <span style={{ color: 'var(--text-secondary)' }}>{resultat.nombreEcheancesExaminees} échéance(s) examinée(s)</span>}
              {resultat && resultat.nombreEcheancesDejaDeclarees > 0 && (
                <span
                  style={{ color: 'var(--text-secondary)' }}
                  title="Ces échéances figurent déjà dans une déclaration antérieure : leur retard a été compté une première fois et n'est plus recompté ici (anti-double-déclaration)."
                >
                  dont <strong>{resultat.nombreEcheancesDejaDeclarees}</strong> déjà déclarée(s)
                  {resultat.derniereBorneDejaDeclaree ? ` jusqu'au ${formatDate(resultat.derniereBorneDejaDeclaree)}` : ''}
                </span>
              )}
              {Object.keys(filters).length > 0 && (
                <button className="btn" onClick={() => setFilters({})} style={{ background: 'transparent', border: 'none', color: 'var(--accent-primary)', textDecoration: 'underline', padding: 0, fontSize: '0.8rem', cursor: 'pointer' }}>
                  Effacer filtres ({Object.keys(filters).length})
                </button>
              )}
            </>
          }
        />
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
