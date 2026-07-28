# TASK-136 Verify — Menu : nouvelle entrée « Délai de paiement »

> Implémentation worker. Câblage de navigation uniquement (`App.tsx`) : aucune règle métier
> ajoutée, aucun écran TASK-130/134 modifié. Build back `dotnet build DeclarationTVA.slnx` :
> **0 erreur** (24 warnings, tous préexistants — aucun fichier non touché par cette TASK). Build
> front `npx tsc -b` + `npx vite build` : **0 erreur**. `npx oxlint` : **0 erreur** (seul warning
> sur le fichier neuf est `only-export-components` sur `task136-harness.tsx`, identique à
> task130/134/138/139-harness). Test e2e Playwright **task136.spec.ts : 1/1 vert**, rejoué avec
> task130.spec.ts + task134.spec.ts (8/8 verts au total) pour confirmer l'absence de régression.

---

## 1. Périmètre livré

### 1.1 Décision PO reprise telle quelle (19/07/2026)

Entrée de menu **autonome** « Délai de paiement », groupe DÉCLARATION, **au même niveau** que
« Déclaration TVA » (pas un regroupement sous une entrée existante) — cf. `IN_PROGRESS/DDP-TASK-
136-menu-entree-delai-de-paiement.md` § Objectif. Contient une **sous-navigation interne** (3
onglets) vers :
1. **Déclarations** — `DeclarationsDelaiPaiementPanel` (TASK-134, écrans 1+2+3 : liste, fiche,
   popup de sélection).
2. **Sélection / Contrôle** — `ControleLignesDelaiPaiementPanel` (TASK-134, écran 4 : visibilité/
   reporting pur).
3. **Conventions** — `ConventionsDelaiPaiementPanel` (TASK-130).

Rapprochement bancaire et Factures **restent inchangés** sous INTERROGATION ; Déclaration TVA
reste inchangée sous DÉCLARATION (aucun champ, aucune requête, aucun style de ces 3 écrans
existants n'a été touché).

### 1.2 Implémentation — `App.tsx`

- `SectionKey` étendu d'une valeur `'delai-paiement'` (les 3 clés existantes `rapprochement` /
  `factures` / `declaration` sont inchangées).
- Nouveau type `DdpSousEcran = 'declarations' | 'controle' | 'conventions'` — **interne à la
  section**, jamais une `SectionKey` : le sidebar n'affiche qu'**une seule** entrée « Délai de
  paiement » (conforme à la décision PO « pas un regroupement », mais aussi pas 3 lignes de
  sidebar — la sous-navigation vit dans le contenu, pas dans le menu latéral).
- Nouvelle entrée ajoutée à `MENU_GROUPS['DÉCLARATION']` : `{ key: 'delai-paiement', label:
  'Délai de paiement', icon: CalendarClock, status: 'live' }` — icône `CalendarClock`
  (`lucide-react`), déjà utilisée par `ConventionsDelaiPaiementPanel.tsx` (TASK-130) pour ce
  domaine, cohérence visuelle directe.
- `Dashboard` : nouvel état `ddpSousEcran` (défaut `'declarations'`), branche de rendu
  `activeSection === 'delai-paiement'` dans `<main>` : un bandeau de sous-navigation (3 boutons en
  contrôle segmenté, **même pattern visuel exact** que le toggle Achat/Vente déjà existant dans
  `ConventionsDelaiPaiementPanel.tsx` — couleurs `--accent-primary`/`--text-primary`, bordures,
  tailles de police identiques) suivi du panneau sélectionné, chacun recevant `societeId={
  user.societeId}` et `showToast` — **les mêmes props que reçoivent déjà tous les autres écrans**
  (`RapprochementInterrogation`, `FactureInterrogation`), aucune prop nouvelle inventée côté
  panneau.
- `Dashboard` **exportée** (`export function Dashboard`, elle ne l'était pas) — uniquement pour
  permettre au harnais de test e2e de monter le vrai shell sans rejouer le flux licence/connexion
  (voir §1.3). Aucun autre changement de signature ; `App` (export default) inchangé.
- Aucune modification de `App.css`/`index.css` : le style du bandeau de sous-navigation est en
  ligne (`style={{...}}`), reprenant exactement les valeurs déjà utilisées ailleurs — pas de
  nouvelle règle CSS introduite, pas de risque de collision avec le style existant des autres
  écrans.

### 1.3 Test e2e — `tests/task136.spec.ts` + harnais `task136-harness.tsx`/`task136.html`

Même pattern que task130/134 (harnais Vite dev uniquement, absent du build de prod — `vite build`
n'a qu'`index.html` en entrée, vérifié §3) : le harnais rend le **VRAI** `Dashboard` (exporté
depuis `App.tsx`) avec un utilisateur fictif, contournant le flux licence/connexion (déjà couvert
par `declaration.spec.ts`, hors périmètre strict de cette TASK). `/api/**` mocké au niveau réseau
par un dispatcher par préfixe (pas besoin du backend .NET ni de la base réelle).

Un seul test, couvrant l'intégralité du critère de validation de la TASK :
1. Section par défaut inchangée : `Déclaration TVA` reste active à l'ouverture (aucune régression
   de comportement).
2. **Régression** : `Rapprochement bancaire` et `Factures` restent atteignables et deviennent bien
   la section active au clic (les 2 écrans INTERROGATION, non touchés par cette TASK).
3. Retour sur `Déclaration TVA` : re-confirmé actif (3e écran existant, non touché).
4. **Nouvelle entrée** `Délai de paiement` visible dans le sidebar, clic → devient la section
   active.
5. Sous-écran 1 par défaut : titre `Déclarations délai de paiement` visible (écran TASK-134).
6. Clic `Sélection / Contrôle` → titre `Contrôle des lignes hors délai` visible (écran TASK-134,
   écran 4).
7. Clic `Conventions` → titre `Conventions de délai de paiement` visible (écran TASK-130).
8. Retour `Déclarations` → re-confirmé (sous-navigation bidirectionnelle).
9. Régression finale : retour sur `Déclaration TVA`, le bouton `Créer une déclaration` (propre à
   cet écran, jamais touché) est bien visible.

`page.on('pageerror', ...)` fait échouer le test sur toute exception React non interceptée — le
test a d'abord échoué une fois pour cette raison exacte (`ControleLignesDelaiPaiementPanel`
attendait la forme complète de `SelectionDdpDto`, pas un tableau vide, sur son fetch automatique
au montage) : corrigé en enrichissant le mock, **aucun changement de code applicatif nécessaire**
— confirme que le composant TASK-134 n'a pas été touché, seul le mock du test était incomplet.

3 captures prises pendant le test (sous-navigation active + sidebar) :
`VERIFY/task136-1-declarations.png`, `-2-controle.png`, `-3-conventions.png`.

---

## 2. Fichiers créés

- `declaration-tva-web/src/task136-harness.tsx` — harnais e2e (hors build de prod).
- `declaration-tva-web/task136.html` — page d'entrée du harnais (servie par vite dev uniquement).
- `declaration-tva-web/tests/task136.spec.ts` — test e2e Playwright de navigation.
- `VERIFY/task136-1-declarations.png`, `VERIFY/task136-2-controle.png`,
  `VERIFY/task136-3-conventions.png` — captures Chromium des 3 sous-écrans atteints depuis le menu.

## 3. Fichiers modifiés

- `declaration-tva-web/src/App.tsx` — import de `CalendarClock` + des 3 composants TASK-130/134,
  extension de `SectionKey`, nouveau type `DdpSousEcran`, nouvelle entrée de menu, nouvel état
  `ddpSousEcran` + branche de rendu dans `Dashboard`, export de `Dashboard`. **Aucune ligne des 3
  branches existantes (`declaration`/`rapprochement`/`factures`) modifiée.**
- `TASKS/DDP-TASK-136-…md` → `IN_PROGRESS/DDP-TASK-136-…md` (déplacement demandé).

### Cohabitation avec la session parallèle (correctif TVA TASK-186→190)

Fichiers d'un **autre fil de travail**, présents dans l'arbre au démarrage de cette session et
**non touchés/non stagés** par celle-ci : `CHANGELOG.md`, `DONE.md`, `TODO.md`,
`LANCEMENT_DEV.md`, `DOCS/GUIDE_PROCESS_DECLARATION_TVA.html`, `TASKS/TASK-029-…md`,
`VERIFY/TASK-006|007|024_verify.md`, `declaration-tva-web/.env.development`,
`declaration-tva-web/vite.config.ts`, `DOCS/PROMPT-WORKER-*.md`, les renommages
`IN_PROGRESS`→`DONE_DETAIL` TASK-186→190 déjà indexés, et les captures `VERIFY/task138-*.png`/
`task139-*.png`. Vérifié par `git status --short` avant et après chaque étape, commit fait avec
des **pathspecs explicites** (jamais `git add -A`).

**Point relevé et corrigé pendant la session** : rejouer `task134.spec.ts` (pour la vérification
de non-régression §5) a régénéré `VERIFY/task134-B1-controle-periode-raisonnee.png` (capture prise
par ce test lui-même) — ce fichier appartient à TASK-134, pas à TASK-136. Restauré à son état
commité via `git checkout -- VERIFY/task134-B1-controle-periode-raisonnee.png` avant tout `git
add`, vérifié absent du `git status` final.

---

## 4. BUILD

| Commande | Résultat |
|---|---|
| `dotnet build DeclarationTVA.slnx` | **0 erreur**, 24 warnings — **tous préexistants** (`NU1510` Setup, `CS8604`/`CS8602`/`CS8618` Export.Xml/Selection, `xUnit1012`, `CS8625`/`CS8602` Orchestration.Tests, `CS0105` Program.cs) ; aucun fichier de cette solution n'a été modifié par TASK-136, ce build ne fait que confirmer l'absence d'impact |
| `npx tsc -b` (front) | **0 erreur** |
| `npx vite build` (front) | **0 erreur**, 1852 modules, `dist/index.html` seul en entrée (les harnais `task*.html` en sont absents, y compris `task136.html`) |
| `npx oxlint` | **0 erreur** ; seul nouveau warning : `only-export-components` sur `task136-harness.tsx`, même nature que task130/134/138/139-harness (fichier de harnais, pas un composant de production) |

## 5. Tests

### 5.1 e2e Playwright — `tests/task136.spec.ts` : **1/1 vert**

Décrit en détail §1.3. Rejoué avec `task130.spec.ts` (4 tests) et `task134.spec.ts` (3 tests) dans
la même commande : **8/8 verts** — confirme qu'exporter `Dashboard` et ajouter la nouvelle entrée
de menu n'a cassé aucun des deux écrans qui dépendaient déjà de TASK-136 pour être atteignables
en usage réel (ils l'étaient déjà, indépendamment, via leurs propres harnais).

### 5.2 Non-régression back

Aucun fichier `.cs` touché par cette TASK ; `dotnet build DeclarationTVA.slnx` confirmé à 0 erreur
suffit (aucune suite de tests back n'est concernée par un changement de navigation front pur).

---

## 6. Checklist

- [x] Build back `dotnet build DeclarationTVA.slnx` — **0 erreur** (24 warnings préexistants).
- [x] Build front `npx tsc -b` **et** `npx vite build` — **0 erreur**.
- [x] `npx oxlint` — **0 erreur**.
- [x] **Les 3 sous-écrans (Déclarations DDP, Sélection/Contrôle, Conventions) sont atteignables
      depuis la nouvelle entrée de menu** — prouvé par e2e (§1.3, étapes 5-8) + captures (§1.3).
- [x] **Aucune régression sur la navigation existante** (Rapprochement, Factures, Déclaration TVA)
      — prouvé par e2e (§1.3, étapes 1-3, 9) : les 3 écrans restent atteignables et affichent leur
      contenu propre sans changement.
- [x] Entrée « Délai de paiement » **au même niveau** que « Déclaration TVA » (groupe DÉCLARATION,
      pas un sous-menu d'un autre écran) — conforme à la décision PO 19/07/2026.
- [x] **Aucune modification des écrans Rapprochement/Factures/Déclaration TVA au-delà du strict
      nécessaire de navigation** : `RapprochementInterrogation.tsx`, `FactureInterrogation.tsx`,
      `DeclarationList.tsx`, `DeclarationStepper.tsx`, `CreateDeclarationModal.tsx` — **aucun de
      ces fichiers n'apparaît dans le diff de cette TASK** (seul `App.tsx` est modifié, et
      uniquement par ajouts : nouvelle entrée, nouvel état, nouvelle branche de rendu).
- [x] Aucune règle métier dupliquée : les 3 panneaux routés sont les composants TASK-130/134
      **tels quels**, reçoivent les mêmes props (`societeId`, `showToast`) que les écrans
      existants — aucune logique de sélection/cycle de vie/génération réécrite dans `App.tsx`.
- [x] Style cohérent avec l'existant : icône `lucide-react` (`CalendarClock`, déjà utilisée par
      TASK-130 pour ce domaine), contrôle segmenté de sous-navigation copiant exactement le
      pattern visuel du toggle Achat/Vente déjà en place dans
      `ConventionsDelaiPaiementPanel.tsx` — aucune nouvelle règle CSS globale ajoutée.
- [x] Test e2e Playwright de navigation demandé par la TASK — livré (`task136.spec.ts`, 1/1 vert),
      rejoué avec task130/task134 pour confirmer l'absence de régression croisée (8/8 verts).
- [x] Aucune modification de schéma, aucun fichier back touché.
- [x] Aucun fichier d'un autre fil de travail modifié ni stagé (`git status --short` relu avant et
      après ; capture `task134-B1-…png` restaurée après régénération accidentelle par le rejeu de
      `task134.spec.ts`, cf. §3).

---

## 7. Décisions worker documentées (pas d'exigence PO explicite — à confirmer)

1. **Sous-navigation interne au contenu, pas dans le sidebar.** Le texte de la TASK dit « avec
   sous-navigation interne (Déclarations / Conventions) » — interprété comme une navigation
   **interne à l'écran** (onglets dans le contenu), pas une expansion du sidebar en 3 lignes
   indentées (aucun style de sidebar imbriqué n'existe dans le dépôt, et l'ASCII-art de la TASK
   montre l'arborescence conceptuelle du domaine, pas nécessairement l'implémentation visuelle
   littérale du sidebar). Alternative rejetée : ajouter 3 `SectionKey` distinctes visibles comme
   3 lignes de sidebar sous « Délai de paiement » — rejetée car (a) aucune CSS de sidebar
   imbriqué n'existe, en ajouter aurait été une modification de `index.css` risquant d'affecter
   le rendu des groupes existants, et (b) cela aurait revenu, dans les faits, à 3 entrées de menu
   plutôt qu'« une entrée autonome » comme demandé. **À confirmer PO** si un sidebar à 2 niveaux
   est préféré à terme — le changement resterait localisé (nouvelle règle CSS + refactor de
   `MENU_GROUPS`), sans toucher aux 3 composants routés.
2. **3 sous-écrans, pas 2.** Le corps de l'étape 1 de la TASK dit « (Déclarations / Conventions) »
   mais l'ASCII-art de l'objectif et les critères de validation en listent explicitement 3
   (Déclarations, Sélection/Contrôle, Conventions) — traité comme une omission dans la phrase de
   l'étape 1 (pas une réduction de périmètre), cohérent avec le tableau `TODO.md` ligne TASK-136
   qui dépend explicitement de 130 **et** 134 (qui livre 2 écrans distincts : liste/fiche
   d'un côté, contrôle de l'autre).
3. **`Dashboard` exportée** uniquement pour les besoins du test e2e (aucun autre appelant). Choix
   fait plutôt que de dupliquer la logique du shell dans le harnais (qui aurait re-testé une
   copie, pas le vrai code) — cohérent avec le principe déjà appliqué par TASK-130/134
   (`ConventionsDelaiPaiementPanel`/`DeclarationsDelaiPaiementPanel` rendus tels quels dans leurs
   harnais respectifs).
4. **Test manuel humain dans l'application réelle : non fait.** Comme documenté par TASK-134 §10
   n°3, cette passe de recette utilisateur devient possible **maintenant** que le branchement au
   menu existe, mais reste à faire par un humain — non substituable par ce VERIFY.

---

## 8. Reste à valider (NON couvert par ce VERIFY)

1. **Recette utilisateur réelle** (mentionnée par TASK-134 §10 n°3 comme dépendant de cette TASK) :
   navigation dans l'application assemblée avec un vrai backend/base, pas seulement le harnais
   Chromium mocké. Notamment la lisibilité de l'intitulé « Sélection / Contrôle » (raccourci
   choisi pour l'onglet — le CDC/TASK-134 nomme l'écran « Contrôle des lignes hors délai », le nom
   affiché **dans le contenu** reste identique ; seul le libellé de l'onglet de navigation est
   raccourci pour tenir dans le bandeau).
2. **Choix « sidebar plat vs sidebar imbriqué »** (§7 n°1) — non tranché par le PO, documenté
   comme un choix de worker justifié mais réversible.
3. **Dépendance déjà signalée par TASK-134 §10 n°1** (`Program.cs` — inscription DI de
   `IDeclarationDelaiPaiementGenerationService`) : **hors périmètre strict de TASK-136**
   (`Program.cs` n'a pas été touché par cette session, cf. §3), mais reste une condition pour que
   les écrans routés ici fonctionnent réellement contre un backend démarré. Rappelé ici pour
   traçabilité, pas re-résolu.

---

## Verdict

Les **3 sous-écrans** demandés (Déclarations DDP, Sélection/Contrôle, Conventions) sont
atteignables depuis la nouvelle entrée de menu **autonome** « Délai de paiement » (groupe
DÉCLARATION, même niveau que « Déclaration TVA »), conformément à la décision PO du 19/07/2026.
Aucun des 3 écrans existants (Rapprochement, Factures, Déclaration TVA) n'est modifié au-delà du
seul ajout additif dans `App.tsx` — vérifié par diff (aucun de leurs fichiers n'apparaît dans le
changement) et par e2e (régression testée explicitement). Builds back et front à **0 erreur**,
oxlint 0 erreur, **e2e Playwright 1/1 vert** (8/8 en comptant le rejeu croisé avec task130/task134
pour la non-régression).

**Points nécessitant le PO** : (1) confirmation du choix « sous-navigation en onglets de contenu »
plutôt qu'un sidebar à 2 niveaux (§7 n°1, réversible) ; (2) la recette humaine dans l'application
réellement assemblée, désormais possible mais non faite par ce VERIFY.
