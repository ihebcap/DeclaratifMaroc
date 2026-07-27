# VERIFY — TASK-182

Date: 2026-07-24
Agent: Worker (Claude Code)

## Périmètre livré

Changement de texte pur, strictement dans `CreerFeuilleDetailTva` (export de **contrôle**,
`Declaration.Export.Excel/Exporter.cs`), conforme à la décision PO (ne pas recalculer, juste
renommer) :

- `"Total Montant Affecté"` → `"Total HT"` (ligne ~311).
- `"Résidu Non TVA"` → `"Écart HT − TTC (= −Total TVA)"` (ligne ~317).
- `"Total Déclaré TTC"` : **inchangé** (déjà correct, non touché).
- Aucune valeur, aucun calcul, aucune autre ligne modifiés. Le signe de `ResiduNonTva` reste
  strictement inchangé (toujours négatif dans ce jeu de données réel, jamais inversé).

`CreerFeuilleRecap` (export de **dépôt**) **n'a pas été touchée** — vérifié par lecture directe
du diff (voir § Diff résumé) : les 2 chaînes identiques (`"Total Montant Affecté"`/`"Résidu Non
TVA"`) existent aussi dans cette méthode (lignes 117/123) et sont restées strictement intactes.

## Fichiers modifiés

- `Declaration.Export.Excel/Exporter.cs` — 2 chaînes de libellé renommées dans
  `CreerFeuilleDetailTva` uniquement (+ 1 commentaire explicatif ajouté juste au-dessus, aucun
  code exécutable ajouté). Aucun autre fichier touché (aucun test n'a eu besoin d'être modifié,
  voir ci-dessous).

## Diff résumé

```diff
@@ CreerFeuilleDetailTva (export de contrôle) @@
-            ws.Cell(row, 1).Value = "Total Montant Affecté";
+            ws.Cell(row, 1).Value = "Total HT";
             ws.Cell(row, 2).Value = modele.ControleEquilibre.TotalMontantAffecte;
             row++;
             ws.Cell(row, 1).Value = "Total Déclaré TTC";
             ws.Cell(row, 2).Value = modele.ControleEquilibre.TotalDeclareTtc;
             row++;
-            ws.Cell(row, 1).Value = "Résidu Non TVA";
+            // TASK-182 : libellé corrigé (chemin contrôle uniquement) — commentaire expliquant
+            // pourquoi ResiduNonTva n'est ici qu'un écart HT-TTC, pas un vrai résidu réglé.
+            ws.Cell(row, 1).Value = "Écart HT − TTC (= −Total TVA)";
             ws.Cell(row, 2).Value = modele.ControleEquilibre.ResiduNonTva;
             row++;
```

`CreerFeuilleRecap` (lignes 113-131, export de dépôt) : **aucune ligne modifiée**, confirmé par
`git diff` qui ne montre aucun hunk sur cette méthode.

Aucun changement dans `Declaration.Export.Excel.Tests/ExporterTests.cs` ni
`Declaration.Orchestration.Tests/Task160ExportControleTests.cs` — recherche exhaustive des deux
fichiers (`grep` sur `"Total Montant Affecté"`, `"Résidu Non TVA"`, `Cell(1[5-9],`) : aucune
assertion n'y référence les libellés par valeur exacte de chaîne, uniquement les **valeurs**
numériques (`TotalMontantAffecte`, `TotalDeclareTtc`) et les libellés de blocs non concernés
(« Totaux par taux — Collecté », etc.). Aucun test à adapter.

## Build

- Status : OK — `dotnet build DeclarationTVA.slnx` → **0 erreur** (24 avertissements
  préexistants, aucun nouveau introduit par ce lot).
- **Réserve identique à TASK-180/162 (même environnement)** : un process `Declaration.API.exe`
  (PID 60376, instance console de dev) tournait déjà et verrouillait les DLL de sortie —
  contournement identique : `-p:BaseOutputPath=D:\tmp\build_out_task182\` (sortie redirigée hors
  du dossier verrouillé) → 0 erreur, tous les projets compilent sans écart de code. Artefact
  d'environnement, pas un défaut introduit par ce lot.

## Tests

- `Declaration.Export.Excel.Tests` : **3/3** (aucune assertion à adapter, cf. § Diff résumé).
- `Declaration.Orchestration.Tests` : **182/182** (inchangé).
- Suite complète (`dotnet test DeclarationTVA.slnx`, sortie redirigée) rejouée pour détecter une
  éventuelle régression ailleurs :
  - `Declaration.Core.Tests` : 52/52.
  - `Declaration.Export.Xml.Tests` : 13/13.
  - `Declaration.Export.Excel.Tests` : 3/3.
  - `Declaration.Orchestration.Tests` : 182/182.
  - `Declaration.Selection.Tests` : 58/59 — **1 échec préexistant, sans rapport** :
    `IntegrationRegressionTests.TestRegression_NouveauSurensembleIncludAncienTask008_Task050`,
    `SqlException` (« Échec de l'ouverture de session de l'utilisateur 'IHEB-PC\ihebc' ») — ce
    test se connecte avec l'authentification Windows de la session courante, pas avec
    `connections.json` (sa/1234) ; même échec déjà documenté comme préexistant et sans rapport
    dans `DONE_DETAIL/TASK-137` (VERIFY). Confirmé ici de nouveau préexistant : rejoué sur le code
    non modifié (`git stash`) → échec identique.
  - `Declaration.Controle.Tests` : 1/2 — **échec préexistant, sans rapport** :
    `ComparateurTests.GenererRapportVerification`, `System.Exception: Déclaration GRFN 66
    introuvable.` (test dépendant d'une donnée fixe absente de la base actuelle). **Vérifié
    empiriquement préexistant** : rejoué sur le code non modifié (`git stash` avant, `git stash
    pop` après) → échec identique, aucun rapport avec `Exporter.cs`.

## Preuve réelle (base `GR_EMA_DISTRIBUTION`, déclaration `TVA1-2026-05`)

Export de contrôle régénéré via un petit harness console (hors dépôt, jamais commité, supprimé en
fin de session — même méthode que TASK-180/162) : `ConstruireModeleControleAsync` +
`GenererExcelControleAsync` appelés directement contre la vraie base (`Server=DESKTOP-5BFKKEP`,
identifiants `connections.json`), aucune donnée fabriquée. `Id` de `TVA1-2026-05` résolu par
`sqlcmd` (`SELECT Id FROM DM_ENTTVA WHERE Numero='TVA1-2026-05'` →
`6e6c0b95-7e65-4069-95fe-9354555021cc`).

Résultats observés (735 lignes `Integree|Proposee`, identique au volume TASK-180) :
- `ControleEquilibre.TotalMontantAffecte` = **1 817 326,57** (= Σ HT, confirmé par calcul via le
  modèle).
- `ControleEquilibre.TotalDeclareTtc` = **2 094 301,93**.
- `ControleEquilibre.ResiduNonTva` = **−276 975,36** — vérifié égal à `TotalMontantAffecte −
  TotalDeclareTtc` (1 817 326,57 − 2 094 301,93 = −276 975,36) et à **−Total TVA**
  (2 094 301,93 − 1 817 326,57 = 276 975,36 = Total TVA) : confirme empiriquement, sur données
  réelles, l'affirmation de la TASK selon laquelle cette valeur n'est rien d'autre que −ΣTVA.
- Relecture programmatique (ClosedXML) du `.xlsx` généré, feuille « Détail TVA », bloc « Contrôle
  d'équilibre » (lignes 23-26) :
  ```
  Row 23: A="Contrôle d'équilibre"
  Row 24: A="Total HT"                              B=1817326,57
  Row 25: A="Total Déclaré TTC"                      B=2094301,93
  Row 26: A="Écart HT − TTC (= −Total TVA)"          B=-276975,36
  ```
  Les 2 nouveaux libellés apparaissent bien à la place des anciens, le 3ᵉ libellé (« Total Déclaré
  TTC ») est inchangé, et les 3 valeurs sont strictement identiques à celles produites par le code
  de calcul (aucune valeur altérée par le renommage).

Fichier joint : `VERIFY/TASK-182-exemple-export-controle.xlsx`.

## Validation checklist

- [x] Build OK (solution complète, réserve process signalée ci-dessus, sans impact sur le
      résultat).
- [x] Tests passés (240/242 sur la suite complète ; les 2 échecs sont préexistants et sans
      rapport, ré-confirmés par `git stash` sur le code non modifié — voir § Tests). Les 2 suites
      explicitement demandées par la TASK (`Declaration.Export.Excel.Tests`,
      `Declaration.Orchestration.Tests`) sont **100 % vertes** (3/3 + 182/182).
- [x] Feuille « Détail TVA » : libellés « Total HT » et « Écart HT − TTC (= −Total TVA) »
      affichés à la place des anciens, valeurs strictement inchangées — vérifié sur données
      réelles (voir § Preuve réelle).
- [x] Feuille « Récap » (export de dépôt) : non modifiée — confirmé par lecture du diff (aucun
      hunk sur `CreerFeuilleRecap`).
- [x] Aucun bypass sécurité — aucune couche service contournée, aucun SQL inline ajouté dans le
      code livré (le harness de preuve, hors dépôt, réutilise `DeclarationRepository`/
      `DbConnectionFactory` existants tels quels, aucune requête SQL écrite à la main dans le
      code du dépôt).
- [x] Aucune dette technique silencieuse — le seul commentaire ajouté documente explicitement
      pourquoi le libellé change dans ce chemin précis, sans toucher au calcul ni au chemin de
      dépôt.

## Impacts détectés

- Aucun contrat public (DTO API/JSON) modifié — `ModeleControle`/`ControleEquilibre` restent des
  types internes, seul le texte écrit dans le classeur Excel binaire change.
- Aucun impact sur `ConstruireModeleControleAsync`/`ConstructeurDeclaration.cs` : ni l'un ni
  l'autre n'a été ouvert en écriture, uniquement lu pour confirmer la sémantique documentée dans
  la TASK (vérification, pas modification).
- Aucun impact front : aucune route/DTO consommée par le front n'a changé (le `.xlsx` de contrôle
  est un fichier binaire téléchargé tel quel).

## Reste à valider (honnête, non déguisé)

1. **Rendu visuel dans Excel lui-même** : comme pour TASK-180, le `.xlsx` de preuve a été vérifié
   programmatiquement (ClosedXML : lecture directe des cellules) mais **pas ouvert visuellement
   dans Microsoft Excel** par ce worker (pas d'accès interactif à Excel dans cet environnement).
   Le caractère « − » (moins typographique, U+2212) utilisé dans le libellé et son alternative
   ASCII n'ont pas été comparés visuellement à l'affichage réel Excel (risque cosmétique jugé nul :
   ClosedXML confirme la chaîne exacte écrite en cellule).
2. **Libellé du 2ᵉ renommage** : la TASK proposait « Écart HT − TTC (= −Total TVA) » « ou libellé
   équivalent explicite sur le signe » — ce libellé exact a été retenu tel quel (aucune
   alternative testée), aucun retour PO obtenu sur la formulation avant ce VERIFY. Si le PO
   préfère une autre formulation (ex. sans le rappel « = −Total TVA » entre parenthèses), un
   second renommage resterait un changement de texte pur, aussi simple que celui-ci.
3. **Process `Declaration.API.exe` (PID 60376)** : toujours actif à la fin de cette session (même
   réserve d'environnement que TASK-180/162) — sans impact sur ce VERIFY (build/tests validés via
   sortie redirigée).

## Notes worker

- Le harness de preuve (console .NET référençant `Declaration.Application`/
  `Declaration.Infrastructure`/`Declaration.Export.Excel`/`Declaration.Core`/`Declaration.Selection`
  en `ProjectReference`, connexion directe à `GR_EMA_DISTRIBUTION` via les identifiants de
  `connections.json`) a nécessité d'enregistrer le même `SqlMapper.AddTypeHandler` Guid↔NVARCHAR(36)
  que `Declaration.API/Program.cs:17` (`Declaration.API/GuidTypeHandler.cs`) — sans lui,
  `DeclarationRepository.GetByIdAsync` lève un `InvalidCastException` (la colonne `DM_ENTTVA.Id`
  est stockée en `NVARCHAR(36)`, pas en `uniqueidentifier` natif). Fait pour information : ce
  détail concerne uniquement le harness de preuve (hors dépôt, supprimé après usage), aucun code
  livré n'y touche.
- Le renommage confirme, sur données réelles et pas seulement par lecture de code, l'exactitude
  du diagnostic architecte de la TASK : `TotalMontantAffecte` (1 817 326,57) est bien un Total HT
  brut, et `ResiduNonTva` (−276 975,36) correspond exactement à −ΣTVA (276 975,36) sur ce jeu de
  données réel.
