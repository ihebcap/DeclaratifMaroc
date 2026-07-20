# TASK-140 Verify — Éliminer les règlements « déjà déclarés » de l'écran ① Sélection + numéro de déclaration sur l'écran Rapprochement

> Implémentation réalisée par l'agent en mode **worker exceptionnel** (à la demande du PO).
> Vérification : build back + front, suite de tests (137/137), et revue point par point de la
> checklist VALIDATION au niveau code. Une capture sur données réelles n'a pas été rejouée dans
> cette passe (voir « Limite assumée »).

## Fichiers modifiés

| Fichier | Nature du changement |
| --- | --- |
| `Declaration.Application/Entities/ReglementRapprochement.cs` | +`DtId`, +`DtIdMin` (bruts SQL), +`NumeroDeclaration` (résolu applicativement). Additif. |
| `Declaration.Infrastructure/Repositories/DeclarationRepository.cs` | Sous-requête agrégée : +`MAX(AF.DT_Id) AS DtId`, +`MIN(AF.DT_Id) AS DtIdMin`. Projection liste : +`A.DtId`, +`A.DtIdMin`. Nouvelle résolution batchée `ResoudreNumeroDeclarationAsync` (2ᵉ appel sur `PersistenceConnection`, fusion mémoire). |
| `Declaration.API/Dtos/ReglementRapprochementDto.cs` | +champ JSON `numeroDeclaration` (additif). |
| `declaration-tva-web/src/RapprochementInterrogation.tsx` | Colonne « Déclaré » : composant `DeclareBadge` (numéro si présent, repli Oui/Non). 3 points : largeur colonne, `renderCell`, panneau détail. |
| `declaration-tva-web/src/ReglementsSelection.tsx` | Filtrage front `items.filter(r => !r.declare)` à la sortie de `fetchAll`. Commentaire `statutDe` mis à jour (exception documentée). |

## Décision technique retenue

Option 1 du cadrage (recommandée) : **pas de JOIN SQL trois-parties**. `RT_AFFECTATION.DT_Id`
(base GRF) et `DM_ENTTVA` (base persistance) vivent sur deux connexions Dapper distinctes ; un JOIN
en dur dépendrait d'un nom de base fragile selon l'environnement. La requête GRF n'ajoute que
`MAX/MIN(AF.DT_Id)` ; le repository résout ensuite les `DT_Id` distincts de la page en numéros via
un **seul** appel batché `SELECT DT_Id, Numero FROM DM_ENTTVA WHERE DT_Id IN @dtIds` sur
`PersistenceConnection`, puis fusionne en mémoire. Coût : 1 requête supplémentaire par page (jamais
N+1).

## Checklist VALIDATION — revue

- [x] **Build OK, tests passés** — `dotnet build Declaration.API` : 0 erreur (10 warnings
  préexistants, non liés). `npm run build` (`tsc -b && vite build`) : 0 erreur. `dotnet test
  Declaration.Orchestration.Tests` : **137/137 réussis**.

- [x] **Étape ① : aucune ligne `declare === true`, ni dans le compteur total, quel que soit le
  filtre** — le filtrage est appliqué **en amont**, sur `items` complet à la sortie de la boucle de
  pagination serveur, AVANT `setAllData`. `allData` ne contient donc jamais de ligne déclarée ; tous
  les dérivés (compteur `data.length`/`allData.length`, filtre État, options de filtre, sélection
  par défaut, virtualisation) opèrent sur ce jeu déjà purgé. Le filtre est posé après la boucle
  (jamais pendant) : la condition d'arrêt `items.length >= totalCount` reste comparée au `totalCount`
  serveur non filtré — pas de rupture de pagination.

- [x] **Étape ① : les autres statuts `bloque` (non affecté, « Autre (impayé) ») restent affichés** —
  le filtre porte STRICTEMENT sur `r.declare`. `statutDe` conserve ses branches
  `nbFacturesAffectees === 0` et `origine startsWith 'Autre ('` ; ces lignes ne sont pas touchées.

- [x] **Écran Rapprochement : colonne « Déclaré » affiche le numéro (ex. TVA1-2026-02) pour un
  règlement verrouillé, « — »/Non sinon** — `DeclareBadge` : `numeroDeclaration` présent ⇒ badge
  indigo avec le numéro (ellipsis + `title` si trop long) ; sinon repli `OuiNonBadge` (Oui si
  `declare` sans DT_Id résolu — cas sélection dans une autre déclaration EnCours ; Non si non
  déclaré). Appliqué aux 2 rendus (grille + panneau détail).

- [x] **Le filtre `declare` (Oui/Non) de l'écran Rapprochement continue de fonctionner** — inchangé :
  `RapprochementFilter.Declare` → `@hasDeclare`/`@declares` sur `A.NbDeclare` côté serveur. Aucune
  ligne du chemin filtre/COUNT n'a été modifiée ; seules des colonnes additives ont été ajoutées à la
  sous-requête agrégée (ignorées par `COUNT(*)`).

- [x] **Plusieurs déclarations différentes : chaque règlement affiche SON numéro** — la résolution
  mappe `DT_Id → Numero` par un dictionnaire ; chaque ligne lit `map[r.DtId]`, jamais une valeur
  partagée. Un jeu multi-déclarations produit donc le bon numéro par ligne.

- [x] **Cas `DT_Id` incohérent entre affectations : pas de valeur inventée, signalement explicite** —
  `MIN` et `MAX(AF.DT_Id)` sont tous deux remontés. Si `DtIdMin != DtId`, `NumeroDeclaration` porte
  `⚠ {numMin} / {numMax} (incohérent)` (repli sur le DT_Id brut si un numéro manque) — les deux sont
  affichés, aucun choix arbitraire silencieux. Ne devrait pas arriver (verrou atomique TASK-028).

## Respect des règles d'architecture

- **Endpoint partagé intouché côté contrat/serveur** : `GET /api/rapprochement` n'ajoute qu'un champ
  JSON `numeroDeclaration` (additif) ; **aucun filtrage serveur** des lignes déclarées. Le masquage
  « déjà déclaré » est **strictement front**, scopé à `ReglementsSelection.tsx` — l'écran
  Rapprochement continue de voir ces lignes (avec le numéro en plus).
- **« Aucune ligne silencieuse »** : l'exception de masquage en ① est **ciblée** (`row.declare` seul),
  **documentée** (commentaires `fetchAll` + `statutDe`), et **compensée** par un enrichissement de
  traçabilité ailleurs (numéro exact de la déclaration verrou, plus qu'un booléen). Principe respecté
  par déplacement de l'information, pas par suppression.
- **Lecture seule** : aucun INSERT/UPDATE/DELETE ajouté. `DM_ENTTVA` et `RT_AFFECTATION.DT_Id` sont
  uniquement LUS.

## Build

- `dotnet build Declaration.API/Declaration.API.csproj` : 0 erreur.
- `npm run build` (`tsc -b && vite build`) : 0 erreur.
- `dotnet test Declaration.Orchestration.Tests` : 137 réussis, 0 échec.

## Limite assumée

Cette passe couvre le build, la suite de tests et une revue code point par point. Elle **n'inclut
pas** une reproduction UI instrumentée sur base réelle (`GR_EMA_DISTRIBUTION`) avec captures
avant/après, contrairement à d'autres VERIFY récents (ex. TASK-125). Les items observables
(disparition effective en ①, rendu du numéro sur l'écran Rapprochement) sont vérifiés au niveau du
code et du contrat, pas par capture d'écran sur données réelles. À demander explicitement si une
preuve visuelle sur données réelles est requise avant clôture.
