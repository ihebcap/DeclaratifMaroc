Tu es le **Développeur & Worker** de ce projet (rôle défini dans `D:\_vibe\ARCHITECTURE.md`).
Avant toute action, lis dans l'ordre : `D:\_vibe\ARCHITECTURE.md`, `D:\_vibe\GRF\TODO.md`, puis le
fichier TASK ci-dessous en entier. Ne suppose jamais un contexte manquant.

## Contexte

Retour PO (24/07/2026) sur une capture d'écran de la feuille « Détail TVA » de l'export Excel de
contrôle : les totaux « par taux » et « par code activité » sont regroupés (pas de distinction
Collecté/Déductible), les colonnes de date affichent une heure, et la date de rapprochement d'un
règlement est enfouie dans le texte du statut (« Rapproché (30/01/2026) ») au lieu d'une colonne
séparée. Diagnostic architecte confirmé par lecture de code — voir le bloc TODO.md (section 🆕 Export
Excel « Détail TVA ») et le détail complet dans la TASK ci-dessous.

**Une 4ᵉ demande du PO** (numéro de règlement dans la liste des factures) est **déjà tracée séparément**
sous TASK-162 (`TASKS/TASK-162-export-excel-ajout-numero-reglement.md`) — hors périmètre de ce prompt,
optionnelle en fin de session si le temps le permet (voir §Ta mission).

## Ta mission

Traiter **TASK-180-export-excel-detail-collecte-deductible-format-dates-rapprochement.md** (dossier
`D:\_vibe\GRF\TASKS\`) — priorité MEDIUM, aucune dépendance bloquante.

Vérifie d'abord que les champs Objectif/Périmètre/Livrables/Critères de validation sont bien remplis
(ils le sont). Déplace le fichier de `TASKS/` vers `IN_PROGRESS/`, implémente, compile, corrige les
erreurs, puis écris `VERIFY/TASK-180_verify.md` en suivant le même niveau de détail que
`VERIFY/TASK-144_verify.md` (sers-t'en de modèle) : section Périmètre livré, Fichiers modifiés,
Checklist, **et une section "Reste à valider" honnête si tout n'a pas pu être vérifié** — ne déclare
jamais un point validé si tu ne l'as pas réellement vérifié.

Si tu as le temps une fois TASK-180 terminée et vérifiée, traite aussi TASK-162 (même fichier
`Exporter.cs`, section disjointe — colonne « N° Règlement » dans `CreerFeuilleDetail`/
`CreerFeuilleFacturesControle`) en **commit séparé**. Ne la commence pas avant d'avoir un VERIFY propre
pour TASK-180.

## Règles de travail (non négociables)

- **Portée exacte** (§Périmètre STRICT de la TASK) : `Declaration.Core/Model.cs`
  (`RecapParTaux`/`RecapParActivite`/`ReglementSelectionneInfo`),
  `Declaration.Core/ConstructeurDeclaration.cs`, `Declaration.Application/Services/
  DeclarationWorkflowService.cs` (`ConstruireModeleControleAsync` uniquement), `Declaration.Export.Excel/
  Exporter.cs` (`CreerFeuilleRecap`, `CreerFeuilleDetailTva`, `CreerFeuilleReglementsSelectionnes`, +
  format date dans `CreerFeuilleDetail`/`CreerFeuilleFacturesControle`). Ne touche à aucun autre fichier,
  pas de refactor, pas de nettoyage non demandé.
- **Point 1 (Collecté/Déductible)** : le clivage se fait sur `LigneDeclarationEnrichie.Source`
  (`Source == SourceAffectation.Encaissement` ⇒ Collecté, sinon ⇒ Déductible) — donnée déjà présente sur
  chaque ligne dans les deux chemins de calcul, **aucune nouvelle lecture base, aucun recalcul TVA**.
  Le choix de rendu (deux tableaux séparés vs colonne « Sens » unique) t'est laissé — la TASK recommande
  deux tableaux séparés par cohérence avec TASK-174 côté front, mais documente ton choix dans le VERIFY,
  ne l'applique pas silencieusement. Vérifie que Collecté + Déductible = ancien total non scindé, sur les
  deux exports (dépôt ET contrôle).
- **Point 2 (dates sans heure)** : applique un `NumberFormat`/`DateFormat` explicite date-seule
  (`"dd/mm/yyyy"`) sur **toutes** les cellules date écrites par `Exporter.cs`, pas seulement celle citée
  par le PO — même défaut structurel sur `DatePaiement`, `DateFacture` (2 méthodes) et `Date` (règlements
  sélectionnés). Vérifie sur un cas réel en base si `AF_Date`/`DO_Date` portent effectivement une heure
  non nulle ; si aucun cas réel trouvé, dis-le explicitement dans le VERIFY (ne fabrique pas une preuve).
- **Point 3 (date de rapprochement séparée)** : `EtatPointage` ne doit plus jamais contenir de date
  concaténée (`"Rapproché"`/`"Non rapproché"` uniquement) ; nouvelle colonne « Date rapprochement »
  peuplée depuis `ReglementRapprochement.DateRapprochement`, vide si non rapproché. Adapte les 2 tests
  existants qui vérifient `EtatPointage` par égalité de chaîne exacte
  (`Declaration.Orchestration.Tests/Task160ExportControleTests.cs:170` et `:189`).
- **Tu as un accès réel à la base de données** : `sqlcmd -S DESKTOP-5BFKKEP -U sa -P 1234 -C -d
  GR_EMA_DISTRIBUTION -Q "..."` (identifiants complets dans `D:\_vibe\GRF\connections.json`). Utilise-le
  pour le point 2 (vérifier une valeur `AF_Date`/`DO_Date` réelle) et pour générer un export réel sur une
  déclaration existante afin de produire le `.xlsx` de preuve du VERIFY.
- Build back (`dotnet build DeclarationTVA.slnx`) ET tests (`Declaration.Core.Tests`,
  `Declaration.Orchestration.Tests`, `Declaration.Export.Excel.Tests`) doivent passer avant d'écrire un
  VERIFY.
- Committe TASK-180 en un seul commit, message clair, jamais `--no-verify`, **ne jamais pousser
  (`git push`)** — le commit reste local pour revue. Si tu traites aussi TASK-162, commit strictement
  séparé.
- Respecte les règles universelles de `ARCHITECTURE.md` §5 (pas de SQL inline hors couche repository,
  pas de secret codé en dur, pas de bypass sécurité, pas de dette technique silencieuse — documente tout
  compromis dans le VERIFY).
- Si un point est réellement ambigu (notamment le rendu du point 1, ou un cas de `Source` inattendu qui
  ne rentre proprement dans aucun des deux sens) : **arrête-toi sur ce point précis, documente-le
  clairement dans le VERIFY** plutôt que d'inventer une réponse, et continue le reste de la TASK.

## À la fin

Laisse TASK-180 (et TASK-162 si traitée) dans `VERIFY/` (ne les déplace pas toi-même vers
`DONE_DETAIL/` — c'est le rôle de l'architecte de les reviewer et de faire la clôture). Indique dans un
résumé final l'état exact d'avancement de chaque point (1 à 3, + TASK-162 si tentée).
