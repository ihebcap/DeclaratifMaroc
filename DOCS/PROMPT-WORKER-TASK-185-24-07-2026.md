Tu es le **Développeur & Worker** de ce projet (rôle défini dans `D:\_vibe\ARCHITECTURE.md`).
Avant toute action, lis dans l'ordre : `D:\_vibe\ARCHITECTURE.md`, `D:\_vibe\GRF\TODO.md`, puis le
fichier TASK ci-dessous en entier.

## Contexte

TASK-184 (déjà livrée et approuvée, commit `1b2b896`) a ajouté une colonne « Domaine Activité »
(Achats/Ventes) aux tableaux « Totaux par taux » de la feuille « Détail TVA » — mauvaise
interprétation d'une demande PO qui voulait en réalité une simple ligne de total par bloc (capture
d'écran fournie : « Total Collecté » sous le tableau Collecté, « Total Deductible » sous le tableau
Déductible). Cette TASK corrige ce mésentendu : retirer la colonne, ajouter les lignes de total.

## Ta mission

Traiter **TASK-185-correction-total-collecte-deductible-au-lieu-domaine.md** (dossier
`D:\_vibe\GRF\TASKS\`) — priorité MEDIUM, backend seul.

Déplace le fichier vers `IN_PROGRESS/`, implémente, compile, corrige les erreurs, puis écris
`VERIFY/TASK-185_verify.md` (même niveau de détail que `VERIFY/TASK-144_verify.md`) : Périmètre
livré, Fichiers modifiés, Checklist, section "Reste à valider" honnête.

## Règles de travail (non négociables)

- **Portée exacte** :
  1. `Declaration.Export.Excel/Exporter.cs::EcrireBlocRecapParTaux` — retirer le paramètre
     `afficherDomaineActivite` et la colonne « Domaine Activité » (introduits par TASK-184, commit
     `1b2b896`) ; revenir à 4 colonnes (Taux/Total HT/Total TVA/Total TTC).
  2. `CreerFeuilleDetailTva` — ajouter une ligne « Total Collecté » à la fin du tableau « Totaux par
     taux — Collecté » (somme HT/TVA/TTC des lignes du bloc) et « Total Deductible » à la fin du
     tableau « — Déductible ». Uniquement dans ce chemin (export de contrôle) — **ne pas** ajouter
     ces lignes dans `CreerFeuilleRecap` (export de dépôt, non demandé).
  3. `Declaration.Core/Model.cs` — supprimer `RecapParTaux.Domaine` (devenu inutile).
  4. `Declaration.Application/Services/DeclarationWorkflowService.cs` — supprimer la résolution du
     domaine (`ResoudreDomaineActiviteRecap`, le chargement de `GetReferentielCodesActiviteAsync()`
     dans `ConstruireModeleControleAsync` s'il ne sert plus qu'à ça) — ne pas laisser de code mort.
- **Exclus** : `CreerFeuilleRecap` (export dépôt) ; « Totaux par code activité » (non touché) ;
  toute autre section de la feuille.
- Adapte les tests (`Declaration.Export.Excel.Tests/ExporterTests.cs` — fixtures/assertions liées à
  `RecapParTaux.Domaine` et aux index de colonnes introduits par TASK-184 ; vérifie aussi
  `Declaration.Orchestration.Tests` si des fakes y référencent `Domaine`).
- **Preuve réelle obligatoire** : régénère un `.xlsx` de contrôle contre `GR_EMA_DISTRIBUTION` (même
  harness que TASK-180 à 184) montrant les 4 colonnes et les 2 nouvelles lignes de total.
- Build (`dotnet build`) 0 erreur avant VERIFY. Rejoue `Declaration.Export.Excel.Tests`,
  `Declaration.Core.Tests`, `Declaration.Orchestration.Tests`.
- Un seul commit, message clair, jamais `--no-verify`, **ne jamais pousser**. Ne stage que les
  fichiers de ton périmètre.
- Point ambigu réel : arrête-toi et documente dans le VERIFY plutôt que d'inventer.

## À la fin

Laisse TASK-185 dans `VERIFY/` (ne la déplace pas vers `DONE_DETAIL/` — clôture faite par
l'architecte). Résume l'état exact d'avancement.
