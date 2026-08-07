// TASK-060 : agrégation pure (sans effet de bord, sans accès réseau) des erreurs de valorisation
// par code, pour affichage dans RapportValorisationModal (FactureInterrogation.tsx).

export type MotifValorisation = {
  code: string;
  message: string;
  refLigne: string;
};

export type CodeMetadata = { label: string; qualiteDonnees: boolean };

export type ErreurGroupee = {
  code: string;
  label: string;
  qualiteDonnees: boolean;
  count: number;
  exemples: string[];
};

export const CODE_METADATA_VALORISATION: Record<string, CodeMetadata> = {
  TIERS_SANS_ICE: { label: 'Fiche tiers sans ICE', qualiteDonnees: true },
  ICE_INVALIDE: { label: 'ICE tiers invalide', qualiteDonnees: true },
  TIERS_SANS_IF: { label: 'Fiche tiers sans Identifiant Fiscal', qualiteDonnees: true },
  IF_INVALIDE: { label: 'Identifiant Fiscal tiers invalide', qualiteDonnees: true },
  REGLEMENT_NON_AFFECTE: { label: 'Règlement non affecté à une facture', qualiteDonnees: false },
  CODE_TAXE_INCONNU: { label: 'Code taxe non reconnu', qualiteDonnees: false },
  ERREUR_FGR: { label: 'Échec de lecture des taxes FGR', qualiteDonnees: false },
  FACTURE_INTROUVABLE: { label: 'Pièce introuvable dans Sage / FGR', qualiteDonnees: false },
  FACTURE_ILLISIBLE_OM: { label: 'Lecture OM Sage échouée / illisible', qualiteDonnees: false },
};

/**
 * Regroupe la liste plate d'erreurs par code. Invariant garanti : la somme des `count` du
 * résultat est toujours égale à `erreurs.length` (TASK-060, livrable de preuve n°2).
 */
export function agregerErreursValorisation(
  erreurs: MotifValorisation[],
  metadata: Record<string, CodeMetadata> = CODE_METADATA_VALORISATION,
): ErreurGroupee[] {
  const map = new Map<string, ErreurGroupee>();
  for (const err of erreurs) {
    const code = err.code || 'AUTRE';
    const meta = metadata[code] || { label: err.message || code, qualiteDonnees: false };
    if (!map.has(code)) {
      map.set(code, { code, label: meta.label, qualiteDonnees: meta.qualiteDonnees, count: 0, exemples: [] });
    }
    const entry = map.get(code)!;
    entry.count++;
    if (err.refLigne && entry.exemples.length < 5 && !entry.exemples.includes(err.refLigne)) {
      entry.exemples.push(err.refLigne);
    }
  }
  return Array.from(map.values()).sort((a, b) => b.count - a.count);
}
