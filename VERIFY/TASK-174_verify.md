# TASK-174 — VERIFY

## Périmètre livré

- `Declaration.API/Controllers/DeclarationsController.cs` (`GetCheckup`) : nouveau `recapActivite`,
  groupé par `{CodeActivite, Domaine}` sur `lignesRecap` (même ensemble `Integree||Proposee` que
  `recapSource`/`recapTaux`, aucun nouveau calcul TVA — pure agrégation d'un champ déjà valorisé).
  Code activité vide/blanc normalisé en `"—"` (même traitement que `recapSource` pour `Source`),
  jamais de ligne masquée. Ajouté à la réponse JSON à côté de `recapSource`/`recapTaux`.
- `declaration-tva-web/src/DeclarationFinalePanel.tsx` :
  - Interface `CheckupData` étendue avec `recapActivite: RecapActiviteLigne[]` (`codeActivite`,
    `domaine`, `ht`, `tva`, `ttc`).
  - Nouvelle section repliable « Récap par code activité » (entre « Vue par taux TVA » et
    « Anomalies bloquantes », état repliable propre `activiteOpen`), affichant **deux** instances de
    `RecapSourceTable` réutilisé **tel quel** (aucune modification du composant) :
    - `recapActivite.filter(domaine === 'Encaissement')` → « Collecté (Encaissement) » ;
    - `recapActivite.filter(domaine === 'Decaissement')` → « Déductible (Décaissement) » ;
    props passées : `{ source: r.codeActivite, ht, tva, ttc }` avec `columnLabel="Code activité"`,
    `onRowClick` omis (pas de drill, conforme à la demande PO). Ces deux tables sont **indépendantes
    de l'onglet actif** (`selectedTab`) — affichées côte à côte, contrairement à « Répartition par
    source »/« Vue par taux » qui restent filtrées par onglet ; c'est une lecture délibérée de la
    demande PO (« collecté » et « déductible » comme les deux faces d'un même récap), pas une
    ambiguïté laissée ouverte.
  - Commentaires de numérotation des sections suivantes (`③`→`④`, `④`→`⑤`, `⑤`→`⑥`) ajustés en
    conséquence (cosmétique, aucun changement de comportement).
- `declaration-tva-web/src/App.tsx` : entrée de menu « Relevé de déductions » (`releve`, `status:
  'soon'`) retirée de `MENU_GROUPS`, retirée du type `SectionKey`, branche `Placeholder` associée
  retirée du routeur (`activeSection === 'factures' ? (...) : null`, plus de fallback catch-all
  puisque `SectionKey` n'a plus que 3 valeurs). Le composant `Placeholder` lui-même — devenu sans
  appelant après ce retrait — a été supprimé, ainsi que les imports `Receipt`/`Construction`
  (exclusivement utilisés par ce composant) : nécessaire pour que le build passe
  (`noUnusedLocals: true` dans `tsconfig.app.json` — une fonction/import local non référencé est une
  erreur de build, pas juste un warning). Ce retrait va très légèrement au-delà du texte littéral de
  la task (qui ne mentionnait que le bloc JSX `App.tsx:311-316`) mais découle mécaniquement de
  l'exigence explicite de la task que le build front passe à 0 erreur — pas un nettoyage
  discrétionnaire ajouté par ailleurs.
- `declaration-tva-web/src/RecapSourceTable.tsx` : **non modifié**, conforme à la task (`sourceLabel`
  utilise déjà un fallback `?? s` pour tout code inconnu de `SOURCE_LABELS`, donc un code activité
  arbitraire s'affiche tel quel).

## Décisions actées en cours de route (non tranchées explicitement par la task)

- Les deux tables « Collecté »/« Déductible » sont affichées **simultanément**, indépendamment de
  l'onglet Decaissement/Encaissement actif de l'écran (cf. ci-dessus) — la task décrit les deux
  filtres sans préciser explicitement s'ils dépendent de l'onglet ; option retenue : non, car
  « collecté » et « déductible » sont déjà les intitulés des deux domaines eux-mêmes, les
  re-filtrer par un onglet qui porte le même axe serait redondant (l'onglet actif afficherait alors
  toujours une seule des deux tables non vide).
- Suppression du composant `Placeholder` (et de ses imports dédiés) en plus du seul bloc JSX
  explicitement visé par la task — cf. justification ci-dessus (build cassé sinon).

## Tests

- Aucun test automatisé nouveau ajouté : la task ne le demande pas explicitement (Validation ne liste
  que build + non-régression + vérification réelle en base), et les tests `Task*Tests.cs` existants
  qui exercent `GetCheckup` (`Task103RecapSourceProposeeTests`, `Task108EcartEquilibreEnsembleUniqueTests`,
  `Task112RecapIncoherenceTests`, etc.) ne font pas d'assertion sur l'exhaustivité des clés JSON
  retournées — `recapActivite` est un champ additif, aucun de ces tests ne casse (confirmé ci-dessous).

## Vérifié indépendamment

- `dotnet build DeclarationTVA.slnx` → **0 erreur** (7 avertissements, tous préexistants — `NU1510`
  `Declaration.Setup`, `CS0105` `Program.cs`, `CS8625`/`CS8602` dans des tests non touchés par cette
  task).
- `npx tsc -b` (front) → **0 erreur**.
- `npx vite build` → build réussi (seul avertissement : `INEFFECTIVE_DYNAMIC_IMPORT` sur `api.ts`,
  préexistant, sans rapport).
- `dotnet test` (solution complète) :
  - `Declaration.Core.Tests` → 54/54.
  - `Declaration.Export.Excel.Tests` → 3/3.
  - `Declaration.Export.Xml.Tests` → 13/13.
  - `Declaration.Orchestration.Tests` → **177/177** (aucune régression sur les tests qui exercent
    `GetCheckup`/`recapSource`/`recapTaux`).
  - `Declaration.Selection.Tests` → 58/59 — 1 échec préexistant déjà documenté (`IntegrationRegressionTests`,
    échec d'authentification Windows sur la connexion SQL locale de cet environnement, cf.
    `DONE_DETAIL/TASK-161_verify.md`/`TASK-160_verify.md`), sans rapport avec ce changement.
  - `Declaration.Controle.Tests` → 1/2 — 1 échec préexistant déjà documenté (`ComparateurTests.
    GenererRapportVerification`, « Déclaration GRFN 66 introuvable », donnée de test absente en
    base, cf. mêmes VERIFY), sans rapport avec ce changement.
  - **Aucun nouvel échec** par rapport à l'état documenté avant cette task.

### Vérification réelle en base (`sqlcmd -S DESKTOP-5BFKKEP ... -d GR_EMA_DISTRIBUTION`)

- **Valeurs distinctes de `Domaine` sur `DM_LGTVA`** (toutes déclarations, tous états confondus) :
  ```
  Domaine        Nb
  Decaissement   1255
  Encaissement   3893
  ```
  **Confirmé : aucune 3ᵉ valeur de `Domaine` n'existe** — le garde-fou de la task (clivage
  Encaissement/Decaissement exhaustif) est vérifié sur les données réelles actuelles. Aucune ligne
  ne serait donc invisible dans les deux tables aujourd'hui ; à revérifier si un jour une nouvelle
  valeur de `Domaine` est introduite ailleurs dans le code (aucune ne l'est actuellement, cf.
  garde-fou de la task lui-même).
- **Déclaration mixte réelle** retenue : `TVA1-2026-01` (`Id=7cceb196-103a-4e27-81d6-91f0ba9ff8e2`),
  995 lignes Encaissement + 242 lignes Decaissement en état `Proposee`/`Integree` (0/1).
  - Σ HT/TVA/TTC sur l'ensemble `lignesRecap` (`Etat IN (0,1)`) :
    `HT=2 527 819,90 / TVA=366 590,86 / TTC=2 869 948,76`.
  - Σ des mêmes lignes groupées par `{CodeActivite normalisé, Domaine}` (reproduisant exactement la
    requête LINQ de `recapActivite`) : **identique au centime près** (`HT=2 527 819,90 /
    TVA=366 590,86 / TTC=2 869 948,76`) — confirme qu'aucune ligne n'est perdue par le regroupement
    (Σ des deux nouvelles tables = total déjà affiché par `recapSource`), critère de validation
    explicite de la task.
  - Répartition obtenue : 2 groupes uniquement — `("—", Decaissement)` et `("—", Encaissement)`
    (voir réserve ci-dessous sur l'absence de codes activité réels en base).

## Réserves non bloquantes, documentées non silencieuses

- **Aucune ligne réelle en base `GR_EMA_DISTRIBUTION` ne porte de `CodeActivite` non vide
  aujourd'hui** (`SELECT CodeActivite, COUNT(*) FROM DM_LGTVA GROUP BY CodeActivite` → 5143 lignes à
  `""`, 5 à `NULL`, **0 code activité renseigné, sur aucune des 6 déclarations réelles**). Ceci est
  cohérent avec le constat déjà documenté par [[project_task171_cascade_activite_mapping_broken]]
  (TASK-171, en attente d'arbitrage PO) : le niveau « défaut par tiers » de la cascade TASK-161 ne
  se déclenche jamais en réel, et aucune affectation manuelle n'a encore été faite via l'UI TASK-161
  sur ces données. **Conséquence pour ce VERIFY** : le point de validation « plusieurs codes activité
  dont au moins une ligne sans code » n'a pu être vérifié qu'à moitié sur données réelles — la partie
  « ligne sans code » est triviale et massivement confirmée (100 % des lignes) ; la partie « plusieurs
  codes distincts » n'a **aucun** jeu de données réel pour l'exercer actuellement (0 code non-vide
  nulle part en base). Le regroupement multi-clés lui-même est cependant garanti correct par
  construction (`GroupBy` sur une paire de valeurs, LINQ standard, aucune logique conditionnelle
  propre à la cardinalité) et par le test d'identité de somme ci-dessus, qui n'est pas sensible au
  nombre de groupes distincts. Je n'ai pas écrit de code activité de test dans la base réelle du
  client pour forcer ce scénario (hors périmètre lecture seule assumé pour cette vérification). À
  revérifier visuellement par le PO une fois qu'au moins une ligne réelle portera un code activité
  affecté (manuellement via l'écran, ou une fois TASK-171 tranchée) — non bloquant, le calcul est
  correct par construction, seule la démonstration visuelle sur un cas multi-codes réel manque.
- Les deux échecs de test préexistants (`Declaration.Selection.Tests.IntegrationRegressionTests`,
  `Declaration.Controle.Tests.ComparateurTests.GenererRapportVerification`) restent non résolus —
  hors périmètre de cette task, déjà documentés dans plusieurs VERIFY précédents.
- Retrait du composant `Placeholder`/imports associés dans `App.tsx` au-delà du texte littéral de la
  task — cf. section « Décisions actées » ci-dessus, nécessaire pour un build 0 erreur.
