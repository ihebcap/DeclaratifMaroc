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
Traitement : l'export lit la valeur AFFICHÉE de chaque cellule (valueGetter pris en compte) et écrit les dates en vraies cellules date Excel (format jj/mm/aaaa, sans heure)
Sortie  : fichier Excel dont chaque colonne visible contient ce que l'écran affiche ; colonnes date triables/filtrables comme des dates dans Excel
```

## Périmètre STRICT
- **Inclus** : `declaration-tva-web/src/grid/gridExport.ts`, un nouveau fichier pur `declaration-tva-web/src/grid/gridExportValues.ts` (aucun import à l'exécution de `ag-grid`/`xlsx`/React : seuls des `import type` sont admis, pour que `node --test` puisse le charger), un test `declaration-tva-web/tests-unit/gridExportValues.test.ts`, et **une seule ligne** de `ControleLignesDelaiPaiementPanel.tsx` (point 5).
  1. Valeur de cellule : remplacer `node.data[col.field]` par `gridApi.getCellValue({ rowNode: node, colKey: <colId> })` (API AG Grid 36). Utiliser le `colId` de l'état de colonne (pas `field`), et ne plus exclure les colonnes sans `field` mais avec `valueGetter` ; continuer à exclure les colonnes masquées et les colonnes d'action/sélection sans valeur (pas de `field` ni `valueGetter`).
  2. **Dates = vraies cellules date Excel** (PO 08/10/2026 : « je veux des dates Excel, l'essentiel le user ne voit pas l'heure »), affichées `jj/mm/aaaa`, **sans heure**. Sont reconnus comme date : une valeur `Date` ; une chaîne ISO (`^\d{4}-\d{2}-\d{2}([T ]\d{2}:\d{2}(:\d{2}(\.\d+)?)?(Z|[+-]\d{2}:?\d{2})?)?$`) ; une chaîne **exactement** `jj/mm/aaaa` (`^\d{2}/\d{2}/\d{4}$`, c'est ce que renvoient les `valueGetter` du Contrôle DDP via `formatDate`). Jour/mois/année sont extraits **par lecture des caractères** (10 premiers pour l'ISO, **aucun `new Date(chaîne)`**, aucune conversion de fuseau, l'heure est ignorée) ; une date impossible (`31/02/2025`) ou une année hors 1900-2999 reste du texte inchangé. La cellule est construite explicitement, **sans passer par la conversion de `Date` de `xlsx`** (elle dépend du fuseau local) : numéro de série `Date.UTC(a, m-1, j) / 86400000 + 25569` (nombre entier, donc aucune heure), `{ t: 'n', v: série, z: 'dd/mm/yyyy' }`. Construire la feuille avec `XLSX.utils.aoa_to_sheet` (en-têtes en première ligne, cellules objets pour les dates) plutôt que `json_to_sheet`, et conserver le nom d'onglet `Données`.
  3. Un texte qui contient une date mais n'est pas **uniquement** une date (ex. `01/01/2025 → 31/03/2025`, colonne « Dates / N° facture » des Conventions) reste du texte.
  4. Sélection des colonnes : exporter une colonne visible si elle a un `field` **ou** un `valueGetter` ; ne pas exporter celles qui n'ont ni l'un ni l'autre (colonnes d'actions). Clé de colonne = `colId`. En-têtes en double dans une même grille : suffixer le second (`Statut (2)`) au lieu d'écraser la colonne (l'objet est indexé par `headerName`, `gridExport.ts:28`). Une valeur qui n'est ni texte, ni nombre, ni booléen, ni `Date` devient une cellule vide (jamais `[object Object]`).
  5. `ControleLignesDelaiPaiementPanel.tsx` l.204, colonne `Statut` : elle n'a ni donnée `statut` ni `valueGetter` (seulement un `cellRenderer` qui affiche « Retard calculé »), donc elle resterait vide après le correctif. Ajouter `valueGetter: (p) => p.data ? 'Retard calculé' : ''`. Aucune autre modification de colonne.
- **Exclus** (ne pas toucher, signaler seulement) : le formatage des montants (le Contrôle DDP exporte `Montant` en texte « 1 234,00 MAD » car `valueGetter` renvoie une chaîne ; passage en nombre = TASK ultérieure si le PO le demande) ; les autres colonnes et `valueGetter` de l'écran et des autres grilles ; les autres exports (`DeclarationFinalePanel`, `VerifierIntegrerPanel`, `ControlGrid` s'ils ont leur propre export — vérifier par grep et signaler ; `ControlGrid` et `ReglementsSelection` ont `showExportButton={false}`) ; l'export XML/Excel serveur (`Declaration.Export.*`) ; tout code .NET.

## Étapes
1. Lire `gridExport.ts`, `ApbsGrid.tsx` (l.166-170, `handleExport`) et le panel Contrôle DDP. `grep exportGridToExcel` : lister toutes les grilles concernées.
2. Implémenter (1) à (3). Pas de nouvelle dépendance. Pas de formateur dupliqué : si un utilitaire de date `jj/mm/aaaa` sans fuseau existe, le réutiliser, sinon fonction locale pure et exportée pour test.
3. Tests. **(a) Unitaire (obligatoire)** : `npm run test:unit` (`node --test tests-unit/**/*.test.ts`, déjà en place, import avec extension `.ts` comme `valorisationErreurs.test.ts`) sur les fonctions pures de `gridExportValues.ts` (conversion de valeur, filtrage/dédoublonnage des colonnes) : tous les cas du tableau ci-dessous, plus `null`/`undefined`/objet/booléen. Vérifier le numéro de série exact (45770 pour 23/04/2025) et l'absence de partie décimale. Pour le décalage de fuseau, lancer aussi avec `TZ=America/Los_Angeles` et `TZ=Pacific/Kiritimati` et noter le résultat. **(b) Playwright (si les données de test le permettent)** : test d'export (`page.waitForEvent('download')`, lecture du `.xlsx` avec la lib `xlsx`) sur l'écran Contrôle DDP avec au moins une ligne : colonnes `Fournisseur` et `Facture` non vides, `Date facture`/`Échéance légale`/`Dernière déclaration`/`Constaté le` au format `jj/mm/aaaa`. Si aucune donnée de test ne permet de charger l'écran, le dire dans le VERIFY et fournir une vérification manuelle pas à pas (ne pas inventer de résultat).
4. Contrôle de non-régression : exporter au moins **2 autres grilles** `ApbsGrid` (une avec colonnes simples, une avec `valueGetter`) et comparer avant/après ; consigner les différences attendues (colonnes calculées désormais remplies, dates reformatées).

## Scénarios (à jouer à la main ou en test)
| Cas | Attendu dans Excel |
|---|---|
| Ligne avec fournisseur `F001 · ACME` et facture `FA2025-123` | colonnes Fournisseur = `F001 · ACME`, Facture = `FA2025-123` |
| `doDate = 2025-04-23T00:00:00` | cellule **date** (numéro de série 45770), affichée `23/04/2025`, sans heure |
| `doDate = 2025-12-31T23:30:00` (heure ignorée, pas de décalage de fuseau) | date `31/12/2025` (série 46022), jamais `01/01/2026` |
| Texte `23/04/2025` (valueGetter du Contrôle DDP) | même cellule date que ci-dessus |
| `31/02/2025`, `1850-01-01` | texte inchangé |
| `01/01/2025 → 31/03/2025` | texte inchangé |
| Tri/filtre par date dans Excel | chronologique (c.-à-d. vraies dates, pas du texte) |
| `borneReference` vide / null | cellule vide |
| Colonne masquée par l'utilisateur | absente du fichier |
| Filtre/tri actifs dans la grille | lignes exportées dans l'ordre et selon le filtre affichés (comportement actuel conservé) |
| Valeur numérique (dépassement `273`) | reste un nombre |
| Texte non date (ex. `2025-04` ou `ABC-2025-01-01`) | inchangé |
| `2025-04-23T00:00:00Z` ou `+01:00` | `23/04/2025` (10 premiers caractères, sans conversion de fuseau : `DO_Date` est une date sans fuseau) |
| Colonne `Statut` du Contrôle DDP | `Retard calculé` |
| Deux colonnes de même en-tête | deux colonnes (`X`, `X (2)`), aucune perdue |
| Colonne d'actions (ni `field` ni `valueGetter`) | absente du fichier |
| Booléen (`valide` = true, Conventions) | `TRUE` dans Excel (valeur brute conservée, signalé) |

## Validation (preuve par critère, datée, dans le VERIFY)
- [ ] `npm run lint` et `npm run build` (dans `declaration-tva-web/`) : 0 erreur
- [ ] `dotnet build DeclarationTVA.slnx` : 0 erreur (aucun .NET modifié : le prouver par `git diff --stat`)
- [ ] Export du Contrôle DDP : Fournisseur et Facture remplis (méthode + capture ou lecture du fichier)
- [ ] 4 colonnes date du Contrôle DDP = vraies cellules date (type numérique + format `dd/mm/yyyy`, relu avec `xlsx` : `cell.t==="n"`, `cell.z`), sans heure ni décalage d'un jour ; tri chronologique vérifié dans Excel
- [ ] Non-régression sur 2 autres grilles (étape 4)
- [ ] `npm run test:unit` : tous verts, y compris sous les 2 fuseaux `TZ` (sortie collée)
- [ ] Colonne `Statut` du Contrôle DDP remplie
- [ ] Liste des grilles impactées + autres exports non traités (grep) dans le VERIFY
- [ ] Checklist review UI de `DOCS/UI_STANDARDS.md`

## Dépendances / risques
- Aucune dépendance bloquante. **TASK-221** (en cours, périmètre .NET/Core/SQL) ne touche aucun fichier de celle-ci ; les deux peuvent être livrées indépendamment (commits séparés).
- Grilles à export actif concernées (8) : Contrôle DDP, Conventions DDP, Liste des déclarations TVA, Déclarations DDP (2 grilles), DomainGrid, Interrogation factures, Interrogation rapprochement. Pour les 3 grilles à colonnes dynamiques (`DomainGrid`, `FactureInterrogation`, `RapprochementInterrogation`, `field = col.key` + `cellRenderer`), le fichier contiendra désormais la valeur brute de la donnée (nombres bruts au lieu d'un affichage formaté, dates reformatées) : à comparer avant/après dans le VERIFY.
- Risque : changement de contenu des exports des autres grilles (colonnes calculées désormais remplies, dates reformatées) — voulu, à signaler dans le message de livraison.

## Correction demandée par la review (08/10/2026) — REJECT non bloquant sur le fond, 1 point
**Largeur des colonnes** : `gridExport.ts` ne définit aucune largeur (`!cols`). Excel affiche `########` dans une cellule **numérique/date** trop étroite (largeur par défaut ~8,4 caractères < `23/04/2025`), alors qu'un texte débordait. Les nouvelles cellules date risquent donc d'apparaître illisibles à l'ouverture.
À faire : poser `worksheet['!cols']` avec, pour chaque colonne, `wch = max(longueur de l'en-tête, longueur du texte affiché de chaque cellule, 10 pour une cellule date) + 2`, plafonné à 60. Fonction pure dans `gridExportValues.ts` (ex. `computeColumnWidths(aoa)`), test unitaire (colonne date >= 12, colonne texte longue plafonnée, en-tête long pris en compte), relecture XLSX du `!cols` dans le test de simulation. Mettre à jour `VERIFY/TASK-223_verify.md` (preuves relancées) ; même point d'arrêt (pas de clôture).
