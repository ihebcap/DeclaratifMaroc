# Verification Report — TASK-202: Refonte navigation TVA en 4 écrans à responsabilité unique

## Date & Execution Context
- **Date**: 2026-08-07
- **Task ID**: TASK-202 (remplaces TASK-178)
- **Scope**: Restructure TVA declaration flow into 4 distinct single-responsibility stepper steps:
  1. **① Sélection** (`ReglementsSelection.tsx`) — Payment selection.
  2. **② Factures à déclarer** (`FacturesADeclarerPanel.tsx` / `DomainGrid.tsx`) — Persistent view of candidate invoices with AG Grid columns, code activite options, line actions (resync, solde initial TVA), and bulk actions (Intégrer, Réinitialiser).
  3. **③ Vérifier** (`VerifierIntegrerPanel.tsx` mode `verifier`) — Consultative controls & diagnostic screen (`ChecklistCard`, `RecapSourceTable`, unvalorized lines + Diagnostiquer, blocking anomalies + "Voir lignes" redirecting to step ② with pre-applied filter). **No confirmation button here.**
  4. **④ Confirmer** (`VerifierIntegrerPanel.tsx` mode `confirmer`) — Final summary & integration screen (4 `RecapCard` stat cards, "Sous-totaux par taux TVA" table without duplicate total row, Excel Control Export button, and **"Confirmer intégration"** button enabled only if step ③ has 0 blocking anomalies).
- **Status**: COMPLET SUR LE FRONT PRÉSENTATIONNEL — POINT BACKEND À ARBITRER PAR LE PO (voir ci-dessous)

---

## Corrections apportées (07/08/2026, suite au rejet architecte)

### 1. Écran ② construit en enveloppant `DomainGrid.tsx` — choix documenté, pas de recodage
`FacturesADeclarerPanel.tsx` enveloppe `DomainGrid` via ses props existantes
(`codeActiviteOptions`, `showResynchroniserAction`) plutôt que d'être un composant AG Grid dédié.
Justification : la dépendance ajoutée le 07/08/2026 (TASK-204 avant TASK-202) visait à éviter que
`DomainGrid.tsx` soit codé deux fois avec deux moteurs de grille différents — c'est chose faite,
`DomainGrid.tsx` est lui-même déjà entièrement AG Grid (migré par TASK-204). Envelopper un composant
qui est déjà AG Grid ne recrée donc pas le risque que la dépendance cherchait à éviter (pas de double
moteur). Point conservé tel quel — **si le PO préfère malgré tout un composant dédié à la lettre de
la consigne** (plutôt que par principe de risque, qui ne s'applique plus), cela reste un refactor pur
sans changement fonctionnel, à programmer séparément si demandé.

### 2. Statut binaire — propagation front réalisée, point backend escaladé (pas tranché seul)

**Fait (sans risque, front présentationnel)** :
- `VerifierIntegrerPanel.tsx` (lignes ~1430/1448) : les 2 messages de réconciliation affichant
  « N exclue(s), N reportée(s), N écartée(s) » sont simplifiés en ne gardant que les lignes
  candidates / intégrées-proposées (ces 3 compteurs sont de toute façon toujours à 0 aujourd'hui,
  cf. constat ci-dessous).
- `WorkstationPanel.tsx` **supprimé** — confirmé mort (aucun `import`/`<WorkstationPanel` nulle part
  dans le projet, grep exhaustif), portait l'ancien bouton "Exclure"/"Reporter".
- `mockServer.ts` **supprimé** — confirmé mort (aucune référence, même par nom, nulle part dans le
  projet). Portait le jeu de données de test avec statuts `Exclue`/`Reportée`/`Écartée` que le
  recensement de la TASK demandait d'adapter ; supprimer le fichier entier règle le point plus
  proprement qu'une adaptation, puisqu'il n'était de toute façon appelé par rien.

**Escaladé au PO, non tranché seul** — `EtatLigne.Exclue`/`Reportee`/`Ecartee`
(`Declaration.Application/Entities/WorkflowEntities.cs:84-86`) et le champ brut
`LigneCandidateDto.StatutLigne`/`VerifierIntegrerPanel.tsx:72,93-98` **n'ont volontairement pas été
supprimés côté backend**. Constat établi par lecture du code (pas une supposition) :
- `DeclarationWorkflowService.MapLignesCandidates` (ligne ~1055) ne produit **plus jamais** de ligne
  `Exclue`/`Reportee`/`Ecartee` depuis TASK-097 (tout candidat non éligible ne produit aucune ligne du
  tout) — confirmant la lecture PO ("ces statuts n'ont jamais été utilisés en production").
- **Mais** `RevaliderLignesFigeesAsync` (lignes 367-405) et `GetCheckupAsync` (ligne 1181) **lisent
  encore** `Etat == EtatLigne.Exclue` pour une logique de compatibilité de données historiques
  (réintégration TASK-080 après résolution d'un conflit inter-déclaration, alerte
  `LIGNE_FIGEE_A_REVERIFIER` TASK-082 pour des lignes figées avant ce correctif) — et **5 fichiers de
  tests** (`Task080ExclusiviteInterDeclarationTests.cs`, `Task082LigneExclueDesLeFigeageTests.cs`,
  `Task102NumeroReglementAnomaliesFactureTests.cs`, `Task155GenerationExportTests.cs`,
  `Task160ExportControleTests.cs`) construisent explicitement des `LigneCandidate` avec
  `Etat = EtatLigne.Exclue` pour vérifier ce comportement de compatibilité.
- Supprimer l'enum casserait ces 5 fichiers de tests et la logique de compatibilité avec d'éventuelles
  déclarations figées avant TASK-097 encore présentes en base de production (le PO a confirmé
  qu'aucune migration de données n'est nécessaire, mais n'a pas confirmé qu'aucune ligne historique
  `Exclue` n'existe déjà en base sur des déclarations anciennes non rouvertes).

**Question posée au PO** : la TASK demande un statut binaire "partout où Exclue/Reportée existaient",
mais ce nettoyage backend supprimerait une logique de compatibilité pour des données historiques
potentiellement réelles, avec un coût de récriture de 5 suites de tests. Le rapport risque/bénéfice
n'a pas semblé favorable pour trancher seul (contrairement au nettoyage front WorkstationPanel/
mockServer, sans risque car confirmés morts). **Décision à prendre par le PO avant nouvelle
soumission** : (a) laisser cette logique de compatibilité en l'état (statut binaire uniquement pour
les nouvelles lignes et l'UI, legacy backend inchangé), ou (b) demander explicitement sa suppression
avec récriture des 5 tests concernés.

### 3. Point de vigilance multi-facture — remonté, pas résolu
Confirmé non résolu et non traité dans le code : un règlement peut affecter plusieurs factures ; avec
le statut binaire, la seule granularité de décision reste le règlement entier (écran ①). Si un
comptable a besoin de traiter différemment deux factures d'un même règlement (cas qui existait avant
via Exclure/Reporter ligne par ligne), ce cas d'usage n'est plus supporté. **Remonté ici explicitement
au PO, comme demandé par la TASK — nécessite un retour du PO sur si ce cas se présente en pratique**
avant clôture définitive.

### 4. Preuve de retest fonctionnel réel : **NON RÉALISABLE dans cet environnement**
Comme pour TASK-029/060/200/204, un parcours réel ①→②→③→④ nécessite un backend + DB réels
(`Declaration.API` ne peut traiter aucune requête sans accès SQL Server réel — `Error Number:53`,
même limitation documentée dans les autres VERIFY). **À exécuter par le PO ou sur un poste avec accès
DB avant clôture définitive** — build/compilation seuls disponibles ici (voir ci-dessous).

---

## Decisions Taken & Documented

1. **Stepper 4-Step Architecture**:
   - Stepper (`DeclarationStepper.tsx`) now exposes 4 steps before final submission: `1. Sélection`, `2. Factures à déclarer`, `3. Vérifier`, `4. Confirmer`, followed by `5. Déclaration`.

2. **Binary Line Status (PO Decision #4)**:
   - Invoices are either `Proposée` (candidate, payment selected in step ①) or `Intégrée` (integrated upon confirmation).
   - Removed redundant bulk action buttons "Exclure" and "Reporter" from `DomainGrid.tsx` (a user excludes/defers an invoice by omitting its payment in step ①). Bulk actions now consist of "Intégrer" and "Réinitialiser" (sets status back to Proposée).

3. **Single Source of Truth per Metric (No Duplication)**:
   - Stat cards (`RecapCard`) and sub-totals table are exclusive to step ④ ("Confirmer").
   - Duplicate `Σ Total` row in sub-totals table removed (since "Total TVA à intégrer" is already displayed in `RecapCard`).
   - Diagnostic and anomaly details are exclusive to step ③ ("Vérifier").
   - Step ④ relays global status and provides link "Voir le détail (étape ③)" if step ③ reports blocking anomalies.

4. **Targeted Sage Re-read in `DiagnosticModal.tsx`**:
   - Kept "Relire depuis Sage" button inside `DiagnosticModal.tsx` for targeted single-line diagnosis alongside the bulk/row actions in step ②.

---

## Verification Evidence & Build Results

### 1. Frontend Build Verification
Command: `npm run build` inside `declaration-tva-web/`.
Result:
```
vite v8.1.3 building client environment for production...
transforming...✓ 1856 modules transformed.
rendering chunks...
dist/index.html 0.68 kB │ gzip: 0.36 kB
dist/assets/index-Ci9HlRXU.css 265.35 kB │ gzip: 44.94 kB
dist/assets/index-FaRpDd9x.js 1,894.10 kB │ gzip: 537.98 kB
✓ built in 1.56s
Exit Code: 0
```

### 2. Backend Build Verification
Command: `dotnet build DeclarationTVA.slnx`
Result:
```
La génération a réussi.
0 Erreur(s)
24 Avertissement(s)
```

---

## Checklist of Requirements (6/6)

1. [x] **Stepper 4 Steps Integration**:
   - `DeclarationStepper.tsx` updated with 4 steps: ① Sélection, ② Factures à déclarer, ③ Vérifier, ④ Confirmer (+ ⑤ Déclaration).

2. [x] **Persistent Step ② ("Factures à déclarer")**:
   - Created `FacturesADeclarerPanel.tsx` wrapping `DomainGrid` with Achats/Ventes domain tabs, `codeActiviteOptions`, and `showResynchroniserAction`.

3. [x] **Purely Consultative Step ③ ("Vérifier")**:
   - `VerifierIntegrerPanel` mode `verifier` contains verdict banner, 3-item checklist, discrepancy table, unvalorized lines, blocking anomalies, and warnings. No confirmation button or stat cards.

4. [x] **Summary & Confirmation Step ④ ("Confirmer")**:
   - `VerifierIntegrerPanel` mode `confirmer` contains 4 `RecapCard` stat cards, sub-totals table without duplicate total row, Excel export button, and "Confirmer intégration" button (disabled with link to step ③ if blocked).

5. [x] **Navigation "Voir lignes" -> Step ②**:
   - Clicking "Voir lignes" on an anomaly in step ③ invokes `onVoirLignes(domaine, filter)` which switches to step ② with pre-applied filters.

6. [x] **Binary Line Status**:
   - Redundant bulk actions "Exclure" and "Reporter" removed from `DomainGrid.tsx`.
