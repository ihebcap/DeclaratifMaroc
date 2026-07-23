import { useState, useEffect, useMemo } from 'react';
import {
    Calculator, AlertTriangle, Loader2, Info, Lock, CheckCircle2, XCircle, ShieldCheck, FileStack, ExternalLink, Search, FileSpreadsheet
} from 'lucide-react';
import { formatMoney } from './utils';
import api from './api';
import type { ReglementRow } from './ReglementsSelection';
import { RecapSourceTable } from './RecapSourceTable';
import { DomainGrid } from './DomainGrid';
import { DiagnosticModal } from './DiagnosticModal';
import type { DomaineTVA } from './DeclarationStepper';

// TASK-142 : colonnes du drill « Lignes incohérentes (TTC ≠ HT+TVA) ». Reprend defaultColumns de
// DomainGrid en insérant « Montant TVA » (2ᵉ opérande de l'égalité isolée par le drill, déjà exposé
// par LigneCandidateDto) entre Taux TVA et Montant TTC, plus une colonne « Écart » purement dérivée
// en rendu (montantHT+montantTVA−montantTTC, marquée `derived` → ni triée ni filtrée côté back).
const incoherenceColumns: { key: string, label: string, filterType: 'list' | 'text' | 'number' | 'date', width?: string, derived?: boolean }[] = [
    { key: 'factureNumero', label: 'N° Facture', filterType: 'text' },
    { key: 'tiers', label: 'Tiers', filterType: 'text' },
    { key: 'origine', label: 'Origine', filterType: 'list' },
    { key: 'montantHT', label: 'Montant HT', filterType: 'number' },
    { key: 'tauxTVA', label: 'Taux TVA', filterType: 'list' },
    { key: 'montantTVA', label: 'Montant TVA', filterType: 'number' },
    { key: 'montantTTC', label: 'Montant TTC', filterType: 'number' },
    { key: 'ecart', label: 'Écart (HT+TVA−TTC)', filterType: 'number', width: '180px', derived: true },
    { key: 'source', label: 'Source', filterType: 'list' },
    { key: 'statutLigne', label: 'Statut', filterType: 'list' },
    { key: 'motif', label: 'Motif Écartement', filterType: 'text', width: '280px' },
];

// TASK-161 : colonnes du drill « Codes activité » — toutes les lignes du domaine (pas seulement
// les anomalies), avec la colonne codeActivite rendue éditable (select, cf. DomainGrid) pour la
// surcharge manuelle par ligne (décision PO : couvre le cas "même facture, deux activités").
const codeActiviteColumns: { key: string, label: string, filterType: 'list' | 'text' | 'number' | 'date', width?: string, derived?: boolean, editable?: boolean }[] = [
    { key: 'factureNumero', label: 'N° Facture', filterType: 'text' },
    { key: 'tiers', label: 'Tiers', filterType: 'text' },
    { key: 'origine', label: 'Origine', filterType: 'list' },
    { key: 'montantHT', label: 'Montant HT', filterType: 'number' },
    { key: 'tauxTVA', label: 'Taux TVA', filterType: 'list' },
    { key: 'montantTTC', label: 'Montant TTC', filterType: 'number' },
    { key: 'codeActivite', label: 'Code activité', filterType: 'text', width: '220px', editable: true },
    { key: 'statutLigne', label: 'Statut', filterType: 'list' },
];

// ─── Écran ③ Vérifier & Intégrer (TASK-090) ──────────────────────────────────
//
// Fusion de Calcul TVA (ancien ③) et Intégration (ancien ④).
// Affiche les sous-totaux par taux TVA, les contrôles pré-intégration,
// et permet l'engagement de la déclaration (POST /cloture).
//
// Principes (anti-régression) :
//  - Source unique = back valorisé (montantHT, montantTVA issus du back).
//  - Aucun front-calcul (pas de TVA = HT × taux ici).
//  - Lignes non valorisées (statutLigne IN {2,3,4}) : affichées avec motif.
//  - RecapCard est robuste au F5 (absorption TASK-086) en lisant /checkup.

type LigneValorisation = {
  id: string;
  ecId: number;
  factureNumero: string;
  tiers: string;
  tiersIdentifiantFiscal: string;
  tiersICE: string;
  tauxTVA: number;
  montantHT: number;
  montantTVA: number;
  montantTTC: number;
  prorata: number;
  montantAffecte: number;
  statutLigne: number; // 0 Proposée, 1 Intégrée, 2 Exclue, 3 Reportée, 4 Écartée
  motif: string;
  origine: string;
  statutConformite: string;
  numeroRapprochement?: string;
  domaine: string;
};

// Statuts non valorisés (cohérent avec AffectationsDrill.tsx TASK-055)
const NON_VALORISE = new Set([2, 3, 4]);

const STATUT_LIGNE_LABELS: Record<number, string> = {
  0: 'Proposée',
  1: 'Intégrée',
  2: 'Exclue',
  3: 'Reportée',
  4: 'Écartée',
};

type RowAggr = {
  ecId: number;
  factureNumero: string;
  tiers: string;
  tiersIdentifiantFiscal: string;
  tiersICE: string;
  tauxTVA: number;
  montantHT: number;    // du back, jamais recalculé
  montantTVA: number;   // du back, jamais recalculé
  nonValorise: boolean;
  motif: string;
  statutLigne: number;
  statutConformite: string;
  prorata: number;
  domaine: string;
};

// Agrégation par (facture, taux) — source unique = back.
function agregParFactureTaux(lignes: LigneValorisation[]): RowAggr[] {
  const map = new Map<string, RowAggr>();
  for (const l of lignes) {
    const key = `${l.factureNumero}__${l.tauxTVA}`;
    const existing = map.get(key);
    if (existing) {
      if (!existing.nonValorise) {
        existing.montantHT += l.montantHT;
        existing.montantTVA += l.montantTVA;
      }
    } else {
      const nonValorise = NON_VALORISE.has(l.statutLigne);
      map.set(key, {
        ecId: l.ecId,
        factureNumero: l.factureNumero,
        tiers: l.tiers,
        tiersIdentifiantFiscal: l.tiersIdentifiantFiscal,
        tiersICE: l.tiersICE,
        tauxTVA: l.tauxTVA,
        montantHT: nonValorise ? 0 : l.montantHT,
        montantTVA: nonValorise ? 0 : l.montantTVA,
        nonValorise,
        motif: l.motif,
        statutLigne: l.statutLigne,
        statutConformite: l.statutConformite,
        prorata: l.prorata,
        domaine: l.domaine,
      });
    }
  }
  return [...map.values()].sort((a, b) => {
    const cmp = a.factureNumero.localeCompare(b.factureNumero);
    if (cmp !== 0) return cmp;
    return a.tauxTVA - b.tauxTVA;
  });
}

// Sous-totaux par taux
type SousTotalTaux = {
  taux: number;
  totalHT: number;
  totalTVA: number;
  nbLignes: number;
};

function sousTotauxParTaux(rows: RowAggr[]): SousTotalTaux[] {
  const map = new Map<number, SousTotalTaux>();
  for (const r of rows) {
    if (r.nonValorise) continue;
    const existing = map.get(r.tauxTVA);
    if (existing) {
      existing.totalHT += r.montantHT;
      existing.totalTVA += r.montantTVA;
      existing.nbLignes += 1;
    } else {
      map.set(r.tauxTVA, { taux: r.tauxTVA, totalHT: r.montantHT, totalTVA: r.montantTVA, nbLignes: 1 });
    }
  }
  return [...map.values()].sort((a, b) => b.taux - a.taux);
}

async function fetchAllLignes(
  declarationId: string
): Promise<LigneValorisation[]> {
  const size = 500;
  let items: LigneValorisation[] = [];
  const domaines = ['Decaissement', 'Encaissement'];
  for (const d of domaines) {
    let page = 1;
    for (;;) {
      const res = await api.get(`/declarations/${declarationId}/lignes`, {
        params: {
          domaine: d,
          page,
          size,
        },
      });
      const chunk: LigneValorisation[] = res.data.items || [];
      const chunkWithDomaine = chunk.map(l => ({ ...l, domaine: d as any }));
      items = items.concat(chunkWithDomaine);
      const total = res.data.totalCount ?? chunk.length;
      console.log(`fetchAllLignes: domaine ${d}, page ${page}, chunk: ${chunk.length}, total accumulated: ${items.length}/${total}`);
      if (chunk.length === 0 || items.length >= total) break;
      page += 1;
    }
  }
  return items;
}

type AlertType = 'bloquant' | 'avertissement' | 'Error' | 'Warning' | 'Info';

interface Alerte {
    type: AlertType;
    message: string;
    ligneId?: string;
    domaine?: string;
    code?: string;
    refLigne?: string;
    filtre?: Record<string, any>;
}

interface Reconciliation {
    candidates: number;
    integrees: number;
    exclues: number;
    reportees: number;
    ecartees: number;
    proposees: number;
}

interface RecapLigne {
    source?: string;
    taux?: number;
    ht: number;
    tva: number;
    ttc: number;
}

interface RecapIncoherenceLigne {
    domaine: string;
    incoherente: boolean;
    ht: number;
    tva: number;
    ttc: number;
    residu: number;
    nbLignes: number;
}

interface CheckupResult {
    equilibre?: { isValid: boolean; ecart?: number; ecartExplique?: boolean };
    alertes: Alerte[];
    reconciliation: Reconciliation;
    recapSource?: (RecapLigne & { source: string })[];
    recapTaux?: (RecapLigne & { taux: number })[];
    /** TASK-112 : axe réellement discriminant pour isoler l'écart (Source était une tautologie) */
    recapIncoherence?: RecapIncoherenceLigne[];
}

function isBloquant(a: Alerte): boolean {
    return a.type === 'bloquant' || a.type === 'Error';
}
function isAvertissement(a: Alerte): boolean {
    return a.type === 'avertissement' || a.type === 'Warning';
}

// TASK-139 : libellé d'onglet lisible (identique aux boutons d'onglet) pour renvoyer l'utilisateur
// vers le domaine où se trouve réellement la ligne incohérente.
function domaineOngletLabel(domaine: string): string {
    return domaine === 'Encaissement' ? 'TVA Collectée (Ventes)' : 'TVA Déductible (Achats)';
}

export interface VerifierIntegrerPanelProps {
    declarationId: string;
    selectedRows: ReglementRow[];
    /** Relecture après intégration (TASK-075) : ignore selectedRows, charge tout par declarationId */
    readOnly?: boolean;
    integree: boolean;
    showToast: (m: string, t?: 'success' | 'error' | 'warning') => void;
    /** Appelé après une intégration réussie pour recharger l'état de la déclaration */
    onIntegrationSuccess: () => void;
}

export function VerifierIntegrerPanel({
    declarationId,
    selectedRows,
    readOnly = false,
    integree,
    showToast,
    onIntegrationSuccess,
}: VerifierIntegrerPanelProps) {
    const [dataByReglement, setDataByReglement] = useState<Record<string, LigneValorisation[]>>({});
    const [loadingLignes, setLoadingLignes] = useState(true);

    // Drill anomalie → grille filtrée (TASK-016), aussi réutilisé pour le drill par source (TASK-107)
    const [drillFiltre, setDrillFiltre] = useState<{
        domaine: DomaineTVA;
        filtre: Record<string, any>;
        label: string;
        kind?: 'anomalie' | 'incoherence' | 'codeActivite';
    } | null>(null);

    // TASK-161 : référentiel des codes activité (P_DECTVAACTIVITE), chargé une fois pour
    // alimenter le select éditable du drill « Codes activité ».
    const [codeActiviteOptions, setCodeActiviteOptions] = useState<{ value: string, label: string }[]>([]);
    useEffect(() => {
        let cancelled = false;
        (async () => {
            try {
                const res = await api.get('/codes-activite');
                if (cancelled) return;
                const options = (res.data || []).map((r: any) => ({ value: r.code, label: `${r.code} — ${r.libelle}` }));
                setCodeActiviteOptions(options);
            } catch (e) {
                console.error('Erreur lors du chargement du référentiel des codes activité', e);
            }
        })();
        return () => { cancelled = true; };
    }, []);

    // TASK-144 : panneau de diagnostic explicatif d'une ligne en anomalie (à la demande).
    const [diagnostic, setDiagnostic] = useState<{ ecId: number; factureNumero: string } | null>(null);
    // TASK-147 : incrémenté après un recalcul réussi depuis le diagnostic — force le rechargement
    // des lignes et du checkup sans dupliquer la logique de fetch existante.
    const [reloadToken, setReloadToken] = useState(0);

    const [checkup, setCheckup] = useState<CheckupResult | null>(null);
    const [loadingCheckup, setLoadingCheckup] = useState(true);
    const [submitting, setSubmitting] = useState(false);
    const [exportingControle, setExportingControle] = useState(false);
    const [confirmed, setConfirmed] = useState(false);

    // Chargement des lignes de valorisation
    useEffect(() => {
        let cancelled = false;
        (async () => {
            if (!readOnly && selectedRows.length === 0) {
                setLoadingLignes(false);
                return;
            }
            setLoadingLignes(true);
            try {
                console.log(`VerifierIntegrerPanel: starting fetch (readOnly=${readOnly}, ${selectedRows.length} selected rows)`);
                const allLignes = await fetchAllLignes(declarationId);
                if (cancelled) return;

                console.log(`VerifierIntegrerPanel: fetched ${allLignes.length} total lines from API`);


                // Relecture (TASK-075)
                const mappedData = readOnly
                    ? { '__declaration__': allLignes }
                    : Object.fromEntries(selectedRows.map(r => {
                          const matchingLignes = allLignes.filter(l => l.numeroRapprochement === r.numeroReglement);
                          return [r.numeroReglement, matchingLignes] as const;
                      }));

                setDataByReglement(mappedData);
            } catch (e) {
                console.error("VerifierIntegrerPanel error loading lines:", e);
                showToast('Erreur lors du chargement de la valorisation', 'error');
            } finally {
                if (!cancelled) setLoadingLignes(false);
            }
        })();
        return () => { cancelled = true; };
    }, [declarationId, selectedRows, readOnly, showToast, reloadToken]);

    useEffect(() => {
        if (loadingLignes) return;
        let cancelled = false;
        (async () => {
            setLoadingCheckup(true);
            try {
                const res = await api.get(`/declarations/${declarationId}/checkup`);
                if (!cancelled) {
                    setCheckup(res.data as CheckupResult);
                }
            } catch (e) {
                console.error(e);
                if (!cancelled) showToast('Erreur lors du chargement du checkup', 'error');
            } finally {
                if (!cancelled) setLoadingCheckup(false);
            }
        })();
        return () => { cancelled = true; };
    }, [declarationId, showToast, loadingLignes, reloadToken]);

    // Toutes les lignes chargées
    const allLignes = useMemo(() =>
        Object.values(dataByReglement).flat(),
        [dataByReglement]
    );

    const [selectedTab, setSelectedTab] = useState<'Decaissement' | 'Encaissement'>('Decaissement');

    // Lignes agrégées (facture × taux)
    const rows = useMemo(() => agregParFactureTaux(allLignes), [allLignes]);

    const filteredRows = useMemo(() => rows.filter(r => r.domaine === selectedTab), [rows, selectedTab]);

    // Sous-totaux par taux (valorisées uniquement)
    const sousTotaux = useMemo(() => sousTotauxParTaux(filteredRows), [filteredRows]);

    // Totaux locaux issus des lignes
    const localTotalTVA = useMemo(() => sousTotaux.reduce((s, st) => s + st.totalTVA, 0), [sousTotaux]);
    const localTotalHT = useMemo(() => sousTotaux.reduce((s, st) => s + st.totalHT, 0), [sousTotaux]);

    // Lignes non valorisées (compteur)
    const nbNonValorise = useMemo(() => filteredRows.filter(r => r.nonValorise).length, [filteredRows]);

    // Helper checking source to domain
    const sourceBelongsToDomain = (source: string, domain: 'Decaissement' | 'Encaissement'): boolean => {
        if (domain === 'Encaissement') {
            return source === 'Encaissement' || source === 'VENTE';
        } else {
            return source === 'Decaissement' || source === 'Espece' || source === 'Depense' || source === 'ACHAT' || source === 'CAISSE' || source === 'Dépense';
        }
    };

    // Robustesse F5 (TASK-086) : Chiffres dérivés du checkup si disponible, sinon locaux
    const displayNbReglements = useMemo(() => {
        if (readOnly) {
            const regs = allLignes.filter(l => l.domaine === selectedTab).map(l => l.numeroRapprochement || l.id);
            return new Set(regs).size;
        }
        return selectedRows.filter(r => (r.domaine === 'Encaissement' ? 'Encaissement' : 'Decaissement') === selectedTab).length;
    }, [selectedRows, selectedTab, readOnly, allLignes]);

    const displayNbLignes = useMemo(() => {
        return filteredRows.filter(r => !r.nonValorise).length;
    }, [filteredRows]);

    const displayTotalTVA = useMemo(() => {
        if (checkup && checkup.recapSource && checkup.recapSource.length > 0) {
            return checkup.recapSource
                .filter(r => sourceBelongsToDomain(r.source, selectedTab))
                .reduce((s, r) => s + r.tva, 0);
        }
        return localTotalTVA;
    }, [checkup, localTotalTVA, selectedTab]);

    const localReconciliation = useMemo(() => {
        const recon = {
            candidates: filteredRows.length,
            integrees: 0,
            exclues: 0,
            reportees: 0,
            proposees: 0,
            ecartees: 0
        };
        filteredRows.forEach(r => {
            if (r.statutLigne === 0) recon.proposees++;
            else if (r.statutLigne === 1) recon.integrees++;
            else if (r.statutLigne === 2) recon.exclues++;
            else if (r.statutLigne === 3) recon.reportees++;
            else if (r.statutLigne === 4) recon.ecartees++;
        });
        return recon;
    }, [filteredRows]);

    const isReadOnly = integree || confirmed;

    // TASK-112 : le groupe « lignes incohérentes » (TTC ≠ HT+TVA) du domaine affiché — c'est
    // l'axe qui compose réellement l'écart (Source, lui, était une tautologie : TASK-112 §
    // Contexte). Absent (undefined) si aucune ligne incohérente sur ce domaine.
    const ligneIncoherente = useMemo(() => {
        return (checkup?.recapIncoherence ?? []).find(r => r.domaine === selectedTab && r.incoherente);
    }, [checkup, selectedTab]);

    // TASK-139 : le badge « Écart détecté » est global (toutes lignes), mais le détail ci-dessus est
    // filtré par onglet. Si la ligne incohérente responsable de l'écart est dans l'AUTRE onglet, on
    // ne trouve rien ici → message trompeur « aucune ligne incohérente ». On repère donc la ligne
    // incohérente hors onglet actif pour renvoyer explicitement l'utilisateur vers le bon domaine
    // (« aucune ligne silencieuse », TASK-112).
    const ligneIncoherenteAutreOnglet = useMemo(() => {
        return (checkup?.recapIncoherence ?? []).find(r => r.domaine !== selectedTab && r.incoherente);
    }, [checkup, selectedTab]);

    // TASK-112 : drill « Lignes incohérentes » → factures/règlements dont TTC ≠ HT+TVA, seul
    // sous-ensemble qui compose l'écart annoncé (remplace le drill Source de TASK-107, invalidé
    // par la tautologie constatée : filtrer le domaine Décaissement par source=Décaissement ne
    // retire aucune ligne).
    const handleDrillIncoherence = () => {
        setDrillFiltre({
            domaine: selectedTab as DomaineTVA,
            filtre: { incoherente: ['true'] },
            label: 'Lignes incohérentes (TTC ≠ HT+TVA)',
            kind: 'incoherence',
        });
    };

    // Filtre les alertes par domaine/tab
    const bloquants = useMemo(() => {
        const list = checkup?.alertes.filter(isBloquant) ?? [];
        return list.filter(a => {
            const dom = a.domaine ? (a.domaine === 'Encaissement' ? 'Encaissement' : 'Decaissement') : 'Decaissement';
            return dom === selectedTab;
        });
    }, [checkup, selectedTab]);

    const avertissements = useMemo(() => {
        const list = checkup?.alertes.filter(isAvertissement) ?? [];
        return list.filter(a => {
            const dom = a.domaine ? (a.domaine === 'Encaissement' ? 'Encaissement' : 'Decaissement') : 'Decaissement';
            return dom === selectedTab;
        });
    }, [checkup, selectedTab]);

    const hasAnyBloquant = useMemo(() => {
        return (checkup?.alertes.filter(isBloquant) ?? []).length > 0;
    }, [checkup]);

    const hasBloquant = bloquants.length > 0;

    const canConfirm = !integree && !confirmed && !hasAnyBloquant && !loadingCheckup && !submitting;

    const controls = buildControls(checkup, displayNbLignes, displayNbReglements, integree, bloquants.length, localReconciliation);
    console.log("CONTROLS:", JSON.stringify(controls.map(c => ({ id: c.id, label: c.label, status: c.status }))));

    const handleConfirm = async () => {
        if (!canConfirm) return;
        setSubmitting(true);
        try {
            await api.post(`/declarations/${declarationId}/cloture`);
            setConfirmed(true);
            showToast('Déclaration intégrée avec succès — tampon DT_Id posé', 'success');
            onIntegrationSuccess();
        } catch (err: any) {
            const msg = err?.response?.data?.message
                || err?.response?.data?.Message
                || 'Erreur lors de l\'intégration';
            showToast(msg, 'error');
        } finally {
            setSubmitting(false);
        }
    };

    // TASK-160 : export Excel de contrôle (règlements sélectionnés + factures à déclarer + détail
    // TVA), disponible dès qu'une déclaration existe — même endpoint/pattern que l'export de dépôt
    // (DeclarationFinalePanel.tsx), téléchargement direct via blob, aucun fichier persisté.
    const exporterControle = async () => {
        setExportingControle(true);
        try {
            const res = await api.get(`/declarations/${declarationId}/export-controle`, { responseType: 'blob' });
            const blobUrl = URL.createObjectURL(res.data);
            const a = document.createElement('a');
            a.href = blobUrl;
            a.download = 'Export_controle.xlsx';
            document.body.appendChild(a);
            a.click();
            a.remove();
            URL.revokeObjectURL(blobUrl);
            showToast('Export de contrôle généré', 'success');
        } catch (err: any) {
            console.error(err);
            showToast(err?.response?.data?.message || err?.response?.data?.Message || 'Erreur lors de l\'export de contrôle', 'error');
        } finally {
            setExportingControle(false);
        }
    };

    // Garde « rien sélectionné » (parcours normal, TASK-054)
    if (!readOnly && selectedRows.length === 0) {
        return (
            <div style={{ flex: 1, display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', textAlign: 'center', padding: '2rem', color: 'var(--text-secondary)', height: '100%' }}>
                <Calculator size={48} style={{ opacity: 0.25, marginBottom: '1rem' }} />
                <p style={{ margin: 0, fontSize: '0.9rem' }}>Aucun règlement sélectionné — retournez à l'étape ① Règlements.</p>
            </div>
        );
    }

    // Garde « rien chargé » (TASK-075, relecture)
    if (readOnly && !loadingLignes && allLignes.length === 0) {
        return (
            <div style={{ flex: 1, display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', textAlign: 'center', padding: '2rem', color: 'var(--text-secondary)', height: '100%' }}>
                <Calculator size={48} style={{ opacity: 0.25, marginBottom: '1rem' }} />
                <p style={{ margin: 0, fontSize: '0.9rem' }}>Aucune ligne de valorisation trouvée pour cette déclaration.</p>
            </div>
        );
    }

    const initialLoading = (loadingLignes && allLignes.length === 0) || (loadingCheckup && !checkup);
    if (initialLoading) {
        return (
            <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100%' }}>
                <Loader2 size={28} className="animate-spin" style={{ color: 'var(--accent-primary)' }} />
            </div>
        );
    }

    // Si on est en vue drill, on affiche la DomainGrid filtrée (TASK-088)
    if (drillFiltre) {
        return (
            <div style={{ display: 'flex', flexDirection: 'column', height: '100%', overflow: 'hidden' }}>
                {/* Bandeau drill */}
                <div style={{
                    padding: '0.5rem 1rem',
                    background: 'var(--status-warning-bg)',
                    borderBottom: '1px solid var(--status-warning-border)',
                    display: 'flex',
                    alignItems: 'center',
                    gap: '0.75rem',
                    flexShrink: 0,
                    fontSize: '0.8125rem',
                }}>
                    <button
                        onClick={() => setDrillFiltre(null)}
                        style={{
                            display: 'flex', alignItems: 'center', gap: '0.4rem',
                            background: 'white', border: '1px solid var(--status-warning-border)',
                            borderRadius: 'var(--radius-md)', padding: '0.25rem 0.6rem',
                            cursor: 'pointer', fontSize: '0.8125rem', fontWeight: 500,
                            color: 'var(--text-primary)',
                        }}
                    >
                        ← Retour au contrôle
                    </button>
                    <span style={{ color: 'var(--text-secondary)' }}>
                        {drillFiltre.kind === 'incoherence' ? 'Drill écart :' : drillFiltre.kind === 'codeActivite' ? 'Codes activité :' : 'Drill anomalie :'}
                    </span>
                    <span style={{ fontWeight: 600 }}>{drillFiltre.label}</span>
                </div>
                {/* Grille filtrée — lecture seule sauf pour le drill « Codes activité » avant
                    clôture/confirmation (surcharge manuelle par ligne, TASK-161). */}
                <div style={{ flex: 1, overflow: 'hidden' }}>
                    <DomainGrid
                        declarationId={declarationId}
                        domaine={drillFiltre.domaine}
                        onActionDone={() => {}}
                        showToast={showToast}
                        initialFilters={drillFiltre.filtre}
                        readonly={drillFiltre.kind === 'codeActivite' ? isReadOnly : true}
                        {...(drillFiltre.kind === 'incoherence' ? {
                            columns: incoherenceColumns,
                            colsStorageKey: 'grf.cols.domain.incoherence',
                        } : {})}
                        {...(drillFiltre.kind === 'codeActivite' ? {
                            columns: codeActiviteColumns,
                            colsStorageKey: 'grf.cols.domain.codeActivite',
                            codeActiviteOptions,
                        } : {})}
                    />
                </div>
            </div>
        );
    }

    return (
        <div style={{ flex: 1, display: 'flex', flexDirection: 'column', height: '100%', overflow: 'hidden', background: 'var(--bg-secondary)' }}>
            {/* TASK-144 : panneau de diagnostic explicatif à la demande */}
            {diagnostic && (
                <DiagnosticModal
                    declarationId={declarationId}
                    ecId={diagnostic.ecId}
                    factureNumero={diagnostic.factureNumero}
                    onClose={() => setDiagnostic(null)}
                    onRecalculated={() => { setReloadToken(t => t + 1); setDiagnostic(null); }}
                />
            )}
            {/* En-tête */}
            <div style={{
                padding: '0.65rem 1rem',
                borderBottom: '1px solid var(--border-color)',
                background: 'white',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                flexWrap: 'wrap',
                gap: '0.5rem',
                flexShrink: 0,
            }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '0.6rem' }}>
                    <Lock size={20} style={{ color: isReadOnly ? 'var(--status-ok-text)' : 'var(--accent-primary)' }} />
                    <div>
                        <h2 style={{ margin: 0, fontSize: '1.05rem', fontWeight: 600 }}>③ Vérifier & Intégrer</h2>
                        <div style={{ fontSize: '0.72rem', color: 'var(--text-secondary)' }}>
                            {isReadOnly
                                ? 'Déclaration intégrée — tampon DT_Id posé · lignes exclues des prochaines recherches'
                                : 'Vérifiez les calculs par taux TVA, contrôlez les anomalies puis confirmez l\'intégration'}
                        </div>
                    </div>
                </div>
                <div style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
                    {isReadOnly && (
                        <span style={{
                            display: 'inline-flex', alignItems: 'center', gap: '0.3rem',
                            fontSize: '0.75rem', fontWeight: 700,
                            padding: '0.25rem 0.65rem', borderRadius: 'var(--radius-full)',
                            background: 'var(--status-ok-bg)', color: 'var(--status-ok-text)',
                            border: '1px solid #bbf7d0',
                        }}>
                            <CheckCircle2 size={13} /> Intégrée
                        </span>
                    )}
                    {nbNonValorise > 0 && (
                        <span style={{ display: 'flex', alignItems: 'center', gap: '0.35rem', fontSize: '0.78rem', color: 'var(--status-warning-text-alt)', fontWeight: 600 }}>
                            <AlertTriangle size={14} />
                            {nbNonValorise} ligne{nbNonValorise > 1 ? 's' : ''} non valorisée{nbNonValorise > 1 ? 's' : ''}
                        </span>
                    )}
                </div>
            </div>

            {/* Tabs for separating domains (TVA Déductible vs TVA Collectée) */}
            <div style={{
                display: 'flex',
                gap: '0.5rem',
                padding: '0.5rem 1rem 0 1rem',
                borderBottom: '1px solid var(--border-color)',
                background: 'white',
                flexShrink: 0
            }}>
                <button
                    onClick={() => setSelectedTab('Decaissement')}
                    style={{
                        padding: '0.6rem 1.2rem',
                        fontSize: '0.875rem',
                        fontWeight: 600,
                        border: 'none',
                        borderBottom: selectedTab === 'Decaissement' ? '3px solid var(--accent-primary)' : '3px solid transparent',
                        background: 'none',
                        color: selectedTab === 'Decaissement' ? 'var(--accent-primary)' : 'var(--text-secondary)',
                        cursor: 'pointer',
                        transition: 'all 0.2s',
                    }}
                >
                    TVA Déductible (Achats)
                </button>
                <button
                    onClick={() => setSelectedTab('Encaissement')}
                    style={{
                        padding: '0.6rem 1.2rem',
                        fontSize: '0.875rem',
                        fontWeight: 600,
                        border: 'none',
                        borderBottom: selectedTab === 'Encaissement' ? '3px solid var(--accent-primary)' : '3px solid transparent',
                        background: 'none',
                        color: selectedTab === 'Encaissement' ? 'var(--accent-primary)' : 'var(--text-secondary)',
                        cursor: 'pointer',
                        transition: 'all 0.2s',
                    }}
                >
                    TVA Collectée (Ventes)
                </button>
            </div>

            {/* Corps scrollable */}
            <div style={{ flex: 1, overflow: 'auto', padding: '0.75rem', display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
                {/* 1. Sous-totaux par taux TVA */}
                {sousTotaux.length > 0 && (
                    <div style={{ flexShrink: 0, background: 'white', border: '1px solid var(--border-color)', borderRadius: '8px', overflow: 'hidden' }}>
                        <div style={{ padding: '0.5rem 0.9rem', background: 'var(--bg-secondary)', borderBottom: '1px solid var(--border-color)', fontSize: '0.78rem', fontWeight: 600, color: 'var(--text-secondary)', display: 'flex', alignItems: 'center', gap: '0.4rem' }}>
                            <Info size={13} />
                            Sous-totaux par taux TVA
                        </div>
                        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.8125rem' }}>
                            <thead>
                                <tr style={{ background: 'var(--bg-secondary)' }}>
                                    <th style={thStyle('left')}>Taux</th>
                                    <th style={thStyle('right')}>Nb lignes</th>
                                    <th style={thStyle('right')}>Total HT</th>
                                    <th style={thStyle('right')}>Total TVA</th>
                                    <th style={thStyle('right')}>Total TTC</th>
                                </tr>
                            </thead>
                            <tbody>
                                {sousTotaux.map(st => (
                                    <tr key={st.taux} style={{ borderTop: '1px solid var(--border-color)' }}>
                                        <td style={tdStyle('left')}>
                                            <TauxBadge taux={st.taux} />
                                        </td>
                                        <td style={{ ...tdStyle('right'), color: 'var(--text-secondary)' }}>{st.nbLignes}</td>
                                        <td style={{ ...tdStyle('right'), fontVariantNumeric: 'tabular-nums' }}>{formatMoney(st.totalHT)}</td>
                                        <td style={{ ...tdStyle('right'), fontWeight: 700, fontVariantNumeric: 'tabular-nums' }}>{formatMoney(st.totalTVA)}</td>
                                        <td style={{ ...tdStyle('right'), fontWeight: 700, fontVariantNumeric: 'tabular-nums' }}>{formatMoney(st.totalHT + st.totalTVA)}</td>
                                    </tr>
                                ))}
                                {/* Ligne totaux */}
                                <tr style={{ borderTop: '2px solid var(--border-color)', background: 'var(--bg-secondary)' }}>
                                    <td style={{ ...tdStyle('left'), fontWeight: 700 }} colSpan={2}>Σ Total</td>
                                    <td style={{ ...tdStyle('right'), fontWeight: 700, fontVariantNumeric: 'tabular-nums' }}>{formatMoney(localTotalHT)}</td>
                                    <td style={{ ...tdStyle('right'), fontWeight: 700, fontVariantNumeric: 'tabular-nums' }}>{formatMoney(localTotalTVA)}</td>
                                    <td style={{ ...tdStyle('right'), fontWeight: 700, fontVariantNumeric: 'tabular-nums' }}>{formatMoney(localTotalHT + localTotalTVA)}</td>
                                </tr>
                            </tbody>
                        </table>
                    </div>
                )}

                {/* 2. Lignes non valorisées — détail motif */}
                {nbNonValorise > 0 && (
                    <div style={{ flexShrink: 0, background: 'white', border: '1px solid var(--status-warning-border)', borderRadius: '8px', overflow: 'hidden' }}>
                        <div style={{ padding: '0.5rem 0.9rem', background: 'var(--status-warning-bg)', borderBottom: '1px solid var(--status-warning-border)', fontSize: '0.78rem', fontWeight: 600, color: 'var(--status-warning-text)', display: 'flex', alignItems: 'center', gap: '0.4rem' }}>
                            <AlertTriangle size={13} />
                            {nbNonValorise} ligne{nbNonValorise > 1 ? 's' : ''} non valorisée{nbNonValorise > 1 ? 's' : ''} — transparence de traçabilité
                        </div>
                        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.8rem' }}>
                            <thead>
                                <tr style={{ background: 'var(--status-warning-bg)' }}>
                                    <th style={thStyle('left')}>Facture</th>
                                    <th style={thStyle('left')}>Tiers</th>
                                    <th style={thStyle('left')}>Statut ligne</th>
                                    <th style={thStyle('left')}>Motif</th>
                                    <th style={thStyle('left')}></th>
                                </tr>
                            </thead>
                            <tbody>
                                {filteredRows.filter(r => r.nonValorise).map((r, i) => (
                                    <tr key={`nv-${i}`} style={{ borderTop: '1px solid var(--status-warning-border)' }}>
                                        <td style={{ ...tdStyle('left'), fontFamily: 'monospace', fontSize: '0.77rem', fontWeight: 500 }}>{r.factureNumero}</td>
                                        <td style={{ ...tdStyle('left'), fontSize: '0.77rem' }}>{r.tiers}</td>
                                        <td style={{ ...tdStyle('left') }}>
                                            <span style={{ display: 'inline-block', padding: '1px 7px', borderRadius: '99px', fontSize: '0.72rem', fontWeight: 600, background: 'var(--status-blocking-bg)', color: 'var(--status-blocking-text)' }}>
                                                {STATUT_LIGNE_LABELS[r.statutLigne] ?? r.statutLigne}
                                            </span>
                                        </td>
                                        <td style={{ ...tdStyle('left'), color: 'var(--status-warning-text)', fontSize: '0.77rem' }}>
                                            {r.motif || '—'}
                                        </td>
                                        <td style={{ ...tdStyle('left') }}>
                                            {r.ecId > 0 && (
                                                <button
                                                    onClick={() => setDiagnostic({ ecId: r.ecId, factureNumero: r.factureNumero })}
                                                    title="Comprendre pourquoi cette ligne est en anomalie"
                                                    style={{ display: 'inline-flex', alignItems: 'center', gap: '0.3rem', padding: '2px 9px', borderRadius: '6px', fontSize: '0.72rem', fontWeight: 600, cursor: 'pointer', background: 'var(--bg-secondary)', border: '1px solid var(--border-color)', color: 'var(--text-primary)', whiteSpace: 'nowrap' }}
                                                >
                                                    <Search size={12} /> Diagnostiquer
                                                </button>
                                            )}
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                )}

                {/* 3. Récapitulatif de l'intégration */}
                <RecapCard
                    nbLignes={displayNbLignes}
                    totalTVA={displayTotalTVA}
                    nbReglements={displayNbReglements}
                    reconciliation={localReconciliation}
                    isReadOnly={isReadOnly}
                />

                {/* 4. Check-list de contrôles */}
                <ChecklistCard
                    controls={controls}
                    loading={loadingCheckup}
                    bloquants={bloquants}
                    avertissements={avertissements}
                    ligneIncoherente={ligneIncoherente}
                    ligneIncoherenteAutreOnglet={ligneIncoherenteAutreOnglet}
                    ecartExplique={checkup?.equilibre?.ecartExplique}
                    reconciliation={localReconciliation}
                    onDrill={(domaine, filtre, label) => setDrillFiltre({ domaine, filtre, label, kind: 'anomalie' })}
                    onDrillIncoherence={handleDrillIncoherence}
                />

                {/* État post-intégration */}
                {isReadOnly && (
                    <div style={{
                        flexShrink: 0,
                        background: '#f0fdf4',
                        border: '1px solid #bbf7d0',
                        borderRadius: '8px',
                        padding: '0.75rem 1rem',
                        display: 'flex',
                        alignItems: 'center',
                        gap: '0.6rem',
                        fontSize: '0.82rem',
                        color: 'var(--status-ok-text)',
                        fontWeight: 500,
                    }}>
                        <ShieldCheck size={16} />
                        Déclaration intégrée — lignes rattachées et exclues des prochaines sélections. Lecture seule.
                    </div>
                )}
            </div>

            {/* Pied — CTA intégration ou statut lecture seule */}
            <div style={{
                flexShrink: 0,
                borderTop: '2px solid var(--border-color)',
                background: 'white',
                padding: '0.75rem 1rem',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                gap: '1rem',
            }}>
                <div style={{ fontSize: '0.8rem', color: 'var(--text-secondary)' }}>
                    {isReadOnly ? (
                        <span style={{ color: 'var(--status-ok-text)', fontWeight: 600 }}>
                            ✓ Intégration confirmée
                        </span>
                    ) : hasBloquant ? (
                        <span style={{ display: 'inline-flex', alignItems: 'center', gap: '0.35rem', color: 'var(--status-blocking-text)', fontWeight: 600 }}>
                            <XCircle size={14} />
                            {bloquants.length} contrôle{bloquants.length > 1 ? 's' : ''} bloquant{bloquants.length > 1 ? 's' : ''} — intégration impossible
                        </span>
                    ) : !loadingCheckup ? (
                        <span style={{ display: 'inline-flex', alignItems: 'center', gap: '0.35rem', color: 'var(--status-ok-text)' }}>
                            <CheckCircle2 size={14} />
                            Tous les contrôles sont passés
                        </span>
                    ) : null}
                </div>

                <div style={{ display: 'inline-flex', alignItems: 'center', gap: '0.6rem' }}>
                    <button
                        onClick={exporterControle}
                        disabled={exportingControle}
                        title="Exporter en Excel les règlements sélectionnés, les factures à déclarer et le détail TVA"
                        style={{
                            display: 'inline-flex', alignItems: 'center', gap: '0.4rem',
                            background: 'white', border: '1px solid var(--border-color)',
                            borderRadius: 'var(--radius-md)', padding: '0.45rem 0.85rem',
                            cursor: exportingControle ? 'not-allowed' : 'pointer', opacity: exportingControle ? 0.6 : 1,
                            fontSize: '0.8125rem', fontWeight: 600, color: 'var(--text-primary)',
                        }}
                    >
                        {exportingControle ? <Loader2 size={14} className="animate-spin" /> : <FileSpreadsheet size={14} />} Export de contrôle (Excel)
                    </button>

                    <button
                        onClick={() => setDrillFiltre({ domaine: selectedTab as DomaineTVA, filtre: {}, label: selectedTab === 'Encaissement' ? 'TVA Collectée (Ventes)' : 'TVA Déductible (Achats)', kind: 'codeActivite' })}
                        title={isReadOnly
                            ? 'Consulter le code activité de chaque ligne (lecture seule — déclaration intégrée/clôturée)'
                            : 'Consulter/modifier le code activité de chaque ligne (surcharge manuelle, écran ② Vérifier & Intégrer)'}
                        style={{
                            display: 'inline-flex', alignItems: 'center', gap: '0.4rem',
                            background: 'white', border: '1px solid var(--border-color)',
                            borderRadius: 'var(--radius-md)', padding: '0.45rem 0.85rem',
                            cursor: 'pointer', fontSize: '0.8125rem', fontWeight: 600, color: 'var(--text-primary)',
                        }}
                    >
                        Codes activité
                    </button>

                    {!isReadOnly && (
                        <button
                            id="btn-confirmer-integration"
                            onClick={handleConfirm}
                            disabled={!canConfirm}
                            style={{
                                display: 'inline-flex', alignItems: 'center', gap: '0.5rem',
                                padding: '0.55rem 1.2rem',
                                borderRadius: '5px',
                                border: 'none',
                                fontWeight: 700,
                                fontSize: '0.875rem',
                                cursor: canConfirm ? 'pointer' : 'not-allowed',
                                background: canConfirm
                                    ? 'var(--accent-primary)'
                                    : 'var(--bg-tertiary)',
                                color: canConfirm ? 'white' : 'var(--text-secondary)',
                                transition: 'background 0.15s, opacity 0.15s',
                                opacity: canConfirm ? 1 : 0.65,
                            }}
                            title={hasBloquant ? 'Des contrôles bloquants empêchent l\'intégration' : 'Confirmer et figer la déclaration'}
                        >
                            {submitting ? (
                                <><Loader2 size={14} className="animate-spin" /> Intégration en cours…</>
                            ) : (
                                <><Lock size={14} /> Confirmer intégration</>
                            )}
                        </button>
                    )}
                </div>
            </div>
        </div>
    );
}

// ─── Helpers de style et composants internes ──────────────────────────────────

function thStyle(align: 'left' | 'right' | 'center' = 'left'): React.CSSProperties {
  return {
    padding: '0.45rem 0.75rem',
    textAlign: align,
    borderBottom: '2px solid var(--border-color)',
    fontWeight: 600,
    fontSize: '0.75rem',
    color: 'var(--text-secondary)',
    whiteSpace: 'nowrap',
    userSelect: 'none',
  };
}

function tdStyle(align: 'left' | 'right' | 'center' = 'left'): React.CSSProperties {
  return {
    padding: '0.42rem 0.75rem',
    textAlign: align,
    whiteSpace: 'nowrap',
    verticalAlign: 'middle',
  };
}

function TauxBadge({ taux }: { taux: number }) {
  const colors: Record<number, { bg: string; color: string }> = {
    20: { bg: '#dbeafe', color: '#1d4ed8' },
    14: { bg: '#ede9fe', color: '#6d28d9' },
    10: { bg: '#dcfce7', color: '#15803d' },
    7:  { bg: '#fef9c3', color: '#854d0e' },
    0:  { bg: '#f3f4f6', color: '#6b7280' },
  };
  const c = colors[taux] ?? { bg: '#f3f4f6', color: '#374151' };
  return (
    <span style={{
      display: 'inline-block',
      padding: '1px 8px',
      borderRadius: '99px',
      fontSize: '0.75rem',
      fontWeight: 700,
      background: c.bg,
      color: c.color,
    }}>
      {taux}%
    </span>
  );
}

function RecapCard({
    nbLignes, totalTVA, nbReglements, reconciliation, isReadOnly,
}: {
    nbLignes: number;
    totalTVA: number;
    nbReglements: number;
    reconciliation: Reconciliation | null;
    isReadOnly: boolean;
}) {
    return (
        <div style={{
            flexShrink: 0,
            background: 'white',
            border: '1px solid var(--border-color)',
            borderRadius: '8px',
            overflow: 'hidden',
        }}>
            <div style={{
                padding: '0.45rem 0.9rem',
                background: 'var(--bg-secondary)',
                borderBottom: '1px solid var(--border-color)',
                fontSize: '0.75rem', fontWeight: 600, color: 'var(--text-secondary)',
                display: 'flex', alignItems: 'center', gap: '0.4rem',
            }}>
                <FileStack size={13} /> Récapitulatif de l'intégration
            </div>
            <div style={{
                display: 'grid',
                gridTemplateColumns: 'repeat(auto-fit, minmax(160px, 1fr))',
                gap: 0,
            }}>
                <RecapCell
                    label="Règlements sélectionnés"
                    value={nbReglements}
                    unit="règlement(s)"
                />
                <RecapCell
                    label="Lignes valorisées"
                    value={nbLignes}
                    unit="ligne(s)"
                    bordered
                />
                <RecapCell
                    label="Total TVA à intégrer"
                    value={formatMoney(totalTVA)}
                    accent
                    bordered
                />
                {reconciliation && (
                    <RecapCell
                        label="Lignes intégrées (back)"
                        value={reconciliation.integrees}
                        unit="ligne(s)"
                        accent={isReadOnly}
                        bordered
                    />
                )}
            </div>
        </div>
    );
}

function RecapCell({
    label, value, unit, accent, bordered,
}: {
    label: string;
    value: number | string;
    unit?: string;
    accent?: boolean;
    bordered?: boolean;
}) {
    return (
        <div style={{
            padding: '0.6rem 1rem',
            borderLeft: bordered ? '1px solid var(--border-color)' : undefined,
        }}>
            <div style={{ fontSize: '0.72rem', color: 'var(--text-secondary)', marginBottom: '0.15rem' }}>{label}</div>
            <div style={{
                fontSize: typeof value === 'string' ? '1.1rem' : '1.3rem',
                fontWeight: 800,
                fontVariantNumeric: 'tabular-nums',
                color: accent ? 'var(--accent-primary)' : 'var(--text-primary)',
                letterSpacing: '-0.01em',
                lineHeight: 1.1,
            }}>
                {value}
                {unit && <span style={{ fontSize: '0.72rem', fontWeight: 500, color: 'var(--text-secondary)', marginLeft: '0.3rem' }}>{unit}</span>}
            </div>
        </div>
    );
}

interface Control {
    id: string;
    label: string;
    description: string;
    status: 'ok' | 'error' | 'warning' | 'pending';
    badgeLabel?: string;
}

function buildControls(
    checkup: CheckupResult | null,
    nbLignes: number,
    nbReglements: number,
    integree: boolean,
    bloquantsCount: number,
    reconciliation: { integrees: number; proposees: number }
): Control[] {
    if (!checkup) {
        return [
            { id: 'reglements', label: 'Règlements sélectionnés', description: 'Vérification de la sélection', status: 'pending' },
            { id: 'lignes', label: 'Lignes valorisées', description: 'Valorisation par le back', status: 'pending' },
            { id: 'equilibre', label: 'Cohérence des totaux déclarés', description: 'Contrôle d\'équilibre des montants', status: 'pending' },
            { id: 'bloquants', label: 'Absence d\'anomalies bloquantes', description: 'Aucune alerte bloquante', status: 'pending' },
        ];
    }

    const equilibre = checkup.equilibre;
    const hasAffectations = reconciliation.integrees > 0 || reconciliation.proposees > 0;

    return [
        {
            id: 'reglements',
            label: 'Règlements sélectionnés',
            description: integree
                ? 'Règlements déjà intégrés en base'
                : `${nbReglements} règlement${nbReglements > 1 ? 's' : ''} en entrée`,
            status: (nbReglements > 0 || integree) ? 'ok' : 'error',
        },
        {
            id: 'lignes',
            label: 'Lignes valorisées présentes',
            description: nbLignes > 0
                ? `${nbLignes} ligne${nbLignes > 1 ? 's' : ''} valorisée${nbLignes > 1 ? 's' : ''} prêtes à intégrer`
                : (integree ? 'Lignes valorisées déjà intégrées' : 'Aucune ligne valorisée — retournez à l\'étape ①'),
            status: (nbLignes > 0 || integree) ? 'ok' : 'error',
        },
        {
            id: 'affectations',
            label: 'Affectations valides',
            description: hasAffectations
                ? `${reconciliation.integrees + reconciliation.proposees} affectation(s) en attente ou intégrée(s)`
                : 'Aucune affectation trouvée',
            status: hasAffectations ? 'ok' : 'warning',
        },
        {
            id: 'equilibre',
            label: 'Cohérence des totaux déclarés',
            description: equilibre
                ? (equilibre.isValid ? 'Équilibre validé — aucun écart' : `Écart détecté : ${formatMoney(equilibre.ecart ?? 0)}`)
                : 'Non vérifié',
            status: equilibre ? (equilibre.isValid ? 'ok' : 'error') : 'warning',
            badgeLabel: (equilibre && !equilibre.isValid) ? 'ÉCART' : undefined,
        },
        {
            id: 'bloquants',
            label: 'Absence d\'anomalies bloquantes',
            description: bloquantsCount === 0
                ? 'Aucune anomalie bloquante détectée'
                : `${bloquantsCount} anomalie${bloquantsCount > 1 ? 's' : ''} bloquante${bloquantsCount > 1 ? 's' : ''} — intégration impossible`,
            status: bloquantsCount === 0 ? 'ok' : 'error',
        },
    ];
}

function ChecklistCard({
    controls, loading, bloquants, avertissements, ligneIncoherente, ligneIncoherenteAutreOnglet, ecartExplique, reconciliation, onDrill, onDrillIncoherence,
}: {
    controls: Control[];
    loading: boolean;
    bloquants: Alerte[];
    avertissements: Alerte[];
    /** TASK-112 : groupe des lignes incohérentes (TTC ≠ HT+TVA) du domaine affiché */
    ligneIncoherente?: { ht: number; tva: number; ttc: number; residu: number; nbLignes: number };
    /** TASK-139 : ligne incohérente présente dans l'AUTRE onglet (renvoi explicite au bon domaine) */
    ligneIncoherenteAutreOnglet?: { domaine: string; nbLignes: number };
    /** TASK-112 : l'écart annoncé est-il intégralement expliqué par ligneIncoherente (calcul back) */
    ecartExplique?: boolean;
    reconciliation?: Reconciliation;
    onDrill: (domaine: DomaineTVA, filtre: Record<string, any>, label: string) => void;
    /** TASK-112 : drill vers les lignes qui composent l'écart (TTC ≠ HT+TVA) */
    onDrillIncoherence?: () => void;
}) {
    return (
        <div style={{
            flexShrink: 0,
            background: 'white',
            border: '1px solid var(--border-color)',
            borderRadius: '8px',
            overflow: 'hidden',
        }}>
            <div style={{
                padding: '0.45rem 0.9rem',
                background: 'var(--bg-secondary)',
                borderBottom: '1px solid var(--border-color)',
                fontSize: '0.75rem', fontWeight: 600, color: 'var(--text-secondary)',
                display: 'flex', alignItems: 'center', gap: '0.4rem',
            }}>
                <Calculator size={13} /> Contrôles pré-intégration
                {loading && <Loader2 size={12} className="animate-spin" style={{ marginLeft: 4 }} />}
            </div>

            <div>
                {controls.map((c, i) => (
                    <div
                        key={c.id}
                        style={{
                            display: 'flex',
                            alignItems: 'flex-start',
                            gap: '0.65rem',
                            padding: '0.55rem 1rem',
                            borderTop: i > 0 ? '1px solid var(--border-color)' : undefined,
                            background: c.status === 'error' ? 'rgba(239,68,68,0.03)' : 'white',
                        }}
                    >
                        <ControlIcon status={c.status} />
                        <div style={{ flex: 1, minWidth: 0 }}>
                            <div style={{
                                fontSize: '0.82rem',
                                fontWeight: 600,
                                color: c.status === 'error' ? 'var(--status-blocking-text)' : c.status === 'warning' ? 'var(--status-warning-text-alt)' : 'var(--text-primary)',
                            }}>
                                {c.label}
                            </div>
                            <div style={{ fontSize: '0.75rem', color: 'var(--text-secondary)', marginTop: '0.1rem' }}>
                                {c.description}
                            </div>
                            {c.id === 'equilibre' && c.status === 'error' && (
                                ligneIncoherente ? (
                                    <div style={{
                                        marginTop: '0.6rem',
                                        border: '1px solid var(--border-color)',
                                        borderRadius: 'var(--radius-md)',
                                        overflow: 'hidden',
                                        background: 'white',
                                    }}>
                                        <RecapSourceTable
                                            recapSource={[{ source: 'incoherente', ht: ligneIncoherente.ht, tva: ligneIncoherente.tva, ttc: ligneIncoherente.ttc }]}
                                            onRowClick={onDrillIncoherence ? () => onDrillIncoherence() : undefined}
                                            columnLabel="Écart"
                                        />
                                        {ecartExplique === false && (
                                            <div style={{ padding: '0.5rem 0.65rem', fontSize: '0.75rem', color: 'var(--status-warning-text-alt)', background: 'var(--status-warning-bg)', borderTop: '1px solid var(--status-warning-border)' }}>
                                                Écart non intégralement expliqué par {ligneIncoherente.nbLignes} ligne(s) incohérente(s)
                                                (résidu {formatMoney(ligneIncoherente.residu)}){reconciliation && (
                                                    ` — ${reconciliation.candidates} ligne(s) candidate(s), ${reconciliation.integrees + reconciliation.proposees} intégrée(s)/proposée(s), ${reconciliation.exclues} exclue(s), ${reconciliation.reportees} reportée(s), ${reconciliation.ecartees} écartée(s).`
                                                )}
                                            </div>
                                        )}
                                    </div>
                                ) : (ligneIncoherenteAutreOnglet && ecartExplique !== false) ? (
                                    // TASK-139 : l'écart global est expliqué, mais la (les) ligne(s)
                                    // incohérente(s) sont dans l'AUTRE onglet — on le dit explicitement
                                    // plutôt que de laisser croire qu'aucune cause n'est identifiée.
                                    <div style={{ marginTop: '0.6rem', padding: '0.5rem 0.65rem', fontSize: '0.75rem', color: 'var(--status-warning-text-alt)', background: 'var(--status-warning-bg)', border: '1px solid var(--status-warning-border)', borderRadius: 'var(--radius-md)' }}>
                                        Écart expliqué : la ou les {ligneIncoherenteAutreOnglet.nbLignes} ligne(s) incohérente(s)
                                        (TTC ≠ HT+TVA) se trouvent dans l'onglet « {domaineOngletLabel(ligneIncoherenteAutreOnglet.domaine)} ».
                                        Basculez sur cet onglet pour en voir le détail.
                                    </div>
                                ) : (
                                    <div style={{ marginTop: '0.6rem', padding: '0.5rem 0.65rem', fontSize: '0.75rem', color: 'var(--status-warning-text-alt)', background: 'var(--status-warning-bg)', border: '1px solid var(--status-warning-border)', borderRadius: 'var(--radius-md)' }}>
                                        Écart détecté mais aucune ligne incohérente identifiée dans les lignes déclarées
                                        {reconciliation && (
                                            ` — ${reconciliation.candidates} ligne(s) candidate(s), ${reconciliation.integrees + reconciliation.proposees} intégrée(s)/proposée(s), ${reconciliation.exclues} exclue(s), ${reconciliation.reportees} reportée(s), ${reconciliation.ecartees} écartée(s).`
                                        )}
                                    </div>
                                )
                            )}
                        </div>
                        <ControlBadge status={c.status} label={c.badgeLabel} />
                    </div>
                ))}
            </div>

            {/* Détail des alertes bloquantes */}
            {bloquants.length > 0 && (
                <div style={{
                    borderTop: '2px solid var(--status-blocking-bg)',
                    background: '#fff5f5',
                    padding: '0.55rem 1rem',
                }}>
                    <div style={{ fontSize: '0.74rem', fontWeight: 700, color: 'var(--status-blocking-text)', marginBottom: '0.3rem', display: 'flex', alignItems: 'center', gap: '0.3rem' }}>
                        <XCircle size={12} /> Anomalies bloquantes
                    </div>
                    {bloquants.map((a, i) => {
                        const hasDrill = !!(a.filtre && Object.keys(a.filtre).length > 0);
                        const domaine = (a.domaine as DomaineTVA) || 'Décaissement';
                        let displayMessage = a.message;
                        if (a.refLigne) {
                            if (a.message.includes("Ligne exclue :")) {
                                displayMessage = a.message.replace("Ligne exclue :", `Ligne exclue (${a.refLigne}) :`);
                            } else {
                                displayMessage = `${a.message} (${a.refLigne})`;
                            }
                        }
                        return (
                            <div key={i} style={{
                                display: 'flex',
                                alignItems: 'center',
                                justifyContent: 'space-between',
                                fontSize: '0.74rem',
                                color: '#7f1d1d',
                                marginTop: '0.2rem',
                                paddingLeft: '1rem',
                                gap: '0.5rem'
                            }}>
                                <span style={{ flex: 1 }}>• {displayMessage}</span>
                                {hasDrill && (
                                    <button
                                        title="Voir les lignes concernées"
                                        onClick={() => onDrill(domaine, a.filtre!, displayMessage)}
                                        style={{
                                            display: 'inline-flex',
                                            alignItems: 'center',
                                            gap: '0.25rem',
                                            padding: '0.1rem 0.4rem',
                                            background: 'white',
                                            border: '1px solid #fca5a5',
                                            borderRadius: 'var(--radius-sm)',
                                            cursor: 'pointer',
                                            fontSize: '0.68rem',
                                            fontWeight: 600,
                                            color: 'var(--status-blocking-text)',
                                            whiteSpace: 'nowrap',
                                            flexShrink: 0,
                                        }}
                                    >
                                        <ExternalLink size={10} /> Voir lignes
                                    </button>
                                )}
                            </div>
                        );
                    })}
                </div>
            )}

            {/* Avertissements non bloquants */}
            {avertissements.length > 0 && (
                <div style={{
                    borderTop: '1px solid var(--status-warning-border)',
                    background: 'var(--status-warning-bg)',
                    padding: '0.55rem 1rem',
                }}>
                    <div style={{ fontSize: '0.74rem', fontWeight: 700, color: 'var(--status-warning-text)', marginBottom: '0.3rem', display: 'flex', alignItems: 'center', gap: '0.3rem' }}>
                        <AlertTriangle size={12} /> Avertissements (non bloquants)
                    </div>
                    {avertissements.map((a, i) => {
                        const hasDrill = !!(a.filtre && Object.keys(a.filtre).length > 0);
                        const domaine = (a.domaine as DomaineTVA) || 'Décaissement';
                        let displayMessage = a.message;
                        if (a.refLigne) {
                            if (a.message.includes("Ligne exclue :")) {
                                displayMessage = a.message.replace("Ligne exclue :", `Ligne exclue (${a.refLigne}) :`);
                            } else {
                                displayMessage = `${a.message} (${a.refLigne})`;
                            }
                        }
                        return (
                            <div key={i} style={{
                                display: 'flex',
                                alignItems: 'center',
                                justifyContent: 'space-between',
                                fontSize: '0.74rem',
                                color: '#78350f',
                                marginTop: '0.2rem',
                                paddingLeft: '1rem',
                                gap: '0.5rem'
                            }}>
                                <span style={{ flex: 1 }}>• {displayMessage}</span>
                                {hasDrill && (
                                    <button
                                        title="Voir les lignes concernées"
                                        onClick={() => onDrill(domaine, a.filtre!, displayMessage)}
                                        style={{
                                            display: 'inline-flex',
                                            alignItems: 'center',
                                            gap: '0.25rem',
                                            padding: '0.1rem 0.4rem',
                                            background: 'white',
                                            border: '1px solid #fed7aa',
                                            borderRadius: 'var(--radius-sm)',
                                            cursor: 'pointer',
                                            fontSize: '0.68rem',
                                            fontWeight: 600,
                                            color: '#c2410c',
                                            whiteSpace: 'nowrap',
                                            flexShrink: 0,
                                        }}
                                    >
                                        <ExternalLink size={10} /> Voir lignes
                                    </button>
                                )}
                            </div>
                        );
                    })}
                </div>
            )}
        </div>
    );
}

function ControlIcon({ status }: { status: Control['status'] }) {
    const size = 16;
    if (status === 'ok') return <CheckCircle2 size={size} style={{ color: 'var(--status-ok-text)', flexShrink: 0, marginTop: 1 }} />;
    if (status === 'error') return <XCircle size={size} style={{ color: 'var(--status-blocking-text)', flexShrink: 0, marginTop: 1 }} />;
    if (status === 'warning') return <AlertTriangle size={size} style={{ color: 'var(--status-warning-text-alt)', flexShrink: 0, marginTop: 1 }} />;
    return <Loader2 size={size} className="animate-spin" style={{ color: 'var(--text-secondary)', flexShrink: 0, marginTop: 1 }} />;
}

function ControlBadge({ status, label }: { status: Control['status']; label?: string }) {
    const map = {
        ok:      { label: 'OK',        bg: 'var(--status-ok-bg)', color: 'var(--status-ok-text)' },
        error:   { label: 'BLOQUANT',  bg: 'var(--status-blocking-bg)', color: 'var(--status-blocking-text)' },
        warning: { label: 'ATTENTION', bg: '#fef9c3', color: 'var(--status-warning-text)' },
        pending: { label: '…',         bg: 'var(--bg-tertiary)', color: 'var(--text-secondary)' },
    } as const;
    const m = map[status];
    return (
        <span style={{
            flexShrink: 0,
            padding: '1px 7px',
            borderRadius: 'var(--radius-full)',
            fontSize: '0.68rem',
            fontWeight: 700,
            background: m.bg,
            color: m.color,
            letterSpacing: '0.04em',
        }}>
            {label || m.label}
        </span>
    );
}
