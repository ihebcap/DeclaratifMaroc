import axios from 'axios';

export const API_BASE = (window as any).GOCOM_CONFIG?.API_BASE || import.meta.env.VITE_API_BASE || '/api';

const api = axios.create({
    baseURL: API_BASE,
});

api.interceptors.request.use((config) => {
    if (config.url === '/societes' || config.url?.endsWith('/societes')) {
        if (config.headers) {
            if (typeof (config.headers as any).delete === 'function') {
                (config.headers as any).delete('Authorization');
                (config.headers as any).delete('authorization');
            }
            delete (config.headers as any).Authorization;
            delete (config.headers as any).authorization;
        }
        return config;
    }
    const token = sessionStorage.getItem('tva_user');
    if (token) {
        try {
            const user = JSON.parse(token);
            if (user.token && config.headers) {
                config.headers.Authorization = `Bearer ${user.token}`;
            }
        } catch (e) {}
    }
    return config;
});

// Token expiré/invalide (401) : la session en sessionStorage est obsolète, on la purge et on
// recharge pour retomber sur l'écran de login plutôt que de laisser l'app en échec silencieux.
api.interceptors.response.use(
    (response) => response,
    (error) => {
        if (error.response?.status === 401) {
            sessionStorage.removeItem('tva_user');
            window.location.reload();
        }
        return Promise.reject(error);
    }
);

export default api;

// TASK-117 : statut de licence ApLicence (GRLicence). Endpoint public (le blocage /api ne l'exclut
// pas — sinon le front ne pourrait jamais savoir pourquoi il est bloqué). Toujours lu au chargement
// de l'app, avant même l'écran de connexion, puisque le blocage s'applique indépendamment de
// l'authentification.
export interface LicenceStatusDto {
  estValide: boolean;
  message: string | null;
  alerteProcheExpiration: boolean;
  joursRestants: number | null;
  dateExpiration: string | null;
}

export async function getLicenceStatus(): Promise<LicenceStatusDto> {
  const res = await api.get<LicenceStatusDto>('/licence/status');
  return res.data;
}

// TASK-144 : diagnostic explicatif en ligne d'une ligne en anomalie. Lecture seule stricte côté
// back — aucune écriture, aucune nouvelle lecture OM Sage. Les trois blocs (identité échéance /
// résultat lecture OM / contrôle collision DO_Numero) sont volontairement séparés : la collision
// n'est jamais présentée comme LA cause de l'anomalie.
export interface DiagnosticCollisionDto {
  ecId: number;
  ecNo: number;
  tiersCode: string;
  tiersIntitule: string;
  aDocumentSage: boolean;
  doPieceSage: string | null;
  dateDocSage: string | null;
  estLigneConsultee: boolean;
}

export interface DiagnosticLigneDto {
  ecId: number;
  ecNo: number;
  doNumero: string;
  tiersCode: string;
  tiersIntitule: string;
  montantDevise: number | null;
  origine: string;
  motifTechnique: string;
  motifErreurCache: string | null;
  explicationMetier: string;
  actionRecommandee: string | null;
  codeMotifReconnu: string;
  collisionDetectee: boolean;
  collisionCommentaire: string | null;
  collisions: DiagnosticCollisionDto[];
  // TASK-147 : cache PÉRIMÉ (relu avec succès après la création de la déclaration).
  cachePerime: boolean;
  cacheDateLecture: string | null;
  cachePerimeCommentaire: string | null;
}

export async function getDiagnosticLigne(declarationId: string, ecId: number): Promise<DiagnosticLigneDto> {
  const res = await api.get<DiagnosticLigneDto>(`/declarations/${declarationId}/lignes/diagnostic/${ecId}`);
  return res.data;
}

// TASK-147 : recalcule une ligne Proposee dont le cache est périmé — ne redéclenche AUCUNE
// lecture OM Sage, reconstruit la ligne depuis le cache déjà relu avec succès.
export async function recalculerLigneDepuisCache(declarationId: string, ecId: number): Promise<{ message: string }> {
  const res = await api.post(`/declarations/${declarationId}/lignes/recalculer-depuis-cache`, { ecId });
  return res.data;
}

// TASK-167 : relit VRAIMENT Sage (OM) pour cette seule pièce (EC_Id) — action manuelle explicite,
// distincte du recalcul depuis cache ci-dessus (qui ne relit jamais Sage). Même endpoint que la
// resynchronisation TASK-078 (déjà générique), sous le verrou soId partagé (TASK-156) : un appel
// concurrent pour la même société est rejeté en 409 (message explicite renvoyé par le serveur).
export async function relireDepuisSage(declarationId: string, ecId: number): Promise<{ resolue: boolean }> {
  const res = await api.post(`/declarations/${declarationId}/lignes/resynchroniser`, { ecId });
  return { resolue: !!res.data?.resolue };
}

// TASK-025 (solde initial, EC_Type=4 — décision PO) : saisie manuelle du taux + montant de TVA
// par le comptable (le solde n'a aucun détail HT/TVA côté Sage, montant connu en TTC seul).
// Resynchronise la ligne dans le même appel côté back.
export async function enregistrerSaisieSoldeInitial(declarationId: string, ecId: number, taux: number, montantTva: number): Promise<{ resolue: boolean }> {
  const res = await api.post(`/declarations/${declarationId}/lignes/solde-initial-tva`, { ecId, taux, montantTva });
  return { resolue: !!res.data?.resolue };
}

// TASK-176 : resynchronisation EN MASSE — même contrat de sélection que les autres bulks
// (ligneIds OU domaine+filter). Réutilise strictement le pipeline unitaire côté back, traité
// SÉQUENTIELLEMENT (verrou soId TASK-156, pas de parallélisme). Retour agrégé synthétique, pas
// N réponses individuelles. Un 409 signifie qu'un autre traitement OM tenait déjà le verrou avant
// la première pièce (rien fait) ; `interrompu=true` signale une interruption en cours de route
// (lignes déjà passées préservées).
export interface ResynchroLigneAnomalie {
  ecId: number;
  numeroFacture: string;
  motif: string;
}

export interface ResynchroBulkResultat {
  totalSelection: number;
  traitees: number;
  resolues: number;
  nonTrouvees: number;
  toujoursEnAnomalie: ResynchroLigneAnomalie[];
  interrompu: boolean;
  messageInterruption?: string | null;
}

export async function resynchroniserLignesBulk(
  declarationId: string,
  selection: { ligneIds?: string[]; domaine?: string; filter?: string }
): Promise<ResynchroBulkResultat> {
  const res = await api.post(`/declarations/${declarationId}/lignes/resynchroniser:bulk`, selection);
  return res.data as ResynchroBulkResultat;
}

// ─── TASK-130 (Délai de Paiement Maroc — Convention par tiers, FRONT) ──────────────────────────
// Consomme le contrôleur créé pour cette TASK (aucun n'existait — cf. VERIFY TASK-129 « reste à
// valider »), lui-même pur passe-plat vers IConventionDelaiPaiementService (TASK-129, métier non
// dupliqué côté front : plafond 180j vérifié ICI en plus, en local, uniquement pour un retour
// immédiat sans aller-retour serveur — la vérité reste le contrôle serveur).

export type DomaineConvention = 'achat' | 'vente';
export type TypeConvention = 'Convention' | 'Facture';

export interface ConventionDelaiPaiementDto {
  cpId: number;
  tiersNo: number;
  tiersCode: string;
  tiersIntitule: string;
  date: string;
  numero: string;
  dateDebut: string | null;
  dateFin: string | null;
  nombreJoursDelaisPaiement: number;
  domaine: string;
  type: TypeConvention;
  factureNo: number | null;
  factureNumero: string | null;
  hasFile: boolean;
  valide: boolean;
}

export async function getConventionsDelaiPaiement(soId: number, domaine: DomaineConvention): Promise<ConventionDelaiPaiementDto[]> {
  const res = await api.get('/conventions-delai-paiement', { params: { soId, domaine } });
  return res.data as ConventionDelaiPaiementDto[];
}

export interface TiersRechercheDto {
  ctNo: number;
  ctCode: string;
  ctIntitule: string;
}

export async function searchTiersConvention(soId: number, domaine: DomaineConvention, recherche: string): Promise<TiersRechercheDto[]> {
  const res = await api.get('/conventions-delai-paiement/tiers', { params: { soId, domaine, recherche } });
  return (res.data as any[]).map(t => ({ ctNo: t.tiersNo, ctCode: t.tiersCode, ctIntitule: t.tiersIntitule }));
}

export interface FactureNonPayeeDto {
  ecId: number;
  doNumero: string;
  doDate: string;
  montant: number;
  solde: number;
}

export async function getFacturesNonPayees(soId: number, tiersNo: number, domaine: DomaineConvention): Promise<FactureNonPayeeDto[]> {
  const res = await api.get('/conventions-delai-paiement/factures-non-payees', { params: { soId, tiersNo, domaine } });
  return res.data as FactureNonPayeeDto[];
}

export interface CreerConventionPayload {
  soId: number;
  tiersNo: number;
  tiersCode: string;
  date: string;
  numero: string;
  dateDebut?: string | null;
  dateFin?: string | null;
  nombreJoursDelaisPaiement: number;
  domaine: DomaineConvention;
  type: TypeConvention;
  factureNo?: number | null;
  fileName?: string | null;
  fileBase64?: string | null;
}

export async function creerConventionDelaiPaiement(payload: CreerConventionPayload): Promise<{ cpId: number }> {
  const res = await api.post('/conventions-delai-paiement', payload);
  return res.data as { cpId: number };
}

export async function terminerConventionDelaiPaiement(cpId: number, nouvelleDateFin: string): Promise<void> {
  await api.put(`/conventions-delai-paiement/${cpId}/terminer`, { nouvelleDateFin });
}

export async function supprimerConventionDelaiPaiement(cpId: number): Promise<void> {
  await api.delete(`/conventions-delai-paiement/${cpId}`);
}

export function urlFichierConventionDelaiPaiement(cpId: number): string {
  return `/conventions-delai-paiement/${cpId}/fichier`;
}

// ─── TASK-134 (Délai de Paiement Maroc — déclaration : liste/fiche/sélection/contrôle) ─────────
//
// Consomme DeclarationsDelaiPaiementController (créé par TASK-134 : aucun endpoint n'existait pour
// ce domaine, point laissé explicitement par les VERIFY TASK-131/132/133), lui-même pur passe-plat
// vers IDeclarationDelaiPaiementService (TASK-132), ISelectionDelaiPaiementService (TASK-131) et
// IDeclarationDelaiPaiementGenerationService (TASK-133).
//
// RÈGLE STRUCTURANTE (demande PO 19/07/2026) : AUCUNE fonction ci-dessous n'accepte de dateDebut /
// dateFin. Les deux seules périodes possibles sont (a) les bornes de la déclaration parente, lues
// côté serveur, et (b) un exercice + type (+ trimestre) dont les bornes sont CALCULÉES par le même
// code que la création d'une déclaration. Aucun filtre de date libre n'existe dans ce périmètre.

export type TypeDeclarationDdp = 'Annuelle' | 'Trimestrielle';
export type StatutDeclarationDdp = 'EnCours' | 'Cloture';

/** Transitions autorisées, calculées CÔTÉ SERVEUR depuis les gardes TASK-132 (jamais déduites ici). */
export interface ActionsDeclarationDdp {
  peutModifierLibelle: boolean;
  peutIntegrerLignes: boolean;
  peutCloturer: boolean;
  peutAnnulerCloture: boolean;
  peutGenererFichier: boolean;
  peutAnnulerGeneration: boolean;
  peutDeposer: boolean;
  peutSupprimer: boolean;
}

export interface DeclarationDdpDto {
  ddpId: number;
  numero: string;
  soId: number;
  date: string;
  exercice: number;
  type: TypeDeclarationDdp;
  /** 1..4, null pour une annuelle. */
  trimestre: number | null;
  dateDebut: string;
  /** Stocké à 23:59:59 du dernier jour de période (convention legacy reproduite par TASK-132). */
  dateFin: string;
  statut: StatutDeclarationDdp;
  estDeposee: boolean;
  fichierGenere: boolean;
  libelle: string | null;
  nombreLignes: number;
  dateCreation: string;
  dateModification: string;
  actions: ActionsDeclarationDdp;
}

/** Ligne candidate (ou bloquée) renvoyée par la sélection TASK-131. */
export interface LigneSelectionDdpDto {
  ecId: number;
  afId: number | null;
  bucket: string;
  statut: 'Candidate' | 'RepriseManuelleRequise';
  echeanceLegale: string;
  nombreJoursDelaiApplique: number;
  origineDelai: string;
  borneActuelle: string;
  borneReference: string | null;
  origineBorneReference: string;
  /** null (jamais 0) pour une ligne « antérieure à la mise en route — retard réel inconnu ». */
  depassement: number | null;
  montantLigne: number;
  doNumero: string | null;
  doDate: string;
  doReference: string | null;
  echeanceContractuelle: string;
  montantEcheance: number;
  soldeEcheance: number;
  tiersNo: number;
  tiersCode: string | null;
  tiersIntitule: string | null;
  typeReglement: string | null;
  dateReglement: string | null;
  dateRapprochement: string | null;
  reglementNumero: string | null;
  reglementPiece: string | null;
}

export interface SelectionDdpDto {
  dateDebutPeriode: string;
  dateFinPeriode: string;
  /** null = société non configurée (TASK-128) ⇒ 0 candidate, tout en reprise manuelle requise. */
  dateMiseEnRouteSociete: string | null;
  nombreEcheancesExaminees: number;
  /** Échéances de la période déjà portées par une déclaration antérieure (anti-double-déclaration). */
  nombreEcheancesDejaDeclarees: number;
  /** Borne la plus récente déjà déclarée (max DDP_DateFin) ; null si aucune. */
  derniereBorneDejaDeclaree: string | null;
  lignes: LigneSelectionDdpDto[];
  lignesRepriseManuelleRequise: LigneSelectionDdpDto[];
}

export interface LigneIntegreeDdpDto {
  ddplId: number;
  ecId: number;
  afId: number | null;
  depassement: number;
  echeanceLegale: string;
  tiersNo: number;
  tiersCode: string | null;
  tiersIntitule: string | null;
  doNumero: string | null;
  doDate: string;
  doReference: string | null;
  echeanceContractuelle: string;
  montantEcheance: number;
  soldeEcheance: number;
  montantAffecte: number | null;
  reglementNumero: string | null;
  reglementPiece: string | null;
  reglementDate: string | null;
  reglementRapproche: boolean | null;
  reglementDateRapprochement: string | null;
}

export interface CleLigneDdp {
  ecId: number;
  afId: number | null;
}

export interface ResultatIntegrationDdpDto {
  ddpId: number;
  nombreCandidates: number;
  nombreIntegrees: number;
  clesDejaIntegrees: CleLigneDdp[];
  clesRefuseesRepriseManuelleRequise: CleLigneDdp[];
  clesIntrouvablesDansSelection: CleLigneDdp[];
  nombreRepriseManuelleRequiseDisponibles: number;
  dateMiseEnRouteSociete: string | null;
}

export interface FournisseurFautifDdpDto {
  tiersNo: number;
  tiersCode: string;
  tiersIntitule: string | null;
  identifiantFiscal: string | null;
  ice: string | null;
  nombreLignes: number;
  /** Motifs déjà libellés par le back (TASK-132) — affichés tels quels, jamais reformulés. */
  motifsLibelles: string[];
}

export interface ControleIfIceDdpDto {
  estConforme: boolean;
  nombreFournisseursExamines: number;
  nombreLignesExaminees: number;
  messageBloquant: string;
  fournisseursFautifs: FournisseurFautifDdpDto[];
}

export async function getDeclarationsDdp(soId: number): Promise<DeclarationDdpDto[]> {
  const res = await api.get('/declarations-delai-paiement', { params: { soId } });
  return res.data as DeclarationDdpDto[];
}

export async function getDeclarationDdp(ddpId: number): Promise<DeclarationDdpDto> {
  const res = await api.get(`/declarations-delai-paiement/${ddpId}`);
  return res.data as DeclarationDdpDto;
}

/** Type de déclaration par défaut de la société (P_SOCIETE.SO_TypeDecDP, CDC §7.1). */
export async function getParametrageTypeDdp(soId: number): Promise<{ typeParDefaut: TypeDeclarationDdp | null }> {
  const res = await api.get('/declarations-delai-paiement/parametrage', { params: { soId } });
  return { typeParDefaut: (res.data?.typeParDefaut ?? null) as TypeDeclarationDdp | null };
}

export interface CreerDeclarationDdpPayload {
  soId: number;
  exercice: number;
  type: 'annuelle' | 'trimestrielle';
  trimestre?: number | null;
  libelle?: string | null;
}

export async function creerDeclarationDdp(payload: CreerDeclarationDdpPayload): Promise<{ ddpId: number }> {
  const res = await api.post('/declarations-delai-paiement', payload);
  return res.data as { ddpId: number };
}

export async function modifierLibelleDeclarationDdp(ddpId: number, libelle: string | null): Promise<void> {
  await api.put(`/declarations-delai-paiement/${ddpId}/libelle`, { libelle });
}

export async function supprimerDeclarationDdp(ddpId: number): Promise<void> {
  await api.delete(`/declarations-delai-paiement/${ddpId}`);
}

export async function getLignesDeclarationDdp(ddpId: number): Promise<LigneIntegreeDdpDto[]> {
  const res = await api.get(`/declarations-delai-paiement/${ddpId}/lignes`);
  return res.data as LigneIntegreeDdpDto[];
}

export async function supprimerLigneDeclarationDdp(ddpId: number, ddplId: number): Promise<void> {
  await api.delete(`/declarations-delai-paiement/${ddpId}/lignes/${ddplId}`);
}

/** Popup de sélection : la période est celle de la déclaration parente (aucune borne transmise). */
export async function getSelectionDeclarationDdp(ddpId: number): Promise<SelectionDdpDto> {
  const res = await api.get(`/declarations-delai-paiement/${ddpId}/selection`);
  return res.data as SelectionDdpDto;
}

export async function integrerLignesDeclarationDdp(ddpId: number, selection: CleLigneDdp[] | null): Promise<ResultatIntegrationDdpDto> {
  const res = await api.post(`/declarations-delai-paiement/${ddpId}/lignes`, { selection });
  return res.data as ResultatIntegrationDdpDto;
}

export async function cloturerDeclarationDdp(ddpId: number): Promise<void> {
  await api.post(`/declarations-delai-paiement/${ddpId}/cloture`);
}

export async function decloturerDeclarationDdp(ddpId: number): Promise<void> {
  await api.post(`/declarations-delai-paiement/${ddpId}/decloture`);
}

export async function deposerDeclarationDdp(ddpId: number): Promise<void> {
  await api.post(`/declarations-delai-paiement/${ddpId}/depot`);
}

/** Contrôle IF/ICE informatif (ne bloque pas) — sert à annoncer les fautifs avant de générer. */
export async function getControleIfIceDdp(ddpId: number): Promise<ControleIfIceDdpDto> {
  const res = await api.get(`/declarations-delai-paiement/${ddpId}/controle-identite-fiscale`);
  return res.data as ControleIfIceDdpDto;
}

export async function genererFichierDeclarationDdp(ddpId: number): Promise<{ fichier: string }> {
  const res = await api.post(`/declarations-delai-paiement/${ddpId}/generation`);
  return res.data as { fichier: string };
}

export async function annulerGenerationDeclarationDdp(ddpId: number): Promise<void> {
  await api.post(`/declarations-delai-paiement/${ddpId}/generation/annulation`);
}

export function urlFichierDeclarationDdp(ddpId: number): string {
  return `/declarations-delai-paiement/${ddpId}/fichier`;
}

/**
 * Écran de contrôle (CDC §5.A-9) : période RAISONNÉE — exercice + type (+ trimestre). Les bornes
 * exactes sont calculées par le serveur (même code que la création d'une déclaration), jamais
 * saisies. LECTURE SEULE : cet écran n'offre aucun chemin d'intégration.
 */
export async function getControleLignesDdp(
  soId: number,
  exercice: number,
  type: 'annuelle' | 'trimestrielle',
  trimestre: number | null,
): Promise<SelectionDdpDto> {
  const res = await api.get('/declarations-delai-paiement/controle', { params: { soId, exercice, type, trimestre } });
  return res.data as SelectionDdpDto;
}

// ─── TASK-128 : paramétrage « date de mise en route » + reprise manuelle (endpoints DÉJÀ livrés) ──
// Aucun front ne les consommait avant TASK-134 : sans la date de mise en route, TASK-131 renvoie
// 0 ligne intégrable (constaté sur données réelles) et l'écran apparaîtrait vide sans explication.

export async function getDateMiseEnRouteDdp(soId: number): Promise<{ dateMiseEnRoute: string | null }> {
  const res = await api.get(`/delai-paiement/parametrage/${soId}`);
  return { dateMiseEnRoute: (res.data?.dateMiseEnRoute ?? null) as string | null };
}

export async function setDateMiseEnRouteDdp(soId: number, dateMiseEnRoute: string): Promise<void> {
  await api.put(`/delai-paiement/parametrage/${soId}`, { dateMiseEnRoute });
}

/** Reprise manuelle « déjà déclaré jusqu'au [date] » pour UNE échéance (solde d'ouverture, TASK-128). */
export async function setRepriseManuelleDdp(soId: number, ecId: number, dateDejaDeclareeJusquau: string): Promise<void> {
  await api.post('/delai-paiement/reprise', { soId, ecId, dateDejaDeclareeJusquau });
}
