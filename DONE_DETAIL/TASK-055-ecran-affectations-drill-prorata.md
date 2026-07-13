# TASK-055 — Écran ② Affectations (drill règlement→factures + prorata multi-taux)

> Étape ② du tunnel (TASK-053). Front. Densité **0 espace perdu**. L'unité métier = **affectation règlement→facture**, pas la facture seule.

## Contexte
Depuis un règlement sélectionné en ① (TASK-054), montrer les **factures qu'il paie** et le calcul de proratisation (`reflexion dectva.md` §3). Le raisonnement doit être **visible** : `payé ÷ TTC = %` puis `TVA Sage × % = TVA déclarée`, y compris multi-paiement et multi-taux. Les données de valorisation existent (worker OM TASK-023, FGR SQL TASK-022, cache TASK-024, conformité IF/ICE TASK-027). Le composant de preuve `ProofModal.tsx` existe déjà (TASK-038) et sert de justificatif à la demande (§7).

## Périmètre STRICT
- **Inclus** : vue drill d'un règlement (en-tête règlement + fournisseur + IF/ICE) ; liste dense des factures affectées avec, par facture, `HT/TVA(taux)/TTC` Sage, `montant payé`, `prorata`, `TVA déclarée` ; gestion multi-taux (plusieurs lignes dans la même facture) ; total TVA du règlement ; ouverture `ProofModal` (justificatif §7).
- **Exclu** : la vue calcul agrégée déclaration (③ = TASK-056) ; toute modif du calcul back (réutiliser tel quel) ; panneau latéral (décision PO : preuve à la demande via modal, pas de side-panel).

## Objectif
```
Entrée : un règlement sélectionné (RT_AFFECTATION → RT_ECHEANCE factures)
Traitement : afficher chaque affectation avec payé÷TTC×TVA (multi-taux = N lignes)
Sortie : détail auditable par facture + total TVA du règlement, justificatif à la demande
```

## Étapes
1. Câbler sur la valorisation existante (origine `EC_Type` Sage/FGR, TASK-022/023 ; cache TASK-024) — jamais un `0` muet : « non valorisé » + motif si lecture OM absente (mémoire `grf-valorisation-tracabilite-blocage-om`).
2. Rendu dense par facture : `payé ÷ TTC = %`, `TVA × % = déclarée` affichés comme une **opération** (pas un chiffre magique).
3. Multi-taux : plusieurs lignes taux dans la même facture, chacune proratée (arrondi `AwayFromZero`/DGI, cohérent TASK-004/006).
4. Bouton preuve → `ProofModal` enrichi timeline `Facture → Règlement → Rapprochement → Calcul` (justificatif §7, réutilisation TASK-038).
5. Total TVA du règlement en pied de section.

## Livrables
- Écran ② (composant drill, réutilisant `DomainGrid`/`ProofModal` autant que possible).
- `VERIFY/TASK-055_verify.md` : preuve réelle (un règlement multi-facture + un cas partiel + un cas multi-taux si présent en base, sinon fixture), cohérence des prorata, justificatif ouvert, build + lint verts.

## Critères de validation
- Prorata et TVA déclarée exacts et **traçables à l'écran** (opération visible).
- Multi-taux et paiement partiel gérés sans écran ad hoc supplémentaire.
- Aucune facture « 0 » silencieuse : « non valorisé » + motif si applicable.
- Justificatif = `ProofModal` (pas de panneau latéral). Lecture seule stricte.

## Risques / dépendances
- Dépend de TASK-054 (sélection) et TASK-053 (navigation).
- Réutilise TASK-022/023/024/027/038 — aucun recalcul back.
- Si le multi-taux est absent de la base réelle (cf. TASK-022 : base mono-bucket), prouver par fixture et le signaler.
- 🚫 **Anti-régression** : le calcul back (valorisation OM/FGR/cache 022/023/024, conformité 027) et `ProofModal.tsx` (TASK-038) sont **intouchables** — cette task n'ajoute qu'une vue drill front qui **consomme** ces sorties. Aucun recalcul front, aucun nouveau chemin de valorisation.
