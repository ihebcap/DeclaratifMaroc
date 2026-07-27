Tu es le **Développeur & Worker** de ce projet (rôle défini dans `D:\_vibe\ARCHITECTURE.md`).
Avant toute action, lis dans l'ordre : `D:\_vibe\ARCHITECTURE.md`, `D:\_vibe\GRF\TODO.md`, puis le
fichier TASK ci-dessous en entier. Ne suppose jamais un contexte manquant.

## Contexte

Suite à la revue de TASK-180, le PO a demandé des explications sur le bloc « Contrôle d'équilibre »
de la feuille « Détail TVA » (export de contrôle). Diagnostic architecte : les valeurs affichées sont
justes, mais les libellés (hérités tels quels du chemin de dépôt légal, où ils sont corrects) sont
trompeurs dans ce chemin précis — `TotalMontantAffecte` y est en réalité un simple Total HT, et
`ResiduExplique` y est codé en dur à 0, ce qui fait que « Résidu Non TVA » n'est en réalité rien
d'autre que −Total TVA. Décision PO : ne pas recalculer, **juste corriger les libellés**, uniquement
dans ce chemin (l'export de dépôt, dont les libellés sont déjà exacts, n'est pas concerné).

## Ta mission

Traiter **TASK-182-export-excel-libelles-controle-equilibre-trompeurs.md** (dossier
`D:\_vibe\GRF\TASKS\`) — seule task de ce lot.

Déplace le fichier de `TASKS/` vers `IN_PROGRESS/`, implémente, compile, corrige les erreurs, puis
écris `VERIFY/TASK-182_verify.md` en suivant le même niveau de détail que `VERIFY/TASK-144_verify.md`
(sers-t'en de modèle) : section Périmètre livré, Fichiers modifiés, Checklist, **et une section
"Reste à valider" honnête si tout n'a pas pu être vérifié** — ne déclare jamais un point validé si tu
ne l'as pas réellement vérifié.

## Règles de travail (non négociables)

- **Portée exacte, un seul fichier de code** : `Declaration.Export.Excel/Exporter.cs::CreerFeuilleDetailTva`
  (lignes ~311 et ~317) — renommer **uniquement** 2 chaînes de libellé :
  - `"Total Montant Affecté"` → `"Total HT"`.
  - `"Résidu Non TVA"` → `"Écart HT − TTC (= −Total TVA)"` (ou libellé équivalent explicite sur le
    signe — la valeur reste négative, ne jamais inverser le signe affiché, juste nommer correctement
    ce qu'elle représente).
  - `"Total Déclaré TTC"` : **inchangé**, déjà correct.
- **Ne touche à aucun autre fichier ni aucune autre feuille** :
  - Pas `CreerFeuilleRecap` (export de dépôt) — ses libellés et ses valeurs sont déjà corrects, ne pas
    y toucher.
  - Aucun changement de `ControleEquilibre`/`ConstruireModeleControleAsync`/
    `ConstructeurDeclaration.cs` — aucun recalcul, décision PO explicite (changement de texte pur
    uniquement).
  - Aucune modification du front.
- Mets à jour les éventuelles assertions de tests qui vérifieraient ces libellés par valeur exacte de
  chaîne (`Declaration.Export.Excel.Tests/ExporterTests.cs`,
  `Declaration.Orchestration.Tests/Task160ExportControleTests.cs` si applicable).
- Build back (`dotnet build DeclarationTVA.slnx`) ET les suites de tests concernées
  (`Declaration.Export.Excel.Tests`, `Declaration.Orchestration.Tests`) doivent passer avant d'écrire
  un VERIFY. Rejoue si possible la suite complète pour t'assurer d'aucune régression ailleurs.
- Régénère un `.xlsx` de contrôle réel (même méthode que TASK-180/162 : harness contre
  `GR_EMA_DISTRIBUTION`) montrant les nouveaux libellés, et joins-le (ou un extrait suffisant) au
  VERIFY.
- Committe le correctif **en un seul commit** pour cette TASK, message clair, jamais `--no-verify`,
  **ne jamais pousser (`git push`)** — le commit reste local pour revue.
- Respecte les règles universelles de `ARCHITECTURE.md` §5 (pas de SQL inline hors repository, pas de
  secret codé en dur, pas de bypass sécurité, pas de dette technique silencieuse — documente tout
  compromis dans le VERIFY).
- Si un point est réellement ambigu : **arrête-toi sur ce point précis, documente-le clairement dans
  le VERIFY** plutôt que d'inventer une réponse.

## À la fin

Laisse la TASK traitée dans `VERIFY/` (ne la déplace pas toi-même vers `DONE_DETAIL/` — c'est le rôle
de l'architecte de la reviewer et de faire la clôture). Si tu n'as pas eu le temps de la terminer,
indique dans un résumé final l'état exact d'avancement et ce qu'il reste à faire.
