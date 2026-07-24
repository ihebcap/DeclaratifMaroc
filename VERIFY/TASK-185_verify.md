# TASK-185 Verify — Correction TASK-184 : ligne Total par bloc au lieu de la colonne Domaine Activité

> Implémentation réalisée en tant que worker. Build solution complète + les 3 suites de tests
> explicitement demandées par la TASK sont vertes. Preuve réelle régénérée contre
> `GR_EMA_DISTRIBUTION` (`TVA1-2026-05`, 735 lignes, même déclaration que TASK-180 à 184) — les 8
> totaux par (Taux, Collecte) sont strictement identiques à ceux déjà publiés dans
> `DONE_DETAIL/TASK-184_verify.md` (non-régression confirmée par comparaison directe, pas seulement
> recalculée). Aucun point resté réellement ambigu.

## Périmètre livré

Feuille « Détail TVA » (export de contrôle, `CreerFeuilleDetailTva`) uniquement :

1. Colonne « Domaine Activité » retirée des tableaux « Totaux par taux — Collecté »/« — Déductible »
   — retour à 4 colonnes (Taux/Total HT/Total TVA/Total TTC), comme avant TASK-184.
2. Ligne « Total Collecté » ajoutée en pied du tableau Collecté, « Total Deductible » en pied du
   tableau Déductible — somme HT/TVA/TTC des taux du bloc, calculée dans la même boucle que l'écriture
   des lignes (pas de second parcours).
3. `RecapParTaux.Domaine` (Model.cs) et sa résolution (`ResoudreDomaineActiviteRecap`, chargement du
   référentiel `GetReferentielCodesActiviteAsync()` dans `ConstruireModeleControleAsync`) supprimés —
   plus aucun consommateur une fois la colonne retirée.
4. `CreerFeuilleRecap` (feuille « Récap », export de dépôt) et « Totaux par code activité » : non
   touchés, strictement inchangés.

## Fichiers modifiés

- `Declaration.Export.Excel/Exporter.cs` — `EcrireBlocRecapParTaux` : paramètre
  `bool afficherDomaineActivite` remplacé par `string? libelleTotal = null` (défaut `null` = pas de
  ligne de total, comportement de `CreerFeuilleRecap` inchangé) ; colonne Domaine retirée ; ligne de
  total ajoutée en fin de bloc si `libelleTotal` renseigné. `CreerFeuilleDetailTva` : les deux appels
  passent désormais `libelleTotal: "Total Collecté"` / `"Total Deductible"` au lieu de
  `afficherDomaineActivite: true`.
- `Declaration.Core/Model.cs` — propriété `RecapParTaux.Domaine` supprimée.
- `Declaration.Application/Services/DeclarationWorkflowService.cs` — dans
  `ConstruireModeleControleAsync` : suppression du chargement `GetReferentielCodesActiviteAsync()` /
  `domaineParCodeActivite`, suppression de la clé `Domaine` dans le `GroupBy`/`Select` de
  `RecapsParTaux`, suppression du `.ThenBy(r => r.Domaine)` (le tri reste `OrderByDescending(r =>
  r.Taux)` seul, identique à avant TASK-184) ; méthode `ResoudreDomaineActiviteRecap` supprimée
  entièrement. `GetReferentielCodesActiviteAsync` (méthode publique du service, wrapper vers le
  repository, utilisée par `GET .../codes-activite` côté API) **non touchée** — toujours un
  consommateur réel hors de ce chemin.
- `Declaration.Export.Excel.Tests/ExporterTests.cs` — fixture `GetFixtureControle` : `Domaine` retiré
  des deux `RecapParTaux` de test. Assertions de `ExporterExcelControle_Genere3FeuillesConformes` :
  colonne Domaine retirée, lignes déplacées en conséquence (blocs "Totaux par taux" décalés de 2
  lignes chacun par l'ajout de la ligne Total, blocs "Totaux par code activité" décalés de 2 lignes),
  nouvelles assertions sur les valeurs des lignes « Total Collecté »/« Total Deductible ».
- `Declaration.Orchestration.Tests` : **aucun changement** — vérifié qu'aucun fake n'assigne
  `RecapParTaux.Domaine` (les nombreuses occurrences de `.Domaine` dans ce projet portent sur
  `LigneCandidate.Domaine`, un concept différent — "Decaissement"/"Encaissement" de la ligne, sans
  rapport avec le domaine Achats/Ventes du code activité retiré ici).

## Checklist

- [x] Build solution complète (`dotnet build DeclarationTVA.slnx`) → 0 erreur, 24 avertissements —
      même compte que la baseline TASK-184 (aucun nouvel avertissement introduit).
- [x] `Declaration.Export.Excel.Tests` → 3/3.
- [x] `Declaration.Core.Tests` → 52/52.
- [x] `Declaration.Orchestration.Tests` → 182/182.
- [x] Tableaux « Totaux par taux » : 4 colonnes (Taux/Total HT/Total TVA/Total TTC), plus de colonne
      Domaine Activité — vérifié sur le `.xlsx` réel.
- [x] Ligne « Total Collecté » en pied du bloc Collecté, « Total Deductible » en pied du bloc
      Déductible — valeur égale à la somme des taux du bloc, vérifiée par calcul indépendant sur les
      vraies données (voir § Preuve réelle).
- [x] Feuille « Récap » (export de dépôt) inchangée — `CreerFeuilleRecap` non modifié dans son corps,
      appelle `EcrireBlocRecapParTaux` sans `libelleTotal` (valeur par défaut `null`), test
      `ExporterExcel_DevraitGenererFichierConforme` rejoué et vert sans modification.
- [x] « Totaux par code activité » non touché — mêmes titres, mêmes colonnes, pas de ligne de total.
- [x] `RecapParTaux.Domaine` et `ResoudreDomaineActiviteRecap` supprimés, aucun code mort restant
      (vérifié par grep sur tout le dépôt : plus aucune référence à `RecapParTaux.Domaine`).
- [x] Aucun bypass sécurité, aucune couche service contournée.
- [x] Aucune dette technique silencieuse.

## Preuve réelle (base `GR_EMA_DISTRIBUTION`, déclaration `TVA1-2026-05`)

Harness console (`scratch/TestTask185/`, hors dépôt car `scratch/` est gitignoré, supprimé après
usage — même méthode que TASK-180/181/182/183/184) : `ConstruireModeleControleAsync` +
`GenererExcelControleAsync` appelés directement contre la vraie base (`Server=DESKTOP-5BFKKEP`), même
`Id` de `TVA1-2026-05` que les VERIFY précédents (`6e6c0b95-7e65-4069-95fe-9354555021cc`). 735 lignes
`Integree|Proposee`, identique au volume TASK-180/182/184.

**Invariant de non-régression** — comparaison directe (pas seulement recalculée dans le harness) des
8 totaux par `(Taux, Collecte)` avec les valeurs déjà publiées dans `DONE_DETAIL/TASK-184_verify.md` :

```
Taux=20 Collecte=True  HT=742456,47 TVA=148491,23 TTC=890947,70   (identique TASK-184)
Taux=20 Collecte=False HT=491494,32 TVA=98298,82  TTC=589793,14   (identique TASK-184)
Taux=10 Collecte=True  HT=190373,50 TVA=19037,46  TTC=209410,96   (identique TASK-184)
Taux=10 Collecte=False HT=108624,39 TVA=10862,43  TTC=119486,82   (identique TASK-184)
Taux=9  Collecte=True  HT=2883,33   TVA=259,50    TTC=3142,83     (identique TASK-184)
Taux=9  Collecte=False HT=288,00    TVA=25,92     TTC=313,92      (identique TASK-184)
Taux=0  Collecte=True  HT=157020,18 TVA=0,00      TTC=157020,18   (identique TASK-184)
Taux=0  Collecte=False HT=124186,38 TVA=0,00      TTC=124186,38   (identique TASK-184)
```

Relecture programmatique (ClosedXML) du `.xlsx` réellement écrit sur disque, feuille « Détail TVA » :

```
Row  1: A="Totaux par taux — Collecté"
Row  2: A="Taux" B="Total HT" C="Total TVA" D="Total TTC"
Row  3: A="20"   B="742456,47" C="148491,23" D="890947,7"
Row  4: A="10"   B="190373,5"  C="19037,46"  D="209410,96"
Row  5: A="9"    B="2883,33"   C="259,5"     D="3142,83"
Row  6: A="0"    B="157020,18" C="0"         D="157020,18"
Row  7: A="Total Collecté" B="1092733,48" C="167788,19" D="1260521,67"
Row  9: A="Totaux par taux — Déductible"
Row 10: A="Taux" B="Total HT" C="Total TVA" D="Total TTC"
Row 11: A="20"   B="491494,32" C="98298,82" D="589793,14"
Row 12: A="10"   B="108624,39" C="10862,43" D="119486,82"
Row 13: A="9"    B="288"       C="25,92"    D="313,92"
Row 14: A="0"    B="124186,38" C="0"        D="124186,38"
Row 15: A="Total Deductible" B="724593,09" C="109187,17" D="833780,26"
Row 17: A="Totaux par code activité — Collecté"   (bloc inchangé, pas de ligne Total ici)
Row 22: A="Totaux par code activité — Déductible" (bloc inchangé, pas de ligne Total ici)
```

Vérification indépendante des sommes : Total Collecté HT = 742456,47+190373,50+2883,33+157020,18 =
**1092733,48** (égal à la cellule B7 ci-dessus) ; Total Deductible HT =
491494,32+108624,39+288,00+124186,38 = **724593,09** (égal à la cellule B15) — mêmes calculs pour TVA
et TTC, tous exacts.

Aucune colonne « Domaine Activité » ni ligne « Contrôle d'équilibre » retrouvées dans les 30 premières
lignes de la feuille (recherche programmatique).

Fichier joint : `VERIFY/TASK-185-exemple-export-controle.xlsx`.

## Impacts détectés

- Aucun contrat public (DTO API/JSON) modifié — `RecapParTaux` reste un type interne consommé
  uniquement par l'export Excel de contrôle ; aucune route front ne l'expose directement.
- `EcrireBlocRecapParTaux` change la nature de son paramètre optionnel (`bool` → `string?`) mais reste
  à un seul appelant modifié en dehors de `CreerFeuilleDetailTva` (`CreerFeuilleRecap`, qui ne passe
  toujours pas ce paramètre) — signature toujours rétrocompatible pour cet appelant.
- `GetReferentielCodesActiviteAsync()` (repository + wrapper service) conservée intacte : toujours
  appelée par `DeclarationsController.cs:214` (`GET .../codes-activite`), hors périmètre de cette
  TASK.
- Front : aucun changement, portée strictement backend comme demandé par la TASK.

## Reste à valider (honnête, non déguisé)

1. **Rendu visuel dans Microsoft Excel** : comme pour TASK-180 à 184, le `.xlsx` de preuve a été
   vérifié programmatiquement (ClosedXML) mais pas ouvert visuellement dans Excel par ce worker (pas
   d'accès interactif à Excel dans cet environnement).
2. **Libellé exact « Total Deductible »** : repris tel quel du texte fourni par le PO dans la TASK
   (sans accent, cohérent avec la capture d'écran citée) — à confirmer si le PO préfère
   « Total Déductible » avec accent ; changement cosmétique pur si besoin.
3. **Process `Declaration.API.exe`** : réserve d'environnement identique à TASK-180/181/182/183/184 —
   build/tests réalisés via sortie redirigée (`-p:BaseOutputPath=D:/tmp/build_out_task185/`), sans
   impact sur le résultat.

## Notes worker

- Le harness de preuve (`scratch/TestTask185/`, hors dépôt car `scratch/` est gitignoré,
  `ProjectReference` vers `Declaration.Application`/`Declaration.Infrastructure`/
  `Declaration.Export.Excel`, même `SqlMapper.AddTypeHandler` Guid↔NVARCHAR(36) que
  TASK-150/180/182/184) a été supprimé après usage ; seul le `.xlsx` produit est conservé dans
  `VERIFY/`.
- Choix de conception : réutilisation de `EcrireBlocRecapParTaux` avec un paramètre `libelleTotal`
  plutôt que dupliquer la méthode ou ajouter un second parcours des `recaps` — la somme est accumulée
  dans la même boucle qui écrit déjà chaque ligne, aucun coût supplémentaire.
