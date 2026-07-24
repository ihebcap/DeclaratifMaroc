import axios from 'axios';

export const API_BASE = (window as any).GOCOM_CONFIG?.API_BASE || import.meta.env.VITE_API_BASE || '/api';

const api = axios.create({
    baseURL: API_BASE,
});

api.interceptors.request.use((config) => {
    const token = sessionStorage.getItem('tva_user');
    if (token) {
        try {
            const user = JSON.parse(token);
            if (user.token) {
                config.headers.Authorization = `Bearer ${user.token}`;
            }
        } catch (e) {}
    }
    return config;
});

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
