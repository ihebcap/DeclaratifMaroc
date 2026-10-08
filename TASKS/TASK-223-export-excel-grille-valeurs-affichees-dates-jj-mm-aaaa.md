# TASK-223 — Export Excel des grilles : colonnes calculées vides (Fournisseur, Facture…) et dates au format jj/mm/aaaa

RISK : MEDIUM (composant partagé par toutes les grilles `ApbsGrid` ; aucun impact déclaratif ni donnée).
Point d'arrêt : dépôt de `VERIFY/TASK-223_verify.md`. **Interdit** : déplacer vers `DONE_DETAIL/`, modifier `DONE.md`/`TODO.md`/`CHANGELOG.md`, commit contenant « approuve ». La clôture revient à une review par un agent qui n'a pas implémenté.
TASK UI : lire `DOCS/UI_STANDARDS.md` ; checklist review UI cochée dans le VERIFY (le bouton d'export existe déjà, aucun changement visuel attendu).

## Contexte
Signalé par le PO (08/10/2026) sur l'écran **Contrôle DDP** (`declaration-tva-web/src/ControleLignesDelaiPaiementPanel.tsx`) : à l'export Excel, les colonnes **Fournisseur** et **Facture** sont vides, et les dates ne sont pas au format `jj/mm/aaaa`.

**Cause (lue dans le code, non reproduite sur machine)** : `exportGridToExcel` (`declaration-tva-web/src/grid/gridExport.ts:26-29`) lit `node.data[colDef.field]`, c.-à-d. la donnée brute de la ligne. Or sur cet écran plusieurs colonnes ont un `field` qui n'existe pas dans la donnée et sont calculées par `valueGetter` (l.204-235) :
- `tiers` (Fournisseur, `tiersCode · tiersIntitule`), `facture` (`doNumero`), `statut`, `origineDelai`, `origineBorneReference`, `montant` (donnée = `montantLigne`), `mode`, `bucket`, `commentaire` → valeur `undefined` → cellule vide ;
- les colonnes date (`doDate`, `echeanceLegale`, `borneReference`, `borneActuelle`) existent dans la donnée mais en chaîne ISO brute (`2025-04-23T00:00:00`), non formatée.
Le bug touche donc **toute grille `ApbsGrid` dont une colonne est calculée**, pas seulement le Contrôle DDP.

## Objectif
```
Entrée  : export Excel lisant node.data[field] (valeurs brutes, colonnes calculées vides, dates ISO)
Traitement : l'export lit la valeur AFFICHÉE de chaque cellule (valueGetter pris en compte) et formate toute date en jj/mm/aaaa
Sortie  : fichier Excel dont chaque colonne visible contient ce que l'écran affiche ; dates en jj/mm/aaaa
```

## Périmètre STRICT
- **Inclus** : `declaration-tva-web/src/grid/gridExport.ts` uniquement (+ test).
  1. Valeur de cellule : remplacer `node.data[col.field]` par `gridApi.getCellValue({ rowNode: node, colKey: <colId> })` (API AG Grid 36). Utiliser le `colId` de l'état de colonne (pas `field`), et ne plus exclure les colonnes sans `field` mais avec `valueGetter` ; continuer à exclure les colonnes masquées et les colonnes d'action/sélection sans valeur (pas de `field` ni `valueGetter`).
  2. Dates : toute valeur de type `Date`, ou chaîne ISO (`^\d{4}-\d{2}-\d{2}([T ]\d{2}:\d{2}(:\d{2}(\.\d+)?)?(Z|[+-]\d{2}:?\d{2})?)?$`), est écrite au format texte `jj/mm/aaaa` (extraction par les 10 premiers caractères pour une chaîne : **aucun passage par `new Date(...)`**, pour éviter tout décalage de fuseau). Ne pas toucher aux nombres ni aux autres textes.
  3. Une valeur déjà affichée en `jj/mm/aaaa` (cas du Contrôle DDP via `formatDate`) reste inchangée.
- **Exclus** (ne pas toucher, signaler seulement) : le formatage des montants (le Contrôle DDP exporte `Montant` en texte « 1 234,00 MAD » car `valueGetter` renvoie une chaîne ; passage en nombre = TASK ultérieure si le PO le demande) ; les colonnes et `valueGetter` de l'écran ; les autres exports (`DeclarationFinalePanel`, `VerifierIntegrerPanel`, `ControlGrid` s'ils ont leur propre export — vérifier par grep et signaler) ; l'export XML/Excel serveur (`Declaration.Export.*`) ; tout code .NET.

## Étapes
1. Lire `gridExport.ts`, `ApbsGrid.tsx` (l.166-170, `handleExport`) et le panel Contrôle DDP. `grep exportGridToExcel` : lister toutes les grilles concernées.
2. Implémenter (1) à (3). Pas de nouvelle dépendance. Pas de formateur dupliqué : si un utilitaire de date `jj/mm/aaaa` sans fuseau existe, le réutiliser, sinon fonction locale pure et exportée pour test.
3. Tests : le front n'a pas de runner unitaire garanti — vérifier (`package.json`) ; sinon test Playwright d'export (`page.waitForEvent('download')`, lecture du `.xlsx` avec la lib `xlsx`) sur l'écran Contrôle DDP avec au moins une ligne : colonnes `Fournisseur` et `Facture` non vides, `Date facture`/`Échéance légale`/`Dernière déclaration`/`Constaté le` au format `jj/mm/aaaa`. Si aucune donnée de test ne permet de charger l'écran, le dire dans le VERIFY et fournir une vérification manuelle pas à pas (ne pas inventer de résultat).
4. Contrôle de non-régression : exporter au moins **2 autres grilles** `ApbsGrid` (une avec colonnes simples, une avec `valueGetter`) et comparer avant/après ; consigner les différences attendues (colonnes calculées désormais remplies, dates reformatées).

## Scénarios (à jouer à la main ou en test)
| Cas | Attendu dans Excel |
|---|---|
| Ligne avec fournisseur `F001 · ACME` et facture `FA2025-123` | colonnes Fournisseur = `F001 · ACME`, Facture = `FA2025-123` |
| `doDate = 2025-04-23T00:00:00` | `23/04/2025` |
| `doDate = 2025-12-31T23:30:00` (pas de décalage de fuseau) | `31/12/2025` |
| `borneReference` vide / null | cellule vide |
| Colonne masquée par l'utilisateur | absente du fichier |
| Filtre/tri actifs dans la grille | lignes exportées dans l'ordre et selon le filtre affichés (comportement actuel conservé) |
| Valeur numérique (dépassement `273`) | reste un nombre |
| Texte non date (ex. `2025-04` ou `ABC-2025-01-01`) | inchangé |

## Validation (preuve par critère, datée, dans le VERIFY)
- [ ] `npm run lint` et `npm run build` (dans `declaration-tva-web/`) : 0 erreur
- [ ] `dotnet build DeclarationTVA.slnx` : 0 erreur (aucun .NET modifié : le prouver par `git diff --stat`)
- [ ] Export du Contrôle DDP : Fournisseur et Facture remplis (méthode + capture ou lecture du fichier)
- [ ] 4 colonnes date au format `jj/mm/aaaa`, sans décalage d'un jour
- [ ] Non-régression sur 2 autres grilles (étape 4)
- [ ] Liste des grilles impactées + autres exports non traités (grep) dans le VERIFY
- [ ] Checklist review UI de `DOCS/UI_STANDARDS.md`

## Dépendances / risques
- Aucune dépendance bloquante. **TASK-221** (en cours, périmètre .NET/Core/SQL) ne touche aucun fichier de celle-ci ; les deux peuvent être livrées indépendamment (commits séparés).
- Risque : changement de contenu des exports des autres grilles (colonnes calculées désormais remplies, dates reformatées) — voulu, à signaler dans le message de livraison.
