# TASK-177 Verify — Une incohérence validée par le PO reste signalée comme anomalie BLOQUANTE au contrôle ④

> Implémenté par un agent worker. Travail réalisé en parallèle des agents TASK-175 (déjà commité,
> `7a85f4a`) et TASK-176 (en cours, mêmes fichiers `DeclarationsController.cs`/
> `DeclarationWorkflowService.cs`) — voir § « Coordination multi-agents » pour le détail de la
> précaution prise afin de ne pas écraser le travail non commité de TASK-176.

## Périmètre livré

`GetCheckupAsync` (`DeclarationWorkflowService.cs`) exclut désormais du déclenchement de l'alerte
bloquante `FACTURE_NON_VENTILEE` (niveau `Error`) toute ligne où `IncoherenceValidee == true` — même
garde que celle déjà en place dans `RevaliderLignesFigeesAsync` (`if (l.IncoherenceValidee) continue;`).

Avant :
```csharp
foreach (var l in integrees.Where(l => !string.IsNullOrEmpty(l.MotifRejet)))
```
Après :
```csharp
foreach (var l in integrees.Where(l => !string.IsNullOrEmpty(l.MotifRejet) && !l.IncoherenceValidee))
```

Aucun changement de valeur (`HT`, `TTC`, `MotifRejet`, `Etat`) : uniquement la condition de
déclenchement de l'alerte. La revalidation `RevaliderLignesFigeesAsync` en fin de `GetCheckupAsync`
(alertes Warning ②) n'a pas été touchée, conformément aux garde-fous de la TASK.

## Recherche des autres emplacements `MotifRejet` (garde-fou explicite de la TASK)

Recherche exhaustive de `MotifRejet` sur `DeclarationWorkflowService.cs` (seul fichier qui le
référence — `OrchestrateurDeclaration.cs` n'existe pas sous ce nom dans le repo actuel, c'est en
réalité `DeclarationWorkflowService.cs` qui porte cette logique ; aucun fichier
`OrchestrateurDeclaration.cs` distinct n'a été trouvé). Résultat, 3 autres emplacements identifiés et
analysés :

1. **`LIGNE_EXCLUE` (Info, ~ligne 1040-1049)** — récapitule les lignes `Etat == Exclue` avec leur
   `MotifRejet`, sans tester `IncoherenceValidee`. **Non corrigé, volontairement** : niveau `Info`
   (non bloquant), et sémantiquement différent — une ligne `Exclue` n'a jamais été valorisée/déclarée
   (mémoire `grf-exclusion-ligne-non-declaration`) ; l'alerte documente *pourquoi* elle a été exclue,
   ce qui reste vrai même si une incohérence a par ailleurs été validée sur ce même `EC_Id` par
   ailleurs. Écarté du périmètre de correction car non bloquant et hors objectif (« exclure … de
   l'alerte **bloquante** »).
2. **`LIGNE_FIGEE_A_REVERIFIER` sur `lignesExcluesIncoherence` (Warning, ~ligne 499-515, dans
   `RevaliderLignesFigeesAsync`)** — ne teste pas `IncoherenceValidee` non plus, mais **ne lit pas
   `MotifRejet`** (utilise `ecIdsEnErreur`, un mécanisme de cache distinct) : hors du périmètre strict
   de la recherche demandée (« alerte à partir de `MotifRejet` »). Niveau `Warning` (non bloquant) et
   comportement volontairement distinct documenté par le commentaire TASK-082 déjà en place (parité
   avec `LIGNE_EXCLUE`, pas avec le contrôle bloquant). Non corrigé.
3. **Boucle `selectedCandidates`/`REGLEMENT_EXCLU` (Warning, ~ligne 1071-1100)** — utilise l'enum
   `MotifRejet` (type, pas le champ `l.MotifRejet` de la ligne persistée) sur des *candidats de
   sélection* non encore figés, avant toute notion d'`IncoherenceValidee` (qui n'existe que sur la
   ligne persistée en base). Non applicable, non corrigé.

**Seul le point 1116 (bloc `integrees`/`FACTURE_NON_VENTILEE`, `Error`) déclenchait effectivement le
message cité mot pour mot par le PO** (« Ligne en anomalie de recalcul (facture …, règlement …) : … »)
sous le contrôle bloquant — confirmé par grep exhaustif du texte exact du message, présent nulle part
ailleurs dans le fichier.

## Fichiers modifiés

- `Declaration.Application/Services/DeclarationWorkflowService.cs` — condition de la boucle
  `FACTURE_NON_VENTILEE` dans `GetCheckupAsync` (+6 lignes : commentaire + garde). Aucune autre ligne
  touchée.
- `Declaration.Orchestration.Tests/Task102NumeroReglementAnomaliesFactureTests.cs` — 2 nouveaux tests
  ajoutés (voir § Tests), réutilisant la fixture existante `FakeDeclarationRepository` de ce fichier
  (déjà utilisée pour les tests `GetCheckupAsync_FactureNonVentilee_*`).

## Coordination multi-agents (précaution prise, à documenter — pas un contournement de règle)

Au moment de committer, `DeclarationWorkflowService.cs` portait aussi les modifications en cours
(non commitées) de l'agent TASK-176 (`ResynchroniserLignesBulkAsync`, `ResynchroBulkResultat`, etc.),
et `DeclarationsController.cs` référençait déjà cette méthode. Pour éviter de committer le travail
non terminé/non testé d'un autre agent sous mon message de commit TASK-177 :
1. Sauvegarde du fichier complet (mon correctif + TASK-176) dans un fichier temporaire.
2. `git checkout HEAD -- <fichier>` pour revenir à l'état commité (`7a85f4a`, TASK-175 seul).
3. Ré-application de **uniquement** mon correctif (une seule ligne modifiée + commentaire) sur cet
   état propre.
4. `git add` + `git commit` — le commit `89db91b` ne contient que le correctif TASK-177 et le fichier
   de test associé.
5. Restauration immédiate du fichier de travail (sauvegarde de l'étape 1) pour ne pas laisser
   `DeclarationsController.cs` cassé (référence à une méthode temporairement absente) pendant que
   l'agent TASK-176 continue son travail. Build revérifié OK après restauration.

Le commit `89db91b` est donc strictement scopé à TASK-177 ; le travail de TASK-176 reste non commité,
dans l'arbre de travail, intact.

## Vérification base réelle (DESKTOP-5BFKKEP, GR_EMA_DISTRIBUTION, dev — pas la prod client)

```sql
SELECT DeclarationId, NumeroFacture, NumeroRapprochement, Etat, MotifRejet, IncoherenceValidee,
       IncoherenceValideePar, IncoherenceValideeLe
FROM DM_LGTVA WHERE NumeroFacture IN ('FC2501717','FC2501667')
```
Résultat (déclaration `7cceb196-...`, `TVA1-2026-01`, `SocieteId=1`, `Statut=EnCours`) :

| NumeroFacture | NumeroRapprochement | Etat | IncoherenceValidee |
|---|---|---|---|
| FC2501717 | RF26040040 | 0 (Proposee) | **0** |
| FC2501667 | RF26030075 | 0 (Proposee) | **0** |

**Constat important, à remonter au PO/architecte** : sur cette base de dev
(`DESKTOP-5BFKKEP`/`GR_EMA_DISTRIBUTION`), les deux lignes citées par le PO ont bien `Etat = Proposee`
(donc dans le périmètre `integrees` visé par le correctif) et `MotifRejet` non vide (confirmé,
message « Incohérence Sage : Σ(HT net+TVA+Parafiscale)... »), **mais `IncoherenceValidee = 0`** —
c'est-à-dire **jamais validées sur cette base**. Cela ne contredit pas le correctif (qui est
correct et vérifié par ailleurs via les tests unitaires avec ces mêmes identifiants réels), mais
signifie que **cette base dev n'est probablement pas celle sur laquelle le PO a cliqué « Valider »**
— son clic a dû avoir lieu sur l'environnement client réel (production), distinct de ce dev. Décision
prise : **ne pas modifier ces lignes en base dev** (par précaution, cette base est en cours
d'utilisation concurrente par les agents TASK-175/176 pour leurs propres vérifications ; une écriture
manuelle risquerait d'interférer avec leur état). La vérification fonctionnelle du correctif a donc
été faite exclusivement par test unitaire isolé (voir § Tests), avec les mêmes valeurs `NumeroFacture`/
`NumeroRapprochement` réelles, ce qui couvre le comportement exact sans risque d'effet de bord sur la
base partagée.

## Tests

Ajoutés dans `Task102NumeroReglementAnomaliesFactureTests.cs` :
- `GetCheckupAsync_LigneIncoherenceValidee_NeGenerePasAlerteFactureNonVentilee` — ligne
  `NumeroFacture="FC2501717"`, `NumeroRapprochement="RF26040040"`, `MotifRejet` non vide,
  `IncoherenceValidee=true` → **aucune** alerte `FACTURE_NON_VENTILEE` générée.
- `GetCheckupAsync_LigneIncoherenceNonValidee_GenereToujoursAlerteFactureNonVentilee` — même cas mais
  `NumeroFacture="FC2501667"`, `IncoherenceValidee=false` → alerte `FACTURE_NON_VENTILEE` **toujours**
  générée (non-régression).

## Checklist

- [x] Build back (`dotnet build DeclarationTVA.slnx`) OK — 0 erreur (25 warnings préexistants, aucun
      nouveau).
- [x] `dotnet test Declaration.Orchestration.Tests` : 182/182 réussis (dont les 2 nouveaux tests
      TASK-177 et les tests préexistants TASK-078/082/102 sur les mêmes pièces `FC2501717`/`FC2501667`
      — aucune régression).
- [x] Nouveau test : ligne `MotifRejet` non vide + `IncoherenceValidee=true` → pas d'alerte bloquante ;
      ligne identique + `IncoherenceValidee=false` → alerte toujours générée.
- [x] Aucun changement de `HT`/`TTC`/`MotifRejet`/`Etat` — seule la condition de la boucle a été
      modifiée (diff vérifié : 1 ligne modifiée + 5 lignes de commentaire, rien d'autre).
- [x] Recherche des autres emplacements `MotifRejet` sans garde `IncoherenceValidee` effectuée
      (§ ci-dessus) — 3 trouvés, analysés, non corrigés avec justification documentée (non bloquants
      ou hors mécanisme `MotifRejet`).
- [x] Aucun bypass sécurité, aucun SQL inline ajouté (aucune requête SQL touchée par ce correctif).
- [ ] **Test réel base de test (valider une incohérence via écran ② → vérifier le vert sur écran ③)**
      — NON réalisé en conditions live via l'UI/API (pas d'écriture manuelle sur `IncoherenceValidee`
      en base partagée, cf. § Coordination). Couvert à la place par test unitaire équivalent (mêmes
      valeurs), qui exerce exactement la même condition côté service. Recommandation : à confirmer une
      fois en environnement isolé (pas la base dev partagée par TASK-175/176), ou après déploiement.
- [ ] **Confirmation PO sur les cas réels production** — non réalisable par un worker (accès
      uniquement à la base dev `DESKTOP-5BFKKEP`, pas à l'environnement client). De plus, constat ci-
      dessus : sur la base dev, `IncoherenceValidee` est à 0 pour ces deux pièces — si le même constat
      est vrai en production, le correctif seul ne suffira pas : le PO devra revalider ces deux lignes
      via l'écran ② une fois le correctif déployé (le correctif ne fabrique aucune validation, il ne
      fait que respecter celle déjà posée).

## Point ouvert — décision PO nécessaire (NON implémenté, volontairement)

Le dernier point du §Objectif de la TASK demande de trancher : faut-il une trace visible de la
validation à l'écran ③/④ (Info/Warning affichant qui/quand a validé), ou la traçabilité déjà présente
côté écran ② (`IncoherenceValideePar`/`Le`) suffit-elle ? **Ce point n'a pas été tranché ici** —
c'est une décision produit explicitement identifiée comme telle par la TASK elle-même. Le correctif
livré est le minimum demandé par l'Objectif (« ne plus être comptée comme anomalie bloquante ») :
la ligne validée disparaît simplement du contrôle bloquant ③/④ sans alerte de substitution. Aucune
UI de traçabilité n'a été ajoutée à l'écran ③/④ tant que le PO n'a pas tranché ce point.

## Impacts détectés

Aucun autre module identifié. Le changement est localisé à une seule condition de boucle dans
`GetCheckupAsync`, fonction déjà utilisée uniquement par l'écran ③/④ (Vérifier & Intégrer / pré-
clôture) via `DeclarationsController.GetCheckup`. Pas d'impact sur l'export XML/Excel, la valorisation,
ni le figeage.

## Verdict

Correctif minimal conforme à l'Objectif et aux Garde-fous de la TASK, build + tests verts. Deux points
de la checklist originale restent ouverts et honnêtement non cochés : la vérification live via
l'écran ② (remplacée par un test unitaire équivalent, par précaution vis-à-vis de la base dev
partagée avec les agents TASK-175/176) et la confirmation PO sur les cas de production (hors de portée
d'un worker). Le constat `IncoherenceValidee=0` en base dev pour les deux pièces citées est à signaler
au PO avant clôture de la TASK — il indique que ces lignes n'ont probablement pas été validées sur cet
environnement dev et devront l'être (à nouveau ou pour la première fois) après déploiement, sur
l'environnement où le contrôle doit passer au vert.
