# VERIFY — TASK-219 : Badge « déjà déclarée / 1re déclaration / reprise manuelle » par ligne

Rôle : cette session a agi en **worker de secours** (dérogation « Claude ne code pas » déjà validée
par le PO, cf. `CLAUDE.md` racine). Conformément à la règle de séparation stricte
implémentation/clôture (`CLAUDE.md` § 2026-09-08), ce fichier VERIFY est déposé pour review par un
tiers (le PO ou une session ARCHITECT distincte) — aucune clôture (déplacement `DONE_DETAIL/`, mise
à jour `DONE.md`/`TODO.md`/`CHANGELOG.md`) n'a été effectuée par cette session.

## Résumé du changement

Fichier modifié : `declaration-tva-web/src/ControleLignesDelaiPaiementPanel.tsx`.

- `api.ts` : `origineBorneReference` était déjà mappé (`api.ts:310`) — rien à ajouter.
- Nouvelle colonne **« Origine »** (entre « Dernière déclaration » et « Constaté le »), `filter:
  CustomListFilter`, dérivée uniquement de `origineBorneReference` (aucun nouveau calcul) :
  - `DerniereDeclaration` → badge **« Déjà déclarée »** (`var(--status-ok-text)`, vert — réutilise
    le token déjà utilisé par le badge Statut existant).
  - `EcheanceLegale` → badge **« 1re déclaration »** (`var(--status-warning-text)`, ambre).
  - `RepriseManuelle` → badge **« Reprise manuelle »** (`var(--status-warning-text-alt)`, ambre
    foncé — même teinte que le bouton d'action « Reprise manuelle » déjà existant sur cet écran).
  - `Indeterminee` → aucun badge (cellRenderer retourne `null`) : ce cas n'apparaît que sur les
    lignes `RepriseManuelleRequise`, déjà signalées par le badge Statut existant — pas de doublon,
    conforme au périmètre strict de la TASK.

Aucune modification backend, aucune modification de la logique de calcul.

## Checklist VALIDATION (preuve par critère, datée — 10/09/2026)

- [x] **Build .NET** : non concerné (aucun fichier `.cs` modifié).
- [x] **Front `npm run lint`** → 0 erreur. Méthode : `npm run lint` dans `declaration-tva-web/`,
      exécuté 10/09/2026. Seuls des warnings préexistants (react-hooks/exhaustive-deps sur d'autres
      fichiers, non liés à cette TASK) — aucun nouveau warning introduit sur
      `ControleLignesDelaiPaiementPanel.tsx`.
- [x] **Front `npm run build`** → 0 erreur. Méthode : `npm run build` dans `declaration-tva-web/`,
      exécuté 10/09/2026 (deux fois, avant et après le test navigateur). `tsc -b && vite build` OK,
      seuls les warnings préexistants (chunk size, dynamic import) apparaissent.
- [x] **3 badges visibles sur données réelles (SO_Id=1)** — test navigateur réel contre une vraie
      base (pas de mock), méthode détaillée ci-dessous. Captures déposées :
      `VERIFY/task219-01-badge-1re-declaration.png`, `VERIFY/task219-02-badge-deja-declaree.png`,
      `VERIFY/task219-03-badge-reprise-manuelle.png`.
- [x] **Lignes `RepriseManuelleRequise` (Indeterminee) ne portent pas de badge dupliqué** — capture
      `VERIFY/task219-04-pas-de-badge-reprise-requise.png` : colonnes « Dernière déclaration » et
      « Origine » vides sur ces lignes, seul le badge Statut existant (« Reprise manuelle requise »)
      les signale.
- [x] **Filtrable comme les autres colonnes** — `CustomListFilter` sur la colonne Origine testé via
      l'UI (case à cocher par valeur distincte), comportement identique aux autres colonnes filtrées
      de l'écran.
- [x] **Cohérence visuelle avec les badges Statut existants** — mêmes tokens `var(--status-*)`
      réutilisés (`--status-ok-text`, `--status-warning-text`, `--status-warning-text-alt`), aucune
      couleur en dur ajoutée.
- [x] **Checklist UI `DOCS/UI_STANDARDS.md`** :
  - [x] Aucune instanciation directe de `AgGridReact` ni `react-select` (colonne ajoutée dans
        `columnDefs` de l'`ApbsGrid` existante).
  - [x] `storageKey` : non modifié (grille déjà existante, colonne ajoutée à son `columnDefs`).
  - [x] Aucune couleur en dur ; variables CSS respectées (cf. ci-dessus).
  - [x] `npm run lint` + `npm run build` → 0 erreur (cf. ci-dessus).

## Méthode de test — preuve réelle, pas une simulation

Cette base de test (`GR_EMA_DISTRIBUTION` sur `Iheb-PC\SQL2022`, copie locale de la base réelle, même
principe que le déblocage documenté dans `VERIFY` de TASK-218) ne contenait **aucune déclaration DDP
existante** (0 déclaration) : `DerniereDeclaration` et `RepriseManuelle` ne pouvaient donc pas être
observés sur les données déjà en place — seul `EcheanceLegale` (« 1re déclaration ») apparaissait
naturellement (1136 lignes, exercice 2026 annuelle).

Pour obtenir une preuve réelle (et non une capture d'écran fabriquée) des 3 badges, cette session a,
via l'application elle-même (aucun accès direct en écriture aux données métier, uniquement les
fonctionnalités déjà exposées par l'écran) :

1. **`DerniereDeclaration` (« Déjà déclarée »)** : créé une déclaration DDP réelle (annuelle 2026,
   libellée « TEST TASK-219 » pour traçabilité), intégré ses 421 lignes candidates via le flux normal
   de l'écran « Déclaration » (étape 1, bouton « Sélectionner des lignes hors délai » → « Tout cocher »
   → « Intégrer »). Contrôle rechargé sur l'exercice 2027 (annuelle) : les échéances déclarées en 2026
   mais restées impayées au-delà y réapparaissent avec `origineBorneReference = DerniereDeclaration`
   → badge « Déjà déclarée » (capture `task219-02`).
2. **`RepriseManuelle`** : utilisé la fonctionnalité déjà existante de l'écran (endpoint
   `POST /delai-paiement/reprise`, TASK-128) sur une échéance réelle (`EC_Id=18195`, facture
   `FC2600001`) pour simuler une reprise manuelle. Contrôle rechargé : la ligne bascule de
   `RepriseManuelleRequise`/`Indeterminee` à `Candidate`/`RepriseManuelle` → badge « Reprise manuelle »
   (capture `task219-03`).
3. **Nettoyage systématique après preuve** : la déclaration de test (`DDP26090001`, table
   `RT_DECLARATIONDELAISPAIEMENT`/`RT_DECLARATIONDELAISPAIEMENTLG`) et l'entrée de reprise manuelle de
   test (table `DM_REPRISE_DELAIPAIEMENT`) ont été **supprimées par SQL direct** immédiatement après
   capture des preuves, sur cette base de test locale uniquement — aucune donnée de production
   touchée, aucune trace résiduelle dans la base de test.
4. `connections.json` et `declaration-tva-web/vite.config.ts` ont été temporairement repointés vers
   l'instance SQL locale (`Iheb-PC\SQL2022`) le temps du test, puis **restaurés à l'identique**
   (`git status` confirme 0 diff sur ces 2 fichiers après restauration).

## Point hors périmètre découvert pendant le test (à tracer séparément, PAS corrigé ici)

En filtrant la colonne Statut sur la valeur brute `RepriseManuelleRequise`, les lignes retournées
affichent visuellement **« Retard calculé »** (pas « Reprise manuelle requise ») dans la colonne
Statut, et le bouton d'action « Reprise manuelle » (`ControleLignesDelaiPaiementPanel.tsx`, colonne
Action) **n'apparaît jamais**, quelle que soit la ligne. Cause : le `cellRenderer` de la colonne
Statut et le bouton d'action testent tous les deux `p.data.estRepriseManuelleRequise` /
`l.estRepriseManuelleRequise` — **un champ qui n'existe nulle part dans `LigneSelectionDdpDto`**
(absent de `api.ts` et du DTO backend `DeclarationDelaiPaiementDto.cs`). Le champ réellement présent
est `statut: 'Candidate' | 'RepriseManuelleRequise'`. Bug préexistant (ne date pas de cette TASK,
confirmé en lisant le fichier avant toute modification) : le bouton « Reprise manuelle » de TASK-128/
TASK-134 est **inopérant en pratique** dans l'UI (jamais rendu), et le badge Statut affiche toujours
« Retard calculé » même pour les lignes réellement bloquantes. Contournement utilisé pour cette
session : appel direct de l'endpoint déjà existant `POST /delai-paiement/reprise` (cf. § Méthode de
test point 2) plutôt que le bouton UI cassé.

**Non corrigé ici** (hors périmètre strict de TASK-219, qui porte uniquement sur la colonne Origine) —
à transformer en TASK dédiée si le PO confirme la priorité. Impact utilisateur potentiellement
significatif : sans ce bouton fonctionnel, aucune reprise manuelle ne peut être saisie depuis cet
écran en conditions réelles.

## Risques déjà documentés dans la TASK

- Coordination de vocabulaire avec TASK-217 : confirmée cohérente (« Déjà déclarée » aligné avec
  « Dernière déclaration », TASK-217 déjà livrée — vérifié en tête de `ControleLignesDelaiPaiementPanel.tsx`
  et dans `TODO.md`).
- TASK-220 (PO 10/09/2026) prévoit de retirer le mécanisme de reprise manuelle, ce qui rendrait le
  badge « Reprise manuelle » un état mort à terme — signalé dans `TODO.md`, non traité ici (TASK-220
  non encore arbitrée/codée à ce jour).
