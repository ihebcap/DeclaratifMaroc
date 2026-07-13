# TASK-034 — Contrat DTO des lignes : affichage `undefined%` / `0,00 MAD` dans le poste de travail

## Contexte
Sur l'écran « Poste de travail » (`WorkstationPanel` → `DomainGrid`), les colonnes de la grille affichent `undefined%` (Taux TVA) et `0,00 MAD` (Montant HT / TTC), et la colonne Désignation reste vide. Diagnostic (09/07/2026) : **ce n'est pas le bug TVA back de TASK-030** (déjà livrée — `taux=0` calculé côté `Ventilateur`/`LecteurTvaFgr`). C'est un **désalignement de contrat entre le JSON renvoyé par l'API et les clés lues par le front**.

- L'endpoint `GET /declarations/{id}/lignes` (`DeclarationsController.cs:60`) renvoie l'entité `LigneCandidate` **brute** (`WorkflowEntities.cs:41`).
- L'API n'a **aucune** configuration `JsonNamingPolicy`/`JsonPropertyName` → sérialisation **camelCase par défaut** d'ASP.NET Core, qui ne minuscule **que la 1re lettre** :

| Propriété C# (`LigneCandidate`) | Clé JSON réellement émise | Clé lue par le front (`DomainGrid.tsx`) | Correspond ? |
|---|---|---|---|
| `NumeroFacture` | `numeroFacture` | `factureNumero` | ❌ |
| `HT` | `hT` | `montantHT` | ❌ |
| `Taux` | `taux` | `tauxTVA` | ❌ |
| `TVA` | `tVA` | (`montantTVA`) | ❌ |
| `TTC` | `tTC` | `montantTTC` | ❌ |
| `TiersNom` | `tiersNom` | `tiers` | ❌ |
| `Etat` (int) | `etat` | `statutLigne` | ❌ |
| `MotifRejet` | `motifRejet` | `motif` | ❌ |
| `Conformite` | `conformite` | `statutConformite` | ❌ |
| (aucune) | — | `designation` | ❌ (champ inexistant) |
| `Source` | `source` | `source` | ✅ |
| `NumeroRapprochement` | `numeroRapprochement` | `numeroRapprochement` | ✅ |
| `TiersIdentifiantFiscal` | `tiersIdentifiantFiscal` | `tiersIdentifiantFiscal` | ✅ |
| `TiersICE` | `tiersICE` | `tiersICE` | ✅ |

- Conséquence : `row[col.key]` = `undefined` → `renderCell` (`DomainGrid.tsx:176-196`) produit `` `${undefined}%` `` (« undefined% ») et `formatMoney(undefined)` (« 0,00 MAD »). Seules les colonnes dont la clé coïncide affichent la vraie valeur (`source` = « Decaissement », etc.), d'où l'impression d'affichage décalé.

## Cause racine
Le contrat front↔back n'est **pas explicite** : il repose sur le camelCase accidentel des noms de membres C#. Tout renommage de propriété, ou une casse non anticipée (`HT`→`hT`), casse silencieusement la grille. C'est exactement le type de fragilité contraire à l'objectif de **transparence/confiance** du module.

## Périmètre STRICT
- **Inclus** : rendre le contrat de l'endpoint `GET /declarations/{id}/lignes` **explicite et aligné** avec le vocabulaire déjà utilisé par le front, pour que toutes les colonnes affichent la donnée réelle.
- **Exclu** : aucune modification du **calcul** de la TVA (déjà traité TASK-030), de la sélection, du workflow, ni des règles métier. Ne pas modifier `mockServer.ts`/`mockData.ts` (mode mock hors périmètre — décor de test). Pas de refonte de `DomainGrid` au-delà des clés/formatage.

## Objectif
```
Entrée  : une déclaration réelle avec lignes valorisées (TVA corrigée par TASK-030)
Sortie  : la grille du poste de travail affiche N° Facture, Tiers, Montant HT,
          Taux TVA (ex. 20 %), Montant TTC, Statut et Motif avec les valeurs réelles.
          Plus aucun « undefined% » ni « 0,00 MAD » parasite.
Contrat : les clés JSON sont fixées explicitement, indépendantes de la casse des membres C#.
```

## Approche recommandée (à valider PO)
**Option A — DTO de réponse explicite côté API (RECOMMANDÉE).**
Projeter `LigneCandidate` vers un `record`/DTO de lignes avec `[JsonPropertyName("…")]` alignés sur le vocabulaire front (`factureNumero`, `tiers`, `montantHT`, `montantTVA`, `tauxTVA`, `montantTTC`, `source`, `statutLigne` = `Etat` en int, `motif`, `statutConformite`, `numeroRapprochement`, `tiersIdentifiantFiscal`, `tiersICE`). Le front **ne change quasiment pas**. Avantage : contrat unique, intentionnel, non cassable par un renommage C#. Lecture seule stricte (projection, aucun effet de bord).

**Option B — alignement des clés côté front (plus léger).**
Modifier `defaultColumns` + les colonnes des onglets (`WorkstationPanel.getColumnsForTab`) + `renderCell` pour lire les clés réellement émises (`numeroFacture`, `hT`, `taux`, `tVA`, `tTC`, `tiersNom`, `etat`, `motifRejet`, `conformite`). Front-only, aucun C#. Inconvénient : conserve la dépendance au camelCase accidentel (`hT`/`tVA`/`tTC` peu lisibles) et reste fragile.

> Recommandation architecte : **Option A**. Elle supprime la fragilité de fond ; l'Option B ne fait que déplacer le problème.

## Décision PO requise
- **Colonne « Désignation »** : aucune source dans `LigneCandidate`. → soit (a) la **retirer** de `defaultColumns`, soit (b) l'alimenter par un champ existant (ex. libellé de pièce / `NumeroFacture`). Défaut proposé si non tranché : **retirer la colonne** (ne rien inventer, cohérent « aucune ligne silencieuse »).

## Étapes (Option A)
1. Créer le DTO de réponse (ex. `LigneCandidateDto`) dans la couche appropriée (Application/API), avec `[JsonPropertyName]` conformes au tableau ci-dessus. `statutLigne` = `Etat` (int) — laissé numérique, `renderCell` mappe déjà l'index vers le libellé (`DomainGrid.tsx:180-183`).
2. Projeter dans `DeclarationsController` (`:64-69`) : `lignes.Select(l => new LigneCandidateDto(l))` — projection pure, lecture seule.
3. Vérifier la **cohérence des filtres/tri** : le front envoie déjà `statutLigne`→`etat` (`DomainGrid.tsx:64`) ; s'assurer que les clés de `sort`/`filter` restent celles attendues par le repo (`GetLignesAsync`). Aucune régression sur le filtrage.
4. Front : retirer la colonne `designation` (ou l'alimenter selon décision PO) ; vérifier que `montantTVA`/`statutConformite` sont bien exposés si affichés.
5. Vérifier les 4 onglets (Factures / Rapprochement / Affectation / Conformité) : toutes les colonnes affichent la donnée réelle.

## Livrables
- Contrat explicite de l'endpoint lignes (DTO + `JsonPropertyName`) OU alignement front documenté.
- `VERIFY/TASK-034_verify.md` : **capture réelle** de la grille sur une déclaration valorisée montrant N° Facture / Tiers / HT / Taux (ex. 20 %) / TTC / Statut réels, sans `undefined%` ni `0,00 MAD` parasite ; sur les 4 onglets. Build `tsc + vite` OK, `oxlint` OK. Si Option A : build C# 0 erreur + preuve que le filtrage/tri fonctionne toujours.

## Critères de validation
- Aucune cellule `undefined%` ni `0,00 MAD` non légitime sur une déclaration valorisée.
- Taux TVA affiché en pourcentage réel (20 %, 14 %, …), montants HT/TTC réels.
- Colonne Désignation traitée (retirée ou alimentée) — pas de colonne vide muette.
- Filtres liste/texte/nombre et tri toujours opérants sur les colonnes.
- Lecture seule stricte : aucune écriture, aucun changement de calcul TVA/sélection.

## Risques / dépendances
- **Indépendant de TASK-030** (calcul back) : cette tâche corrige l'**affichage**, pas le calcul. Les deux doivent être livrées pour un rendu correct (TASK-030 ✅ déjà faite).
- **Dépendance douce avec TASK-035** (navigation 2 entrées) : 034 doit idéalement précéder ou accompagner 035 — inutile de restructurer un écran qui n'affiche pas ses données. Ordonner **034 → 035**.
- Faible risque si Option A (projection lecture seule) ; vérifier seulement que le renommage n'orpheline pas une clé de filtre/tri.
