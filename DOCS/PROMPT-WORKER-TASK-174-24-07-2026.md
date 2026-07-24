Tu es le **Développeur & Worker** de ce projet (rôle défini dans `D:\_vibe\CLAUDE.md`).
Avant toute action, lis dans l'ordre : `D:\_vibe\CLAUDE.md`, `D:\_vibe\GRF\TODO.md`, puis le fichier
TASK ci-dessous en entier. Ne suppose jamais un contexte manquant.

## Ta mission

Traiter une seule task : `D:\_vibe\GRF\TASKS\TASK-174-recap-collecte-deductible-par-code-activite.md`
— lis-la **intégralement** avant de commencer, elle contient tout le contexte (constat de code précis,
lignes exactes), le périmètre et les critères de validation.

**Cette task est indépendante de TASK-172 et TASK-173** (confirmé PO) et peut être traitée **en
parallèle** même si ces deux tasks sont encore en cours/en VERIFY. Si `TASK-172`/`TASK-173` sont déjà
déplacées dans `IN_PROGRESS/` au moment où tu commences : **ne touche à aucun de leurs fichiers en
cours de modification** en dehors de ce que TASK-174 demande explicitement (elle ne touche que la
méthode `GetCheckup` de `DeclarationsController.cs` — jamais `GET/PATCH .../code-activite` ni
`POST {id}/lignes:bulk`, qui appartiennent à 172/173).

## Ce que cette task NE demande PAS (ne pas réinventer)

1. **Aucun nouveau calcul de TVA** — `RecapParActivite` est déjà calculé par
   `ConstructeurDeclaration.cs:188` et déjà exporté en Excel. Tu ajoutes uniquement une projection en
   lecture sur des données déjà valorisées.
2. **Aucune modification du référentiel `P_DECTVAACTIVITE`** ni de la logique de résolution du code
   activité (TASK-161/171) — tu lis le champ `CodeActivite` déjà présent sur chaque ligne, tel quel.
3. **Aucun drill/clic** sur les nouvelles tables (`onRowClick` omis) — affichage passif uniquement,
   conforme à la demande PO (« c'est tout »).
4. Le composant `RecapSourceTable.tsx` **ne se modifie pas** — il est déjà générique (`columnLabel`),
   tu l'instancies deux fois avec des données différentes, point.

## Règles de travail (non négociables)

- Déplace le fichier de `TASKS/` vers `IN_PROGRESS/`, implémente les 4 points de la section Objectif
  (back `recapActivite`, front 2 tables, typage `CheckupData`, retrait du menu orphelin), compile,
  corrige les erreurs, puis écris `VERIFY/TASK-174_verify.md` en suivant le même niveau de détail que
  `DONE_DETAIL/TASK-166_verify.md` ou `DONE_DETAIL/TASK-160_verify.md` (section Périmètre livré,
  Décisions actées en cours de route, Tests, Vérifié indépendamment, Réserves non bloquantes
  documentées — jamais silencieuses). Ne déclare jamais un point validé si tu ne l'as pas réellement
  vérifié.
- **Tu as un accès réel à la base de données** : `sqlcmd -S DESKTOP-5BFKKEP -U sa -P 1234 -C -d
  GR_EMA_DISTRIBUTION -Q "..."`. **Utilise-le pour vérifier réellement** — avant d'écrire le VERIFY —
  qu'aucune 3ᵉ valeur de `Domaine` n'existe sur les lignes d'une déclaration réelle au-delà de
  `Encaissement`/`Decaissement` (point de garde-fou explicite de la task : si une 3ᵉ valeur existe, les
  lignes correspondantes seraient invisibles dans les deux tables — à signaler dans le VERIFY, pas à
  masquer). Vérifie aussi sur une déclaration réelle mixte (au moins 1 ligne Encaissement + 1 ligne
  Décaissement, plusieurs codes activité dont au moins une ligne sans code) que Σ des deux nouvelles
  tables = total déjà affiché par `recapSource`.
- Build back (`dotnet build DeclarationTVA.slnx`) et build front (`cd declaration-tva-web && npx tsc -b
  && npx vite build`) doivent passer **avant** d'écrire le VERIFY. Lance aussi `dotnet test` sur la
  solution complète et compare aux échecs préexistants déjà documentés (cf. `DONE_DETAIL/TASK-160_verify.md`,
  `Declaration.Selection.Tests` 58/59 échec préexistant sans rapport) — tout nouvel échec doit être
  expliqué, jamais ignoré.
- Périmètre repository strict : uniquement `D:\_vibe\GRF`, ne touche pas `apbs-gr_winform`.
- Ne touche à aucun fichier hors de la section `Files` de la task
  (`DeclarationsController.cs` méthode `GetCheckup` uniquement, `DeclarationFinalePanel.tsx`,
  `RecapSourceTable.tsx` en lecture seule/non modifié, `App.tsx`). Pas de refactor, pas de nettoyage non
  demandé au-delà du retrait explicite de l'entrée « Relevé de déductions ».
- Committe le travail **en un seul commit** à la fin (message clair décrivant l'ajout), jamais de
  `--no-verify`, **ne jamais pousser (`git push`)** — le commit reste local pour revue.
- Si un point est réellement ambigu (ex. une 3ᵉ valeur de `Domaine` découverte en base) : **arrête-toi
  sur ce point précis, documente-le clairement dans le VERIFY** plutôt que d'inventer un comportement.

## À la fin

Laisse le VERIFY dans `VERIFY/TASK-174_verify.md` (ne le déplace pas toi-même vers `DONE_DETAIL/` —
c'est le rôle de l'architecte de le reviewer et de faire la clôture). Termine par un résumé indiquant
clairement : build back/front OK ou KO, résultat de la vérification réelle en base (valeurs distinctes
de `Domaine` trouvées), et tout point laissé ouvert pour la revue architecte.
