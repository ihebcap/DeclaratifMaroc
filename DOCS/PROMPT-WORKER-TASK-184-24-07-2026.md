Tu es le **Développeur & Worker** de ce projet (rôle défini dans `D:\_vibe\ARCHITECTURE.md`).
Avant toute action, lis dans l'ordre : `D:\_vibe\ARCHITECTURE.md`, `D:\_vibe\GRF\TODO.md`, puis le
fichier TASK ci-dessous en entier. Ne suppose jamais un contexte manquant.

## Contexte

Le bloc « Contrôle d'équilibre » (feuille « Détail TVA » de l'export de contrôle) affiche
`Total HT − Total Déclaré TTC`. Or HT + TVA = TTC par construction, donc cet écart vaut
**systématiquement** exactement `−ΣTVA`, quelle que soit la qualité réelle des données — ce n'est
pas un contrôle, ça ne peut jamais rien détecter. Décision PO (24/07/2026) : retirer ce bloc, et
ajouter à la place, dans les tableaux « Totaux par taux — Collecté »/« — Déductible » déjà existants,
une colonne « Domaine Activité » (Achats/Ventes) — donnée déjà disponible dans le référentiel
`P_DECTVAACTIVITE`, aucune nouvelle requête SQL nécessaire.

## Ta mission

Traiter **TASK-184-controle-equilibre-suppression-et-domaine-totaux-taux.md** (dossier
`D:\_vibe\GRF\TASKS\`) — priorité MEDIUM, backend seul (`Declaration.Core`, `Declaration.Application`,
`Declaration.Export.Excel`), aucun changement front.

Déplace le fichier de `TASKS/` vers `IN_PROGRESS/`, implémente, compile, corrige les erreurs, puis
écris `VERIFY/TASK-184_verify.md` en suivant le même niveau de détail que `VERIFY/TASK-144_verify.md`
(sers-t'en de modèle) : section Périmètre livré, Fichiers modifiés, Checklist, **et une section
"Reste à valider" honnête si tout n'a pas pu être vérifié** — ne déclare jamais un point validé si tu
ne l'as pas réellement vérifié.

## Règles de travail (non négociables)

- **Portée exacte** :
  1. `Declaration.Export.Excel/Exporter.cs::CreerFeuilleDetailTva` — supprimer les lignes ~308-323
     (bloc « Contrôle d'équilibre » : titre + 3 lignes de valeurs). Vérifie les numéros réels avant
     d'éditer, le fichier a pu bouger depuis la rédaction de la TASK.
  2. `Declaration.Core/Model.cs` — ajouter un champ domaine à `RecapParTaux` (ex. `string? Domaine`,
     valeurs attendues : `"Achats"`, `"Ventes"`, ou un bucket explicite type `"Non résolu"` pour les
     lignes dont le `CodeActivite` n'a pas été résolu — ne jamais masquer ce cas, cf. décision PO déjà
     prise en TASK-161 pour `RecapParActivite`).
  3. `Declaration.Application/Services/DeclarationWorkflowService.cs::ConstruireModeleControleAsync`
     (~lignes 1392-1403) — résoudre ce domaine par `CodeActivite` en t'appuyant sur le référentiel déjà
     exposé par `DeclarationRepository.GetReferentielCodesActiviteAsync()` (retourne
     `Code`/`Libelle`/`Domaine` pour tous les codes ; `Domaine` y est l'entier `DTA_Domaine` 1/2 —
     reporte-toi à `DomaineVersDtaDomaine`/`GetDomaineCodeActiviteAsync` dans
     `DeclarationRepository.cs` pour le mapping 1=Encaissement/Ventes, 2=Decaissement/Achats). Charge
     le référentiel **une seule fois** (dictionnaire `Code → Domaine`), inclus ce domaine dans le
     `GroupBy` qui construit `modele.RecapsParTaux`. **Aucune nouvelle requête SQL/jointure
     `RT_MOUVEMENT` n'est nécessaire** — si tu penses en avoir besoin, arrête-toi et documente pourquoi
     dans le VERIFY plutôt que d'étendre le périmètre.
  4. `Exporter.cs::EcrireBlocRecapParTaux` — ajouter la colonne « Domaine Activité » à l'affichage des
     tableaux « Totaux par taux — Collecté »/« — Déductible ».
- **Exclus** : `CreerFeuilleRecap` (export de dépôt, bloc `ControleEquilibre` correct, ne pas y
  toucher) ; classe `ControleEquilibre` (ne pas la supprimer, toujours utilisée côté dépôt) ; « Totaux
  par code activité » (non demandé) ; tout recalcul basé sur les montants réels Sage
  (`MontantAffecte`/OM) — hors sujet ici, aucune décision PO en ce sens.
- **Invariant de non-régression** : pour chaque taux, la somme des sous-totaux par domaine
  (Achats+Ventes+Non résolu) doit être strictement égale au total par taux déjà affiché avant cette
  TASK — vérifie-le explicitement (calcul ou test) avant de conclure.
- Adapte les tests impactés (`Declaration.Export.Excel.Tests/ExporterTests.cs` — fixtures
  `ControleEquilibre`/`RecapParTaux`, assertions sur le contenu de la feuille « Détail TVA » ;
  `Declaration.Orchestration.Tests` si des tests y verrouillent aussi ce bloc).
- **Preuve réelle obligatoire** : régénère un `.xlsx` de contrôle contre `GR_EMA_DISTRIBUTION` (même
  harness que TASK-180/181/182/183) montrant l'absence du bloc « Contrôle d'équilibre » et la nouvelle
  colonne Domaine Activité renseignée sur des données réelles.
- Build (`dotnet build`) doit passer, 0 erreur, avant d'écrire un VERIFY. Rejoue au minimum
  `Declaration.Export.Excel.Tests`, `Declaration.Core.Tests`, `Declaration.Orchestration.Tests`.
- Committe en un seul commit pour cette TASK, message clair, jamais `--no-verify`, **ne jamais
  pousser (`git push`)** — le commit reste local pour revue. Ne stage que les fichiers de ton
  périmètre (jamais `git add -A`).
- Respecte les règles universelles de `ARCHITECTURE.md` §5 (pas de dette technique silencieuse —
  documente tout compromis dans le VERIFY).
- Si un point est réellement ambigu (ex. libellé exact des valeurs de domaine à afficher, position de
  la nouvelle colonne dans le tableau) : **arrête-toi sur ce point précis, documente-le clairement dans
  le VERIFY** plutôt que d'inventer une réponse, et continue le reste de la TASK.

## À la fin

Laisse TASK-184 dans `VERIFY/` (ne la déplace pas toi-même vers `DONE_DETAIL/` — c'est le rôle de
l'architecte de la reviewer et de faire la clôture). Indique dans un résumé final l'état exact
d'avancement.
